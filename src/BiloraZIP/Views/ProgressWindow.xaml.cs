using BiloraZIP.Services;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using BiloraZIP.Models;

namespace BiloraZIP.Views;

public partial class ProgressWindow : Window
{
    private readonly Stopwatch _watch = Stopwatch.StartNew();
    private bool _complete;
    private bool _allowClose;
    public event EventHandler? CancelRequested;

    public ProgressWindow(string operationTitle)
    {
        InitializeComponent();
        LocalizationService.Register(this);
        OperationTitleText.Text = LocalizationService.Translate(operationTitle);
    }

    public void Update(OperationProgressInfo info)
    {
        if (_complete) return;
        StageText.Text = LocalizationService.Translate(info.Stage);
        FileText.Text = info.CurrentItem;
        OverallProgress.Value = info.Percent;
        PercentText.Text = $"{info.Percent:0.0}%";
        ItemsText.Text = LocalizationService.IsMongolian ? $"{info.TotalItems} файлаас {info.ItemsCompleted}" : $"{info.ItemsCompleted} of {info.TotalItems} files";
        BytesText.Text = LocalizationService.IsMongolian ? $"Боловсруулсан: {FormatBytes(info.BytesProcessed)} / {FormatBytes(info.TotalBytes)}" : $"Processed {FormatBytes(info.BytesProcessed)} of {FormatBytes(info.TotalBytes)}";
        ElapsedText.Text = _watch.Elapsed.ToString(@"hh\:mm\:ss");
        if (info.BytesProcessed > 0 && info.TotalBytes > info.BytesProcessed && _watch.Elapsed.TotalSeconds > 0.5)
        {
            double rate = info.BytesProcessed / _watch.Elapsed.TotalSeconds;
            TimeSpan left = TimeSpan.FromSeconds((info.TotalBytes - info.BytesProcessed) / rate);
            RemainingText.Text = left.TotalDays <= 30 ? left.ToString(@"hh\:mm\:ss") : LocalizationService.Translate("Calculating…");
        }
        else RemainingText.Text = LocalizationService.Translate("Calculating…");
    }

    public void Finish(string message, string? errorDetails = null)
    {
        _complete = true;
        _watch.Stop();
        StageText.Text = LocalizationService.Translate(message);
        if (!string.IsNullOrWhiteSpace(errorDetails)) FileText.Text = errorDetails;
        if (message == "Completed")
        {
            OverallProgress.Value = 100;
            PercentText.Text = "100%";
            RemainingText.Text = "00:00:00";
        }
        BackgroundButton.Content = LocalizationService.Translate("Close");
        CancelButton.IsEnabled = false;
    }

    public void CompleteAndClose()
    {
        _allowClose = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        CancelButton.IsEnabled = false;
        StageText.Text = LocalizationService.Translate("Cancelling…");
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Background_Click(object sender, RoutedEventArgs e)
    {
        if (_complete) CompleteAndClose();
        else Hide();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose && !_complete)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private static string FormatBytes(long value)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = value;
        int index = 0;
        while (size >= 1024 && index < units.Length - 1) { size /= 1024; index++; }
        return $"{size:0.#} {units[index]}";
    }
}
