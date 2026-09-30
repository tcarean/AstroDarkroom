# SPDX-License-Identifier: GPL-3.0-or-later
# claude-stop-gate.ps1 - keeps Claude from ending its answer with unverified code.
#
# WHAT IT DOES
#   Claude Code calls it each time Claude is about to finish an answer (a "turn"). It takes
#   the list of files this session edited and groups them by project: the nearest folder above
#   each file that holds kit.json (Find-KitProject in kit-config.ps1), so an edit in a worktree
#   is judged by that worktree's copy of the project, not by the folder the session started in
#   (the kit's ADR 0020). A file in no project is not judged. Each project gets two checks,
#   on its own files, with its own kit.json:
#     A. scripts/, .claude/skills/ and .claude/agents/ are the kit's: in a project that is not
#        the kit (kit.json's "kit" is not "."), an edit under one of them ends no turn; the
#        change is made in the kit and copied with scripts/sync.ps1.
#     B. code was edited (kit.json's codeFolders, codeFiles or testFolders): the turn may end
#        only when scripts/verify.ps1 ran green with tests after the last such edit:
#        .verify-stamp, which only such a run writes (-SkipTests does not), is newer than the
#        newest edited file. Whether the change needs a CHANGELOG line is a judgment, not
#        checked here (the kit's ADR 0023).
#   If not, it answers exit 2 with the reasons under each project's full path, and Claude Code sends Claude back to work
#   (the message points to /wrap-up). It never runs verify itself, so it takes under a
#   second. When Claude is already continuing because of this hook (stop_hook_active), it
#   lets the turn end, so a session can never be trapped in a loop.
#
# WHO CALLS IT
#   Claude Code, as the Stop hook in .claude/settings.json, at the end of every turn; only
#   when Claude Code runs in this project folder. Nobody calls it by hand. It reads two
#   files written by others:
#     - %TEMP%\claude-edits-<session id>.txt, the files this session edited, written by
#       scripts/hooks/claude-track-edits.ps1;
#     - .verify-stamp in each project folder, written by that project's scripts/verify.ps1.
#
# RESULT (as Claude Code reads it)
#   exit 0  the turn may end
#   exit 2  the turn goes on; the text on stderr tells Claude what is missing
#
# Errors inside this script are not shown and do not stop it (SilentlyContinue): it carries
# on with the next line.
$ErrorActionPreference = 'SilentlyContinue'
# Claude Code sends JSON on standard input: session_id, stop_hook_active, ...
$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }
try { $payload = $raw | ConvertFrom-Json } catch { exit 0 }
if ($payload.stop_hook_active -eq $true) { exit 0 }

# No list of edited files for this session means nothing was edited: nothing to check.
$session = $payload.session_id
if (-not $session) { exit 0 }
$list = Join-Path $env:TEMP ("claude-edits-" + $session + ".txt")
if (-not (Test-Path $list)) { exit 0 }

. (Join-Path $PSScriptRoot '..\kit-config.ps1')

function Test-Under([string]$path, [string[]]$dirs) {
    foreach ($d in $dirs) { if ($path.StartsWith($d, [System.StringComparison]::OrdinalIgnoreCase)) { return $true } }
    return $false
}

# The problems of one project with the files of it this session edited; an empty list when
# there are none.
function Get-ProjectProblems([string]$projectDir, [string[]]$edited) {
    # kit.json says where the code is. Without a readable one there is nothing to judge by, so
    # the project passes; verify.ps1 will refuse the same file at commit time.
    try { $kit = Read-KitConfig $projectDir } catch { return @() }
    $srcDirs = @($kit.codeFolders | ForEach-Object { Get-KitFolderPath $projectDir $_ })
    $testDirs = @($kit.testFolders | ForEach-Object { Get-KitFolderPath $projectDir $_ })
    $srcFiles = @($kit.codeFiles | ForEach-Object { Join-Path $projectDir ($_ -replace '/', '\') })
    # The folders that are the kit's, mirrored by scripts/sync.ps1.
    $sharedDirs = @('scripts\', '.claude\skills\', '.claude\agents\') | ForEach-Object { Join-Path $projectDir $_ }

    $problems = @()

    # A. A shared folder edited in a project that is not the kit.
    $kitPath = Get-KitPath $kit $projectDir
    $isKit = $kitPath -and ($kitPath -eq $projectDir)
    $sharedEdits = @($edited | Where-Object { Test-Under $_ $sharedDirs })
    if (-not $isKit -and $sharedEdits.Count -gt 0) {
        $problems += 'scripts/, .claude/skills/ or .claude/agents/ was edited here, but those folders are the kit''s (byte-identical in every project). Make the change in the kit, then run scripts/sync.ps1 here; project-specific values go in kit.json, project-specific skill steps in docs/skills.md. Edited: ' + (($sharedEdits | ForEach-Object { $_.Substring($projectDir.Length + 1) }) -join ', ')
    }

    # B. Code edits: those under a code folder, a test folder, or named as a code file.
    $codeEdits = @($edited | Where-Object { (Test-Under $_ $srcDirs) -or (Test-Under $_ $testDirs) -or ($srcFiles -contains $_) })
    if ($codeEdits.Count -gt 0) {
        # verify ran green, with tests, after the last code edit: compare the stamp's time
        # with the newest modification time among the edited files that still exist.
        $stamp = Join-Path $projectDir '.verify-stamp'
        $lastEdit = ($codeEdits | Where-Object { Test-Path $_ } | ForEach-Object { (Get-Item $_).LastWriteTimeUtc } | Sort-Object -Descending | Select-Object -First 1)
        if (-not (Test-Path $stamp)) {
            $problems += 'scripts/verify.ps1 has not run green with tests in this project (no .verify-stamp; -SkipTests does not write it).'
        }
        elseif ($lastEdit -and (Get-Item $stamp).LastWriteTimeUtc -lt $lastEdit) {
            $problems += 'scripts/verify.ps1 has not run green with tests since the last code edit (-SkipTests does not count).'
        }
    }
    return $problems
}

# Each edited path once, as a full path, grouped by the project it belongs to (the nearest
# folder above it with kit.json). A path in no project is not this hook's to judge.
$edited = Get-Content $list | ForEach-Object { [System.IO.Path]::GetFullPath($_) } | Sort-Object -Unique
$byProject = @{}
foreach ($path in $edited) {
    $projectDir = Find-KitProject $path
    if (-not $projectDir) { continue }
    if (-not $byProject.ContainsKey($projectDir)) { $byProject[$projectDir] = @() }
    $byProject[$projectDir] += $path
}

# Each project judged on its own files; its problems listed under its full path, so a
# refusal says which copy (the shared checkout or which worktree) is missing what.
$problems = @()
foreach ($projectDir in ($byProject.Keys | Sort-Object)) {
    $own = @(Get-ProjectProblems $projectDir $byProject[$projectDir])
    if ($own.Count -gt 0) {
        $problems += $projectDir + ':'
        $problems += @($own | ForEach-Object { '  - ' + $_ })
    }
}

if ($problems.Count -eq 0) { exit 0 }

# Refuse: the reasons go to stderr, which Claude Code shows to Claude.
[Console]::Error.WriteLine('Stop gate: before ending this turn:')
$problems | ForEach-Object { [Console]::Error.WriteLine('  ' + $_) }
[Console]::Error.WriteLine('Run /wrap-up (verify, requirements, decisions, CHANGELOG when the change warrants it), then end the turn.')
exit 2
