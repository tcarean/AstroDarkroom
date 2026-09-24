# Verifies tests/testdata/manifest.json: every listed file exists and its SHA-256 matches.
# 'repo' files must be present. 'large' files may be absent locally (warning) but fail in CI
# when requiredInCi is true. Use -Update to rewrite the hashes after changing a fixture on purpose.
[CmdletBinding()]
param([switch]$Update)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$testdata = Join-Path $root 'tests\testdata'
$manifestPath = Join-Path $testdata 'manifest.json'
$largeDir = if ($env:STARTER_TESTDATA_LARGE) { $env:STARTER_TESTDATA_LARGE } else { Join-Path $testdata 'large' }
$inCi = $env:CI -eq 'true'

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
$failed = $false

foreach ($entry in $manifest.files) {
    $dir = if ($entry.location -eq 'repo') { Join-Path $testdata 'fixtures' } else { $largeDir }
    $path = Join-Path $dir $entry.name

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
    $manifest | ConvertTo-Json -Depth 5 | Set-Content $manifestPath -Encoding utf8
}

if ($failed) { exit 1 } else { exit 0 }
