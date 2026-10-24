# SPDX-License-Identifier: GPL-3.0-or-later
# merge-publish.ps1 - merges one published folder into another, refusing to overwrite a file.
#
# WHAT IT DOES
#   Puts every file under -From into -Into, at the same relative path, in two passes:
#     1. compares each file of -From with the file of the same relative path under -Into, if
#        there is one, by SHA-256 (a fingerprint of the file's bytes). Equal bytes: the file
#        is already there, skipped. Different bytes: a collision, two publishes that want
#        different files at one path (two runtime versions, two versions of a library, two
#        licence files);
#     2. copies the files that are not under -Into yet, subfolders kept, but only when pass 1
#        found no collision. On any collision nothing is copied, not even the files that do
#        not collide, so -Into is left as it was.
#   A release that bundles several publishes into one folder (release.publish in kit.json)
#   would otherwise let the last publish overwrite the others' files unseen (the kit's ADR
#   0036).
#
# WHO CALLS IT
#   - scripts/release.ps1, step 5, once per release.publish entry, from its staging folder
#     into artifacts/<version>/<output>/
#   - you, by hand, to see what two publishes have in common:
#       powershell -NoProfile -ExecutionPolicy Bypass -File scripts/merge-publish.ps1 -From a -Into b
#
# OPTIONS
#   -From <dir>   the folder to take the files from; required, must exist
#   -Into <dir>   the folder to put them in; required, created when absent
#
# RESULT
#   exit 0  merged; it prints how many files were copied and how many were already there
#   exit 1  one line per collision, "collision  <relative path>  (<From> differs from <Into>)",
#           and nothing copied
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$From,
    [Parameter(Mandatory = $true)][string]$Into
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $From -PathType Container)) { throw "merge-publish: -From $From is not a folder." }
$fromFull = [System.IO.Path]::GetFullPath($From).TrimEnd('\', '/')
$intoFull = [System.IO.Path]::GetFullPath($Into).TrimEnd('\', '/')

# --- pass 1: compare --------------------------------------------------------------------
$copy = New-Object System.Collections.Generic.List[string]
$collisions = New-Object System.Collections.Generic.List[string]
$same = 0
foreach ($file in Get-ChildItem -LiteralPath $fromFull -Recurse -File -Force) {
    # The path below -From, with forward slashes, as the collision line prints it.
    $relative = $file.FullName.Substring($fromFull.Length + 1)
    $target = Join-Path $intoFull $relative
    if (-not (Test-Path -LiteralPath $target -PathType Leaf)) {
        $copy.Add($relative)
        continue
    }
    $a = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    $b = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    if ($a -eq $b) { $same++ } else { $collisions.Add($relative.Replace('\', '/')) }
}

if ($collisions.Count -gt 0) {
    foreach ($c in $collisions) {
        Write-Host ("collision  {0}  ({1} differs from {2})" -f $c, $fromFull, $intoFull) -ForegroundColor Red
    }
    Write-Host ("merge-publish: {0} collision(s), nothing copied into {1}." -f $collisions.Count, $intoFull) -ForegroundColor Red
    exit 1
}

# --- pass 2: copy -----------------------------------------------------------------------
foreach ($relative in $copy) {
    $target = Join-Path $intoFull $relative
    $dir = Split-Path -Parent $target
    if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    Copy-Item -LiteralPath (Join-Path $fromFull $relative) -Destination $target
}
if (-not (Test-Path -LiteralPath $intoFull)) { New-Item -ItemType Directory -Path $intoFull -Force | Out-Null }
Write-Host ("merged   {0} file(s) copied, {1} already there with the same bytes, into {2}" -f $copy.Count, $same, $intoFull)
exit 0
