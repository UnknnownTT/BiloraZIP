$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

$project = Join-Path $PSScriptRoot 'src\BiloraZIP\BiloraZIP.csproj'
$iss = Join-Path $PSScriptRoot 'installer\BiloraZIP.iss'
$icon = Join-Path $PSScriptRoot 'src\BiloraZIP\BiloraZIP.ico'
$publish = Join-Path $PSScriptRoot 'publish'
$publishedExe = Join-Path $publish 'BiloraZIP.exe'

# Inno Setup is the source of truth for installer version and filename.
# Avoid hardcoded installer names that break on the next upgrade.
if (-not (Test-Path -LiteralPath $iss -PathType Leaf)) { throw "Missing Inno Setup script: $iss" }
$issText = Get-Content -LiteralPath $iss -Raw
$versionMatch = [regex]::Match($issText, '(?m)^\s*#define\s+MyAppVersion\s+"([^"]+)"')
$baseMatch = [regex]::Match($issText, '(?m)^\s*OutputBaseFilename\s*=\s*([^\r\n]+)')
$dirMatch = [regex]::Match($issText, '(?m)^\s*OutputDir\s*=\s*([^\r\n]+)')
if (-not $versionMatch.Success) { throw 'Missing MyAppVersion in installer/BiloraZIP.iss' }
if (-not $baseMatch.Success) { throw 'Missing OutputBaseFilename in installer/BiloraZIP.iss' }
$appVersion = $versionMatch.Groups[1].Value
$outputBase = $baseMatch.Groups[1].Value.Trim().Replace('{#MyAppVersion}', $appVersion)
if ($outputBase.Contains('{#')) { throw "Unresolved Inno Setup constant in output name: $outputBase" }
$outputDir = if ($dirMatch.Success) { $dirMatch.Groups[1].Value.Trim() } else { 'Output' }
$installerOutput = Join-Path (Join-Path (Split-Path -Parent $iss) $outputDir) ($outputBase + '.exe')
Write-Host "BiloraZIP v$appVersion - build and installer diagnostics" -ForegroundColor Cyan
Write-Host "Expected installer: $installerOutput"


foreach ($file in @($project, $iss, $icon)) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Missing required project file: $file"
    }
    Write-Host "Found: $file"
}

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) { throw 'dotnet SDK was not found in PATH. Install the .NET 10 SDK.' }

Write-Host 'Publishing application...' -ForegroundColor Cyan
& dotnet publish $project -c Release -r win-x64 --self-contained true '-p:PublishSingleFile=true' '-p:PublishTrimmed=false' -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }
if (-not (Test-Path -LiteralPath $publishedExe -PathType Leaf)) {
    throw "The published exe is missing: $publishedExe"
}
Write-Host "Published EXE: $publishedExe" -ForegroundColor Green

$paths = @(
    "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 7\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
)
$iscc = $paths | Where-Object { $_ -and (Test-Path -LiteralPath $_ -PathType Leaf) } | Select-Object -First 1
if (-not $iscc) {
    Write-Host 'Checked these compiler locations:'
    $paths | ForEach-Object { Write-Host "  $_" }
    throw 'Inno Setup Compiler (ISCC.exe) not found. Install/repair Inno Setup from https://jrsoftware.org/isdl.php'
}

Write-Host "Inno Setup compiler: $iscc" -ForegroundColor Cyan
Write-Host "ISS script: $iss"
Write-Host "Icon file: $icon"
Write-Host "EXE exists: $(Test-Path -LiteralPath $publishedExe)"

$log = Join-Path $PSScriptRoot 'installer-build.log'
Write-Host "Compiling installer; output is also saved to: $log" -ForegroundColor Cyan
# Remove only the expected installer so an old file cannot mask a failed compiler run.
if (Test-Path -LiteralPath $installerOutput -PathType Leaf) { Remove-Item -LiteralPath $installerOutput -Force }
# Absolute paths avoid dependence on the current working directory.
& $iscc $iss 2>&1 | Tee-Object -FilePath $log
$exitCode = $LASTEXITCODE
if ($exitCode -ne 0) {
    throw "Inno Setup compilation failed (exit code $exitCode). Read the preceding compiler messages or send installer-build.log."
}
if (-not (Test-Path -LiteralPath $installerOutput -PathType Leaf)) {
    throw "Compiler returned success but the installer file was not found: $installerOutput"
}
Write-Host "SUCCESS: $installerOutput" -ForegroundColor Green
