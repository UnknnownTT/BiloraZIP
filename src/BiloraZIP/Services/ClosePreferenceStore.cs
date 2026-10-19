using System.IO;

namespace BiloraZIP.Services;

public static class ClosePreferenceStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BiloraZIP", "close-behavior.txt");

    public static string Read()
    {
        try
        {
            string mode = File.ReadAllText(FilePath).Trim();
            return mode is "tray" or "exit" ? mode : "ask";
        }
        catch (IOException) { return "ask"; }
        catch (UnauthorizedAccessException) { return "ask"; }
    }

    public static void Save(string mode)
    {
        if (mode is not ("tray" or "exit" or "ask")) throw new ArgumentOutOfRangeException(nameof(mode));
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, mode);
    }
}
