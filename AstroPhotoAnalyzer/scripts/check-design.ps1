# SPDX-License-Identifier: GPL-3.0-or-later
# check-design.ps1 - checks the design notes in docs/design/: their index and their Covers paths.
#
# WHAT IT DOES
#   A design note says how the project's code works and how its parts fit (the kit's rule
#   "Design notes", the kit's ADR 0031). Every .md in docs/design/ except README.md is a note.
#   The script checks, letter case included:
#     - the index: every note is named in docs/design/README.md as `name.md`, and every
#       `name.md` in a row of a table there (a line that starts with "|") is a note, unless it
#       names another file of the project;
#     - the Covers sentences: every note has one above its first section (the first heading
#       of level 2 or more; a note without one counts whole), and every Covers sentence,
#       wherever it is, names at least one file or folder as a path;
#     - the paths: every code span of every Covers sentence that looks like a path names a file
#       or a folder of this project that git tracks, or would add, and that exists. Ignored
#       files, such as build output, do not count.
#   A Covers sentence starts at the marker "Covers:", plain or emphasised ("**Covers:**" or
#   "**Covers**:"), in a quote block or not, outside code, HTML comments and headings. It runs
#   to the first "." outside code that a space or a line end follows, except the one of "e.g.",
#   "i.e.", "vs.", "etc.", "cf." or a list number, or to a blank line or a heading. Fenced
#   blocks (``` or ~~~, quoted or not) are not read; a code span may wrap across lines. A span
#   looks like a path when it holds no white space, "<", ">", "{", "}", "\" or ":", does not
#   start with "-", and has a "/" or ends in a known extension written in lower case (cs,
#   csproj, props, targets, sln, xaml, json, md, py, ps1, txt, yml, yaml, xml, ico, png); other
#   spans (a class, a method, a test, a flag) are not checked. A path may start at the project
#   root, at a code or test folder of kit.json, or at docs/design/; "*" and "?" match within one
#   name, and nothing else is a wildcard; a name without "/" may also be a file found anywhere
#   in the project.
#
#   With -Since, nothing is checked: the script lists the notes whose Covers sentences name a
#   code or test file (the folders and files kit.json lists) changed since the merge base of
#   <ref> and HEAD: committed, staged, unstaged, deleted, either side of a rename, or untracked.
#   /wrap-up runs it in the task's worktree to find the notes a task must read again. Where HEAD
#   is on <ref> itself, as in the main checkout, there is nothing to compare: that is an error.
#
# WHO CALLS IT
#   - scripts/verify.ps1, before the build; so the git pre-commit hook, Claude's commit hook
#     and CI all run it
#   - /wrap-up, with -Since main, in the task's worktree
#   - you:
#       powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-design.ps1 [-Since main]
#
# OPTIONS
#   -Since <ref>   list the notes that cover a code or test file changed since <ref>, and check
#                  nothing
#
# RESULT
#   exit 0  the index and every Covers sentence are right, or the list was printed
#   exit 1  one red line per problem: INDEX, COVERS or MISSING, with the note and the span; a GIT
#           line when git cannot answer (no repository, an unknown ref, HEAD on the ref itself)
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
$ordinal = [System.StringComparer]::Ordinal

# git's output lines, read as UTF-8; $script:gitFailed says whether git could answer. git is
# started directly: through PowerShell its output would be decoded in the console's code page.
function Invoke-Git([string[]]$Arguments) {
    $start = New-Object System.Diagnostics.ProcessStartInfo 'git'
    $start.Arguments = (@('-C', $root, '-c', 'core.quotepath=off') + $Arguments |
        ForEach-Object { '"' + $_.Replace('"', '\"') + '"' }) -join ' '
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.CreateNoWindow = $true
    $start.StandardOutputEncoding = New-Object System.Text.UTF8Encoding $false
    try {
        $process = [System.Diagnostics.Process]::Start($start)
        $errors = $process.StandardError.ReadToEndAsync()
        $out = $process.StandardOutput.ReadToEnd()
        $process.WaitForExit()
        [void]$errors.Result
        $script:gitFailed = $process.ExitCode -ne 0
    }
    catch {
        $script:gitFailed = $true
        return @()
    }
    return @($out.Split("`n") | ForEach-Object { $_.TrimEnd("`r") } | Where-Object { $_ })
}

$problems = New-Object System.Collections.Generic.List[string]
function Add-Problem([string]$Kind, [string]$File, [string]$Text) {
    $problems.Add(("{0,-11}{1}: {2}" -f $Kind, $File, $Text))
}

# A note's text as the checks read it: UTF-8 (Windows PowerShell 5.1 would read a file without
# a byte order mark in the ANSI code page), LF, HTML comments, fenced blocks and lines of white
# space emptied, and the quote markers at the start of a line removed; lines keep their places.
$noComment = [System.Text.RegularExpressions.MatchEvaluator] { param($m) $m.Value -replace '[^\n]', '' }
function Read-Text([string]$Path) {
    $text = [System.IO.File]::ReadAllText($Path, [System.Text.Encoding]::UTF8) -replace "`r", ''
    $lines = [regex]::Replace($text, '(?s)<!--.*?-->', $noComment).Split("`n")
    $fence = ''
    for ($i = 0; $i -lt $lines.Length; $i++) {
        $line = $lines[$i] -replace '^[ \t]*(?:>[ \t]?)+', ''
        if ($fence) {
            # A fence closes on a line of the same character, at least as long, and nothing else.
            if ($line -match '^[ \t]{0,3}(`+|~+)[ \t]*$' -and $Matches[1][0] -eq $fence[0] -and $Matches[1].Length -ge $fence.Length) { $fence = '' }
            $line = ''
        }
        elseif ($line -match '^[ \t]{0,3}(`{3,}|~{3,})(.*)$' -and -not ($Matches[1][0] -eq '`' -and $Matches[2].Contains('`'))) {
            $fence = $Matches[1]
            $line = ''
        }
        elseif ($line -match '^[ \t]+$') { $line = '' }
        $lines[$i] = $line
    }
    return $lines -join "`n"
}

# The inline code spans of a text, as CommonMark reads them: a run of backticks opens one and the
# next run of the same length closes it, across a line break but not a blank line. Each is
# Start and End (the backticks included) and Value: the content, line breaks as spaces, without
# the one space at each end when both are there.
function Get-CodeSpans([string]$Text) {
    $runs = [regex]::Matches($Text, '`+')
    $spans = New-Object System.Collections.Generic.List[object]
    $i = 0
    while ($i -lt $runs.Count) {
        $open = $runs[$i]
        $j = $i + 1
        while ($j -lt $runs.Count -and $runs[$j].Length -ne $open.Length) { $j++ }
        if ($j -lt $runs.Count) {
            $start = $open.Index + $open.Length
            $inner = $Text.Substring($start, $runs[$j].Index - $start)
            if (-not $inner.Contains("`n`n")) {
                $value = $inner.Replace("`n", ' ')
                if ($value.Length -ge 2 -and $value.StartsWith(' ') -and $value.EndsWith(' ') -and $value.Trim()) {
                    $value = $value.Substring(1, $value.Length - 2)
                }
                $spans.Add(@{ Start = $open.Index; End = $runs[$j].Index + $runs[$j].Length; Value = $value })
                $i = $j + 1
                continue
            }
        }
        $i++
    }
    return , $spans
}

# The same text with every code span, backticks included, turned into backticks, so that a
# marker, a "." or a line break in code is not seen; the indexes stay those of the text.
function Get-Masked([string]$Text, $Spans) {
    $masked = New-Object System.Text.StringBuilder ($Text.Length)
    $at = 0
    foreach ($s in $Spans) {
        [void]$masked.Append($Text, $at, $s.Start - $at).Append([char]96, $s.End - $s.Start)
        $at = $s.End
    }
    return $masked.Append($Text, $at, $Text.Length - $at).ToString()
}

# Where a Covers sentence that starts at $Start ends in the masked text: at a "." followed by
# white space or the end, unless it closes an abbreviation or a list number, or before a blank
# line or a heading.
$sentenceStop = New-Object System.Text.RegularExpressions.Regex '\.(?=\s|$)|\n(?=\n|[ \t]{0,3}#{1,6}(?:[ \t]|$))'
$abbreviations = @('e.g', 'i.e', 'vs', 'etc', 'cf')
function Get-SentenceEnd([string]$Masked, [int]$Start) {
    $m = $sentenceStop.Match($Masked, $Start)
    while ($m.Success) {
        if ($Masked[$m.Index] -ne '.') { return $m.Index }
        $line = $Masked.LastIndexOf([char]10, [Math]::Max($m.Index - 1, 0)) + 1
        $before = $Masked.Substring($line, $m.Index - $line)
        $word = ($before -split '\s')[-1].TrimStart('(', '[', '"', "'").ToLowerInvariant()
        if ($before -notmatch '^[ \t]*\d+$' -and $abbreviations -notcontains $word) { return $m.Index }
        $m = $m.NextMatch()
    }
    return $Masked.Length
}

function Test-PathLike([string]$Span) {
    if ($Span -match '[\s<>{}\\:]' -or $Span.StartsWith('-') -or $Span.Trim('/', '.') -eq '') { return $false }
    return $Span.Contains('/') -or $Span -cmatch "\.($extensions)$"
}

# A note's Covers sentences: whether one is above its first section, the path-like spans of each,
# and all of those spans once.
$marker = New-Object System.Text.RegularExpressions.Regex '(?<![\w*_])[*_]{0,2}Covers(?::[*_]{0,2}|[*_]{1,2}:)'
function Get-Covers([string]$Path) {
    $text = Read-Text $Path
    $spans = Get-CodeSpans $text
    $masked = Get-Masked $text $spans
    $section = [regex]::Match($masked, '(?m)^#{2,6}[ \t]')
    $top = if ($section.Success) { $section.Index } else { $masked.Length }
    $onTop = $false
    $sentences = New-Object System.Collections.Generic.List[object]
    $all = New-Object System.Collections.Generic.List[string]
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ($ordinal)
    foreach ($m in $marker.Matches($masked)) {
        $line = $masked.LastIndexOf([char]10, [Math]::Max($m.Index - 1, 0)) + 1
        if ($masked.Substring($line, $m.Index - $line) -match '^[ \t]{0,3}#') { continue }
        if ($m.Index -lt $top) { $onTop = $true }
        $start = $m.Index + $m.Length
        $end = Get-SentenceEnd $masked $start
        $paths = New-Object System.Collections.Generic.List[string]
        foreach ($s in $spans) {
            if ($s.Start -ge $start -and $s.Start -lt $end -and (Test-PathLike $s.Value)) {
                $paths.Add($s.Value)
                if ($seen.Add($s.Value)) { $all.Add($s.Value) }
            }
        }
        $sentences.Add($paths)
    }
    return @{ OnTop = $onTop; Sentences = $sentences; Spans = $all }
}

# A span with "*" or "?" as a regular expression over relative paths: each matches within one
# name; everything else, "[" and "]" included, stands for itself.
function Get-GlobRegex([string]$Glob) {
    $pattern = [regex]::Escape($Glob).Replace('\*', '[^/]*').Replace('\?', '[^/]')
    return New-Object System.Text.RegularExpressions.Regex ('^' + $pattern + '$')
}

$names = @([System.IO.Directory]::GetFiles($dir) | ForEach-Object { [System.IO.Path]::GetFileName($_) })
$notes = [string[]]@($names | Where-Object { $_ -cmatch '\.md$' -and $_ -cne 'README.md' })
[Array]::Sort($notes, $ordinal)
$covers = New-Object 'System.Collections.Generic.Dictionary[string,object]' ($ordinal)
foreach ($n in $notes) { $covers[$n] = Get-Covers (Join-Path $dir $n) }

# Every file of the project as git sees it (tracked, or untracked and not ignored) that exists,
# relative with "/"; every folder that holds one, ending in "/"; and every file name.
$paths = New-Object 'System.Collections.Generic.HashSet[string]' ($ordinal)
$fileNames = New-Object 'System.Collections.Generic.HashSet[string]' ($ordinal)
$files = @(Invoke-Git @('ls-files', '--cached', '--others', '--exclude-standard'))
if ($script:gitFailed) {
    Write-Host ("GIT        cannot list the files of {0}: the check reads them from git" -f $root) -ForegroundColor Red
    exit 1
}
foreach ($f in $files) {
    if ($f.EndsWith('/') -or -not [System.IO.File]::Exists([System.IO.Path]::Combine($root, $f))) { continue }
    [void]$paths.Add($f)
    [void]$fileNames.Add($f.Substring($f.LastIndexOf('/') + 1))
    $i = $f.LastIndexOf('/')
    while ($i -gt 0) {
        [void]$paths.Add($f.Substring(0, $i + 1))
        $i = $f.LastIndexOf('/', $i - 1)
    }
}

# Whether a span names something at one of the bases, and whether it does at all (a name without
# "/" may be a file found anywhere). Kept, since -Since asks once per changed file.
$resolved = New-Object 'System.Collections.Generic.Dictionary[string,object]' ($ordinal)
function Resolve-Span([string]$Span) {
    if ($resolved.ContainsKey($Span)) { return $resolved[$Span] }
    $glob = $Span -match '[*?]'
    $atBase = $false
    foreach ($b in $bases) {
        $c = $b + $Span
        if ($glob) {
            $re = Get-GlobRegex $c
            foreach ($p in $paths) { if ($re.IsMatch($p)) { $atBase = $true; break } }
        }
        elseif ($paths.Contains($c) -or $paths.Contains($c.TrimEnd('/') + '/')) { $atBase = $true }
        if ($atBase) { break }
    }
    $byName = $false
    if (-not $atBase -and -not $Span.Contains('/')) {
        if ($glob) {
            $re = Get-GlobRegex $Span
            foreach ($n in $fileNames) { if ($re.IsMatch($n)) { $byName = $true; break } }
        }
        else { $byName = $fileNames.Contains($Span) }
    }
    $result = @{ AtBase = $atBase; Exists = ($atBase -or $byName) }
    $resolved[$Span] = $result
    return $result
}

if ($Since) {
    $head = @(Invoke-Git @('rev-parse', '--abbrev-ref', 'HEAD'))
    if (-not $script:gitFailed -and $head.Count -eq 1 -and $head[0] -ceq $Since) {
        Write-Host ("GIT        HEAD is on '{0}' itself in {1}: run -Since in the task's worktree" -f $Since, $root) -ForegroundColor Red
        exit 1
    }
    $base = @(Invoke-Git @('merge-base', $Since, 'HEAD'))
    if ($script:gitFailed -or $base.Count -eq 0) {
        Write-Host ("GIT        cannot find where '{0}' and HEAD meet in {1}" -f $Since, $root) -ForegroundColor Red
        exit 1
    }
    # Relative to the project folder, as kit.json's folders are: both sides of a rename, deleted
    # files, and the working tree's changes, committed or not.
    $changed = @(Invoke-Git @('diff', '--name-only', '--relative', '--no-renames', $base[0]))
    if (-not $script:gitFailed) { $changed += @(Invoke-Git @('ls-files', '--others', '--exclude-standard')) }
    if ($script:gitFailed) {
        Write-Host ("GIT        cannot list the files changed since '{0}' in {1}" -f $Since, $root) -ForegroundColor Red
        exit 1
    }

    # The code and test files among them, compared as the Stop hook compares them.
    $inScope = New-Object 'System.Collections.Generic.SortedSet[string]' ($ordinal)
    foreach ($file in $changed) {
        $isCode = $kit.codeFiles -contains $file
        foreach ($folder in @($kit.codeFolders) + @($kit.testFolders)) {
            if ($file.StartsWith($folder, [System.StringComparison]::OrdinalIgnoreCase)) { $isCode = $true }
        }
        if ($isCode) { [void]$inScope.Add($file) }
    }

    $listed = 0
    foreach ($n in $notes) {
        $hits = New-Object System.Collections.Generic.List[string]
        foreach ($file in $inScope) {
            $name = $file.Substring($file.LastIndexOf('/') + 1)
            foreach ($span in $covers[$n].Spans) {
                $glob = $span -match '[*?]'
                $match = $false
                foreach ($b in $bases) {
                    $c = $b + $span
                    if ($glob) { $match = (Get-GlobRegex $c).IsMatch($file) }
                    else { $match = ($file -ceq $c) -or $file.StartsWith($c.TrimEnd('/') + '/', [System.StringComparison]::Ordinal) }
                    if ($match) { break }
                }
                # A name without "/" that no base resolves is a file found somewhere in the project.
                if (-not $match -and -not $span.Contains('/') -and -not (Resolve-Span $span).AtBase) {
                    $match = if ($glob) { (Get-GlobRegex $span).IsMatch($name) } else { $name -ceq $span }
                }
                if ($match) { $hits.Add($file); break }
            }
        }
        if ($hits.Count -gt 0) {
            Write-Host ("touched  docs/design/{0}: {1}" -f $n, ($hits -join ', '))
            $listed++
        }
    }
    if ($listed -eq 0) { Write-Host 'ok       no design note covers a changed code or test file' }
    exit 0
}

# The index: every note named in README.md as `name.md`, and every `name.md` in a table row a
# note, or another file of the project.
if ($names -cnotcontains 'README.md') {
    Add-Problem 'INDEX' 'docs/design/README.md' 'missing: it is the index of the notes'
}
else {
    $text = Read-Text (Join-Path $dir 'README.md')
    $named = New-Object 'System.Collections.Generic.HashSet[string]' ($ordinal)
    $rows = New-Object 'System.Collections.Generic.SortedSet[string]' ($ordinal)
    foreach ($s in (Get-CodeSpans $text)) {
        [void]$named.Add($s.Value)
        $line = $text.LastIndexOf([char]10, [Math]::Max($s.Start - 1, 0)) + 1
        if ($s.Value -cmatch '^[^/\s]+\.md$' -and $text.Substring($line, $s.Start - $line).TrimStart().StartsWith('|')) {
            [void]$rows.Add($s.Value)
        }
    }
    foreach ($n in $notes) {
        if (-not $named.Contains($n)) { Add-Problem 'INDEX' "docs/design/$n" 'not named in docs/design/README.md' }
    }
    foreach ($name in $rows) {
        if ($notes -cnotcontains $name -and -not (Resolve-Span $name).Exists) {
            Add-Problem 'INDEX' 'docs/design/README.md' "names ``$name``, which is not a note in docs/design/"
        }
    }
}

foreach ($n in $notes) {
    $c = $covers[$n]
    if (-not $c.OnTop) { Add-Problem 'COVERS' "docs/design/$n" "no 'Covers:' sentence above its first section" }
    foreach ($sentence in $c.Sentences) {
        if ($sentence.Count -eq 0) {
            Add-Problem 'COVERS' "docs/design/$n" "a 'Covers:' sentence names no file or folder as a path"
            break
        }
    }
    foreach ($span in $c.Spans) {
        if (-not (Resolve-Span $span).Exists) {
            Add-Problem 'MISSING' "docs/design/$n" "Covers names ``$span``, which is no file or folder of this project"
        }
    }
}

foreach ($p in $problems) { Write-Host $p -ForegroundColor Red }
if ($problems.Count -gt 0) { exit 1 }
Write-Host ("ok       {0} design notes in docs/design/" -f $notes.Count)
exit 0
