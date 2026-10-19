$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
Write-Host 'Running BiloraZIP Windows security self-tests...' -ForegroundColor Cyan
& dotnet run --project '.\tests\BiloraZIP.SelfTests\BiloraZIP.SelfTests.csproj' --configuration Release
if ($LASTEXITCODE -ne 0) { throw "Self-tests failed with exit code $LASTEXITCODE" }
Write-Host 'All self-tests passed.' -ForegroundColor Green
