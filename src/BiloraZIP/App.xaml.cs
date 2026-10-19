using BiloraZIP.Views;
using System.Windows;
using BiloraZIP.Services;

namespace BiloraZIP;

public partial class App : Application
{
    private Mutex? _singleInstanceMutex;
    private CancellationTokenSource? _pipeCancellation;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstanceMutex = new Mutex(true, SingleInstanceService.MutexName, out bool firstInstance);
        if (!firstInstance)
        {
            // Explorer's classic %1 verb can launch one process per selected file.
            // Preserve its existing collector before forwarding one grouped command.
            string[] forwarded = e.Args;
            if (forwarded.Length > 1 && forwarded[0].Equals("--compress", StringComparison.OrdinalIgnoreCase))
            {
                IReadOnlyList<string>? collected = await CompressionInvocationHub.CollectAsync(forwarded.Skip(1).ToArray());
                if (collected is null) { Shutdown(); return; }
                forwarded = ["--compress", .. collected];
            }
            if (!await SingleInstanceService.ForwardAsync(forwarded))
                System.Windows.MessageBox.Show("BiloraZIP is already running but could not receive the request. Try opening it from the system tray.", "BiloraZIP");
            Shutdown();
            return;
        }

        PreviewCacheService.CleanupOld();
        DragExportCache.CleanupOld();
        ThemeManager.Initialize();
        LocalizationService.Initialize();
        IReadOnlyList<string>? paths = null;
        if (e.Args.Length > 1 && e.Args[0].Equals("--compress", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                paths = await CompressionInvocationHub.CollectAsync(e.Args.Skip(1).ToArray());
                if (paths is null) { Shutdown(); return; }
            }
            catch (Exception ex)
            {
                BiloraDialog.Show("Could not collect selected files: " + ex.Message, "BiloraZIP");
                paths = e.Args.Skip(1).ToArray();
            }
        }
        // Explorer's action verbs are background operations, not a request to open
        // the full archive manager. An ordinary file association/open still shows it.
        bool shellAction = e.Args.Length > 0 && IsShellOperation(e.Args[0]);
        var mainWindow = new MainWindow(paths, shellAction);
        MainWindow = mainWindow;
        if (shellAction)
        {
            // Keep the manager minimized in the taskbar. Do not hide the only
            // main window: the user can still restore it even without tray support.
            mainWindow.WindowState = WindowState.Minimized;
            mainWindow.ShowActivated = false;
        }
        mainWindow.Show();
        _pipeCancellation = new CancellationTokenSource();
        CancellationToken cancel = _pipeCancellation.Token;
        _ = SingleInstanceService.ListenAsync(args =>
            Dispatcher.BeginInvoke(new Action(() => _ = mainWindow.ProcessExternalArgumentsAsync(args))), cancel);
    }

    internal static bool IsShellOperation(string verb) =>
        verb.Equals("--compress", StringComparison.OrdinalIgnoreCase) ||
        verb.Equals("--extract-to", StringComparison.OrdinalIgnoreCase) ||
        verb.Equals("--extract-here", StringComparison.OrdinalIgnoreCase) ||
        verb.Equals("--extract-subfolder", StringComparison.OrdinalIgnoreCase);

    protected override void OnExit(ExitEventArgs e)
    {
        _pipeCancellation?.Cancel();
        _pipeCancellation?.Dispose();
        if (_singleInstanceMutex is not null)
        {
            try { _singleInstanceMutex.ReleaseMutex(); }
            catch (ApplicationException) { } // secondary process never owned it
            _singleInstanceMutex.Dispose();
        }
        base.OnExit(e);
    }
}
