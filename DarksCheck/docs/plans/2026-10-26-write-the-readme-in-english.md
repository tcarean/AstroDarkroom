# 2026-10-26 Write the README in English, current and relevant

## Context

On 2026-10-26 the owner asked: rewrite `README.md` in English and make sure it is up to date and
relevant. Today the README is the user documentation in Romanian, by ADR 0005 (README and
`THIRD-PARTY-NOTICES.md` in the Romanian zone) and ADR 0021, whose context kept "the README
Romanian only, documenting `--lang`". The rule "Languages" in `CLAUDE.md`, its Layout line,
the intro of `docs/requirements.md` and `docs/skills.md` (/new-feature step 8) all say so.
The same day the repository got an English front page (AstroDarkroom ADR 0008) and
AstroDarkroom's README went English (its ADR 0009). The front page still says every tool's
README is in Romanian.

Owner's answers (2026-10-26, asked in plan mode):
- `THIRD-PARTY-NOTICES.md` is translated too, in this task.
- Depth: keep every check, tightened. That means what each row of checks 2 to 9, the index,
  the tables and the window tabs means and when the verdict turns. Re-check the figures, cut
  the stale parts and add what is missing.

## What is out of date

Found against the code, `docs/requirements.md`, `docs/validation.md` and today's English runs.
Every case was run with the main checkout's Release console, built 2026-10-25 21:49, after the
last change to `src/`. The outputs are in the session scratchpad `runs/`.
1. **Status paragraph.** It narrates the build plan's steps and calls 1.0.0 "the first". In
   fact `<Version>` is 1.0.0, no `darkscheck-v1.0.0` tag exists and the CHANGELOG has only
   `[Unreleased]`. Replace it with one line: the version, not yet released.
2. **Timings.** The README still has ADR 0007's first figures: Smida in 35 s at 4.5-4.7 GB
   "without the lights". The latest runs on an idle machine (`docs/validation.md`, 2026-10-25,
   .NET 8, 4 threads):
   - Smida with its 32 lights: 62.9-64.7 s at 5.72-5.87 GB.
   - The library: 24.2-25.9 s at 3.22-3.29 GB.
   - The partner adds about 27 s (2026-10-16).

   Today's runs agree on memory: Smida 5.83 GB, the library 3.11 GB, Smida with the library as
   partner 5.89 GB, Veil with the library 5.30 GB. They ran at 100 % CPU, though (StarNet and
   an indexer), at 114 s and 52 s, so their times are not used.
3. **Inventory time.** The README says 0.3 s for Smida's 82 raws. Today it took 1.5 s with the
   files cached and 2.9 s from a cold disk, both under load. Write "under three seconds".
4. **Library folder name.** It is `2026-10-10-ISO1600-120s-15.8celsius-idx67` since
   2026-10-20; the examples still use the old name.
5. **Check 3's time constant indoors.** The README says "20 .. 30 min"; the series measured
   18, 21, 24 and 28 min (V-004, V-010, V-009, V-008). Write "18 to 28".
6. **`Write the files...` dialog.** The README says it "proposes the session folder". The
   dialog does open there (`MainWindow.xaml.cs`, `InitialDirectory`), but that folder is
   refused. Tell the reader to pick a folder outside the session and the partner.
7. **Building.** The README does not name the target framework (.NET 8, ADR 0018). It claims
   "the console alone compiles anywhere", which was never measured; drop it. Add `-PublishOnly`.
8. **Missing for a reader who meets the tool in Astro Darkroom:**
   - Where to get it: the suite's zip on the front page holds the window, and the console comes
     from `scripts/release.ps1 -Version X.Y.Z -PublishOnly` in this folder.
   - What it needs: Windows 10 or 11 x64 and nothing else installed, since the executables are
     self-contained single files.
   - Which cameras: LibRaw's raws, but the figures are validated on the D3300 only, and the
     `electrons` row knows the D3300 only.
9. **Language.** Every example and label is Romanian, while English is the default since ADR
   0021.
10. **Order.** "Which folder" opens with the lights-only case, before the ordinary one.
11. **Developer note.** The line "the user documentation lives here; do not duplicate it in
    comments" speaks to developers. It moves to "For developers".

## The new README

Sections, all English:
1. **Title and introduction.** The nine checks, the index and the label. Read-only. Console and
   window over one core, with the same figures. Part of Astro Darkroom. Version 1.0.0, not yet
   released.
2. **Getting it and what it needs.**
3. **The folder you give it.** In this order: a session, a library, a session with lights but
   no darks (with a partner). Then the refusals, the formats and the order of the frames.
4. **The console.**
   - Usage, exit codes and `--lang`, checked against `Golden/en_help.txt`, R-004, R-065 and
     R-090 to R-092.
   - The full run: the decoding order, the cap, the timings of item 2 and the ADU-eq
     normalisation. A sample: the header and checks 2-3 from today's Smida run.
   - One paragraph per section: checks 2 to 9, INDEX with the label, ADU SCALE EXPLAINED,
     FRAMES TABLE and LIGHTS TABLE, as now but tightened. Each is verified against its R-0xx,
     with English row names and verdict words. A sample: check 9 and INDEX.
   - `--against`, with the partner rows of today's Smida-vs-library run, and the lights-only
     case from today's Veil run.
   - FITS frames, then `--out`, then `--inventory` with a sample.
5. **The window.** The English tab and button names, checked against the XAML `En=` pairs, with
   the Romanian names in one line for `--lang ro`. The top bar, the settings file, `Write the
   files...` and `About` (F1).
6. **Building.** The .NET 9 SDK 9.0.318 or a later 9.0.3xx (`global.json`), targeting .NET 8.
   Build, the gate, and publishing to `artifacts/<version>/console/` and `wpf/`.
7. **For developers.** Pointers: `CLAUDE.md`, the kit's process rules, requirements, decisions,
   design notes, validation, `prototype/`, and the line from item 11.
8. **Licence.** GPLv3 or later, `LICENSE`, `THIRD-PARTY-NOTICES.md`.

Rule for the samples: excerpts copied verbatim from today's runs, never edited figures. A row
cut short ends in `...`.

## Other files

- `THIRD-PARTY-NOTICES.md`: English, same content. The headings `## Sdcb.LibRaw` and
  `## LibRaw (...)` stay, since `ThirdPartyTests` matches them.
- `docs/decisions/0022-the-readme-and-the-notices-are-in-english.md`. It supersedes "0005 in
  part (README.md and THIRD-PARTY-NOTICES.md in Romanian)" and "0021 in part (the README in
  Romanian only)". The Status lines of 0005 and 0021 gain the matching clauses, which
  `check-decisions.ps1` checks.
- `CLAUDE.md`:
  - Layout: "`README.md` - the user documentation, English".
  - The rule "Languages": `README.md` and `THIRD-PARTY-NOTICES.md` are English (ADR 0022); the
    Romanian wordings of the interface, and the tests and docs that quote them, are the one
    exception. This mirrors AstroDarkroom's wording.
- `docs/requirements.md`, line 21: "in Romanian" becomes "in English".
- `docs/skills.md`, /new-feature step 8: "`README.md` is in English and names every console
  option".
- `CHANGELOG.md` `[Unreleased]`, Changed: the README and the notices are in English, the README
  rewritten against the current behaviour (ADR 0022).
- Second commit, outside DarksCheck. AstroDarkroom ADR 0008 asks that a task changing a fact the
  front page restates edit the front page too.
  - The root `README.md`: the sentence "the user guide is the folder's `README.md`, in
    Romanian" names DarksCheck's README as English.
  - `AstroDarkroom/docs/decisions/0009-the-readme-is-in-english.md`: a dated
    `(Restated 2026-10-26: ...)` note after "The five tools' READMEs stay in Romanian".
  - The pre-commit hook runs AstroDarkroom's gate for this commit.

## Steps

1. From `DarksCheck/`, create the worktree:
   `git worktree add .claude/worktrees/Write-the-README-in-English -b claude/Write-the-README-in-English main`.
   Save this plan as `docs/plans/2026-10-26-write-the-readme-in-english.md`.
2. Write `README.md`.
3. Translate `THIRD-PARTY-NOTICES.md`.
4. Write ADR 0022 and the two Status lines, then edit `CLAUDE.md`, `requirements.md`,
   `skills.md` and the CHANGELOG.
5. Gate, read the whole diff, commit on the branch.
6. Edit the root README and the AstroDarkroom ADR note, then commit.
7. `/wrap-up`: record the outcome in the plan, then propose the merge (`--no-ff`, the owner's
   call).

## Verification

- `scripts/verify.ps1` is green in the worktree's `DarksCheck/`. It covers:
  - format, build and tests, unchanged since there is no code change;
  - `check-decisions` (the ADR header and both sides of each supersession);
  - `ThirdPartyTests` (the notices' headings);
  - `check-design -Since main`.
- AstroDarkroom's gate passes through the pre-commit hook of the second commit.
- Every figure in the README traces to a run output or a dated line of `docs/validation.md`.
  Every option traces to `en_help.txt`, every window label to an `En=` pair, every threshold to
  its R-0xx or `Thresholds.cs`. Every relative link resolves.
- No golden file, no figure of `validation.json` and no code change: `git diff --stat` shows
  docs only.

## What deviated

- **Branch name.** `claude/Write-the-README-in-English` was already taken by AstroPhotoAnalyzer's
  README task, running in parallel, and LightsCheck's runs as
  `claude/Rewrite-the-README-in-English`. This task is `claude/Write-the-DarksCheck-README-in-English`,
  worktree `.claude/worktrees/Write-the-DarksCheck-README-in-English`. All three branches edit
  the same sentence of the front page, each naming its own tool, so every merge after the first
  needs a one-line resolution that names all the tools whose README is English.
- **No release-status line.** The introduction does not say "1.0.0, not yet released": that line
  would go stale at the release, and the programs print their version.
- **Added beyond the list:** the memory a run needs (under "Getting it"), a sample of the frames
  table, and the exit code of a frame left out for its size or settings (it is listed as an
  error, `HasErrors`, so the run exits `1`).
- **Measured for "Building":** `release.ps1 -Version 1.0.0 -PublishOnly` in the worktree, 67 s at
  100 % CPU: 11 files, 209 MB, `console\darkscheck.exe` 67 MB and `wpf\DarksCheck.Wpf.exe`
  141 MB beside their `.pdb` and `.xml` files; `git status` clean afterwards but for this plan.
- **Samples checked mechanically:** the 53 report lines the README quotes were compared with the
  English run outputs, each equal to an output line or, cut with `...`, a prefix of one.
- **Found, not fixed here** (a change of the interface's text): check 2's verdict joins
  `; that is ` to a sentence that has its own verb, so the English reads
  `that is +0.79 ADU is 0.55 times the dark master's noise per pixel (1.43 ADU)` on the leak set
  (`DarkReport.cs`, the `against` clause). The README quotes the `constant light` row's own
  clause instead.
