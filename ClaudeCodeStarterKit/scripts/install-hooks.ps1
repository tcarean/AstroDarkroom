# install-hooks.ps1 - tells git to run this project's git hooks at every commit.
#
# WHAT IT DOES
#   git can run small scripts, called hooks, at fixed moments. The kit has two, both run at
#   every commit: scripts/hooks/pre-commit (the gate) and scripts/hooks/commit-msg (the
#   one-line message rule). git looks for hooks in the folder named by its setting
#   core.hooksPath; this script sets it, depending on where the project sits:
#     - standalone repository (this folder is the git root): core.hooksPath = scripts/hooks,
#       so git runs the files in scripts/hooks/ directly;
#     - folder inside a monorepo (a bigger repository, like photo): it copies both hooks to
#       <git root>/.githooks/, with Unix (LF) line endings, and sets core.hooksPath =
#       .githooks for the whole repository. That one copy serves every project folder: the
#       pre-commit hook finds each project's own scripts/verify.ps1 by itself.
#   It changes only the git setting and the .githooks/ copy; it runs no check.
#
# WHO CALLS IT
#   Nobody automatically. You run it by hand:
#       powershell -NoProfile -ExecutionPolicy Bypass -File scripts/install-hooks.ps1
#   - standalone: once after git init, and once in every new clone (git settings are not
#     cloned);
#   - monorepo: only the kit's copy, and only after a hook in scripts/hooks/ changed, to
#     refresh .githooks/ (then commit .githooks/). A project's own copy would overwrite
#     .githooks/ with that project's hooks, which may be older. A new project folder needs
#     no install; a fresh clone of the monorepo needs only:
#       git config core.hooksPath .githooks
#
# RESULT
#   exit 0  hooks installed; it prints what it set
#   exit 1  not inside a git repository, or git config failed
$ErrorActionPreference = 'Stop'
# The project folder is the parent of scripts/.
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    # Find the root of the git repository this folder belongs to.
    $gitRoot = (git rev-parse --show-toplevel 2>$null)
    if ($LASTEXITCODE -ne 0 -or -not $gitRoot) { throw 'Not inside a git repository. Run git init first.' }
    $gitRoot = [System.IO.Path]::GetFullPath($gitRoot)
    $here = [System.IO.Path]::GetFullPath($root)

    # Standalone: the project folder is the repository root; point git at scripts/hooks.
    if ($gitRoot.TrimEnd('\', '/') -eq $here.TrimEnd('\', '/')) {
        git config core.hooksPath scripts/hooks
        Write-Host 'standalone repo: core.hooksPath = scripts/hooks'
    }
    else {
        # Monorepo: copy the two hooks to <git root>/.githooks/ and point git there.
        $target = Join-Path $gitRoot '.githooks'
        if (-not (Test-Path $target)) { New-Item -ItemType Directory -Path $target | Out-Null }
        foreach ($name in @('pre-commit', 'commit-msg')) {
            $source = Join-Path $PSScriptRoot ('hooks\' + $name)
            $dest = Join-Path $target $name
            # LF endings: git runs the hook through sh, which chokes on CR (Windows line
            # endings). Written as UTF-8 without a byte order mark, for the same reason.
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
    # $LASTEXITCODE is the exit code of the git config above.
    if ($LASTEXITCODE -ne 0) { throw 'git config failed.' }
}
finally {
    Pop-Location
}
