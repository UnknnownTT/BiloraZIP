using System.IO;
using System.IO.Compression;
using System.Text;
using BiloraZIP.Models;

namespace BiloraZIP.Services;

// Best-effort recovery: a ZIP with missing central directory cannot be opened here.
// Never replaces the original. Decompressed content is never executed.
public sealed class ZipRecoveryService
{
    private const long MaxEntrySize = 512L * 1024 * 1024;
    private const long MaxTotalSize = 2L * 1024 * 1024 * 1024;

    public Task<string> RecoverAsync(string original, IProgress<OperationProgressInfo>? progress, CancellationToken token)
        => Task.Run(() => Recover(original, progress, token), token);

    private static string Recover(string original, IProgress<OperationProgressInfo>? progress, CancellationToken token)
    {
        if (!original.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Best-effort recovery currently supports ZIP only.");
        // Recovery creates a new unencrypted ZIP. Refuse encrypted/mixed inputs
        // instead of silently publishing recovered plaintext entries.
        if (new ArchiveReadService().NeedsPassword(original))
            throw new InvalidOperationException("Recovery of encrypted or mixed-encryption ZIP archives is blocked to avoid creating unprotected copies.");

        string folder = Path.GetDirectoryName(original) ?? throw new DirectoryNotFoundException();
        string target = Path.Combine(folder, Path.GetFileNameWithoutExtension(original) + ".recovered.zip");
        if (File.Exists(target)) throw new IOException("Recovered archive already exists: " + target);

        string temp = Path.Combine(folder, ".bilorazip-recovery-" + Guid.NewGuid().ToString("N") + ".tmp");
        var report = new StringBuilder("BiloraZIP recovery report" + Environment.NewLine);
        int restored = 0;
        long processed = 0;

        try
        {
            using (var input = ZipFile.OpenRead(original))
            using (var output = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                var entries = input.Entries.Where(e => !e.FullName.EndsWith('/')).ToArray();
                int i = 0;
                foreach (var entry in entries)
                {
                    token.ThrowIfCancellationRequested();
                    string relative = entry.FullName.Replace('\\', '/');
                    try { ArchiveEntryPathPolicy.Validate(relative); }
                    catch (InvalidDataException)
                    {
                        report.AppendLine("SKIPPED unsafe path: " + entry.FullName);
                        continue;
                    }
                    if (entry.Length > MaxEntrySize || processed + entry.Length > MaxTotalSize)
                    {
                        report.AppendLine("SKIPPED oversized entry: " + entry.FullName);
                        continue;
                    }

                    string tmpEntry = Path.Combine(Path.GetTempPath(), "bilorazip-entry-" + Guid.NewGuid().ToString("N") + ".tmp");
                    try
                    {
                        // Decode the whole entry successfully before adding to the recovered ZIP.
                        using (var src = entry.Open())
                        using (var staging = new FileStream(tmpEntry, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            byte[] buffer = new byte[128 * 1024];
                            long count = 0;
                            while (true)
                            {
                                token.ThrowIfCancellationRequested();
                                int n = src.Read(buffer, 0, buffer.Length);
                                if (n == 0) break;
                                count += n;
                                if (count > MaxEntrySize) throw new InvalidDataException("Entry exceeds recovery size limit.");
                                staging.Write(buffer, 0, n);
                            }
                            if (count != entry.Length) throw new InvalidDataException("Entry size mismatch.");
                        }
                        var restoredEntry = output.CreateEntry(relative, CompressionLevel.Optimal);
                        using (var src = File.OpenRead(tmpEntry))
                        using (var dst = restoredEntry.Open()) src.CopyTo(dst);
                        processed += entry.Length;
                        restored++;
                        report.AppendLine("RESTORED: " + relative);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException)
                    {
                        report.AppendLine("SKIPPED corrupt/unreadable entry: " + relative + " — " + ex.GetType().Name);
                    }
                    finally { if (File.Exists(tmpEntry)) File.Delete(tmpEntry); }
                    i++;
                    progress?.Report(new OperationProgressInfo {
                        Stage="Recovering readable entries", CurrentItem=entry.FullName,
                        ItemsCompleted=i, TotalItems=entries.Length, BytesProcessed=i, TotalBytes=entries.Length });
                }
            }
            token.ThrowIfCancellationRequested();
            if (restored == 0) throw new InvalidDataException("No readable ZIP entries could be recovered.");
            File.Move(temp, target);
            try { File.WriteAllText(target + ".report.txt", report.ToString()); } catch (IOException) { }
            return target;
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
