using System.IO;

namespace BiloraZIP.Services;

public static class PreviewCacheService
{
    public static void CleanupOld()
    {
        string basePath = Path.Combine(Path.GetTempPath(), "BiloraZIP-Preview");
        if (!Directory.Exists(basePath)) return;
        foreach (string folder in Directory.EnumerateDirectories(basePath))
        {
            try
            {
                if (Directory.GetLastWriteTimeUtc(folder) < DateTime.UtcNow.AddDays(-1))
                    Directory.Delete(folder, recursive: true);
            }
            catch (IOException) { } // Do not disrupt files still in use.
            catch (UnauthorizedAccessException) { }
        }
    }
}
