using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using BiloraZIP.Services;

namespace BiloraZIP.Views;

/// <summary>Consistent, localizable alternative to stock Windows MessageBox.</summary>
public static class BiloraDialog
{
    public static MessageBoxResult Show(string message, string title = "BiloraZIP")
        => Show(Application.Current?.MainWindow, message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public static MessageBoxResult Show(Window? owner, string message, string title = "BiloraZIP")
        => Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public static MessageBoxResult Show(Window? owner, string message, string title,
        MessageBoxButton buttons, MessageBoxImage image = MessageBoxImage.Information)
    {
        var dialog = NewWindow(LocalizationService.Translate(title), 520);
        var root = NewLayout();
        root.Children.Add(new TextBlock {
            Text = image == MessageBoxImage.Warning ? "⚠ " + LocalizationService.Translate("Attention") :
                image == MessageBoxImage.Error ? "✕ " + LocalizationService.Translate("Error") : "ⓘ BiloraZIP",
            FontSize = 20, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 14)
        });
        var description = new TextBlock { Text = LocalizationService.Translate(message), TextWrapping = TextWrapping.Wrap,
            MaxWidth = 530, Margin = new Thickness(0, 0, 0, 20) };
        description.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryText");
        root.Children.Add(description);
        var actions = new WrapPanel { HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        MessageBoxResult selection = MessageBoxResult.Cancel;
        void Add(string label, MessageBoxResult result, bool isDefault = false)
        {
            var b = new Button { Content = LocalizationService.Translate(label), MinWidth = 100, MinHeight = 35,
                Margin = new Thickness(6, 4, 0, 4), IsDefault = isDefault };
            b.Click += (_, _) => { selection = result; dialog.Close(); };
            actions.Children.Add(b);
        }
        switch (buttons)
        {
            case MessageBoxButton.YesNo: Add("Yes", MessageBoxResult.Yes); Add("No", MessageBoxResult.No, true); break;
            case MessageBoxButton.YesNoCancel: Add("Yes", MessageBoxResult.Yes); Add("No", MessageBoxResult.No); Add("Cancel", MessageBoxResult.Cancel, true); break;
            case MessageBoxButton.OKCancel: Add("OK", MessageBoxResult.OK, true); Add("Cancel", MessageBoxResult.Cancel); break;
            default: Add("OK", MessageBoxResult.OK, true); break;
        }
        root.Children.Add(actions);
        dialog.Content = root;
        AttachOwner(dialog, owner);
        dialog.ShowDialog();
        return selection;
    }

    public static string? Prompt(Window? owner, string title, string label, string initialValue)
    {
        var dialog = NewWindow(LocalizationService.Translate(title), 525);
        var root = NewLayout();
        root.Children.Add(new TextBlock { Text = LocalizationService.Translate(label), Margin = new Thickness(0, 0, 0, 12), TextWrapping = TextWrapping.Wrap });
        var input = new System.Windows.Controls.TextBox { Text = initialValue, MinHeight = 33, Padding = new Thickness(7), Margin = new Thickness(0, 0, 0, 18) };
        root.Children.Add(input);
        var actions = new WrapPanel { HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        bool accepted = false;
        var ok = new Button { Content = LocalizationService.Translate("OK"), MinWidth = 100, IsDefault = true, Margin = new Thickness(5) };
        ok.Click += (_, _) => { accepted = true; dialog.Close(); };
        var cancel = new Button { Content = LocalizationService.Translate("Cancel"), MinWidth = 100, IsCancel = true, Margin = new Thickness(5) };
        cancel.Click += (_, _) => dialog.Close();
        actions.Children.Add(ok); actions.Children.Add(cancel); root.Children.Add(actions);
        dialog.Content = root;
        AttachOwner(dialog, owner);
        dialog.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        dialog.ShowDialog();
        return accepted ? input.Text.Trim() : null;
    }

    private static Window NewWindow(string title, double width)
    {
        var dialog = new Window { Title = title, Width = width, MinWidth = 425, SizeToContent = SizeToContent.Height,
            MaxHeight = 730, ResizeMode = ResizeMode.CanResize, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI"), FontSize = 13, ShowInTaskbar = false };
        dialog.SetResourceReference(Window.BackgroundProperty, "WindowBg");
        dialog.SetResourceReference(Window.ForegroundProperty, "TextColor");
        LocalizationService.Register(dialog);
        return dialog;
    }
    private static StackPanel NewLayout() => new() { Margin = new Thickness(23), MinHeight = 108 };
    private static void AttachOwner(Window dialog, Window? owner)
    {
        if (owner is not null && owner.IsVisible && owner.WindowState != WindowState.Minimized && owner != dialog)
            dialog.Owner = owner;
        else if (Application.Current?.MainWindow is Window main && main.IsVisible &&
                 main.WindowState != WindowState.Minimized && main != dialog)
            dialog.Owner = main;
        else
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.ShowInTaskbar = true;
        }
    }
}
