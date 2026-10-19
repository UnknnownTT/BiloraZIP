using System.Windows;
using BiloraZIP.Services;

namespace BiloraZIP.Views;

public partial class ExitChoiceWindow : Window
{
    public string Choice { get; private set; } = "cancel";
    public bool Remember => RememberChoice.IsChecked == true;
    public ExitChoiceWindow()
    {
        InitializeComponent();
        LocalizationService.Register(this);
    }
    private void Tray_Click(object sender, RoutedEventArgs e) { Choice = "tray"; DialogResult = true; }
    private void Exit_Click(object sender, RoutedEventArgs e) { Choice = "exit"; DialogResult = true; }
    private void Cancel_Click(object sender, RoutedEventArgs e) { Choice = "cancel"; DialogResult = false; }
}
