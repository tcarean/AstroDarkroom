# SPDX-License-Identifier: GPL-3.0-or-later
# mutate.ps1 - measures how well the tests guard FlatCheck.Core (mutation testing).
#
# WHAT IT DOES
#   Stryker.NET makes many small deliberate bugs ("mutants") in FlatCheck.Core, for example
#   turning < into <= or + into -, and runs the tests against each one. A mutant the tests
#   catch is "killed"; one they miss "survives" and points at a missing or weak test. This
#   script fetches Stryker if it is not there yet (a local tool, pinned in
#   .config/dotnet-tools.json), runs it with FlatCheck.Tests/stryker-config.json, then prints
#   the score and every survivor as file:line with the change that was made. The full report
#   goes to FlatCheck.Tests/StrykerOutput/<timestamp>/reports/ (git-ignored).
#
# WHO CALLS IT
#   - you, by hand, after adding or changing numeric code, or before a release:
#       powershell -NoProfile -ExecutionPolicy Bypass -File scripts/mutate.ps1 -Mutate "**/Metrics.cs"
#   - Claude, in the /mutate skill, which turns the survivors into proposed tests
#   Not part of scripts/verify.ps1: with 881 tests it runs the suite once per mutant, so a
#   whole-Core run is hours; always pass -Mutate on this project.
#
# OPTIONS
#   -Mutate "**/Metrics.cs"   only mutate matching files
#   -BreakAt 80               exit 1 when the mutation score is below 80 %
#
# RESULT
#   Stryker's own exit code.
[CmdletBinding()]
param(
    [string]$Mutate,
    [int]$BreakAt = -1,
    [string]$TestProject = 'FlatCheck.Tests'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

Push-Location $root
try {
    dotnet tool restore --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed' }
}
finally {
    Pop-Location
}

$args = @()
if ($Mutate) { $args += @('--mutate', $Mutate) }
if ($BreakAt -ge 0) { $args += @('--break-at', $BreakAt) }

Push-Location (Join-Path $root $TestProject)
try {
    Write-Host ("== stryker {0}" -f ($args -join ' ')) -ForegroundColor Cyan
    dotnet stryker @args
    $code = $LASTEXITCODE
    $latest = Get-ChildItem 'StrykerOutput' -Directory -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
    if ($latest) {
        $json = Join-Path $latest.FullName 'reports\mutation-report.json'
        if (Test-Path $json) {
            $report = Get-Content $json -Raw | ConvertFrom-Json
            $killed = 0; $survived = 0; $nocov = 0; $timeout = 0; $other = 0
            foreach ($file in $report.files.PSObject.Properties) {
                foreach ($m in $file.Value.mutants) {
                    switch ($m.status) {
                        'Killed' { $killed++ }
                        'Survived' { $survived++ }
                        'NoCoverage' { $nocov++ }
                        'Timeout' { $timeout++ }
                        default { $other++ }
                    }
                }
            }
            $detected = $killed + $timeout
            $undetected = $survived + $nocov
            $total = $detected + $undetected
            $score = if ($total -gt 0) { [math]::Round(100.0 * $detected / $total, 1) } else { 0 }
            Write-Host ("== mutants: {0} total, {1} killed, {2} survived, {3} no coverage, {4} timeout; score {5} %" -f $total, $killed, $survived, $nocov, $timeout, $score) -ForegroundColor Cyan
            if ($undetected -gt 0) {
                Write-Host '== survivors (file:line mutation):'
                foreach ($file in $report.files.PSObject.Properties) {
                    foreach ($m in $file.Value.mutants) {
                        if ($m.status -eq 'Survived' -or $m.status -eq 'NoCoverage') {
                            Write-Host ("   {0}:{1} [{2}] {3} -> {4}" -f (Split-Path $file.Name -Leaf), $m.location.start.line, $m.status, $m.mutatorName, $m.replacement)
                        }
                    }
                }
            }
            Write-Host ("== report: {0}" -f (Join-Path $latest.FullName 'reports\mutation-report.html'))
        }
    }
    Write-Host ("== done in {0:n0}s" -f $stopwatch.Elapsed.TotalSeconds)
    exit $code
}
finally {
    Pop-Location
}
