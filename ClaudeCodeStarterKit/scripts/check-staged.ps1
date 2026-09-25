# Staged-change rules, run by the git pre-commit hook and by the Claude Code commit gate.
# Rule 9 of CLAUDE.md made deterministic: a commit that touches src/ must also touch
# CHANGELOG.md. A change with nothing user-visible still gets one line under "### Internal".
# Works whether this project is the git root or a folder inside a monorepo.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $gitRoot = (git rev-parse --show-toplevel 2>$null)
    if ($LASTEXITCODE -ne 0) { exit 0 }
    $gitRoot = [System.IO.Path]::GetFullPath($gitRoot).TrimEnd('\', '/')
    $here = [System.IO.Path]::GetFullPath($root).TrimEnd('\', '/')
    $prefix = ''
    if ($here.Length -gt $gitRoot.Length) {
        $prefix = $here.Substring($gitRoot.Length + 1).Replace('\', '/') + '/'
    }

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
