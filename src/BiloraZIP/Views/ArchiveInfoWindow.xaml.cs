using BiloraZIP.Services;
using System.Windows;
using System.Windows.Controls;
using BiloraZIP.Models;

namespace BiloraZIP.Views;

public sealed record ArchiveDetailRow(string Field, string Value);

public partial class ArchiveInfoWindow : Window
{
    public ArchiveInfoWindow(ArchiveMetadata metadata)
    {
        InitializeComponent();
        LocalizationService.Register(this);
        ArchiveName.Text = metadata.FileName;
        ArchiveDetails.ItemsSource = new[]
        {
            new ArchiveDetailRow(LocalizationService.Translate("Archive type"), metadata.Format),
            new ArchiveDetailRow(LocalizationService.Translate("Full path"), metadata.FullPath),
            new ArchiveDetailRow(LocalizationService.Translate("Modified"), metadata.LastModified),
            new ArchiveDetailRow(LocalizationService.Translate("Archive size"), FormatBytes(metadata.ArchiveBytes)),
            new ArchiveDetailRow(LocalizationService.Translate("Total files"), metadata.Files.ToString()),
            new ArchiveDetailRow(LocalizationService.Translate("Total folders"), metadata.Folders.ToString()),
            new ArchiveDetailRow(LocalizationService.Translate("Original size"), FormatBytes(metadata.OriginalBytes)),
            new ArchiveDetailRow(LocalizationService.Translate("Packed contents size"), FormatBytes(metadata.PackedContentsBytes)),
            new ArchiveDetailRow(LocalizationService.Translate("Compression ratio"), metadata.CompressionRatio),
            new ArchiveDetailRow(LocalizationService.Translate("Encryption"), metadata.Encryption),
            new ArchiveDetailRow(LocalizationService.Translate("Headers encrypted"), metadata.HeaderEncryption),
            new ArchiveDetailRow(LocalizationService.Translate("Dictionary size"), metadata.DictionarySize),
            new ArchiveDetailRow(LocalizationService.Translate("Recovery record"), metadata.RecoveryRecord),
            new ArchiveDetailRow(LocalizationService.Translate("Integrity"), metadata.Integrity)
        };
        CommentText.Text = metadata.Comment;
        foreach (var column in ArchiveDetails.Columns)
            if (column.Header is string header) column.Header = LocalizationService.Translate(header);
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private static string FormatBytes(long value)
    {
        string[] suffix = ["B", "KB", "MB", "GB", "TB"];
        double size = value;
        int i = 0;
        while (size >= 1024 && i < suffix.Length - 1) { size /= 1024; i++; }
        return $"{size:0.#} {suffix[i]}";
    }
}
