# SPDX-License-Identifier: GPL-3.0-or-later
# sync.ps1 - makes a project's shared folders and the process keys of its settings identical to
# the kit's, or says where they differ.
#
# WHAT IT DOES
#   Four folders have one source, the kit (ClaudeCodeStarterKit): scripts/, .claude/skills/,
#   .claude/agents/ and .claude/rules/kit/ (Get-KitSharedFolders in kit-config.ps1, the one
#   list). Every project made from the kit carries byte-identical copies and keeps its own
#   values in kit.json, its own skill steps in docs/skills.md and its own rules in CLAUDE.md. A
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
#   .githooks/) and the three git hooks scripts/hooks/pre-commit, scripts/hooks/pre-merge-commit
#   and scripts/hooks/commit-msg (one active copy, in .githooks/, installed from the kit). See $KitOnly below.
#   .claude/settings.json is shared in part (the kit's ADR 0028). Its process keys are the
#   kit's: "hooks", "permissions.ask", "permissions.deny", the "permissions.allow" entries for
#   git commands, and "attribution". They are compared parsed, the three lists as sets, so the
#   layout of the file does not count. The other allow entries (the commands a project uses),
#   "$comment" and any other key are the project's. A sync rewrites only the process keys that
#   differ, keeping the rest; a project without the file gets the kit's copy.
#   With -Check it changes nothing: it lists every path or key that differs (differs / missing
#   / extra) and exits 1 when there is one. That is step 0 of scripts/verify.ps1, which makes
#   an isolated edit in a project's shared folders or process keys impossible to commit, and
#   asks every project for the sync after a kit change.
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
$Shared = @(Get-KitSharedFolders)
$SharedNames = ($Shared | ForEach-Object { $_ + '/' }) -join ', '
# Files that exist only in the kit, relative to the project root, with forward slashes.
$KitOnly = @('scripts/rename.ps1', 'scripts/install-hooks.ps1', 'scripts/hooks/pre-commit', 'scripts/hooks/pre-merge-commit', 'scripts/hooks/commit-msg')

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

# An allow entry that is a git command, "Bash(git status*)" or "PowerShell(git commit*)": the
# kit's, like "ask" and "deny"; the others are the project's.
function Test-GitAllow([string]$entry) { return $entry -match '^(Bash|PowerShell)\(git[ *)]' }

# A list as a set: sorted, one per line, so order and layout do not count.
function Get-SetText($list) { return (@($list | Where-Object { $null -ne $_ }) | Sort-Object) -join "`n" }

# The process keys of parsed settings, each as text that compares equal when the kit's and
# the project's say the same.
function Get-ProcessKeys($settings) {
    $permissions = $settings.permissions
    return [ordered]@{
        'hooks'             = ConvertTo-Json -InputObject $settings.hooks -Depth 32 -Compress
        'permissions.ask'   = Get-SetText $permissions.ask
        'permissions.deny'  = Get-SetText $permissions.deny
        'permissions.allow' = Get-SetText (@($permissions.allow) | Where-Object { Test-GitAllow $_ })
        'attribution'       = ConvertTo-Json -InputObject $settings.attribution -Depth 32 -Compress
    }
}

# Sets property $name of $object to $value, keeping its place, or appends it; a $null value
# removes it.
function Set-Key($object, [string]$name, $value) {
    $existing = $object.PSObject.Properties[$name]
    if ($null -eq $value) {
        if ($existing) { $object.PSObject.Properties.Remove($name) }
    }
    elseif ($existing) { $existing.Value = $value }
    else { $object | Add-Member -NotePropertyName $name -NotePropertyValue $value }
}

# The project's settings with the kit's process keys. The allow list is the kit's entries that
# are git commands or that the project already has, in the kit's order, then the project's own.
function Merge-ProcessKeys($kitSettings, $projectSettings) {
    Set-Key $projectSettings 'hooks' $kitSettings.hooks
    Set-Key $projectSettings 'attribution' $kitSettings.attribution
    if ($null -eq $projectSettings.permissions) { Set-Key $projectSettings 'permissions' ([pscustomobject]@{}) }
    $kitPermissions = $kitSettings.permissions
    $permissions = $projectSettings.permissions
    $kitAllow = @($kitPermissions.allow | Where-Object { $null -ne $_ })
    $ownAllow = @($permissions.allow | Where-Object { $null -ne $_ })
    $allow = @($kitAllow | Where-Object { (Test-GitAllow $_) -or ($ownAllow -contains $_) }) +
        @($ownAllow | Where-Object { -not (Test-GitAllow $_) -and ($kitAllow -notcontains $_) })
    Set-Key $permissions 'allow' $(if ($allow.Count -gt 0) { , $allow } else { $null })
    Set-Key $permissions 'ask' $(if ($null -ne $kitPermissions.ask) { , @($kitPermissions.ask) } else { $null })
    Set-Key $permissions 'deny' $(if ($null -ne $kitPermissions.deny) { , @($kitPermissions.deny) } else { $null })
    return $projectSettings
}

# JSON as the settings files are written: two spaces per level, one item per line, non-ASCII
# and apostrophes kept as they are (ConvertTo-Json in Windows PowerShell would escape them and
# indent unevenly).
function ConvertTo-KitJson($Value, [string]$Indent = '') {
    $inner = $Indent + '  '
    if ($null -eq $Value) { return 'null' }
    if ($Value -is [string]) { return ConvertTo-KitJsonString $Value }
    if ($Value -is [bool]) { return $Value.ToString().ToLowerInvariant() }
    if ($Value -is [ValueType]) { return [string]::Format([System.Globalization.CultureInfo]::InvariantCulture, '{0}', $Value) }
    if ($Value -is [System.Collections.IEnumerable]) {
        $items = @($Value)
        if ($items.Count -eq 0) { return '[]' }
        $lines = foreach ($item in $items) { $inner + (ConvertTo-KitJson $item $inner) }
        return "[`n" + ($lines -join ",`n") + "`n" + $Indent + ']'
    }
    $properties = @($Value.PSObject.Properties)
    if ($properties.Count -eq 0) { return '{}' }
    $lines = foreach ($p in $properties) { $inner + (ConvertTo-KitJsonString $p.Name) + ': ' + (ConvertTo-KitJson $p.Value $inner) }
    return "{`n" + ($lines -join ",`n") + "`n" + $Indent + '}'
}

function ConvertTo-KitJsonString([string]$Text) {
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append('"')
    foreach ($c in $Text.ToCharArray()) {
        $code = [int]$c
        if ($code -eq 34) { [void]$sb.Append('\"') }
        elseif ($code -eq 92) { [void]$sb.Append('\\') }
        elseif ($code -eq 10) { [void]$sb.Append('\n') }
        elseif ($code -eq 13) { [void]$sb.Append('\r') }
        elseif ($code -eq 9) { [void]$sb.Append('\t') }
        elseif ($code -lt 32) { [void]$sb.Append(('\u{0:x4}' -f $code)) }
        else { [void]$sb.Append($c) }
    }
    [void]$sb.Append('"')
    return $sb.ToString()
}

function Read-Settings([string]$path) { return Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json }

# Compares (and, without -Check, copies) the process keys of the kit's .claude/settings.json
# into one project's. Returns the number of keys that differ (1 for a missing file).
function Sync-Settings([string]$kitDir, [string]$projectDir) {
    $name = '.claude/settings.json'
    $src = Join-Path $kitDir '.claude\settings.json'
    $dst = Join-Path $projectDir '.claude\settings.json'
    if (-not (Test-Path -LiteralPath $src)) { return 0 }
    if (-not (Test-Path -LiteralPath $dst)) {
        if ($Check) { Write-Host ("  missing  {0}" -f $name) -ForegroundColor Red }
        else {
            $dir = Split-Path -Parent $dst
            if (-not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
            Copy-Item -LiteralPath $src -Destination $dst -Force
            Write-Host ("  copied   {0}" -f $name)
        }
        return 1
    }
    $kitSettings = Read-Settings $src
    $projectSettings = Read-Settings $dst
    $kitKeys = Get-ProcessKeys $kitSettings
    $projectKeys = Get-ProcessKeys $projectSettings
    $differing = @($kitKeys.Keys | Where-Object { $kitKeys[$_] -cne $projectKeys[$_] })
    if ($differing.Count -eq 0) { return 0 }
    if ($Check) {
        foreach ($key in $differing) { Write-Host ("  differs  {0}: {1}" -f $name, $key) -ForegroundColor Red }
    }
    else {
        $merged = Merge-ProcessKeys $kitSettings $projectSettings
        $utf8 = New-Object System.Text.UTF8Encoding $false
        [System.IO.File]::WriteAllText($dst, (ConvertTo-KitJson $merged) + "`n", $utf8)
        Write-Host ("  updated  {0}: {1}" -f $name, ($differing -join ', '))
    }
    return $differing.Count
}

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
    # The git root: the nearest folder upwards with .git (a folder in a clone, a file in a
    # linked worktree).
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
    $total += Sync-Settings $pair[0] $pair[1]
}

if ($Check) {
    if ($total -gt 0) {
        Write-Host ("sync: {0} difference(s). {1} and the process keys of .claude/settings.json are the kit's: run scripts/sync.ps1 here to take the kit's copy, and make changes in the kit." -f $total, $SharedNames) -ForegroundColor Red
        exit 1
    }
    Write-Host ("sync: {0} and the process keys of .claude/settings.json identical with the kit." -f $SharedNames)
    exit 0
}
Write-Host ("sync: {0} file(s) or key(s) changed. Commit them in each project listed above." -f $total)
exit 0
