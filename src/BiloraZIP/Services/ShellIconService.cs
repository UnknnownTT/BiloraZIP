using System.IO;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BiloraZIP.Services;

/// <summary>Native Windows icons for file extensions, based on registered file types.</summary>
public static class ShellIconService
{
    private const uint ShgfiIcon = 0x100;
    private const uint ShgfiSmallIcon = 0x1;
    private const uint ShgfiUseFileAttributes = 0x10;
    private const uint FileAttributeDirectory = 0x10;
    private const uint FileAttributeNormal = 0x80;
    private static readonly ConcurrentDictionary<string, ImageSource> Icons = new(StringComparer.OrdinalIgnoreCase);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShFileInfo
    {
        public IntPtr Icon;
        public int IconIndex;
        public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, out ShFileInfo psfi, uint cbFileInfo, uint uFlags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static ImageSource? Get(string path, bool directory)
    {
        string key = directory ? "<DIR>" : Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(key)) key = "<FILE>";
        try { return Icons.GetOrAdd(key, _ => Load(key, directory)); }
        catch { return null; }
    }

    private static ImageSource Load(string extension, bool directory)
    {
        string sample = directory ? "folder" : "sample" + (extension == "<FILE>" ? ".file" : extension);
        uint attributes = directory ? FileAttributeDirectory : FileAttributeNormal;
        IntPtr result = SHGetFileInfo(sample, attributes, out ShFileInfo info,
            (uint)Marshal.SizeOf<ShFileInfo>(), ShgfiIcon | ShgfiSmallIcon | ShgfiUseFileAttributes);
        if (result == IntPtr.Zero || info.Icon == IntPtr.Zero)
            throw new InvalidOperationException("Unable to read Windows icon.");
        try
        {
            BitmapSource bitmap = Imaging.CreateBitmapSourceFromHIcon(info.Icon, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bitmap.Freeze();
            return bitmap;
        }
        finally { DestroyIcon(info.Icon); }
    }
}
