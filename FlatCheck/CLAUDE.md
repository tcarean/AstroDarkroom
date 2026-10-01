# FlatCheck

Quantitative diagnostic for astrophotography flat frames, for a photographic lens on a DSLR
(the author's Samyang 135 f/2, Nikkor 35 DX and Tokina 11-20 on a Nikon D3300, Siril workflow).
Seven modes: structure (is the master radially symmetric, and does its vignetting match the
lens's published profile), `--ratio` (camera-fixed versus world-fixed by 180° rotation),
`--light` (does the flat correct a real light), `--expo` (is a raw flat exposed right),
`--histeq` (Siril's histogram display, a picture, no verdict), `--smooth` (writes a second
master with the diffuser texture removed), `--synthetic` / `--lenses` / `--against` (writes a
radial flat from the lens's Adobe or lensfun profile and compares). Two applications, a console
(`flatcheck.exe`) and a WPF window, over one core library; neither computes a number itself.
It is not an image processor and it never decides what a shape on a map is.

## Commands

- Gate (what pre-commit, pre-merge-commit and the Claude commit hook run, about 3 min):
  `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`
- Fast loop, no tests: `... scripts/verify.ps1 -SkipTests`
- Build: `dotnet build FlatCheck.sln -c Release --nologo` (Windows only: the WPF project;
  the console alone builds anywhere: `dotnet build FlatCheck/FlatCheck.csproj -c Release`)
- Test: `dotnet test FlatCheck.sln -c Release --nologo --filter "Category!=Validation"`
- One test: `dotnet test FlatCheck.sln --filter "FullyQualifiedName~R012"`
- Format: `dotnet format FlatCheck.sln` (check only: `--verify-no-changes`)
- Run: `dotnet run --project FlatCheck -- <file.fit> [--ratio B.fit | --light | --expo | ...]`;
  the flag may come before or after the path (R-003). Window: `dotnet run --project FlatCheck.Wpf -- <session folder>`
- Golden console texts, re-approved only after the owner read the diff:
  `FLATCHECK_UPDATE_GOLDEN=1 dotnet test FlatCheck.sln` (`Golden/parity_*.txt` are the 43
  scenarios the Python prototype confirmed on 2026-09-26; re-approving one is a C#-only call)
- Window renders as PNGs without running it: `FLATCHECK_DUMP_SCREENS=<dir> dotnet test FlatCheck.Wpf.Tests --filter WindowSmokeTests`
- Figures on the real masters (minutes, not in the gate): `/validate`, or
  `dotnet test FlatCheck.sln -c Release --filter "Category=Validation"` with
  `FLATCHECK_TESTDATA_LARGE` pointing at the masters
- Test data hashes after an intentional change: `scripts/check-testdata.ps1 -Update`
- Mutation score of one core file: `scripts/mutate.ps1 -Mutate "**/Metrics.cs"`
- Release (verify, version in `Directory.Build.props`, CHANGELOG, publish; no commit, no tag):
  `scripts/release.ps1 -Version X.Y.Z [-WhatIf]`
- Take the kit's shared folders and settings keys after a change there: `scripts/sync.ps1`
  (`-Check` only compares)

## Layout

- `FlatCheck.Core/` - everything that computes and everything that formats. No Console, no UI,
  no entry point: a compile boundary. `Analysis/` decides numbers, `Report/` decides words
  (Romanian), `Runs/` chains load, analyze, report, map; both applications call `Runs/` only.
- `FlatCheck/` - the console, `flatcheck.exe`: options, dispatch, exit code, writing maps to disk.
- `FlatCheck.Wpf/` - the window: one Siril session folder in, tabs out. Formats, never decides.
- `FlatCheck.Tests/` (core + console, portable) and `FlatCheck.Wpf.Tests/` (window, Windows-only).
  `Golden/` holds the console snapshots; `testdata/manifest.json` hashes the real masters, which
  stay outside git. Every FITS fixture is generated in the test.
- `flatcheck.py` - the Python prototype, frozen at 2.2.0, and `crosscheck.py`, its comparison
  tool: neither is maintained or run (ADR 0047). Prior art, not a reference.
- `lensfun/` - the lensfun database, committed whole, copied beside the exe. `Icon/` - the icon.
- `kit.json` - what the kit's scripts, skills and agent need to know about FlatCheck (solution,
  test filter, code and test folders, test data, the two publish targets, what the auditor may
  also read, where the kit is). `docs/skills.md` - FlatCheck's additions to the kit's skills, one
  section per skill. `scripts/`, `.claude/skills/`, `.claude/agents/`, `.claude/rules/kit/`
  and the process keys of `.claude/settings.json` are the kit's, the same here (ADR 0048,
  ADR 0049; the kit's ADR 0028).
- `docs/requirements.md` - the behaviour, with IDs. `docs/decisions/` - ADRs. `docs/validation.md`
  + `.json` - figures on the real masters. `docs/design/` - the explanations, per area, read on
  demand. `docs/plans/` - approved plans. `README.md` - the user documentation, Romanian.
- The process rules are the kit's, in `.claude/rules/kit/process.md`; FlatCheck's own are in this
  file. The reasons behind the process are in `GHID.md` in the kit folder `ClaudeCodeStarterKit`
  beside this project in the `photo` repository (for people, not loaded).

## Rules

The process rules every project made from the kit follows are in `.claude/rules/kit/process.md`,
loaded with this file (the kit's ADR 0028). These are FlatCheck's own; one that extends or varies
a kit rule names it.

- **Golden files** (extends the kit's rule "No test edited to pass"). A golden file is never
  changed to make something pass either; golden files are re-approved only after the owner has
  read the diff.
- **Package notices** (extends the kit's rule "Packages"). A package also needs an entry in
  `THIRD-PARTY-NOTICES.md` with its GPLv3 compatibility argued, and one in `ThirdParty.cs`
  (`ThirdPartyTests` enforces both). The FITS reader and the PNG encoder stay hand-rolled.
- **Measured on the masters** (extends the kit's rule "Measured numbers"). Here the real data are
  the masters `docs/validation.md` lists.
- **Languages** (varies the kit's rule "Languages", ADR 0006). English in everything the model
  reads or writes about the process: docs, tests of invariants, scripts, commits, CHANGELOG.
  Romanian in everything the user sees: CLI text, `--help`, window labels, `README.md`, and in
  the existing code comments, which stay as they are. Never leave a file in two languages.
- **Computed only in the core.** Nothing is computed outside `FlatCheck.Core`: a new number,
  threshold or sentence of diagnosis goes in `Analysis/`, `Thresholds.cs` or `Report/`, never in
  a view-model or the console.
- **Code structure in design notes.** Do not describe code structure in this file; it goes
  stale. It goes in `docs/design/`.
- **Ratios.** Every metric is a ratio, so ISO and gain cancel. A metric that is not
  scale-invariant is a design smell. Thresholds are empirical rules of thumb in `Th`, never
  literals in code or text.
- **Formula changes re-validate.** A formula change (`Metrics.cs`, `Stats.cs`, `Analysis/`,
  `Report/`) re-runs `/validate` on the real masters before the task ends, with the drift table
  pasted; a new numeric branch gets a `ParityGoldenTests` scenario. There is no second
  implementation to compare against.
- **License headers.** Every source file (`.cs`, `.xaml`, `.py`) starts with the GPL notice;
  `LicenseHeaderTests` fails otherwise. Scripts carry the SPDX line.

## Workflow

The kit's workflow in `.claude/rules/kit/process.md` applies. FlatCheck adds:

- `/wrap-up` also runs `/validate` when a formula moved (the rule "Formula changes re-validate"),
  and checks `README.md`, the user documentation.
- `R###_...` tests may live in either test project; `RepoInvariantTests` looks in both.
- A release is tagged `flatcheck-vX.Y.Z`.
- The main checkout of the kit's rule "Worktrees" is the shared checkout `E:\Photografie\Code`.

## Read before touching

`docs/design/README.md` maps every area to its note. The ones that have caught a careless edit:

- `Metrics.RadialModel` / `RingFit`: `docs/design/radial-model-even-term.md` (five designs
  rejected; rings drawn by the model, not the flat).
- `SmoothAnalyzer`: `docs/design/smooth-validation.md` (two designs failed on real masters; the
  clip floor is load-bearing).
- Anything in `LensProfiles/`: `docs/design/lens-profiles.md` (two coordinate conventions,
  quoted from their sources; the calibration crop is part of a lens's identity).
- The Siril session rules of the window: `docs/design/two-applications.md` (two places for the
  master; the Ratio tab proposes nothing on purpose; the smoothed master is never the master).
- Any threshold or verdict wording: `docs/design/thresholds.md`.
- A "bug" in `known-issues.md` is probably deliberate; read it first.

## Style

- `.editorconfig` holds whitespace only; `dotnet format` enforces it, `IDE0005` removes unused
  usings. Warnings are errors in the gate. Code style analyzers and `Nullable` in the three
  application projects are off on purpose (ADR 0003); enabling them is a task of its own.
- Fix, do not suppress, unless the suppression carries a comment with the reason.
