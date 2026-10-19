using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Bilora.BarmCodec;

/// <summary>
/// BARM-1: experimental, lossless, independently implemented Bilora codec.
/// This is a single-stream codec, embedded by the experimental BILZ container.
/// The format is documented in spec/BARM-1.md.
/// </summary>
public enum BarmCompressionProfile { Fast, Maximum }

public static class BarmCodec
{
    private static readonly byte[] Magic = [(byte)'B', (byte)'A', (byte)'R', (byte)'M'];
    private static readonly uint[] CrcTable = MakeCrcTable();
    public const int DefaultBlockSize = 65536;
    public const ulong DefaultMaxExtractedBytes = 1UL << 30;

    public static void Compress(Stream input, Stream output, int blockSize = DefaultBlockSize,
        BarmCompressionProfile profile = BarmCompressionProfile.Maximum)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        if (!input.CanRead || !input.CanSeek || !output.CanWrite)
            throw new ArgumentException("Compression requires readable, seekable input and writable output.");
        if (ReferenceEquals(input, output))
            throw new ArgumentException("Input and output must be separate streams.");
        if (!Enum.IsDefined(profile))
            throw new ArgumentOutOfRangeException(nameof(profile));
        if (blockSize is < 1 or > DefaultBlockSize)
            throw new ArgumentOutOfRangeException(nameof(blockSize));

        long remaining = input.Length - input.Position;
        if (remaining < 0)
            throw new InvalidDataException("Invalid input stream length.");
        using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write((byte)1);
        writer.Write((uint)blockSize);
        writer.Write((ulong)remaining);
        writer.Write((byte)0);

        byte[] buffer = new byte[blockSize];
        while (remaining > 0)
        {
            int length = (int)Math.Min(remaining, blockSize);
            input.ReadExactly(buffer.AsSpan(0, length));
            byte[] original = buffer.AsSpan(0, length).ToArray();
            byte[] bestPayload = original;
            byte bestTransform = 0;
            byte storage = 0;

            // Fast tries three predictive modes; Maximum tests all seven.
            // Both produce exactly the same BARM-1 decoder-compatible stream.
            byte[] modes = profile == BarmCompressionProfile.Fast
                ? [0, 1, 4] : [0, 1, 2, 3, 4, 5, 6];
            foreach (byte mode in modes)
            {
                byte[] tokenized = EncodeTokens(Transform(original, mode));
                if (tokenized.Length < bestPayload.Length)
                {
                    bestPayload = tokenized;
                    bestTransform = mode;
                    storage = 1;
                }
            }

            writer.Write((uint)length);
            writer.Write(bestTransform);
            writer.Write(storage);
            writer.Write((uint)bestPayload.Length);
            writer.Write(Crc32(original));
            writer.Write(bestPayload);
            remaining -= length;
        }
        writer.Flush();
    }

    public static void Decompress(Stream input, Stream output,
        ulong maxExtractedBytes = DefaultMaxExtractedBytes)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(output);
        if (!input.CanRead || !output.CanWrite)
            throw new ArgumentException("Decompression requires readable input and writable output.");
        if (ReferenceEquals(input, output))
            throw new ArgumentException("Input and output must be separate streams.");
        using var reader = new BinaryReader(input, Encoding.UTF8, leaveOpen: true);
        if (!reader.ReadBytes(4).AsSpan().SequenceEqual(Magic))
            throw new InvalidDataException("Invalid BARM magic.");
        byte version = reader.ReadByte();
        uint blockSize = reader.ReadUInt32();
        ulong totalSize = reader.ReadUInt64();
        byte flags = reader.ReadByte();
        if (version != 1 || flags != 0 || blockSize is < 1 or > DefaultBlockSize)
            throw new InvalidDataException("Unsupported BARM header.");
        if (totalSize > maxExtractedBytes)
            throw new InvalidDataException("Output exceeds configured safety limit.");

        ulong produced = 0;
        while (produced < totalSize)
        {
            uint rawSize = reader.ReadUInt32();
            byte transform = reader.ReadByte();
            byte storage = reader.ReadByte();
            uint payloadSize = reader.ReadUInt32();
            uint expectedCrc = reader.ReadUInt32();

            if (rawSize == 0 || rawSize > blockSize || rawSize > totalSize - produced)
                throw new InvalidDataException("Invalid block length.");
            if (transform > 6 || storage > 1 || payloadSize > rawSize)
                throw new InvalidDataException("Invalid block mode.");
            if (storage == 0 && (transform != 0 || payloadSize != rawSize))
                throw new InvalidDataException("Invalid raw block.");
            if (storage == 1 && payloadSize >= rawSize)
                throw new InvalidDataException("Token stream must be smaller than raw block.");

            byte[] payload = reader.ReadBytes((int)payloadSize);
            if (payload.Length != payloadSize)
                throw new InvalidDataException("Truncated payload.");
            byte[] raw = storage == 0
                ? payload
                : ReverseTransform(DecodeTokens(payload, (int)rawSize), transform);
            if (Crc32(raw) != expectedCrc)
                throw new InvalidDataException("BARM CRC32 mismatch.");
            output.Write(raw);
            produced += rawSize;
        }
        if (reader.BaseStream.ReadByte() != -1)
            throw new InvalidDataException("Unexpected trailing data.");
    }

    private static byte[] Transform(byte[] src, int mode)
    {
        if (mode == 0) return src;
        int width = mode switch { 1 or 4 => 1, 2 or 5 => 2, _ => 4 };
        bool xor = mode >= 4;
        byte[] result = new byte[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            int prev = i >= width ? src[i - width] : 0;
            result[i] = xor ? (byte)(src[i] ^ prev) : unchecked((byte)(src[i] - prev));
        }
        return result;
    }

    private static byte[] ReverseTransform(byte[] src, int mode)
    {
        if (mode == 0) return src;
        int width = mode switch { 1 or 4 => 1, 2 or 5 => 2, _ => 4 };
        bool xor = mode >= 4;
        byte[] result = new byte[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            int prev = i >= width ? result[i - width] : 0;
            result[i] = xor ? (byte)(src[i] ^ prev) : unchecked((byte)(src[i] + prev));
        }
        return result;
    }

    private static int Key(byte[] data, int i) =>
        (data[i] << 16) | (data[i + 1] << 8) | data[i + 2];

    private static byte[] EncodeTokens(byte[] data)
    {
        int n = data.Length;
        var result = new List<byte>(n + n / 64 + 2);
        var cache = new Dictionary<int, List<int>>(Math.Min(n, 65536));
        int i = 0;
        int literalStart = 0;

        void FlushLiterals(int end)
        {
            while (literalStart < end)
            {
                int length = Math.Min(64, end - literalStart);
                result.Add((byte)(length - 1));
                for (int j = literalStart; j < literalStart + length; j++)
                    result.Add(data[j]);
                literalStart += length;
            }
        }

        while (i < n)
        {
            int maxRun = Math.Min(67, n - i);
            int zeroLen = 0;
            if (data[i] == 0)
                while (zeroLen < maxRun && data[i + zeroLen] == 0) zeroLen++;
            int repeatLen = 1;
            while (repeatLen < maxRun && data[i + repeatLen] == data[i]) repeatLen++;

            int matchLen = 0, distance = 0;
            if (i + 3 <= n && cache.TryGetValue(Key(data, i), out var candidates))
            {
                for (int k = candidates.Count - 1; k >= 0; k--)
                {
                    int at = candidates[k];
                    int d = i - at;
                    if (d is < 1 or > 65535) continue;
                    int length = 3;
                    while (length < maxRun && data[at + length] == data[i + length]) length++;
                    if (length > matchLen) { matchLen = length; distance = d; }
                    if (matchLen == maxRun) break;
                }
            }

            bool useZero = zeroLen >= 4 && zeroLen >= matchLen && zeroLen >= repeatLen;
            bool useRepeat = !useZero && repeatLen >= 4 && repeatLen >= matchLen;
            bool useMatch = !useZero && !useRepeat && matchLen >= 4;
            int count = useZero ? zeroLen : useRepeat ? repeatLen : useMatch ? matchLen : 1;

            if (useZero || useRepeat || useMatch)
            {
                FlushLiterals(i);
                if (useZero) result.Add((byte)(0xC0 | (count - 4)));
                else if (useRepeat)
                {
                    result.Add((byte)(0x40 | (count - 4)));
                    result.Add(data[i]);
                }
                else
                {
                    result.Add((byte)(0x80 | (count - 4)));
                    result.Add((byte)distance);
                    result.Add((byte)(distance >> 8));
                }
            }

            int end = i + count;
            for (int pos = i; pos < end; pos++)
            {
                if (pos + 3 > n) continue;
                int key = Key(data, pos);
                if (!cache.TryGetValue(key, out var positions))
                {
                    positions = new List<int>(4);
                    cache.Add(key, positions);
                }
                positions.Add(pos);
                if (positions.Count > 4) positions.RemoveAt(0);
            }
            i = end;
            if (useZero || useRepeat || useMatch) literalStart = i;
        }
        FlushLiterals(n);
        return result.ToArray();
    }

    private static byte[] DecodeTokens(byte[] src, int expectedSize)
    {
        var output = new List<byte>(expectedSize);
        int i = 0;
        while (i < src.Length)
        {
            byte header = src[i++];
            int type = header >> 6;
            int length = (header & 63) + (type == 0 ? 1 : 4);
            if (length > expectedSize - output.Count)
                throw new InvalidDataException("Token expands beyond block.");
            switch (type)
            {
                case 0:
                    if (length > src.Length - i)
                        throw new InvalidDataException("Truncated literal.");
                    for (int j = 0; j < length; j++) output.Add(src[i++]);
                    break;
                case 1:
                    if (i >= src.Length) throw new InvalidDataException("Truncated repeat run.");
                    byte value = src[i++];
                    for (int j = 0; j < length; j++) output.Add(value);
                    break;
                case 2:
                    if (src.Length - i < 2) throw new InvalidDataException("Truncated match distance.");
                    int dist = src[i] | src[i + 1] << 8;
                    i += 2;
                    if (dist == 0 || dist > output.Count)
                        throw new InvalidDataException("Invalid match distance.");
                    for (int j = 0; j < length; j++)
                        output.Add(output[output.Count - dist]); // supports overlapping matches
                    break;
                case 3:
                    for (int j = 0; j < length; j++) output.Add(0);
                    break;
            }
        }
        if (output.Count != expectedSize)
            throw new InvalidDataException("Decoded block length mismatch.");
        return output.ToArray();
    }

    private static uint[] MakeCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
                c = (c & 1) == 0 ? c >> 1 : 0xEDB88320U ^ (c >> 1);
            table[(int)n] = c;
        }
        return table;
    }

    private static uint Crc32(byte[] data)
    {
        uint crc = 0xffffffffU;
        foreach (byte b in data)
            crc = CrcTable[(int)((crc ^ b) & 255)] ^ (crc >> 8);
        return crc ^ 0xffffffffU;
    }
}
