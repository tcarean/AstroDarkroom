# Claude Code PostToolUse hook on Edit|Write|MultiEdit. Appends the edited path to a per-session
# list in %TEMP%, so the Stop hook knows whether THIS session touched src/ or tests/ (and not
# just whether the working tree is dirty, which could be the owner's own uncommitted work).
$ErrorActionPreference = 'SilentlyContinue'
$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }
try { $payload = $raw | ConvertFrom-Json } catch { exit 0 }

$path = $payload.tool_input.file_path
$session = $payload.session_id
if (-not $path -or -not $session) { exit 0 }

$list = Join-Path $env:TEMP ("claude-edits-" + $session + ".txt")
[System.IO.File]::AppendAllText($list, $path + "`n", (New-Object System.Text.UTF8Encoding $false))
exit 0
