# BiloraZIP v0.9.0 - Build Fix 1

## Cause

`CS0104: 'Button' is an ambiguous reference between System.Windows.Controls.Button and System.Windows.Forms.Button` occurs because the WPF application also references Windows Forms for its System Tray NotifyIcon.

## Fix

New file `src/BiloraZIP/WpfTypeAliases.cs` explicitly selects WPF types for the main user interface while keeping `System.Windows.Forms.NotifyIcon` and `ContextMenuStrip` available by fully-qualified name.

## Windows build / installer

From the root `BiloraZIP` directory in PowerShell:

```powershell
dotnet clean .\src\BiloraZIP\BiloraZIP.csproj -c Release
dotnet build .\src\BiloraZIP\BiloraZIP.csproj -c Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1
```

The Inno Setup installer is emitted to `installer/output/` after publishing/compilation succeeds.

**Verification limitation:** This patch was checked statically, not compiled on Windows in the authoring environment. If there are further compiler errors, copy the complete output from `dotnet build` rather than the final `Publish failed` line.
