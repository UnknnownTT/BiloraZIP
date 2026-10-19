using System.IO;
using System.Text.Json;

namespace BiloraZIP.Services;

/// <summary>Non-secret, per-user preferences. Never stores archive passwords.</summary>
public sealed class BiloraPreferences
{
    public int ExplorerWheelLines { get; set; } = 3;
    public bool VerifyAfterCreate { get; set; } = true;
    public bool WarnMixedEncryption { get; set; } = true;
    public bool ShowDonationHint { get; set; } = true;
    public bool ShowDetailedErrors { get; set; } = true;
    public int MaxExtractionGiB { get; set; } = 8;
    public int MaxSingleFileGiB { get; set; } = 4;
    public int MaxEntries { get; set; } = 100000;
    public int MaxCompressionRatio { get; set; } = 500;
    public string DonationUrl { get; set; } = string.Empty;
}

public static class PreferencesService
{
    private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "BiloraZIP", "preferences.json");
    public static BiloraPreferences Current { get; private set; } = Load();

    private static BiloraPreferences Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return Validate(JsonSerializer.Deserialize<BiloraPreferences>(File.ReadAllText(FilePath)) ?? new());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
        return new();
    }

    public static void Save(BiloraPreferences value)
    {
        Current = Validate(value);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, FilePath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static BiloraPreferences Validate(BiloraPreferences p)
    {
        p.ExplorerWheelLines = Math.Clamp(p.ExplorerWheelLines, 1, 15);
        p.MaxExtractionGiB = Math.Clamp(p.MaxExtractionGiB, 1, 1024);
        p.MaxSingleFileGiB = Math.Clamp(p.MaxSingleFileGiB, 1, p.MaxExtractionGiB);
        p.MaxEntries = Math.Clamp(p.MaxEntries, 100, 1000000);
        p.MaxCompressionRatio = Math.Clamp(p.MaxCompressionRatio, 10, 100000);
        p.DonationUrl ??= string.Empty;
        return p;
    }
}
