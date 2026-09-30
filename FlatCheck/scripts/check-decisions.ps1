# SPDX-License-Identifier: GPL-3.0-or-later
# check-decisions.ps1 - checks the headers of the decision records (ADRs) in docs/decisions/.
#
# WHAT IT DOES
#   Reads every .md in docs/decisions/. Each is named NNNN-slug.md, lowercase words joined by
#   hyphens; 0000-template.md is skipped, and no other file may use 0000. Then it checks the
#   first lines of each ADR, as the kit's ADR 0024 fixes them (letter case counts):
#     1  # NNNN. <title>              NNNN is the number in the file name
#     2  (blank)
#     3  Date: YYYY-MM-DD
#     4  Status: proposed | accepted YYYY-MM-DD | superseded by REF, the last two followed by
#        any number of "; <part> superseded by REF", each with an optional " (<note>)"
#     5  Supersedes: NNNN or NNNN in part (<part>), separated by "; " or ", " (optional line,
#        followed by a blank line)
#   REF is the number of an ADR of this project; in a project that is not the kit it may also
#   be "the kit's ADR NNNN".
#   Then, across the files:
#     - no number is used twice, and every ADR has exactly one "## Decision" section;
#     - no dated correction note ("Corrected|Restated|Amended YYYY-MM-DD", in any letter case)
#       sits inside "## Decision": a note goes next to the text it corrects, or right after
#       the section when that text is inside it;
#     - every number named on a Status or Supersedes line exists and is older than the ADR
#       that supersedes it;
#     - both sides of a supersession agree: "Supersedes: X" in N goes with "Status: superseded
#       by N" in X, and "X in part (...)" in N with a "; <part> superseded by N" clause in X;
#     - "the kit's ADR NNNN" names an ADR in the kit's docs/decisions/, checked only when
#       kit.json's "kit" names a folder, other than this project, that has docs/decisions/.
#   Whether an edit to an accepted ADR should have been a new ADR is not checked here: that is
#   a judgment made at /wrap-up.
#
# WHO CALLS IT
#   - scripts/verify.ps1, before the build; so the git pre-commit hook, Claude's commit hook
#     and CI all run it
#   - you, to see the problems alone:
#       powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-decisions.ps1
#
# RESULT
#   exit 0  every header is well formed and every supersession is recorded on both sides
#   exit 1  one red line per problem: file, what is wrong, what was expected
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
# The project folder is the parent of scripts/.
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'kit-config.ps1')
$kit = Read-KitConfig $root
$dir = Join-Path $root 'docs\decisions'
if (-not (Test-Path -LiteralPath $dir)) {
    Write-Host 'decisions: no docs/decisions/ folder, nothing to check' -ForegroundColor Yellow
    exit 0
}

# Where the kit's decisions are, if this project may cite them and they can be checked.
$kitPath = Get-KitPath $kit $root
$here = [System.IO.Path]::GetFullPath($root).TrimEnd('\', '/')
$isKit = $kitPath -and ($kitPath -eq $here)
$kitDecisions = $null
if ($kitPath -and -not $isKit) {
    $candidate = Join-Path $kitPath 'docs\decisions'
    if (Test-Path -LiteralPath $candidate) { $kitDecisions = $candidate }
}

# REF: a number of this project, or "the kit's ADR NNNN". The kit itself may not use the second.
$ref = '(?:\d{4}|the kit''s ADR \d{4})'
$clause = "; [^;]+? superseded by (?<ref>$ref)(?: \([^();]+\))?"
$statusRx = "^Status: (?:proposed|(?:accepted \d{4}-\d{2}-\d{2}|superseded by (?<head>$ref))(?:$clause)*)$"
$item = '(?<num>\d{4})(?<part> in part \([^()]+\))?'
$supersedesRx = "^Supersedes: $item(?:(?:; |, )$item)*$"
$noteRx = '(?i)\b(?:Corrected|Restated|Amended) \d{4}-\d{2}-\d{2}'

$problems = New-Object System.Collections.Generic.List[string]
function Add-Problem([string]$Kind, [string]$File, [string]$Text) {
    $problems.Add(("{0,-11}{1}: {2}" -f $Kind, $File, $Text))
}

# number -> @{ File; Head (the ref after "superseded by", or $null); Clauses (list of refs);
#              Supersedes (list of @{ Num; Part }) }
$adrs = @{}
$files = Get-ChildItem -LiteralPath $dir -Filter '*.md' -File | Sort-Object Name
foreach ($f in $files) {
    $name = $f.Name
    $m = [regex]::Match($name, '^(\d{4})-[a-z0-9]+(?:-[a-z0-9]+)*\.md$')
    if (-not $m.Success) { Add-Problem 'NAME' $name 'not NNNN-slug.md (lowercase words joined by hyphens)'; continue }
    $number = $m.Groups[1].Value
    if ($name -ceq '0000-template.md') { continue }
    if ($number -eq '0000') { Add-Problem 'NAME' $name '0000 is the number of 0000-template.md only'; continue }

    # UTF-8 on purpose: Windows PowerShell 5.1 would read a file without a BOM in the ANSI code page.
    $lines = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8) -split "`r?`n"
    while ($lines.Count -lt 6) { $lines += '' }

    if ($lines[0] -cnotmatch "^# $number\. \S") { Add-Problem 'TITLE' $name ("line 1 '{0}' is not '# $number. <title>'" -f $lines[0]) }
    if ($lines[1] -ne '') { Add-Problem 'HEADER' $name ("line 2 '{0}' must be blank" -f $lines[1]) }
    if ($lines[2] -cnotmatch '^Date: \d{4}-\d{2}-\d{2}$') { Add-Problem 'DATE' $name ("line 3 '{0}' is not 'Date: YYYY-MM-DD'" -f $lines[2]) }

    $entry = @{ File = $name; Head = $null; Clauses = @(); Supersedes = @() }
    $sm = [regex]::Match($lines[3], $statusRx)
    if (-not $sm.Success) {
        Add-Problem 'STATUS' $name ("line 4 '{0}' is not 'Status: proposed | accepted YYYY-MM-DD | superseded by NNNN', optionally followed by '; <part> superseded by NNNN'" -f $lines[3])
    }
    else {
        if ($sm.Groups['head'].Success) { $entry.Head = $sm.Groups['head'].Value }
        # The refs of the match that validated the line, one capture per clause. Do not match the
        # clauses again on their own: a part that itself contains "superseded by NNNN" would be
        # split differently from the validated line.
        foreach ($c in $sm.Groups['ref'].Captures) { $entry.Clauses += $c.Value }
    }

    if ($lines[4].StartsWith('Supersedes:')) {
        $pm = [regex]::Match($lines[4], $supersedesRx)
        if (-not $pm.Success) {
            Add-Problem 'SUPERSEDES' $name ("line 5 '{0}' is not 'Supersedes: NNNN' or 'NNNN in part (<part>)', separated by '; ' or ', '" -f $lines[4])
        }
        else {
            $caps = $pm.Groups['num'].Captures
            $parts = $pm.Groups['part'].Captures
            # A part belongs to the number just before it: match them by position in the line.
            foreach ($n in $caps) {
                $isPart = $false
                foreach ($p in $parts) { if ($p.Index -eq $n.Index + $n.Length) { $isPart = $true } }
                $entry.Supersedes += @{ Num = $n.Value; Part = $isPart }
            }
        }
        if ($lines[5] -ne '') { Add-Problem 'HEADER' $name ("line 6 '{0}' must be blank after the Supersedes line" -f $lines[5]) }
    }
    elseif ($lines[4] -ne '') { Add-Problem 'HEADER' $name ("line 5 '{0}' must be blank or 'Supersedes: ...'" -f $lines[4]) }

    # Exactly one "## Decision", and no dated correction note inside it. Lines are joined first:
    # a note may wrap, as in "(Restated" at the end of one line and the date on the next.
    $starts = @(for ($i = 0; $i -lt $lines.Count; $i++) { if ($lines[$i] -ceq '## Decision') { $i } })
    if ($starts.Count -ne 1) { Add-Problem 'DECISION' $name ("has {0} '## Decision' sections, not one" -f $starts.Count) }
    else {
        $section = @()
        for ($i = $starts[0] + 1; $i -lt $lines.Count -and -not $lines[$i].StartsWith('## '); $i++) { $section += $lines[$i] }
        $joined = (($section -join ' ') -replace '\s+', ' ')
        $note = [regex]::Match($joined, $noteRx)
        if ($note.Success) {
            Add-Problem 'NOTE' $name ("'{0}' inside '## Decision': a correction note goes right after the section when the text it corrects is inside it; a changed decision is a new ADR" -f $note.Value)
        }
    }

    if ($adrs.ContainsKey($number)) { Add-Problem 'DUPLICATE' $name ("number {0} is also used by {1}" -f $number, $adrs[$number].File) }
    else { $adrs[$number] = $entry }
}

# A REF is either local (NNNN) or the kit's; the kit may not cite itself that way, and a kit
# ADR is only checked when the kit is beside this project.
function Test-KitRef([string]$File, [string]$Ref) {
    $n = $Ref.Substring($Ref.Length - 4)
    if (-not $kitPath) { return }
    if ($isKit) { Add-Problem 'LINK' $File "'$Ref': the kit names its own ADRs by number"; return }
    # 0000 is the kit's template, not an ADR.
    if ($kitDecisions -and ($n -eq '0000' -or -not (Get-ChildItem -LiteralPath $kitDecisions -Filter "$n-*.md" -File))) {
        Add-Problem 'LINK' $File "'$Ref' does not exist in the kit's docs/decisions/"
    }
}

foreach ($number in ($adrs.Keys | Sort-Object)) {
    $a = $adrs[$number]
    # This ADR supersedes others: each must exist, be older, and say so in its Status.
    foreach ($s in $a.Supersedes) {
        $x = $s.Num
        if (-not $adrs.ContainsKey($x)) { Add-Problem 'LINK' $a.File "supersedes $x, which does not exist"; continue }
        if ([int]$x -ge [int]$number) { Add-Problem 'LINK' $a.File "supersedes $x, which is not older"; continue }
        $old = $adrs[$x]
        if ($s.Part) {
            if ($old.Clauses -notcontains $number) { Add-Problem 'LINK' $old.File "is superseded in part by $number, but its Status has no '; <part> superseded by $number'" }
        }
        elseif ($old.Head -ne $number) { Add-Problem 'LINK' $old.File "is superseded by $number, but its Status is not 'superseded by $number'" }
    }
    # This ADR says it is superseded: the newer ADR must exist and name it on its Supersedes line.
    $refs = @()
    if ($a.Head) { $refs += @{ Ref = $a.Head; Part = $false } }
    foreach ($c in $a.Clauses) { $refs += @{ Ref = $c; Part = $true } }
    foreach ($r in $refs) {
        if ($r.Ref -notmatch '^\d{4}$') { Test-KitRef $a.File $r.Ref; continue }
        $by = $r.Ref
        if (-not $adrs.ContainsKey($by)) { Add-Problem 'LINK' $a.File "is superseded by $by, which does not exist"; continue }
        if ([int]$by -le [int]$number) { Add-Problem 'LINK' $a.File "is superseded by $by, which is not newer"; continue }
        $named = @($adrs[$by].Supersedes | Where-Object { $_.Num -eq $number -and $_.Part -eq $r.Part })
        if ($named.Count -eq 0) {
            $want = if ($r.Part) { "$number in part (...)" } else { $number }
            Add-Problem 'LINK' $adrs[$by].File "supersedes $number according to $($a.File), but its Supersedes line does not name '$want'"
        }
    }
}

foreach ($p in $problems) { Write-Host $p -ForegroundColor Red }
if ($problems.Count -gt 0) { exit 1 }
Write-Host ("ok       {0} decisions in docs/decisions/" -f $adrs.Count)
exit 0
