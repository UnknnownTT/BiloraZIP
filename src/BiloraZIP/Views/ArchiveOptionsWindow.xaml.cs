using BiloraZIP.Services;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace BiloraZIP.Views;

public partial class ArchiveOptionsWindow : Window
{
    public string DestinationPath => DestinationBox.Text.Trim();
    public CompressionLevel Level { get; private set; } = CompressionLevel.Optimal;
    public bool VerifyAfterCreate => VerifyBox.IsChecked == true;
    public bool IsTar => FormatBox.SelectedIndex == 1;
    public bool IsBilz => FormatBox.SelectedIndex == 2;
    public bool IsTarGz => FormatBox.SelectedIndex == 3;
    public int BilzSplitMiB => IsBilz && BilzSplitBox.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out int size) ? size : 0;
    public string? Password => EncryptBox.IsChecked == true ? ArchivePasswordBox.Password : null;

    public ArchiveOptionsWindow(string suggestedFileName)
    {
        InitializeComponent();
        LocalizationService.Register(this);
        DestinationBox.Text = suggestedFileName;
        VerifyBox.IsChecked = PreferencesService.Current.VerifyAfterCreate;
    }

    private void FormatBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DestinationBox is null || EncryptBox is null) return;
        string extension = IsTar ? ".tar" : IsBilz ? ".bilz" : IsTarGz ? ".tar.gz" : ".zip";
        if (!string.IsNullOrWhiteSpace(DestinationBox.Text))
        {
            string current = DestinationBox.Text;
            if (current.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)) current = current[..^7];
            else current = Path.Combine(Path.GetDirectoryName(current) ?? "", Path.GetFileNameWithoutExtension(current));
            DestinationBox.Text = current + extension;
        }
        EncryptBox.IsEnabled = !IsTar && !IsBilz && !IsTarGz;
        if (IsTar || IsBilz || IsTarGz) EncryptBox.IsChecked = false;
        if (BilzSplitPanel is not null) BilzSplitPanel.Visibility = IsBilz ? Visibility.Visible : Visibility.Collapsed;
    }

    private void EncryptBox_Changed(object sender, RoutedEventArgs e)
    {
        if (ArchivePasswordBox is null || ConfirmPasswordBox is null) return;
        bool enabled = EncryptBox.IsChecked == true;
        ArchivePasswordBox.IsEnabled = ConfirmPasswordBox.IsEnabled = enabled;
        if (!enabled)
        {
            ArchivePasswordBox.Clear();
            ConfirmPasswordBox.Clear();
        }
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Save archive",
            Filter = IsTar ? "TAR archive (*.tar)|*.tar" : IsBilz ? "BILZ archive (*.bilz)|*.bilz" : IsTarGz ? "TAR.GZ archive (*.tar.gz)|*.tar.gz" : "ZIP archive (*.zip)|*.zip",
            DefaultExt = IsTar ? ".tar" : IsBilz ? ".bilz" : IsTarGz ? ".tar.gz" : ".zip",
            AddExtension = true,
            FileName = Path.GetFileName(DestinationBox.Text)
        };
        if (dlg.ShowDialog(this) == true) DestinationBox.Text = dlg.FileName;
    }

    private void Create_Click(object sender, RoutedEventArgs e)
    {
        string file;
        try { file = Path.GetFullPath(DestinationPath); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            BiloraDialog.Show(this, "Invalid destination path.");
            return;
        }
        if (!(IsTarGz ? file.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) :
            string.Equals(Path.GetExtension(file), IsTar ? ".tar" : IsBilz ? ".bilz" : ".zip", StringComparison.OrdinalIgnoreCase)))
        {
            BiloraDialog.Show(this, "Archive filename extension must match its selected format.");
            return;
        }
        if (File.Exists(file))
        {
            BiloraDialog.Show(this, "A file already exists at this destination. Choose a new name.");
            return;
        }
        if (!Directory.Exists(Path.GetDirectoryName(file)))
        {
            BiloraDialog.Show(this, "The destination folder does not exist.");
            return;
        }

        if (EncryptBox.IsChecked == true &&
            (ArchivePasswordBox.Password.Length == 0 || ArchivePasswordBox.Password != ConfirmPasswordBox.Password))
        {
            BiloraDialog.Show(this, "Enter a password and confirm it exactly. Empty passwords are not supported.", "BiloraZIP", MessageBoxButton.OK, MessageBoxImage.Warning);
            ArchivePasswordBox.Focus();
            return;
        }
        Level = (CompressionBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() switch
        {
            "NoCompression" => CompressionLevel.NoCompression,
            "Fastest" => CompressionLevel.Fastest,
            "SmallestSize" => CompressionLevel.SmallestSize,
            _ => CompressionLevel.Optimal
        };
        DestinationBox.Text = file;
        DialogResult = true;
    }
}
