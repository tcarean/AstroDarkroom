# SPDX-License-Identifier: GPL-3.0-or-later
# mutate.ps1 - measures how well the tests guard the Core code (mutation testing).
#
# WHAT IT DOES
#   Stryker.NET makes many small deliberate bugs ("mutants") in the Core project, for
#   example turning < into <= or + into -, and runs the tests against each one. A mutant the
#   tests catch is "killed"; one they miss "survives" and points at a missing or weak test.
#   This script fetches Stryker if it is not there yet (a local tool, pinned in
#   .config/dotnet-tools.json), runs it from the test project kit.json names
#   (mutate.testProject, in the kit tests/Starter.Tests/) with the stryker-config.json there,
#   then prints the score and every survivor as file:line with the change that was made.
#   It changes no source file. The full report goes to
#   <test project>/StrykerOutput/<timestamp>/reports/mutation-report.html (and .json).
#
# WHO CALLS IT
#   - you, by hand, after adding or changing numeric code, or before a release:
#       powershell -NoProfile -ExecutionPolicy Bypass -File scripts/mutate.ps1
#   - Claude, in the /mutate skill, which turns the survivors into proposed tests
#   Not part of scripts/verify.ps1: it runs the whole test suite once per mutant, so it takes
#   minutes, not seconds (ADR 0008). On a large suite always pass -Mutate.
#
# OPTIONS
#   -Mutate "**/Stats.cs"   only mutate matching files (faster, focused)
#   -BreakAt 80             exit 1 when the mutation score is below 80 %
#   -TestProject <folder>   the test project Stryker runs from (default: kit.json's)
#
# RESULT
#   Stryker's own exit code: 0 when it finished (and the score reached -BreakAt, if given),
#   non-zero when the score is below -BreakAt or Stryker itself failed.
[CmdletBinding()]
param(
    [string]$Mutate,
    [int]$BreakAt = -1,
    [string]$TestProject
)

$ErrorActionPreference = 'Stop'
# The project folder is the parent of scripts/.
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'kit-config.ps1')
$kit = Read-KitConfig $root
if (-not $TestProject) { $TestProject = $kit.mutate.testProject }
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# Fetch the tools pinned in .config/dotnet-tools.json (Stryker.NET), if not there yet.
Push-Location $root
try {
    dotnet tool restore --verbosity quiet
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed' }
}
finally {
    Pop-Location
}

# Stryker's command-line arguments, built from the options. ($args is also the name
# PowerShell gives to unnamed script arguments; this script does not use those, and simply
# reuses the name for its own list.)
$args = @()
if ($Mutate) { $args += @('--mutate', $Mutate) }
if ($BreakAt -ge 0) { $args += @('--break-at', $BreakAt) }

# Stryker runs from the test project folder and reads stryker-config.json there.
Push-Location (Join-Path $root ($TestProject -replace '/', '\'))
try {
    Write-Host ("== stryker {0}" -f ($args -join ' ')) -ForegroundColor Cyan
    dotnet stryker @args
    $code = $LASTEXITCODE
    # Summary from the newest report: count the mutants by status and list the ones no test
    # caught. Folder names are timestamps, so sorting them by name finds the newest.
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
            # The score as Stryker counts it: killed and timed-out mutants were detected;
            # survivors and mutants that no test even runs (NoCoverage) were not.
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
