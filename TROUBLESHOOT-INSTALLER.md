# BiloraZIP v0.9.0 — installer troubleshooting

This package contains **installer diagnostics**, not a new feature release.

1. Extract the folder, install .NET 10 SDK and Inno Setup 6 or 7.
2. Run PowerShell at the BiloraZIP project root:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build-installer.ps1
```

The script explicitly checks the project, the `.iss` file, the icon, and the published EXE. It uses absolute paths to invoke the Inno Setup compiler and writes its output to `installer-build.log`.

If you still get `The system cannot find the file specified`, the cause cannot be established from that message alone. Please send **the entire `installer-build.log`** and tell us the installed Inno Setup version. A damaged Inno Setup installation, a missing published file, or a compiler path/dependency issue are all possible, but should be distinguished from the compiler output rather than guessed.

This bundle also adds the missing `using System.IO;` to `ShellIconService.cs` that previously caused C# `CS0103`.

**Verification:** static file-path and archive checks only; cannot compile Windows .NET/WPF and Inno Setup on this host.
