# Rewrite FlatCheck's README in English, current and for users only

Branch `claude/Rewrite-the-user-guide-in-English`, worktree
`FlatCheck/.claude/worktrees/Rewrite-the-user-guide-in-English`, cut from `main` at 876782d (the
merge of `claude/Identify-the-lens-on-every-mount`). Saved to
`docs/plans/2026-10-26-rewrite-the-user-guide-in-english.md` once approved.

## Context

The owner asked on 2026-10-26: rewrite `README.md` in English and make sure it is up to date and
relevant. Today it is 1563 lines of Romanian, by the rule "Languages" in `CLAUDE.md` ("`README.md`
stays Romanian", from ADR 0006). Since ADR 0062 the interface speaks English by default, the
repository's front page and AstroDarkroom's README are English, and sibling sessions are rewriting
the AstroPhotoAnalyzer and LightsCheck READMEs.

What exploration found:

- **Measured figures hold.** `/validate` on `main` (eb29c6e), 2026-10-26 17:32: 40 of 40 cases
  pass, no drift, so the measured tables the README quotes (V-016 to V-040) stay as they are.
- **Stale:** "one dependency" (there are two packages, LibRaw and ImageSharp, plus the lensfun
  data); "four projects" (five); no word on the build SDK (`global.json`: .NET 9 SDK 9.0.318,
  latest patch); a plain `dotnet test` (the gate filters out `Category=Validation`); the output
  examples are said to be Romanian; "since 1.2.0" and other version history; Python/rawpy
  comparisons throughout.
- **Not for users:** the development process section, the parity goldens with the Python
  cross-check commands, the list of differences from Python (already in
  `docs/design/python-parity.md` and ADR 0038), the history of the fixed ring artefact
  (`radial-model-even-term.md` holds it), and the notes "this table mirrors SessionMessages..."
  (the code comments already say it).
- **The lens branch** `claude/Identify-the-lens-on-every-mount` also edits `README.md` (`.pef`, the
  lens identified by name on every mount) and takes ADR 0068; its merge began at 17:31 and stopped.

## Decisions (the owner's answers, 2026-10-26)

- English replaces the Romanian; no Romanian copy (stated, not asked: Romanian readers have the
  interface with `--lang ro`, and two 1000-line files would drift).
- User guide only: the cuts above; a short "Development" section points at `CLAUDE.md` and
  `docs/`. Kept: everything a user needs, the measured tables, and the tables that mirror the
  window's guides, in the window's own English wording (`SessionMessages.cs`).
- Start from `main` after the lens branch has been merged.

## Steps

1. When `main` holds the lens merge: from `FlatCheck/`,
   `git worktree add .claude/worktrees/Rewrite-the-user-guide-in-English -b claude/Rewrite-the-user-guide-in-English main`
   (the names `Write-...`/`Rewrite-the-README-in-English` are taken by the sibling sessions).
   Save this plan as `docs/plans/2026-10-26-rewrite-the-user-guide-in-english.md`. Every build and
   test from here runs in the worktree, never in the main checkout.
2. **Write `README.md` in English**, British spelling like the code's English texts, no internal
   anchor links (Bitbucket and VS Code make different anchors; sections are named instead),
   `[TOC]` as on the front page. Outline:
   - What it is (the About window's tagline, `SessionMessages.AboutTagline`), the two applications
     over one core, the language switch; **Getting it** (the window in the Astro Darkroom suite;
     building both with `scripts/release.ps1 -Version X.Y.Z -PublishOnly` or `dotnet publish`;
     Windows, Siril 1.4.4+, Adobe profiles optional); **Quick start** (window on a session folder,
     three console lines).
   - Which file to give it; the basic idea (radial symmetry, the sensor's centre).
   - The six modes, numbered 1 to 6 as today (structure with dust, the blind spot and the sixth
     test; `--light`; `--expo`; `--histeq` with `--rainbow`; `--smooth` with `--scale auto`;
     `--synthetic` / `--lenses` / `--against`), condensed but complete.
   - When `--light` works and when it does not (the console's texts point at this section), the
     residual scale, thresholds (completed from `Thresholds.cs`: the exposure bands, the smoothing
     and profile tolerances), exit codes, **Options** (mirrors `WriteUsage`, every flag, as
     `docs/design/process.md` asks), what invalidates a master, the PNG map (colour scale, the
     shapes, who decides).
   - The window: how to use it, Run in Siril, About, the seven tabs, what it does not do; the
     long Synthetic and Split paragraphs broken into lists.
   - Development (projects, build, test, gate, where the rules and docs live; one line on the
     frozen `flatcheck.py`), licence and third-party components.

   Sources, quoted not retyped: every console word from `FlatCheck.Tests/Golden/en_*.txt` and
   `en_help.txt`, tab and button names from the XAML `En=` labels and `SessionMessages`,
   behaviour from `docs/requirements.md`, figures from `docs/validation.md`.
3. **ADR 0069** "The README is English and holds the user guide only", superseding 0006 in part
   (the README in Romanian); ADR 0006's Status line gets the matching clause (the kit's ADR 0024).
4. **Every place that says the README is Romanian or names a Romanian section:** `CLAUDE.md`
   (Layout, the rule "Languages"), `docs/requirements.md` (intro), `docs/skills.md`
   (`/new-feature` step 8), `docs/design/process.md` ("Opțiuni", "Build, rulare, teste",
   "uses diacritics"), `docs/design/README.md` (index row), `docs/design/dependencies.md`
   ("Componente terțe"), and the comments in `FlatCheck.Wpf/Session/SessionMessages.cs` that name
   README sections ("Expunere", "Iesire PNG", "Netezire"). Accepted ADRs and dated plans stay as
   history.
5. CHANGELOG, `[Unreleased]` / Changed: one line (the README is English and user-only, ADR 0069).
6. **Cross-check:** a fresh subagent reads the new README against the goldens, the requirements,
   `Thresholds.cs`, the XAML and `SessionMessages` and lists every claim that does not match;
   I fix each one.
7. `/wrap-up`: gate green, read the whole diff, commit on the branch, propose the merge.

Out of scope: the front page (`E:\Photografie\Code\README.md`, AstroDarkroom's) still lists the
removed Ratio tab and says each tool's guide is Romanian; that becomes a task of its own once
the README rewrites have landed. No version bump.

## Verification

- `scripts/verify.ps1` green in the worktree (it runs `check-decisions.ps1` and
  `check-design.ps1`); `scripts/check-design.ps1 -Since main` lists the notes touched, reread.
- Scripted greps on the new README: no diacritics or Romanian section names, no `--ratio`, every
  flag of `en_help.txt` present in "Options", every `.ext` the help accepts listed.
- Greps over `docs/`, `CLAUDE.md` and the code comments: no "Opțiuni", "Iesire PNG",
  "Componente terțe", "README.md stays Romanian" left outside ADRs and dated plans.
- The subagent's report, with each finding fixed or answered.

## Outcome and deviations (2026-10-26)

- Done as planned: README.md in English (1476 lines against 1563), ADR 0069, the documents of
  step 4, the CHANGELOG line. The gate was green on the worktree (`== OK in 463s`, 1088 + 326
  tests) and again without tests after the last documentation edits.
- `/validate` ran twice: first on eb29c6e in the main checkout (40 of 40), which overlapped a
  merge started there at 17:31; then on 876782d in this worktree (48 of 48, 5.8 minutes). Every
  later build and test ran in the worktree. The re-run is recorded in `docs/validation.md`.
- Also fixed in place, beyond step 4: ADR 0008 named the README's table by its Romanian heading
  (a renamed reference, the first of the kit's ADR 0024 classes).
- The cross-check read about 280 claims and found 12 imprecise or stale ones and 3 user facts the
  rewrite had lost (the old Romanian file names, the dating of the `--against` figures, what the
  lens list says without `lensfun/`); all were fixed. Among them: a Nikon writes no lens name for
  any lens measured (R-108), the illumination offset now reads `undetermined` outside the frame
  (ADR 0014), the window's map chips read `+/-10 %`.
- Left for the owner, outside this task: `THIRD-PARTY-NOTICES.md`, which the README links, is
  still Romanian; the console's offset note "beyond a threshold it falls back toward 0"
  (`StructureReport.cs`, in every structure golden and in the 15 structure parity goldens) and the
  window's map-guide sentence "The rotation test is just as blind to it" are stale; R-081 says the
  window saves its report "beside the master", the code saves it in the session folder; the
  repository's front page still lists the Ratio tab and calls every tool's guide Romanian.
