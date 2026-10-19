using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Bilora.BarmCodec;

public sealed record BilzEntry(string Path, bool IsDirectory, long OriginalSize, long StoredSize);

/// <summary>
/// BILZ v1: experimental multi-file container storing independent BARM-1 streams.
/// No encryption, symlinks, hardlinks, overwrites, or solid compression.
/// See spec/BILZ-1.md. Treat archives from unknown sources as untrusted.
/// </summary>
public static class BilzArchive
{
    private static readonly byte[] Magic = "BILZ"u8.ToArray();
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private const int MaxEntries = 100_000;
    private const int MaxPathBytes = 4096;
    private const long MaxFileBytes = 1L << 30; // Prototype safety limit: 1 GiB/file
    private const long MaxTotalBytes = 4L << 30;
    private const long MaxPayloadBytes = 1L << 31;

    private sealed record InputEntry(string FullPath, string ArchivePath, bool IsDirectory);
    private sealed record EntryHeader(string Path, bool IsDirectory, long RawSize, long PayloadSize, byte[] Sha256);

    public static void Create(string outputPath, IEnumerable<string> inputPaths,
        BarmCompressionProfile profile = BarmCompressionProfile.Maximum)
    {
        ArgumentNullException.ThrowIfNull(outputPath);
        ArgumentNullException.ThrowIfNull(inputPaths);
        if (!Enum.IsDefined(profile)) throw new ArgumentOutOfRangeException(nameof(profile));
        string output = Path.GetFullPath(outputPath);
        if (File.Exists(output) || Directory.Exists(output))
            throw new IOException("Output archive already exists. No files were overwritten.");
        string parent = Path.GetDirectoryName(output)!;
        Directory.CreateDirectory(parent);
        List<InputEntry> entries = GatherInputs(inputPaths, output);
        if (entries.Count == 0) throw new ArgumentException("Choose at least one file or directory.");

        string tempOutput = Path.Combine(parent, ".bilz-create-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var result = new FileStream(tempOutput, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(result, StrictUtf8, leaveOpen: true))
            {
                writer.Write(Magic);
                writer.Write((byte)1); // version
                writer.Write((byte)0); // flags
                writer.Write((uint)entries.Count);
                foreach (InputEntry item in entries)
                {
                    if (item.IsDirectory)
                    {
                        WriteHeader(writer, item.ArchivePath, true, 0, 0, new byte[32]);
                        continue;
                    }

                    // Hash first, encode separately. Store payload in a temporary file
                    // to avoid buffering large files in memory or requiring backpatches.
                    byte[] digest;
                    long rawSize;
                    string tempData = Path.Combine(parent, ".bilz-data-" + Guid.NewGuid().ToString("N") + ".tmp");
                    try
                    {
                        using (var input = OpenSafeInput(item.FullPath))
                        {
                            rawSize = input.Length;
                            if (rawSize > MaxFileBytes) throw new InvalidDataException("Input file exceeds prototype 1 GiB limit.");
                            digest = SHA256.HashData(input);
                            input.Position = 0; // reuse the same read-only shared handle
                            using var compressed = new FileStream(tempData, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                            BarmCodec.Compress(input, compressed, profile: profile);
                        }

                        long storedSize = new FileInfo(tempData).Length;
                        if (storedSize > MaxPayloadBytes) throw new InvalidDataException("Stored payload is too large.");
                        WriteHeader(writer, item.ArchivePath, false, rawSize, storedSize, digest);
                        using var readCompressed = File.OpenRead(tempData);
                        readCompressed.CopyTo(result);
                    }
                    finally
                    {
                        if (File.Exists(tempData)) File.Delete(tempData);
                    }
                }
                writer.Flush();
            }
            File.Move(tempOutput, output); // never overwrite
        }
        finally
        {
            if (File.Exists(tempOutput)) File.Delete(tempOutput);
        }
    }

    public static IReadOnlyList<BilzEntry> List(string archivePath)
    {
        using var input = File.OpenRead(archivePath);
        using var reader = new BinaryReader(input, StrictUtf8, leaveOpen: true);
        int count = ReadArchiveHeader(reader);
        var result = new List<BilzEntry>(count);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        for (int i = 0; i < count; i++)
        {
            EntryHeader entry = ReadEntryHeader(reader);
            CheckEntry(entry, names, ref total);
            result.Add(new BilzEntry(entry.Path, entry.IsDirectory, entry.RawSize, entry.PayloadSize));
            if (entry.PayloadSize > input.Length - input.Position)
                throw new InvalidDataException("Truncated BILZ file payload.");
            input.Seek(entry.PayloadSize, SeekOrigin.Current);
        }
        if (input.Position != input.Length) throw new InvalidDataException("Unexpected data after BILZ entries.");
        return result;
    }

    public static void Test(string archivePath) => ReadAndVerify(archivePath, null);

    public static void Extract(string archivePath, string destinationPath)
    {
        string dest = Path.GetFullPath(destinationPath);
        if (Directory.Exists(dest) || File.Exists(dest))
            throw new IOException("Extraction target already exists. Choose a new folder.");
        string parent = Path.GetDirectoryName(dest)!;
        Directory.CreateDirectory(parent);
        string staging = Path.Combine(parent, ".bilz-extract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            ReadAndVerify(archivePath, staging);
            Directory.Move(staging, dest); // publish only after all files verified
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }

    private static void ReadAndVerify(string archivePath, string? extractRoot)
    {
        using var input = File.OpenRead(archivePath);
        using var reader = new BinaryReader(input, StrictUtf8, leaveOpen: true);
        int count = ReadArchiveHeader(reader);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (int _ in Enumerable.Range(0, count))
        {
            EntryHeader entry = ReadEntryHeader(reader);
            CheckEntry(entry, names, ref total);
            if (entry.IsDirectory)
            {
                if (extractRoot is not null)
                    Directory.CreateDirectory(ToDestination(extractRoot, entry.Path));
                continue;
            }
            if (entry.PayloadSize > input.Length - input.Position)
                throw new InvalidDataException("Truncated BILZ file payload.");
            using var payload = new BoundedReadStream(input, entry.PayloadSize);
            string? target = extractRoot is null ? null : ToDestination(extractRoot, entry.Path);
            if (target is not null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (File.Exists(target) || Directory.Exists(target))
                    throw new InvalidDataException("Duplicate or conflicting path in archive.");
            }
            using var sink = target is null
                ? Stream.Null
                : new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var hashingOutput = new HashingWriteStream(sink);
            BarmCodec.Decompress(payload, hashingOutput, (ulong)entry.RawSize);
            hashingOutput.Flush();
            if (payload.Remaining != 0 || hashingOutput.BytesWritten != entry.RawSize ||
                !CryptographicOperations.FixedTimeEquals(hashingOutput.FinalDigest(), entry.Sha256))
                throw new InvalidDataException($"BILZ integrity check failed: {entry.Path}");
        }
        if (input.Position != input.Length) throw new InvalidDataException("Unexpected data after BILZ entries.");
    }

    private static string ToDestination(string root, string relativePath)
    {
        // Paths have already been validated to exclude parent traversal and separators
        // other than '/'. Staging root is freshly created, with no archive symlink support.
        string fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        string result = Path.GetFullPath(Path.Combine(root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!result.StartsWith(fullRoot, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("Archive path escapes extraction directory.");
        return result;
    }

    private static FileStream OpenSafeInput(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException("Symlink or reparse point input is not supported.");
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    private static List<InputEntry> GatherInputs(IEnumerable<string> inputPaths, string archiveOutput)
    {
        var entries = new List<InputEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        string[] paths = inputPaths.Select(Path.GetFullPath).Distinct(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).ToArray();
        foreach (string source in paths)
        {
            if (!File.Exists(source) && !Directory.Exists(source))
                throw new FileNotFoundException("Input does not exist", source);
            string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(source));
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Cannot archive a filesystem root.");
            Visit(source, name);
        }
        return entries;

        void Visit(string fullPath, string archivePath)
        {
            ValidatePath(archivePath);
            if (!seen.Add(archivePath)) throw new InvalidDataException("Duplicate archive entry name: " + archivePath);
            if (entries.Count >= MaxEntries) throw new InvalidDataException("Too many archive entries.");
            FileAttributes attributes = File.GetAttributes(fullPath);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Symbolic links/reparse points are not supported.");
            if ((attributes & FileAttributes.Directory) != 0)
            {
                entries.Add(new InputEntry(fullPath, archivePath, true));
                foreach (string child in Directory.EnumerateFileSystemEntries(fullPath).OrderBy(x => x, StringComparer.Ordinal))
                    Visit(child, archivePath + "/" + Path.GetFileName(child));
            }
            else
            {
                if (string.Equals(Path.GetFullPath(fullPath), archiveOutput,
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    throw new InvalidDataException("Archive output cannot also be an input file.");
                long size = new FileInfo(fullPath).Length;
                if (size > MaxFileBytes || total > MaxTotalBytes - size)
                    throw new InvalidDataException("Input size exceeds prototype safety limits.");
                total += size;
                entries.Add(new InputEntry(fullPath, archivePath, false));
            }
        }
    }

    private static void WriteHeader(BinaryWriter writer, string path, bool isDirectory,
        long rawSize, long payloadSize, byte[] hash)
    {
        byte[] name = StrictUtf8.GetBytes(path);
        if (name.Length is < 1 or > MaxPathBytes) throw new InvalidDataException("Invalid path byte count.");
        writer.Write((byte)(isDirectory ? 1 : 0));
        writer.Write((ushort)name.Length);
        writer.Write(name);
        writer.Write((ulong)rawSize);
        writer.Write((ulong)payloadSize);
        writer.Write(hash);
    }

    private static int ReadArchiveHeader(BinaryReader reader)
    {
        if (!reader.ReadBytes(4).AsSpan().SequenceEqual(Magic))
            throw new InvalidDataException("Invalid BILZ signature.");
        if (reader.ReadByte() != 1 || reader.ReadByte() != 0)
            throw new InvalidDataException("Unsupported BILZ version or flags.");
        uint count = reader.ReadUInt32();
        if (count > MaxEntries) throw new InvalidDataException("Too many BILZ entries.");
        return (int)count;
    }

    private static EntryHeader ReadEntryHeader(BinaryReader reader)
    {
        byte kind = reader.ReadByte();
        ushort nameSize = reader.ReadUInt16();
        if (kind > 1 || nameSize is < 1 or > MaxPathBytes)
            throw new InvalidDataException("Invalid BILZ entry header.");
        byte[] nameBytes = reader.ReadBytes(nameSize);
        if (nameBytes.Length != nameSize) throw new InvalidDataException("Truncated entry name.");
        string name = StrictUtf8.GetString(nameBytes);
        ValidatePath(name);
        ulong rawSize = reader.ReadUInt64();
        ulong dataSize = reader.ReadUInt64();
        byte[] hash = reader.ReadBytes(32);
        if (hash.Length != 32 || rawSize > (ulong)MaxFileBytes || dataSize > (ulong)MaxPayloadBytes)
            throw new InvalidDataException("Entry sizes or SHA256 invalid.");
        if (kind == 1 && (rawSize != 0 || dataSize != 0 || hash.Any(b => b != 0)))
            throw new InvalidDataException("Invalid directory metadata.");
        if (kind == 0 && dataSize < 18)
            throw new InvalidDataException("Missing BARM stream.");
        return new EntryHeader(name, kind == 1, (long)rawSize, (long)dataSize, hash);
    }

    private static void CheckEntry(EntryHeader entry, HashSet<string> names, ref long total)
    {
        if (!names.Add(entry.Path)) throw new InvalidDataException("Duplicate entry: " + entry.Path);
        if (total > MaxTotalBytes - entry.RawSize)
            throw new InvalidDataException("BILZ output exceeds 4 GiB prototype limit.");
        total += entry.RawSize;
    }

    private static void ValidatePath(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith('/') || name.Contains('\\') ||
            name.Contains(':') || name.IndexOfAny(['\0', '"', '<', '>', '|', '?', '*']) >= 0)
            throw new InvalidDataException("Unsafe BILZ entry path.");
        string[] segments = name.Split('/');
        foreach (string segment in segments)
        {
            if (segment is "" or "." or ".." || segment.EndsWith(' ') || segment.EndsWith('.') ||
                segment.Any(ch => ch < 32))
                throw new InvalidDataException("Unsafe BILZ path segment.");
            string first = segment.Split('.')[0].ToUpperInvariant();
            if (first is "CON" or "PRN" or "AUX" or "NUL" ||
                (first.Length == 4 && (first.StartsWith("COM") || first.StartsWith("LPT")) &&
                 first[3] is >= '1' and <= '9'))
                throw new InvalidDataException("Windows device names are not supported.");
        }
        if (StrictUtf8.GetByteCount(name) > MaxPathBytes)
            throw new InvalidDataException("BILZ entry path too long.");
    }

    private sealed class BoundedReadStream(Stream source, long limit) : Stream
    {
        public long Remaining { get; private set; } = limit;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            if (Remaining == 0) return 0;
            int got = source.Read(buffer[..(int)Math.Min(Remaining, buffer.Length)]);
            Remaining -= got;
            return got;
        }
        public override int ReadByte()
        {
            if (Remaining == 0) return -1;
            int value = source.ReadByte();
            if (value >= 0) Remaining--;
            return value;
        }
        protected override void Dispose(bool disposing) { /* archive stream belongs to caller */ }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class HashingWriteStream(Stream target) : Stream
    {
        private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        public long BytesWritten { get; private set; }
        public byte[] FinalDigest() => hash.GetHashAndReset();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            hash.AppendData(buffer);
            target.Write(buffer);
            BytesWritten += buffer.Length;
        }
        public override void Flush() => target.Flush();
        protected override void Dispose(bool disposing)
        {
            if (disposing) hash.Dispose();
            base.Dispose(disposing);
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
