# SPDX-License-Identifier: GPL-3.0-or-later
# release.ps1 - the mechanical part of a release: checks, version number, CHANGELOG, build.
#
# WHAT IT DOES
#   1. Checks, and stops without changing anything if one fails (the kit's ADR 0029):
#        - the checkout is the main checkout (not a linked worktree) and is on main, or on
#          the branch kit.json names as release.branch: a release is cut there, where the
#          owner commits and tags; a task branch, a detached HEAD or a worktree is refused;
#        - this project's working tree is clean;
#        - -Version is MAJOR.MINOR.PATCH without leading zeros (2.3.0, not 2.03.0, which
#          would compare as 2.3.0 but be written as 2.03.0), is not already a released
#          heading of CHANGELOG.md ("## [X.Y.Z] - date"), and is above the highest of them:
#          that is the floor, since a release always closes [Unreleased] under such a
#          heading (with none, no floor);
#        - -Version is not below <Version> in Directory.Build.props. It may equal it: the
#          owner may bump <Version> per feature, and the release then uses it;
#        - CHANGELOG.md has something under [Unreleased];
#        - every release.publish entry is in a kit project (a folder with kit.json, the one
#          nearest above the entry's project file). An entry in another project, a sibling
#          folder such as ../X/src/X.Cli/X.Cli.csproj, makes that project a *component* of
#          this release (the kit's ADR 0036): it must have a readable kit.json, a <Version>
#          in its Directory.Build.props and a CHANGELOG.md, and its folder must be clean.
#          It is published from its checkout as it is; it need not be released first;
#        - the git tag for this version does not exist yet.
#   2. Runs scripts/verify.ps1. If it is red, it stops; still nothing changed.
#   3. Writes the new version into <Version> in Directory.Build.props, unless it is already
#      there.
#   4. In CHANGELOG.md, turns "## [Unreleased]" into "## [X.Y.Z] - <today>" and puts a new,
#      empty "## [Unreleased]" above it.
#   5. Publishes what kit.json lists under release.publish into artifacts/<version>/, which
#      it empties first, unless -NoPublish:
#        - each entry with its own extra `dotnet publish` arguments into a staging folder,
#          artifacts/<version>/.stage/<n>/, then merged into artifacts/<version>/<output>/ by
#          scripts/merge-publish.ps1, which copies nothing when a file already there has other
#          bytes (a collision: two publishes that want different files at one path). A
#          collision stops the release after steps 3 and 4; the owner decides, usually an
#          output subfolder for that entry, and re-runs with -PublishOnly. .stage/ is removed
#          after the last merge;
#        - writes artifacts/<version>/release.json: this project's name (its folder), the
#          version, the date, the commit, and each component (this project first) with its
#          <Version>, its last commit, whether its CHANGELOG has [Unreleased] content and
#          whether its folder was clean (the two version files a full release has just
#          changed do not count). An entry that published a release.json at the root
#          collides with it. Outside git the commits are empty and clean is false;
#        - when kit.json sets release.zip, zips artifacts/<version>/ into artifacts/<zip name>
#          ("{version}" replaced) and writes the zip's SHA-256 beside it in <zip name>.sha256.
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
#   -WhatIf          run the step 1 checks and show what would change and which components
#                    would be bundled; touch nothing
#   -NoPublish       skip step 5
#   -PublishOnly     run step 5 alone for -Version, with none of the checks and no change to
#                    Directory.Build.props or CHANGELOG.md: the retry after a collision, or a
#                    measurement of what a release would publish, from any branch. With
#                    -WhatIf it lists the components and what it would publish, and stops
#
# RESULT
#   exit 0  done, or the -WhatIf preview finished; it prints the files changed and the tag
#   exit 1  a check failed or verify was red (nothing changed), or publish failed or two
#           publishes collided. A failed publish or a collision comes after steps 3 and 4:
#           Directory.Build.props and CHANGELOG.md are already changed at that point.
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [switch]$NoPublish,
    [switch]$PublishOnly
)

$ErrorActionPreference = 'Stop'
# The project folder is the parent of scripts/.
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'kit-config.ps1')
$kit = Read-KitConfig $root

# <Version> in Directory.Build.props, and the [Unreleased] section of CHANGELOG.md: from its
# heading to the next "## [" heading or the end of the file.
$VersionPattern = '<Version>([^<]+)</Version>'
$UnreleasedPattern = '(?ms)^## \[Unreleased\]\s*$(.*?)(?=^## \[|\z)'

# git's output in $Dir as one trimmed string, or $null when git fails. Its error message is
# dropped, not raised: under Windows PowerShell 5.1 a native command's stderr, even sent to
# $null, stops a script whose $ErrorActionPreference is Stop (measured 2026-10-24).
function Invoke-GitText([string]$Dir, [string[]]$Arguments) {
    $ErrorActionPreference = 'Continue'
    $text = & git -C $Dir @Arguments 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    return (@($text) -join "`n").Trim()
}

# A kit.json path (forward slashes) as a path under the project on this system: pwsh on Linux,
# where CI may run, takes a backslash for part of a file name.
function ConvertTo-LocalPath([string]$Relative) {
    return Join-Path $root ($Relative -replace '[\\/]', [System.IO.Path]::DirectorySeparatorChar)
}

# The projects the release.publish entries belong to, this project first, each once: the
# nearest folder with kit.json above the entry's project file. Throws for an entry in no kit
# project, with the folder named.
function Get-ReleaseComponents {
    $here = [System.IO.Path]::GetFullPath($root).TrimEnd('\', '/')
    $roots = New-Object System.Collections.Generic.List[string]
    $roots.Add($here)
    foreach ($p in $kit.release.publish) {
        $projectFile = [System.IO.Path]::GetFullPath((ConvertTo-LocalPath $p.project))
        $componentRoot = Find-KitProject $projectFile
        if (-not $componentRoot) {
            throw ("release.publish entry '{0}' is in no kit project: no kit.json in {1} or above it, inside the repository." -f $p.project, (Split-Path -Parent $projectFile))
        }
        if (-not ($roots | Where-Object { $_ -eq $componentRoot })) { $roots.Add($componentRoot) }
    }
    return $roots.ToArray()
}

# What release.json says about one component: its folder name, its <Version> ('' when absent),
# its last commit ('' outside git), whether its CHANGELOG.md has content under [Unreleased],
# and whether its folder is clean, with the uncommitted paths when it is not. $Exclude names
# paths, relative to the component, that do not count against clean: the version files a
# full release has just changed itself.
function Get-ComponentInfo([string]$ComponentRoot, [string[]]$Exclude = @()) {
    $version = ''
    $propsFile = Join-Path $ComponentRoot 'Directory.Build.props'
    if (Test-Path -LiteralPath $propsFile) {
        $vm = [regex]::Match([System.IO.File]::ReadAllText($propsFile), $VersionPattern)
        if ($vm.Success) { $version = $vm.Groups[1].Value }
    }
    $pending = $false
    $changelogFile = Join-Path $ComponentRoot 'CHANGELOG.md'
    if (Test-Path -LiteralPath $changelogFile) {
        $um = [regex]::Match([System.IO.File]::ReadAllText($changelogFile), $UnreleasedPattern)
        $pending = $um.Success -and $um.Groups[1].Value.Trim().Length -gt 0
    }
    $commit = Invoke-GitText $ComponentRoot @('log', '-1', '--format=%H', '--', '.')
    # Where-Object first: $null piped on would make one bare ':(exclude)', which excludes all.
    $status = Invoke-GitText $ComponentRoot (@('status', '--porcelain', '--', '.') + @($Exclude | Where-Object { $_ } | ForEach-Object { ":(exclude)$_" }))
    $dirty = if ($null -eq $status) { @('(git status failed: not in a git repository?)') } elseif ($status) { @($status -split "`n") } else { @() }
    return [pscustomobject]@{
        Name = Split-Path $ComponentRoot -Leaf
        Version = $version
        Commit = if ($commit) { $commit } else { '' }
        Unreleased = $pending
        Clean = $dirty.Count -eq 0
        Dirty = $dirty
    }
}

# A JSON string literal: quotes, backslashes and control characters escaped.
function ConvertTo-JsonText([string]$Text) {
    $sb = New-Object System.Text.StringBuilder
    foreach ($ch in $Text.ToCharArray()) {
        switch ($ch) {
            '"' { [void]$sb.Append('\"') }
            '\' { [void]$sb.Append('\\') }
            default {
                if ([int]$ch -lt 32) { [void]$sb.AppendFormat('\u{0:x4}', [int]$ch) } else { [void]$sb.Append($ch) }
            }
        }
    }
    return '"' + $sb.ToString() + '"'
}

# Step 5: stage and merge every publish, write release.json, zip. Throws on a failed publish
# or a collision, with what to do next. -AfterVersionEdits: a full release, which has just
# changed Directory.Build.props and CHANGELOG.md itself; they do not make this project unclean.
function Publish-Release([switch]$AfterVersionEdits) {
    # Resolved first: an entry in no kit project stops before anything is built.
    $componentRoots = @(Get-ReleaseComponents)
    $out = Join-Path (ConvertTo-LocalPath 'artifacts') $Version
    # A previous run's files would be compared with this run's and reported as collisions,
    # and would end up in the zip: the folder is rebuilt from nothing.
    if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }
    New-Item -ItemType Directory -Path $out -Force | Out-Null
    $stage = Join-Path $out '.stage'
    $versionFiles = if ($AfterVersionEdits) { 'Directory.Build.props and CHANGELOG.md are already changed.' } else { 'The version files were not touched (-PublishOnly).' }
    $rerun = "re-run with -PublishOnly -Version $Version. $versionFiles"
    $index = 0
    foreach ($p in $kit.release.publish) {
        $index++
        $staged = Join-Path $stage $index
        $target = if ($p.output) { Join-Path $out ($p.output -replace '[\\/]', [System.IO.Path]::DirectorySeparatorChar) } else { $out }
        $extra = @($p.args)
        dotnet publish (ConvertTo-LocalPath $p.project) -c Release -o $staged --nologo -v q @extra
        if ($LASTEXITCODE -ne 0) { throw ("dotnet publish ({0}) failed; fix the cause shown above and {1}" -f $p.project, $rerun) }
        & (Get-KitShell) -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'merge-publish.ps1') -From $staged -Into $target
        # merge-publish.ps1: exit 1 is a collision, any other failure is an error of its own.
        if ($LASTEXITCODE -eq 1) { throw ("{0} collides with what the entries before it published. Resolve it (usually an output subfolder for that entry in kit.json) and {1}" -f $p.project, $rerun) }
        if ($LASTEXITCODE -ne 0) { throw ("merge-publish.ps1 failed for {0} (exit {1}); see its message above, then {2}" -f $p.project, $LASTEXITCODE, $rerun) }
    }
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }

    # release.json is the release's own file: an entry that published one at the root collides
    # with it, as two entries collide, and is never overwritten.
    $manifest = Join-Path $out 'release.json'
    if (Test-Path -LiteralPath $manifest) {
        Write-Host ("collision  release.json  (published by an entry; the release writes its own into {0})" -f $out) -ForegroundColor Red
        throw ("An entry of release.publish published release.json into {0}, where the release writes its own. Give that entry an output subfolder in kit.json and {1}" -f $out, $rerun)
    }

    # release.json: what this folder holds, for the people who receive it and for a program
    # that shows the versions. UTF-8 without a byte order mark, LF line ends. A component whose
    # folder held uncommitted files says so ("clean": false): -PublishOnly checks nothing.
    $components = @()
    foreach ($componentRoot in $componentRoots) {
        $exclude = if ($AfterVersionEdits -and $components.Count -eq 0) { @('Directory.Build.props', 'CHANGELOG.md') } else { @() }
        $components += Get-ComponentInfo $componentRoot $exclude
    }
    $head = Invoke-GitText $root @('rev-parse', 'HEAD')
    if (-not $head) { $head = '' }
    $lines = @(
        '{',
        ('  "name": {0},' -f (ConvertTo-JsonText (Split-Path ([System.IO.Path]::GetFullPath($root).TrimEnd('\', '/')) -Leaf))),
        ('  "version": {0},' -f (ConvertTo-JsonText $Version)),
        ('  "date": {0},' -f (ConvertTo-JsonText (Get-Date).ToString('yyyy-MM-dd'))),
        ('  "commit": {0},' -f (ConvertTo-JsonText $head)),
        '  "components": ['
    )
    for ($i = 0; $i -lt $components.Count; $i++) {
        $c = $components[$i]
        $comma = if ($i -lt $components.Count - 1) { ',' } else { '' }
        $lines += ('    {{ "name": {0}, "version": {1}, "commit": {2}, "unreleased": {3}, "clean": {4} }}{5}' -f
            (ConvertTo-JsonText $c.Name), (ConvertTo-JsonText $c.Version), (ConvertTo-JsonText $c.Commit),
            $c.Unreleased.ToString().ToLowerInvariant(), $c.Clean.ToString().ToLowerInvariant(), $comma)
    }
    $lines += @('  ]', '}')
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($manifest, (($lines -join "`n") + "`n"), $utf8NoBom)
    foreach ($c in $components) {
        $flag = if ($c.Unreleased) { ', unreleased changes' } else { '' }
        if (-not $c.Clean) { $flag += ', NOT CLEAN' }
        Write-Host ("component {0} {1} ({2}{3})" -f $c.Name, $c.Version, $(if ($c.Commit) { $c.Commit.Substring(0, [Math]::Min(7, $c.Commit.Length)) } else { 'no commit' }), $flag)
    }

    # -Force: a hidden file is published like any other, as merge-publish.ps1 copied it.
    $files = @(Get-ChildItem -LiteralPath $out -Recurse -File -Force)
    $size = ($files | Measure-Object Length -Sum).Sum
    Write-Host ("published {0} ({1} files, {2:n1} MB)" -f $out, $files.Count, ($size / 1MB))

    # The zip sits in artifacts/, beside the folder, so it never contains itself. Entry by
    # entry, with forward slashes: under Windows PowerShell 5.1 both Compress-Archive and
    # ZipFile.CreateFromDirectory write backslashes into the entry names (measured
    # 2026-10-24), which other unzip tools read as part of the file name.
    if ($kit.release.zip) {
        $zipName = $kit.release.zip.Replace('{version}', $Version)
        $zip = Join-Path (ConvertTo-LocalPath 'artifacts') $zipName
        if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
        Add-Type -AssemblyName System.IO.Compression
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        $outFull = [System.IO.Path]::GetFullPath($out).TrimEnd('\', '/')
        $stream = [System.IO.File]::Open($zip, [System.IO.FileMode]::CreateNew)
        $archive = New-Object System.IO.Compression.ZipArchive($stream, [System.IO.Compression.ZipArchiveMode]::Create)
        try {
            foreach ($f in Get-ChildItem -LiteralPath $outFull -Recurse -File -Force) {
                $entry = $f.FullName.Substring($outFull.Length + 1).Replace([System.IO.Path]::DirectorySeparatorChar, '/')
                [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $f.FullName, $entry, [System.IO.Compression.CompressionLevel]::Optimal)
            }
        }
        finally {
            $archive.Dispose()
            $stream.Dispose()
        }
        $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
        [System.IO.File]::WriteAllText($zip + '.sha256', ("{0}  {1}`n" -f $hash, $zipName), $utf8NoBom)
        Write-Host ("zipped    {0} ({1:n1} MB), SHA-256 in {2}.sha256" -f $zip, ((Get-Item -LiteralPath $zip).Length / 1MB), $zipName)
    }
}

Push-Location $root
try {
    # No leading zeros: the text goes into the heading and the tag, the number into comparisons.
    if ($Version -notmatch '^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$') { throw "Version must be MAJOR.MINOR.PATCH without leading zeros, got '$Version'." }
    # [version] compares numerically: 0.10.0 is greater than 0.9.0.
    $new = [version]$Version
    if ($PublishOnly -and $NoPublish) { throw '-PublishOnly and -NoPublish exclude each other.' }

    # -PublishOnly: step 5 alone, no checks, no version files (the kit's ADR 0036).
    if ($PublishOnly) {
        Write-Host ("== publish only {0}: no checks, Directory.Build.props and CHANGELOG.md untouched" -f $Version) -ForegroundColor Cyan
        # -WhatIf previews here: step 5 runs dotnet publish and a child script, which a
        # -WhatIf would not reach, so nothing of it starts.
        if ($WhatIfPreference) {
            $targets = ($kit.release.publish | ForEach-Object { $_.project }) -join ', '
            $zipPlan = if ($kit.release.zip) { ', zip ' + $kit.release.zip.Replace('{version}', $Version) } else { '' }
            foreach ($componentRoot in @(Get-ReleaseComponents)) { Write-Host ("component {0} ({1})" -f (Split-Path $componentRoot -Leaf), $componentRoot) }
            Write-Host ("WhatIf: would publish {0} into artifacts/{1}/ after emptying it, write release.json{2}." -f $(if ($targets) { $targets } else { 'nothing' }), $Version, $zipPlan)
            exit 0
        }
        Publish-Release
        Write-Host '== published. Nothing is committed or tagged by this script.' -ForegroundColor Green
        exit 0
    }

    # --- preconditions -----------------------------------------------------------------
    # A release is cut on main (or kit.json's release.branch), in the main checkout (the kit's
    # ADR 0029). An empty name is a detached HEAD. The main checkout is the worktree whose git
    # folder is the common one, the test the git pre-commit hook makes (the kit's ADR 0026);
    # absolute forms, since git may report either side relative to a subfolder.
    $releaseBranch = $kit.release.branch
    $gitDir = [System.IO.Path]::GetFullPath((git rev-parse --absolute-git-dir)).TrimEnd('\', '/')
    $commonDir = [System.IO.Path]::GetFullPath((git rev-parse --path-format=absolute --git-common-dir)).TrimEnd('\', '/')
    if ($gitDir -ne $commonDir) {
        throw "A release is cut on $releaseBranch, in the main checkout; this is a linked worktree ($root)."
    }
    $branch = (git branch --show-current 2>$null)
    if ($branch -ne $releaseBranch) {
        $where = if ($branch) { "branch '$branch'" } else { 'a detached HEAD' }
        throw "A release is cut on $releaseBranch, in the main checkout; this checkout is on $where."
    }

    # 'git status --porcelain -- .' lists the uncommitted changes in this folder, one per line.
    $dirty = @(git status --porcelain -- . 2>$null | Where-Object { $_ })
    if ($dirty.Count -gt 0) {
        throw ("Working tree is not clean; commit or stash first:`n" + ($dirty -join "`n"))
    }

    # The current version is the text between <Version> and </Version>.
    $propsPath = Join-Path $root 'Directory.Build.props'
    $props = [System.IO.File]::ReadAllText($propsPath)
    $m = [regex]::Match($props, $VersionPattern)
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

    # The [Unreleased] section ($UnreleasedPattern).
    $unreleased = [regex]::Match($changelog, $UnreleasedPattern)
    if (-not $unreleased.Success) { throw 'CHANGELOG.md has no [Unreleased] section.' }
    if ($unreleased.Groups[1].Value.Trim().Length -eq 0) { throw '[Unreleased] is empty; nothing to release.' }

    # The components: the other kit projects the entries publish from (the kit's ADR 0036).
    # Each is published from its folder as it is, so the folder must be committed; a problem
    # is collected, and -WhatIf lists every component before the first problem stops it.
    $here = [System.IO.Path]::GetFullPath($root).TrimEnd('\', '/')
    $componentLines = @()
    $problems = @()
    foreach ($componentRoot in @(Get-ReleaseComponents | Where-Object { $_ -ne $here })) {
        $name = Split-Path $componentRoot -Leaf
        $info = Get-ComponentInfo $componentRoot
        $problem = $null
        try { $null = Read-KitConfig $componentRoot } catch { $problem = "Component $name has no usable kit.json: $($_.Exception.Message)" }
        if (-not $problem) {
            if (-not $info.Version) { $problem = "Component $name has no <Version> in Directory.Build.props." }
            elseif (-not (Test-Path -LiteralPath (Join-Path $componentRoot 'CHANGELOG.md'))) { $problem = "Component $name has no CHANGELOG.md." }
            elseif (-not $info.Clean) { $problem = "Component $name is not clean; commit or stash first:`n" + ($info.Dirty -join "`n") }
        }
        $state = if ($problem) { 'NOT READY' } else { 'clean' }
        $flag = if ($info.Unreleased) { ', unreleased changes' } else { '' }
        $componentLines += ("component {0} {1} ({2}{3}) - {4}" -f $name, $info.Version, $componentRoot, $flag, $state)
        if ($problem) { $problems += $problem }
    }
    if ($WhatIfPreference) { $componentLines | ForEach-Object { Write-Host $_ } }
    if ($problems.Count -gt 0) { throw ($problems -join "`n") }

    # --- tag name ---------------------------------------------------------------------
    # Standalone repository: v1.2.3. Folder inside a monorepo: <folder>-v1.2.3, so the tags
    # of different projects in one repository do not collide.
    $gitRoot = [System.IO.Path]::GetFullPath((git rev-parse --show-toplevel)).TrimEnd('\', '/')
    $tag = if ($gitRoot -eq $here) { "v$Version" } else { (Split-Path $here -Leaf).ToLowerInvariant() + "-v$Version" }
    if (git tag --list $tag) { throw "Tag $tag already exists." }

    # -WhatIf stops here: the checks passed, nothing was changed.
    $floorText = if ($floor) { $floor } else { 'none' }
    Write-Host ("== release {0} (<Version> {1}, newest released {2}), tag {3}" -f $new, $current, $floorText, $tag) -ForegroundColor Cyan
    if ($WhatIfPreference) {
        $targets = ($kit.release.publish | ForEach-Object { $_.project }) -join ', '
        $propsPlan = if ($new -eq $current) { "leave <Version> at $current" } else { "set <Version> to $new" }
        $zipPlan = if ($kit.release.zip) { ', zip ' + $kit.release.zip.Replace('{version}', $Version) } else { '' }
        Write-Host ("WhatIf: would run verify, {0}, close [Unreleased] in CHANGELOG, publish {1}, write release.json{2}." -f $propsPlan, $targets, $zipPlan)
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
    # artifacts/ is git-ignored.
    if (-not $NoPublish) { Publish-Release -AfterVersionEdits }

    Write-Host ("== ready. Propose: git commit -m ""Release {0}"" -- Directory.Build.props CHANGELOG.md, then git tag -a {1} -m ""Release {0}"". Nothing is committed or tagged by this script." -f $Version, $tag) -ForegroundColor Green
    exit 0
}
finally {
    Pop-Location
}
