# Installs the versioned pre-commit gate for this git repository. Run once per clone:
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/install-hooks.ps1
#
# Standalone repo (this folder is the git root): core.hooksPath = scripts/hooks.
# Monorepo (this folder is inside a bigger repo): the hook is copied to <git root>/.githooks/
# and core.hooksPath = .githooks, so one hook serves every project folder that has a
# scripts/verify.ps1. Re-running the script refreshes the copy.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    $gitRoot = (git rev-parse --show-toplevel 2>$null)
    if ($LASTEXITCODE -ne 0 -or -not $gitRoot) { throw 'Not inside a git repository. Run git init first.' }
    $gitRoot = [System.IO.Path]::GetFullPath($gitRoot)
    $here = [System.IO.Path]::GetFullPath($root)

    if ($gitRoot.TrimEnd('\', '/') -eq $here.TrimEnd('\', '/')) {
        git config core.hooksPath scripts/hooks
        Write-Host 'standalone repo: core.hooksPath = scripts/hooks'
    }
    else {
        $target = Join-Path $gitRoot '.githooks'
        if (-not (Test-Path $target)) { New-Item -ItemType Directory -Path $target | Out-Null }
        foreach ($name in @('pre-commit', 'commit-msg')) {
            $source = Join-Path $PSScriptRoot ('hooks\' + $name)
            $dest = Join-Path $target $name
            # LF endings: git runs the hook through sh, which chokes on CR.
            $text = [System.IO.File]::ReadAllText($source) -replace "`r`n", "`n"
            [System.IO.File]::WriteAllText($dest, $text, (New-Object System.Text.UTF8Encoding $false))
        }
        # Keep them LF after a checkout on Windows too, whatever core.autocrlf says.
        [System.IO.File]::WriteAllText((Join-Path $target '.gitattributes'), "* text eol=lf`n", (New-Object System.Text.UTF8Encoding $false))
        Push-Location $gitRoot
        try { git config core.hooksPath .githooks } finally { Pop-Location }
        Write-Host ("monorepo: pre-commit and commit-msg copied to {0}; core.hooksPath = .githooks (repo root {1})" -f $target, $gitRoot)
        Write-Host 'Commit .githooks/ so every clone can run the same install.'
    }
    if ($LASTEXITCODE -ne 0) { throw 'git config failed.' }
}
finally {
    Pop-Location
}
