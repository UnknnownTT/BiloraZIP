using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Threading;
using BiloraZIP.Models;
using BiloraZIP.Services;
using BiloraZIP.Views;
using Microsoft.Win32;

namespace BiloraZIP;

public partial class MainWindow : Window
{
    private readonly ZipArchiveService _zip = new();
    private readonly ArchiveReadService _reader = new();
    private readonly EncryptedZipService _encryptedZip = new();
    private readonly TarArchiveService _tar = new();
    private readonly TarGzipArchiveService _tarGzip = new();
    private readonly BilzArchiveService _bilz = new();
    private readonly ZipRecoveryService _recovery = new();
    private string? _currentArchive;
    private string? _currentPassword;
    private bool _archiveEncrypted;
    private bool IsEditableZip => !_archiveEncrypted && _currentPassword is null && _currentArchive?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true;
    private bool _busy;
    private string? _currentFolder;
    private readonly Stack<string> _navigationHistory = new();
    private ProgressWindow? _progressDialog;
    private readonly List<DiagnosticItem> _diagnosticsLog = new();
    private CancellationTokenSource? _activeOperationCts;
    private IReadOnlyList<ArchiveEntryInfo> _archiveEntries = Array.Empty<ArchiveEntryInfo>();
    private string _archivePrefix = string.Empty;
    private readonly List<(Button Button, string Label, RoutedEventHandler Action)> _toolbarCommands = new();
    private bool _toolbarReady;
    private System.Windows.Forms.NotifyIcon? _trayIcon;
    private readonly SemaphoreSlim _externalCommandGate = new(1, 1);
    private bool _allowClose;
    private readonly IReadOnlyList<string>? _startupCompressionPaths;
    private readonly bool _shellStartup;
    // True only while handling a context-menu request. Dialogs must not be
    // owned by the hidden/minimized main window or Windows will hide them too.
    private bool _shellRequestActive;
    private System.Windows.Point? _pointerDown;
    private bool _pointerOnRow;
    private bool _marqueeActive;
    private bool _marqueeScrolled;
    private HashSet<ArchiveEntryInfo> _marqueePreviouslyTouched = new();
    private string? _dragCacheKey;
    private string[]? _dragCachePaths;
    private bool _outboundDragging;
    private string[] _cutPaths = Array.Empty<string>();
    private HashSet<ArchiveEntryInfo> _selectionAtMarqueeStart = new();
    private readonly DispatcherTimer _marqueeScrollTimer = new() { Interval = TimeSpan.FromMilliseconds(65) };
    private System.Windows.Point _lastMousePoint;


    public MainWindow(IReadOnlyList<string>? startupCompressionPaths = null, bool shellStartup = false)
    {
        _startupCompressionPaths = startupCompressionPaths;
        _shellStartup = shellStartup;
        InitializeComponent();
        LocalizationService.Register(this);
        EntriesList.ItemsSource = Array.Empty<ArchiveEntryInfo>();
        UpdateThemeButton();
        WelcomeDonationHint.Visibility = PreferencesService.Current.ShowDonationHint ? Visibility.Visible : Visibility.Collapsed;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        Loaded += MainWindow_Loaded;
        _toolbarCommands.AddRange(new (Button, string, RoutedEventHandler)[] {
            (AddButton, "Add", Create_Click), (ExtractButton, "Extract", Extract_Click),
            (TestButton, "Test", Test_Click), (ViewButton, "Open entry", View_Click),
            (DeleteButton, "Delete", Delete_Click), (FindButton, "Find", Find_Click),
            (InfoButton, "Info", Info_Click), (OpenToolbarButton, "Open archive", Open_Click),
            (FavoriteToolbarButton, "Favorite", FavoriteAdd_Click),
            (OptionsToolbarButton, "Options", Integration_Click)
        });
        _toolbarReady = true;
        UpdateLanguageMenu();
        Loaded += (_, _) => UpdateToolbarOverflow();
        Closing += MainWindow_Closing;
        InitializeTrayIcon();
        _marqueeScrollTimer.Tick += (_, _) => {
            if (_marqueeActive && Mouse.LeftButton == MouseButtonState.Pressed)
            {
                ScrollMarqueeIfNeeded();
                UpdateMarquee(_lastMousePoint);
            }
        };
    }

    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (_currentArchive is null && EntriesList.SelectedItems.Count > 0)
        {
            string[] currentSelection = EntriesList.SelectedItems.OfType<ArchiveEntryInfo>()
                .Select(entry => entry.FileSystemPath).OfType<string>().ToArray();
            if (currentSelection.Length > 0)
            {
                await CreateFromPathsAsync(currentSelection);
                return;
            }
        }
        var picker = new OpenFileDialog
        {
            Title = "Select files to compress",
            Multiselect = true,
            CheckFileExists = true
        };
        if (picker.ShowDialog(this) != true) return;
        string suggestion = Path.Combine(Path.GetDirectoryName(picker.FileNames[0])!, Path.GetFileNameWithoutExtension(picker.FileNames[0]) + ".zip");
        var options = new ArchiveOptionsWindow(suggestion) { Owner = this };
        if (options.ShowDialog() != true) return;

        string path = options.DestinationPath;
        CompressionLevelHolder level = new(options.Level, options.VerifyAfterCreate, options.Password, options.IsTar, options.IsBilz, options.IsTarGz, options.BilzSplitMiB);
        string[] selected = picker.FileNames;
        await RunProgressOperation("Creating ZIP archive", async (progress, token) =>
        {
            if (level.IsBilz)
                await _bilz.CreateAsync(path, selected, level.Level == System.IO.Compression.CompressionLevel.Fastest, progress, token);
            else if (level.IsTar)
                await _tar.CreateAsync(path, selected, progress, token);
            else if (level.IsTarGz)
                await _tarGzip.CreateAsync(path, selected, level.Level, progress, token);
            else if (level.Password is null)
                await _zip.CreateAsync(path, selected, level.Level, progress, token);
            else
                await _encryptedZip.CreateAsync(path, selected, level.Password, level.Level, progress, token);
            if (level.Verify) await _reader.TestAsync(path, progress, token, level.Password);
            if (level.IsBilz && level.BilzSplitMiB > 0)
                await BilzVolumeService.SplitAsync(path, level.BilzSplitMiB, token);
        }, async () =>
        {
            await LoadArchive(level.IsBilz && level.BilzSplitMiB > 0 ? path + ".001" : path, level.Password);
            StatusText.Text = "Archive created successfully.";
        });
    }

    private async void Folder_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var picker = new OpenFolderDialog { Title = "Select folder to compress" };
        if (picker.ShowDialog(this) != true) return;
        string source = picker.FolderName;
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(source)) + ".zip";
        var options = new ArchiveOptionsWindow(Path.Combine(Path.GetDirectoryName(source)!, name)) { Owner = this };
        if (options.ShowDialog() != true) return;
        string path = options.DestinationPath;
        CompressionLevelHolder level = new(options.Level, options.VerifyAfterCreate, options.Password, options.IsTar, options.IsBilz, options.IsTarGz, options.BilzSplitMiB);
        await RunProgressOperation("Creating ZIP archive", async (progress, token) =>
        {
            if (level.IsBilz)
                await _bilz.CreateAsync(path, [source], level.Level == System.IO.Compression.CompressionLevel.Fastest, progress, token);
            else if (level.IsTar)
                await _tar.CreateFolderAsync(path, source, progress, token);
            else if (level.IsTarGz)
                await _tarGzip.CreateFolderAsync(path, source, level.Level, progress, token);
            else if (level.Password is null)
                await _zip.CreateFolderAsync(path, source, level.Level, progress, token);
            else
                await _encryptedZip.CreateFolderAsync(path, source, level.Password, level.Level, progress, token);
            if (level.Verify) await _reader.TestAsync(path, progress, token, level.Password);
            if (level.IsBilz && level.BilzSplitMiB > 0)
                await BilzVolumeService.SplitAsync(path, level.BilzSplitMiB, token);
        }, async () =>
        {
            await LoadArchive(level.IsBilz && level.BilzSplitMiB > 0 ? path + ".001" : path, level.Password);
            StatusText.Text = "Folder compressed successfully.";
        });
    }

    private sealed record CompressionLevelHolder(System.IO.Compression.CompressionLevel Level, bool Verify, string? Password, bool IsTar, bool IsBilz, bool IsTarGz, int BilzSplitMiB);

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var pick = new OpenFileDialog { Title = "Open archive", Filter = "Archives (*.zip;*.rar;*.7z;*.tar;*.zipx;*.xapk;*.bilz;*.bilz.001;*.tar.gz;*.tgz)|*.zip;*.rar;*.7z;*.tar;*.zipx;*.xapk;*.bilz;*.bilz.001;*.tar.gz;*.tgz|All files (*.*)|*.*", CheckFileExists = true };
        if (pick.ShowDialog(this) != true) return;
        await RunSimpleOperation(() => LoadArchive(pick.FileName));
    }

    private void CloseArchive_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        string folder = _currentFolder ?? (_currentArchive is null ? HomeDirectory : Path.GetDirectoryName(_currentArchive)!);
        NavigateTo(folder);
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void InitializeTrayIcon()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            _trayIcon = new System.Windows.Forms.NotifyIcon {
                Text = "BiloraZIP", Icon = exe is null ? System.Drawing.SystemIcons.Application :
                    System.Drawing.Icon.ExtractAssociatedIcon(exe) ?? System.Drawing.SystemIcons.Application,
                Visible = false
            };
            _trayIcon.DoubleClick += (_, _) => Dispatcher.BeginInvoke(new Action(RestoreFromTray));
            var menu = new System.Windows.Forms.ContextMenuStrip();
            menu.Items.Add("Open BiloraZIP", null, (_, _) => Dispatcher.BeginInvoke(new Action(RestoreFromTray)));
            menu.Items.Add("Exit", null, (_, _) => Dispatcher.BeginInvoke(new Action(ExitFromTray)));
            _trayIcon.ContextMenuStrip = menu;
        }
        catch (Exception) { _trayIcon = null; } // Tray support can be absent in restricted desktops.
    }

    private T PrepareOperationDialog<T>(T dialog) where T : Window
    {
        // A minimized WPF owner also minimizes its owned windows. In shell mode
        // each small dialog is independent and visible on the taskbar.
        if (_shellRequestActive || !IsVisible || WindowState == WindowState.Minimized)
        {
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            dialog.ShowInTaskbar = true;
        }
        else dialog.Owner = this;
        return dialog;
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        if (_trayIcon is not null) _trayIcon.Visible = false;
    }

    private void ExitFromTray()
    {
        if (_busy)
        {
            RestoreFromTray();
            BiloraDialog.Show(this, "Wait for the current archive operation to finish or cancel it first.", "BiloraZIP");
            return;
        }
        _allowClose = true;
        _trayIcon?.Dispose();
        Close();
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) { _trayIcon?.Dispose(); return; }
        if (_busy)
        {
            e.Cancel = true;
            BiloraDialog.Show(this,
                LocalizationService.IsMongolian ? "Одоогийн үйлдлийг дуусгах эсвэл эхлээд цуцална уу." : "Finish or cancel the active operation before exiting.",
                "BiloraZIP", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        string mode = ClosePreferenceStore.Read();
        if (mode == "ask")
        {
            var dialog = new ExitChoiceWindow { Owner = this };
            if (dialog.ShowDialog() != true) { e.Cancel = true; return; }
            mode = dialog.Choice;
            if (dialog.Remember) ClosePreferenceStore.Save(mode);
        }
        if (mode == "tray")
        {
            if (_trayIcon is null)
            {
                e.Cancel = true;
                BiloraDialog.Show(this, "System tray is not available on this desktop.", "BiloraZIP");
                return;
            }
            e.Cancel = true;
            _trayIcon.Visible = true;
            Hide();
            return;
        }
        _allowClose = true;
        _trayIcon?.Dispose();
    }

    private async void Extract_Click(object sender, RoutedEventArgs e)
        => await ExtractWithOptionsAsync();

    private async Task ExtractWithOptionsAsync()
    {
        if (_busy || _currentArchive is null) return;
        string current = _currentArchive;
        var options = PrepareOperationDialog(new ExtractionOptionsWindow(Path.Combine(Path.GetDirectoryName(current)!, Path.GetFileNameWithoutExtension(current))));
        if (options.ShowDialog() != true) return;
        string destination = options.DestinationPath;
        string? password;
        try { password = await EnsureOperationPasswordAsync(current); }
        catch (OperationCanceledException) { return; }
        await RunProgressOperation("Extracting archive", (progress, token) => _reader.ExtractAsync(current, destination, progress, token, password, options.Policy), () =>
        {
            if (options.OpenAfter && Directory.Exists(destination))
                Process.Start(new ProcessStartInfo { FileName = destination, UseShellExecute = true });
            StatusText.Text = "Extraction finished successfully.";
            return Task.CompletedTask;
        });
    }

    private async void ExtractSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _currentArchive is null) return;
        string[] chosen = EntriesList.SelectedItems.OfType<ArchiveEntryInfo>().Select(x => x.Name).Distinct().ToArray();
        if (chosen.Length == 0)
        {
            BiloraDialog.Show(this, LocalizationService.IsMongolian ? "Эхлээд файл эсвэл хавтас сонгоно уу." : "Choose files or folders first.",
                "BiloraZIP", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        string path = _currentArchive;
        var options = new ExtractionOptionsWindow(Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path))) { Owner = this };
        if (options.ShowDialog() != true) return;
        string? password;
        try { password = await EnsureOperationPasswordAsync(path); }
        catch (OperationCanceledException) { return; }
        await RunProgressOperation("Extract selected", (progress, token) =>
            _reader.ExtractSelectedAsync(path, options.DestinationPath, chosen, progress, token, password, options.Policy), () =>
            {
                if (options.OpenAfter && Directory.Exists(options.DestinationPath))
                    Process.Start(new ProcessStartInfo { FileName = options.DestinationPath, UseShellExecute = true });
                StatusText.Text = LocalizationService.IsMongolian ? "Сонгосон файлуудыг задлалаа." : "Selected entries extracted.";
                return Task.CompletedTask;
            });
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _currentArchive is null) return;
        string current = _currentArchive;
        string? password;
        try { password = await EnsureOperationPasswordAsync(current); }
        catch (OperationCanceledException) { return; }
        await RunProgressOperation("Testing archive readability", (progress, token) => _reader.TestAsync(current, progress, token, password), () =>
        {
            StatusText.Text = "Archive entries were read successfully.";
            return Task.CompletedTask;
        });
    }

    private async void View_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _currentArchive is null || EntriesList.SelectedItem is not ArchiveEntryInfo selected) return;
        await OpenArchiveEntryAsync(selected);
    }

    private async Task OpenArchiveEntryAsync(ArchiveEntryInfo entry)
    {
        if (_currentArchive is null) return;
        if (entry.Type == "Folder")
        {
            _archivePrefix = NormalizeArchiveKey(entry.Name).TrimEnd('/') + "/";
            ShowArchiveDirectory();
            return;
        }
        string archivePath = _currentArchive;
        string extension = Path.GetExtension(entry.Name).ToLowerInvariant();
        string[] activeTypes = [".exe", ".com", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".msi", ".scr", ".lnk", ".hta", ".reg", ".url", ".wsf", ".wsh", ".cpl", ".jar"];
        if (activeTypes.Contains(extension) && BiloraDialog.Show(this,
            "This file can run code. Only open it if you trust the archive and its source. Continue?",
            "BiloraZIP security warning", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        try
        {
            string? password = await EnsureOperationPasswordAsync(archivePath);
            await RunSimpleOperation(async () =>
            {
                string preview = await _reader.ExtractPreviewAsync(archivePath, entry.Name, password, CancellationToken.None);
                Process.Start(new ProcessStartInfo(preview) { UseShellExecute = true });
                StatusText.Text = "Opened a temporary copy. Changes will not modify the archive.";
            });
        }
        catch (OperationCanceledException) { StatusText.Text = "File opening cancelled."; }
        catch (Exception ex) { HandleError(ex); }
    }

    private static string NormalizeArchiveKey(string key) => key.Replace('\\', '/');

    private void ShowArchiveDirectory()
    {
        if (_currentArchive is null) return;
        var folders = new Dictionary<string, ArchiveEntryInfo>(StringComparer.OrdinalIgnoreCase);
        var files = new List<ArchiveEntryInfo>();
        foreach (var entry in _archiveEntries)
        {
            string name = NormalizeArchiveKey(entry.Name);
            if (!name.StartsWith(_archivePrefix, StringComparison.OrdinalIgnoreCase)) continue;
            string rest = name[_archivePrefix.Length..];
            if (rest.Length == 0) continue;
            int slash = rest.IndexOf('/');
            if (slash >= 0)
            {
                string folder = _archivePrefix + rest[..slash] + "/";
                if (!folders.ContainsKey(folder))
                    folders[folder] = new ArchiveEntryInfo(folder, "—", "—", "Folder", "—", "—")
                    { IconSource = ShellIconService.Get(folder, true) };
            }
            else if (entry.Type != "Folder")
                files.Add(entry with { IconSource = ShellIconService.Get(entry.Name, false) });
        }
        var rows = folders.Values.OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Concat(files.OrderBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)).ToList();
        EntriesList.ItemsSource = rows;
        PathBox.Text = _currentArchive + (_archivePrefix.Length == 0 ? string.Empty : " / " + _archivePrefix);
        CountText.Text = $"{rows.Count} items";
        SearchBox.Clear();
        SetBusy(_busy);
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (_currentArchive is null)
        {
            await DeleteFilesystemSelectionAsync(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
            return;
        }
        if (!IsEditableZip || _currentArchive is null) return;
        var entries = EntriesList.SelectedItems.OfType<ArchiveEntryInfo>().ToArray();
        if (entries.Length == 0) return;
        if (BiloraDialog.Show(this,
            LocalizationService.Translate("Remove selected entries from ZIP permanently? This cannot be undone."),
            "BiloraZIP", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        string archive = _currentArchive;
        await RunProgressOperation("Removing ZIP entries", async (progress, token) => {
            foreach (var entry in entries)
            {
                token.ThrowIfCancellationRequested();
                await _zip.DeleteEntryAsync(archive, entry.Name, progress, token);
            }
        }, () => LoadArchive(archive));
    }

    // The context menu is rebuilt for the *actual* target each time. A filesystem
    // item, writable ZIP entry, and read-only RAR entry have different capabilities.
    private void EntriesList_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (_busy) { e.Handled = true; return; }
        var hit = FindEntryAt(Mouse.DirectlyOver as DependencyObject);
        if (hit is not null && !EntriesList.SelectedItems.Contains(hit))
            EntriesList.SelectedItem = hit;
        var paths = SelectedFilesystemPaths();
        bool canEditLocal = _currentArchive is null && _currentFolder is not null;
        bool filesystem = _currentArchive is null;
        bool hasSelection = EntriesList.SelectedItems.Count > 0;
        var menu = EntriesList.ContextMenu!;
        menu.Items.Clear();
        void Add(string caption, Action run, bool enabled = true, string shortcut = "")
        {
            var item = new MenuItem { Header = LocalizationService.Translate(caption), IsEnabled = enabled,
                InputGestureText = shortcut };
            item.Click += (_, _) => run();
            menu.Items.Add(item);
        }
        void Break() { if (menu.Items.Count > 0) menu.Items.Add(new Separator()); }
        if (filesystem)
        {
            Add("Open", () => OpenSelected_Click(this, new RoutedEventArgs()), hasSelection, "Enter");
            if (paths.Length > 0 && paths.All(ArchiveReadService.Supports))
            {
                Add("Extract Here", () => _ = ExtractSelectedFilesystemArchiveAsync(false));
                Add("Extract to named folder", () => _ = ExtractSelectedFilesystemArchiveAsync(true));
                Break();
            }
            if (paths.Length == 1 && Path.GetExtension(paths[0]).Equals(".iso", StringComparison.OrdinalIgnoreCase))
            {
                Add("Mount ISO image (read-only)…", () => _ = MountIsoPathAsync(paths[0], true));
                Add("Unmount ISO image…", () => _ = MountIsoPathAsync(paths[0], false));
                Break();
            }
            Add("Add to archive…", () => _ = CreateFromSelectionAsync(), paths.Length > 0);
            Add("Create ZIP archive", () => _ = CreateFromSelectionAsync(), paths.Length > 0);
            Break();
            Add("Cut", () => CopySelectedFiles(true), paths.Length > 0 && canEditLocal, "Ctrl+X");
            Add("Copy", () => CopySelectedFiles(false), paths.Length > 0, "Ctrl+C");
            Add("Paste", () => _ = PasteFilesystemAsync(), _currentFolder is not null && Clipboard.ContainsFileDropList(), "Ctrl+V");
            Break();
            Add("Rename", () => RenameSelectedFile(), paths.Length == 1 && canEditLocal, "F2");
            Add("Delete", () => _ = DeleteFilesystemSelectionAsync(false), paths.Length > 0 && canEditLocal, "Del");
            Add("Properties", () => ShowSelectedProperties(), paths.Length == 1, "Alt+Enter");
            Break();
            Add("New folder", () => CreateNewFolder(), _currentFolder is not null);
        }
        else
        {
            Add("Open", () => OpenSelected_Click(this, new RoutedEventArgs()), hasSelection, "Enter");
            Add("Extract selected…", () => ExtractSelected_Click(this, new RoutedEventArgs()), hasSelection);
            Add("Extract Here", () => ExtractHere_Click(this, new RoutedEventArgs()));
            Add("Extract to named folder", () => ExtractSubfolder_Click(this, new RoutedEventArgs()));
            Break();
            Add("Delete", () => Delete_Click(this, new RoutedEventArgs()), IsEditableZip && hasSelection, "Del");
            Add("Properties", () => Info_Click(this, new RoutedEventArgs()));
        }
        Break();
        Add("Select all", () => EntriesList.SelectAll(), true, "Ctrl+A");
    }

    private string[] SelectedFilesystemPaths() => EntriesList.SelectedItems.OfType<ArchiveEntryInfo>()
        .Select(x => x.FileSystemPath).OfType<string>().Where(p => File.Exists(p) || Directory.Exists(p))
        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

    private async void OpenSelected_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || EntriesList.SelectedItem is not ArchiveEntryInfo selected) return;
        if (_currentArchive is not null) { await OpenArchiveEntryAsync(selected); return; }
        if (selected.FileSystemPath is not string path) return;
        try
        {
            if (Directory.Exists(path)) NavigateTo(path);
            else if (ArchiveReadService.Supports(path)) await RunSimpleOperation(() => LoadArchive(path));
            else if (File.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) { HandleError(ex); }
    }

    private async Task CreateFromSelectionAsync()
    {
        var selected = SelectedFilesystemPaths();
        if (selected.Length > 0) await CreateFromPathsAsync(selected);
    }

    private async Task ExtractSelectedFilesystemArchiveAsync(bool subfolder)
    {
        string[] selected = SelectedFilesystemPaths().Where(ArchiveReadService.Supports).ToArray();
        foreach (var path in selected)
        {
            if (_busy) break;
            string folder = Path.GetDirectoryName(path)!;
            if (subfolder) folder = Path.Combine(folder, Path.GetFileNameWithoutExtension(path));
            await ExtractToPathAsync(path, folder);
        }
        if (_currentFolder is not null && Directory.Exists(_currentFolder)) NavigateTo(_currentFolder, false);
    }

    private void CopySelectedFiles(bool cut)
    {
        string[] paths = SelectedFilesystemPaths();
        if (paths.Length == 0) return;
        var list = new System.Collections.Specialized.StringCollection();
        list.AddRange(paths);
        Clipboard.SetFileDropList(list);
        _cutPaths = cut ? paths : Array.Empty<string>();
        StatusText.Text = LocalizationService.Translate(cut ? "Items ready to move." : "Items copied to clipboard.");
    }

    private async Task PasteFilesystemAsync()
    {
        if (_busy || _currentFolder is null || !Clipboard.ContainsFileDropList()) return;
        string[] paths = Clipboard.GetFileDropList().Cast<string>().ToArray();
        if (paths.Length == 0) return;
        string destination = _currentFolder;
        bool moving = _cutPaths.Length > 0 && paths.SequenceEqual(_cutPaths, StringComparer.OrdinalIgnoreCase);
        try
        {
            if (moving)
                await RunSimpleOperation(() => Task.Run(() => MovePaths(paths, destination)));
            else
                await RunSimpleOperation(() => Task.Run(() => CopyDroppedPaths(paths, destination)));
            if (moving) { _cutPaths = Array.Empty<string>(); Clipboard.Clear(); }
            NavigateTo(destination, addHistory: false);
        }
        catch (Exception ex) { HandleError(ex); }
    }

    private static void MovePaths(IEnumerable<string> paths, string destination)
    {
        foreach (string path in paths)
        {
            string target = Path.Combine(destination, Path.GetFileName(Path.TrimEndingDirectorySeparator(path)));
            if (string.Equals(Path.GetFullPath(path), Path.GetFullPath(target), StringComparison.OrdinalIgnoreCase)) continue;
            if (File.Exists(target) || Directory.Exists(target)) throw new IOException("Destination exists: " + target);
            if (Directory.Exists(path))
            {
                if (Path.GetFullPath(target).StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)) throw new IOException("Cannot move a folder into itself.");
                Directory.Move(path, target);
            }
            else if (File.Exists(path)) File.Move(path, target);
        }
    }

    private void RenameSelectedFile()
    {
        var paths = SelectedFilesystemPaths();
        if (_busy || _currentFolder is null || paths.Length != 1) return;
        string oldPath = paths[0];
        string? newName = BiloraDialog.Prompt(this, "Rename", "New name", Path.GetFileName(Path.TrimEndingDirectorySeparator(oldPath)));
        if (newName is null || newName.Length == 0) return;
        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || newName is "." or "..")
        { BiloraDialog.Show(this, "Invalid file or folder name."); return; }
        string target = Path.Combine(Path.GetDirectoryName(oldPath)!, newName);
        if (string.Equals(oldPath, target, StringComparison.OrdinalIgnoreCase)) return;
        if (File.Exists(target) || Directory.Exists(target))
        { BiloraDialog.Show(this, "A file or folder with this name already exists."); return; }
        try
        {
            if (Directory.Exists(oldPath)) Directory.Move(oldPath, target);
            else File.Move(oldPath, target);
            if (_currentFolder is not null) NavigateTo(_currentFolder, false);
        }
        catch (Exception ex) { HandleError(ex); }
    }

    private async Task DeleteFilesystemSelectionAsync(bool permanent)
    {
        string[] paths = SelectedFilesystemPaths();
        if (paths.Length == 0 || _currentArchive is not null || _currentFolder is null || _busy) return;
        string prompt = permanent ? "Permanently delete the selected items?" : "Move selected items to Recycle Bin?";
        if (BiloraDialog.Show(this, prompt, "BiloraZIP", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        string? folder = _currentFolder;
        await RunSimpleOperation(() => Task.Run(() => {
            foreach (string path in paths)
            {
                if (Directory.Exists(path))
                {
                    if (permanent) Directory.Delete(path, recursive: true);
                    else Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(path,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                }
                else if (File.Exists(path))
                {
                    if (permanent) File.Delete(path);
                    else Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(path,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                }
            }
        }));
        if (folder is not null && Directory.Exists(folder)) NavigateTo(folder, false);
        else NavigateThisPc(false);
    }

    private void ShowSelectedProperties()
    {
        if (_currentArchive is not null) { Info_Click(this, new RoutedEventArgs()); return; }
        string[] selected = SelectedFilesystemPaths();
        if (selected.Length != 1) return;
        string path = selected[0];
        try
        {
            var info = (FileSystemInfo)(Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path));
            string size = info is FileInfo file ? $"\n{LocalizationService.Translate("Size")}: {FormatBytes(file.Length)}" : string.Empty;
            BiloraDialog.Show(this, $"{LocalizationService.Translate("Name")}: {info.Name}\n{LocalizationService.Translate("Location")}: {info.FullName}{size}\n{LocalizationService.Translate("Modified")}: {info.LastWriteTime:yyyy-MM-dd HH:mm}\n{LocalizationService.Translate("Type")}: {(info is DirectoryInfo ? LocalizationService.Translate("Folder") : info.Extension)}",
                LocalizationService.Translate("Properties"));
        }
        catch (Exception ex) { HandleError(ex); }
    }

    private void CreateNewFolder()
    {
        if (_currentFolder is null || _busy) return;
        string? name = BiloraDialog.Prompt(this, "New folder", "Folder name", "New folder");
        if (string.IsNullOrWhiteSpace(name)) return;
        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name is "." or "..")
        { BiloraDialog.Show(this, "Invalid file or folder name."); return; }
        try { Directory.CreateDirectory(Path.Combine(_currentFolder, name)); NavigateTo(_currentFolder, false); }
        catch (Exception ex) { HandleError(ex); }
    }

    private void Find_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private async void Info_Click(object sender, RoutedEventArgs e)
    {
        if (_currentArchive is null)
        {
            BiloraDialog.Show(this, "Open an archive to view its information.", "BiloraZIP", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            string path = _currentArchive;
            var info = await Task.Run(() => _reader.GetMetadata(path, _currentPassword));
            new ArchiveInfoWindow(info) { Owner = this }.ShowDialog();
        }
        catch (Exception ex) { HandleError(ex); }
    }

    private void About_Click(object sender, RoutedEventArgs e)
        => new AboutWindow { Owner = this }.ShowDialog();

    private void Support_Click(object sender, RoutedEventArgs e)
        => new SupportWindow { Owner = this }.ShowDialog();

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (new SettingsWindow { Owner = this }.ShowDialog() == true)
            WelcomeDonationHint.Visibility = _currentArchive is null && _currentFolder is null && PreferencesService.Current.ShowDonationHint
                ? Visibility.Visible : Visibility.Collapsed;
    }

    private void FavoriteAdd_Click(object sender, RoutedEventArgs e)
    {
        if (_currentArchive is null)
        {
            BiloraDialog.Show(this, "Open an archive first.", "BiloraZIP", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        StatusText.Text = ThemeManager.AddFavorite(_currentArchive)
            ? "Archive added to favorites." : "Archive is already in favorites.";
    }

    private void FavoritesMenu_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        FavoritesMenu.Items.Clear();
        FavoritesMenu.Items.Add(new MenuItem
        {
            Header = "Add current archive to favorites",
            IsEnabled = _currentArchive is not null,
            Command = null
        });
        if (FavoritesMenu.Items[0] is MenuItem addItem) addItem.Click += FavoriteAdd_Click;
        FavoritesMenu.Items.Add(new Separator());
        foreach (string path in ThemeManager.Favorites.ToList())
        {
            var item = new MenuItem { Header = Path.GetFileName(path), ToolTip = path, Tag = path };
            item.Click += async (_, _) =>
            {
                if (_busy) return;
                if (!File.Exists(path))
                {
                    ThemeManager.RemoveFavorite(path);
                    BiloraDialog.Show(this, "This archive is no longer at the saved location.");
                    return;
                }
                await RunSimpleOperation(() => LoadArchive(path));
            };
            FavoritesMenu.Items.Add(item);
        }
        if (ThemeManager.Favorites.Count == 0)
            FavoritesMenu.Items.Add(new MenuItem { Header = "(No favorites saved)", IsEnabled = false });
    }

    private void LanguageEnglish_Click(object sender, RoutedEventArgs e) => ChangeLanguage("en");
    private void LanguageMongolian_Click(object sender, RoutedEventArgs e) => ChangeLanguage("mn");

    private void ChangeLanguage(string code)
    {
        LocalizationService.SetLanguage(code);
        UpdateLanguageMenu();
        UpdateThemeButton();
        UpdateToolbarOverflow();
    }

    private void UpdateLanguageMenu()
    {
        LanguageEnglishMenu.IsChecked = !LocalizationService.IsMongolian;
        LanguageMongolianMenu.IsChecked = LocalizationService.IsMongolian;
    }

    private void UpdateToolbarOverflow()
    {
        if (!_toolbarReady || CompactToolbar is null) return;
        // Reserve the theme switch and a fixed 92-pixel overflow button.
        double area = Math.Max(240, ActualWidth - 175);
        int total = _toolbarCommands.Count;
        int fit = (int)Math.Floor(area / 74.0);
        fit = Math.Clamp(fit, 2, total);
        if (fit < total) fit = Math.Max(2, (int)Math.Floor((area - 96) / 74.0));
        MoreMenu.Items.Clear();
        for (int i = 0; i < total; i++)
        {
            var command = _toolbarCommands[i];
            command.Button.Visibility = i < fit ? Visibility.Visible : Visibility.Collapsed;
            if (i < fit) continue;
            var item = new MenuItem { Header = LocalizationService.Translate(command.Label), IsEnabled = command.Button.IsEnabled };
            // App.xaml custom MenuItem template now renders the Icon slot explicitly.
            // Fetch the resource by stable command key instead of inspecting visual children.
            string? iconKey = command.Label switch
            {
                "Add" => "add", "Extract" => "extract", "Test" => "test",
                "Open entry" => "view", "Delete" => "delete", "Find" => "find",
                "Info" => "info", "Open archive" => "open", "Favorite" => "favorites",
                "Options" => "options", _ => null
            };
            if (iconKey is not null && TryFindResource("Icon_" + iconKey) is System.Windows.Media.ImageSource icon)
                item.Icon = new Image { Source = icon, Width = 20, Height = 20, Stretch = Stretch.Uniform };
            RoutedEventHandler handler = command.Action;
            item.Click += (sender, args) => handler(sender, args);
            MoreMenu.Items.Add(item);
        }
        MoreMenu.Visibility = fit == total ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void MountIso_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var picker = new OpenFileDialog { Title = "Mount ISO (read-only)", Filter = "ISO images (*.iso)|*.iso", CheckFileExists = true };
        if (picker.ShowDialog(this) != true) return;
        await MountIsoPathAsync(picker.FileName, true);
    }

    private async void UnmountIso_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var picker = new OpenFileDialog { Title = "Unmount ISO", Filter = "ISO images (*.iso)|*.iso", CheckFileExists = true };
        if (picker.ShowDialog(this) != true) return;
        await MountIsoPathAsync(picker.FileName, false);
    }

    private async Task MountIsoPathAsync(string path, bool mount)
    {
        try
        {
            if (mount && BiloraDialog.Show(this,
                LocalizationService.IsMongolian
                    ? "ISO-г зөвхөн унших виртуал дискээр холбох уу? ISO доторх программ автоматаар ажиллахгүй."
                    : "Mount this ISO as a read-only virtual drive? No program inside it will be executed automatically by BiloraZIP.",
                "BiloraZIP", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            string result = mount ? await IsoMountService.MountAsync(path) : await IsoMountService.UnmountAsync(path);
            BiloraDialog.Show(this, mount
                ? (LocalizationService.IsMongolian ? "ISO холбогдлоо. Диск: " : "ISO mounted. Drive: ") + (string.IsNullOrWhiteSpace(result) ? "Windows Explorer" : result)
                : (LocalizationService.IsMongolian ? "ISO салгагдлаа." : "ISO unmounted."));
        }
        catch (Exception ex) { HandleError(ex); }
    }

    private void DarkTheme_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.SetTheme("Dark");
        UpdateThemeButton();
        UpdateToolbarOverflow();
    }

    private void LightTheme_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.SetTheme("Light");
        UpdateThemeButton();
        UpdateToolbarOverflow();
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        ThemeManager.SetTheme(ThemeManager.IsDark ? "Light" : "Dark");
        UpdateThemeButton();
        UpdateToolbarOverflow();
    }

    private void UpdateThemeButton()
    {
        ThemeLabel.Text = LocalizationService.IsMongolian
            ? (ThemeManager.IsDark ? "Харанхуй" : "Гэрэлтэй")
            : (ThemeManager.IsDark ? "Dark" : "Light");
        string icon = ThemeManager.IsDark ? "Icon_light" : "Icon_dark";
        ThemeImage.SetResourceReference(System.Windows.Controls.Image.SourceProperty, icon);
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (EntriesList?.ItemsSource is null) return;
        ICollectionView view = CollectionViewSource.GetDefaultView(EntriesList.ItemsSource);
        string search = SearchBox.Text.Trim();
        view.Filter = obj => obj is ArchiveEntryInfo info &&
            (search.Length == 0 || info.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        CountText.Text = $"{EntriesList.Items.Count} shown";
    }

    private void EntriesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        bool chosen = EntriesList.SelectedItem is ArchiveEntryInfo && (IsEditableZip || (_currentArchive is null && _currentFolder is not null));
        DeleteButton.IsEnabled = MenuDelete.IsEnabled = chosen && !_busy;
        bool file = EntriesList.SelectedItem is ArchiveEntryInfo { Type: "File" } && _currentArchive is not null;
        ViewButton.IsEnabled = MenuView.IsEnabled = file && !_busy;
        UpdateToolbarOverflow();
    }

    private async void EntriesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Use the row actually double-clicked, not a previously selected row.
        // Double-clicks on empty space and column headers must not navigate.
        if (_busy || FindEntryAt(e.OriginalSource as DependencyObject) is not ArchiveEntryInfo row) return;
        _pointerDown = null; // a double-click is navigation, never an outbound drag
        e.Handled = true;
        if (_currentArchive is null && row.FileSystemPath is not null)
        {
            if (Directory.Exists(row.FileSystemPath)) NavigateTo(row.FileSystemPath);
            else if (File.Exists(row.FileSystemPath) && ArchiveReadService.Supports(row.FileSystemPath))
                await RunSimpleOperation(() => LoadArchive(row.FileSystemPath));
            else if (File.Exists(row.FileSystemPath))
                Process.Start(new ProcessStartInfo(row.FileSystemPath) { UseShellExecute = true });
            return;
        }
        await OpenArchiveEntryAsync(row);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        // Reject drops originating from this very list. A slight mouse movement
        // during a double-click can otherwise drop a folder onto itself.
        if (_outboundDragging)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        bool hasFiles = !_busy && e.Data.GetDataPresent(DataFormats.FileDrop);
        // Only a plain writable ZIP accepts additions. Filesystem mode copies into the visible folder.
        bool canDrop = hasFiles && (IsEditableZip || _currentFolder is not null ||
            (_currentArchive is null && _currentFolder is null &&
                e.Data.GetData(DataFormats.FileDrop) is string[] dropped && dropped.Length == 1 &&
                ArchiveReadService.Supports(dropped[0])));
        e.Effects = canDrop ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (_busy || _outboundDragging || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0) return;
        try
        {
            if (_currentArchive is not null && IsEditableZip)
            {
                string archive = _currentArchive;
                string prefix = _archivePrefix;
                // Drop onto a folder to target it; otherwise target the currently browsed archive directory.
                if (FindEntryAt(e.OriginalSource as DependencyObject) is { Type: "Folder" } target)
                    prefix = NormalizeArchiveKey(target.Name).TrimEnd('/') + "/";
                await RunProgressOperation("Adding files to ZIP",
                    (progress, token) => _zip.AddFilesAndFoldersAsync(archive, paths, progress, token, prefix),
                    async () => {
                        await LoadArchive(archive);
                        _archivePrefix = prefix;
                        ShowArchiveDirectory();
                        StatusText.Text = "Added items to the current ZIP folder.";
                    });
                return;
            }
            if (_currentArchive is not null) { StatusText.Text = "This archive is read-only; cannot add items."; return; }
            if (_currentFolder is not null && Directory.Exists(_currentFolder))
            {
                string folder = _currentFolder;
                if (FindEntryAt(e.OriginalSource as DependencyObject) is { Type: "Folder", FileSystemPath: not null } target)
                    folder = target.FileSystemPath;
                await RunSimpleOperation(() => Task.Run(() => CopyDroppedPaths(paths, folder)));
                NavigateTo(folder, addHistory: false);
                StatusText.Text = "Copied dropped items into " + folder;
                return;
            }
            if (paths.Length == 1 && File.Exists(paths[0]) && ArchiveReadService.Supports(paths[0]))
                await RunSimpleOperation(() => LoadArchive(paths[0]));
        }
        catch (Exception ex) { HandleError(ex); }
    }

    private static void CopyDroppedPaths(IReadOnlyList<string> paths, string folder)
    {
        string root = Path.GetFullPath(folder);
        foreach (string path in paths)
        {
            string full = Path.GetFullPath(path);
            string target = Path.Combine(root, Path.GetFileName(Path.TrimEndingDirectorySeparator(full)));
            // A source dragged from the currently displayed folder back onto
            // itself is a no-op, not a failed file operation.
            if (FileDropSafety.IsSamePath(full, target)) continue;
            if (File.Exists(full))
            {
                if ((File.GetAttributes(full) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Symbolic link files are not copied by drag/drop.");
                if (File.Exists(target) || Directory.Exists(target))
                    throw new IOException("Destination already exists (nothing overwritten): " + target);
                File.Copy(full, target);
            }
            else if (Directory.Exists(full))
            {
                if (FileDropSafety.IsInsideFolder(full, target))
                    throw new IOException("Cannot copy a folder into one of its subfolders.");
                if (File.Exists(target) || Directory.Exists(target))
                    throw new IOException("Destination already exists (nothing overwritten): " + target);
                CopyDirectorySafely(full, target);
            }
        }
    }

    private static void CopyDirectorySafely(string origin, string target)
    {
        if ((File.GetAttributes(origin) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Folder junctions are not copied.");
        Directory.CreateDirectory(target);
        foreach (string file in Directory.EnumerateFiles(origin))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: false);
        }
        foreach (string sub in Directory.EnumerateDirectories(origin))
            if ((File.GetAttributes(sub) & FileAttributes.ReparsePoint) == 0)
                CopyDirectorySafely(sub, Path.Combine(target, Path.GetFileName(sub)));
    }

    private static ArchiveEntryInfo? FindEntryAt(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ListViewItem row) return row.DataContext as ArchiveEntryInfo;
            source = source is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }
        return null;
    }

    // Route wheel events to the Explorer list's own ScrollViewer. In WPF a child
    // visual may consume the default ListView wheel event, leaving the list stuck.
    // The event is handled only if there is scrollable content and movement occurs.
    private void EntriesList_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_marqueeActive) return;
        ScrollViewer? viewer = FindScrollViewer(EntriesList);
        if (viewer is null || viewer.ScrollableHeight <= 0) return;
        double offset = ExplorerWheelScroll.CalculateOffset(viewer.VerticalOffset,
            viewer.ScrollableHeight, e.Delta, PreferencesService.Current.ExplorerWheelLines);
        if (Math.Abs(offset - viewer.VerticalOffset) < 0.001) return;
        viewer.ScrollToVerticalOffset(offset);
        e.Handled = true;
    }

    private void EntriesList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_busy || _outboundDragging) return;
        // Second click of a double-click must not arm a drag operation.
        if (e.ClickCount > 1) { _pointerDown = null; return; }
        _pointerDown = e.GetPosition(EntriesList);
        _lastMousePoint = _pointerDown.Value;
        _pointerOnRow = FindEntryAt(e.OriginalSource as DependencyObject) is not null;
        _selectionAtMarqueeStart = EntriesList.SelectedItems.OfType<ArchiveEntryInfo>().ToHashSet();
        _marqueeScrolled = false;
        _marqueePreviouslyTouched.Clear();
    }

    private void EntriesList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_marqueeActive)
        {
            EndMarquee();
            e.Handled = true;
        }
        _pointerDown = null;
    }

    private async void EntriesList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_busy || _outboundDragging || _pointerDown is null) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            if (_marqueeActive) EndMarquee();
            _pointerDown = null;
            return;
        }
        System.Windows.Point now = e.GetPosition(EntriesList);
        _lastMousePoint = now;
        // Require a deliberate movement to avoid interpreting a slightly shaky
        // double-click as a drag from the Explorer list.
        bool threshold = Math.Abs(now.X - _pointerDown.Value.X) >= Math.Max(12, SystemParameters.MinimumHorizontalDragDistance) ||
                         Math.Abs(now.Y - _pointerDown.Value.Y) >= Math.Max(12, SystemParameters.MinimumVerticalDragDistance);
        if (!_marqueeActive && !threshold) return;
        if (_pointerOnRow)
        {
            _pointerDown = null;
            _outboundDragging = true;
            try { await BeginExportDragAsync(); }
            catch (Exception ex) { HandleError(ex); }
            finally { _outboundDragging = false; }
            return;
        }
        if (!_marqueeActive)
        {
            _marqueeActive = true;
            MarqueeBorder.Visibility = Visibility.Visible;
            EntriesList.CaptureMouse();
            _marqueeScrollTimer.Start();
        }
        UpdateMarquee(now);
        e.Handled = true;
    }

    private void UpdateMarquee(System.Windows.Point point)
    {
        if (!_marqueeActive || _pointerDown is null) return;
        double l = Math.Max(0, Math.Min(_pointerDown.Value.X, point.X));
        double t = Math.Max(0, Math.Min(_pointerDown.Value.Y, point.Y));
        double r = Math.Min(EntriesList.ActualWidth, Math.Max(_pointerDown.Value.X, point.X));
        double b = Math.Min(EntriesList.ActualHeight, Math.Max(_pointerDown.Value.Y, point.Y));
        Canvas.SetLeft(MarqueeBorder, l);
        Canvas.SetTop(MarqueeBorder, t);
        MarqueeBorder.Width = Math.Max(0, r - l);
        MarqueeBorder.Height = Math.Max(0, b - t);
        var selectionRect = new Rect(new System.Windows.Point(l, t), new System.Windows.Point(r, b));
        bool keepExisting = (Keyboard.Modifiers & ModifierKeys.Control) != 0;
        EntriesList.SelectionChanged -= EntriesList_SelectionChanged;
        try
        {
            var desired = new HashSet<ArchiveEntryInfo>(keepExisting
                ? _selectionAtMarqueeStart : Enumerable.Empty<ArchiveEntryInfo>());
            if (_marqueeScrolled) desired.UnionWith(_marqueePreviouslyTouched);
            foreach (ArchiveEntryInfo row in EntriesList.Items.OfType<ArchiveEntryInfo>())
            {
                if (EntriesList.ItemContainerGenerator.ContainerFromItem(row) is not ListViewItem container) continue;
                var topLeft = container.TranslatePoint(new System.Windows.Point(0, 0), EntriesList);
                var rect = new Rect(topLeft, new Size(container.ActualWidth, container.ActualHeight));
                if (selectionRect.IntersectsWith(rect))
                {
                    desired.Add(row);
                    if (_marqueeScrolled) _marqueePreviouslyTouched.Add(row);
                }
            }
            foreach (ArchiveEntryInfo row in EntriesList.SelectedItems.OfType<ArchiveEntryInfo>().ToArray())
                if (!desired.Contains(row)) EntriesList.SelectedItems.Remove(row);
            foreach (ArchiveEntryInfo row in desired)
                if (!EntriesList.SelectedItems.Contains(row)) EntriesList.SelectedItems.Add(row);
        }
        finally { EntriesList.SelectionChanged += EntriesList_SelectionChanged; }
    }

    private void ScrollMarqueeIfNeeded()
    {
        if (!_marqueeActive) return;
        var viewer = FindScrollViewer(EntriesList);
        if (viewer is null) return;
        if (_lastMousePoint.Y >= EntriesList.ActualHeight - 18)
        {
            _marqueePreviouslyTouched.UnionWith(EntriesList.SelectedItems.OfType<ArchiveEntryInfo>());
            _marqueeScrolled = true;
            viewer.ScrollToVerticalOffset(viewer.VerticalOffset + 1);
        }
        else if (_lastMousePoint.Y < 20)
        {
            _marqueePreviouslyTouched.UnionWith(EntriesList.SelectedItems.OfType<ArchiveEntryInfo>());
            _marqueeScrolled = true;
            viewer.ScrollToVerticalOffset(viewer.VerticalOffset - 1);
        }
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject node)
    {
        if (node is ScrollViewer viewer) return viewer;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            if (FindScrollViewer(VisualTreeHelper.GetChild(node, i)) is { } child) return child;
        return null;
    }

    private void EndMarquee()
    {
        _marqueeActive = false;
        _pointerDown = null;
        _marqueeScrollTimer.Stop();
        MarqueeBorder.Visibility = Visibility.Collapsed;
        EntriesList.ReleaseMouseCapture();
        SetBusy(_busy);
    }

    private async Task BeginExportDragAsync()
    {
        var selected = EntriesList.SelectedItems.OfType<ArchiveEntryInfo>().ToArray();
        if (selected.Length == 0) return;
        string[] exportPaths;
        if (_currentArchive is null)
        {
            exportPaths = selected.Select(item => item.FileSystemPath).OfType<string>().Where(path => File.Exists(path) || Directory.Exists(path)).ToArray();
        }
        else
        {
            string archive = _currentArchive;
            string[] chosen = selected.Select(item => NormalizeArchiveKey(item.Name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            // Selected folder already includes its children; only export topmost selected items.
            chosen = chosen.Where(key => !chosen.Any(parent => parent != key && key.StartsWith(parent.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase))).ToArray();
            string cacheKey = archive + "|" + File.GetLastWriteTimeUtc(archive).Ticks + "|" + string.Join(";", chosen.OrderBy(k => k));
            if (_dragCacheKey == cacheKey && _dragCachePaths is { Length: > 0 } saved &&
                saved.All(path => File.Exists(path) || Directory.Exists(path)))
            {
                exportPaths = saved;
            }
            else
            {
                string temp = DragExportCache.CreateSession();
                bool success = false;
                string? password;
                try { password = await EnsureOperationPasswordAsync(archive); }
                catch (OperationCanceledException) { return; }
                await RunProgressOperation("Preparing files for Explorer", (progress, token) =>
                    _reader.ExtractSelectedAsync(archive, temp, chosen, progress, token, password),
                    () => { success = true; return Task.CompletedTask; });
                if (!success) return;
                exportPaths = chosen.Select(k => Path.Combine(temp, k.Replace('/', Path.DirectorySeparatorChar)))
                    .Where(path => File.Exists(path) || Directory.Exists(path)).ToArray();
                _dragCacheKey = cacheKey;
                _dragCachePaths = exportPaths;
            }
        }
        if (exportPaths.Length == 0) return;
        if (Mouse.LeftButton != MouseButtonState.Pressed)
        {
            StatusText.Text = "Files are prepared. Drag the selected items again to copy into Explorer.";
            return;
        }
        // Explorer receives real filesystem paths only after safe extraction finishes.
        var data = new System.Windows.DataObject();
        data.SetData(DataFormats.FileDrop, exportPaths);
        System.Windows.DragDrop.DoDragDrop(EntriesList, data, DragDropEffects.Copy);
    }

    private async Task LoadArchive(string path, string? knownPassword = null)
    {
        string? password = knownPassword;
        IReadOnlyList<ArchiveEntryInfo> entries;
        try { entries = await Task.Run(() => _reader.List(path, password)); }
        catch (Exception ex) when (ArchiveReadService.IsPasswordException(ex))
        {
            password = await PromptAndVerifyPasswordAsync(path);
            entries = await Task.Run(() => _reader.List(path, password));
        }
        _currentArchive = path;
        _currentPassword = password;
        _archiveEncrypted = await Task.Run(() => _reader.NeedsPassword(path));
        if (PreferencesService.Current.WarnMixedEncryption && Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var counts = await Task.Run(() => _reader.GetZipEncryptionCounts(path));
                if (counts.Encrypted > 0 && counts.Plain > 0)
                    BiloraDialog.Show(this, LocalizationService.IsMongolian
                        ? $"Анхааруулга: ZIP дотор {counts.Encrypted} шифрлэсэн, {counts.Plain} хамгаалалтгүй файл байна. Хамгаалалтгүй файлууд нууц үг шаардахгүй."
                        : $"Warning: this ZIP contains {counts.Encrypted} encrypted and {counts.Plain} unencrypted files. Unencrypted files are not password-protected.",
                        "BiloraZIP", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or ICSharpCode.SharpZipLib.Zip.ZipException) { }
        }
        _archiveEntries = entries;
        _archivePrefix = string.Empty;
        WelcomeDonationHint.Visibility = Visibility.Collapsed;
        ArchiveTitle.Text = Path.GetFileName(path);
        string formatLabel = BilzVolumeService.IsFirstVolume(path) ? "BILZ (multi-volume)" :
            path.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase) ? "TAR.GZ" :
            Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
        ArchiveDetail.Text = $"{formatLabel} • {FormatBytes(new FileInfo(path).Length)} • {entries.Count} entries";
        ShowArchiveDirectory();
        StatusText.Text = "Archive opened successfully.";
    }

    private async Task<string> PromptAndVerifyPasswordAsync(string path)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            var prompt = PrepareOperationDialog(new PasswordPromptWindow(path));
            if (prompt.ShowDialog() != true) throw new OperationCanceledException("Password entry cancelled.");
            try
            {
                await _reader.VerifyPasswordAsync(path, prompt.Password, CancellationToken.None);
                return prompt.Password;
            }
            catch (Exception ex)
            {
                BiloraDialog.Show(this, $"Unable to unlock archive ({attempt + 1}/3): {ex.Message}",
                    "BiloraZIP", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        throw new UnauthorizedAccessException("Could not unlock this archive.");
    }

    private async Task<string?> EnsureOperationPasswordAsync(string path)
    {
        if (_currentPassword is not null) return _currentPassword;
        bool encrypted = await Task.Run(() => _reader.NeedsPassword(path));
        if (!encrypted) return null;
        _currentPassword = await PromptAndVerifyPasswordAsync(path);
        return _currentPassword;
    }

    private async Task RunSimpleOperation(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        SetBusy(true);
        try { await action(); }
        catch (OperationCanceledException) { StatusText.Text = "Password entry cancelled."; }
        catch (Exception ex) { HandleError(ex); }
        finally { _busy = false; SetBusy(false); }
    }

    private async Task RunProgressOperation(string title,
        Func<IProgress<OperationProgressInfo>, CancellationToken, Task> operation, Func<Task> success)
    {
        if (_busy) return;
        _busy = true;
        SetBusy(true);
        using var cts = new CancellationTokenSource();
        _activeOperationCts = cts;
        var dialog = PrepareOperationDialog(new ProgressWindow(title));
        _progressDialog = dialog;
        dialog.CancelRequested += (_, _) => cts.Cancel();
        dialog.Show();
        var progress = new Progress<OperationProgressInfo>(update =>
        {
            dialog.Update(update);
            StatusText.Text = $"{update.Stage}: {update.Percent:0.0}% — {update.CurrentItem}";
        });
        try
        {
            await operation(progress, cts.Token);
            dialog.Finish("Completed");
            await success();
            await Task.Delay(550);
        }
        catch (OperationCanceledException)
        {
            dialog.Finish("Cancelled");
            StatusText.Text = "Operation cancelled. Files already extracted may remain.";
            await Task.Delay(500);
        }
        catch (Exception ex)
        {
            dialog.Finish("Failed", ErrorMessageService.Describe(ex));
            HandleError(ex);
            await Task.Delay(450);
        }
        finally
        {
            dialog.CompleteAndClose();
            _progressDialog = null;
            _activeOperationCts = null;
            _busy = false;
            SetBusy(false);
        }
    }

    private void ShowProgress_Click(object sender, RoutedEventArgs e)
    {
        if (_progressDialog is null) return;
        _progressDialog.Show();
        _progressDialog.Activate();
    }

    private void SetBusy(bool busy)
    {
        AddButton.IsEnabled = FolderButton.IsEnabled = !busy;
        ExtractButton.IsEnabled = MenuExtract.IsEnabled = !busy && _currentArchive is not null;
        ExtractHereButton.IsEnabled = MenuExtractHere.IsEnabled = MenuExtractFolder.IsEnabled = !busy && _currentArchive is not null;
        TestButton.IsEnabled = MenuTest.IsEnabled = !busy && _currentArchive is not null;
        ShowProgressButton.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        bool selected = EntriesList.SelectedItem is ArchiveEntryInfo;
        DeleteButton.IsEnabled = MenuDelete.IsEnabled = !busy && selected && (IsEditableZip || (_currentArchive is null && _currentFolder is not null));
        ViewButton.IsEnabled = MenuView.IsEnabled = !busy && _currentArchive is not null &&
                             EntriesList.SelectedItem is ArchiveEntryInfo { Type: "File" };
        UpdateToolbarOverflow();
    }

    private void HandleError(Exception ex)
    {
        StatusText.Text = LocalizationService.IsMongolian ? ErrorMessageService.Describe(ex) : "Operation failed: " + ex.Message;
        var issues = new List<DiagnosticItem>();
        for (Exception? current = ex; current is not null; current = current.InnerException)
            issues.Add(new DiagnosticItem(LocalizationService.Translate("Error"),
                PreferencesService.Current.ShowDetailedErrors ? current.GetType().Name + ": " + ErrorMessageService.Describe(current) : ErrorMessageService.Describe(current)));
        _diagnosticsLog.AddRange(issues);
        // If the operation has a progress dialog, put diagnostics in front of it.
        var dialog = new DiagnosticsWindow(issues, _activeOperationCts is not null)
        {
            Owner = _progressDialog is { IsVisible: true } ? _progressDialog :
                _shellRequestActive || !IsVisible || WindowState == WindowState.Minimized ? null : this,
            ShowInTaskbar = _shellRequestActive || !IsVisible || WindowState == WindowState.Minimized
        };
        if (dialog.Owner is null) dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        dialog.BreakRequested += (_, _) => _activeOperationCts?.Cancel();
        dialog.ShowDialog();
    }

    private static string FormatBytes(long size)
    {
        string[] names = ["B", "KB", "MB", "GB", "TB"];
        double amount = size;
        int index = 0;
        while (amount >= 1024 && index < names.Length - 1) { amount /= 1024; index++; }
        return $"{amount:0.#} {names[index]}";
    }

    private static string HomeDirectory => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private const string ThisPc = "This PC";

    private void NavigateThisPc(bool addHistory = true)
    {
        if (addHistory && (_currentArchive is not null || _currentFolder is not null))
            _navigationHistory.Push(_currentArchive ?? _currentFolder!);
        _currentArchive = null;
        _archivePrefix = string.Empty;
        _archiveEntries = Array.Empty<ArchiveEntryInfo>();
        _currentPassword = null;
        _archiveEncrypted = false;
        _currentFolder = null;
        var rows = new List<ArchiveEntryInfo>();
        foreach (var item in new[] {
            ("Desktop", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)),
            ("Documents", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
            ("Downloads", Path.Combine(HomeDirectory, "Downloads")) })
            if (Directory.Exists(item.Item2))
                rows.Add(new ArchiveEntryInfo(item.Item1, "—", "—", "Folder", "—", "—") { FileSystemPath = item.Item2, IconSource = ShellIconService.Get(item.Item2, true) });
        foreach (DriveInfo drive in DriveInfo.GetDrives())
            rows.Add(new ArchiveEntryInfo(drive.Name, "—", "—", "Drive", "—", "—") { FileSystemPath = drive.RootDirectory.FullName, IconSource = ShellIconService.Get(drive.RootDirectory.FullName, true) });
        EntriesList.ItemsSource = rows;
        PathBox.Text = ThisPc;
        WelcomeDonationHint.Visibility = PreferencesService.Current.ShowDonationHint ? Visibility.Visible : Visibility.Collapsed;
        ArchiveTitle.Text = "Welcome to BiloraZIP";
        ArchiveDetail.Text = "Choose a drive or common location";
        SearchBox.Clear();
        CountText.Text = $"{rows.Count} locations";
        StatusText.Text = "Ready";
        SetBusy(_busy);
    }

    private void NavigateTo(string folder, bool addHistory = true)
    {
        string full = Path.GetFullPath(folder);
        if (!Directory.Exists(full)) throw new DirectoryNotFoundException($"Folder not found: {full}");
        if (addHistory)
            _navigationHistory.Push(_currentArchive ?? _currentFolder ?? ThisPc);

        _currentArchive = null;
        _archivePrefix = string.Empty;
        _archiveEntries = Array.Empty<ArchiveEntryInfo>();
        _currentPassword = null;
        _archiveEncrypted = false;
        _currentFolder = full;
        var rows = new List<ArchiveEntryInfo>();
        try
        {
            foreach (string directory in Directory.EnumerateDirectories(full).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                rows.Add(new ArchiveEntryInfo(Path.GetFileName(directory), "—", "—", "Folder", "—", "—") { FileSystemPath = directory, IconSource = ShellIconService.Get(directory, true) });
            foreach (string file in Directory.EnumerateFiles(full).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                var info = new FileInfo(file);
                rows.Add(new ArchiveEntryInfo(info.Name, FormatBytes(info.Length), "—", Path.GetExtension(file).ToUpperInvariant(), "—", info.LastWriteTime.ToString("yyyy-MM-dd HH:mm")) { FileSystemPath = file, IconSource = ShellIconService.Get(file, false) });
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            StatusText.Text = "Cannot list all files: " + ex.Message;
        }
        EntriesList.ItemsSource = rows;
        PathBox.Text = full;
        string folderLabel = Path.GetFileName(Path.TrimEndingDirectorySeparator(full));
        WelcomeDonationHint.Visibility = Visibility.Collapsed;
        ArchiveTitle.Text = string.IsNullOrEmpty(folderLabel) ? full : folderLabel;
        ArchiveDetail.Text = "Local folder · double-click directories or supported archives";
        SearchBox.Clear();
        CountText.Text = $"{rows.Count} items";
        StatusText.Text = "Folder opened";
        SetBusy(_busy);
    }

    private async Task NavigateToPathAsync(string path)
    {
        if (path.Trim().Equals(ThisPc, StringComparison.OrdinalIgnoreCase)) { NavigateThisPc(); return; }
        string full = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));
        if (Directory.Exists(full)) NavigateTo(full);
        else if (File.Exists(full) && ArchiveReadService.Supports(full)) await RunSimpleOperation(() => LoadArchive(full));
        else BiloraDialog.Show(this, "This path is not an existing folder or supported archive.", "BiloraZIP", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void PathBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        await NavigateToPathAsync(PathBox.Text);
    }

    private async void Back_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (_currentArchive is not null && _archivePrefix.Length > 0)
        {
            Up_Click(sender, e);
            return;
        }
        if (_navigationHistory.TryPop(out string? previous))
        {
            if (Directory.Exists(previous)) NavigateTo(previous, addHistory: false);
            else await NavigateToPathAsync(previous);
        }
        else if (_currentArchive is not null) NavigateTo(Path.GetDirectoryName(_currentArchive)!);
    }

    private void Up_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (_currentArchive is not null && _archivePrefix.Length > 0)
        {
            string parentPrefix = _archivePrefix.TrimEnd('/');
            int boundary = parentPrefix.LastIndexOf('/');
            _archivePrefix = boundary < 0 ? string.Empty : parentPrefix[..(boundary + 1)];
            ShowArchiveDirectory();
            return;
        }
        string? parent = _currentArchive is not null ? Path.GetDirectoryName(_currentArchive) :
            _currentFolder is null ? null : Directory.GetParent(_currentFolder)?.FullName;
        if (parent is not null) NavigateTo(parent);
        else NavigateThisPc();
    }

    private void Home_Click(object sender, RoutedEventArgs e)
    {
        if (!_busy) NavigateThisPc();
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateToolbarOverflow();

    // Requests from Explorer/shortcuts are sent to this existing window by the
    // per-user single-instance IPC service. Tray-hidden windows are restored.
    public async Task ProcessExternalArgumentsAsync(string[] args)
    {
        // IPC from Explorer must not restore a tray-hidden or minimized manager.
        // Normal explicit --open requests still bring the manager to the front.
        bool shellAction = args.Length > 0 && App.IsShellOperation(args[0]);
        await _externalCommandGate.WaitAsync();
        bool previousShellMode = _shellRequestActive;
        try
        {
            if (!shellAction) RestoreFromTray();
            _shellRequestActive = shellAction;
            for (int i = 0; i < 1200 && _busy; i++) await Task.Delay(100);
            if (_busy)
            {
                BiloraDialog.Show(this, "The current operation is still in progress. Please retry your request.");
                return;
            }
            await ProcessLaunchArgumentsAsync(args);
        }
        finally
        {
            _shellRequestActive = previousShellMode;
            _externalCommandGate.Release();
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_shellStartup)
        {
            // The main window was opened minimized. Show shell dialogs as
            // independent taskbar windows, never owned by this minimized window.
            _shellRequestActive = true;
        }
        try
        {
            if (_startupCompressionPaths is { Count: > 0 })
            {
                await CreateFromPathsAsync(_startupCompressionPaths);
                return;
            }
            await ProcessLaunchArgumentsAsync(Environment.GetCommandLineArgs().Skip(1).ToArray());
        }
        catch (Exception ex) { HandleError(ex); }
        finally { _shellRequestActive = false; }
    }

    private async Task ProcessLaunchArgumentsAsync(string[] arguments)
    {
        if (arguments.Length == 0)
        {
            if (_currentArchive is null && _currentFolder is null) NavigateThisPc(addHistory: false);
            return;
        }
        string action = arguments[0].StartsWith("--", StringComparison.Ordinal) ? arguments[0] : "--open";
        string target = action == "--open" && arguments[0] != "--open" ? arguments[0] : arguments.ElementAtOrDefault(1) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(target)) return;
        try
        {
            if (action == "--compress")
            {
                await CreateFromPathsAsync(arguments.Skip(1).ToArray());
                return;
            }
            if (!File.Exists(target)) throw new FileNotFoundException("Archive no longer exists.", target);
            if (!ArchiveReadService.Supports(target)) throw new NotSupportedException("This archive format is not supported for reading.");
            await RunSimpleOperation(() => LoadArchive(target));
            if (_currentArchive is null) return;
            if (action == "--extract-to")
            {
                // Await the entire extraction, not an async-void click handler.
                // Otherwise shell mode ends before the progress dialog opens.
                await ExtractWithOptionsAsync();
            }
            else if (action == "--extract-here" || action == "--extract-subfolder")
            {
                string parent = Path.GetDirectoryName(target) ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string destination = action == "--extract-here" ? parent : Path.Combine(parent, Path.GetFileNameWithoutExtension(target));
                await ExtractToPathAsync(target, destination);
            }
        }
        catch (Exception ex) { HandleError(ex); }
    }

    // One operation, one options dialog, and one archive for all selected Explorer items.
    private async Task CreateFromPathsAsync(IReadOnlyCollection<string> paths)
    {
        string[] sources = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (sources.Length == 0) return;
        foreach (string path in sources)
            if (!File.Exists(path) && !Directory.Exists(path))
                throw new FileNotFoundException("Selected item no longer exists.", path);
        string root = Path.GetDirectoryName(Path.GetFullPath(sources[0]))!;
        string name = sources.Length == 1
            ? Path.GetFileNameWithoutExtension(Path.TrimEndingDirectorySeparator(sources[0]))
            : "Selected files";
        var options = PrepareOperationDialog(new ArchiveOptionsWindow(Path.Combine(root, name + ".zip")));
        if (options.ShowDialog() != true) return;
        await RunProgressOperation("Creating archive", async (progress, token) => {
            if (options.IsBilz)
                await _bilz.CreateAsync(options.DestinationPath, sources, options.Level == System.IO.Compression.CompressionLevel.Fastest, progress, token);
            else if (options.IsTar)
                await _tar.CreateMixedAsync(options.DestinationPath, sources, progress, token);
            else if (options.IsTarGz)
                await _tarGzip.CreateAsync(options.DestinationPath, sources, options.Level, progress, token);
            else if (options.Password is null)
                await _zip.CreateMixedAsync(options.DestinationPath, sources, options.Level, progress, token);
            else
                await _encryptedZip.CreateMixedAsync(options.DestinationPath, sources, options.Password, options.Level, progress, token);
            if (options.VerifyAfterCreate)
                await _reader.TestAsync(options.DestinationPath, progress, token, options.Password);
            if (options.IsBilz && options.BilzSplitMiB > 0)
                await BilzVolumeService.SplitAsync(options.DestinationPath, options.BilzSplitMiB, token);
        }, () => LoadArchive(options.IsBilz && options.BilzSplitMiB > 0 ? options.DestinationPath + ".001" : options.DestinationPath, options.Password));
    }

    private async Task CreateFromFilePathAsync(string path)
    {
        var options = new ArchiveOptionsWindow(Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + ".zip")) { Owner = this };
        if (options.ShowDialog() != true) return;
        await RunProgressOperation("Creating ZIP archive", async (progress, token) => {
            if (options.IsBilz)
                await _bilz.CreateAsync(options.DestinationPath, [path], options.Level == System.IO.Compression.CompressionLevel.Fastest, progress, token);
            else if (options.IsTar)
                await _tar.CreateAsync(options.DestinationPath, [path], progress, token);
            else if (options.IsTarGz)
                await _tarGzip.CreateAsync(options.DestinationPath, [path], options.Level, progress, token);
            else if (options.Password is null)
                await _zip.CreateAsync(options.DestinationPath, [path], options.Level, progress, token);
            else
                await _encryptedZip.CreateAsync(options.DestinationPath, [path], options.Password, options.Level, progress, token);
            if (options.VerifyAfterCreate) await _reader.TestAsync(options.DestinationPath, progress, token, options.Password);
            if (options.IsBilz && options.BilzSplitMiB > 0)
                await BilzVolumeService.SplitAsync(options.DestinationPath, options.BilzSplitMiB, token);
        }, () => LoadArchive(options.IsBilz && options.BilzSplitMiB > 0 ? options.DestinationPath + ".001" : options.DestinationPath, options.Password));
    }

    private async Task CreateFromFolderPathAsync(string path)
    {
        var options = new ArchiveOptionsWindow(Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)) + ".zip")) { Owner = this };
        if (options.ShowDialog() != true) return;
        await RunProgressOperation("Creating ZIP archive", async (progress, token) => {
            if (options.IsBilz)
                await _bilz.CreateAsync(options.DestinationPath, [path], options.Level == System.IO.Compression.CompressionLevel.Fastest, progress, token);
            else if (options.IsTar)
                await _tar.CreateFolderAsync(options.DestinationPath, path, progress, token);
            else if (options.IsTarGz)
                await _tarGzip.CreateFolderAsync(options.DestinationPath, path, options.Level, progress, token);
            else if (options.Password is null)
                await _zip.CreateFolderAsync(options.DestinationPath, path, options.Level, progress, token);
            else
                await _encryptedZip.CreateFolderAsync(options.DestinationPath, path, options.Password, options.Level, progress, token);
            if (options.VerifyAfterCreate) await _reader.TestAsync(options.DestinationPath, progress, token, options.Password);
            if (options.IsBilz && options.BilzSplitMiB > 0)
                await BilzVolumeService.SplitAsync(options.DestinationPath, options.BilzSplitMiB, token);
        }, () => LoadArchive(options.IsBilz && options.BilzSplitMiB > 0 ? options.DestinationPath + ".001" : options.DestinationPath, options.Password));
    }

    private async Task ExtractToPathAsync(string archive, string destination)
    {
        string? password;
        try { password = await EnsureOperationPasswordAsync(archive); }
        catch (OperationCanceledException) { return; }
        await RunProgressOperation("Extracting archive", (progress, token) => _reader.ExtractAsync(archive, destination, progress, token, password), () => {
            StatusText.Text = "Archive extracted successfully.";
            return Task.CompletedTask;
        });
    }

    private async void ExtractHere_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _currentArchive is null) return;
        string current = _currentArchive;
        await ExtractToPathAsync(current, Path.GetDirectoryName(current)!);
    }

    private async void ExtractSubfolder_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _currentArchive is null) return;
        string current = _currentArchive;
        string destination = Path.Combine(Path.GetDirectoryName(current)!, Path.GetFileNameWithoutExtension(current));
        await ExtractToPathAsync(current, destination);
    }

    private void CopyNames_Click(object sender, RoutedEventArgs e)
    {
        if (EntriesList.SelectedItems.Count == 0) return;
        Clipboard.SetText(string.Join(Environment.NewLine, EntriesList.SelectedItems.Cast<ArchiveEntryInfo>().Select(x => x.Name)));
        StatusText.Text = "Selected names copied to clipboard.";
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e) => EntriesList.SelectAll();

    private void OpenArchiveFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_currentArchive is null) return;
        NavigateTo(Path.GetDirectoryName(_currentArchive)!);
    }

    private void Integration_Click(object sender, RoutedEventArgs e)
        => new IntegrationWindow { Owner = this }.ShowDialog();

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _currentArchive is null) return;
        if (BiloraDialog.Show(this,
            "Use Microsoft Defender to scan the archive file? An archive-level scan may not inspect every internal file, especially encrypted contents.",
            "BiloraZIP virus scan", MessageBoxButton.YesNo, MessageBoxImage.Information) != MessageBoxResult.Yes) return;
        string path = _currentArchive;
        StatusText.Text = "Scanning archive with Microsoft Defender…";
        await RunSimpleOperation(() => DefenderScanService.ScanAsync(path));
        if (StatusText.Text.StartsWith("Scanning", StringComparison.Ordinal))
        {
            StatusText.Text = "Scan command finished — review Windows Security protection history for detections.";
            BiloraDialog.Show(this,
                "Defender scan command completed. This is not a guarantee the archive is safe. Check Windows Security > Protection history for detections.",
                "BiloraZIP", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void Repair_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _currentArchive is null) return;
        string path = _currentArchive;
        if (!path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            BiloraDialog.Show(this, "Best-effort recovery currently supports only ZIP. RAR and 7Z recovery are not implemented.", "BiloraZIP");
            return;
        }
        if (BiloraDialog.Show(this,
            "Try to salvage readable ZIP entries into a NEW .recovered.zip archive? This cannot rebuild a destroyed central directory or recover missing bytes. The original file will not be changed.",
            "BiloraZIP ZIP recovery", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        string? restored = null;
        await RunProgressOperation("Recovering ZIP", async (progress, token) => {
            restored = await _recovery.RecoverAsync(path, progress, token);
        }, async () => {
            if (restored is not null)
            {
                await LoadArchive(restored);
                StatusText.Text = "Recovery saved: " + restored;
                BiloraDialog.Show(this, "Recovered ZIP saved at:\n" + restored + "\nReview the .report.txt file for skipped entries.", "BiloraZIP", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        });
    }

    private void Diagnostics_Click(object sender, RoutedEventArgs e)
    {
        var items = _diagnosticsLog.Count == 0
            ? new List<DiagnosticItem> { new("Info", "No diagnostic messages in this session.") }
            : _diagnosticsLog.ToList();
        new DiagnosticsWindow(items, _activeOperationCts is not null) { Owner = this }.ShowDialog();
    }

    private void DefaultApps_Click(object sender, RoutedEventArgs e)
        => Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true });

    private void Search_Archive_Click(object sender, RoutedEventArgs e) => Find_Click(sender, e);

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool editingText = Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase or PasswordBox;
        if (editingText) return;
        ModifierKeys mods = Keyboard.Modifiers;
        if (mods == ModifierKeys.Control && e.Key == Key.O) Open_Click(sender, new RoutedEventArgs());
        else if (mods == ModifierKeys.Control && e.Key == Key.F) Find_Click(sender, new RoutedEventArgs());
        else if (mods == ModifierKeys.Control && e.Key == Key.A) EntriesList.SelectAll();
        else if (_currentArchive is null && mods == ModifierKeys.Control && e.Key == Key.C) CopySelectedFiles(false);
        else if (_currentArchive is null && _currentFolder is not null && mods == ModifierKeys.Control && e.Key == Key.X) CopySelectedFiles(true);
        else if (_currentArchive is null && mods == ModifierKeys.Control && e.Key == Key.V) _ = PasteFilesystemAsync();
        else if (_currentArchive is null && mods == ModifierKeys.None && e.Key == Key.F2) RenameSelectedFile();
        else if (_currentArchive is null && mods == ModifierKeys.Control && e.Key == Key.N) CreateNewFolder();
        else if (mods == ModifierKeys.Alt && e.Key == Key.Enter) ShowSelectedProperties();
        else if (mods == ModifierKeys.None && e.Key == Key.Delete) Delete_Click(sender, new RoutedEventArgs());
        else if (_currentArchive is null && mods == ModifierKeys.Shift && e.Key == Key.Delete)
            _ = DeleteFilesystemSelectionAsync(true);
        else if (mods == ModifierKeys.None && e.Key == Key.Enter) OpenSelected_Click(sender, new RoutedEventArgs());
        else if (mods == ModifierKeys.None && e.Key == Key.Back) Back_Click(sender, new RoutedEventArgs());
        else return;
        e.Handled = true;
    }
}
