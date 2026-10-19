using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace BiloraZIP.Services;

/// <summary>
/// Experimental multi-volume transport for complete BILZ-1 files.
/// Every part has a checked header, per-part SHA-256 and the original archive SHA-256.
/// This is not a new compression algorithm; the original BILZ bytes are reassembled
/// and verified before any archive data is listed, tested, previewed or extracted.
/// </summary>
public static class BilzVolumeService
{
    private static readonly byte[] Magic = Encoding.ASCII.GetBytes("BILZVOL1");
    private const int HeaderSize = 100;
    private const int MaxParts = 9999;
    private const long MaxTotalLength = 1024L * 1024 * 1024 * 1024; // 1 TiB safety cap

    public static bool IsFirstVolume(string path) => path.EndsWith(".bilz.001", StringComparison.OrdinalIgnoreCase);

    public static async Task<string> SplitAsync(string archivePath, int volumeMiB, CancellationToken ct)
    {
        if (volumeMiB is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(volumeMiB));
        string original = Path.GetFullPath(archivePath);
        if (!original.EndsWith(".bilz", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Expected a .bilz archive.");
        long chunkSize = checked(volumeMiB * 1024L * 1024);
        var info = new FileInfo(original);
        if (!info.Exists || info.Length <= 0 || info.Length > MaxTotalLength) throw new InvalidDataException("Invalid BILZ archive length.");
        int count = checked((int)((info.Length + chunkSize - 1) / chunkSize));
        if (count > MaxParts) throw new InvalidDataException("Too many BILZ parts.");
        var targets = Enumerable.Range(1, count).Select(i => original + "." + i.ToString("D3")).ToArray();
        if (targets.Any(File.Exists)) throw new IOException("A destination BILZ volume already exists. Nothing was overwritten.");
        var tempFiles = new List<string>();
        var published = new List<string>();
        try
        {
            byte[] archiveHash;
            await using (var source = new FileStream(original, FileMode.Open, FileAccess.Read, FileShare.Read))
                archiveHash = await SHA256.HashDataAsync(source, ct);
            await using (var input = new FileStream(original, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var buffer = new byte[128 * 1024];
                for (int index = 1; index <= count; index++)
                {
                    ct.ThrowIfCancellationRequested();
                    string temp = targets[index - 1] + "." + Guid.NewGuid().ToString("N") + ".partial";
                    tempFiles.Add(temp);
                    long expected = Math.Min(chunkSize, input.Length - input.Position);
                    await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                    {
                        using (var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true))
                        {
                            writer.Write(Magic);
                            writer.Write(1); // header version
                            writer.Write(index);
                            writer.Write(count);
                            writer.Write(info.Length);
                            writer.Write(expected); // Int64 payload length supports up to 4096 MiB
                            writer.Write(archiveHash);
                            writer.Write(new byte[32]); // SHA-256 filled after streaming
                        }
                        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                        long remaining = expected;
                        while (remaining > 0)
                        {
                            ct.ThrowIfCancellationRequested();
                            int n = await input.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), ct);
                            if (n == 0) throw new EndOfStreamException("BILZ archive shortened during splitting.");
                            await output.WriteAsync(buffer.AsMemory(0, n), ct);
                            hash.AppendData(buffer.AsSpan(0, n));
                            remaining -= n;
                        }
                        output.Position = HeaderSize - 32;
                        await output.WriteAsync(hash.GetHashAndReset(), ct);
                        await output.FlushAsync(ct);
                    }
                }
                if (input.Position != input.Length) throw new InvalidDataException("BILZ archive changed while splitting.");
            }
            ct.ThrowIfCancellationRequested();
            for (int i = 0; i < count; i++)
            {
                File.Move(tempFiles[i], targets[i]);
                published.Add(targets[i]);
            }
            File.Delete(original); // only after every volume has been published
            return targets[0];
        }
        catch
        {
            foreach (string path in published) { try { File.Delete(path); } catch (IOException) { } }
            throw;
        }
        finally
        {
            foreach (string path in tempFiles) { try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } }
        }
    }

    public static ResolvedArchive Resolve(string path, CancellationToken ct = default)
    {
        if (!IsFirstVolume(path)) return new ResolvedArchive(Path.GetFullPath(path), null);
        string first = Path.GetFullPath(path);
        string basePath = first[..^4]; // remove ".001"
        string tempRoot = Path.Combine(Path.GetTempPath(), "BiloraZIP-Volumes", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        string assembled = Path.Combine(tempRoot, "verified.bilz");
        try
        {
            using var output = new FileStream(assembled, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var totalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            int count = 0;
            long totalLength = -1;
            long written = 0;
            byte[]? expectedArchiveHash = null;
            byte[] buffer = new byte[128 * 1024];
            for (int i = 1; i <= (count == 0 ? 1 : count); i++)
            {
                ct.ThrowIfCancellationRequested();
                string part = basePath + "." + i.ToString("D3");
                using var input = new FileStream(part, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (input.Length < HeaderSize) throw new InvalidDataException("Truncated BILZ volume: " + part);
                using var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
                if (!reader.ReadBytes(8).AsSpan().SequenceEqual(Magic)) throw new InvalidDataException("Invalid BILZ volume header: " + part);
                int version = reader.ReadInt32();
                int index = reader.ReadInt32();
                int partCount = reader.ReadInt32();
                long length = reader.ReadInt64();
                long partLength = reader.ReadInt64();
                byte[] fullHash = reader.ReadBytes(32);
                byte[] partHash = reader.ReadBytes(32);
                if (version != 1 || index != i || partCount is < 1 or > MaxParts || length is <= 0 or > MaxTotalLength ||
                    partLength <= 0 || partLength > 4096L * 1024 * 1024 || input.Length != HeaderSize + partLength)
                    throw new InvalidDataException("Invalid BILZ volume size or index: " + part);
                if (count == 0) { count = partCount; totalLength = length; expectedArchiveHash = fullHash; }
                else if (count != partCount || totalLength != length || !fullHash.AsSpan().SequenceEqual(expectedArchiveHash))
                    throw new InvalidDataException("BILZ parts belong to different archives.");
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                int n;
                while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    hash.AppendData(buffer.AsSpan(0, n));
                    totalHash.AppendData(buffer.AsSpan(0, n));
                    output.Write(buffer, 0, n);
                    written = checked(written + n);
                    if (written > totalLength) throw new InvalidDataException("BILZ volume data exceeds expected length.");
                }
                if (!hash.GetHashAndReset().AsSpan().SequenceEqual(partHash))
                    throw new InvalidDataException("BILZ volume SHA-256 mismatch: " + part);
            }
            output.Flush();
            if (written != totalLength || !totalHash.GetHashAndReset().AsSpan().SequenceEqual(expectedArchiveHash))
                throw new InvalidDataException("Reassembled BILZ archive SHA-256 mismatch.");
            return new ResolvedArchive(assembled, tempRoot);
        }
        catch
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch (IOException) { }
            throw;
        }
    }

    public sealed class ResolvedArchive(string path, string? tempDirectory) : IDisposable
    {
        public string Path { get; } = path;
        public void Dispose()
        {
            if (tempDirectory is not null)
            {
                try { Directory.Delete(tempDirectory, recursive: true); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
