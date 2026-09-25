# SPDX-License-Identifier: GPL-3.0-or-later
# check-testdata.ps1 - checks that the test data files are present and unchanged.
#
# WHAT IT DOES
#   Reads FlatCheck.Tests/testdata/manifest.json, the list of every test data file with its
#   SHA-256 hash, and checks each file:
#     - "repo" files live in FlatCheck.Tests/testdata/fixtures/ and must be there (FlatCheck
#       has none today: every fixture is generated in the test by FitsTestData, and the
#       golden texts under FlatCheck.Tests/Golden/ are compared, not hashed);
#     - "large" files are the real masters and raws under E:\Photografie\Astro, never in
#       git. They are looked for under the folder named by FLATCHECK_TESTDATA_LARGE, or
#       under FlatCheck.Tests/testdata/large/ when that variable is unset. A missing one is
#       a yellow warning on your machine and an error in CI only with "requiredInCi": true;
#     - a file that is there must have exactly the hash in the manifest. Another hash means
#       the master was re-stacked, so the validation figures no longer describe it.
#   With -Update it writes the current hashes into the manifest instead of comparing them.
#
# WHO CALLS IT
#   - scripts/verify.ps1, as its first step
#   - you, with -Update, after a fixture or a master changed on purpose:
#       powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-testdata.ps1 -Update
#   - Claude, in the /validate skill, to list which large files are available
#   Never use -Update to make a failing check pass (CLAUDE.md rule 3).
#
# RESULT
#   exit 0  every required file is there with the right hash (yellow warnings allowed)
#   exit 1  a required file is missing (red MISSING) or has another hash (red HASH)
[CmdletBinding()]
param([switch]$Update)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$testdata = Join-Path $root 'FlatCheck.Tests\testdata'
$manifestPath = Join-Path $testdata 'manifest.json'
$largeDir = if ($env:FLATCHECK_TESTDATA_LARGE) { $env:FLATCHECK_TESTDATA_LARGE } else { Join-Path $testdata 'large' }
$inCi = $env:CI -eq 'true'

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$failed = $false

foreach ($entry in $manifest.files) {
    $dir = if ($entry.location -eq 'repo') { Join-Path $testdata 'fixtures' } else { $largeDir }
    # Large entries name a path relative to the large folder, with forward slashes.
    $path = Join-Path $dir ($entry.name -replace '/', '\')

    if (-not (Test-Path $path)) {
        if ($entry.location -eq 'repo' -or ($inCi -and $entry.requiredInCi)) {
            Write-Host ("MISSING  {0}" -f $entry.name) -ForegroundColor Red
            $failed = $true
        }
        else {
            Write-Host ("absent   {0} (large, not required here)" -f $entry.name) -ForegroundColor Yellow
        }
        continue
    }

    $actual = (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($Update) {
        $entry.sha256 = $actual
        Write-Host ("updated  {0} {1}" -f $entry.name, $actual)
    }
    elseif ($actual -ne $entry.sha256) {
        Write-Host ("HASH     {0}: manifest {1} actual {2}" -f $entry.name, $entry.sha256, $actual) -ForegroundColor Red
        $failed = $true
    }
    else {
        Write-Host ("ok       {0}" -f $entry.name)
    }
}

if ($Update) {
    # UTF-8 without a byte order mark and LF: Set-Content -Encoding utf8 writes a mark in
    # Windows PowerShell 5.1, and System.Text.Json refuses it. PowerShell's own JSON layout
    # (four-space indentation); reformat by hand if that matters.
    $json = ($manifest | ConvertTo-Json -Depth 5) -replace "`r`n", "`n"
    [System.IO.File]::WriteAllText($manifestPath, $json + "`n", (New-Object System.Text.UTF8Encoding $false))
}

if ($failed) { exit 1 } else { exit 0 }
