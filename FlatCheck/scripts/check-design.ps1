# SPDX-License-Identifier: GPL-3.0-or-later
# check-design.ps1 - checks the design notes in docs/design/: their index and their Covers paths.
#
# WHAT IT DOES
#   A design note says how the project's code works and how its parts fit (the kit's rule
#   "Design notes", the kit's ADR 0031). Every .md in docs/design/ except README.md is a note.
#   The script checks, letter case included:
#     - the index: every note is named in docs/design/README.md as `name.md`, and every such
#       name there is a note that exists (fenced blocks are not read);
#     - the Covers sentence: every note has a "Covers:" marker, bold or plain, in a quote block
#       or not, outside inline code, above its first section (the first heading of level 2 or
#       more; a note without one counts whole);
#     - the paths: every code span of every Covers sentence that looks like a path names a file
#       or a folder of this project.
#   A Covers sentence runs from the marker to the first "." outside backticks that a space or a
#   line end follows, or to a blank line or a heading. A span looks like a path when it holds no
#   space, "<", ">", "{", "}", "\" or ":", does not start with "-", and has a "/" or ends in a
#   known extension (cs, csproj, props, targets, sln, xaml, json, md, py, ps1, txt, yml, yaml,
#   xml, ico, png); other spans (a class, a method, a test, a flag) are not checked. A path may
#   start at the project root, at a code or test folder of kit.json, or at docs/design/; "*" and
#   "?" match within one name; a name without "/" may also be a file found anywhere in the
#   project. Build output, .git, .vs and worktrees are never searched.
#
#   With -Since, nothing is checked: the script lists the notes whose Covers sentence names a
#   file changed since the merge base of <ref> and HEAD (committed, staged, unstaged, deleted,
#   either side of a rename, or untracked), among the code and test folders and files of
#   kit.json. /wrap-up runs it to find the notes a task must read again.
#
# WHO CALLS IT
#   - scripts/verify.ps1, before the build; so the git pre-commit hook, Claude's commit hook
#     and CI all run it
#   - /wrap-up, with -Since main
#   - you:
#       powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-design.ps1 [-Since main]
#
# OPTIONS
#   -Since <ref>   list the notes that cover a file changed since <ref>, and check nothing
#
# RESULT
#   exit 0  the index and every Covers path are right, or the list was printed
#   exit 1  one red line per problem: INDEX, COVERS or MISSING, with the note and the span;
#           with -Since, a GIT line when git cannot answer (no repository, an unknown ref)
[CmdletBinding()]
param([string]$Since = '')

$ErrorActionPreference = 'Stop'
# The project folder is the parent of scripts/.
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'kit-config.ps1')
$kit = Read-KitConfig $root
$dir = Join-Path $root 'docs\design'
if (-not (Test-Path -LiteralPath $dir)) {
    Write-Host 'design notes: no docs/design/ folder, nothing to check' -ForegroundColor Yellow
    exit 0
}

# The folders a Covers path may start at, relative and ending in "/"; '' is the project root.
$bases = @('') + @($kit.codeFolders) + @($kit.testFolders) + @('docs/design/')
$extensions = 'cs|csproj|props|targets|sln|xaml|json|md|py|ps1|txt|yml|yaml|xml|ico|png'
# Folders that are never searched: build output, tool state and other checkouts.
$skip = @('bin', 'obj', 'StrykerOutput', 'artifacts', 'TestResults', '.vs', '.git', 'worktrees')

# Every file and folder of the project, relative, with "/" (a folder ends in "/"), compared
# with their exact letter case so that Windows and Linux agree; and every file by its name.
$paths = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)
$byName = New-Object 'System.Collections.Generic.Dictionary[string,int]' ([System.StringComparer]::Ordinal)
$pending = New-Object System.Collections.Generic.Stack[string]
$pending.Push('')
while ($pending.Count -gt 0) {
    $rel = $pending.Pop()
    $full = if ($rel) { Join-Path $root $rel } else { $root }
    foreach ($d in [System.IO.Directory]::GetDirectories($full)) {
        $name = [System.IO.Path]::GetFileName($d)
        if ($skip -contains $name) { continue }
        [void]$paths.Add($rel + $name + '/')
        $pending.Push($rel + $name + '/')
    }
    foreach ($f in [System.IO.Directory]::GetFiles($full)) {
        $name = [System.IO.Path]::GetFileName($f)
        [void]$paths.Add($rel + $name)
        $byName[$name] = 1
    }
}

$problems = New-Object System.Collections.Generic.List[string]
function Add-Problem([string]$Kind, [string]$File, [string]$Text) {
    $problems.Add(("{0,-11}{1}: {2}" -f $Kind, $File, $Text))
}

# A note's text as the parser reads it: UTF-8 (Windows PowerShell 5.1 would read a file without
# a byte order mark in the ANSI code page), LF, fenced blocks emptied and the quote markers at
# the start of a line removed.
function Read-Text([string]$Path) {
    $text = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8) -replace "`r", ''
    $text = [regex]::Replace($text, '(?ms)^[ \t]*```.*?^[ \t]*```[ \t]*$', '')
    return [regex]::Replace($text, '(?m)^[ \t]*>[ \t]?', '')
}

# The same text with the inside of every inline code span blanked, so that a marker or a "."
# in code is not seen; the indexes stay those of the text.
$blank = [System.Text.RegularExpressions.MatchEvaluator] { param($m) '`' + (' ' * ($m.Length - 2)) + '`' }
function Get-Masked([string]$Text) { return [regex]::Replace($Text, '`[^`\n]*`', $blank) }

# Where a Covers sentence that starts at $Start ends in the masked text: at a "." followed by
# a space or a line end, or before a blank line or a heading.
function Get-SentenceEnd([string]$Masked, [int]$Start) {
    for ($i = $Start; $i -lt $Masked.Length; $i++) {
        $c = $Masked[$i]
        if ($c -eq '.' -and ($i + 1 -ge $Masked.Length -or [char]::IsWhiteSpace($Masked[$i + 1]))) { return $i }
        if ($c -eq "`n" -and $i + 1 -lt $Masked.Length -and ($Masked[$i + 1] -eq "`n" -or $Masked[$i + 1] -eq '#')) { return $i }
    }
    return $Masked.Length
}

# The code spans of a note's Covers sentences that look like paths, and whether the note has a
# Covers marker above its first section.
function Get-Covers([string]$Path) {
    $text = Read-Text $Path
    $masked = Get-Masked $text
    $section = [regex]::Match($masked, '(?m)^#{2,6}[ \t]')
    $top = if ($section.Success) { $section.Index } else { $masked.Length }
    $spans = New-Object System.Collections.Generic.List[string]
    $onTop = $false
    foreach ($m in [regex]::Matches($masked, '(?:\*\*)?Covers:(?:\*\*)?')) {
        if ($m.Index -lt $top) { $onTop = $true }
        $start = $m.Index + $m.Length
        $sentence = $text.Substring($start, (Get-SentenceEnd $masked $start) - $start)
        foreach ($s in [regex]::Matches($sentence, '`([^`\n]+)`')) {
            $span = $s.Groups[1].Value
            if ($span -match '[\s<>{}\\:]' -or $span.StartsWith('-')) { continue }
            if ($span.Contains('/') -or $span -match "\.($extensions)$") { $spans.Add($span) }
        }
    }
    return @{ OnTop = $onTop; Spans = $spans }
}

# Whether one of the bases makes $Span a file or a folder of the project.
function Test-AtBase([string]$Span) {
    foreach ($b in $bases) {
        $c = $b + $Span
        if ($Span -match '[*?]') { foreach ($p in $paths) { if ($p -clike $c) { return $true } } }
        elseif ($paths.Contains($c) -or $paths.Contains($c.TrimEnd('/') + '/')) { return $true }
    }
    return $false
}

$notes = @(Get-ChildItem -LiteralPath $dir -Filter '*.md' -File | Where-Object { $_.Name -cne 'README.md' } | Sort-Object Name)
$covers = @{}
foreach ($n in $notes) { $covers[$n.Name] = Get-Covers $n.FullName }

if ($Since) {
    # git's output lines; 'Continue' because under 'Stop' Windows PowerShell turns a native
    # command's stderr into a terminating error even when it is sent to $null.
    function Invoke-Git {
        $ErrorActionPreference = 'Continue'
        $out = @(& git -c core.quotepath=off @args 2>$null)
        $script:gitFailed = $LASTEXITCODE -ne 0
        return $out
    }
    # File names come back as UTF-8; the console's own code page would garble a non-ASCII one.
    $encoding = $null
    try { $encoding = [Console]::OutputEncoding; [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false } catch { $encoding = $null }
    Push-Location $root
    try {
        $base = @(Invoke-Git merge-base $Since HEAD)
        if ($script:gitFailed -or $base.Count -eq 0) {
            Write-Host ("GIT        cannot find where '{0}' and HEAD meet in {1}" -f $Since, $root) -ForegroundColor Red
            exit 1
        }
        # Relative to the project folder, as kit.json's folders are: both sides of a rename,
        # deleted files, and the working tree's changes, committed or not.
        $changed = @(Invoke-Git diff --name-only --relative --no-renames $base[0])
        if (-not $script:gitFailed) { $changed += @(Invoke-Git ls-files --others --exclude-standard) }
        if ($script:gitFailed) {
            Write-Host ("GIT        cannot list the files changed since '{0}' in {1}" -f $Since, $root) -ForegroundColor Red
            exit 1
        }
    }
    finally {
        Pop-Location
        if ($encoding) { try { [Console]::OutputEncoding = $encoding } catch { } }
    }

    $code = @($kit.codeFolders) + @($kit.testFolders)
    $inScope = @($changed | Where-Object {
        $file = $_
        ($kit.codeFiles -ccontains $file) -or @($code | Where-Object { $file.StartsWith($_, [System.StringComparison]::Ordinal) }).Count -gt 0
    } | Sort-Object -Unique)

    $listed = 0
    foreach ($n in $notes) {
        $hits = New-Object System.Collections.Generic.List[string]
        foreach ($file in $inScope) {
            $name = [System.IO.Path]::GetFileName($file)
            foreach ($span in $covers[$n.Name].Spans) {
                $match = $false
                foreach ($b in $bases) {
                    $c = $b + $span
                    if ($span -match '[*?]') { $match = $file -clike $c }
                    else { $match = ($file -ceq $c) -or $file.StartsWith($c.TrimEnd('/') + '/', [System.StringComparison]::Ordinal) }
                    if ($match) { break }
                }
                # A bare file name that no base resolves is a file found somewhere in the project.
                if (-not $match -and -not $span.Contains('/') -and -not (Test-AtBase $span)) { $match = $name -ceq $span }
                if ($match) { if (-not $hits.Contains($file)) { $hits.Add($file) }; break }
            }
        }
        if ($hits.Count -gt 0) {
            Write-Host ("touched  docs/design/{0}: {1}" -f $n.Name, ($hits -join ', '))
            $listed++
        }
    }
    if ($listed -eq 0) { Write-Host 'ok       no design note covers a changed file' }
    exit 0
}

# The index: every note named in README.md as `name.md`, and every such name a note.
$readme = Join-Path $dir 'README.md'
if (-not (Test-Path -LiteralPath $readme)) {
    Add-Problem 'INDEX' 'docs/design/README.md' 'missing: it is the index of the notes'
}
else {
    $named = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::Ordinal)
    foreach ($m in [regex]::Matches((Read-Text $readme), '`([a-z0-9-]+\.md)`')) { [void]$named.Add($m.Groups[1].Value) }
    foreach ($n in $notes) {
        if (-not $named.Contains($n.Name)) { Add-Problem 'INDEX' "docs/design/$($n.Name)" 'not named in docs/design/README.md' }
    }
    foreach ($name in ($named | Sort-Object)) {
        if (-not ($notes | Where-Object { $_.Name -ceq $name })) {
            Add-Problem 'INDEX' 'docs/design/README.md' "names ``$name``, which is not a note in docs/design/"
        }
    }
}

foreach ($n in $notes) {
    $c = $covers[$n.Name]
    if (-not $c.OnTop) { Add-Problem 'COVERS' "docs/design/$($n.Name)" "no 'Covers:' sentence above its first section" }
    foreach ($span in $c.Spans) {
        if (Test-AtBase $span) { continue }
        if (-not $span.Contains('/') -and $span -notmatch '[*?]' -and $byName.ContainsKey($span)) { continue }
        Add-Problem 'MISSING' "docs/design/$($n.Name)" "Covers names ``$span``, which is no file or folder of this project"
    }
}

foreach ($p in $problems) { Write-Host $p -ForegroundColor Red }
if ($problems.Count -gt 0) { exit 1 }
Write-Host ("ok       {0} design notes in docs/design/" -f $notes.Count)
exit 0
