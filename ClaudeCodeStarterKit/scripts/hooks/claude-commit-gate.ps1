# SPDX-License-Identifier: GPL-3.0-or-later
# claude-commit-gate.ps1 - stops a git commit by Claude unless the gate is green.
#
# WHAT IT DOES
#   Claude Code calls it before every shell command Claude is about to run, and hands it the
#   command as JSON on standard input. Anything that is not a git commit passes at once
#   (exit 0), so ordinary commands cost almost nothing. For a git commit it:
#     1. blocks git commit --no-verify (or -n), which would skip the git hooks;
#     2. runs scripts/check-staged.ps1 (a src/ change needs a CHANGELOG line);
#     3. runs scripts/verify.ps1, the full gate.
#   If one of them fails, the commit does not run, and Claude gets the reason and the output.
#   It fails closed: an error inside this hook itself also blocks the commit (exit 2), it
#   never lets one through by accident. Next to the git pre-commit hook it is a second check
#   that does not depend on the git hooks being installed, and it catches --no-verify, which
#   skips them.
#
# WHO CALLS IT
#   Claude Code, as a PreToolUse hook on its Bash and PowerShell tools, configured in
#   .claude/settings.json (timeout 600 s); only when Claude Code runs in this project folder.
#   Nobody calls it by hand.
#
# RESULT (as Claude Code reads it)
#   exit 0  let the command run
#   exit 2  block the command; the text on stderr is shown to Claude as the reason
#   Any other exit code would mean "let it run, just show the text", so this hook never
#   ends with one on purpose.
$ErrorActionPreference = 'Stop'
# The tool call, as JSON: {"tool_input": {"command": "..."}, ...}. Empty or unreadable
# input: do not get in the way.
$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

try { $payload = $raw | ConvertFrom-Json } catch { exit 0 }
$command = $payload.tool_input.command
if (-not $command) { exit 0 }
# Only a git commit *command* counts: at the start of the line or after ; & | - not the words
# inside an echo or a commit message.
if ($command -notmatch '(^|[;&|]\s*)git\s+commit\b') { exit 0 }

# Runs one of the check scripts in its own PowerShell and returns its exit code; what it
# printed (stdout and stderr together) is left in $script:checkOutput. cmd does the 2>&1
# merge, at the operating-system level. If PowerShell did it here, under 'Stop' the child's
# first stderr line (dotnet format prints its errors there) would become a terminating error,
# and this hook would end with exit 1, which Claude Code reads as "let the commit run".
function Invoke-Check([string]$scriptPath) {
    $script:checkOutput = & cmd /c ('powershell -NoProfile -ExecutionPolicy Bypass -File "' + $scriptPath + '" 2>&1') | Out-String
    return $LASTEXITCODE
}

# Decides for a git commit command; returns the exit code for Claude Code.
function Test-Commit {
    try {
        # 1. --no-verify (short form -n) anywhere in that git commit command.
        if ($command -match '(^|[;&|]\s*)git\s+commit\b[^;&|]*(\s--no-verify\b|\s-n\b)') {
            [Console]::Error.WriteLine('Blocked: git commit --no-verify is forbidden (CLAUDE.md). Run scripts/verify.ps1 and fix what fails.')
            return 2
        }

        # The project folder: Claude Code gives it to hooks in CLAUDE_PROJECT_DIR; otherwise the
        # current folder.
        $projectDir = if ($env:CLAUDE_PROJECT_DIR) { $env:CLAUDE_PROJECT_DIR } else { (Get-Location).Path }

        # 2. check-staged.
        $checkStaged = Join-Path $projectDir 'scripts\check-staged.ps1'
        if ((Test-Path $checkStaged) -and (Invoke-Check $checkStaged) -ne 0) {
            [Console]::Error.WriteLine('Blocked: staged changes break a rule. Output:')
            [Console]::Error.WriteLine($script:checkOutput)
            return 2
        }

        # 3. verify. A folder without verify.ps1 has no gate to run.
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
        # Fail closed: a broken hook must not let a commit through.
        [Console]::Error.WriteLine('Blocked: the commit gate hook itself failed, so the gate could not be checked: ' + $_.Exception.Message)
        return 2
    }
}

exit (Test-Commit)
