# BiloraZIP v0.9.0 — Build Fix 3

Fix for compiler diagnostic:

````text
Services/ThemeManager.cs(66,34): error CS0104: 'ColorConverter' is an ambiguous reference between 'System.Drawing.ColorConverter' and 'System.Windows.Media.ColorConverter'
````

Changes:
- ThemeManager now explicitly calls `System.Windows.Media.ColorConverter.ConvertFromString`.
- `WpfTypeAliases.cs` adds an explicit alias for `ColorConverter`.
- Preserves Build Fix 1 and 2 (WPF `Button`, `DataGrid`, etc).

After extracting the archive, open PowerShell in the nested `BiloraZIP` folder and run:

```powershell
dotnet clean .\src\BiloraZIP\BiloraZIP.csproj -c Release
dotnet build .\src\BiloraZIP\BiloraZIP.csproj -c Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1
```

The authoring environment does not have Windows/.NET SDK; this fix has not been tested by compiling on Windows. If compiler errors remain, provide the entire `error CS...` output.
