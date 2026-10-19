using System.IO;
using System.IO.Compression;
using BiloraZIP.Services;
using ICSharpCode.SharpZipLib.Zip;
using SharpZipFile = ICSharpCode.SharpZipLib.Zip.ZipFile;

int passed = 0;
string root = Path.Combine(Path.GetTempPath(), "BiloraZIP-SelfTest-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    void Assert(bool condition, string label)
    {
        if (!condition) throw new Exception("FAILED: " + label);
        Console.WriteLine("PASS " + label);
        passed++;
    }

    // Regression: dragging a folder onto itself while navigating is a no-op.
    string navigationFolder = Path.Combine(root, "navigation");
    string childFolder = Path.Combine(navigationFolder, "child");
    Assert(FileDropSafety.IsSamePath(navigationFolder, Path.Combine(root, "navigation")),
        "Explorer drop guard recognizes a folder dropped onto itself");
    Assert(FileDropSafety.IsSamePath(navigationFolder + Path.DirectorySeparatorChar, navigationFolder),
        "Explorer drop guard normalizes trailing separators");
    Assert(FileDropSafety.IsInsideFolder(navigationFolder, childFolder),
        "Explorer drop guard detects recursive folder copies");
    Assert(!FileDropSafety.IsInsideFolder(navigationFolder, Path.Combine(root, "navigation-other")),
        "Explorer drop guard does not confuse sibling folder names");

    string fileA = Path.Combine(root, "a.txt");
    string fileB = Path.Combine(root, "b.txt");
    File.WriteAllText(fileA, "BiloraZIP AES-256 test A");
    File.WriteAllText(fileB, "BiloraZIP AES-256 test B");
    string archive = Path.Combine(root, "encrypted.zip");
    const string password = "test-password-2026";
    var writer = new EncryptedZipService();
    await writer.CreateAsync(archive, [fileA, fileB], password, CompressionLevel.Optimal, null, CancellationToken.None);
    Assert(File.Exists(archive), "AES ZIP was created");
    using (var zip = new SharpZipFile(File.OpenRead(archive)))
    {
        var entries = zip.Cast<ZipEntry>().Where(e => !e.IsDirectory).ToArray();
        Assert(entries.Length == 2 && entries.All(e => e.IsCrypted && e.AESKeySize == 256),
            "Every ZIP file is AES-256 encrypted");
    }

    var reader = new ArchiveReadService();
    Assert(reader.NeedsPassword(archive), "AES ZIP requires a password");
    Assert(reader.GetZipEncryptionCounts(archive) == (2, 0), "No plaintext entries in AES ZIP");
    await reader.VerifyPasswordAsync(archive, password, CancellationToken.None);
    Assert(true, "Correct ZIP password verified");
    bool wrongRejected = false;
    try { await reader.VerifyPasswordAsync(archive, "incorrect", CancellationToken.None); }
    catch { wrongRejected = true; }
    Assert(wrongRejected, "Incorrect password rejected");

    string extracted = Path.Combine(root, "extracted");
    await reader.ExtractAsync(archive, extracted, null, CancellationToken.None, password);
    Assert(File.ReadAllText(Path.Combine(extracted, "a.txt")) == File.ReadAllText(fileA),
        "Encrypted file A round-trip");
    Assert(File.ReadAllText(Path.Combine(extracted, "b.txt")) == File.ReadAllText(fileB),
        "Encrypted file B round-trip");

    string mixed = Path.Combine(root, "mixed.zip");
    using (var stream = File.Create(mixed))
    using (var zip = new ZipOutputStream(stream))
    {
        zip.IsStreamOwner = true;
        zip.Password = password;
        zip.PutNextEntry(new ZipEntry("encrypted.txt"));
        byte[] data = "protected"u8.ToArray();
        zip.Write(data, 0, data.Length);
        zip.CloseEntry();
        zip.Password = null;
        zip.PutNextEntry(new ZipEntry("plain.txt"));
        data = "public"u8.ToArray();
        zip.Write(data, 0, data.Length);
        zip.CloseEntry();
        zip.Finish();
    }
    Assert(reader.GetZipEncryptionCounts(mixed) == (1, 1), "Mixed ZIP detected");
    Assert(reader.NeedsPassword(mixed), "Mixed ZIP still requests a password");
    bool blocked = false;
    try { await reader.ExtractAsync(mixed, Path.Combine(root, "blocked"), null, CancellationToken.None); }
    catch { blocked = true; }
    Assert(blocked, "Mixed ZIP extraction without password is blocked");
    bool recoveryBlocked = false;
    try { await new ZipRecoveryService().RecoverAsync(mixed, null, CancellationToken.None); }
    catch (InvalidOperationException) { recoveryBlocked = true; }
    Assert(recoveryBlocked, "Encrypted/mixed ZIP recovery cannot leak unencrypted copies");

    string malicious = Path.Combine(root, "zip-slip.zip");
    using (var stream = File.Create(malicious))
    using (var zip = new ZipOutputStream(stream))
    {
        zip.IsStreamOwner = true;
        zip.PutNextEntry(new ZipEntry("../outside.txt"));
        byte[] data = "not allowed"u8.ToArray();
        zip.Write(data, 0, data.Length);
        zip.CloseEntry();
        zip.Finish();
    }
    bool zipSlipBlocked = false;
    try { await reader.ExtractAsync(malicious, Path.Combine(root, "safe"), null, CancellationToken.None); }
    catch (InvalidDataException) { zipSlipBlocked = true; }
    Assert(zipSlipBlocked && !File.Exists(Path.Combine(root, "outside.txt")), "ZIP Slip traversal blocked");

    // Extraction options: default remains no-overwrite, while explicit policies work.
    string collisionRoot = Path.Combine(root, "collision");
    Directory.CreateDirectory(collisionRoot);
    File.WriteAllText(Path.Combine(collisionRoot, "a.txt"), "existing data");
    await reader.ExtractAsync(archive, collisionRoot, null, CancellationToken.None, password,
        ExtractionCollisionPolicy.Skip);
    Assert(File.ReadAllText(Path.Combine(collisionRoot, "a.txt")) == "existing data" &&
        File.Exists(Path.Combine(collisionRoot, "b.txt")), "Skip policy preserves existing files");
    await reader.ExtractAsync(archive, collisionRoot, null, CancellationToken.None, password,
        ExtractionCollisionPolicy.Rename);
    Assert(File.Exists(Path.Combine(collisionRoot, "a (1).txt")), "Rename policy creates a new filename");
    await reader.ExtractAsync(archive, collisionRoot, null, CancellationToken.None, password,
        ExtractionCollisionPolicy.Overwrite);
    Assert(File.ReadAllText(Path.Combine(collisionRoot, "a.txt")) == File.ReadAllText(fileA),
        "Explicit overwrite replaces existing file");

    // TAR.GZ writer is a standard gzip-compressed TAR; test reading and extraction.
    string tarGz = Path.Combine(root, "sample.tar.gz");
    await new TarGzipArchiveService().CreateAsync(tarGz, [fileA, fileB],
        CompressionLevel.Optimal, null, CancellationToken.None);
    Assert(ArchiveReadService.Supports(tarGz), "TAR.GZ extension is recognized");
    await reader.TestAsync(tarGz, null, CancellationToken.None);
    string tarGzExtract = Path.Combine(root, "targz-extracted");
    await reader.ExtractAsync(tarGz, tarGzExtract, null, CancellationToken.None);
    Assert(File.ReadAllText(Path.Combine(tarGzExtract, "a.txt")) == File.ReadAllText(fileA),
        "TAR.GZ round-trip");

    // BILZ volume transport tests use opaque bytes: no codec is needed to verify
    // split/reassemble integrity, missing parts and tamper detection.
    string syntheticBilz = Path.Combine(root, "synthetic.bilz");
    byte[] payload = new byte[2_500_000];
    System.Security.Cryptography.RandomNumberGenerator.Fill(payload);
    File.WriteAllBytes(syntheticBilz, payload);
    string firstVolume = await BilzVolumeService.SplitAsync(syntheticBilz, 1, CancellationToken.None);
    Assert(firstVolume.EndsWith(".bilz.001") && File.Exists(firstVolume) && !File.Exists(syntheticBilz),
        "BILZ split writes .001 volumes and removes source only on success");
    using (var assembled = BilzVolumeService.Resolve(firstVolume))
        Assert(File.ReadAllBytes(assembled.Path).SequenceEqual(payload), "BILZ parts reassemble byte-for-byte");
    string secondVolume = syntheticBilz + ".002";
    byte[] originalSecond = File.ReadAllBytes(secondVolume);
    using (var corrupt = new FileStream(secondVolume, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
    {
        corrupt.Position = corrupt.Length - 1;
        int last = corrupt.ReadByte();
        corrupt.Position--;
        corrupt.WriteByte((byte)(last ^ 0x01));
    }
    bool corruptRejected = false;
    try { using var _ = BilzVolumeService.Resolve(firstVolume); }
    catch (InvalidDataException) { corruptRejected = true; }
    Assert(corruptRejected, "Corrupted BILZ part is rejected before extraction");
    File.WriteAllBytes(secondVolume, originalSecond);
    File.Delete(secondVolume);
    bool missingRejected = false;
    try { using var _ = BilzVolumeService.Resolve(firstVolume); }
    catch (FileNotFoundException) { missingRejected = true; }
    Assert(missingRejected, "Missing BILZ part is rejected");

    Assert(ExplorerWheelScroll.CalculateOffset(10, 50, 120, 3) == 7, "Mouse wheel up scrolls Explorer list");
    Assert(ExplorerWheelScroll.CalculateOffset(10, 50, -120, 3) == 13, "Mouse wheel down scrolls Explorer list");
    Assert(ExplorerWheelScroll.CalculateOffset(0, 50, 120, 3) == 0, "Mouse wheel clamps at top");
    Assert(ExplorerWheelScroll.CalculateOffset(49, 50, -120, 3) == 50, "Mouse wheel clamps at bottom");
    foreach (string unsafePath in new[] { "../escape.txt", "C:/root.txt", "CON.txt", "aux/config", "file.txt:secret", "trailing. ", "a//b", "folder/../escape", "NUL", "file?.txt" })
    {
        bool rejected = false;
        try { ArchiveEntryPathPolicy.Validate(unsafePath); }
        catch (InvalidDataException) { rejected = true; }
        Assert(rejected, "Unsafe Windows archive path rejected: " + unsafePath);
    }
    foreach (string validPath in new[] { "Монгол/файл.txt", "folder/subdir/file.txt", "photo (1).png", "nested/folder/" })
    {
        ArchiveEntryPathPolicy.Validate(validPath);
        Assert(true, "Safe archive path accepted: " + validPath);
    }
    string reservedArchive = Path.Combine(root, "reserved-name.zip");
    using (var stream = File.Create(reservedArchive))
    using (var zip = new ZipOutputStream(stream))
    {
        zip.PutNextEntry(new ZipEntry("CON.txt"));
        zip.WriteByte(65);
        zip.CloseEntry();
        zip.Finish();
    }
    bool reservedBlocked = false;
    try { await reader.ExtractAsync(reservedArchive, Path.Combine(root, "reserved-out"), null, CancellationToken.None); }
    catch (InvalidDataException) { reservedBlocked = true; }
    Assert(reservedBlocked && !Directory.Exists(Path.Combine(root, "reserved-out")), "Reserved filename rejected before extraction writes");

    // Folder creation must preserve structure across all supported standard writers.
    string folderSource = Path.Combine(root, "nested-input");
    Directory.CreateDirectory(Path.Combine(folderSource, "Монгол"));
    File.WriteAllText(Path.Combine(folderSource, "Монгол", "тест.txt"), "BiloraZIP Unicode path");
    string folderZip = Path.Combine(root, "folder.zip");
    await new ZipArchiveService().CreateFolderAsync(folderZip, folderSource,
        CompressionLevel.Optimal, null, CancellationToken.None);
    string folderExtract = Path.Combine(root, "folder-out");
    await reader.ExtractAsync(folderZip, folderExtract, null, CancellationToken.None);
    Assert(File.ReadAllText(Path.Combine(folderExtract, "nested-input", "Монгол", "тест.txt")) == "BiloraZIP Unicode path",
        "ZIP folder round-trip preserves Unicode paths");
    string folderTar = Path.Combine(root, "folder.tar");
    await new TarArchiveService().CreateFolderAsync(folderTar, folderSource, null, CancellationToken.None);
    string tarFolderExtract = Path.Combine(root, "folder-tar-out");
    await reader.ExtractAsync(folderTar, tarFolderExtract, null, CancellationToken.None);
    Assert(File.ReadAllText(Path.Combine(tarFolderExtract, "nested-input", "Монгол", "тест.txt")) == "BiloraZIP Unicode path",
        "TAR folder round-trip preserves Unicode paths");

    bool entryLimitBlocked = false;
    try { ArchiveSafetyPolicy.CheckPlan(int.MaxValue, 0); }
    catch (InvalidDataException) { entryLimitBlocked = true; }
    Assert(entryLimitBlocked, "Archive entry safety limit enforced");
    Console.WriteLine($"SUCCESS: {passed} self-tests passed.");
}
finally
{
    try { Directory.Delete(root, recursive: true); }
    catch (IOException) { }
    catch (UnauthorizedAccessException) { }
}
