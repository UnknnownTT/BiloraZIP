using BiloraZIP.Services;
using System.Reflection;
using System.Windows;

namespace BiloraZIP.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        LocalizationService.Register(this);
        VersionLabel.Text = $"{LocalizationService.Translate("Version")} 1.0.0 RC1";
    }
}
