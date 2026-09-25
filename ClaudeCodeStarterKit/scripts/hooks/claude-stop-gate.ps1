# claude-stop-gate.ps1 - keeps Claude from ending its answer with unverified code.
#
# WHAT IT DOES
#   Claude Code calls it each time Claude is about to finish an answer (a "turn"). If this
#   session edited nothing under src/ or tests/, the turn ends at once. Otherwise the turn
#   may end only when:
#     1. scripts/verify.ps1 ran green with tests after the last such edit: .verify-stamp,
#        which only such a run writes (-SkipTests does not), is newer than the newest
#        edited file; and
#     2. if a src/ file edited by this session still has uncommitted changes, CHANGELOG.md has
#        uncommitted changes too. Edits that are already committed passed check-staged.ps1
#        at commit time, so after that commit a clean CHANGELOG is right, not a problem.
#   If not, it answers exit 2 with the reasons, and Claude Code sends Claude back to work
#   (the message points to /wrap-up). It never runs verify itself, so it takes under a
#   second. When Claude is already continuing because of this hook (stop_hook_active), it
#   lets the turn end, so a session can never be trapped in a loop.
#
# WHO CALLS IT
#   Claude Code, as the Stop hook in .claude/settings.json, at the end of every turn; only
#   when Claude Code runs in this project folder. Nobody calls it by hand. It reads two
#   files written by others:
#     - %TEMP%\claude-edits-<session id>.txt, the files this session edited, written by
#       scripts/hooks/claude-track-edits.ps1;
#     - .verify-stamp in the project folder, written by scripts/verify.ps1.
#
# RESULT (as Claude Code reads it)
#   exit 0  the turn may end
#   exit 2  the turn goes on; the text on stderr tells Claude what is missing
#
# Errors inside this script are not shown and do not stop it (SilentlyContinue): it carries
# on with the next line.
$ErrorActionPreference = 'SilentlyContinue'
# Claude Code sends JSON on standard input: session_id, stop_hook_active, ...
$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }
try { $payload = $raw | ConvertFrom-Json } catch { exit 0 }
if ($payload.stop_hook_active -eq $true) { exit 0 }

# No list of edited files for this session means nothing was edited: nothing to check.
$session = $payload.session_id
if (-not $session) { exit 0 }
$list = Join-Path $env:TEMP ("claude-edits-" + $session + ".txt")
if (-not (Test-Path $list)) { exit 0 }

# The project folder: Claude Code gives it to hooks in CLAUDE_PROJECT_DIR; otherwise the
# current folder.
$projectDir = if ($env:CLAUDE_PROJECT_DIR) { $env:CLAUDE_PROJECT_DIR } else { (Get-Location).Path }
$projectDir = [System.IO.Path]::GetFullPath($projectDir).TrimEnd('\', '/')
$src = Join-Path $projectDir 'src'
$tests = Join-Path $projectDir 'tests'

# Each edited path once, as a full path; keep only those under src/ or tests/.
$edited = Get-Content $list | ForEach-Object { [System.IO.Path]::GetFullPath($_) } | Sort-Object -Unique
$codeEdits = @($edited | Where-Object { $_.StartsWith($src, [System.StringComparison]::OrdinalIgnoreCase) -or $_.StartsWith($tests, [System.StringComparison]::OrdinalIgnoreCase) })
if ($codeEdits.Count -eq 0) { exit 0 }

$problems = @()

# 1. verify ran green, with tests, after the last code edit: compare the stamp's time with
#    the newest modification time among the edited files that still exist.
$stamp = Join-Path $projectDir '.verify-stamp'
$lastEdit = ($codeEdits | Where-Object { Test-Path $_ } | ForEach-Object { (Get-Item $_).LastWriteTimeUtc } | Sort-Object -Descending | Select-Object -First 1)
if (-not (Test-Path $stamp)) {
    $problems += 'scripts/verify.ps1 has not run green with tests in this project (no .verify-stamp; -SkipTests does not write it).'
}
elseif ($lastEdit -and (Get-Item $stamp).LastWriteTimeUtc -lt $lastEdit) {
    $problems += 'scripts/verify.ps1 has not run green with tests since the last edit under src/ or tests/ (-SkipTests does not count).'
}

# 2. CHANGELOG touched: git status lists a file when it has changes not yet committed (in the
#    working tree or staged). Required only while some src/ file this session edited is
#    itself still uncommitted; once those edits are committed (check-staged.ps1 demanded the
#    CHANGELOG line then), a clean CHANGELOG is the normal state, not a missing line.
$srcEdits = @($codeEdits | Where-Object { $_.StartsWith($src, [System.StringComparison]::OrdinalIgnoreCase) })
Push-Location $projectDir
$changelogDirty = (git status --porcelain -- CHANGELOG.md 2>$null)
$srcDirty = if ($srcEdits.Count -gt 0) { @(git status --porcelain -- $srcEdits 2>$null) } else { @() }
Pop-Location
if ($srcDirty.Count -gt 0 -and -not $changelogDirty) {
    $problems += 'src/ was edited but CHANGELOG.md is unchanged; add a line under [Unreleased] (or under Internal for a pure refactor).'
}

if ($problems.Count -eq 0) { exit 0 }

# Refuse: the reasons go to stderr, which Claude Code shows to Claude.
[Console]::Error.WriteLine('Stop gate: this session edited code, so before ending:')
$problems | ForEach-Object { [Console]::Error.WriteLine('  - ' + $_) }
[Console]::Error.WriteLine('Run /wrap-up (verify, CHANGELOG, requirements, decisions), then end the turn.')
exit 2
