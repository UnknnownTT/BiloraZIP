using BiloraZIP.Services;
using System.Windows;

namespace BiloraZIP.Views;

public sealed record DiagnosticItem(string Level, string Message);

public partial class DiagnosticsWindow : Window
{
    private readonly IReadOnlyList<DiagnosticItem> _issues;
    public event EventHandler? BreakRequested;

    public DiagnosticsWindow(IReadOnlyList<DiagnosticItem> issues, bool canBreak = false)
    {
        InitializeComponent();
        LocalizationService.Register(this);
        _issues = issues;
        IssueList.ItemsSource = issues;
        if (issues.Count > 0) IssueList.SelectedIndex = 0;
        TotalText.Text = LocalizationService.IsMongolian ? $"Нийт мэдээлэл: {issues.Count}" : $"Total messages: {issues.Count}";
        BreakButton.IsEnabled = canBreak;
    }

    private void IssueList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SelectedMessageBox is null) return;
        SelectedMessageBox.Text = (IssueList.SelectedItem as DiagnosticItem)?.Message ?? string.Empty;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void Break_Click(object sender, RoutedEventArgs e)
    {
        BreakRequested?.Invoke(this, EventArgs.Empty);
        Close();
    }
    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(string.Join(Environment.NewLine, _issues.Select(x => x.Level + ": " + x.Message)));
    }
}
