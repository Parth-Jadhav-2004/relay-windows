$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "Building Relay.Core"
dotnet build "$root\src\Relay.Core\Relay.Core.csproj" -v q --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Running harness"
dotnet run --project "$root\tests\Relay.Harness\Relay.Harness.csproj" -v q --nologo --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet run --project "$root\tests\Relay.Platform.Harness\Relay.Platform.Harness.csproj" -v q --nologo
exit $LASTEXITCODE
