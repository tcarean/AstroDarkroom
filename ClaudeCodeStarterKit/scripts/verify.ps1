# The one gate. Runs the same way from a terminal, from the git pre-commit hook, from the
# Claude Code commit hook and from CI. Windows PowerShell 5.1 compatible on purpose.
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
#   ... -SkipTests     build and format only (fast loop while editing)
#   ... -Configuration Debug
[CmdletBinding()]
param(
    [switch]$SkipTests,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sln = Join-Path $root 'Starter.sln'
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

function Invoke-Step {
    param([string]$Name, [scriptblock]$Action)
    Write-Host ("== {0}" -f $Name) -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        Write-Host ("FAILED: {0}" -f $Name) -ForegroundColor Red
        exit 1
    }
}

Push-Location $root
try {
    Invoke-Step 'test data manifest' { & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'check-testdata.ps1') }
    Invoke-Step 'restore'            { dotnet restore $sln --nologo -v q }
    Invoke-Step 'format'             { dotnet format $sln --verify-no-changes --no-restore -v q }
    Invoke-Step 'build'              { dotnet build $sln -c $Configuration --no-restore --nologo -v q -warnaserror }
    if (-not $SkipTests) {
        Invoke-Step 'test'           { dotnet test $sln -c $Configuration --no-build --nologo -v q }
    }
    Write-Host ("== OK in {0:n0}s" -f $stopwatch.Elapsed.TotalSeconds) -ForegroundColor Green
    exit 0
}
finally {
    Pop-Location
}
