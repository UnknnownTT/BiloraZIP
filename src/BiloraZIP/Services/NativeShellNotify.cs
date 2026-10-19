using System.Runtime.InteropServices;

namespace BiloraZIP.Services;

internal static class NativeShellNotify
{
    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
    public static void Refresh() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); // SHCNE_ASSOCCHANGED
}
