# BiloraZIP v0.9.2 — Windows build and regression checklist

Development source package; Windows 10/11 x64, .NET 10 SDK, Inno Setup 6 or 7.

## Build

Run PowerShell **from the `BiloraZIP` root folder**:

```powershell
dotnet clean .\src\BiloraZIP\BiloraZIP.csproj -c Release
dotnet build .\src\BiloraZIP\BiloraZIP.csproj -c Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1
```

Expected installer output: `installer\output\BiloraZIP-Setup-0.9.2-win-x64.exe`.
Close the running BiloraZIP before upgrading. Keep a backup of important archives, especially before testing Delete.

## v0.9.2 changes

- Right-click inside BiloraZIP now builds a context menu appropriate to the current view: filesystem or archive. Archive extraction and ZIP creation commands are no longer replaced by a three-command popup.
- Local folder commands: Open, Cut, Copy, Paste, Rename, Delete, Properties, Create new folder, Add to ZIP (one archive for all selection). Keyboard: Ctrl+C/X/V/A, F2, Delete, Shift+Delete, Alt+Enter, Enter, Backspace.
- Filesystem Delete uses Recycle Bin by default; Shift+Delete is permanent, always confirmed. ZIP delete rebuilds archive (irreversible); read-only archive types cannot be edited.
- Built-in themed dialogs replace 25 stock MessageBox.Show call sites. New Rename and Property dialogs follow the selected language and theme.
- Archive Info TabControl and DataGrid column headers no longer use default white theme chrome. Archive metadata field names localized.
- Exit Options window uses flexible translated text and a wider action row. Archive creation options window widened.
- Localization dictionary duplicate keys removed; 50+ additional text keys added.
- Prior v0.9.1 fixes retained: marquee selection, ZIP folder drop, outbound drag via temp files, selected extraction, classic context-menu registration and multi-file compression collector.

## Windows functional tests (must be run on a real Windows host)

1. Start app in `Dark`/`English`; right-click a file in an ordinary folder. Verify Copy, Cut, Paste, Rename, Delete, Properties, Add to archive, New folder. Repeat in `Light`/`Монгол`.
2. Ctrl+A select 3 files; right-click `Add to archive`. Only one options dialog should open; create ZIP and inspect all 3 entries.
3. Ctrl+C / Ctrl+V between different writable directories. Ctrl+X / Ctrl+V moves (same-volume move); ensure no file of same name is overwritten.
4. Press F2 to rename. Delete (confirm) sends to Recycle Bin; Shift+Delete confirms permanent deletion. Do NOT use important files to test.
5. Open ZIP; right-click entry and inspect Extract selected, Extract Here, Open, Properties, Delete if writable. Open RAR/7Z; Delete must be unavailable.
6. Confirm all menu separators and icon gutters in File / Commands / Right-click are theme-consistent.
7. Open Archive information (Info, Options, Comment, SFX), verify tab background and header of property table. Switch language and reopen.
8. Close program, check Exit Choices window in both languages: no clipped descriptions/buttons, minimize to tray and restore.
9. Drag Explorer files into the current ZIP directory; drag selected archive items out into Explorer (first drag may prepare temp files; re-drag if released before ready). Test Marquee Selection.
10. In setup, check **Explorer right-click commands**; on Windows 11 examine **Show more options**. If absent run Options → Windows integration and check classic commands. A native top-level Windows 11 shell extension is not included.

## Limitations and safety

- **RAR/7Z creation is not implemented**. Their contents can be read/extracted when supported; only ZIP/TAR creation is offered.
- ZIP archive entries cannot be renamed in-place. Filesystem Rename and Delete are supported; other archive formats are read-only.
- Cut/Copy/Paste uses the Windows FileDrop clipboard; BiloraZIP remembers its own Cut operations. Paste refuses to overwrite existing destination names (no silent overwrite). Cross-volume moves and external cut semantics need further testing.
- Outbound drag uses temporary extraction; operation must finish preparing files before Explorer will receive a drop.
- Some lower-level library or Windows error messages may remain in English. Program-generated core dialogs and Archive Info field names have extra localization in this version.
- Windows 11 **native primary context menu** integration needs a separate COM/IExplorerCommand packaged shell extension and is not part of this version.
- This source package was statically checked for XML, event handlers, duplicate resource keys, and bracket structure. **Windows compile/runtime and installer tests were not run** in the Linux build environment. Send the full compiler output if build fails.
