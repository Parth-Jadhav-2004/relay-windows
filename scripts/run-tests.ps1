$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "Building Tinycast.Core"
dotnet build "$root\src\Tinycast.Core\Tinycast.Core.csproj" -v q --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Running harness"
dotnet run --project "$root\tests\Tinycast.Harness\Tinycast.Harness.csproj" -v q --nologo --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet run --project "$root\tests\Tinycast.Platform.Harness\Tinycast.Platform.Harness.csproj" -v q --nologo
exit $LASTEXITCODE
