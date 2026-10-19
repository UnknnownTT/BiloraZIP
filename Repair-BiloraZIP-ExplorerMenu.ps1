# BiloraZIP Explorer context menu repair / repair tool.
# For the current Windows user only. No admin privileges required.
# Run: powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Repair-BiloraZIP-ExplorerMenu.ps1
# Optional: -ExePath 'C:\Program Files\BiloraZIP\BiloraZIP.exe'
# Revert these user-level entries: -Remove
[CmdletBinding()]
param(
    [string]$ExePath = '',
    [switch]$Remove
)
$ErrorActionPreference = 'Stop'

$verbs = @(
    'Software\Classes\*\shell\BiloraZIP.Compress',
    'Software\Classes\Directory\shell\BiloraZIP.Compress',
    'Software\Classes\AllFilesystemObjects\shell\BiloraZIP.Compress'
)
$sendToDir = Join-Path ([Environment]::GetFolderPath('ApplicationData')) 'Microsoft\Windows\SendTo'
$sendToLnk = Join-Path $sendToDir 'BiloraZIP - Create archive.lnk'

if ($Remove) {
    foreach ($relative in $verbs) {
        try { [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($relative, $false) }
        catch { Write-Warning "Cannot remove: $relative : $_" }
    }
    # Remove BiloraZIP-only per-user archive verbs created from Options > Integration.
    foreach ($ext in @('.zip','.rar','.7z','.tar','.zipx','.xapk','.bilz')) {
        $menu = "Software\Classes\SystemFileAssociations\$ext\shell\BiloraZIP"
        try { [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($menu, $false) }
        catch { Write-Warning "Cannot remove: $menu : $_" }
        try {
            $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey("Software\Classes\$ext\OpenWithProgids", $true)
            if ($null -ne $key) { $key.DeleteValue('BiloraZIP.Archive', $false); $key.Close() }
        } catch { Write-Warning "Cannot remove OpenWith for $ext : $_" }
    }
    foreach ($path in @('Software\Classes\BiloraZIP.Archive','Software\BiloraZIP\Capabilities')) {
        try { [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($path, $false) }
        catch { Write-Warning "Cannot remove: $path : $_" }
    }
    try {
        $apps = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey('Software\RegisteredApplications', $true)
        if ($null -ne $apps) { $apps.DeleteValue('BiloraZIP', $false); $apps.Close() }
    } catch { Write-Warning "Cannot remove RegisteredApplications entry: $_" }
    if (Test-Path -LiteralPath $sendToLnk) { Remove-Item -LiteralPath $sendToLnk -Force }
    Write-Host 'BiloraZIP per-user compression context-menu entries removed.' -ForegroundColor Yellow
    exit 0
}

if ([string]::IsNullOrWhiteSpace($ExePath)) {
    $candidates = @(
        (Join-Path $env:ProgramFiles 'BiloraZIP\BiloraZIP.exe'),
        (Join-Path ${env:ProgramFiles(x86)} 'BiloraZIP\BiloraZIP.exe'),
        (Join-Path $env:LOCALAPPDATA 'Programs\BiloraZIP\BiloraZIP.exe')
    )
    $ExePath = $candidates | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } | Select-Object -First 1
}
if ([string]::IsNullOrWhiteSpace($ExePath) -or !(Test-Path -LiteralPath $ExePath -PathType Leaf)) {
    throw 'Installed BiloraZIP.exe was not found. Run with -ExePath "C:\Path\To\BiloraZIP.exe".'
}
$ExePath = (Resolve-Path -LiteralPath $ExePath).ProviderPath
if ([IO.Path]::GetFileName($ExePath) -ine 'BiloraZIP.exe') {
    throw 'The selected file must be BiloraZIP.exe.'
}
Write-Host "Executable: $ExePath" -ForegroundColor Cyan

# Keep this as Document: Explorer may invoke once per item and the current
# CompressionInvocationHub merges those launches into one options dialog.
# The Document legacy verb may not display for >15 items; use Send to instead.
$command = '"' + $ExePath + '" --compress "%1"'
foreach ($relative in $verbs) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($relative)
    if ($null -eq $key) { throw "Could not create $relative" }
    try {
        $key.SetValue('', 'Compress with BiloraZIP (ZIP / BILZ)...', [Microsoft.Win32.RegistryValueKind]::String)
        $key.SetValue('Icon', '"' + $ExePath + '",0', [Microsoft.Win32.RegistryValueKind]::String)
        $key.SetValue('MultiSelectModel', 'Document', [Microsoft.Win32.RegistryValueKind]::String)
    }
    finally { $key.Dispose() }
    $sub = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($relative + '\command')
    try { $sub.SetValue('', $command, [Microsoft.Win32.RegistryValueKind]::String) }
    finally { $sub.Dispose() }
    Write-Host "Registered: HKCU\$relative"
}

# The Send To shortcut is a fallback if the shell filters a multi-selection.
if (-not (Test-Path -LiteralPath $sendToDir -PathType Container)) {
    New-Item -ItemType Directory -Path $sendToDir -Force | Out-Null
}
$wsh = New-Object -ComObject WScript.Shell
$shortcut = $wsh.CreateShortcut($sendToLnk)
$shortcut.TargetPath = $ExePath
$shortcut.Arguments = '--compress'
$shortcut.WorkingDirectory = [IO.Path]::GetDirectoryName($ExePath)
$shortcut.IconLocation = $ExePath + ',0'
$shortcut.Description = 'Compress selected files and folders with BiloraZIP'
$shortcut.Save()

# Notify Explorer of association updates without killing Explorer processes.
try {
    if (-not ('BiloraZipShellNotify' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class BiloraZipShellNotify {
    [DllImport("shell32.dll")]
    public static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}
'@
    }
    [BiloraZipShellNotify]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)
}
catch { Write-Warning 'Explorer might need to be restarted to refresh menus.' }

Write-Host ''
Write-Host 'BiloraZIP legacy right-click menu repaired for the current user.' -ForegroundColor Green
Write-Host 'Test in File Explorer: select ONE file, right-click -> Show more options.'
Write-Host 'Then test one folder and five selected files/folders.'
Write-Host 'Choose Compress with BiloraZIP (ZIP / BILZ)... and select format in the dialog.'
Write-Host 'For a large selection use Send to -> BiloraZIP - Create archive.'
Write-Host 'RAR compression is not supported by the current BiloraZIP engine.'
