using System.Diagnostics;
using System.Windows;
using BiloraZIP.Services;

namespace BiloraZIP.Views;

public partial class SupportWindow : Window
{
    public SupportWindow()
    {
        InitializeComponent();
        LocalizationService.Register(this);
        string url = PreferencesService.Current.DonationUrl;
        DonateButton.IsEnabled = Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.Scheme == Uri.UriSchemeHttps;
        if (DonateButton.IsEnabled) LinkStatus.Text = url;
    }

    private void Donate_Click(object sender, RoutedEventArgs e)
    {
        string url = PreferencesService.Current.DonationUrl;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || parsed.Scheme != Uri.UriSchemeHttps) return;
        try { Process.Start(new ProcessStartInfo(parsed.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) { BiloraDialog.Show(this, "Unable to open donation link: " + ex.Message); }
    }
}
