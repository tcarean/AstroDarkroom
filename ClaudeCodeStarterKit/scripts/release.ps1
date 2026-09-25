# The mechanical part of a release. Edits files and builds; commits nothing, tags nothing.
# The /release skill runs this, then proposes the commit and the tag (rule 13).
#
#   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release.ps1 -Version 0.2.0
#   ... -WhatIf        show what would change, touch nothing
#   ... -NoPublish     skip the artifact build
#
# Steps: preconditions (clean tree, version is newer, CHANGELOG has [Unreleased] content),
# scripts/verify.ps1, set <Version> in Directory.Build.props, turn [Unreleased] into
# [X.Y.Z] - date with a fresh empty [Unreleased] above it, dotnet publish the entry point to
# artifacts/<version>/. Prints the tag name to use (v1.2.3 standalone, <folder>-v1.2.3 in a
# monorepo) and the files it changed.
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [switch]$NoPublish,
    [string]$PublishProject = 'src\Starter.Cli'
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
        Write-Host 'WhatIf: would run verify, set <Version>, close [Unreleased] in CHANGELOG, publish.'
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

    $date = (Get-Date).ToString('yyyy-MM-dd')
    $nl = if ($changelog.Contains("`r`n")) { "`r`n" } else { "`n" }
    $header = "## [Unreleased]$nl$nl## [$Version] - $date"
    $changelog = $changelog.Substring(0, $unreleased.Index) + $header + $changelog.Substring($unreleased.Index + '## [Unreleased]'.Length)
    [System.IO.File]::WriteAllText($changelogPath, $changelog, $utf8)
    Write-Host ("changed  Directory.Build.props ({0} -> {1}), CHANGELOG.md ([{1}] - {2})" -f $current, $Version, $date)

    # --- artifact -----------------------------------------------------------------------
    if (-not $NoPublish) {
        $out = Join-Path $root ("artifacts\" + $Version)
        dotnet publish (Join-Path $root $PublishProject) -c Release -o $out --nologo -v q
        if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed; version files are already changed, fix and re-run with -NoPublish.' }
        $size = (Get-ChildItem $out -Recurse -File | Measure-Object Length -Sum).Sum
        Write-Host ("published {0} ({1:n1} MB)" -f $out, ($size / 1MB))
    }

    Write-Host ("== ready. Propose: commit 'Release {0}' and tag {1}. Nothing is committed or tagged by this script." -f $Version, $tag) -ForegroundColor Green
    exit 0
}
finally {
    Pop-Location
}
