namespace BiloraZIP.Models;

public sealed class OperationProgressInfo
{
    public string Stage { get; init; } = string.Empty;
    public string CurrentItem { get; init; } = string.Empty;
    public int ItemsCompleted { get; init; }
    public int TotalItems { get; init; }
    public long BytesProcessed { get; init; }
    public long TotalBytes { get; init; }
    public double Percent => TotalBytes <= 0 ? 0 : Math.Min(100.0, BytesProcessed * 100d / TotalBytes);
}
