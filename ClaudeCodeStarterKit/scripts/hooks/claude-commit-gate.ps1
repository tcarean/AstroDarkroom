# SPDX-License-Identifier: GPL-3.0-or-later
# claude-commit-gate.ps1 - stops a git commit by Claude unless the gate is green.
#
# WHAT IT DOES
#   Claude Code calls it before every shell command Claude is about to run, and hands it the
#   command as JSON on standard input. Anything that is not a git commit passes at once
#   (exit 0), so ordinary commands cost almost nothing. For a git commit it:
#     1. blocks git commit --no-verify (or -n), which would skip the git hooks;
#     2. finds the folder the commit runs in, and the project of that folder (ADR 0020);
#     3. runs that project's scripts/check-staged.ps1 (a code change needs a CHANGELOG line);
#     4. runs that project's scripts/verify.ps1, the full gate.
#   If one of them fails, the commit does not run, and Claude gets the reason and the output.
#   It fails closed: an error inside this hook itself also blocks the commit (exit 2), it
#   never lets one through by accident. Next to the git pre-commit hook it is a second check
#   that does not depend on the git hooks being installed, and it catches --no-verify, which
#   skips them.
#
#   The folder of the commit is, in this order: the path of `git -C <path> commit`; else the
#   last `cd <path>` (or Set-Location, Push-Location, pushd) before the commit in the same
#   command; else the session's current folder, `cwd` in the JSON; else CLAUDE_PROJECT_DIR.
#   A relative path is taken from the folder before it; a Git Bash path (/e/...) is read as
#   E:\... . CLAUDE_PROJECT_DIR alone is not enough: it stays at the folder the session started
#   in, while the commit is usually made in a worktree. The project is the nearest folder at or
#   above that folder holding kit.json (Find-KitProject in kit-config.ps1). A commit in no
#   project passes: the git pre-commit hook still checks every project its staged files touch.
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
# The tool call, as JSON: {"cwd": "...", "tool_input": {"command": "..."}, ...}. Empty or
# unreadable input: do not get in the way.
$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

try { $payload = $raw | ConvertFrom-Json } catch { exit 0 }
$command = $payload.tool_input.command
if (-not $command) { exit 0 }

# One path argument: in double quotes, in single quotes, or up to the next space or separator.
$pathArg = '(?:"[^"]*"|''[^'']*''|[^\s;&|]+)'
# Only a git commit *command* counts: at the start of a line or after ; & | - not the words
# inside an echo or a commit message. git's own options may stand between git and commit:
# -C <path> (captured, possibly more than once) and -c <key=value>.
$commitPattern = '(?m)(^|[;&|]\s*)git(?:\s+-C\s+(?<gitdir>' + $pathArg + ')|\s+-c\s+[^\s;&|]+)*\s+commit\b'
$commit = [regex]::Match($command, $commitPattern)
if (-not $commit.Success) { exit 0 }

# Runs one of the check scripts in its own PowerShell and returns its exit code; what it
# printed (stdout and stderr together) is left in $script:checkOutput. cmd does the 2>&1
# merge, at the operating-system level. If PowerShell did it here, under 'Stop' the child's
# first stderr line (dotnet format prints its errors there) would become a terminating error,
# and this hook would end with exit 1, which Claude Code reads as "let the commit run".
function Invoke-Check([string]$scriptPath) {
    $script:checkOutput = & cmd /c ('powershell -NoProfile -ExecutionPolicy Bypass -File "' + $scriptPath + '" 2>&1') | Out-String
    return $LASTEXITCODE
}

# A path as the command wrote it, made a full Windows path: quotes dropped, a Git Bash drive
# (/e/...) turned into E:\..., ~ into the home folder, a relative path taken from $base.
function Resolve-CommandPath([string]$path, [string]$base) {
    $path = $path.Trim('"', "'")
    if ($path -match '^/([a-zA-Z])(/.*)?$') { $path = $Matches[1] + ':' + $(if ($Matches[2]) { $Matches[2] } else { '/' }) }
    if ($path -eq '~' -or $path.StartsWith('~/') -or $path.StartsWith('~\')) { $path = $HOME + $path.Substring(1) }
    $path = $path.Replace('/', '\')
    if (-not [System.IO.Path]::IsPathRooted($path)) { $path = Join-Path $base $path }
    return [System.IO.Path]::GetFullPath($path)
}

# The folder the commit runs in (see WHAT IT DOES).
function Get-CommitFolder {
    $folder = if ($payload.cwd) { $payload.cwd } elseif ($env:CLAUDE_PROJECT_DIR) { $env:CLAUDE_PROJECT_DIR } else { (Get-Location).Path }
    $folder = [System.IO.Path]::GetFullPath($folder)
    # The last change of folder before the commit, in the same command.
    $before = $command.Substring(0, $commit.Index + $commit.Groups[1].Length)
    $cdPattern = '(?m)(?:^|[;&|(]\s*)(?:cd|pushd|Set-Location|Push-Location|sl)\s+(?:-(?:Literal)?Path\s+)?(?<path>' + $pathArg + ')'
    $cds = [regex]::Matches($before, $cdPattern)
    if ($cds.Count -gt 0) { $folder = Resolve-CommandPath $cds[$cds.Count - 1].Groups['path'].Value $folder }
    # git -C, each one taken from the folder before it, as git does.
    foreach ($capture in $commit.Groups['gitdir'].Captures) { $folder = Resolve-CommandPath $capture.Value $folder }
    return $folder
}

# Decides for a git commit command; returns the exit code for Claude Code.
function Test-Commit {
    try {
        # 1. --no-verify (short form -n) in the arguments of any git commit of the command,
        #    before anything else runs.
        foreach ($m in [regex]::Matches($command, $commitPattern)) {
            if ($command.Substring($m.Index + $m.Length) -match '^[^;&|\r\n]*(\s--no-verify\b|\s-n\b)') {
                [Console]::Error.WriteLine('Blocked: git commit --no-verify is forbidden (CLAUDE.md). Run scripts/verify.ps1 and fix what fails.')
                return 2
            }
        }

        # 2. The project of the commit's folder. None: not this hook's to judge.
        . (Join-Path $PSScriptRoot '..\kit-config.ps1')
        $projectDir = Find-KitProject (Get-CommitFolder)
        if (-not $projectDir) { return 0 }

        # 3. check-staged.
        $checkStaged = Join-Path $projectDir 'scripts\check-staged.ps1'
        if ((Test-Path $checkStaged) -and (Invoke-Check $checkStaged) -ne 0) {
            [Console]::Error.WriteLine("Blocked: staged changes break a rule in $projectDir. Output:")
            [Console]::Error.WriteLine($script:checkOutput)
            return 2
        }

        # 4. verify. A project without verify.ps1 has no gate to run.
        $verify = Join-Path $projectDir 'scripts\verify.ps1'
        if (-not (Test-Path $verify)) { return 0 }
        if ((Invoke-Check $verify) -ne 0) {
            [Console]::Error.WriteLine("Blocked: scripts/verify.ps1 failed in $projectDir before commit. Output:")
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
