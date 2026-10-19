using System.IO;

namespace BiloraZIP.Services;

/// <summary>Fail-closed limits for untrusted archives. Applied before and during extraction.</summary>
public static class ArchiveSafetyPolicy
{
    public static void CheckPlan(int entries, long totalBytes)
    {
        var p = PreferencesService.Current;
        if (entries > p.MaxEntries) throw new InvalidDataException($"Archive contains {entries:N0} entries; safety limit is {p.MaxEntries:N0}. Change it in Settings only for trusted archives.");
        if (totalBytes < 0 || totalBytes > p.MaxExtractionGiB * (1L << 30))
            throw new InvalidDataException($"Archive would extract more than the {p.MaxExtractionGiB} GiB safety limit.");
    }

    public static void CheckEntry(long original, long compressed, string name)
    {
        var p = PreferencesService.Current;
        if (original < 0) throw new InvalidDataException("Invalid archive size metadata: " + name);
        if (original > p.MaxSingleFileGiB * (1L << 30))
            throw new InvalidDataException($"Entry exceeds the {p.MaxSingleFileGiB} GiB per-file safety limit: {name}");
        // Do not divide by zero. Tiny files are exempt from ratio checks to avoid false positives.
        if (original > 1024 * 1024 && compressed >= 0 && (compressed == 0 || original / Math.Max(1, compressed) > p.MaxCompressionRatio))
            throw new InvalidDataException($"Suspicious compression ratio for {name}; safety limit is {p.MaxCompressionRatio}:1.");
    }

    public static void CheckWritten(long entryWritten, long totalWritten, string name)
    {
        var p = PreferencesService.Current;
        if (entryWritten > p.MaxSingleFileGiB * (1L << 30) || totalWritten > p.MaxExtractionGiB * (1L << 30))
            throw new InvalidDataException("Archive decompression exceeded configured safety limits: " + name);
    }
}
