# SPDX-License-Identifier: GPL-3.0-or-later
# claude-commit-gate.ps1 - stops a git commit by Claude unless the gate is green.
#
# WHAT IT DOES
#   Claude Code calls it before every shell command Claude is about to run, and hands it the
#   command as JSON on standard input. Anything that is not a git commit passes at once
#   (exit 0), so ordinary commands cost almost nothing. For a git commit it:
#     1. blocks a commit that would skip the git hooks: --no-verify, -n alone or in a group of
#        short options (-anm), or git -c core.hooksPath=...;
#     2. finds the folder the commit runs in (ADR 0020), and blocks the commit when the command
#        names a folder it cannot find (a variable, a folder that does not exist);
#     3. blocks the commit unless that folder is on a branch named claude/..., the branches the
#        assistant creates: main, any other branch and a detached HEAD are the owner's, who
#        commits there by hand (the kit's ADR 0021);
#     4. asks git, in that folder, which files the commit takes (the staged ones, plus the
#        changed tracked ones with -a) and finds the project of each: the nearest folder above
#        it holding scripts/verify.ps1, as the git pre-commit hook does. With no such file, the
#        project of the folder itself;
#     5. runs, for each project once, scripts/check-staged.ps1 (a code change needs a
#        CHANGELOG line), then scripts/verify.ps1, the full gate.
#   If one of them fails, the commit does not run, and Claude gets the reason and the output.
#   It fails closed: an error inside this hook itself also blocks the commit (exit 2), it
#   never lets one through by accident. Next to the git pre-commit hook it is a second check
#   that does not depend on the git hooks being installed, and it catches --no-verify, which
#   skips them. A commit that touches no project passes, as it does in pre-commit.
#
#   The folder of the commit is the session's current folder (`cwd` in the JSON; else
#   CLAUDE_PROJECT_DIR, else this process's), moved by every cd, chdir, Set-Location, sl,
#   pushd, Push-Location, popd and Pop-Location before the commit in the same command (text
#   inside a heredoc or a PowerShell here-string does not count), then by each
#   `git -C <path>` of the commit. A relative path is taken from the folder before it; a
#   Git Bash path (/e/...) is read as E:\...; a backslash before a space is Bash's escape.
#   CLAUDE_PROJECT_DIR alone is not enough: it stays at the folder the session started in,
#   while the commit is usually made in a worktree.
#
# WHO CALLS IT
#   Claude Code, as a PreToolUse hook on its Bash and PowerShell tools, configured in
#   .claude/settings.json (timeout 600 s); only when Claude Code runs in this project folder.
#   Nobody calls it by hand. Windows only, like the settings that call it (powershell, cmd).
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

# One path argument: in double quotes, in single quotes, or up to the next unescaped space or
# separator.
$pathArg = '(?:"[^"]*"|''[^'']*''|(?:\\ |[^\s;&|])+)'
# Only a git commit *command* counts: at the start of a line or after ; & | - not the words
# inside an echo or a commit message. git's own options may stand between git and commit:
# -C <path> and -c <key=value>, each possibly more than once.
$commitPattern = '(?m)(^|[;&|]\s*)git(?:\s+-C\s+(?<gitdir>' + $pathArg + ')|\s+-c\s+(?<config>[^\s;&|]+))*\s+commit\b'
$commits = [regex]::Matches($command, $commitPattern)
if ($commits.Count -eq 0) { exit 0 }
$commit = $commits[0]

# Runs one of the check scripts in its own PowerShell and returns its exit code; what it
# printed (stdout and stderr together) is left in $script:checkOutput. cmd does the 2>&1
# merge, at the operating-system level. If PowerShell did it here, under 'Stop' the child's
# first stderr line (dotnet format prints its errors there) would become a terminating error,
# and this hook would end with exit 1, which Claude Code reads as "let the commit run".
function Invoke-Check([string]$scriptPath) {
    $script:checkOutput = & cmd /c ('powershell -NoProfile -ExecutionPolicy Bypass -File "' + $scriptPath + '" 2>&1') | Out-String
    return $LASTEXITCODE
}

# git's output lines, or none when git fails (not a repository). 'Continue' here because under
# 'Stop' Windows PowerShell turns a native command's stderr into a terminating error even when
# it is sent to $null.
function Invoke-Git {
    $ErrorActionPreference = 'Continue'
    $out = @(& git -c core.quotepath=off @args 2>$null)
    if ($LASTEXITCODE -ne 0) { return @() }
    return $out
}

# The arguments of one git commit: the rest of its line, up to the next separator, with
# quoted text (a message) emptied, so that "-n" inside a message is not an option.
function Get-CommitArgs($match) {
    $rest = $command.Substring($match.Index + $match.Length)
    $rest = [regex]::Match($rest, '^[^;&|\r\n]*').Value
    return ' ' + ($rest -replace '"[^"]*"|''[^'']*''', '""') + ' '
}

# The short options of a commit's arguments: in a group like -anm every letter counts up to
# the first one that takes a value (-m, -F, -c, -C, -t, -u, -S), whose value is the rest.
function Get-ShortOptions([string]$arguments) {
    $letters = @()
    foreach ($group in [regex]::Matches($arguments, '(?<=\s)-([a-zA-Z]+)')) {
        foreach ($c in $group.Groups[1].Value.ToCharArray()) {
            $letters += [string]$c
            if ('mFcCtuS'.IndexOf($c) -ge 0) { break }
        }
    }
    return $letters
}

# A path as the command wrote it, made a full Windows path: quotes dropped (or Bash's "\ "
# escape undone), a Git Bash drive (/e/...) turned into E:\..., ~ into the home folder, a
# relative path taken from $base. $null when it is no path at all (a variable such as $env:X).
function Resolve-CommandPath([string]$path, [string]$base) {
    if ($path -match '^(["'']).*\1$') { $path = $path.Substring(1, $path.Length - 2) } else { $path = $path.Replace('\ ', ' ') }
    $path = $path.Trim()
    if ($path -match '^/([a-zA-Z])(/.*)?$') { $path = $Matches[1] + ':' + $(if ($Matches[2]) { $Matches[2] } else { '/' }) }
    if ($path -eq '~' -or $path.StartsWith('~/') -or $path.StartsWith('~\')) { $path = $HOME + $path.Substring(1) }
    $path = $path.Replace('/', '\')
    try {
        if (-not [System.IO.Path]::IsPathRooted($path)) { $path = Join-Path $base $path }
        return [System.IO.Path]::GetFullPath($path)
    }
    catch { return $null }
}

# Moves $folder to a path of the command, or records the path it cannot read and returns $null.
function Move-CommitFolder([string]$folder, [string]$path) {
    $next = Resolve-CommandPath $path $folder
    if (-not $next) { $script:unreadPath = $path }
    return $next
}

# The folder the commit runs in (see WHAT IT DOES).
function Get-CommitFolder {
    $folder = if ($payload.cwd) { $payload.cwd } elseif ($env:CLAUDE_PROJECT_DIR) { $env:CLAUDE_PROJECT_DIR } else { (Get-Location).Path }
    $folder = [System.IO.Path]::GetFullPath($folder)
    # The command before the commit, without heredoc and here-string bodies: their lines are
    # text written somewhere, not commands run here.
    $before = $command.Substring(0, $commit.Index + $commit.Groups[1].Length)
    $before = [regex]::Replace($before, '(?ms)<<-?[ \t]*([''"]?)(\w+)\1[^\n]*\n.*?^[ \t]*\2[ \t]*$', '')
    $before = [regex]::Replace($before, '(?s)@([''"])\r?\n.*?\r?\n\1@', '')
    # Every change of folder, in order; pushd remembers the folder it leaves, popd returns to it.
    $stack = New-Object System.Collections.Stack
    $locationPattern = '(?m)(?:^|[;&|(]\s*)(?<verb>cd|chdir|sl|Set-Location|pushd|Push-Location|popd|Pop-Location)\b(?:[ \t]+(?:-(?:Literal)?Path[ \t]+)?(?<path>' + $pathArg + '))?'
    foreach ($m in [regex]::Matches($before, $locationPattern)) {
        $verb = $m.Groups['verb'].Value
        if ($verb -eq 'popd' -or $verb -eq 'Pop-Location') {
            if ($stack.Count -gt 0) { $folder = $stack.Pop() }
            continue
        }
        if (-not $m.Groups['path'].Success) { continue }
        if ($verb -eq 'pushd' -or $verb -eq 'Push-Location') { $stack.Push($folder) }
        $folder = Move-CommitFolder $folder $m.Groups['path'].Value
        if (-not $folder) { return $null }
    }
    # git -C, each one taken from the folder before it, as git does.
    foreach ($capture in $commit.Groups['gitdir'].Captures) {
        $folder = Move-CommitFolder $folder $capture.Value
        if (-not $folder) { return $null }
    }
    return $folder
}

# The projects the commit touches, as full paths (see WHAT IT DOES, step 4).
function Get-CommitProjects([string]$folder) {
    $top = @(Invoke-Git -C $folder rev-parse --show-toplevel)
    if ($top.Count -eq 0) { return @() }
    $files = @(Invoke-Git -C $folder diff --cached --name-only --diff-filter=ACMR)
    $arguments = Get-CommitArgs $commit
    if ($arguments -match '\s--all\b' -or (Get-ShortOptions $arguments) -ccontains 'a') {
        $files += @(Invoke-Git -C $folder diff --name-only --diff-filter=ACMR)
    }
    $projects = @($files | ForEach-Object { Find-KitProject (Join-Path $top[0] $_) 'scripts\verify.ps1' })
    if ($files.Count -eq 0) { $projects = @(Find-KitProject $folder 'scripts\verify.ps1') }
    return @($projects | Where-Object { $_ } | Sort-Object -Unique)
}

# Decides for a git commit command; returns the exit code for Claude Code.
function Test-Commit {
    try {
        # 1. Anything that skips the git hooks, in any git commit of the command, before
        #    anything else runs. git accepts a long option by any unambiguous prefix;
        #    --no-veri is the shortest that is not also --no-verbose.
        foreach ($m in $commits) {
            $arguments = Get-CommitArgs $m
            if ($arguments -match '\s--no-veri[a-z]*(\s|=)' -or (Get-ShortOptions $arguments) -ccontains 'n') {
                [Console]::Error.WriteLine('Blocked: git commit --no-verify (or -n) is forbidden (CLAUDE.md). Run scripts/verify.ps1 and fix what fails.')
                return 2
            }
            if (@($m.Groups['config'].Captures | Where-Object { $_.Value -match '^[''"]?core\.hooksPath' }).Count -gt 0) {
                [Console]::Error.WriteLine('Blocked: git -c core.hooksPath=... would skip the git hooks (CLAUDE.md rule 4).')
                return 2
            }
        }

        # 2. The commit's folder; one the command names but that is not there cannot be judged.
        . (Join-Path $PSScriptRoot '..\kit-config.ps1')
        $folder = Get-CommitFolder
        if (-not $folder -or -not (Test-Path -LiteralPath $folder -PathType Container)) {
            $named = if ($folder) { "$folder does not exist" } else { "$($script:unreadPath) is not a path" }
            [Console]::Error.WriteLine("Blocked: cannot tell which folder this commit runs in ($named). Write the folder literally in the command (git -C <path> commit, or cd <path> first), not through a variable.")
            return 2
        }

        # 3. Only on a branch the assistant created. git names the branch of the folder's own
        #    worktree, so a task worktree commits while the main checkout stays on main.
        $branch = @(Invoke-Git -C $folder branch --show-current) -join ''
        if (-not $branch.StartsWith('claude/')) {
            $where = if ($branch) { "on $branch" } else { 'on a detached HEAD' }
            [Console]::Error.WriteLine("Blocked: $folder is $where. A commit there is the owner's, made by hand (CLAUDE.md rule 13, the kit's ADR 0021); the assistant commits only on the claude/ branch of its task.")
            return 2
        }

        # 4 and 5. Each project the commit touches: check-staged, then verify.
        foreach ($projectDir in (Get-CommitProjects $folder)) {
            $checkStaged = Join-Path $projectDir 'scripts\check-staged.ps1'
            if ((Test-Path $checkStaged) -and (Invoke-Check $checkStaged) -ne 0) {
                [Console]::Error.WriteLine("Blocked: staged changes break a rule in $projectDir. Output:")
                [Console]::Error.WriteLine($script:checkOutput)
                return 2
            }
            if ((Invoke-Check (Join-Path $projectDir 'scripts\verify.ps1')) -ne 0) {
                [Console]::Error.WriteLine("Blocked: scripts/verify.ps1 failed in $projectDir before commit. Output:")
                [Console]::Error.WriteLine($script:checkOutput)
                return 2
            }
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
