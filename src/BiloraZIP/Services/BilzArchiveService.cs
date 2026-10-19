using Bilora.BarmCodec;
using BiloraZIP.Models;
using System.IO;

namespace BiloraZIP.Services;

/// <summary>Experimental BILZ-1 adapter. No password/solid mode; no overwrites.</summary>
public sealed class BilzArchiveService
{
    public static bool IsBilz(string path) => Path.GetExtension(path).Equals(".bilz", StringComparison.OrdinalIgnoreCase)
        || BilzVolumeService.IsFirstVolume(path);

    public IReadOnlyList<ArchiveEntryInfo> List(string path)
    {
        using var resolved = BilzVolumeService.Resolve(path);
        return BilzArchive.List(resolved.Path).Select(e =>
            new ArchiveEntryInfo(e.Path, e.IsDirectory ? "—" : Pretty(e.OriginalSize),
                e.IsDirectory ? "—" : Pretty(e.StoredSize), e.IsDirectory ? "Folder" : "File",
                e.IsDirectory || e.OriginalSize == 0 ? "—" :
                    $"{Math.Max(0, 100d * (1 - e.StoredSize / (double)e.OriginalSize)):0}%", "—")).ToArray();
    }

    public ArchiveMetadata GetMetadata(string path)
    {
        using var resolved = BilzVolumeService.Resolve(path);
        var entries = BilzArchive.List(resolved.Path);
        long raw = entries.Sum(e => e.OriginalSize);
        long packed = entries.Sum(e => e.StoredSize);
        var info = new FileInfo(path);
        long fullArchiveLength = new FileInfo(resolved.Path).Length;
        return new ArchiveMetadata(info.Name, info.FullName, "BILZ-1 (experimental)", fullArchiveLength,
            raw, packed, entries.Count(e => !e.IsDirectory), entries.Count(e => e.IsDirectory),
            raw == 0 ? "—" : $"{Math.Max(0, 100d * (1 - packed / (double)raw)):0.0}%",
            "Not supported", info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
            "Not encrypted", "Experimental BARM-1 codec", "Not available", "64 KiB blocks", "Not tested");
    }

    public Task CreateAsync(string output, IReadOnlyCollection<string> inputs, bool fast,
        IProgress<OperationProgressInfo>? progress, CancellationToken token)
        => Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            BilzArchive.Create(output, inputs, fast ? BarmCompressionProfile.Fast : BarmCompressionProfile.Maximum);
            // The prototype codec is not cancellable mid-block. If cancellation
            // was requested during creation, remove only the archive we just made.
            if (token.IsCancellationRequested)
            {
                File.Delete(output);
                token.ThrowIfCancellationRequested();
            }
            progress?.Report(new OperationProgressInfo { Stage = "BILZ archive created", ItemsCompleted = 1, TotalItems = 1, BytesProcessed = 1, TotalBytes = 1 });
        }, token);

    public Task TestAsync(string path, IProgress<OperationProgressInfo>? progress, CancellationToken token)
        => Task.Run(() =>
        {
            token.ThrowIfCancellationRequested();
            using var resolved = BilzVolumeService.Resolve(path, token);
            BilzArchive.Test(resolved.Path);
            progress?.Report(new OperationProgressInfo { Stage = "BILZ integrity verified", ItemsCompleted = 1, TotalItems = 1, BytesProcessed = 1, TotalBytes = 1 });
        }, token);

    public Task ExtractAsync(string path, string destination, IProgress<OperationProgressInfo>? progress,
        CancellationToken token, IReadOnlyCollection<string>? selected = null,
        ExtractionCollisionPolicy policy = ExtractionCollisionPolicy.Fail)
        => Task.Run(() => Extract(path, destination, progress, token, selected, policy), token);

    private static void Extract(string path, string destination, IProgress<OperationProgressInfo>? progress,
        CancellationToken token, IReadOnlyCollection<string>? selected, ExtractionCollisionPolicy policy)
    {
        // Core codec only publishes into a brand-new directory. Stage and verify
        // the entire archive before copying chosen entries into an existing target.
        token.ThrowIfCancellationRequested();
        string sourceArchivePath = Path.GetFullPath(path);
        using var resolved = BilzVolumeService.Resolve(path, token);
        path = resolved.Path;
        var metadata = BilzArchive.List(path);
        ArchiveSafetyPolicy.CheckPlan(metadata.Count, metadata.Where(e => !e.IsDirectory).Sum(e => e.OriginalSize));
        foreach (var e in metadata.Where(e => !e.IsDirectory))
            ArchiveSafetyPolicy.CheckEntry(e.OriginalSize, e.StoredSize, e.Path);
        string staging = Path.Combine(Path.GetTempPath(), "BiloraZIP-BILZ-" + Guid.NewGuid().ToString("N"));
        try
        {
            BilzArchive.Extract(path, staging); // SHA-256/CRC checked before publication
            token.ThrowIfCancellationRequested();
            string root = Path.GetFullPath(destination);
            string prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
            string[] chosen = selected?.Select(p => p.Replace('\\', '/').TrimEnd('/')).ToArray() ?? [];
            bool Include(string relative) => selected is null || chosen.Any(key =>
                relative.Equals(key, StringComparison.Ordinal) || relative.StartsWith(key + "/", StringComparison.Ordinal));
            var allEntries = BilzArchive.List(path).Where(e => Include(e.Path)).ToList();
            if (selected is not null && allEntries.Count == 0) throw new FileNotFoundException("Selected BILZ entries were not found.");
            // Check all destination paths before moving anything. Reject reparse points
            // in pre-existing ancestor directories and reject overwriting every file.
            foreach (var e in allEntries)
            {
                token.ThrowIfCancellationRequested();
                ArchiveEntryPathPolicy.Validate(e.Path);
                string target = Path.GetFullPath(Path.Combine(root, e.Path.Replace('/', Path.DirectorySeparatorChar)));
                if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsafe archive path.");
                if (string.Equals(target, sourceArchivePath, StringComparison.OrdinalIgnoreCase) ||
                    (BilzVolumeService.IsFirstVolume(sourceArchivePath) &&
                     target.StartsWith(sourceArchivePath[..^4] + ".", StringComparison.OrdinalIgnoreCase) &&
                     int.TryParse(Path.GetExtension(target).TrimStart('.'), out _)))
                    throw new IOException("Cannot overwrite BILZ source archive volumes while extracting.");
                if (e.IsDirectory && File.Exists(target)) throw new IOException("File blocks folder: " + target);
                if (!e.IsDirectory && Directory.Exists(target)) throw new IOException("Folder blocks file: " + target);
                if (!e.IsDirectory && File.Exists(target))
                {
                    if ((File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Destination is a symbolic link: " + target);
                    if (policy == ExtractionCollisionPolicy.Fail)
                        throw new IOException("Destination already exists: " + target);
                }
                string? parent = Path.GetDirectoryName(target);
                while (parent is not null)
                {
                    if (Directory.Exists(parent) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Extraction through a symlink or junction is not allowed.");
                    parent = Path.GetDirectoryName(parent);
                }
            }
            Directory.CreateDirectory(root);
            foreach (var e in allEntries.Where(e => e.IsDirectory).OrderBy(e => e.Path.Length))
            {
                token.ThrowIfCancellationRequested();
                Directory.CreateDirectory(Path.Combine(root, e.Path.Replace('/', Path.DirectorySeparatorChar)));
            }
            var files = allEntries.Where(e => !e.IsDirectory).ToArray();
            for (int i = 0; i < files.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                var e = files[i];
                string relative = e.Path.Replace('/', Path.DirectorySeparatorChar);
                string output = Path.Combine(root, relative);
                if (File.Exists(output))
                {
                    if (policy == ExtractionCollisionPolicy.Skip) continue;
                    if (policy == ExtractionCollisionPolicy.Rename)
                    {
                        string folder = Path.GetDirectoryName(output)!;
                        string name = Path.GetFileNameWithoutExtension(output);
                        string ext = Path.GetExtension(output);
                        int index = 1;
                        string candidate;
                        do
                        {
                            if (index > 10000) throw new IOException("Too many filename conflicts.");
                            candidate = Path.Combine(folder, $"{name} ({index++}){ext}");
                        } while (File.Exists(candidate) || Directory.Exists(candidate) || files.Any(other =>
                            string.Equals(Path.Combine(root, other.Path.Replace('/', Path.DirectorySeparatorChar)), candidate, StringComparison.OrdinalIgnoreCase)));
                        output = candidate;
                    }
                }
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                // Stage on the destination volume for an atomic, no-overwrite final rename.
                // File.Move directly from %TEMP% can fail when destination is a different drive.
                string partial = output + ".bilora-" + Guid.NewGuid().ToString("N") + ".partial";
                try
                {
                    using (var from = new FileStream(Path.Combine(staging, relative), FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var to = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        from.CopyTo(to);
                    token.ThrowIfCancellationRequested();
                    if (File.Exists(output) && (File.GetAttributes(output) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Destination changed to a symbolic link: " + output);
                    File.Move(partial, output, overwrite: policy == ExtractionCollisionPolicy.Overwrite);
                }
                finally { if (File.Exists(partial)) File.Delete(partial); }
                progress?.Report(new OperationProgressInfo { Stage = "Extracting BILZ", CurrentItem = e.Path,
                    ItemsCompleted = i + 1, TotalItems = files.Length, BytesProcessed = i + 1, TotalBytes = files.Length });
            }
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    public Task<string> ExtractPreviewAsync(string path, string entry, CancellationToken token)
        => Task.Run(() =>
        {
            using var resolved = BilzVolumeService.Resolve(path, token);
            path = resolved.Path;
            var item = BilzArchive.List(path).FirstOrDefault(e => e.Path.Equals(entry, StringComparison.Ordinal));
            if (item is null || item.IsDirectory) throw new FileNotFoundException("BILZ entry not found", entry);
            if (item.OriginalSize > 512L * 1024 * 1024) throw new IOException("Preview file exceeds 512 MiB.");
            var all = BilzArchive.List(path);
            ArchiveSafetyPolicy.CheckPlan(all.Count, all.Where(e => !e.IsDirectory).Sum(e => e.OriginalSize));
            string root = Path.Combine(Path.GetTempPath(), "BiloraZIP-Preview", Guid.NewGuid().ToString("N"));
            // Experimental format extracts and verifies entire archive before preview.
            // This also handles folder hierarchy safely.
            BilzArchive.Extract(path, root);
            token.ThrowIfCancellationRequested();
            return Path.Combine(root, entry.Replace('/', Path.DirectorySeparatorChar));
        }, token);

    private static string Pretty(long bytes) => bytes < 1024 ? $"{bytes} B" : bytes < 1024 * 1024 ? $"{bytes / 1024d:0.0} KB" : $"{bytes / (1024d * 1024):0.0} MB";
}
