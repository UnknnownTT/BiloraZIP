using System.IO;

namespace BiloraZIP.Services;

internal static class DragExportCache
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "BiloraZIP-DragDrop");
    public static string CreateSession()
    {
        Directory.CreateDirectory(Root);
        string path = Path.Combine(Root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    // Do not delete immediately after DoDragDrop: Explorer may still be copying source bytes.
    public static void CleanupOld()
    {
        try
        {
            if (!Directory.Exists(Root)) return;
            foreach (string path in Directory.EnumerateDirectories(Root))
            {
                try
                {
                    if (Directory.GetCreationTimeUtc(path) < DateTime.UtcNow.AddDays(-3))
                        Directory.Delete(path, recursive: true);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        catch (IOException) { }
    }
}
