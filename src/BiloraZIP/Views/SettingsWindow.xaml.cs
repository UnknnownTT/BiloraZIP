using System.Windows;
using BiloraZIP.Services;

namespace BiloraZIP.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        LocalizationService.Register(this);
        var p = PreferencesService.Current;
        WheelLinesBox.Text = p.ExplorerWheelLines.ToString();
        VerifyBox.IsChecked = p.VerifyAfterCreate;
        MixedBox.IsChecked = p.WarnMixedEncryption;
        HintBox.IsChecked = p.ShowDonationHint;
        DetailsBox.IsChecked = p.ShowDetailedErrors;
        MaxTotalBox.Text = p.MaxExtractionGiB.ToString();
        MaxFileBox.Text = p.MaxSingleFileGiB.ToString();
        MaxEntriesBox.Text = p.MaxEntries.ToString();
        MaxRatioBox.Text = p.MaxCompressionRatio.ToString();
        DonationUrlBox.Text = p.DonationUrl;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(WheelLinesBox.Text, out int wheelLines) || wheelLines is < 1 or > 15)
        {
            BiloraDialog.Show(this, "Explorer mouse-wheel scroll lines must be between 1 and 15.");
            return;
        }
        if (!int.TryParse(MaxTotalBox.Text, out int total) || total is < 1 or > 1024 ||
            !int.TryParse(MaxFileBox.Text, out int file) || file < 1 || file > total ||
            !int.TryParse(MaxEntriesBox.Text, out int entries) || entries is < 100 or > 1000000 ||
            !int.TryParse(MaxRatioBox.Text, out int ratio) || ratio is < 10 or > 100000)
        {
            BiloraDialog.Show(this, "Invalid security limits. Check the numeric values and ensure the per-file limit does not exceed the total.");
            return;
        }
        string url = DonationUrlBox.Text.Trim();
        if (url.Length > 0 && (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || parsed.Scheme != Uri.UriSchemeHttps))
        {
            BiloraDialog.Show(this, "Donation link must use HTTPS, or leave it empty.");
            return;
        }
        try
        {
            PreferencesService.Save(new BiloraPreferences
            {
                ExplorerWheelLines = wheelLines,
                VerifyAfterCreate = VerifyBox.IsChecked == true,
                WarnMixedEncryption = MixedBox.IsChecked == true,
                ShowDonationHint = HintBox.IsChecked == true,
                ShowDetailedErrors = DetailsBox.IsChecked == true,
                MaxExtractionGiB = total,
                MaxSingleFileGiB = file,
                MaxEntries = entries,
                MaxCompressionRatio = ratio,
                DonationUrl = url
            });
            DialogResult = true;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            BiloraDialog.Show(this, "Unable to save settings: " + ex.Message);
        }
    }
}
