using System.IO;

namespace BiloraZIP.Services;

/// <summary>Filesystem path guards for Explorer drag/drop and paste.</summary>
public static class FileDropSafety
{
    public static bool IsSamePath(string first, string second) =>
        string.Equals(Normalize(first), Normalize(second), StringComparison.OrdinalIgnoreCase);

    public static bool IsInsideFolder(string sourceFolder, string destination)
    {
        string source = Normalize(sourceFolder) + Path.DirectorySeparatorChar;
        return Normalize(destination).StartsWith(source, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}
