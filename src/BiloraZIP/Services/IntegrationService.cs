using Microsoft.Win32;
using System.IO;

namespace BiloraZIP.Services;

public static class IntegrationService
{
    public static readonly string[] Formats = [".zip", ".rar", ".7z", ".tar", ".zipx", ".xapk", ".bilz"];
    private const string UserClasses = @"Software\Classes";

    // Registers BiloraZIP only as a *candidate* Open With app; Windows owns the actual user default.
    public static void Apply(IReadOnlyCollection<string> formats, bool contextMenu)
    {
        string exe = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate running executable.");
        using (RegistryKey root = Registry.CurrentUser.CreateSubKey(UserClasses + @"\BiloraZIP.Archive")!)
            root.SetValue("", "BiloraZIP archive");
        using (RegistryKey icon = Registry.CurrentUser.CreateSubKey(UserClasses + @"\BiloraZIP.Archive\DefaultIcon")!)
            icon.SetValue("", exe + ",0");
        using (RegistryKey command = Registry.CurrentUser.CreateSubKey(UserClasses + @"\BiloraZIP.Archive\shell\open\command")!)
            command.SetValue("", $"\"{exe}\" --open \"%1\"");
        using (RegistryKey capabilities = Registry.CurrentUser.CreateSubKey(@"Software\BiloraZIP\Capabilities")!)
        {
            capabilities.SetValue("ApplicationName", "BiloraZIP");
            capabilities.SetValue("ApplicationDescription", "Archive manager for ZIP, RAR, 7Z, TAR, ZIPX, XAPK, and BILZ");
        }
        using (RegistryKey apps = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications")!)
            apps.SetValue("BiloraZIP", @"Software\BiloraZIP\Capabilities");
        using RegistryKey associations = Registry.CurrentUser.CreateSubKey(@"Software\BiloraZIP\Capabilities\FileAssociations")!;
        foreach (string ext in Formats)
        {
            bool enabled = formats.Contains(ext, StringComparer.OrdinalIgnoreCase);
            string openWithPath = UserClasses + @"\" + ext + @"\OpenWithProgids";
            if (enabled)
            {
                associations.SetValue(ext, "BiloraZIP.Archive");
                using RegistryKey openWith = Registry.CurrentUser.CreateSubKey(openWithPath)!;
                openWith.SetValue("BiloraZIP.Archive", Array.Empty<byte>(), RegistryValueKind.None);
            }
            else
            {
                associations.DeleteValue(ext, throwOnMissingValue: false);
                using RegistryKey? openWith = Registry.CurrentUser.OpenSubKey(openWithPath, writable: true);
                openWith?.DeleteValue("BiloraZIP.Archive", throwOnMissingValue: false);
            }
            string menuPath = UserClasses + @"\SystemFileAssociations\" + ext + @"\shell\BiloraZIP";
            if (enabled && contextMenu)
            {
                using RegistryKey menu = Registry.CurrentUser.CreateSubKey(menuPath)!;
                menu.SetValue("MUIVerb", "BiloraZIP");
                menu.SetValue("Icon", exe + ",0");
                menu.SetValue("SubCommands", "");
                foreach ((string key, string label, string argument) in new[] {
                    ("Open", "Open with BiloraZIP", "--open"),
                    ("Extract", "Extract files...", "--extract-to"),
                    ("ExtractHere", "Extract Here", "--extract-here"),
                    ("ExtractFolder", "Extract to named folder", "--extract-subfolder")})
                {
                    using RegistryKey cmd = Registry.CurrentUser.CreateSubKey(menuPath + @"\shell\" + key)!;
                    cmd.SetValue("", label);
                    using RegistryKey handler = Registry.CurrentUser.CreateSubKey(menuPath + @"\shell\" + key + @"\command")!;
                    handler.SetValue("", $"\"{exe}\" {argument} \"%1\"");
                }
            }
            else
            {
                try { Registry.CurrentUser.DeleteSubKeyTree(menuPath, throwOnMissingSubKey: false); }
                catch (ArgumentException) { }
            }
        }
        // Repair all three Explorer legacy selection classes. The old version used
        // AllFilesystemObjects alone; some Explorer configurations hid the verb.
        // Document is intentional: CompressionInvocationHub collects the per-item
        // launches into one archive dialog (legacy shell limit: 15 selections).
        foreach (string kind in new[] { "*", "Directory", "AllFilesystemObjects" })
        {
            string compressPath = UserClasses + @"\" + kind + @"\shell\BiloraZIP.Compress";
            if (contextMenu)
            {
                using var menu = Registry.CurrentUser.CreateSubKey(compressPath)!;
                menu.SetValue("", "Compress with BiloraZIP (ZIP / BILZ)...");
                menu.SetValue("Icon", exe + ",0");
                menu.SetValue("MultiSelectModel", "Document");
                using var cmd = Registry.CurrentUser.CreateSubKey(compressPath + @"\command")!;
                cmd.SetValue("", $"\"{exe}\" --compress \"%1\"");
            }
            else
                Registry.CurrentUser.DeleteSubKeyTree(compressPath, throwOnMissingSubKey: false);
        }
        // Refresh the shell's cached association/context-menu descriptions.
        NativeShellNotify.Refresh();
    }

    public static bool IsAssociated(string extension)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\BiloraZIP\Capabilities\FileAssociations");
        return string.Equals(key?.GetValue(extension) as string, "BiloraZIP.Archive", StringComparison.Ordinal);
    }
}
