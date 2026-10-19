using System.IO;
using System.IO.Compression;
using System.Text;
using BiloraZIP.Models;

namespace BiloraZIP.Services;

public sealed class ZipArchiveService
{
    private const int BufferSize = 128 * 1024;

    public IReadOnlyList<ArchiveEntryInfo> List(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        return zip.Entries.Select(entry => new ArchiveEntryInfo(
            entry.FullName,
            IsDirectory(entry) ? "—" : FormatBytes(entry.Length),
            IsDirectory(entry) ? "—" : FormatBytes(entry.CompressedLength),
            IsDirectory(entry) ? "Folder" : "File",
            IsDirectory(entry) || entry.Length == 0 ? "—" : $"{Math.Max(0, 100d * (1 - entry.CompressedLength / (double)entry.Length)):0}%",
            entry.LastWriteTime == default ? "—" : entry.LastWriteTime.LocalDateTime.ToString("yyyy-MM-dd HH:mm")
        )).ToList();
    }

    public Task CreateAsync(string outputPath, IReadOnlyList<string> inputFiles, CompressionLevel level, IProgress<OperationProgressInfo>? progress, CancellationToken token)
        => Task.Run(() => Create(outputPath, inputFiles, level, progress, token), token);

    public Task CreateMixedAsync(string outputPath, IReadOnlyCollection<string> sources, CompressionLevel level,
        IProgress<OperationProgressInfo>? progress, CancellationToken token)
        => Task.Run(() => CreateMixed(outputPath, sources, level, progress, token), token);

    public Task CreateFolderAsync(string outputPath, string sourceFolder, CompressionLevel level, IProgress<OperationProgressInfo>? progress, CancellationToken token)
        => Task.Run(() => CreateFolder(outputPath, sourceFolder, level, progress, token), token);

    public Task ExtractAsync(string archivePath, string destination, IProgress<OperationProgressInfo>? progress, CancellationToken token)
        => Task.Run(() => Extract(archivePath, destination, progress, token), token);

    public Task TestArchiveAsync(string archivePath, IProgress<OperationProgressInfo>? progress, CancellationToken token)
        => Task.Run(() => TestArchive(archivePath, progress, token), token);

    public Task AddFilesAndFoldersAsync(string archivePath, IReadOnlyCollection<string> sources,
        IProgress<OperationProgressInfo>? progress, CancellationToken token, string entryPrefix = "")
        => Task.Run(() => AddFilesAndFolders(archivePath, sources, progress, token, entryPrefix), token);

    public Task DeleteEntryAsync(string archivePath, string entryName, IProgress<OperationProgressInfo>? progress, CancellationToken token)
        => Task.Run(() => DeleteEntry(archivePath, entryName, progress, token), token);

    public string ReadTextPreview(string archivePath, string entryName)
    {
        using var zip = ZipFile.OpenRead(archivePath);
        var entry = zip.GetEntry(entryName) ?? throw new FileNotFoundException("Entry not found in archive.");
        if (IsDirectory(entry)) throw new InvalidOperationException("Folders cannot be previewed.");
        if (entry.Length > 1024 * 1024) throw new InvalidDataException("Text preview is limited to files up to 1 MB.");
        string[] extensions = [".txt", ".md", ".csv", ".json", ".xml", ".log", ".ini", ".yaml", ".yml", ".cs", ".xaml", ".ps1"];
        if (!extensions.Contains(Path.GetExtension(entry.Name), StringComparer.OrdinalIgnoreCase))
            throw new NotSupportedException("Only small plain-text files can be previewed in this version.");
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        char[] preview = new char[1024 * 1024 + 1];
        int count = 0;
        while (count < preview.Length)
        {
            int n = reader.Read(preview, count, preview.Length - count);
            if (n == 0) break;
            count += n;
        }
        if (count > 1024 * 1024) throw new InvalidDataException("Preview exceeds the 1 MB limit.");
        string content = new string(preview, 0, count);
        if (content.Contains('\0')) throw new InvalidDataException("This file does not appear to contain plain text.");
        return content;
    }

    private void CreateMixed(string outputPath, IReadOnlyCollection<string> sources, CompressionLevel level,
        IProgress<OperationProgressInfo>? progress, CancellationToken token)
    {
        string output = Path.GetFullPath(outputPath);
        if (File.Exists(output)) throw new IOException("Destination archive already exists.");
        var (files, directories) = ArchiveInputPlanner.Build(sources, output);
        long total = files.Sum(f => new FileInfo(f.Source).Length), done = 0;
        int completed = 0;
        string temp = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                foreach (string dir in directories) { token.ThrowIfCancellationRequested(); zip.CreateEntry(dir); }
                foreach (var (file, name) in files)
                {
                    token.ThrowIfCancellationRequested();
                    var info = new FileInfo(file);
                    var entry = zip.CreateEntry(name, level);
                    entry.LastWriteTime = info.LastWriteTime;
                    using var input = File.OpenRead(file);
                    using var stream = entry.Open();
                    CopyWithProgress(input, stream, total, ref done, name, completed, files.Count, progress, token, "Compressing");
                    completed++;
                    Report(progress, "Compressing", name, completed, files.Count, done, total);
                }
            }
            token.ThrowIfCancellationRequested();
            File.Move(temp, output);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private void Create(string outputPath, IReadOnlyList<string> inputFiles, CompressionLevel level, IProgress<OperationProgressInfo>? progress, CancellationToken token)
    {
        if (inputFiles.Count == 0) throw new InvalidOperationException("Select at least one file.");
        string output = Path.GetFullPath(outputPath);
        if (File.Exists(output)) throw new IOException("An archive with that name already exists.");
        if (inputFiles.Any(p => Path.GetFullPath(p).Equals(output, StringComparison.OrdinalIgnoreCase)))
            throw new IOException("The output ZIP cannot contain itself.");
        if (inputFiles.Any(p => !File.Exists(p))) throw new FileNotFoundException("One or more source files are missing.");
        if (inputFiles.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count() != inputFiles.Count)
            throw new IOException("Two selected files have the same name. Select files with unique names.");
        long total = inputFiles.Sum(f => new FileInfo(f).Length);
        string temp = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
        long done = 0;
        int filesDone = 0;
        try
        {
            using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                foreach (string file in inputFiles)
                {
                    token.ThrowIfCancellationRequested();
                    var info = new FileInfo(file);
                    var entry = zip.CreateEntry(info.Name, level);
                    entry.LastWriteTime = info.LastWriteTime;
                    using var input = File.OpenRead(file);
                    using var outputStream = entry.Open();
                    CopyWithProgress(input, outputStream, total, ref done, info.Name, filesDone, inputFiles.Count, progress, token, "Compressing");
                    filesDone++;
                    Report(progress, "Compressing", info.Name, filesDone, inputFiles.Count, done, total);
                }
            }
            token.ThrowIfCancellationRequested();
            File.Move(temp, output);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private void CreateFolder(string outputPath, string sourceFolder, CompressionLevel level, IProgress<OperationProgressInfo>? progress, CancellationToken token)
    {
        // Share the same safe input planner as mixed selections. Do not silently
        // traverse symlinks/junctions when archiving a folder.
        CreateMixed(outputPath, [sourceFolder], level, progress, token);
    }

    private void Extract(string archivePath, string destination, IProgress<OperationProgressInfo>? progress, CancellationToken token)
    {
        string root = Path.GetFullPath(destination);
        string prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        using var zip = ZipFile.OpenRead(archivePath);
        var plan = new List<(ZipArchiveEntry Entry, string Target, bool Directory)>();
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            string name = entry.FullName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            if (string.IsNullOrWhiteSpace(name)) continue;
            ArchiveEntryPathPolicy.Validate(entry.FullName);
            if (Path.IsPathRooted(name) || name.Contains(':')) throw new InvalidDataException("Unsafe archive path: " + entry.FullName);
            string target = Path.GetFullPath(Path.Combine(root, name));
            if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Archive path traverses outside extraction folder.");
            if (!targets.Add(Path.TrimEndingDirectorySeparator(target))) throw new InvalidDataException("Duplicate archive paths were detected.");
            if (File.Exists(target) || Directory.Exists(target)) throw new IOException("Extraction target already exists: " + target);
            if (IsSymlink(entry)) throw new InvalidDataException("Symbolic links in archives are not supported.");
            plan.Add((entry, target, IsDirectory(entry)));
        }
        var plannedFiles = new HashSet<string>(plan.Where(p => !p.Directory).Select(p => p.Target), StringComparer.OrdinalIgnoreCase);
        foreach (var entry in plan)
        {
            string? parent = Path.GetDirectoryName(entry.Target);
            while (parent is not null && parent.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                if (plannedFiles.Contains(parent)) throw new InvalidDataException("Archive contains conflicting file and folder paths.");
                if (Directory.Exists(parent) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Extraction through an existing symbolic-link folder is blocked.");
                parent = Path.GetDirectoryName(parent);
            }
        }
        ArchiveSafetyPolicy.CheckPlan(plan.Count, plan.Where(p => !p.Directory).Sum(p => p.Entry.Length));
        foreach (var item in plan.Where(p => !p.Directory))
            ArchiveSafetyPolicy.CheckEntry(item.Entry.Length, item.Entry.CompressedLength, item.Entry.FullName);
        long total = plan.Where(p => !p.Directory).Sum(p => p.Entry.Length);
        long done = 0;
        int fileCount = plan.Count(p => !p.Directory);
        int filesDone = 0;
        Directory.CreateDirectory(root);
        foreach (var item in plan)
        {
            token.ThrowIfCancellationRequested();
            if (item.Directory) { Directory.CreateDirectory(item.Target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(item.Target)!);
            try
            {
                using var input = item.Entry.Open();
                using var output = new FileStream(item.Target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                CopyWithProgress(input, output, total, ref done, item.Entry.FullName, filesDone, fileCount, progress, token, "Extracting");
            }
            catch
            {
                if (File.Exists(item.Target)) File.Delete(item.Target);
                throw;
            }
            filesDone++;
            Report(progress, "Extracting", item.Entry.FullName, filesDone, fileCount, done, total);
        }
    }

    private void TestArchive(string archivePath, IProgress<OperationProgressInfo>? progress, CancellationToken token)
    {
        using var zip = ZipFile.OpenRead(archivePath);
        var files = zip.Entries.Where(e => !IsDirectory(e)).ToList();
        long total = files.Sum(e => e.Length);
        long done = 0;
        int count = 0;
        byte[] buffer = new byte[BufferSize];
        foreach (var entry in files)
        {
            token.ThrowIfCancellationRequested();
            using var source = entry.Open();
            long entryBytes = 0;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                int n = source.Read(buffer, 0, buffer.Length);
                if (n == 0) break;
                done += n;
                entryBytes += n;
                Report(progress, "Reading archive", entry.FullName, count, files.Count, done, total);
            }
            if (entryBytes != entry.Length) throw new InvalidDataException("Uncompressed length mismatch: " + entry.FullName);
            count++;
            Report(progress, "Reading archive", entry.FullName, count, files.Count, done, total);
        }
    }

    private void DeleteEntry(string archivePath, string entryName, IProgress<OperationProgressInfo>? progress, CancellationToken token)
    {
        string archive = Path.GetFullPath(archivePath);
        string temp = archive + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using (var source = ZipFile.OpenRead(archive))
        {
            bool folder = entryName.EndsWith('/');
            bool found = source.Entries.Any(e => string.Equals(e.FullName, entryName, StringComparison.Ordinal));
            if (!found) throw new FileNotFoundException("Entry was not found in archive.");
            var entries = source.Entries.Where(e => !(string.Equals(e.FullName, entryName, StringComparison.Ordinal) ||
                                        (folder && e.FullName.StartsWith(entryName, StringComparison.Ordinal)))).ToList();
            long total = entries.Where(e => !IsDirectory(e)).Sum(e => e.Length);
            long done = 0;
            int count = 0;
            try
            {
                using (var output = ZipFile.Open(temp, ZipArchiveMode.Create))
                {
                    foreach (var entry in entries)
                    {
                        token.ThrowIfCancellationRequested();
                        var newEntry = output.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                        newEntry.LastWriteTime = entry.LastWriteTime;
                        if (!IsDirectory(entry))
                        {
                            using var inputStream = entry.Open();
                            using var outputStream = newEntry.Open();
                            CopyWithProgress(inputStream, outputStream, total, ref done, entry.FullName, count, entries.Count, progress, token, "Rebuilding ZIP");
                        }
                        count++;
                        Report(progress, "Rebuilding ZIP", entry.FullName, count, entries.Count, done, total);
                    }
                }
                token.ThrowIfCancellationRequested();
            }
            catch { if (File.Exists(temp)) File.Delete(temp); throw; }
        }
        try { File.Replace(temp, archive, null); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    // Rebuild a temporary copy and only swap it after all streams close and no errors occur.
    // This intentionally permits adding to plain ZIP archives only (never RAR/7Z/encrypted ZIP).
    private void AddFilesAndFolders(string archivePath, IReadOnlyCollection<string> sources,
        IProgress<OperationProgressInfo>? progress, CancellationToken token, string entryPrefix)
    {
        if (sources.Count == 0) return;
        string archive = Path.GetFullPath(archivePath);
        if (!File.Exists(archive)) throw new FileNotFoundException("ZIP archive missing", archive);
        string basePrefix = entryPrefix.Replace('\\', '/').Trim('/');
        if (basePrefix.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(s => s == "." || s == ".." || s.Contains(':')))
            throw new InvalidDataException("Unsafe archive destination.");
        if (basePrefix.Length > 0) basePrefix += "/";
        var additions = new List<(string FilePath, string EntryKey)>();
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string input in sources)
        {
            token.ThrowIfCancellationRequested();
            string source = Path.GetFullPath(input);
            if (string.Equals(source, archive, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Cannot add the archive to itself.");
            if (Directory.Exists(source))
            {
                if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Symbolic-link folders cannot be added by drag/drop.");
                string rootName = Path.GetFileName(Path.TrimEndingDirectorySeparator(source));
                directories.Add(basePrefix + rootName + "/");
                foreach (string childFolder in Directory.EnumerateDirectories(source, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
                {
                    if ((File.GetAttributes(childFolder) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("Symbolic-link folders cannot be added.");
                    directories.Add(basePrefix + rootName + "/" + Path.GetRelativePath(source, childFolder).Replace('\\', '/') + "/");
                }
                foreach (string file in Directory.EnumerateFiles(source, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }))
                    additions.Add((file, basePrefix + rootName + "/" + Path.GetRelativePath(source, file).Replace('\\', '/')));
            }
            else if (File.Exists(source))
                additions.Add((source, basePrefix + Path.GetFileName(source)));
            else throw new FileNotFoundException("Dropped source not found", source);
        }

        using (var original = ZipFile.OpenRead(archive))
        {
            var used = new HashSet<string>(original.Entries.Select(e => e.FullName), StringComparer.OrdinalIgnoreCase);
            foreach (var dir in directories)
                if (!used.Add(dir)) throw new IOException("ZIP already contains: " + dir);
            foreach (var item in additions)
            {
                if ((File.GetAttributes(item.FilePath) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Symbolic-link files cannot be added.");
                if (string.Equals(Path.GetFullPath(item.FilePath), archive, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Cannot add archive to itself.");
                if (!used.Add(item.EntryKey)) throw new IOException("ZIP already contains: " + item.EntryKey);
            }
        }

        string temp = archive + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            token.ThrowIfCancellationRequested();
            File.Copy(archive, temp, overwrite: false);
            using (var zip = ZipFile.Open(temp, ZipArchiveMode.Update))
            {
                foreach (string name in directories) zip.CreateEntry(name);
                long total = additions.Sum(x => new FileInfo(x.FilePath).Length);
                long done = 0;
                int completed = 0;
                foreach (var item in additions)
                {
                    token.ThrowIfCancellationRequested();
                    var entry = zip.CreateEntry(item.EntryKey, CompressionLevel.Optimal);
                    entry.LastWriteTime = File.GetLastWriteTime(item.FilePath);
                    using var input = File.OpenRead(item.FilePath);
                    using var output = entry.Open();
                    CopyWithProgress(input, output, total, ref done, item.EntryKey,
                        completed, additions.Count, progress, token, "Adding to ZIP");
                    completed++;
                    Report(progress, "Adding to ZIP", item.EntryKey, completed, additions.Count, done, total);
                }
            }
            token.ThrowIfCancellationRequested();
            File.Replace(temp, archive, null);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static bool IsDirectory(ZipArchiveEntry entry)
        => entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');

    private static bool IsSymlink(ZipArchiveEntry entry)
    {
        int unixMode = (entry.ExternalAttributes >> 16) & 0xF000;
        return unixMode == 0xA000;
    }

    private static void CopyWithProgress(Stream source, Stream destination, long total, ref long done,
        string current, int completed, int totalFiles, IProgress<OperationProgressInfo>? progress, CancellationToken token, string stage)
    {
        byte[] buffer = new byte[BufferSize];
        while (true)
        {
            token.ThrowIfCancellationRequested();
            int n = source.Read(buffer, 0, buffer.Length);
            if (n == 0) break;
            destination.Write(buffer, 0, n);
            done += n;
            Report(progress, stage, current, completed, totalFiles, done, total);
        }
    }

    private static void Report(IProgress<OperationProgressInfo>? progress, string stage, string current, int count, int totalFiles, long done, long total)
        => progress?.Report(new OperationProgressInfo
        {
            Stage = stage,
            CurrentItem = current,
            ItemsCompleted = count,
            TotalItems = totalFiles,
            BytesProcessed = done,
            TotalBytes = total
        });

    private static string FormatBytes(long value)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double n = value;
        int i = 0;
        while (n >= 1024 && i < sizes.Length - 1) { n /= 1024; i++; }
        return $"{n:0.#} {sizes[i]}";
    }
}
