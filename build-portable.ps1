$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
$project = Join-Path $PSScriptRoot 'src\BiloraZIP\BiloraZIP.csproj'
$publish = Join-Path $PSScriptRoot 'portable-publish'
$release = Join-Path $PSScriptRoot 'release'
$archive = Join-Path $release 'BiloraZIP-1.0.0-rc1-portable-win-x64.zip'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install .NET 10 SDK first.' }
& dotnet publish $project -c Release -r win-x64 --self-contained true '-p:PublishSingleFile=true' '-p:PublishTrimmed=false' -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Portable publish failed.' }
if (-not (Test-Path (Join-Path $publish 'BiloraZIP.exe'))) { throw 'BiloraZIP.exe not found.' }
New-Item -ItemType Directory -Force -Path $release | Out-Null
foreach ($notice in @('LICENSE','NOTICE','THIRD-PARTY-NOTICES.md','PORTABLE-README-MN.md')) {
  Copy-Item -LiteralPath (Join-Path $PSScriptRoot $notice) -Destination $publish -Force
}
if (Test-Path $archive) { Remove-Item $archive -Force }
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $archive -CompressionLevel Optimal
if (-not (Test-Path $archive)) { throw 'Portable archive not created.' }
Get-FileHash $archive -Algorithm SHA256 | Format-List
Write-Host "Portable release created: $archive" -ForegroundColor Green
