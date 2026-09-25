# SPDX-License-Identifier: GPL-3.0-or-later
# release.ps1 - the mechanical part of a release: checks, version number, CHANGELOG, build.
#
# WHAT IT DOES
#   1. Checks, and stops without changing anything if one fails: this project's working tree
#      is clean; -Version is MAJOR.MINOR.PATCH and greater than the current <Version> in
#      Directory.Build.props; flatcheck.py agrees with that current version; CHANGELOG.md
#      has something under [Unreleased]; the git tag flatcheck-vX.Y.Z does not exist yet.
#   2. Runs scripts/verify.ps1. If it is red, it stops; still nothing changed.
#   3. Writes the new version into <Version> in Directory.Build.props AND into __version__
#      in flatcheck.py: the two must agree (VersionTests.R097_PythonReportsTheSameVersionAsTheCSharp
#      and crosscheck.py both check it).
#   4. In CHANGELOG.md, turns "## [Unreleased]" into "## [X.Y.Z] - <today>" and puts a new,
#      empty "## [Unreleased]" above it.
#   5. Publishes to artifacts/<version>/: the console as the single copyable flatcheck.exe
#      (self-contained, single file, native libraw DLLs extracted from it) under console/,
#      and the window app under wpf/. Both carry the lensfun/ folder beside them.
#   It commits nothing and tags nothing. At the end it prints the commit and the tag to
#   propose. The tag is flatcheck-vX.Y.Z because the photo repository holds several projects.
#
# WHO CALLS IT
#   - Claude, in the /release skill: first with -WhatIf, then for real; the skill then
#     proposes the commit and the tag and asks who runs them (CLAUDE.md rule 13)
#   - you, by hand:
#       powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release.ps1 -Version 2.3.0
#
# OPTIONS
#   -Version X.Y.Z   the new version; required
#   -WhatIf          run the step 1 checks and show what would change; touch nothing
#   -NoPublish       skip step 5
#
# RESULT
#   exit 0  done, or the -WhatIf preview finished
#   exit 1  a check failed or verify was red (nothing changed), or publish failed (steps 3
#           and 4 are already done at that point)
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [switch]$NoPublish
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Version must be MAJOR.MINOR.PATCH, got '$Version'." }
    $new = [version]$Version

    # --- preconditions -----------------------------------------------------------------
    $dirty = @(git status --porcelain -- . 2>$null | Where-Object { $_ })
    if ($dirty.Count -gt 0) {
        throw ("Working tree is not clean; commit or stash first:`n" + ($dirty -join "`n"))
    }

    $propsPath = Join-Path $root 'Directory.Build.props'
    $props = [System.IO.File]::ReadAllText($propsPath)
    $m = [regex]::Match($props, '<Version>([^<]+)</Version>')
    if (-not $m.Success) { throw 'No <Version> element in Directory.Build.props.' }
    $current = [version]$m.Groups[1].Value
    if ($new -le $current) { throw "New version $new must be greater than current $current." }

    $pyPath = Join-Path $root 'flatcheck.py'
    $py = [System.IO.File]::ReadAllText($pyPath)
    $pm = [regex]::Match($py, '(?m)^__version__\s*=\s*"([^"]+)"')
    if (-not $pm.Success) { throw 'No __version__ in flatcheck.py.' }
    if ($pm.Groups[1].Value -ne $m.Groups[1].Value) {
        throw ("flatcheck.py says {0} but Directory.Build.props says {1}; fix that first." -f $pm.Groups[1].Value, $m.Groups[1].Value)
    }

    $changelogPath = Join-Path $root 'CHANGELOG.md'
    $changelog = [System.IO.File]::ReadAllText($changelogPath)
    $unreleased = [regex]::Match($changelog, '(?ms)^## \[Unreleased\]\s*$(.*?)(?=^## \[|\z)')
    if (-not $unreleased.Success) { throw 'CHANGELOG.md has no [Unreleased] section.' }
    if ($unreleased.Groups[1].Value.Trim().Length -eq 0) { throw '[Unreleased] is empty; nothing to release.' }

    # --- tag name ---------------------------------------------------------------------
    $gitRoot = [System.IO.Path]::GetFullPath((git rev-parse --show-toplevel)).TrimEnd('\', '/')
    $here = [System.IO.Path]::GetFullPath($root).TrimEnd('\', '/')
    $tag = if ($gitRoot -eq $here) { "v$Version" } else { (Split-Path $here -Leaf).ToLowerInvariant() + "-v$Version" }
    if (git tag --list $tag) { throw "Tag $tag already exists." }

    Write-Host ("== release {0} -> {1}, tag {2}" -f $current, $new, $tag) -ForegroundColor Cyan
    if ($WhatIfPreference) {
        Write-Host 'WhatIf: would run verify, set <Version> and __version__, close [Unreleased] in CHANGELOG, publish console and wpf.'
        Write-Host ("Unreleased content ({0} lines):" -f ($unreleased.Groups[1].Value.Trim() -split "`n").Count)
        Write-Host $unreleased.Groups[1].Value.Trim()
        exit 0
    }

    # --- gate ---------------------------------------------------------------------------
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'verify.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'verify.ps1 failed; release aborted, nothing changed.' }

    # --- files --------------------------------------------------------------------------
    $utf8 = New-Object System.Text.UTF8Encoding $false
    $props = $props.Substring(0, $m.Groups[1].Index) + $Version + $props.Substring($m.Groups[1].Index + $m.Groups[1].Length)
    [System.IO.File]::WriteAllText($propsPath, $props, $utf8)
    $py = $py.Substring(0, $pm.Groups[1].Index) + $Version + $py.Substring($pm.Groups[1].Index + $pm.Groups[1].Length)
    [System.IO.File]::WriteAllText($pyPath, $py, $utf8)

    $date = (Get-Date).ToString('yyyy-MM-dd')
    $nl = if ($changelog.Contains("`r`n")) { "`r`n" } else { "`n" }
    $header = "## [Unreleased]$nl$nl## [$Version] - $date"
    $changelog = $changelog.Substring(0, $unreleased.Index) + $header + $changelog.Substring($unreleased.Index + '## [Unreleased]'.Length)
    [System.IO.File]::WriteAllText($changelogPath, $changelog, $utf8)
    Write-Host ("changed  Directory.Build.props and flatcheck.py ({0} -> {1}), CHANGELOG.md ([{1}] - {2})" -f $current, $Version, $date)

    # --- artifacts ----------------------------------------------------------------------
    if (-not $NoPublish) {
        $out = Join-Path $root ("artifacts\" + $Version)
        dotnet publish (Join-Path $root 'FlatCheck\FlatCheck.csproj') -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o (Join-Path $out 'console') --nologo -v q
        if ($LASTEXITCODE -ne 0) { throw 'dotnet publish (console) failed; version files are already changed, fix and re-run with -NoPublish.' }
        dotnet publish (Join-Path $root 'FlatCheck.Wpf\FlatCheck.Wpf.csproj') -c Release -r win-x64 --self-contained -o (Join-Path $out 'wpf') --nologo -v q
        if ($LASTEXITCODE -ne 0) { throw 'dotnet publish (wpf) failed; version files are already changed, fix and re-run with -NoPublish.' }
        $size = (Get-ChildItem $out -Recurse -File | Measure-Object Length -Sum).Sum
        Write-Host ("published {0} ({1:n1} MB)" -f $out, ($size / 1MB))
    }

    Write-Host ("== ready. Propose: commit 'Release {0}' and tag {1}. Nothing is committed or tagged by this script." -f $Version, $tag) -ForegroundColor Green
    exit 0
}
finally {
    Pop-Location
}
