# Claude Code Stop hook. When this session edited src/ or tests/, the turn may not end until:
#   1. scripts/verify.ps1 has run green after the last such edit (it writes .verify-stamp), and
#   2. CHANGELOG.md has been modified.
# Otherwise exit 2 with the reason on stderr: Claude is told to continue and fix it (/wrap-up).
# stop_hook_active=true means Claude is already continuing because of this hook; then we let
# the turn end, so a session can never be trapped.
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
$src = Join-Path $projectDir 'src'
$tests = Join-Path $projectDir 'tests'

$edited = Get-Content $list | ForEach-Object { [System.IO.Path]::GetFullPath($_) } | Sort-Object -Unique
$codeEdits = @($edited | Where-Object { $_.StartsWith($src, [System.StringComparison]::OrdinalIgnoreCase) -or $_.StartsWith($tests, [System.StringComparison]::OrdinalIgnoreCase) })
if ($codeEdits.Count -eq 0) { exit 0 }

$problems = @()

# 1. verify ran green after the last code edit
$stamp = Join-Path $projectDir '.verify-stamp'
$lastEdit = ($codeEdits | Where-Object { Test-Path $_ } | ForEach-Object { (Get-Item $_).LastWriteTimeUtc } | Sort-Object -Descending | Select-Object -First 1)
if (-not (Test-Path $stamp)) {
    $problems += 'scripts/verify.ps1 has not run in this project (no .verify-stamp).'
}
elseif ($lastEdit -and (Get-Item $stamp).LastWriteTimeUtc -lt $lastEdit) {
    $problems += 'scripts/verify.ps1 has not run since the last edit under src/ or tests/.'
}

# 2. CHANGELOG touched (working tree or index)
Push-Location $projectDir
$changelogDirty = (git status --porcelain -- CHANGELOG.md 2>$null)
Pop-Location
$srcEdits = @($codeEdits | Where-Object { $_.StartsWith($src, [System.StringComparison]::OrdinalIgnoreCase) })
if ($srcEdits.Count -gt 0 -and -not $changelogDirty) {
    $problems += 'src/ was edited but CHANGELOG.md is unchanged; add a line under [Unreleased] (or under Internal for a pure refactor).'
}

if ($problems.Count -eq 0) { exit 0 }

[Console]::Error.WriteLine('Stop gate: this session edited code, so before ending:')
$problems | ForEach-Object { [Console]::Error.WriteLine('  - ' + $_) }
[Console]::Error.WriteLine('Run /wrap-up (verify, CHANGELOG, requirements, decisions), then end the turn.')
exit 2
