namespace BiloraZIP.Services;

/// <summary>Pure wheel-to-scroll mapping; independent of WPF for automated tests.</summary>
public static class ExplorerWheelScroll
{
    public static double CalculateOffset(double current, double maximum, int wheelDelta, int lines)
    {
        if (!double.IsFinite(current) || !double.IsFinite(maximum) || maximum <= 0 || wheelDelta == 0)
            return current;
        int safeLines = Math.Clamp(lines, 1, 15);
        return Math.Clamp(current - wheelDelta / 120.0 * safeLines, 0.0, maximum);
    }
}
