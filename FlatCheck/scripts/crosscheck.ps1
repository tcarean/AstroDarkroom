# SPDX-License-Identifier: GPL-3.0-or-later
# crosscheck.ps1 - the parity check between the C# tool and flatcheck.py, the numeric oracle.
#
# WHAT IT DOES
#   1. Runs the DumpsFixtures test with FLATCHECK_DUMP_FIXTURES set, which writes the 43
#      scenario FITS files and the C# output for each into a temporary folder.
#   2. Runs crosscheck.py over that folder: it runs flatcheck.py on the same files and
#      compares the full text and every numeric token, after checking that both report the
#      same version.
#   Nothing is written into the repository.
#
# WHO CALLS IT
#   - you, or Claude in /new-feature and /wrap-up, after any change to a formula: anything
#     in FlatCheck.Core/Metrics.cs, FlatCheck.Core/Stats.cs, FlatCheck.Core/Analysis/,
#     FlatCheck.Core/Report/ or flatcheck.py (CLAUDE.md, "Parity with Python").
#       powershell -NoProfile -ExecutionPolicy Bypass -File scripts/crosscheck.ps1
#   Not part of scripts/verify.ps1: it needs Python with numpy, astropy and rawpy, which
#   the gate must not depend on (ADR 0005).
#
# OPTIONS
#   -Dir <folder>   where to dump the fixtures (default: a fresh folder under %TEMP%)
#
# RESULT
#   crosscheck.py's exit code: 0 when every scenario agrees numerically, non-zero otherwise
#   or on a version desync; 1 when the dump test itself failed.
[CmdletBinding()]
param([string]$Dir)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Dir) { $Dir = Join-Path $env:TEMP ('flatcheck-crosscheck-' + (Get-Date).ToString('yyyyMMdd-HHmmss')) }
New-Item -ItemType Directory -Force -Path $Dir | Out-Null

Push-Location $root
try {
    $env:FLATCHECK_DUMP_FIXTURES = $Dir
    Write-Host ("== dumping fixtures to {0}" -f $Dir) -ForegroundColor Cyan
    dotnet test (Join-Path $root 'FlatCheck.Tests\FlatCheck.Tests.csproj') -c Release --nologo -v q --filter DumpsFixtures
    if ($LASTEXITCODE -ne 0) { Write-Host 'FAILED: fixture dump' -ForegroundColor Red; exit 1 }
    Write-Host '== crosscheck.py' -ForegroundColor Cyan
    python (Join-Path $root 'crosscheck.py') $Dir
    exit $LASTEXITCODE
}
finally {
    Remove-Item Env:\FLATCHECK_DUMP_FIXTURES -ErrorAction SilentlyContinue
    Pop-Location
}
