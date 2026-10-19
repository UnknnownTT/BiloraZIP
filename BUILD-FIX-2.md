# BiloraZIP 0.9.0 — Build Fix 2

## Changes
- `LocalizationService.cs` now explicitly references `System.Windows.Controls.DataGrid` instead of the ambiguous `DataGrid`.
- `WpfTypeAliases.cs` also includes a WPF `DataGrid` alias, retaining the existing WPF/Windows Forms disambiguation rules.
- No user-facing functionality or installer settings were deliberately changed.

## Build on Windows

Open PowerShell in the extracted `BiloraZIP` folder and run:

```powershell
dotnet clean .\src\BiloraZIP\BiloraZIP.csproj -c Release
dotnet build .\src\BiloraZIP\BiloraZIP.csproj -c Release -v:minimal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1
```

If it still fails, send the **entire compiler error output**, not just the last line of the script.

**Verification:** XML syntax, XAML-to-code event names, package paths and fix text can be checked here. The Windows WPF compiler and Inno Setup are not installed in this environment; this patch is not a Windows runtime/build certification.
