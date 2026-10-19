using System.IO;
using BiloraZIP.Models;
using SharpCompress.Archives;
using SharpCompress.Readers;

namespace BiloraZIP.Services;

// Read-only multi-format engine: ZIP, RAR and 7Z are intentionally never rewritten here.
public sealed class ArchiveReadService
{
    private const int BufferSize = 128 * 1024;
    private readonly BilzArchiveService _bilz = new();
    public static readonly string[] Extensions = [".zip", ".rar", ".7z", ".tar", ".zipx", ".xapk", ".bilz"];

    public static bool Supports(string path)
        => BilzVolumeService.IsFirstVolume(path)
            || Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)
            || path.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<ArchiveEntryInfo> List(string path, string? password = null)
    {
        EnsureSupported(path);
        if (BilzArchiveService.IsBilz(path)) return _bilz.List(path);
        using var archive = ArchiveFactory.OpenArchive(path, new ReaderOptions { Password = password });
        return archive.Entries.Select(e => new ArchiveEntryInfo(
            e.Key ?? string.Empty,
            e.IsDirectory ? "—" : FormatBytes(e.Size),
            e.IsDirectory ? "—" : FormatBytes(e.CompressedSize),
            e.IsDirectory ? "Folder" : "File",
            e.IsDirectory || e.Size == 0 ? "—" : $"{Math.Max(0, 100d * (1 - e.CompressedSize / (double)e.Size)):0}%",
            e.LastModifiedTime?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—"
        )).ToList();
    }

    public Task ExtractAsync(string path, string destination, IProgress<OperationProgressInfo>? progress, CancellationToken token, string? password = null, ExtractionCollisionPolicy policy = ExtractionCollisionPolicy.Fail)
        => BilzArchiveService.IsBilz(path) ? _bilz.ExtractAsync(path, destination, progress, token, null, policy) :
            Task.Run(() => Extract(path, destination, progress, token, password, null, policy), token);

    public Task ExtractSelectedAsync(string path, string destination, IReadOnlyCollection<string> selectedKeys,
        IProgress<OperationProgressInfo>? progress, CancellationToken token, string? password = null,
        ExtractionCollisionPolicy policy = ExtractionCollisionPolicy.Fail)
    {
        if (selectedKeys.Count == 0) throw new ArgumentException("Select one or more entries", nameof(selectedKeys));
        return BilzArchiveService.IsBilz(path) ?
            _bilz.ExtractAsync(path, destination, progress, token, selectedKeys, policy) :
            Task.Run(() => Extract(path, destination, progress, token, password, selectedKeys, policy), token);
    }

    public Task TestAsync(string path, IProgress<OperationProgressInfo>? progress, CancellationToken token, string? password = null)
        => BilzArchiveService.IsBilz(path) ? _bilz.TestAsync(path, progress, token) :
            Task.Run(() => Test(path, progress, token, password), token);

    public Task VerifyPasswordAsync(string path, string password, CancellationToken token)
        => BilzArchiveService.IsBilz(path) ? Task.CompletedTask :
            Task.Run(() => VerifyPassword(path, password, token), token);

    public ArchiveMetadata GetMetadata(string path, string? password = null)
    {
        EnsureSupported(path);
        if (BilzArchiveService.IsBilz(path)) return _bilz.GetMetadata(path);
        using var archive = ArchiveFactory.OpenArchive(path, new ReaderOptions { Password = password });
        var entries = archive.Entries.ToList();
        long original = entries.Where(e => !e.IsDirectory).Sum(e => e.Size);
        long packed = entries.Where(e => !e.IsDirectory).Sum(e => e.CompressedSize);
        bool encrypted = entries.Any(e => e.IsEncrypted);
        string protection = encrypted ? "Protected (type varies by archive)" : "None detected";
        if (Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var counts = GetZipEncryptionCounts(path);
                if (counts.Encrypted > 0 && counts.Plain > 0)
                    protection = LocalizationService.IsMongolian
                        ? $"ХОЛИМОГ: {counts.Encrypted} шифрлэсэн, {counts.Plain} хамгаалалтгүй файл"
                        : $"MIXED: {counts.Encrypted} encrypted, {counts.Plain} unencrypted files";
                else if (counts.Encrypted > 0)
                    protection = LocalizationService.IsMongolian
                        ? $"Шифрлэсэн {counts.Encrypted} файл (нэрс харагдана)"
                        : $"Encrypted file contents ({counts.Encrypted} entries; names remain visible)";
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ICSharpCode.SharpZipLib.Zip.ZipException) { }
        }
        string ratio = original == 0 ? "—" : $"{Math.Max(0.0, 100d * (1d - packed / (double)original)):0.0}%";
        var info = new FileInfo(path);
        return new ArchiveMetadata(info.Name, info.FullName,
            info.Extension.TrimStart('.').ToUpperInvariant(), info.Length,
            original, packed, entries.Count(e => !e.IsDirectory), entries.Count(e => e.IsDirectory),
            ratio, protection,
            info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
            "Unknown", "Not available", "Unknown", "Not available", "Not tested");
    }

    public (int Encrypted, int Plain) GetZipEncryptionCounts(string path)
    {
        if (!Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase)) return (0, 0);
        using var zip = new ICSharpCode.SharpZipLib.Zip.ZipFile(File.OpenRead(path));
        int encrypted = 0, plain = 0;
        foreach (ICSharpCode.SharpZipLib.Zip.ZipEntry entry in zip)
        {
            if (entry.IsDirectory) continue;
            if (entry.IsCrypted) encrypted++; else plain++;
        }
        return (encrypted, plain);
    }

    public bool NeedsPassword(string path)
    {
        EnsureSupported(path);
        if (BilzArchiveService.IsBilz(path)) return false;
        if (Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            try { if (GetZipEncryptionCounts(path).Encrypted > 0) return true; }
            catch (Exception ex) when (ex is InvalidDataException or IOException or ICSharpCode.SharpZipLib.Zip.ZipException)
            { /* Let SharpCompress handle unusual ZIP variants. */ }
        }
        try
        {
            using var archive = ArchiveFactory.OpenArchive(path);
            return archive.Entries.Any(e => e.IsEncrypted);
        }
        catch (Exception ex) when (IsPasswordException(ex))
        {
            // Some RAR and 7Z files encrypt their file-name headers too.
            return true;
        }
    }

    public static bool IsPasswordException(Exception ex)
    {
        string message = ex.ToString();
        return message.Contains("password", StringComparison.OrdinalIgnoreCase)
            || message.Contains("encrypted", StringComparison.OrdinalIgnoreCase)
            || message.Contains("cryptographic", StringComparison.OrdinalIgnoreCase);
    }

    private void VerifyPassword(string path, string password, CancellationToken token)
    {
        if (Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            VerifyZipPassword(path, password, token);
            return;
        }
        using var archive = ArchiveFactory.OpenArchive(path, new ReaderOptions { Password = password });
        var encryptedEntries = archive.Entries.Where(e => !e.IsDirectory && e.IsEncrypted).ToList();
        if (encryptedEntries.Count == 0)
        {
            var first = archive.Entries.FirstOrDefault(e => !e.IsDirectory);
            if (first is null) return; // Empty archive with encrypted headers.
            encryptedEntries.Add(first); // Some formats encrypt headers but do not expose per-entry flags.
        }
        var buffer = new byte[BufferSize];
        foreach (var entry in encryptedEntries)
        {
            token.ThrowIfCancellationRequested();
            using var entryStream = entry.OpenEntryStream();
            while (entryStream.Read(buffer, 0, buffer.Length) != 0)
                token.ThrowIfCancellationRequested();
        }
    }

    private static void VerifyZipPassword(string path, string password, CancellationToken token)
    {
        using var zip = new ICSharpCode.SharpZipLib.Zip.ZipFile(File.OpenRead(path));
        zip.Password = password;
        byte[] buffer = new byte[BufferSize];
        int encrypted = 0;
        foreach (ICSharpCode.SharpZipLib.Zip.ZipEntry entry in zip)
        {
            if (entry.IsDirectory || !entry.IsCrypted) continue;
            encrypted++;
            token.ThrowIfCancellationRequested();
            using var input = zip.GetInputStream(entry);
            while (input.Read(buffer, 0, buffer.Length) != 0)
                token.ThrowIfCancellationRequested();
        }
        if (encrypted == 0) throw new InvalidDataException("ZIP has no encrypted files to verify.");
    }

    // Extract exactly one selected entry into a session-specific preview folder.
    // The archive controls neither the destination path nor the output filename hierarchy.
    public Task<string> ExtractPreviewAsync(string path, string entryKey, string? password, CancellationToken token)
        => BilzArchiveService.IsBilz(path) ? _bilz.ExtractPreviewAsync(path, entryKey, token) :
            Task.Run(() => ExtractPreview(path, entryKey, password, token), token);

    private static string ExtractPreview(string path, string entryKey, string? password, CancellationToken token)
    {
        using var archive = ArchiveFactory.OpenArchive(path, new ReaderOptions { Password = password });
        var entry = archive.Entries.FirstOrDefault(e => !e.IsDirectory && string.Equals(e.Key?.Replace('\\', '/'), entryKey.Replace('\\', '/'), StringComparison.Ordinal));
        if (entry is null) throw new FileNotFoundException("Entry no longer exists inside archive.", entryKey);
        if (entry.LinkTarget is not null) throw new InvalidDataException("Links cannot be previewed safely.");
        const long limit = 512L * 1024 * 1024;
        if (entry.Size > limit) throw new IOException("This file exceeds the 512 MB preview limit. Extract it explicitly instead.");
        ArchiveSafetyPolicy.CheckEntry(entry.Size, entry.CompressedSize, entryKey);
        ArchiveEntryPathPolicy.Validate(entryKey);
        string name = Path.GetFileName(entryKey.Replace('\\', '/'));
        if (name is "." or ".." or "" || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new InvalidDataException("Unsafe filename in archive.");
        string previewRoot = Path.Combine(Path.GetTempPath(), "BiloraZIP-Preview", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(previewRoot);
        string output = Path.Combine(previewRoot, name);
        try
        {
            using var input = entry.OpenEntryStream();
            using var file = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            byte[] buffer = new byte[128 * 1024];
            long written = 0;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                int count = input.Read(buffer, 0, buffer.Length);
                if (count == 0) break;
                written += count;
                if (written > limit || written > entry.Size)
                    throw new InvalidDataException("Preview file exceeds safety limits.");
                file.Write(buffer, 0, count);
            }
            if (written != entry.Size) throw new InvalidDataException("Preview size mismatch.");
            return output;
        }
        catch
        {
            try { Directory.Delete(previewRoot, recursive: true); } catch (IOException) { }
            throw;
        }
    }

    private static void EnsureSupported(string path)
    {
        if (!Supports(path)) throw new NotSupportedException("Unsupported archive extension. ZIP, RAR, 7Z, TAR, ZIPX, XAPK and BILZ are supported where codecs are available.");
    }

    private void Extract(string path, string destination, IProgress<OperationProgressInfo>? progress,
        CancellationToken token, string? password, IReadOnlyCollection<string>? selectedKeys = null,
        ExtractionCollisionPolicy policy = ExtractionCollisionPolicy.Fail)
    {
        EnsureSupported(path);
        string root = Path.GetFullPath(destination);
        string prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        using var archive = ArchiveFactory.OpenArchive(path, new ReaderOptions { Password = password });
        var entries = archive.Entries.ToList();
        if (entries.Any(e => e.IsEncrypted) && string.IsNullOrEmpty(password))
            throw new UnauthorizedAccessException("Archive contains encrypted entries. Enter the archive password before extracting.");
        if (selectedKeys is not null)
        {
            var chosen = selectedKeys.Select(k => k.Replace('\\', '/')).ToArray();
            entries = entries.Where(e =>
            {
                string key = (e.Key ?? "").Replace('\\', '/');
                return chosen.Any(selectedPrefix => string.Equals(key.TrimEnd('/'), selectedPrefix.TrimEnd('/'), StringComparison.Ordinal)
                    || key.StartsWith(selectedPrefix.TrimEnd('/') + "/", StringComparison.Ordinal));
            }).ToList();
            if (entries.Count == 0) throw new FileNotFoundException("Selected entries were not found in archive.");
        }
        ArchiveSafetyPolicy.CheckPlan(entries.Count, entries.Where(e => !e.IsDirectory).Sum(e => e.Size));
        var plan = new List<(SharpCompress.Archives.IArchiveEntry Entry, string Target, bool Directory)>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var reservedOriginalTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
        {
            string key = e.Key ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(key))
                reservedOriginalTargets.Add(Path.GetFullPath(Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar))));
        }
        foreach (var entry in entries)
        {
            token.ThrowIfCancellationRequested();
            if (!entry.IsDirectory) ArchiveSafetyPolicy.CheckEntry(entry.Size, entry.CompressedSize, entry.Key ?? string.Empty);
            if (!string.IsNullOrEmpty(entry.LinkTarget))
                throw new InvalidDataException("Archive contains a symbolic link, which is blocked for safe extraction.");
            string raw = entry.Key ?? string.Empty;
            if (string.IsNullOrWhiteSpace(raw)) continue;
            ArchiveEntryPathPolicy.Validate(raw);
            string relative = raw.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            if (Path.IsPathRooted(relative) || relative.IndexOf(':') >= 0 || relative.StartsWith(Path.DirectorySeparatorChar))
                throw new InvalidDataException("Unsafe archive path: " + raw);
            string target = Path.GetFullPath(Path.Combine(root, relative));
            if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Archive entry would escape destination: " + raw);
            string normalized = Path.TrimEndingDirectorySeparator(target);
            if (string.Equals(normalized, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                throw new IOException("Cannot overwrite the archive currently being extracted.");
            if (!names.Add(normalized)) throw new InvalidDataException("Duplicate archive path: " + raw);
            if (entry.IsDirectory)
            {
                if (File.Exists(target)) throw new IOException("A file blocks an archive folder: " + target);
            }
            else
            {
                if (Directory.Exists(target)) throw new IOException("A folder blocks an archive file: " + target);
                if (File.Exists(target))
                {
                    if ((File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Overwriting a symbolic link is not allowed: " + target);
                    switch (policy)
                    {
                        case ExtractionCollisionPolicy.Skip: continue;
                        case ExtractionCollisionPolicy.Rename:
                            string folder = Path.GetDirectoryName(target)!;
                            string baseName = Path.GetFileNameWithoutExtension(target);
                            string ext = Path.GetExtension(target);
                            int index = 1;
                            string candidate;
                            do
                            {
                                if (index > 10000) throw new IOException("Too many file name conflicts.");
                                candidate = Path.Combine(folder, $"{baseName} ({index++}){ext}");
                            } while (File.Exists(candidate) || Directory.Exists(candidate) || names.Contains(candidate) || reservedOriginalTargets.Contains(candidate));
                            target = candidate;
                            names.Add(target);
                            break;
                        case ExtractionCollisionPolicy.Overwrite: break;
                        default: throw new IOException("Destination exists; no files were overwritten: " + target);
                    }
                }
            }
            plan.Add((entry, target, entry.IsDirectory));
        }
        var files = new HashSet<string>(plan.Where(p => !p.Directory).Select(p => p.Target), StringComparer.OrdinalIgnoreCase);
        foreach (var item in plan)
        {
            string? parent = Path.GetDirectoryName(item.Target);
            while (parent is not null && parent.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                if (files.Contains(parent)) throw new InvalidDataException("File/folder conflict inside archive.");
                if (Directory.Exists(parent) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Extracting through an existing junction/symlink is blocked.");
                parent = Path.GetDirectoryName(parent);
            }
        }
        if (Directory.Exists(root) && (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Extraction root is a junction or symbolic link.");
        Directory.CreateDirectory(root);
        long total = plan.Where(p => !p.Directory).Sum(p => p.Entry.Size);
        long done = 0;
        int totalFiles = plan.Count(p => !p.Directory), completed = 0;
        byte[] buffer = new byte[BufferSize];
        foreach (var item in plan)
        {
            token.ThrowIfCancellationRequested();
            if (item.Directory) { Directory.CreateDirectory(item.Target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(item.Target)!);
            string partial = item.Target + ".bilorazip-" + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                long entryWritten = 0;
                using (var from = item.Entry.OpenEntryStream())
                using (var to = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    while (true)
                    {
                        token.ThrowIfCancellationRequested();
                        int n = from.Read(buffer, 0, buffer.Length);
                        if (n == 0) break;
                        entryWritten = checked(entryWritten + n);
                        long nextTotal = checked(done + n);
                        ArchiveSafetyPolicy.CheckWritten(entryWritten, nextTotal, item.Entry.Key ?? string.Empty);
                        if (entryWritten > item.Entry.Size)
                            throw new InvalidDataException("Decompressed data exceeds declared size: " + item.Entry.Key);
                        to.Write(buffer, 0, n);
                        done = nextTotal;
                        Report(progress, "Extracting", item.Entry.Key ?? string.Empty, completed, totalFiles, done, total);
                    }
                }
                token.ThrowIfCancellationRequested();
                if (entryWritten != item.Entry.Size)
                    throw new InvalidDataException("Decompressed file size differs from archive metadata: " + item.Entry.Key);
                // Do not follow a destination symlink, even if it appeared during extraction.
                if (File.Exists(item.Target) && (File.GetAttributes(item.Target) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Destination changed to a symbolic link: " + item.Target);
                File.Move(partial, item.Target, overwrite: policy == ExtractionCollisionPolicy.Overwrite);
            }
            finally { if (File.Exists(partial)) File.Delete(partial); }
            completed++;
            Report(progress, "Extracting", item.Entry.Key ?? string.Empty, completed, totalFiles, done, total);
        }
    }

    private void Test(string path, IProgress<OperationProgressInfo>? progress, CancellationToken token, string? password)
    {
        EnsureSupported(path);
        using var archive = ArchiveFactory.OpenArchive(path, new ReaderOptions { Password = password });
        var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
        ArchiveSafetyPolicy.CheckPlan(entries.Count, entries.Sum(e => checked(e.Size)));
        foreach (var entry in entries)
        {
            ArchiveSafetyPolicy.CheckEntry(entry.Size, entry.CompressedSize, entry.Key ?? string.Empty);
            ArchiveEntryPathPolicy.Validate(entry.Key ?? string.Empty);
            if (!string.IsNullOrEmpty(entry.LinkTarget))
                throw new InvalidDataException("Archive contains a link, which cannot be tested as a regular file.");
        }
        long total = entries.Sum(e => checked(e.Size)), done = 0;
        int completed = 0;
        byte[] buffer = new byte[BufferSize];
        foreach (var entry in entries)
        {
            token.ThrowIfCancellationRequested();
            using var stream = entry.OpenEntryStream();
            long actual = 0;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                int n = stream.Read(buffer, 0, buffer.Length);
                if (n == 0) break;
                actual = checked(actual + n);
                done = checked(done + n);
                ArchiveSafetyPolicy.CheckWritten(actual, done, entry.Key ?? string.Empty);
                if (actual > entry.Size) throw new InvalidDataException("Decompressed data exceeds declared entry size.");
                Report(progress, "Testing", entry.Key ?? string.Empty, completed, entries.Count, done, total);
            }
            if (actual != entry.Size) throw new InvalidDataException("Entry size does not match: " + entry.Key);
            completed++;
            Report(progress, "Testing", entry.Key ?? string.Empty, completed, entries.Count, done, total);
        }
    }

    private static void Report(IProgress<OperationProgressInfo>? progress, string stage, string name, int complete, int totalFiles, long done, long total)
        => progress?.Report(new OperationProgressInfo {
            Stage = stage, CurrentItem = name, ItemsCompleted = complete,
            TotalItems = totalFiles, BytesProcessed = done, TotalBytes = total
        });

    private static string FormatBytes(long size)
    {
        string[] suffix = ["B", "KB", "MB", "GB", "TB"];
        double value = size;
        int i = 0;
        while (value >= 1024 && i < suffix.Length - 1) { value /= 1024; i++; }
        return $"{value:0.#} {suffix[i]}";
    }
}
