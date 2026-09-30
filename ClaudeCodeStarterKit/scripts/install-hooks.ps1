# SPDX-License-Identifier: GPL-3.0-or-later
# install-hooks.ps1 - tells git to run this project's git hooks at every commit.
#
# WHAT IT DOES
#   git can run small scripts, called hooks, at fixed moments. The kit has three:
#   scripts/hooks/pre-commit (the gate, at every commit), scripts/hooks/pre-merge-commit (the
#   same gate, at a merge git records without conflicts) and scripts/hooks/commit-msg (the
#   one-line message rule). git looks for hooks in the folder named by its setting
#   core.hooksPath; this script sets it, depending on where the project sits:
#     - standalone repository (this folder is the git root): core.hooksPath = scripts/hooks,
#       so git runs the files in scripts/hooks/ directly;
#     - folder inside a monorepo (a bigger repository, like photo): it copies the three hooks to
#       <git root>/.githooks/, with Unix (LF) line endings, and sets core.hooksPath for the
#       whole repository to the absolute path of the main checkout's .githooks/ (for photo,
#       E:/Photografie/Code/.githooks). That one copy serves every project folder and every
#       worktree: the pre-commit hook finds each project's own scripts/verify.ps1 by itself.
#       The path is absolute so that every worktree, a hand-made one included, runs the main
#       checkout's copy, which only a merge into main changes; a relative one would run each
#       branch's own copy (the kit's ADR 0016).
#   It changes only the git setting and the .githooks/ copy; it runs no check. It warns when a
#   per-worktree setting (config.worktree) hides the one it wrote.
#
# WHO CALLS IT
#   Nobody automatically. You run it by hand:
#       powershell -NoProfile -ExecutionPolicy Bypass -File scripts/install-hooks.ps1
#   - standalone: once after git init, and once in every new clone (git settings are not
#     cloned);
#   - monorepo: only the kit's copy, once in every new clone, and after a hook in
#     scripts/hooks/ changed, to refresh .githooks/ (then commit .githooks/). A project's own
#     copy would overwrite .githooks/ with that project's hooks, which may be older. A new
#     project folder needs no install. Run from a worktree, it writes the copy in that
#     worktree, but git keeps running the main checkout's copy until the change is merged.
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
        # Monorepo: copy the three hooks to <git root>/.githooks/ and point git there.
        $target = Join-Path $gitRoot '.githooks'
        if (-not (Test-Path $target)) { New-Item -ItemType Directory -Path $target | Out-Null }
        foreach ($name in @('pre-commit', 'pre-merge-commit', 'commit-msg')) {
            $source = Join-Path $PSScriptRoot ('hooks\' + $name)
            $dest = Join-Path $target $name
            # LF endings: git runs the hook through sh, which chokes on CR (Windows line
            # endings). Written as UTF-8 without a byte order mark, for the same reason.
            $text = [System.IO.File]::ReadAllText($source) -replace "`r`n", "`n"
            [System.IO.File]::WriteAllText($dest, $text, (New-Object System.Text.UTF8Encoding $false))
        }
        # Keep them LF after a checkout on Windows too, whatever core.autocrlf says.
        [System.IO.File]::WriteAllText((Join-Path $target '.gitattributes'), "* text eol=lf`n", (New-Object System.Text.UTF8Encoding $false))
        # The main checkout is the first entry of git worktree list, also when this runs in a
        # linked worktree. Its .githooks/ is the one every worktree runs, by absolute path.
        $main = ((git worktree list --porcelain) | Select-Object -First 1) -replace '^worktree ', ''
        $hooksPath = ($main.TrimEnd('\', '/') -replace '\\', '/') + '/.githooks'
        git config core.hooksPath $hooksPath
        if ($LASTEXITCODE -ne 0) { throw 'git config failed.' }
        Write-Host ("monorepo: pre-commit, pre-merge-commit and commit-msg copied to {0}; core.hooksPath = {1}" -f $target, $hooksPath)
        Write-Host 'Commit .githooks/ so every clone can run the same install.'
        # A per-worktree value (config.worktree) wins over the one just written.
        $effective = (git config --get core.hooksPath)
        if ($effective -ne $hooksPath) {
            Write-Warning ("this worktree overrides core.hooksPath with {0}; git config --worktree core.hooksPath {1} fixes it" -f $effective, $hooksPath)
        }
    }
    # $LASTEXITCODE is the exit code of the last git command above.
    if ($LASTEXITCODE -ne 0) { throw 'git config failed.' }
}
finally {
    Pop-Location
}
