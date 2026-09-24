# Claude Code PreToolUse hook (see .claude/settings.json). Receives the tool call as JSON on
# stdin. When the command is a git commit, runs scripts/verify.ps1 first and blocks the commit
# (exit 2, message on stderr) if it fails or if --no-verify is present. Any other command
# passes through untouched (exit 0).
$ErrorActionPreference = 'Stop'
$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

try { $payload = $raw | ConvertFrom-Json } catch { exit 0 }
$command = $payload.tool_input.command
if (-not $command) { exit 0 }
# Only a git commit *command* counts: at the start of the line or after ; & | - not the words
# inside an echo or a commit message.
if ($command -notmatch '(^|[;&|]\s*)git\s+commit\b') { exit 0 }

if ($command -match '(^|[;&|]\s*)git\s+commit\b[^;&|]*(\s--no-verify\b|\s-n\b)') {
    [Console]::Error.WriteLine('Blocked: git commit --no-verify is forbidden (CLAUDE.md). Run scripts/verify.ps1 and fix what fails.')
    exit 2
}

$projectDir = if ($env:CLAUDE_PROJECT_DIR) { $env:CLAUDE_PROJECT_DIR } else { (Get-Location).Path }
$verify = Join-Path $projectDir 'scripts\verify.ps1'
if (-not (Test-Path $verify)) { exit 0 }

$output = & powershell -NoProfile -ExecutionPolicy Bypass -File $verify 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) {
    [Console]::Error.WriteLine('Blocked: scripts/verify.ps1 failed before commit. Output:')
    [Console]::Error.WriteLine($output)
    exit 2
}
exit 0
