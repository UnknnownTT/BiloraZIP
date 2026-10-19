# BiloraZIP v0.9.2 — Build Fix 1

Resolves the four **CS0104** ambiguous WPF/Windows Forms type names in `Views/BiloraDialog.cs` reported by the Windows compiler:

- `HorizontalAlignment` (two occurrences) → `System.Windows.HorizontalAlignment`
- `TextBox` → `System.Windows.Controls.TextBox`
- `FontFamily` → `System.Windows.Media.FontFamily`

The UI file uses fully qualified WPF types, and `WpfTypeAliases.cs` also declares explicit global WPF aliases for these names. All v0.9.2 application features remain unchanged.

## Build (PowerShell, project root)

```powershell
dotnet clean .\src\BiloraZIP\BiloraZIP.csproj -c Release
dotnet build .\src\BiloraZIP\BiloraZIP.csproj -c Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1
```

**Note:** the actual Windows build/runtime is not available in the packaging environment. Static checks are not a substitute for successful `dotnet build` on Windows.
