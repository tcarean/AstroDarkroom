# SPDX-License-Identifier: GPL-3.0-or-later
# sync.ps1 - makes a project's shared folders identical to the kit's, or says where they differ.
#
# WHAT IT DOES
#   Three folders have one source, the kit (ClaudeCodeStarterKit): scripts/, .claude/skills/
#   and .claude/agents/ ($Shared below). Every project made from the kit carries byte-identical
#   copies and keeps its own values in kit.json and its own skill steps in docs/skills.md. A
#   fix or a feature in one of those folders is made in the kit and then copied to every
#   project; this script does the copy.
#     - Run in a project: mirrors the kit's shared folders (the kit is the folder kit.json
#       names as "kit") over this project's: files are added, overwritten and deleted so the
#       folders hold the same files with the same bytes.
#     - Run in the kit ("kit": "." in kit.json): does the same for every project folder
#       directly under the git root whose kit.json points at this kit.
#   Kit-only files are left out of the mirror in both directions, neither copied to a project
#   nor deleted from it: scripts/rename.ps1 (renames a fresh copy once),
#   scripts/install-hooks.ps1 (runs from the kit; a project's copy could overwrite the shared
#   .githooks/) and the two git hooks scripts/hooks/pre-commit and scripts/hooks/commit-msg
#   (one active copy, in .githooks/, installed from the kit). See $KitOnly below.
#   With -Check it changes nothing: it lists every path that differs (differs / missing /
#   extra) and exits 1 when there is one. That is step 0 of scripts/verify.ps1, which makes
#   an isolated edit in a project's shared folders impossible to commit, and asks every
#   project for the sync after a kit change.
#
# WHO CALLS IT
#   - scripts/verify.ps1, with -Check, in a project that has the kit beside it
#   - you or Claude, in the kit, after a change under a shared folder, then one commit per
#     project:
#       powershell -NoProfile -ExecutionPolicy Bypass -File scripts/sync.ps1
#   - you or Claude, in a project whose gate went red at step 0: the same command there
#
# OPTIONS
#   -Check   compare only; exit 1 on any difference
#
# RESULT
#   exit 0  identical (after the copy, or already with -Check)
#   exit 1  -Check found a difference, or the kit folder is missing
[CmdletBinding()]
param([switch]$Check)

$ErrorActionPreference = 'Stop'
# The project folder is the parent of scripts/.
$root = [System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\', '/')
. (Join-Path $PSScriptRoot 'kit-config.ps1')
$kit = Read-KitConfig $root

# The folders mirrored from the kit, relative to the project root, with forward slashes.
$Shared = @('scripts', '.claude/skills', '.claude/agents')
# Files that exist only in the kit, relative to the project root, with forward slashes.
$KitOnly = @('scripts/rename.ps1', 'scripts/install-hooks.ps1', 'scripts/hooks/pre-commit', 'scripts/hooks/commit-msg')

# Every file under the shared folders of one project root, relative with forward slashes,
# minus the kit-only ones.
function Get-SharedFiles([string]$projectRoot) {
    $files = @()
    foreach ($folder in $Shared) {
        $dir = Join-Path $projectRoot ($folder -replace '/', '\')
        if (-not (Test-Path -LiteralPath $dir)) { continue }
        $files += @(Get-ChildItem -LiteralPath $dir -Recurse -File |
            ForEach-Object { $_.FullName.Substring($projectRoot.Length).TrimStart('\', '/').Replace('\', '/') })
    }
    return @($files | Where-Object { $KitOnly -notcontains $_ } | Sort-Object)
}

function Get-Hash([string]$path) { return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }

# Compares (and, without -Check, mirrors) the kit's shared folders into one project. Returns
# the number of differences found.
function Sync-Project([string]$kitDir, [string]$projectDir) {
    $kitFiles = Get-SharedFiles $kitDir
    $projectFiles = Get-SharedFiles $projectDir
    $differences = 0
    foreach ($f in $kitFiles) {
        $src = Join-Path $kitDir ($f -replace '/', '\')
        $dst = Join-Path $projectDir ($f -replace '/', '\')
        if (-not (Test-Path -LiteralPath $dst)) { $state = 'missing' }
        elseif ((Get-Hash $src) -ne (Get-Hash $dst)) { $state = 'differs' }
        else { continue }
        $differences++
        if ($Check) {
            Write-Host ("  {0,-8} {1}" -f $state, $f) -ForegroundColor Red
        }
        else {
            $dir = Split-Path -Parent $dst
            if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
            Copy-Item -LiteralPath $src -Destination $dst -Force
            Write-Host ("  copied   {0}" -f $f)
        }
    }
    foreach ($f in $projectFiles) {
        if ($kitFiles -contains $f) { continue }
        $differences++
        if ($Check) {
            Write-Host ("  extra    {0}" -f $f) -ForegroundColor Red
        }
        else {
            Remove-Item -LiteralPath (Join-Path $projectDir ($f -replace '/', '\')) -Force
            Write-Host ("  deleted  {0}" -f $f)
        }
    }
    return $differences
}

$kitPath = Get-KitPath $kit $root
if (-not $kitPath) {
    Write-Host 'sync: kit.json names no kit folder ("kit"); nothing to compare with. Copy the shared folders from the kit by hand.' -ForegroundColor Yellow
    exit 0
}

# Which projects: this one, from the kit beside it; or, in the kit, every adopter.
$pairs = @()
if ($kitPath -eq $root) {
    # The git root: the nearest folder upwards with .git, as check-staged.ps1 finds it.
    $gitRoot = $root
    while ($gitRoot -and -not (Test-Path -LiteralPath (Join-Path $gitRoot '.git'))) {
        $gitRoot = Split-Path -Parent $gitRoot
    }
    if ($gitRoot) {
        foreach ($dir in Get-ChildItem -LiteralPath $gitRoot -Directory) {
            if (-not (Test-Path -LiteralPath (Join-Path $dir.FullName 'kit.json'))) { continue }
            $candidate = $dir.FullName.TrimEnd('\', '/')
            if ($candidate -eq $root) { continue }
            try { $theirs = Get-KitPath (Read-KitConfig $candidate) $candidate } catch { continue }
            if ($theirs -eq $root) { $pairs += , @($root, $candidate) }
        }
    }
    if ($pairs.Count -eq 0) {
        Write-Host 'sync: no project beside this kit has a kit.json pointing at it.' -ForegroundColor Yellow
        exit 0
    }
}
else {
    if (-not (Test-Path -LiteralPath (Join-Path $kitPath 'scripts'))) {
        Write-Host ("sync: kit not found at {0} (kit.json, key 'kit')." -f $kitPath) -ForegroundColor Red
        exit 1
    }
    $pairs += , @($kitPath, $root)
}

$total = 0
foreach ($pair in $pairs) {
    $mode = if ($Check) { 'check' } else { 'sync' }
    Write-Host ("== {0} {1} <- {2}" -f $mode, (Split-Path $pair[1] -Leaf), $pair[0]) -ForegroundColor Cyan
    $total += Sync-Project $pair[0] $pair[1]
}

if ($Check) {
    if ($total -gt 0) {
        Write-Host ("sync: {0} difference(s). scripts/, .claude/skills/ and .claude/agents/ are the kit's: run scripts/sync.ps1 here to take the kit's copy, and make changes in the kit." -f $total) -ForegroundColor Red
        exit 1
    }
    Write-Host 'sync: scripts/, .claude/skills/ and .claude/agents/ identical with the kit.'
    exit 0
}
Write-Host ("sync: {0} file(s) changed. Commit them in each project listed above." -f $total)
exit 0
