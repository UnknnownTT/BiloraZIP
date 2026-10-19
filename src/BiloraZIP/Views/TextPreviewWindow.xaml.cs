using BiloraZIP.Services;
using System.Windows;

namespace BiloraZIP.Views;

public partial class TextPreviewWindow : Window
{
    public TextPreviewWindow(string name, string content)
    {
        InitializeComponent();
        LocalizationService.Register(this);
        Title = "View — " + name;
        FileNameText.Text = name;
        PreviewText.Text = content;
    }
}
