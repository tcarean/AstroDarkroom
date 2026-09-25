# check-testdata.ps1 - checks that the test data files are present and unchanged.
#
# WHAT IT DOES
#   Reads tests/testdata/manifest.json, the list of every test data file with its SHA-256
#   hash (a fingerprint of the file's bytes), and checks each file:
#     - "repo" files live in tests/testdata/fixtures/ and must be there;
#     - "large" files are kept out of git, in tests/testdata/large/ or in the folder named by
#       the STARTER_TESTDATA_LARGE environment variable. A missing one is only a yellow
#       warning on your machine, but an error in CI when its entry has "requiredInCi": true;
#     - a file that is there must have exactly the hash in the manifest. Another hash means
#       the file changed since the manifest was written, so the tests that use it may no
#       longer test what they were written for.
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
$testdata = Join-Path $root 'tests\testdata'
$manifestPath = Join-Path $testdata 'manifest.json'
# Large files: an environment variable can point to another place (another disk, a share).
$largeDir = if ($env:STARTER_TESTDATA_LARGE) { $env:STARTER_TESTDATA_LARGE } else { Join-Path $testdata 'large' }
# CI services set CI=true; the kit's CI files also set it themselves.
$inCi = $env:CI -eq 'true'

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$failed = $false

foreach ($entry in $manifest.files) {
    # Where the file should be: fixtures/ for "repo" files, the large folder for the others.
    $dir = if ($entry.location -eq 'repo') { Join-Path $testdata 'fixtures' } else { $largeDir }
    $path = Join-Path $dir $entry.name

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

# -Update saves the new hashes. PowerShell writes its own JSON layout, so the file's
# indentation changes too; reformat it by hand if that matters to you.
if ($Update) {
    $manifest | ConvertTo-Json -Depth 5 | Set-Content $manifestPath -Encoding utf8
}

if ($failed) { exit 1 } else { exit 0 }
