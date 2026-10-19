using BiloraZIP.Services;
using System.IO;
using System.Windows;

namespace BiloraZIP.Views;

public partial class PasswordPromptWindow : Window
{
    public string Password => PasswordInput.Password;

    public PasswordPromptWindow(string archivePath)
    {
        InitializeComponent();
        LocalizationService.Register(this);
        ArchiveLabel.Text = Path.GetFileName(archivePath);
        Loaded += (_, _) => PasswordInput.Focus();
    }

    private void Unlock_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(PasswordInput.Password))
        {
            BiloraDialog.Show(this, "Enter the password first.", "BiloraZIP", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        DialogResult = true;
    }
}
