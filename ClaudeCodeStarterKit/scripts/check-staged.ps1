# check-staged.ps1 - refuses a commit that changes code without a CHANGELOG line.
#
# WHAT IT DOES
#   Looks at the files staged for the commit (what `git add` put in). If any of them is
#   under src/ and CHANGELOG.md is not staged with them, it stops the commit and lists the
#   src/ files. Rule 9 of CLAUDE.md, checked by a machine. A change with nothing visible to
#   users still gets one line, under "### Internal".
#
# WHO CALLS IT
#   - git, through scripts/hooks/pre-commit, on every commit (yours or Claude's), before
#     verify.ps1
#   - Claude Code, through scripts/hooks/claude-commit-gate.ps1, before Claude's git commit
#   - you, by hand, to check before committing:
#       powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-staged.ps1
#
# RESULT
#   exit 0  fine, or nothing to check (not inside a git repository)
#   exit 1  src/ is staged without CHANGELOG.md
#
# Works both when this project is the git root and when it is a folder inside a monorepo.
$ErrorActionPreference = 'Stop'
# The project folder is the parent of scripts/.
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    # Where is the root of the git repository? The nearest folder upwards that holds .git (a
    # folder in a clone, a file in a linked worktree). Not asked from git: in a hook run from a
    # linked worktree git sets GIT_DIR, and `git rev-parse --show-toplevel` then answers with
    # the current folder, this project, instead of the root. Outside a repository there is
    # nothing to check.
    $gitRoot = [System.IO.Path]::GetFullPath($root)
    while ($gitRoot -and -not (Test-Path -LiteralPath (Join-Path $gitRoot '.git'))) {
        $gitRoot = Split-Path -Parent $gitRoot
    }
    if (-not $gitRoot) { exit 0 }
    # git names staged files relative to the repository root. In a monorepo this project is
    # a subfolder, so its files show up as "<folder>/src/...". $prefix is that "<folder>/"
    # part; it stays empty when the project is the repository root.
    $gitRoot = [System.IO.Path]::GetFullPath($gitRoot).TrimEnd('\', '/')
    $here = [System.IO.Path]::GetFullPath($root).TrimEnd('\', '/')
    $prefix = ''
    if ($here.Length -gt $gitRoot.Length) {
        $prefix = $here.Substring($gitRoot.Length + 1).Replace('\', '/') + '/'
    }

    # Which files are staged? A = added, C = copied, M = modified, R = renamed. D (deleted)
    # is not in the list, so a commit that only deletes files under src/ is not checked.
    # @( ) makes the result a list even when git prints one line or none.
    $staged = @(git diff --cached --name-only --diff-filter=ACMR)
    $srcTouched = @($staged | Where-Object { $_.StartsWith($prefix + 'src/') })
    $changelogTouched = $staged -contains ($prefix + 'CHANGELOG.md')

    if ($srcTouched.Count -gt 0 -and -not $changelogTouched) {
        Write-Host 'check-staged: src/ changed but CHANGELOG.md is not staged.' -ForegroundColor Red
        Write-Host '  Add a line under [Unreleased] (Added/Changed/Fixed/Removed, or Internal for a pure refactor) and stage it.'
        $srcTouched | ForEach-Object { Write-Host ("  " + $_) }
        exit 1
    }
    exit 0
}
finally {
    Pop-Location
}
