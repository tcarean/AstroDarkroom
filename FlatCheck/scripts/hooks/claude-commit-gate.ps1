# SPDX-License-Identifier: GPL-3.0-or-later
# claude-commit-gate.ps1 - stops a git commit by Claude unless the gate is green.
#
# WHAT IT DOES
#   Claude Code calls it before every shell command Claude is about to run, and hands it the
#   command as JSON on standard input. Anything that is not a git commit passes at once
#   (exit 0). For a git commit it:
#     1. blocks git commit --no-verify (or -n), which would skip the git hooks;
#     2. runs scripts/check-staged.ps1 (a code change needs a CHANGELOG line);
#     3. runs scripts/verify.ps1, the full gate.
#   If one of them fails, the commit does not run, and Claude gets the reason and the output.
#   It fails closed: an error inside this hook itself also blocks the commit (exit 2).
#
# WHO CALLS IT
#   Claude Code, as a PreToolUse hook on its Bash and PowerShell tools, configured in
#   .claude/settings.json (timeout 600 s); only when Claude Code runs in this project folder.
#
# RESULT (as Claude Code reads it)
#   exit 0  let the command run
#   exit 2  block the command; the text on stderr is shown to Claude as the reason
$ErrorActionPreference = 'Stop'
$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

try { $payload = $raw | ConvertFrom-Json } catch { exit 0 }
$command = $payload.tool_input.command
if (-not $command) { exit 0 }
if ($command -notmatch '(^|[;&|]\s*)git\s+commit\b') { exit 0 }

# Runs one of the check scripts in its own PowerShell and returns its exit code; what it
# printed is left in $script:checkOutput. cmd merges stderr at the operating-system level:
# under 'Stop', a child's stderr line would otherwise become a terminating error here and
# end this hook with exit 1, which Claude Code reads as "let the commit run".
function Invoke-Check([string]$scriptPath) {
    $script:checkOutput = & cmd /c ('powershell -NoProfile -ExecutionPolicy Bypass -File "' + $scriptPath + '" 2>&1') | Out-String
    return $LASTEXITCODE
}

function Test-Commit {
    try {
        if ($command -match '(^|[;&|]\s*)git\s+commit\b[^;&|]*(\s--no-verify\b|\s-n\b)') {
            [Console]::Error.WriteLine('Blocked: git commit --no-verify is forbidden (CLAUDE.md). Run scripts/verify.ps1 and fix what fails.')
            return 2
        }

        $projectDir = if ($env:CLAUDE_PROJECT_DIR) { $env:CLAUDE_PROJECT_DIR } else { (Get-Location).Path }

        $checkStaged = Join-Path $projectDir 'scripts\check-staged.ps1'
        if ((Test-Path $checkStaged) -and (Invoke-Check $checkStaged) -ne 0) {
            [Console]::Error.WriteLine('Blocked: staged changes break a rule. Output:')
            [Console]::Error.WriteLine($script:checkOutput)
            return 2
        }

        $verify = Join-Path $projectDir 'scripts\verify.ps1'
        if (-not (Test-Path $verify)) { return 0 }
        if ((Invoke-Check $verify) -ne 0) {
            [Console]::Error.WriteLine('Blocked: scripts/verify.ps1 failed before commit. Output:')
            [Console]::Error.WriteLine($script:checkOutput)
            return 2
        }
        return 0
    }
    catch {
        [Console]::Error.WriteLine('Blocked: the commit gate hook itself failed, so the gate could not be checked: ' + $_.Exception.Message)
        return 2
    }
}

exit (Test-Commit)
