# SPDX-License-Identifier: GPL-3.0-or-later
# rename.ps1 - gives a fresh copy of the kit its project name.
#
# WHAT IT DOES
#   The kit's projects carry the placeholder name "Starter". This script replaces that word,
#   case-sensitively, everywhere in the copy, with the name you give:
#     - inside files: Starter -> MyTool, starter -> mytool, STARTER -> MYTOOL;
#     - in file and folder names: Starter -> MyTool (Starter.sln -> MyTool.sln, ...).
#   The kit's own name, ClaudeCodeStarterKit, is never touched: CLAUDE.md points at that
#   folder for the guide, and the pointer must still work after the rename.
#   It skips bin/, obj/, .git/, .vs/ and this script. Then it runs dotnet format once: the
#   new name changes the alphabetical order of the using lines, and the gate checks that order.
#
# WHO CALLS IT
#   Only you, once, right after copying the kit into a new project folder and before the
#   first commit (README.md, step 2 of "Pornirea unui proiect nou"):
#       powershell -NoProfile -ExecutionPolicy Bypass -File scripts/rename.ps1 -Name MyTool
#   Nothing runs it automatically. Never run it in the kit itself: it would rename the kit.
#
# OPTIONS
#   -Name   the project name, PascalCase letters and digits (e.g. FlatCheck); required
#
# RESULT
#   exit 0  done; it lists every file it changed and every file or folder it renamed
#   exit 1  the name is not PascalCase, or dotnet format failed afterwards
[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$Name)

$ErrorActionPreference = 'Stop'
# PascalCase: a capital letter, then letters and digits only; the name becomes part of
# namespaces and file names.
if ($Name -notmatch '^[A-Z][A-Za-z0-9]*$') { throw 'Name must be PascalCase letters and digits, e.g. FlatCheck.' }
# The project folder is the parent of scripts/.
$root = Split-Path -Parent $PSScriptRoot
$lower = $Name.ToLowerInvariant()
$upper = $Name.ToUpperInvariant()

# Build output, git's own folder and Visual Studio's folder are left alone.
$skip = '\\(bin|obj|\.git|\.vs)(\\|$)'
$files = Get-ChildItem $root -Recurse -File | Where-Object { $_.FullName -notmatch $skip -and $_.Name -ne 'rename.ps1' }

# 1. File contents. -creplace is the case-sensitive replace. A file is written back (as
#    UTF-8 without a byte order mark) only when something in it changed. The kit's own
#    name, ClaudeCodeStarterKit, is left alone: (?<!ClaudeCode) and (?!Kit) skip a "Starter"
#    that sits between those two words, so CLAUDE.md keeps pointing at the kit folder.
foreach ($f in $files) {
    $text = [System.IO.File]::ReadAllText($f.FullName)
    $new = $text -creplace '(?<!ClaudeCode)Starter(?!Kit)', $Name -creplace 'starter', $lower -creplace 'STARTER', $upper
    if ($new -ne $text) {
        [System.IO.File]::WriteAllText($f.FullName, $new, (New-Object System.Text.UTF8Encoding $false))
        Write-Host ("content  {0}" -f $f.FullName.Substring($root.Length + 1))
    }
}

# 2. File and folder names. Deepest paths first so a renamed directory does not invalidate
#    a child's path.
$items = Get-ChildItem $root -Recurse | Where-Object { $_.FullName -notmatch $skip -and $_.Name -cmatch 'Starter' } |
    Sort-Object { $_.FullName.Length } -Descending
foreach ($i in $items) {
    $newName = $i.Name -creplace 'Starter', $Name
    Rename-Item $i.FullName $newName
    Write-Host ("renamed  {0} -> {1}" -f $i.Name, $newName)
}

# 3. The new name changes the alphabetical order of `using` lines; let the formatter re-sort
#    them so the first verify run is green.
Write-Host 'formatting (using order depends on the project name)'
Push-Location $root
try {
    dotnet format (Join-Path $root ("{0}.sln" -f $Name)) -v q
    if ($LASTEXITCODE -ne 0) { throw 'dotnet format failed after the rename; run scripts/verify.ps1 to see why.' }
}
finally {
    Pop-Location
}

Write-Host ("Done. Review CLAUDE.md and README.md, then: git add -A; git commit -m 'Start {0} from ClaudeCodeStarterKit'" -f $Name)
