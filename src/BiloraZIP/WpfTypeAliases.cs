// BiloraZIP mixes WPF for its UI and Windows Forms ONLY for the notification
// area icon. Enabling both desktop SDKs can bring identically named types
// into scope (Button, MessageBox, OpenFileDialog, Clipboard, etc.).
// Explicit WPF aliases ensure application UI code and generated partial
// classes use WPF types without affecting System.Windows.Forms.NotifyIcon.

global using Application = System.Windows.Application;
global using Button = System.Windows.Controls.Button;
global using CheckBox = System.Windows.Controls.CheckBox;
global using Clipboard = System.Windows.Clipboard;
global using Color = System.Windows.Media.Color;
global using ColorConverter = System.Windows.Media.ColorConverter;
global using DataFormats = System.Windows.DataFormats;
global using DataGrid = System.Windows.Controls.DataGrid;
global using ListViewItem = System.Windows.Controls.ListViewItem;
global using ListView = System.Windows.Controls.ListView;
global using DragDropEffects = System.Windows.DragDropEffects;
global using DragEventArgs = System.Windows.DragEventArgs;
global using Image = System.Windows.Controls.Image;
global using KeyEventArgs = System.Windows.Input.KeyEventArgs;
global using MenuItem = System.Windows.Controls.MenuItem;
global using MessageBox = System.Windows.MessageBox;
global using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
global using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
global using Separator = System.Windows.Controls.Separator;
global using Size = System.Windows.Size;
global using FontFamily = System.Windows.Media.FontFamily;
global using HorizontalAlignment = System.Windows.HorizontalAlignment;
global using TextBox = System.Windows.Controls.TextBox;
