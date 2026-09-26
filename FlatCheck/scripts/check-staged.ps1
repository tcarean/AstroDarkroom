# SPDX-License-Identifier: GPL-3.0-or-later
# check-staged.ps1 - refuses a commit that changes code without a CHANGELOG line.
#
# WHAT IT DOES
#   Looks at the files staged for the commit. If any of them is code (under one of the
#   three application folders) and CHANGELOG.md is not
#   staged with them, it stops the commit and lists the files. Rule 9 of CLAUDE.md, checked
#   by a machine. A change with nothing visible to users still gets one line, under
#   "### Internal". Test-only changes are not checked.
#
# WHO CALLS IT
#   - git, through <repo root>/.githooks/pre-commit, on every commit, before verify.ps1
#   - Claude Code, through scripts/hooks/claude-commit-gate.ps1, before Claude's git commit
#   - you, by hand:
#       powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-staged.ps1
#
# RESULT
#   exit 0  fine, or nothing to check (not inside a git repository)
#   exit 1  code is staged without CHANGELOG.md
#
# FlatCheck keeps its projects at the folder root rather than under src/ (ADR 0002), so the
# code folders are listed here instead of being a single prefix.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$codeFolders = @('FlatCheck.Core/', 'FlatCheck/', 'FlatCheck.Wpf/')
$codeFiles = @()
Push-Location $root
try {
    # The repository root is the nearest folder upwards that holds .git (a folder in a clone,
    # a file in a linked worktree). Not asked from git: in a hook run from a linked worktree
    # git sets GIT_DIR, and `git rev-parse --show-toplevel` then answers with the current
    # folder, FlatCheck, instead of the root. Outside a repository there is nothing to check.
    $gitRoot = [System.IO.Path]::GetFullPath($root)
    while ($gitRoot -and -not (Test-Path -LiteralPath (Join-Path $gitRoot '.git'))) {
        $gitRoot = Split-Path -Parent $gitRoot
    }
    if (-not $gitRoot) { exit 0 }
    # git names staged files relative to the repository root; in the photo monorepo this
    # project is a subfolder, so its files show up as "FlatCheck/...". $prefix is that part.
    $gitRoot = [System.IO.Path]::GetFullPath($gitRoot).TrimEnd('\', '/')
    $here = [System.IO.Path]::GetFullPath($root).TrimEnd('\', '/')
    $prefix = ''
    if ($here.Length -gt $gitRoot.Length) {
        $prefix = $here.Substring($gitRoot.Length + 1).Replace('\', '/') + '/'
    }

    # A = added, C = copied, M = modified, R = renamed; deletions are not checked.
    $staged = @(git diff --cached --name-only --diff-filter=ACMR)
    $codeTouched = @($staged | Where-Object {
        $f = $_
        (@($codeFolders | Where-Object { $f.StartsWith($prefix + $_) })).Count -gt 0 -or
        (@($codeFiles | Where-Object { $f -eq ($prefix + $_) })).Count -gt 0
    })
    $changelogTouched = $staged -contains ($prefix + 'CHANGELOG.md')

    if ($codeTouched.Count -gt 0 -and -not $changelogTouched) {
        Write-Host 'check-staged: code changed but CHANGELOG.md is not staged.' -ForegroundColor Red
        Write-Host '  Add a line under [Unreleased] (Added/Changed/Fixed/Removed, or Internal for a pure refactor) and stage it.'
        $codeTouched | ForEach-Object { Write-Host ("  " + $_) }
        exit 1
    }
    exit 0
}
finally {
    Pop-Location
}
