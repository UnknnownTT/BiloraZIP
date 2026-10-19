using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BiloraZIP.Services;

public static class ThemeManager
{
    private sealed class StoredSettings
    {
        public string Theme { get; set; } = "Dark";
        public List<string> Favorites { get; set; } = new();
    }

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BiloraZIP", "settings.json");

    private static StoredSettings _settings = new();
    public static bool IsDark => _settings.Theme == "Dark";
    public static IReadOnlyList<string> Favorites => _settings.Favorites;

    public static void Initialize()
    {
        try
        {
            if (File.Exists(SettingsPath))
                _settings = JsonSerializer.Deserialize<StoredSettings>(File.ReadAllText(SettingsPath)) ?? new();
        }
        catch (Exception) { _settings = new(); }
        SetTheme(_settings.Theme == "Light" ? "Light" : "Dark", save: false);
    }

    public static void SetTheme(string name, bool save = true)
    {
        bool dark = name != "Light";
        _settings.Theme = dark ? "Dark" : "Light";
        var colors = dark
            ? new Dictionary<string, string>
            {
                ["WindowBg"] = "#202329", ["PanelBg"] = "#292D35",
                ["ToolbarBg"] = "#292C33", ["MenuBg"] = "#24272D",
                ["ListBg"] = "#22262C", ["ItemHover"] = "#363F52",
                ["ItemSelected"] = "#324B74", ["BorderColor"] = "#414650",
                ["TextColor"] = "#F2F4F7", ["SecondaryText"] = "#B9C1CC",
                ["InputBg"] = "#191C21", ["Accent"] = "#74A5FA",
                ["AccentText"] = "#141E30", ["ButtonBg"] = "#363D48",
                ["ButtonHover"] = "#4B5463", ["ProgressTrack"] = "#444A55"
            }
            : new Dictionary<string, string>
            {
                ["WindowBg"] = "#F3F5F9", ["PanelBg"] = "#FFFFFF",
                ["ToolbarBg"] = "#EBEFF5", ["MenuBg"] = "#FAFBFD",
                ["ListBg"] = "#FFFFFF", ["ItemHover"] = "#ECF3FF",
                ["ItemSelected"] = "#D7E6FF", ["BorderColor"] = "#D5DDE8",
                ["TextColor"] = "#202C3C", ["SecondaryText"] = "#56677C",
                ["InputBg"] = "#FFFFFF", ["Accent"] = "#3966CC",
                ["AccentText"] = "#FFFFFF", ["ButtonBg"] = "#FFFFFF",
                ["ButtonHover"] = "#E3ECFB", ["ProgressTrack"] = "#E5E9F0"
            };

        string[] tileNames = ["add", "extract", "test", "view", "delete", "find", "info", "open", "favorites", "options"];
        string[] tileDark = ["#294B47", "#304963", "#485037", "#433F5D", "#543C43", "#3D4C5D", "#4D4737", "#35495A", "#55453B", "#404759"];
        string[] tileLight = ["#E3F2EA", "#E2EDFA", "#EEF1DF", "#EEE8FA", "#FBE8E8", "#E5EFF7", "#F7EFD9", "#E4F1FA", "#F8EADD", "#E9ECF8"];
        for (int i = 0; i < tileNames.Length; i++)
            colors["IconTile_" + tileNames[i]] = dark ? tileDark[i] : tileLight[i];

        foreach (var pair in colors)
        {
            Color color = (Color)System.Windows.Media.ColorConverter.ConvertFromString(pair.Value);
            // DynamicResource consumers will update when we replace the brush.
            // Do not mutate a brush which WPF may have frozen.
            Application.Current.Resources[pair.Key] = new SolidColorBrush(color);
        }
        // Icon assets are transparent; update each image resource with a palette
        // appropriate for the current theme (not an opaque white tile).
        string variant = dark ? "dark" : "light";
        foreach (string icon in new[] { "add", "folder", "extract", "test", "view", "delete", "find", "info", "open", "extracthere", "favorites", "options", "dark", "light" })
        {
            var uri = new Uri($"pack://application:,,,/Assets/Icons/{icon}-{variant}.png", UriKind.Absolute);
            Application.Current.Resources["Icon_" + icon] = new BitmapImage(uri);
        }
        foreach (Window w in Application.Current.Windows) WindowThemeService.Apply(w);
        if (save) Save();
    }

    public static bool AddFavorite(string path)
    {
        if (_settings.Favorites.Contains(path, StringComparer.OrdinalIgnoreCase)) return false;
        _settings.Favorites.Add(path);
        Save();
        return true;
    }

    public static void RemoveFavorite(string path)
    {
        _settings.Favorites.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        Save();
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            string temp = SettingsPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, SettingsPath, overwrite: true);
        }
        catch (IOException) { /* theme still applies for this session */ }
        catch (UnauthorizedAccessException) { }
    }
}
