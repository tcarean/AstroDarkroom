# SPDX-License-Identifier: GPL-3.0-or-later
# check-testdata.ps1 - checks that the test data files are present and unchanged.
#
# WHAT IT DOES
#   Reads manifest.json in the test data folder named by kit.json (testData.folder, in the
#   kit tests/testdata/), the list of every test data file with its SHA-256 hash (a
#   fingerprint of the file's bytes), and checks each file:
#     - "repo" files live in <testData.folder>/fixtures/ and must be there;
#     - "large" files are kept out of git, in <testData.folder>/large/ or in the folder named
#       by the environment variable kit.json calls testData.largeEnv (in the kit
#       STARTER_TESTDATA_LARGE). A missing one is only a yellow warning on your machine, but
#       an error in CI when its entry has "requiredInCi": true;
#     - a file that is there must have exactly the hash in the manifest. Another hash means
#       the file changed since the manifest was written, so the tests that use it may no
#       longer test what they were written for.
#   An entry's "name" may be a path relative to its folder, with forward slashes.
#   With -Update it writes the current hashes into the manifest instead of comparing them.
#
# WHO CALLS IT
#   - scripts/verify.ps1, as its first step; so the git pre-commit hook, Claude's commit
#     hook and CI all run it
#   - you, with -Update, after you changed a fixture on purpose:
#       powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-testdata.ps1 -Update
#   - Claude, in the /validate skill, to list which large files are available
#   Never use -Update to make a failing check pass (CLAUDE.md rule 3): first find out why
#   the file changed.
#
# RESULT
#   exit 0  every required file is there with the right hash (yellow warnings allowed)
#   exit 1  a required file is missing (red MISSING) or has another hash (red HASH)
[CmdletBinding()]
param([switch]$Update)

$ErrorActionPreference = 'Stop'
# The project folder is the parent of scripts/.
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'kit-config.ps1')
$kit = Read-KitConfig $root
$testdata = Get-KitFolderPath $root $kit.testData.folder
$manifestPath = Join-Path $testdata 'manifest.json'
# Large files: an environment variable can point to another place (another disk, a share).
$largeOverride = [System.Environment]::GetEnvironmentVariable($kit.testData.largeEnv)
$largeDir = if ($largeOverride) { $largeOverride } else { Join-Path $testdata 'large' }
# CI services set CI=true; the kit's CI files also set it themselves.
$inCi = $env:CI -eq 'true'

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$failed = $false

foreach ($entry in $manifest.files) {
    # Where the file should be: fixtures/ for "repo" files, the large folder for the others.
    $dir = if ($entry.location -eq 'repo') { Join-Path $testdata 'fixtures' } else { $largeDir }
    $path = Join-Path $dir ($entry.name -replace '/', '\')

    # Missing file: an error for "repo" files and for large files CI requires, else a warning.
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

    # File present: compute its hash, then record it (-Update) or compare it with the manifest.
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

# -Update saves the new hashes, as UTF-8 without a byte order mark and with LF line endings:
# Set-Content -Encoding utf8 writes a mark in Windows PowerShell 5.1, and System.Text.Json
# refuses a file that starts with one. PowerShell writes its own JSON layout, so the
# indentation changes too; reformat it by hand if that matters to you.
if ($Update) {
    $json = ($manifest | ConvertTo-Json -Depth 5) -replace "`r`n", "`n"
    [System.IO.File]::WriteAllText($manifestPath, $json + "`n", (New-Object System.Text.UTF8Encoding $false))
}

if ($failed) { exit 1 } else { exit 0 }
