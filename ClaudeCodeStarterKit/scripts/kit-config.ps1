# SPDX-License-Identifier: GPL-3.0-or-later
# kit-config.ps1 - reads kit.json, the one file that makes the shared scripts fit a project.
#
# WHAT IT DOES
#   Every script in scripts/ is the same in the kit and in every project made from it; what
#   differs per project (the solution name, where the code and the tests are, what a release
#   publishes) lives in kit.json at the project root, never in a script. This file is
#   dot-sourced by the scripts (". (Join-Path $PSScriptRoot 'kit-config.ps1')") and gives them
#   Read-KitConfig, which reads <project folder>/kit.json and returns its values with the paths
#   already normalised (forward slashes, one trailing slash on folders), and Find-KitProject,
#   which finds the project a file or folder belongs to (the Claude Code hooks use it, ADR 0020).
#   A missing kit.json, or a missing required key, is an error with the key named: a project
#   that has scripts/ must say what it is; nothing is guessed from the folder layout.
#
#   Keys (the kit's own kit.json is the example to copy):
#     solution            the .sln file verify.ps1 restores, formats, builds and tests
#     testFilter          optional; a `dotnet test --filter` expression the gate applies
#                         ("" runs everything)
#     codeFolders         folders whose change needs a green verify before a Claude turn
#                         ends (claude-stop-gate.ps1)
#     codeFiles           optional; single files that count as code, the same way
#     testFolders         folders whose change needs a green verify before a turn ends
#     testData.folder     the folder holding manifest.json, fixtures/ and large/
#     testData.largeEnv   the environment variable that may point at the large files instead
#     mutate.testProject  the test project folder Stryker runs from (its stryker-config.json)
#     release.publish     what release.ps1 publishes: a list of { project, output, args };
#                         output is a subfolder of artifacts/<version>/ ("" is that folder
#                         itself), args are extra `dotnet publish` arguments
#     audit.alsoRead      optional; files or folders, besides docs/ and the test data README
#                         and manifest, that the docs-auditor agent may read: expected outputs,
#                         notices. Never code.
#     kit                 optional; the kit folder, relative to the project ("." in the kit
#                         itself). sync.ps1 copies scripts/, .claude/skills/ and
#                         .claude/agents/ from there, and verify.ps1 checks that they are
#                         identical. Absent: no check, sync by hand.
#
# WHO CALLS IT
#   Every script in scripts/ and scripts/hooks/ that needs a project value. Nobody runs it.
#
# RESULT
#   Read-KitConfig returns the object; it throws when kit.json or a required key is missing.
#   Find-KitProject returns a full path, or $null.

# Reads and checks kit.json in $ProjectDir. Throws with the file or key named.
function Read-KitConfig([string]$ProjectDir) {
    $path = Join-Path $ProjectDir 'kit.json'
    if (-not (Test-Path -LiteralPath $path)) {
        throw "kit.json not found in $ProjectDir. The shared scripts need it; copy the kit's and set the values for this project."
    }
    $cfg = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json

    # A required key that is absent is a mistake, not a default: say which one.
    foreach ($key in @('solution', 'codeFolders', 'testFolders', 'testData', 'mutate', 'release')) {
        if ($null -eq $cfg.PSObject.Properties[$key]) { throw "kit.json: missing key '$key'." }
    }
    if (-not $cfg.testData.folder) { throw "kit.json: missing key 'testData.folder'." }
    if (-not $cfg.testData.largeEnv) { throw "kit.json: missing key 'testData.largeEnv'." }
    if (-not $cfg.mutate.testProject) { throw "kit.json: missing key 'mutate.testProject'." }
    if ($null -eq $cfg.release.PSObject.Properties['publish']) { throw "kit.json: missing key 'release.publish'." }

    # Optional keys get their empty value, so a script never tests for their presence.
    if ($null -eq $cfg.PSObject.Properties['testFilter']) { $cfg | Add-Member -NotePropertyName testFilter -NotePropertyValue '' }
    if ($null -eq $cfg.PSObject.Properties['codeFiles']) { $cfg | Add-Member -NotePropertyName codeFiles -NotePropertyValue @() }
    if ($null -eq $cfg.PSObject.Properties['kit']) { $cfg | Add-Member -NotePropertyName kit -NotePropertyValue '' }
    if ($null -eq $cfg.PSObject.Properties['audit']) { $cfg | Add-Member -NotePropertyName audit -NotePropertyValue ([pscustomobject]@{ alsoRead = @() }) }
    if ($null -eq $cfg.audit.PSObject.Properties['alsoRead']) { $cfg.audit | Add-Member -NotePropertyName alsoRead -NotePropertyValue @() }
    $cfg.audit.alsoRead = @($cfg.audit.alsoRead | ForEach-Object { $_.Replace('\', '/') })

    # Folders: forward slashes and exactly one trailing slash, whatever the file says. @( )
    # keeps a one-element list a list; ConvertFrom-Json would hand a single string back.
    $cfg.codeFolders = @($cfg.codeFolders | ForEach-Object { ConvertTo-KitFolder $_ })
    $cfg.testFolders = @($cfg.testFolders | ForEach-Object { ConvertTo-KitFolder $_ })
    $cfg.codeFiles = @($cfg.codeFiles | ForEach-Object { $_.Replace('\', '/') })
    $cfg.testData.folder = ConvertTo-KitFolder $cfg.testData.folder
    $cfg.mutate.testProject = ConvertTo-KitFolder $cfg.mutate.testProject
    $cfg.release.publish = @($cfg.release.publish)
    foreach ($p in $cfg.release.publish) {
        if (-not $p.project) { throw "kit.json: every release.publish entry needs 'project'." }
        if ($null -eq $p.PSObject.Properties['output']) { $p | Add-Member -NotePropertyName output -NotePropertyValue '' }
        if ($null -eq $p.PSObject.Properties['args']) { $p | Add-Member -NotePropertyName args -NotePropertyValue @() }
        $p.args = @($p.args)
    }
    return $cfg
}

# "src\" or "src" or "src/" -> "src/".
function ConvertTo-KitFolder([string]$Folder) {
    return $Folder.Replace('\', '/').TrimEnd('/') + '/'
}

# The kit folder named in kit.json, as a full path, or $null when kit.json names none.
function Get-KitPath($Config, [string]$ProjectDir) {
    if (-not $Config.kit) { return $null }
    return [System.IO.Path]::GetFullPath((Join-Path $ProjectDir $Config.kit)).TrimEnd('\', '/')
}

# The project $Path (a file or a folder, existing or not) belongs to: the nearest folder at or
# above it that holds $Marker (kit.json unless told otherwise; the commit gate asks for
# scripts\verify.ps1, the git pre-commit hook's rule), as a full path without a trailing
# separator; $null when there is none. The walk stops at the top of the repository or worktree
# the path is in (a folder holding .git), as pre-commit does, so a path in a worktree made under
# a project's .claude/worktrees/ is never taken for a path of that project.
function Find-KitProject([string]$Path, [string]$Marker = 'kit.json') {
    if (-not $Path) { return $null }
    $dir = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $dir -PathType Container)) { $dir = [System.IO.Path]::GetDirectoryName($dir) }
    while ($dir) {
        if (Test-Path -LiteralPath (Join-Path $dir $Marker) -PathType Leaf) { return $dir.TrimEnd('\', '/') }
        if (Test-Path -LiteralPath (Join-Path $dir '.git')) { return $null }
        $dir = [System.IO.Path]::GetDirectoryName($dir)
    }
    return $null
}

# Full path of a project folder from a kit.json value ("src/" -> <project>\src\).
function Get-KitFolderPath([string]$ProjectDir, [string]$Folder) {
    return (Join-Path $ProjectDir ($Folder.Replace('/', '\'))).TrimEnd('\') + '\'
}
