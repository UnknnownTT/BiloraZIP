using System.IO;
using System.IO.Compression;
using BiloraZIP.Models;
using ICSharpCode.SharpZipLib.Zip;

namespace BiloraZIP.Services;

// AES-256 encrypted ZIP writer, independent from the built-in unencrypted .NET ZIP writer.
// Uses SharpZipLib (MIT). RAR creation is deliberately not supported.
public sealed class EncryptedZipService
{
    public Task CreateAsync(string destination, IReadOnlyList<string> files, string password, CompressionLevel level,
        IProgress<OperationProgressInfo>? progress, CancellationToken token)
        => Task.Run(() => Create(destination, files, password, level, progress, token), token);

    public Task CreateMixedAsync(string destination, IReadOnlyCollection<string> sources, string password,
        CompressionLevel level, IProgress<OperationProgressInfo>? progress, CancellationToken token)
        => Task.Run(() => {
            var (files, dirs) = ArchiveInputPlanner.Build(sources, destination);
            Write(Path.GetFullPath(destination), files, dirs, password, level, progress, token);
        }, token);

    public Task CreateFolderAsync(string destination, string folder, string password, CompressionLevel level,
        IProgress<OperationProgressInfo>? progress, CancellationToken token)
        => Task.Run(() => CreateFolder(destination, folder, password, level, progress, token), token);

    private static void Create(string destination, IReadOnlyList<string> files, string password, CompressionLevel level,
        IProgress<OperationProgressInfo>? progress, CancellationToken token)
    {
        if (files.Count == 0) throw new InvalidOperationException("Select at least one file.");
        if (files.Any(f => !File.Exists(f))) throw new FileNotFoundException("Some source files no longer exist.");
        if (files.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != files.Count)
            throw new IOException("Selected files must have unique file names.");
        string output = Path.GetFullPath(destination);
        if (File.Exists(output)) throw new IOException("An archive with that name already exists.");
        if (files.Any(f => string.Equals(Path.GetFullPath(f), output, StringComparison.OrdinalIgnoreCase)))
            throw new IOException("The archive cannot include itself.");
        var entries = files.Select(f => (Source: f, Name: Path.GetFileName(f))).ToList();
        Write(output, entries, [], password, level, progress, token);
    }

    private static void CreateFolder(string destination, string folder, string password, CompressionLevel level,
        IProgress<OperationProgressInfo>? progress, CancellationToken token)
    {
        var (files, dirs) = ArchiveInputPlanner.Build([folder], destination);
        Write(Path.GetFullPath(destination), files, dirs, password, level, progress, token);
    }

    private static void VerifyEveryFileEncrypted(string zipPath, int expectedFiles, string password, CancellationToken token)
    {
        using var zip = new ICSharpCode.SharpZipLib.Zip.ZipFile(File.OpenRead(zipPath));
        zip.Password = password;
        byte[] buffer = new byte[128 * 1024];
        int verified = 0;
        foreach (ICSharpCode.SharpZipLib.Zip.ZipEntry entry in zip)
        {
            if (entry.IsDirectory) continue; // ZIP directory names are metadata, not encrypted.
            if (!entry.IsCrypted || entry.AESKeySize != 256)
                throw new InvalidDataException("ZIP security verification failed: an entry is not AES-256 encrypted: " + entry.Name);
            token.ThrowIfCancellationRequested();
            using (var input = zip.GetInputStream(entry))
                while (input.Read(buffer, 0, buffer.Length) != 0)
                    token.ThrowIfCancellationRequested();
            verified++;
        }
        if (verified != expectedFiles)
            throw new InvalidDataException("ZIP security verification failed: the file count differs from the input.");
    }

    private static void Write(string output, List<(string Source, string Name)> files, List<string> dirs,
        string password, CompressionLevel level, IProgress<OperationProgressInfo>? progress, CancellationToken token)
    {
        if (password.Length == 0) throw new ArgumentException("Password cannot be empty.");
        long total = files.Sum(f => new FileInfo(f.Source).Length);
        long done = 0;
        int completed = 0;
        string temp = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
        byte[] buffer = new byte[128 * 1024];
        int zipLevel = level switch
        {
            CompressionLevel.NoCompression => 0,
            CompressionLevel.Fastest => 1,
            CompressionLevel.SmallestSize => 9,
            _ => 6
        };
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var zip = new ZipOutputStream(stream))
            {
                zip.Password = null; // Empty directory entries remain unencrypted.
                zip.SetLevel(zipLevel);
                zip.IsStreamOwner = true;
                foreach (string dir in dirs)
                {
                    token.ThrowIfCancellationRequested();
                    zip.PutNextEntry(new ZipEntry(dir) { DateTime = DateTime.Now });
                    zip.CloseEntry();
                }
                zip.Password = password; // AES-256 encrypts every file payload.
                foreach (var (sourcePath, archiveName) in files)
                {
                    token.ThrowIfCancellationRequested();
                    var info = new FileInfo(sourcePath);
                    var entry = new ZipEntry(archiveName)
                    {
                        DateTime = info.LastWriteTime,
                        AESKeySize = 256,
                        Size = info.Length
                    };
                    zip.PutNextEntry(entry);
                    using (var input = File.OpenRead(sourcePath))
                    {
                        while (true)
                        {
                            token.ThrowIfCancellationRequested();
                            int read = input.Read(buffer, 0, buffer.Length);
                            if (read == 0) break;
                            zip.Write(buffer, 0, read);
                            done += read;
                            progress?.Report(new OperationProgressInfo {
                                Stage = "Compressing (AES-256)", CurrentItem = archiveName, ItemsCompleted = completed,
                                TotalItems = files.Count, BytesProcessed = done, TotalBytes = total
                            });
                        }
                    }
                    zip.CloseEntry();
                    completed++;
                    progress?.Report(new OperationProgressInfo {
                        Stage = "Compressing (AES-256)", CurrentItem = archiveName, ItemsCompleted = completed,
                        TotalItems = files.Count, BytesProcessed = done, TotalBytes = total
                    });
                }
                zip.Finish();
            }
            token.ThrowIfCancellationRequested();
            VerifyEveryFileEncrypted(temp, files.Count, password, token);
            File.Move(temp, output);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
