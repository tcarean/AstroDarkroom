# claude-track-edits.ps1 - remembers which files this Claude session edited.
#
# WHAT IT DOES
#   After every file edit Claude makes, it appends the file's full path to a list kept for
#   the session: %TEMP%\claude-edits-<session id>.txt, one path per line. The Stop hook
#   (claude-stop-gate.ps1) reads that list, so it judges only what THIS session changed,
#   not the owner's own uncommitted work in the same folder. It never blocks an edit.
#   The lists are small and stay in %TEMP%; nothing deletes them.
#
# WHO CALLS IT
#   Claude Code, as a PostToolUse hook on its Edit, Write and MultiEdit tools, configured in
#   .claude/settings.json; only when Claude Code runs in this project folder. Nobody calls it
#   by hand.
#
# RESULT
#   Always exit 0.
$ErrorActionPreference = 'SilentlyContinue'
# Claude Code sends the finished tool call as JSON on standard input, including
# tool_input.file_path and session_id. Anything missing or unreadable: do nothing.
$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }
try { $payload = $raw | ConvertFrom-Json } catch { exit 0 }

$path = $payload.tool_input.file_path
$session = $payload.session_id
if (-not $path -or -not $session) { exit 0 }

# Append one line (UTF-8 without a byte order mark); the file is created on the first edit.
$list = Join-Path $env:TEMP ("claude-edits-" + $session + ".txt")
[System.IO.File]::AppendAllText($list, $path + "`n", (New-Object System.Text.UTF8Encoding $false))
exit 0
