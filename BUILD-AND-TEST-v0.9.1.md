# BiloraZIP v0.9.1 — Bugfix source + installer

This package is based on the last reported working v0.9.0 Build Fix 3 source.

## Compile and install

1. Extract the entire ZIP to a **new folder**; do not mix v0.9.0 files into it.
2. Open PowerShell in the extracted `BiloraZIP` folder.
3. `dotnet clean .\src\BiloraZIP\BiloraZIP.csproj -c Release`
4. `dotnet build .\src\BiloraZIP\BiloraZIP.csproj -c Release`
5. `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1`
6. Install `installer\output\BiloraZIP-Setup-0.9.1-win-x64.exe` and **tick the classic Explorer right-click commands** in the installer task list. If upgraded Windows Explorer does not display BiloraZIP, check `Show more options` and run `Options -> Windows integration & associations -> Apply for this user`. Windows 11's primary context menu is not implemented by this legacy registry integration.

## What changed

- WPF ContextMenu's reserved light icon gutter removed via a complete themed ControlTemplate.
- Separator template overridden for generated MenuItem separators as well as explicitly defined separators, with brushes that track theme changes.
- Drag a selection from an archive toward Explorer: files are extracted safely to `%TEMP%\BiloraZIP-DragDrop\<session>`. The same selection's prepared files are cached; if the user releases the mouse while extraction runs, **drag again** and it copies from the prepared cache. Cache folders older than three days are cleaned at next app launch. This is a real-file drag (not shell virtual files), so preparing large selections may take time and disk space.
- Drag/drop from Explorer to a plain ZIP appends within the currently displayed archive folder, or into a specific folder if dropped onto that folder's row. RAR/7Z are read-only. Existing names are rejected rather than overwritten.
- Drop into a local folder shown in BiloraZIP Explorer copies the incoming files/folders there. Existing names are rejected rather than overwritten. Reparse points are not copied.
- Marquee/rubber-band selection from blank list space, with Ctrl modifier and auto-scroll while dragging at the edges.
- Compress several selected files/folders in a single ZIP or TAR (including AES password ZIP). No RAR writer: RAR creation remains unavailable.
- Added a per-user classic context-menu registration for compression, machine-wide installer verbs, shell-change notification when selecting integrations, and short-lived per-user IPC grouping of Explorer's multiple `--compress "%1"` invocations. Windows may launch widely separated invocations outside the grouping window; **Send to -> BiloraZIP - Create archive** is a fallback that sends the selection as one batch.

## Windows manual regression tests

- Open File, Commands, Tools, Options, Help in both themes: no white horizontal separator or icon gutter; verify right-click inside the file list.
- Select multiple file rows using blank-area drag and Ctrl+drag; click a file and drag it to Explorer. For archive contents, allow prep to finish and drag a second time if necessary.
- Open an ordinary unencrypted ZIP, navigate into `Documents/2026`, and drop a new `report.pdf` into the list: it should appear under `Documents/2026/report.pdf`, not the ZIP root.
- Drop a file onto a visible folder row to target that folder. Repeat with a name that exists to see a safe duplicate warning (no overwrite).
- In local folder mode, drop a small file from Windows Explorer; it should be copied into the viewed folder.
- In Explorer, select multiple files/folders and choose BiloraZIP compression in the *classic* context menu. Expect a single archive-options dialog; if Explorer invokes items too slowly, use Send To fallback.
- Test with a password protected ZIP, RAR, and 7Z. Drag-out requires a successful password when needed. Confirm source archives are not modified by drag-out.

## Test status

Static checks: XML parsing of 11 XAML files, event-handler references, brace balance, version/installer paths and ZIP integrity. **Windows SDK build, Inno Setup compilation and actual UI runtime were not available in the packaging environment.** Please share complete compiler errors if any appear.
