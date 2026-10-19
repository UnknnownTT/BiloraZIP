using System.IO;

namespace BiloraZIP.Services;

/// <summary>
/// Validate untrusted archive entry names BEFORE creating any output files.
/// Blocks Windows reserved device names, ADS, ambiguous trailing characters,
/// rooted paths, traversal, and characters illegal on Windows.
/// </summary>
public static class ArchiveEntryPathPolicy
{
    private static readonly HashSet<string> DeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static void Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidDataException("Archive entry has an empty path.");
        string relative = name.Replace('\\', '/');
        if (relative.StartsWith('/') || relative.Contains(':'))
            throw new InvalidDataException("Unsafe rooted or alternate-stream archive path: " + name);
        string[] segments = relative.TrimEnd('/').Split('/');
        if (segments.Length == 0) throw new InvalidDataException("Empty archive entry path.");
        foreach (string segment in segments)
        {
            if (segment.Length == 0 || segment is "." or ".." || segment.Length > 255 ||
                segment.EndsWith(' ') || segment.EndsWith('.') ||
                segment.Any(c => c < 32 || c is '<' or '>' or '"' or '|' or '?' or '*'))
                throw new InvalidDataException("Unsafe Windows filename in archive: " + name);
            string device = segment.Split('.')[0];
            if (DeviceNames.Contains(device))
                throw new InvalidDataException("Windows reserved device name in archive: " + name);
        }
    }
}
