# SPDX-License-Identifier: GPL-3.0-or-later
# verify.ps1 - the gate: the one definition of "green" for this project.
#
# WHAT IT DOES
#   Runs eight checks in order and stops at the first one that fails:
#     0. kit files           the shared folders (scripts/, .claude/skills/, .claude/agents/,
#                            .claude/rules/kit/) are byte-identical with the kit's, and the
#                            process keys of .claude/settings.json say what the kit's say
#                            (sync.ps1 -Check); only when kit.json names a kit folder that
#                            exists and is not this project, else the step is skipped with a
#                            yellow line
#     1. test data manifest  every file listed in <testData.folder>/manifest.json is there,
#                            with the SHA-256 hash written in the manifest (check-testdata.ps1)
#     2. decisions           the headers of docs/decisions/ are well formed and every
#                            supersession is recorded on both sides (check-decisions.ps1)
#     3. design notes        every note in docs/design/ is in its index and has a Covers
#                            sentence whose paths name files or folders that exist
#                            (check-design.ps1)
#     4. restore             downloads the NuGet packages; in CI (locked mode) it also fails
#                            when a packages.lock.json does not match
#     5. format              fails if dotnet format would change any file (it only checks)
#     6. build               builds the solution; every warning counts as an error
#     7. test                runs the tests, on exactly what step 6 built; kit.json's
#                            testFilter, when set, is passed to `dotnet test --filter`
#   It does not touch the code. Locally, restore may update a packages.lock.json after a
#   package change; commit it with that change. After a green run with tests it writes
#   .verify-stamp (date and time, git-ignored), which the Claude Code Stop hook reads.
#   The solution name and the filter come from kit.json (scripts/kit-config.ps1).
#
# WHO CALLS IT
#   - you, from a terminal:
#       powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
#   - git, through scripts/hooks/pre-commit, on every commit that touches this project
#   - Claude Code, through scripts/hooks/claude-commit-gate.ps1, before Claude's git commit
#   - CI: .github/workflows/ci.yml and bitbucket-pipelines.yml (in a monorepo, the files in
#     monorepo/ go to the repository root instead)
#   - scripts/release.ps1, before it changes anything
#   - Claude, in the /wrap-up and /mutate skills, and with -SkipTests while writing code
#     (/new-feature)
#   The Stop hook (scripts/hooks/claude-stop-gate.ps1) never runs it; it only reads the stamp.
#
# OPTIONS
#   -SkipTests             every step but the tests, a fast loop while editing; writes no
#                          stamp, so it does not count as "verify ran" for the Stop hook
#   -Configuration Debug   build and test in Debug instead of Release
#
# RESULT
#   exit 0  green: prints "== OK in <seconds>s"
#   exit 1  red: prints "FAILED: <step>" after that step's own error output
#
# Kept compatible with Windows PowerShell 5.1 on purpose: pwsh is not on every machine
# (the kit's ADR 0003).
[CmdletBinding()]
param(
    [switch]$SkipTests,
    [string]$Configuration = 'Release'
)

# Any PowerShell error ends the script at once, instead of printing and carrying on.
$ErrorActionPreference = 'Stop'
# The project folder is the parent of scripts/, wherever the script is started from.
$root = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'kit-config.ps1')
$kit = Read-KitConfig $root
$sln = Join-Path $root $kit.solution
# Child scripts run in the same PowerShell as this one (Windows PowerShell here, pwsh in CI).
$shell = Get-KitShell
$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

# Runs one step: prints its name, runs it, and if the program it ran returned a non-zero
# exit code ($LASTEXITCODE), prints FAILED and ends the whole script with exit 1.
function Invoke-Step {
    param([string]$Name, [scriptblock]$Action)
    Write-Host ("== {0}" -f $Name) -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) {
        Write-Host ("FAILED: {0}" -f $Name) -ForegroundColor Red
        exit 1
    }
}

# Work from the project folder; the finally block returns to the caller's folder at the end.
Push-Location $root
try {
    # Step 0 only makes sense in a project that has the kit beside it: the kit compares with
    # itself, and a standalone repository or a CI runner without the kit has nothing to
    # compare with; both print why and go on.
    $kitPath = Get-KitPath $kit $root
    $here = [System.IO.Path]::GetFullPath($root).TrimEnd('\', '/')
    if (-not $kitPath) {
        Write-Host '== kit files: skipped (kit.json names no kit folder)' -ForegroundColor Yellow
    }
    elseif ($kitPath -eq $here) {
        Write-Host '== kit files: skipped (this project is the kit)' -ForegroundColor Yellow
    }
    elseif (-not (Test-Path -LiteralPath $kitPath)) {
        Write-Host ("== kit files: skipped (kit not found at {0})" -f $kitPath) -ForegroundColor Yellow
    }
    else {
        Invoke-Step 'kit files'      { & $shell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'sync.ps1') -Check }
    }
    # -v q (quiet) keeps each tool's output short. --no-restore and --no-build reuse what the
    # previous step produced instead of doing it again.
    Invoke-Step 'test data manifest' { & $shell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'check-testdata.ps1') }
    Invoke-Step 'decisions'          { & $shell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'check-decisions.ps1') }
    Invoke-Step 'design notes'       { & $shell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'check-design.ps1') }
    Invoke-Step 'restore'          { dotnet restore $sln --nologo -v q }
    Invoke-Step 'format'             { dotnet format $sln --verify-no-changes --no-restore -v q }
    Invoke-Step 'build'              { dotnet build $sln -c $Configuration --no-restore --nologo -v q -warnaserror }
    if (-not $SkipTests) {
        # An empty filter is not passed: `--filter ""` would be an error, not "everything".
        $testArgs = @()
        if ($kit.testFilter) { $testArgs = @('--filter', $kit.testFilter) }
        Invoke-Step 'test'           { dotnet test $sln -c $Configuration --no-build --nologo -v q @testArgs }
        # Success stamp, read by the Claude Code Stop hook ("did verify run after the last edit?").
        # Only a run with tests writes it: -SkipTests is the edit loop, not the gate.
        [System.IO.File]::WriteAllText((Join-Path $root '.verify-stamp'), (Get-Date).ToString('o'))
    }
    Write-Host ("== OK in {0:n0}s" -f $stopwatch.Elapsed.TotalSeconds) -ForegroundColor Green
    exit 0
}
finally {
    Pop-Location
}
