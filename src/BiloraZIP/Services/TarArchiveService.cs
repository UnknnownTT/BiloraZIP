using System.Formats.Tar;
using System.IO;
using BiloraZIP.Models;

namespace BiloraZIP.Services;

public sealed class TarArchiveService
{
    public Task CreateAsync(string destination, IReadOnlyList<string> files, IProgress<OperationProgressInfo>? progress, CancellationToken ct)
        => Task.Run(() => Create(destination, files, progress, ct), ct);

    public Task CreateMixedAsync(string destination, IReadOnlyCollection<string> sources,
        IProgress<OperationProgressInfo>? progress, CancellationToken ct)
        => Task.Run(() => CreateMixed(destination, sources, progress, ct), ct);

    public Task CreateFolderAsync(string destination, string folder, IProgress<OperationProgressInfo>? progress, CancellationToken ct)
        => Task.Run(() => CreateFolder(destination, folder, progress, ct), ct);

    private static void CreateMixed(string destination, IReadOnlyCollection<string> sources,
        IProgress<OperationProgressInfo>? progress, CancellationToken ct)
    {
        string output = Path.GetFullPath(destination);
        if (File.Exists(output)) throw new IOException("Destination already exists.");
        var (files, dirs) = ArchiveInputPlanner.Build(sources, output);
        string temp = output + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new TarWriter(stream, leaveOpen: false))
            {
                // Directory entries are represented by the parent paths of file entries.
                // Empty folders need explicit directory entries.
                foreach (string dir in dirs)
                {
                    ct.ThrowIfCancellationRequested();
                    var entry = new PaxTarEntry(TarEntryType.Directory, dir);
                    writer.WriteEntry(entry);
                }
                int n = 0;
                foreach (var (source, name) in files)
                {
                    ct.ThrowIfCancellationRequested();
                    writer.WriteEntry(source, name);
                    n++;
                    progress?.Report(new OperationProgressInfo { Stage = "Creating TAR", CurrentItem = name,
                        ItemsCompleted = n, TotalItems = files.Count, BytesProcessed = n, TotalBytes = files.Count });
                }
            }
            ct.ThrowIfCancellationRequested();
            File.Move(temp, output);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static void Create(string destination, IReadOnlyList<string> files, IProgress<OperationProgressInfo>? progress, CancellationToken ct)
    {
        if (files.Count == 0) throw new ArgumentException("Select files first.");
        if (File.Exists(destination)) throw new IOException("Destination already exists.");
        if (files.Any(f => !File.Exists(f))) throw new FileNotFoundException("Source file is missing.");
        var names = files.Select(Path.GetFileName).ToList();
        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)
            throw new IOException("Duplicate file names cannot coexist at archive root.");
        string temp = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
        long total = files.Sum(f => new FileInfo(f).Length), done = 0;
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new TarWriter(stream, leaveOpen: false))
            {
                for (int i = 0; i < files.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    writer.WriteEntry(files[i], Path.GetFileName(files[i]));
                    done += new FileInfo(files[i]).Length;
                    progress?.Report(new OperationProgressInfo { Stage="Creating TAR", CurrentItem=files[i], ItemsCompleted=i+1, TotalItems=files.Count, BytesProcessed=done, TotalBytes=total });
                }
            }
            ct.ThrowIfCancellationRequested();
            File.Move(temp, destination);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private static void CreateFolder(string destination, string folder, IProgress<OperationProgressInfo>? progress, CancellationToken ct)
    {
        // Avoid TarFile.CreateFromDirectory following reparse points unexpectedly.
        CreateMixed(destination, [folder], progress, ct);
    }
}
