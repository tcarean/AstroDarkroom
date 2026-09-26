# SPDX-License-Identifier: GPL-3.0-or-later
# claude-stop-gate.ps1 - keeps Claude from ending its answer with unverified code.
#
# WHAT IT DOES
#   Claude Code calls it each time Claude is about to finish an answer (a "turn"). If this
#   session edited no code (the three application folders or the two test folders),
#   the turn ends at once. Otherwise the turn may end only when:
#     1. scripts/verify.ps1 ran green with tests after the last such edit: .verify-stamp,
#        which only such a run writes (-SkipTests does not), is newer than the newest
#        edited file; and
#     2. if an application file edited by this session still has uncommitted changes,
#        CHANGELOG.md has uncommitted changes too. Edits already committed passed
#        check-staged.ps1 at commit time, so after that a clean CHANGELOG is right.
#   If not, it answers exit 2 with the reasons, and Claude Code sends Claude back to work
#   (the message points to /wrap-up). It never runs verify itself. When Claude is already
#   continuing because of this hook (stop_hook_active), it lets the turn end.
#
# WHO CALLS IT
#   Claude Code, as the Stop hook in .claude/settings.json, at the end of every turn; only
#   when Claude Code runs in this project folder. It reads two files written by others:
#     - %TEMP%\claude-edits-<session id>.txt, written by scripts/hooks/claude-track-edits.ps1;
#     - .verify-stamp in the project folder, written by scripts/verify.ps1.
#
# RESULT (as Claude Code reads it)
#   exit 0  the turn may end
#   exit 2  the turn goes on; the text on stderr tells Claude what is missing
$ErrorActionPreference = 'SilentlyContinue'
$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }
try { $payload = $raw | ConvertFrom-Json } catch { exit 0 }
if ($payload.stop_hook_active -eq $true) { exit 0 }

$session = $payload.session_id
if (-not $session) { exit 0 }
$list = Join-Path $env:TEMP ("claude-edits-" + $session + ".txt")
if (-not (Test-Path $list)) { exit 0 }

$projectDir = if ($env:CLAUDE_PROJECT_DIR) { $env:CLAUDE_PROJECT_DIR } else { (Get-Location).Path }
$projectDir = [System.IO.Path]::GetFullPath($projectDir).TrimEnd('\', '/')
# FlatCheck keeps its projects at the folder root (ADR 0002): the code folders are listed.
$srcDirs = @('FlatCheck.Core', 'FlatCheck', 'FlatCheck.Wpf') | ForEach-Object { (Join-Path $projectDir $_) + '\' }
$testDirs = @('FlatCheck.Tests', 'FlatCheck.Wpf.Tests') | ForEach-Object { (Join-Path $projectDir $_) + '\' }
$srcFiles = @()

function Test-Under([string]$path, [string[]]$dirs) {
    foreach ($d in $dirs) { if ($path.StartsWith($d, [System.StringComparison]::OrdinalIgnoreCase)) { return $true } }
    return $false
}

$edited = Get-Content $list | ForEach-Object { [System.IO.Path]::GetFullPath($_) } | Sort-Object -Unique
$srcEdits = @($edited | Where-Object { (Test-Under $_ $srcDirs) -or ($srcFiles -contains $_) })
$codeEdits = @($edited | Where-Object { (Test-Under $_ $srcDirs) -or (Test-Under $_ $testDirs) -or ($srcFiles -contains $_) })
if ($codeEdits.Count -eq 0) { exit 0 }

$problems = @()

$stamp = Join-Path $projectDir '.verify-stamp'
$lastEdit = ($codeEdits | Where-Object { Test-Path $_ } | ForEach-Object { (Get-Item $_).LastWriteTimeUtc } | Sort-Object -Descending | Select-Object -First 1)
if (-not (Test-Path $stamp)) {
    $problems += 'scripts/verify.ps1 has not run green with tests in this project (no .verify-stamp; -SkipTests does not write it).'
}
elseif ($lastEdit -and (Get-Item $stamp).LastWriteTimeUtc -lt $lastEdit) {
    $problems += 'scripts/verify.ps1 has not run green with tests since the last code edit (-SkipTests does not count).'
}

Push-Location $projectDir
$changelogDirty = (git status --porcelain -- CHANGELOG.md 2>$null)
$srcDirty = if ($srcEdits.Count -gt 0) { @(git status --porcelain -- $srcEdits 2>$null) } else { @() }
Pop-Location
if ($srcDirty.Count -gt 0 -and -not $changelogDirty) {
    $problems += 'application code was edited but CHANGELOG.md is unchanged; add a line under [Unreleased] (or under Internal for a pure refactor).'
}

if ($problems.Count -eq 0) { exit 0 }

[Console]::Error.WriteLine('Stop gate: this session edited code, so before ending:')
$problems | ForEach-Object { [Console]::Error.WriteLine('  - ' + $_) }
[Console]::Error.WriteLine('Run /wrap-up (verify, CHANGELOG, requirements, decisions), then end the turn.')
exit 2
