# SPDX-License-Identifier: GPL-3.0-or-later
# release.ps1 - the mechanical part of a release: checks, version number, CHANGELOG, build.
#
# WHAT IT DOES
#   1. Checks, and stops without changing anything if one fails (the kit's ADR 0029):
#        - the checkout is on main: a release is cut there, in the main checkout, where the
#          owner commits and tags; a task branch or a detached HEAD is refused;
#        - this project's working tree is clean;
#        - -Version is MAJOR.MINOR.PATCH, is not already a released heading of CHANGELOG.md
#          ("## [X.Y.Z] - date"), and is above the highest of them: that is the floor, since
#          a release always closes [Unreleased] under such a heading (with none, no floor);
#        - -Version is not below <Version> in Directory.Build.props. It may equal it: the
#          owner may bump <Version> per feature, and the release then uses it;
#        - CHANGELOG.md has something under [Unreleased];
#        - the git tag for this version does not exist yet.
#   2. Runs scripts/verify.ps1. If it is red, it stops; still nothing changed.
#   3. Writes the new version into <Version> in Directory.Build.props, unless it is already
#      there.
#   4. In CHANGELOG.md, turns "## [Unreleased]" into "## [X.Y.Z] - <today>" and puts a new,
#      empty "## [Unreleased]" above it.
#   5. Publishes what kit.json lists under release.publish, each entry to
#      artifacts/<version>/<output>/ with its own extra `dotnet publish` arguments (in the
#      kit: src/Starter.Cli to artifacts/<version>/ itself), unless -NoPublish.
#   It commits nothing and tags nothing. At the end it prints the commit and the tag to
#   propose: the commit limited to Directory.Build.props and CHANGELOG.md, the tag vX.Y.Z in a
#   standalone repository, <folder>-vX.Y.Z inside a monorepo.
#
# WHO CALLS IT
#   - Claude, in the /release skill, in a session the owner opened in the project folder of
#     the main checkout: first with -WhatIf, then for real; the skill then proposes the commit
#     and the tag, which the owner runs (the kit's ADRs 0010, 0021 and 0029)
#   - you, by hand:
#       powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release.ps1 -Version 0.2.0
#
# OPTIONS
#   -Version X.Y.Z   the new version; required
#   -WhatIf          run the step 1 checks and show what would change; touch nothing
#   -NoPublish       skip step 5
#
# RESULT
#   exit 0  done, or the -WhatIf preview finished; it prints the files changed and the tag
#   exit 1  a check failed or verify was red (nothing changed), or publish failed. A failed
#           publish comes after steps 3 and 4: Directory.Build.props and CHANGELOG.md are
#           already changed at that point.
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [switch]$NoPublish
)

$ErrorActionPreference = 'Stop'
# The project folder is the parent of scripts/.
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'kit-config.ps1')
$kit = Read-KitConfig $root
Push-Location $root
try {
    if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must be MAJOR.MINOR.PATCH, got '$Version'." }
    # [version] compares numerically: 0.10.0 is greater than 0.9.0.
    $new = [version]$Version

    # --- preconditions -----------------------------------------------------------------
    # A release is cut on main (the kit's ADR 0029). An empty name is a detached HEAD.
    $branch = (git branch --show-current 2>$null)
    if ($branch -ne 'main') {
        $where = if ($branch) { "branch '$branch'" } else { 'a detached HEAD' }
        throw "A release is cut on main, in the main checkout; this checkout is on $where."
    }

    # 'git status --porcelain -- .' lists the uncommitted changes in this folder, one per line.
    $dirty = @(git status --porcelain -- . 2>$null | Where-Object { $_ })
    if ($dirty.Count -gt 0) {
        throw ("Working tree is not clean; commit or stash first:`n" + ($dirty -join "`n"))
    }

    # The current version is the text between <Version> and </Version>.
    $propsPath = Join-Path $root 'Directory.Build.props'
    $props = [System.IO.File]::ReadAllText($propsPath)
    $m = [regex]::Match($props, '<Version>([^<]+)</Version>')
    if (-not $m.Success) { throw 'No <Version> element in Directory.Build.props.' }
    $current = [version]$m.Groups[1].Value
    if ($new -lt $current) { throw "Version $new is below <Version> $current in Directory.Build.props, which the owner already set; release $current or above." }

    # The released versions: every "## [X.Y.Z] - date" heading. The highest is the floor.
    $changelogPath = Join-Path $root 'CHANGELOG.md'
    $changelog = [System.IO.File]::ReadAllText($changelogPath)
    $released = @([regex]::Matches($changelog, '(?m)^## \[(\d+\.\d+\.\d+)\]') | ForEach-Object { [version]$_.Groups[1].Value })
    if ($released -contains $new) { throw "Version $new is already released: CHANGELOG.md has a [$new] heading." }
    $floor = $released | Sort-Object -Descending | Select-Object -First 1
    if ($floor -and $new -le $floor) { throw "Version $new is not above $floor, the newest released heading in CHANGELOG.md." }

    # The [Unreleased] section: from its heading to the next "## [" heading or the end of file.
    $unreleased = [regex]::Match($changelog, '(?ms)^## \[Unreleased\]\s*$(.*?)(?=^## \[|\z)')
    if (-not $unreleased.Success) { throw 'CHANGELOG.md has no [Unreleased] section.' }
    if ($unreleased.Groups[1].Value.Trim().Length -eq 0) { throw '[Unreleased] is empty; nothing to release.' }

    # --- tag name ---------------------------------------------------------------------
    # Standalone repository: v1.2.3. Folder inside a monorepo: <folder>-v1.2.3, so the tags
    # of different projects in one repository do not collide.
    $gitRoot = [System.IO.Path]::GetFullPath((git rev-parse --show-toplevel)).TrimEnd('\', '/')
    $here = [System.IO.Path]::GetFullPath($root).TrimEnd('\', '/')
    $tag = if ($gitRoot -eq $here) { "v$Version" } else { (Split-Path $here -Leaf).ToLowerInvariant() + "-v$Version" }
    if (git tag --list $tag) { throw "Tag $tag already exists." }

    # -WhatIf stops here: the checks passed, nothing was changed.
    $floorText = if ($floor) { $floor } else { 'none' }
    Write-Host ("== release {0} (<Version> {1}, newest released {2}), tag {3}" -f $new, $current, $floorText, $tag) -ForegroundColor Cyan
    if ($WhatIfPreference) {
        $targets = ($kit.release.publish | ForEach-Object { $_.project }) -join ', '
        Write-Host ("WhatIf: would run verify, set <Version>, close [Unreleased] in CHANGELOG, publish {0}." -f $targets)
        Write-Host ("Unreleased content ({0} lines):" -f ($unreleased.Groups[1].Value.Trim() -split "`n").Count)
        Write-Host $unreleased.Groups[1].Value.Trim()
        exit 0
    }

    # --- gate ---------------------------------------------------------------------------
    & (Get-KitShell) -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'verify.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'verify.ps1 failed; release aborted, nothing changed.' }

    # --- files --------------------------------------------------------------------------
    # From here on, files change. Only the number inside <Version>...</Version> is replaced;
    # the rest of Directory.Build.props stays as it is. When the owner already set it, the file
    # is left alone.
    $utf8 = New-Object System.Text.UTF8Encoding $false
    $propsChange = if ($new -eq $current) { 'unchanged, already {0}' -f $Version } else { '{0} -> {1}' -f $current, $Version }
    if ($new -ne $current) {
        $props = $props.Substring(0, $m.Groups[1].Index) + $Version + $props.Substring($m.Groups[1].Index + $m.Groups[1].Length)
        [System.IO.File]::WriteAllText($propsPath, $props, $utf8)
    }

    # CHANGELOG: a new, empty [Unreleased] on top; the old one becomes "[X.Y.Z] - date".
    # The file keeps its own line endings (CRLF or LF).
    $date = (Get-Date).ToString('yyyy-MM-dd')
    $nl = if ($changelog.Contains("`r`n")) { "`r`n" } else { "`n" }
    $header = "## [Unreleased]$nl$nl## [$Version] - $date"
    $changelog = $changelog.Substring(0, $unreleased.Index) + $header + $changelog.Substring($unreleased.Index + '## [Unreleased]'.Length)
    [System.IO.File]::WriteAllText($changelogPath, $changelog, $utf8)
    Write-Host ("changed  Directory.Build.props ({0}), CHANGELOG.md ([{1}] - {2})" -f $propsChange, $Version, $date)

    # --- artifacts ----------------------------------------------------------------------
    # dotnet publish puts each entry of release.publish in artifacts/<version>/<output>
    # (git-ignored); the script then prints the total size of what was published.
    if (-not $NoPublish) {
        $out = Join-Path $root ("artifacts\" + $Version)
        foreach ($p in $kit.release.publish) {
            $target = if ($p.output) { Join-Path $out ($p.output -replace '/', '\') } else { $out }
            $extra = @($p.args)
            dotnet publish (Join-Path $root ($p.project -replace '/', '\')) -c Release -o $target --nologo -v q @extra
            if ($LASTEXITCODE -ne 0) { throw ("dotnet publish ({0}) failed; version files are already changed, fix and re-run with -NoPublish." -f $p.project) }
        }
        $size = (Get-ChildItem $out -Recurse -File | Measure-Object Length -Sum).Sum
        Write-Host ("published {0} ({1:n1} MB)" -f $out, ($size / 1MB))
    }

    Write-Host ("== ready. Propose: git commit -m ""Release {0}"" -- Directory.Build.props CHANGELOG.md, then git tag -a {1} -m ""Release {0}"". Nothing is committed or tagged by this script." -f $Version, $tag) -ForegroundColor Green
    exit 0
}
finally {
    Pop-Location
}
