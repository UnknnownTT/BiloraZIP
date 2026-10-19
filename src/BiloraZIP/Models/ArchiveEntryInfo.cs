namespace BiloraZIP.Models;

public sealed record ArchiveEntryInfo(
    string Name,
    string Size,
    string CompressedSize,
    string Type,
    string CompressionRatio,
    string Modified)
{
    // Non-null only when displaying the local filesystem explorer, never archive entries.
    public string? FileSystemPath { get; init; }
    public System.Windows.Media.ImageSource? IconSource { get; init; }
    public string DisplayName => Name.Replace('\\', '/').TrimEnd('/').Split('/').LastOrDefault() ?? Name;
}
