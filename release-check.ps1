$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
Write-Host 'BiloraZIP v1.0 RC1 — Windows release verification' -ForegroundColor Cyan
& .\run-selftests.ps1
if ($LASTEXITCODE -ne 0) { throw 'Self-tests failed.' }
& .\build-portable.ps1
if ($LASTEXITCODE -ne 0) { throw 'Portable build failed.' }
& .\build-installer.ps1
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
$artifacts = @(
  '.\release\BiloraZIP-1.0.0-rc1-portable-win-x64.zip',
  '.\installer\output\BiloraZIP-Setup-1.0.0-rc1-win-x64.exe'
)
foreach ($path in $artifacts) {
  if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing artifact: $path" }
  Get-FileHash -LiteralPath $path -Algorithm SHA256 | Format-Table -AutoSize
}
Write-Host 'Automated build gates passed. Manual Windows UI/security and license checks are still required.' -ForegroundColor Yellow
