using System.IO;
using System.IO.Compression;
using BiloraZIP.Models;

namespace BiloraZIP.Services;

/// <summary>Standard gzip-compressed TAR archive writer (.tar.gz). No password encryption.</summary>
public sealed class TarGzipArchiveService
{
    private readonly TarArchiveService _tar = new();

    public Task CreateAsync(string destination, IReadOnlyCollection<string> sources,
        CompressionLevel level, IProgress<OperationProgressInfo>? progress, CancellationToken ct)
        => CreateCoreAsync(destination, level, progress, ct,
            temp => _tar.CreateMixedAsync(temp, sources, progress, ct));

    public Task CreateFolderAsync(string destination, string folder,
        CompressionLevel level, IProgress<OperationProgressInfo>? progress, CancellationToken ct)
        => CreateCoreAsync(destination, level, progress, ct,
            temp => _tar.CreateFolderAsync(temp, folder, progress, ct));

    private static async Task CreateCoreAsync(string destination, CompressionLevel level,
        IProgress<OperationProgressInfo>? progress, CancellationToken ct, Func<string, Task> writeTar)
    {
        string target = Path.GetFullPath(destination);
        if (File.Exists(target)) throw new IOException("Archive destination already exists.");
        string tarTemp = target + "." + Guid.NewGuid().ToString("N") + ".tar";
        string gzipTemp = target + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            await writeTar(tarTemp);
            ct.ThrowIfCancellationRequested();
            await using (var input = new FileStream(tarTemp, FileMode.Open, FileAccess.Read, FileShare.Read))
            await using (var output = new FileStream(gzipTemp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await using (var gzip = new GZipStream(output, level, leaveOpen: true))
            {
                var buffer = new byte[128 * 1024];
                long done = 0;
                int count;
                while ((count = await input.ReadAsync(buffer, ct)) > 0)
                {
                    await gzip.WriteAsync(buffer.AsMemory(0, count), ct);
                    done += count;
                    progress?.Report(new OperationProgressInfo { Stage = "Compressing TAR.GZ",
                        CurrentItem = Path.GetFileName(destination), BytesProcessed = done, TotalBytes = input.Length,
                        ItemsCompleted = 0, TotalItems = 1 });
                }
            }
            ct.ThrowIfCancellationRequested();
            File.Move(gzipTemp, target);
            progress?.Report(new OperationProgressInfo { Stage = "TAR.GZ created",
                ItemsCompleted = 1, TotalItems = 1, BytesProcessed = 1, TotalBytes = 1 });
        }
        finally
        {
            if (File.Exists(tarTemp)) File.Delete(tarTemp);
            if (File.Exists(gzipTemp)) File.Delete(gzipTemp);
        }
    }
}
