using BiloraZIP.Services;
using Microsoft.Win32;
using System.IO;
using System.Windows;

namespace BiloraZIP.Views;

public partial class ExtractionOptionsWindow : Window
{
    public string DestinationPath { get; private set; } = string.Empty;
    public ExtractionCollisionPolicy Policy { get; private set; } = ExtractionCollisionPolicy.Fail;
    public bool OpenAfter => OpenFolderBox.IsChecked == true;

    public ExtractionOptionsWindow(string suggestedDestination)
    {
        InitializeComponent();
        LocalizationService.Register(this);
        DestinationBox.Text = suggestedDestination;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFolderDialog { Title = "Choose extraction destination" };
        if (picker.ShowDialog(this) == true) DestinationBox.Text = picker.FolderName;
    }

    private void Extract_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(DestinationBox.Text)) throw new ArgumentException("Choose a destination folder.");
            string path = Path.GetFullPath(DestinationBox.Text.Trim());
            if (File.Exists(path)) throw new IOException("Destination is an existing file, not a folder.");
            Policy = SkipRadio.IsChecked == true ? ExtractionCollisionPolicy.Skip :
                RenameRadio.IsChecked == true ? ExtractionCollisionPolicy.Rename :
                OverwriteRadio.IsChecked == true ? ExtractionCollisionPolicy.Overwrite : ExtractionCollisionPolicy.Fail;
            if (Policy == ExtractionCollisionPolicy.Overwrite &&
                BiloraDialog.Show(this,
                    LocalizationService.IsMongolian ? "Ижил нэртэй файлуудыг дарж бичнэ. Үргэлжлүүлэх үү?" :
                    "Existing files will be permanently replaced. Continue?",
                    "BiloraZIP", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            DestinationPath = path;
            DialogResult = true;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or PathTooLongException)
        {
            BiloraDialog.Show(this, ex.Message, "BiloraZIP", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
