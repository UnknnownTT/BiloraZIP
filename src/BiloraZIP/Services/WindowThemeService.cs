using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace BiloraZIP.Services;

/// <summary>Uses the supported Windows immersive dark-caption hint without disabling native resize/Snap.</summary>
public static class WindowThemeService
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    public static void Apply(Window window)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763)) return;
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        int enabled = ThemeManager.IsDark ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, 20, ref enabled, sizeof(int));
    }
}
