# Run the solution tests, including Playwright end-to-end tests.
#
# Usage:
#   .\scripts\test.ps1 [--headed] [dotnet test arguments]
#   .\scripts\test.cmd [--headed] [dotnet test arguments]
#
#   --headed   Show Chromium windows (CARDGAMES_E2E_HEADED=1).
#   Any other arguments are forwarded to `dotnet test`.

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$dotnetArgs = New-Object System.Collections.Generic.List[string]
foreach ($arg in $args) {
    if ($arg -eq '--headed') {
        $env:CARDGAMES_E2E_HEADED = '1'
    }
    else {
        $dotnetArgs.Add($arg)
    }
}

Write-Host "Running dotnet tests."
if ($env:CARDGAMES_E2E_HEADED -eq '1') {
    Write-Host "Chromium will run headed."
}

& dotnet test (Join-Path $repoRoot 'src\CardGames.slnx') @dotnetArgs
exit $LASTEXITCODE
