# SPDX-License-Identifier: GPL-3.0-or-later
# verify.ps1 - the gate: the one definition of "green" for FlatCheck.
#
# WHAT IT DOES
#   Runs five checks in order and stops at the first one that fails:
#     1. test data manifest  every file listed in FlatCheck.Tests/testdata/manifest.json is
#                            there, with the SHA-256 written in the manifest; the large
#                            masters are only warned about when absent (check-testdata.ps1)
#     2. restore             downloads the NuGet packages; in CI (locked mode) it also fails
#                            when a packages.lock.json does not match
#     3. format              fails if dotnet format would change any file (it only checks)
#     4. build               builds the solution; every warning counts as an error
#     5. test                runs all the tests, on exactly what step 4 built, except the
#                            validation cases on the real masters (trait Category=Validation),
#                            which /validate and /release run (ADR 0004)
#   It does not touch the code. After a green run with tests it writes .verify-stamp (date
#   and time, git-ignored), which the Claude Code Stop hook reads.
#
# WHO CALLS IT
#   - you, from a terminal:
#       powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
#   - git, through <repo root>/.githooks/pre-commit, on every commit that touches FlatCheck
#     (the photo repository is a monorepo; the hook finds this script by itself)
#   - Claude Code, through scripts/hooks/claude-commit-gate.ps1, before Claude's git commit
#   - scripts/release.ps1, before it changes anything
#   - Claude, in the /wrap-up and /mutate skills, and with -SkipTests while writing code
#   The Stop hook (scripts/hooks/claude-stop-gate.ps1) never runs it; it only reads the stamp.
#
# OPTIONS
#   -SkipTests             steps 1-4 only, for a fast loop while editing; writes no stamp,
#                          so it does not count as "verify ran" for the Stop hook
#   -Configuration Debug   build and test in Debug instead of Release
#
# RESULT
#   exit 0  green: prints "== OK in <seconds>s"
#   exit 1  red: prints "FAILED: <step>" after that step's own error output
#
# Measured 2026-09-25: about 3 minutes (build 20 s, 881 core tests 46 s, 307 WPF tests
# 1 min 46 s). Kept compatible with Windows PowerShell 5.1 on purpose: pwsh is not on
# every machine.
[CmdletBinding()]
param(
    [switch]$SkipTests,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
# The project folder is the parent of scripts/, wherever the script is started from.
$root = Split-Path -Parent $PSScriptRoot
$sln = Join-Path $root 'FlatCheck.sln'
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# Runs one step: prints its name, runs it, and if the program it ran returned a non-zero
# exit code ($LASTEXITCODE), prints FAILED and ends the whole script with exit 1.
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
        Invoke-Step 'test'           { dotnet test $sln -c $Configuration --no-build --nologo -v q --filter 'Category!=Validation' }
        # Success stamp, read by the Claude Code Stop hook ("did verify run after the last edit?").
        # Only a run with tests writes it: -SkipTests is the edit loop, not the gate.
        [System.IO.File]::WriteAllText((Join-Path $root '.verify-stamp'), (Get-Date).ToString('o'))
    }
    Write-Host ("== OK in {0:n0}s" -f $stopwatch.Elapsed.TotalSeconds) -ForegroundColor Green
    exit 0
}
finally {
    Pop-Location
}
