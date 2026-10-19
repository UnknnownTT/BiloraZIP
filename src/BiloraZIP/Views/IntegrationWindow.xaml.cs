using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using BiloraZIP.Services;

namespace BiloraZIP.Views;

public partial class IntegrationWindow : Window
{
    public IntegrationWindow()
    {
        InitializeComponent();
        LocalizationService.Register(this);
        CheckBox[] boxes = [ZipBox, RarBox, SevenBox, TarBox, ZipxBox, XapkBox, BilzBox];
        for (int i = 0; i < boxes.Length; i++)
            boxes[i].IsChecked = IntegrationService.IsAssociated(IntegrationService.Formats[i]);
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        CheckBox[] boxes = [ZipBox, RarBox, SevenBox, TarBox, ZipxBox, XapkBox, BilzBox];
        var selected = new List<string>();
        for (int i = 0; i < boxes.Length; i++)
            if (boxes[i].IsChecked == true) selected.Add(IntegrationService.Formats[i]);
        try
        {
            IntegrationService.Apply(selected, ExplorerMenuBox.IsChecked == true);
            BiloraDialog.Show(this, "Per-user Open With and classic Explorer commands were updated. Your Windows default app was not changed.", "BiloraZIP", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            BiloraDialog.Show(this, ex.Message, "Integration failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Defaults_Click(object sender, RoutedEventArgs e)
        => Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
