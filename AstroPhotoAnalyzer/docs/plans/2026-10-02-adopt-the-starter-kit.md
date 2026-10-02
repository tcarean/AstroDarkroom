# 2026-10-02 Adopt the ClaudeCodeStarterKit process

Status: **approved** by the owner on 2026-10-02 ("Ok, aprob planul, trei commit-uri, fara
variabila. Vorbim de un singur fisier acuma. Vreau sa-l copiezi in
AstroPhotoAnalyzer.Tests/testdata/large/"), with two instructions that override the body below:

- **Three commits**, one per layer, as the body proposes.
- **No environment variable.** The reference image is one file, so it is copied into
  `AstroPhotoAnalyzer.Tests/testdata/large/` (git-ignored) instead of being reached through
  `APA_TESTDATA_LARGE`. The `kit.json` key `testData.largeEnv` is required by the kit's
  `kit-config.ps1`, so it keeps the name `APA_TESTDATA_LARGE` as an override nobody sets.
  Wherever the body says to set the variable or that it names `E:\Photografie\Claude`
  (decision 4, layer 1 step 5, layer 3 step 4, Appendices A, C, D.4, D.5 and D.6), the copy
  in `testdata/large/` is meant instead; ADR 0004 records the choice.

The implementing session fills "What deviated" at its wrap-up. The file was written in the
main checkout and moved into the task's worktree before the first step.

## Goal

Bring AstroPhotoAnalyzer onto the process the kit at `E:\Photografie\Code\ClaudeCodeStarterKit`
defines (`GHID.md`, "Migrarea unui proiect existent"), in the three layers the guide describes,
exactly as FlatCheck was migrated on 2026-09-25 (`../FlatCheck/docs/plans/2026-09-25-adopt-starter-kit.md`,
FlatCheck ADR 0001), and bring the documentation to the state it would have had if the process
had been used from the start: `CLAUDE.md` holds only the project's rules; the behaviour is in
`docs/requirements.md` with IDs; the decisions are in `docs/decisions/`; the explanations are in
`docs/design/`; the figures on the reference image are in `docs/validation.json`, read by a
test. **Nothing written today is deleted: it is moved.**

The difference from FlatCheck: there the 2709-line `CLAUDE.md` was the document to split. Here
`CLAUDE.md` is 201 lines and the document to split is `docs/specification.md` (727 lines), which
today is requirements, design and file map in one; `docs/implementation-plan.md` (231 lines)
holds the decisions and the lessons; `docs/acceptance-de-printat.md` (73 lines) holds the
acceptance figures.

## Read first (the implementing session, before any edit)

1. `../ClaudeCodeStarterKit/.claude/rules/kit/process.md` - the rules the project will follow
   from the first commit. Every rule is cited by its name in quotes, never by a number.
2. `../ClaudeCodeStarterKit/README.md` - "Ce e unde" and "Ce e impus mecanic".
3. `../ClaudeCodeStarterKit/GHID.md`, section "Migrarea unui proiect existent" (Romanian).
4. `../FlatCheck/CLAUDE.md` - the template for the new `CLAUDE.md` (136 lines; same shape here).
5. `../FlatCheck/docs/plans/2026-09-25-adopt-starter-kit.md` and `../FlatCheck/docs/decisions/0001-adopt-the-starter-kit-process.md`
   to 0007 - the same migration, one week ago, with what deviated.
6. `../FlatCheck/docs/design/README.md` (the index and the "Covers" rule), one note such as
   `../FlatCheck/docs/design/thresholds.md` (the header boilerplate of a moved note), and
   `../ClaudeCodeStarterKit/docs/design/README.md` (the exact Covers syntax `check-design.ps1`
   parses).
7. `../FlatCheck/docs/requirements.md` (the row format), `../FlatCheck/docs/validation.md` and
   `validation.json`, `../FlatCheck/FlatCheck.Tests/RepoInvariantTests.cs`, `ValidationTests.cs`,
   `TestData.cs`, `../FlatCheck/docs/skills.md`, `../FlatCheck/kit.json`.
8. `../ClaudeCodeStarterKit/scripts/kit-config.ps1` (every `kit.json` key), `check-design.ps1`
   and `check-decisions.ps1` headers (what the gate will check in `docs/`).
9. This project's `CLAUDE.md`, `docs/specification.md`, `docs/implementation-plan.md`,
   `docs/acceptance-de-printat.md`, `README.md`, `THIRD-PARTY-NOTICES.md`, whole.

## Measured before planning (2026-10-02, main at 68e3af8)

| | AstroPhotoAnalyzer |
| --- | --- |
| build Release | 9 s incremental, **0 warnings** in all five projects |
| tests | 236 (core + console) + 34 (window), green, 3 s + 4 s |
| `dotnet format --verify-no-changes`, no `.editorconfig` | whitespace in 3 files (`IccProfileInfo.cs`, `FileFactsTests.cs`, `ResultTabs.cs`) |
| the same with FlatCheck's `.editorconfig` (whitespace + `IDE0005`) | about 12 files: the three above plus unused usings and reflowed lines in `CropAnalyzer`, `NoiseAnalyzer`, `PrintSizeAnalyzer`, `PrintStretchSimulator`, `QuantizationAnalyzer`, `ImageSharpReader`, `StaFixture` and two csproj-level findings; measure again in layer 1 and record the count in ADR 0003 |
| `CLAUDE.md` | 201 lines; its "Conventions" repeat kit rules in other words (version, commits, dependencies, "do not commit") |
| `docs/` | `specification.md` 727 lines, `implementation-plan.md` 231, `acceptance-de-printat.md` 73; no index, no ADRs, no requirements with IDs, no validation read by tests |
| `Nullable` | disabled in Core, console and Wpf; enabled in both test projects |
| package versions | in the csproj files; no `Directory.Packages.props`, no lock files, no `global.json` |
| git hooks | installed at the root of the `photo` repository (`.githooks/`, `core.hooksPath` absolute); they pick up this project as soon as `scripts/verify.ps1` exists |
| stray files | `.github/copilot-instructions.md` (Azure boilerplate from Visual Studio, three lines); `.vs/` (ignored by the root `.gitignore`) |
| the reference image | `E:\Photografie\Claude\de_printat.png`, 103 387 067 bytes, present on this machine; not in the repository |
| environment variable prefix already in use | `APA_` (`APA_UPDATE_GOLDEN`, `APA_DUMP_SCREENS`) |
| Claude memory | one note about printing `de_printat.png` (the owner's taste, a test strip); about the image, not the tool; it stays |

## Decisions (each an ADR, accepted with the plan)

The migration's own, ADR 0001 to 0006:

1. **0001 Adopt the kit's process; the specification becomes requirements, decisions and design
   notes.** The analog of FlatCheck's 0001. `docs/specification.md` is split: behaviour into
   `docs/requirements.md`, decisions into `docs/decisions/`, mechanics into `docs/design/`, the
   file map into `docs/design/code-map.md`; then it is removed, since every section has a home.
   `docs/implementation-plan.md` moves unchanged to `docs/plans/2026-09-20-implementation-plan.md`
   (a plan is history, never updated). `docs/acceptance-de-printat.md` becomes
   `docs/validation.md` plus three cases in `docs/validation.json`. `CLAUDE.md` is cut to the
   rules, the commands and the pointers; the process rules are the kit's, in
   `.claude/rules/kit/process.md`. The rule "User-facing documentation lives in `README.md`
   only" stays a `CLAUDE.md` rule, with its one-line why.
2. **0002 Projects stay at the folder root; the kit's values for this project are in
   `kit.json`.** The five projects stay where they are (the kit's ADR 0017: a project
   describes itself in `kit.json`); the values: solution, test filter `Category!=Validation`,
   the three code folders, the two test folders, test data under
   `AstroPhotoAnalyzer.Tests/testdata/` with `APA_TESTDATA_LARGE`, Stryker from
   `AstroPhotoAnalyzer.Tests/`, two publish targets (the console as one managed single file,
   the window as a folder), the auditor may also read `THIRD-PARTY-NOTICES.md` and
   `AstroPhotoAnalyzer.Tests/Golden/`.
3. **0003 Gate strictness: whitespace and `IDE0005` by `dotnet format`, warnings as errors on
   the command line; code style analyzers and `Nullable` stay off in the application
   projects.** The same as FlatCheck's 0003, with this project's numbers: 0 warnings today, one
   `dotnet format` pass over about 12 files, no behaviour change (the goldens and the 270
   tests are the proof). Reproducibility from the kit in full: `global.json` (SDK 9.0.318,
   which builds net8.0), `Directory.Packages.props`, `packages.lock.json` per project,
   `Deterministic`.
4. **0004 Validation on the reference image runs outside the gate, on the file where it is.**
   `de_printat.png` stays in `E:\Photografie\Claude\`; `APA_TESTDATA_LARGE` names that folder;
   the manifest lists the file with its SHA-256; `ValidationTests` carry
   `Category=Validation`; a run is about 11 s per case, so `/validate` is cheap but still not
   in the gate, because the file exists on one machine (a gate that passes without the data
   and fails with it is not one gate; FlatCheck's 0004).
5. **0005 Romanian for what the user sees and for the code comments, without diacritics in code
   and messages; English for the process files** (varies the kit's rule "Languages", like
   FlatCheck's 0006). Markdown for people (`README.md`, `THIRD-PARTY-NOTICES.md`) keeps its
   diacritics; everything the model reads or writes about the process (`docs/`, `CHANGELOG.md`,
   the tests of invariants, commits) is English; a file is never left in two languages, so a
   comment added to an existing Romanian source file is Romanian, and the comments added to
   `Directory.Build.props` and the csproj files in this task are Romanian.
6. **0006 The changelog starts at 1.0.0; the git hooks are not copied; root CI is deferred; the
   Copilot file is deleted.** FlatCheck's 0007 word for word, with `[1.0.0] - 2026-09-21`
   summarising what 1.0.0 holds (the eleven commits of 2026-09-20/21).

Retroactive, ADR 0007 to 0024, written from the sources named (each says "Retroactive:
recorded on 2026-10-02 from <source>; the decision itself dates from <phase or pass>" in its
Context, and ends with a "Source:" line):

| ADR | Title (short statement of the decision) | Sources |
| --- | --- | --- |
| 0007 | One core library computes and formats; the console and the window only call `Runs/`; core types are internal and the public view-models cannot expose them (`Rows.cs`) | spec §1 end, §2 "Invariants", CLAUDE.md "Nothing is computed outside the core", "The two applications", "The window"; impl-plan "The window adds no number", "View-models are public" |
| 0008 | The input is a finished, display-referred image: linear data is refused at `LinearMedianMax`, nothing is exported, the print background is a labelled gamma simulation, colour is not managed | spec §1 "What it is not", §3 "Refusal of linear data"; CLAUDE.md "The image is display-referred and final"; impl-plan "Decisions taken before the first line" (input state, exports) |
| 0009 | `SixLabors.ImageSharp` 3.1.11 decodes TIFF, PNG and JPEG and encodes JPEG and TIFF for the export table, under the Six Labors Split License, pinned, reached from two core files; the FITS reader is hand-written | CLAUDE.md "The dependencies"; spec §13; `THIRD-PARTY-NOTICES.md`; the Core csproj comment; impl-plan "image library"; FlatCheck ADR 0010 as the model (the licence reasoning is the same; here the package is not peripheral) |
| 0010 | Pixels are `float[]` planes in 0..1 with the green plane as the reference channel; percentiles follow numpy's linear interpolation; the random source is a seeded xorshift64\* | spec §3 "The raster", "Statistics"; impl-plan "pixel storage", "FITS reader copied from FlatCheck" |
| 0011 | Every recommendation carries its evidence (`Because`), topics are in decision order, verdicts are monotone `Ok < Attention < Problem`, and the exit code follows the verdict | spec §6 head, §9 exit codes; CLAUDE.md "Every recommendation carries its evidence", exit codes; impl-plan "The report" |
| 0012 | Thresholds are empirical rules of thumb kept in `Thresholds.cs` with their provenance; a literal a window text needs is lifted there first, even one no rule reads | spec §7 head, §15 "An explanatory text"; CLAUDE.md "Thresholds are empirical", the `ScreenBackground*` sentence; impl-plan "The explanation pass" |
| 0013 | "Fundal" names the sky; its level (Tonuri, `BackgroundMedian`) and its colour (Culoare, `BackgroundResult.Levels`) are two measurements named as such, and no tab is called Fundal | CLAUDE.md "Fundal names one thing"; spec §10 "Layout"; impl-plan "The naming pass"; commit 7bcdaf5 |
| 0014 | The background level is the median of the reference channel (right when the sky dominates); the background colour is the darkest quarter of the stride-3 subsample above the bottom 25 % of rows, selected with `<=` the percentile | spec §5 B1, E1, §14 first bullet; impl-plan phase 2 notes ("Background for the simulation", "Background mask uses <=", "Corner regions per channel") |
| 0015 | Two tile criteria on purpose: wide (range up to 23.3 levels, seed 0, 400 candidates) for the noise sigma, strict (range < 6, mean < 70, seed 1, 120 tiles in 5000 attempts) for the plateau | impl-plan phase 2 "Two different tile criteria"; spec §5 B3, C1 |
| 0016 | Star candidates are every local maximum of a 25 x 25 window above the plane mean plus the minimum amplitude, not a random 20 000 above 3 x mean; the half-max estimator is kept with its known +2.5 % bias | impl-plan phase 3 and phase 4 notes; spec §5 D1 "Known"; acceptance "star FWHM" row |
| 0017 | The natural viewing distance cannot pick a size: the test carries a 15 % tolerance there, no row is recommended without an explicit distance, and without stars the dpi floor (the physical one from a given distance, else 180 dpi) is stated and nothing is recommended | spec §5 D3 the two bold paragraphs; acceptance "Run without --distance"; `NaturalDistanceTolerance`, `FallbackMinDpi` comments |
| 0018 | Edge regions that depart in colour are a problem to fix; regions that brighten neutrally are the object and only informative | spec §5 E2, §6 Colour rows; acceptance "Edge regions that brighten neutrally"; impl-plan "Edge-region verdicts changed" |
| 0019 | JPEG verdicts use the max error and the share of samples over two levels, never the mean error; the sizes and errors are ImageSharp's encoder's; TIFF LZW is measured with the horizontal predictor | spec §5 C3 "Measured and relied upon"; impl-plan phase 3 and 4 notes on JPEG; acceptance "ImageSharp's LZW needs the horizontal predictor" |
| 0020 | The window explains and never decides: each metric row's `Description`, each table's `GuideParagraph`s, each preview tile's `Check` and each field's `*Help` are written in the view-models with every number interpolated; tables are pre-aligned mono strings; parameters are text fields parsed once at "Analizeaza" | spec §10 (tabs table, "Tables are pre-aligned", "Presentation"), §15 "An explanatory text"; impl-plan phases 5-7 notes, "The explanation pass"; CLAUDE.md "The window" |
| 0021 | Every number is formatted through `Fmt` with the invariant culture, the WPF project has no `InvariantGlobalization` and forces the culture at startup, and the window's `Raport.txt` is byte-identical to the console's output, banner included | spec §8 "Every number goes through Fmt", §10 "Culture"; the Wpf csproj comment; impl-plan "The saved Raport.txt" |
| 0022 | The version lives in `Directory.Build.props` only, `VersionInfo` reads it from the assembly, and every run prints the banner and the copyright as its first two lines; the owner bumps (the kit's ADR 0029) | CLAUDE.md "Versioning"; spec §12; `VersionTests`; `Directory.Build.props` comment |
| 0023 | The GPL notice on every source file, XAML included, with an explicit project list in `LicenseHeaderTests`; "Despre" and `--help` carry the §5(d) warranty text; a package needs `THIRD-PARTY-NOTICES.md` and `ThirdParty.cs`, bound to the csproj by `ThirdPartyTests`, and the README table by hand | CLAUDE.md "Licensing", "A new package means three places"; spec §12, §13; FlatCheck ADR 0040 as the model |
| 0024 | The window is tested by rendering its content with a real result on one STA thread per process; an unshown `Window` renders transparent; `APA_DUMP_SCREENS` saves the renders | spec §11 window row; impl-plan "WindowSmokeTests renders the window's content"; CLAUDE.md "The window" last sentence |

If, while writing, two of these turn out to be one decision, merge them and renumber before
the commit (numbers are assigned once only after they are on `main`); if a source holds a
decision not listed here, add it at the end and say so under "What deviated". Twenty-four is
the expected count, not a target.

## Ground rules for the implementing session

- **Where.** Open the session in `E:\Photografie\Code\AstroPhotoAnalyzer` (the project folder
  of the main checkout, never the git root, never the desktop app's worktree option: the kit's
  ADR 0027). Before anything, `git worktree list` and `git status`: another session on the same
  task, or a dirty tree, is a stop. Then make the task's worktree and branch in one command,
  from the project folder:

  ```bash
  git worktree add .claude/worktrees/Adopt-the-starter-kit-process -b claude/Adopt-the-starter-kit-process main
  ```

  Every edit, build, test and commit happens under
  `E:\Photografie\Code\AstroPhotoAnalyzer\.claude\worktrees\Adopt-the-starter-kit-process\AstroPhotoAnalyzer\`
  (the worktree holds the whole `photo` repository; `../ClaudeCodeStarterKit` inside it is the
  kit at the same `main`). Copy this plan there first:
  `docs/plans/2026-10-02-adopt-the-starter-kit.md`. The root `.gitignore` already ignores
  `**/.claude/worktrees/`.
- **The git hooks are live from the first commit that adds `scripts/verify.ps1`:** the root
  `pre-commit` runs `scripts/verify.ps1` in this project (expect about a minute), refuses a
  session's commit on a `claude/` branch in the main checkout (hence the worktree), and
  `commit-msg` refuses a message that is not one line of at most 100 characters. The Claude
  Code hooks of `.claude/settings.json` are loaded at session start, so they are not live in
  the implementing session; the git hooks gate the commits anyway.
- **Never edit** `scripts/`, `.claude/skills/`, `.claude/agents/` or `.claude/rules/kit/` in
  this project, nor the process keys of `.claude/settings.json`: they are the kit's, copied.
  If a script cannot do what this project needs, that is a kit change, a separate task; say so.
- **Nothing is deleted, it is moved.** Every paragraph of `specification.md`,
  `implementation-plan.md` and `acceptance-de-printat.md` has a destination in the mapping
  tables below. Do not improve the prose while moving it; do not de-duplicate between a
  requirement row and a design note in this task (FlatCheck did that two days later, its ADR
  0051; it is a task of its own). Pointers are the only new sentences.
- **Never bump the version, never roll the year, never commit on `main`.** The branch's commits
  are the session's (the kit's ADR 0021): one line, English, at most 100 characters, saying
  why; no body, no trailers. The merge is proposed and asked at the end.
- **Languages.** English in `docs/`, `CHANGELOG.md`, `kit.json`, the new tests, the commits;
  Romanian without diacritics in a comment added to an existing source file, csproj or
  `Directory.Build.props`; Romanian with diacritics in `README.md`.
- **Editing long files.** Do not rewrite a 700-line file through a heredoc: a Python or
  PowerShell edit script written to the scratchpad first, or the Edit tool, keeps `\n` and
  backticks intact. Use `git mv` for the two file moves so history follows.
- **Subagents.** The retroactive ADRs (18) and the design notes (11) are independent writing
  jobs from fixed sources; FlatCheck used three subagents for its 34 ADRs. Give each subagent
  this plan's table rows, the ADR template and one finished ADR as the model, and review every
  file before it is committed. Requirements rows are better written by the main session, in one
  pass, so the IDs stay consistent with the tests renamed in layer 3.
- **Commits.** Three on the branch, one per layer, each with the gate green (FlatCheck did one
  at the owner's instruction; three are easier to review; the owner may say otherwise at
  approval):
  1. `Adopt the kit's gate: scripts, kit.json, settings, changelog, lock files, one format pass`
  2. `Split the specification into requirements, decisions and design notes; CLAUDE.md to the rules`
  3. `Tie requirements to tests: RepoInvariantTests, ValidationTests on de_printat, first R IDs`
- **Checkpoints that need the owner** (stop and ask; everything else is decided here):
  the plan's approval (which accepts the 24 ADRs); `de_printat.png` not found at the path
  above; a `dotnet format` change that is not whitespace, usings or line reflow; a source
  paragraph with no destination in the mapping tables; the `/docs-health` report (relayed,
  nothing fixed); the merge.

## Layer 1: the gate (about an hour, no code)

Every path below is relative to the worktree's `AstroPhotoAnalyzer/` folder.

1. **Bootstrap the shared folders.** Copy `../ClaudeCodeStarterKit/scripts/` to `scripts/`
   without the kit-only files (`rename.ps1`, `install-hooks.ps1`, `hooks/pre-commit`,
   `hooks/pre-merge-commit`, `hooks/commit-msg`); keep `hooks/claude-commit-gate.ps1`,
   `hooks/claude-track-edits.ps1`, `hooks/claude-stop-gate.ps1`. Write `kit.json` (Appendix B,
   verbatim). Then run, in the project:

   ```bash
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/sync.ps1
   ```

   It mirrors `scripts/` (kit-only files are neither copied nor deleted), copies
   `.claude/skills/`, `.claude/agents/`, `.claude/rules/kit/` and, since the project has no
   `.claude/settings.json`, the kit's copy of it. Then `scripts/sync.ps1 -Check` must exit 0.
   `.claude/settings.local.json` (one local grant) stays; it is git-ignored.
2. **`.claude/settings.json`**: replace its `$comment` with the project's (Appendix D.1: the
   FlatCheck comment with the name changed). Add no `allow` entries: the project's commands are
   covered by the kit's list. `sync.ps1 -Check` must still exit 0 (the comment is the
   project's).
3. **Reproducibility files.**
   - `global.json`: copy FlatCheck's (`9.0.318`, `latestPatch`).
   - `Directory.Packages.props`: new, Appendix D.2; then remove the `Version="..."` attribute
     from the five `PackageReference` lines in `AstroPhotoAnalyzer.Core.csproj`,
     `AstroPhotoAnalyzer.Tests.csproj` and `AstroPhotoAnalyzer.Wpf.Tests.csproj` (central
     package management refuses a version in a csproj). `ThirdPartyTests` reads only the
     `Include` names, so it is unaffected; add one Romanian sentence to the Core csproj comment
     saying the version now lives in `Directory.Packages.props`.
   - `Directory.Build.props`: add the second `PropertyGroup` of FlatCheck's (`Deterministic`,
     `ContinuousIntegrationBuild`, `ManagePackageVersionsCentrally`,
     `RestorePackagesWithLockFile`, `RestoreLockedMode`, `SatelliteResourceLanguages`) with a
     Romanian comment ("Reproductibilitate, ADR 0003: ... strictetea o aplica
     scripts/verify.ps1 din linia de comanda, ca un build din IDE sa ramana ingaduitor").
     Leave `<Version>1.0.0</Version>` and the copyright untouched.
   - `dotnet restore AstroPhotoAnalyzer.sln` writes a `packages.lock.json` in each of the five
     project folders; they are committed.
   - `.editorconfig`: FlatCheck's verbatim (whitespace rules, `IDE0005` as warning; the ADR
     number in its comment is 0003 here too).
   - `.gitattributes`: FlatCheck's plus `*.tif binary`, `*.jpg binary`, `*.jpeg binary` (the
     root `.gitattributes` has them already; the project's copy is the one a standalone clone
     would keep).
   - `.gitignore`: FlatCheck's lines minus `__pycache__/`, with the test data line
     `AstroPhotoAnalyzer.Tests/testdata/large/` and, since the root `.gitignore` covers
     `*.tiff` but not `*.tif` or `*.png`, no sample-image lines here either: sample images are
     kept out by hand, as today (CLAUDE.md "Committing").
   - `.config/dotnet-tools.json`: FlatCheck's (Stryker 4.16.0).
     `AstroPhotoAnalyzer.Tests/stryker-config.json`: the kit's with
     `"project": "AstroPhotoAnalyzer.Core.csproj"`.
4. **`CHANGELOG.md`**: Appendix D.3 (an empty `[Unreleased]`, a `[1.0.0] - 2026-09-21` summary,
   earlier history in `git log`).
5. **Test data.** Create `AstroPhotoAnalyzer.Tests/testdata/` with `README.md` (Appendix D.4)
   and `manifest.json` (Appendix D.5, the hash computed by the script): set
   `$env:APA_TESTDATA_LARGE = 'E:\Photografie\Claude'` in the session, then

   ```bash
   powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-testdata.ps1 -Update
   ```

   and read the manifest back (one entry, `location` `large`, `requiredInCi` false, the
   SHA-256 of the 103 387 067-byte file). Ask the owner to set `APA_TESTDATA_LARGE` at user
   level (`setx`) so `/validate` works in later sessions; without it the gate only prints a
   yellow warning.
6. **Delete** `.github/copilot-instructions.md` (then the empty `.github/` folder). Root CI is
   not added (ADR 0006).
7. **Format pass.** `dotnet format AstroPhotoAnalyzer.sln`, then `git diff --stat` and read
   the diff: whitespace, removed usings and reflowed lines only. Record the file count for
   ADR 0003. Anything else is a checkpoint.
8. **Gate.** `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`. Steps
   0 to 7 green: `kit files` (sync -Check), `test data manifest`, `decisions` (yellow: no
   folder yet), `design notes` (passes without `docs/design/`), restore, format, build with
   `-warnaserror`, 270 tests. Paste the last ten lines in the session.
9. **Commit 1** on the branch (the git pre-commit hook runs verify again; the commit-msg hook
   checks the line). Report the hash.

## Layer 2: the documentation (one long session, no code)

### 2.1 `docs/requirements.md`

Format: FlatCheck's exactly (the header with the rules for entries, then one table per area,
columns `ID | Status | Requirement`, then `## Dropped`). Rules of this file:

- IDs in blocks of ten, one block per area, assigned once; gaps are deliberate.
- Status `accepted` for every row (the behaviour is agreed and in the code); layer 3 moves the
  rows whose tests get an `R###_` name to `implemented`. No row is `draft`.
- A row states observable behaviour: inputs, outputs, the flags on the record, the thresholds
  **by their `Thresholds` name and their value on 2026-10-02**, and one concrete example with
  numbers (the acceptance figures from `acceptance-de-printat.md` and the examples already in
  the spec are the examples to use). No adjectives. The error behaviour and the exit code are
  rows too.
- A row says *what*; the design note of the area keeps *how* and *why*. When a spec paragraph is
  both, the row is a condensed restatement and the paragraph moves whole to the note.
- Thresholds are quoted as `` `Thresholds.PaperBlackOkPct` (2 %) ``.

The blocks and the rows (write them in this order; the titles below are the content each row
must carry, not its text; a row may be split in two when it would exceed about ten lines, and
two may be merged when one example covers both; keep the block):

**Console contract (R-001 to R-009)**, from spec §9, §8 head, CLAUDE.md "Versioning", "The two
applications":
- R-001 the banner `AstroPhotoAnalyzer <version>` and the copyright on lines 1-2 of every run,
  `--help` and failures included; the version is `<Version>` of `Directory.Build.props`; no
  `--version` flag.
- R-002 exit codes 0 / 1 / 2 / 3 and every cause of 1 (bad argument, invalid parameter
  combination, missing file, a folder, unreadable format, refused linear image, `-o` not
  writable); monotone.
- R-003 the usage line; flags before or after the image; exactly one positional argument;
  `Options.Parse` never throws; a bad value, a missing value, an unknown option, two images are
  `EROARE: ...` on stderr with exit 1 (example from `Golden/bad-option.txt`).
- R-004 `--help`, `-h` and no arguments print the usage, the GPLv3 warranty lines
  (`RunReport.WarrantyLines`) and the pointer to `LICENSE`; the exit code as
  `Golden/help.txt` records it.
- R-005 `-o | --out <file>` writes `ReportWriter.WriteFull` (banner + body) and prints
  `    -> raport salvat: <file>`; the saved file is byte-identical to what the window saves.
- R-006 the console and the core run under `InvariantGlobalization`: `.` as the decimal
  separator whatever the Windows culture; the test host too, so the goldens compare against
  the console's formatting.
- R-007 the progress stages, in order (`citire`, `fisier`, `tonuri`, `zgomot`, `fundal`,
  `stele`, `cuantizare`, `JPEG`, `raport`), through `IProgress<string>`, no percentage.

**Inputs (R-010 to R-019)**, from spec §3:
- R-010 reader by extension, never by sniffing; the accepted extensions; an unknown one is an
  error naming them.
- R-011 FITS: first HDU with NAXIS 2 or 3, BITPIX 8/16/32 with BZERO/BSCALE and -32/-64, NAXIS3
  1 or 3 else refused, `ROWORDER = 'BOTTOM-UP'` flips, empty primary HDUs skipped.
- R-012 TIFF/PNG/JPEG: colour as `Rgb48`, mono as `L16`; bit depth and channel count from the
  format's own metadata (PNG `BitDepth`/`ColorType`, TIFF `BitsPerSample`/`Photometric`, JPEG
  8); compression name; the ICC profile from the decoded image (`Metadata.IccProfile` or
  `Frames[0]`); palette and multi-page files converted and noted.
- R-013 the raster: 1 or 3 `float[]` planes in 0..1, row 0 at the top; normalisation per bit
  depth; clipping to [0, 1] counted in the note; `Reference` = green on colour, the only plane
  on mono.
- R-014 `SourceInfo`: every field, `IccDescription` from the `desc` tag (v2 text or v4 mluc,
  null when absent or malformed), `FileBytes`, `Note`.
- R-015 the linear refusal: median of the reference plane at stride 8 below
  `Thresholds.LinearMedianMax` (0.02) raises `RefusedImageException` with the median in the
  message; the console exits 1 (`Golden/linear-fits.txt`), the window shows the error card.
- R-016 colour maths: sRGB per IEC 61966-2-1, Y = Rec. 709 on linear values, L\* per CIE 1976
  with the linear segment; paper black = L\*(10^-Dmax): baryta 2.0 -> 9.0, canvas 1.5 -> 20.7,
  acrylic 2.2 -> 5.7; `YOfLstar` the exact inverse; `GrayLevelOfLstar` / `LstarOfGrayLevel`.
- R-017 percentiles with numpy's linear interpolation; `SeededRandom` deterministic on every
  runtime.

**Parameters (R-020 to R-029)**, from spec §4:
- R-020 the medium and Dmax: `baryta|canvas|acrilic` (case-insensitive names), Dmax 2.0 / 1.5
  / 2.2, `--dmax n` with 0 < n <= 4 overriding; `Parameters.Dmax` derived.
- R-021 the defaults: widths 50..100, passepartout 7, frame 3, distance null (= 1.5 x the
  diagonal per row), `--sizes a,b,c`.
- R-022 `--aspect w:h` (also `wxh`), null = native.
- R-023 `--target-bg 0..1` default 0.19; `SimulationTargets` 0.15 and 0.20 fixed; `Seed` 0,
  neither exposed.
- R-024 the console and the window validate syntax only, invariant culture; a comma decimal is
  an error that says to use the point; neither holds a copy of the defaults (an unset field is
  the core's default; `CliTests.OptionsMapOntoParametersWithoutACopyOfTheDefaults`).
- R-025 `Parameters.Validate()`: the passepartout is at least
  `Thresholds.PassepartoutMinFrameRatio` (2) x the frame, 0 means none and is not checked;
  the console exits 1 before any reading, the window shows the core's message.
- R-026 the window's help texts (`DistanceHelp`, `AspectHelp`, `TargetBgHelp`,
  `PassepartoutHelp`, `FrameHelp`) as tooltips on the field and its label, two to four
  paragraphs separated by a blank line, and what each one must say (the spec's §4 paragraph
  lists it: the 1.5 x rule and its origin, the native ratio and the fixed-format labs, the
  target level with `ScreenBackground*` / `PrintBackground*`, gamma = ln(target) /
  ln(background), the origin of 0.19); every number interpolated.

**File facts, bit depth, crop (R-030 to R-039)**, from spec §5 A1-A3.
**Tonal distribution and the simulation (R-040 to R-049)**, from §5 B1, B2, B4 (four rows:
levels, L\* and flags, the ladder, the simulation with the 0.103 -> 0.19 example).
**Noise (R-050 to R-059)**, from §5 B3 (one row; the four flags and `Insufficient`).
**Background colour and edge regions (R-060 to R-069)**, from §5 E1, E2 (two rows; the two
reference-image corners as the example).
**Stars and print size (R-070 to R-079)**, from §5 D1, D2, D3: D1 candidates and the estimator;
D2 regions and `SoftCorners`; D3 the size table formulas and `ReferenceRow` / `CanRecommend`;
the natural-distance rule (no recommended row, the two footer lines, `NaturalRatio` 1.018 on
the reference image); the no-stars rule (the dpi floor, nothing recommended, the legend); the
`DpiLadder` reference scale (window only).
**Quantization and JPEG (R-080 to R-089)**, from §5 C1, C2, C4, C3 (four rows).
**Recommendations (R-090 to R-099)**, from §6: R-090 the record, the non-empty `Because`,
the monotone verdicts, section and overall maxima, the six topics in decision order; then one
row per topic (Format, Background, Colour, Size, Denoise, Warnings) carrying the conditions,
verdicts and the gist of the text from the spec's table, and the noise-after-restretch slope
formula under Denoise.
**The report (R-100 to R-109)**, from §8: R-100 `WriteFull` / `WriteBody` and the body layout
(the code block of §8, verbatim) with "(nimic de semnalat)" and the six appendices; R-101
every number through `Fmt` (`F0..F4`, `S1`, `Pct`, `Mb`, `Int`, `Dim`), NaN as `n/a`, Romanian
without diacritics, the format pinned by the goldens.
**The window (R-110 to R-119)**, from §10: R-110 the layout, starts maximised, the file from
the command line or the open dialog, F1 = Despre; R-111 `MainViewModel`'s contract (the
properties, `OpenAsync`, `RunAsync` on a pool thread, `Error` from the joined inner messages
without a stack, `SaveReport`, `DefaultReportPath` = `Raport.txt` beside the image, opens no
dialogs); R-112 to R-117 one row per tab (Raport, Tonuri, Export, Rezolutie, Culoare,
Preview) with what it shows, from the spec's tabs table, including "! colorat" / "! neutru" and
the Preview step-back rule; R-118 the culture (`App.ForceInvariantNumbers`, no
`InvariantGlobalization` on the WPF project, the report identical under `ro-RO`); R-119
"Despre": version, copyright, the warranty lines, the third-party list from
`ThirdParty.Components`.
**Process and legal (R-120 to R-129)**, from §12, §13, CLAUDE.md: R-120 the version in
`Directory.Build.props` only, no csproj overrides, `VersionInfo` from the assembly; R-121 the
GPL notice on every `.cs`, `.xaml` and `.py` in the five projects, `LICENSE` verbatim; R-122
the package triad (csproj, `THIRD-PARTY-NOTICES.md`, `ThirdParty.cs`) bound by
`ThirdPartyTests`, the README table by hand; R-123 the two publish commands, the console as one
pure-managed single file, the window as a folder.

About 65 rows. The file's header says that the spec was split on 2026-10-02 (ADR 0001) and
that `accepted` is the state of most rows since then, as FlatCheck's does.

### 2.2 `docs/decisions/`

Copy `../ClaudeCodeStarterKit/docs/decisions/0000-template.md`. Write 0001 to 0006 from this
plan, 0007 to 0024 from the table above (subagents, reviewed). Header format, letter for
letter (`check-decisions.ps1` is a gate step): line 1 `# NNNN. <title>`, line 2 blank, line 3
`Date: 2026-10-02`, line 4 `Status: accepted 2026-10-02`, line 5 blank; no `Supersedes` line
(nothing is superseded). Sections `## Context`, `## Decision`, `## Alternatives rejected`,
`## Consequences`; one `## Decision` per file. Numbers cited as `ADR 0009` mean this project's;
the kit's are always `the kit's ADR NNNN`. A retroactive ADR's Context starts with
"Retroactive: recorded on 2026-10-02 from <file and section>; the decision itself dates from
<phase>" and the file ends with `Source: <files>`. Consequences name the tests that guard the
decision (the goldens, `RecommendationsTests`, `CultureTests`, `VersionTests`,
`LicenseHeaderTests`, `ThirdPartyTests`, `WindowSmokeTests`, `ParameterFieldsTests`, the
analyzer tests by file).

### 2.3 `docs/design/`

The index `README.md` first (FlatCheck's text adapted: the first paragraph says the notes were
`docs/specification.md` §2-§16 and `docs/implementation-plan.md`'s notes until 2026-10-02, ADR
0001, moved unchanged; the Covers rule; the table with three columns `Area | Note | Read before
editing`; the closing paragraph pointing at `../decisions/`, `../requirements.md`,
`../validation.md`). Every note starts with the quote-block boilerplate FlatCheck's notes use:

> Moved unchanged from `docs/specification.md` §N (and `docs/implementation-plan.md`, "...") on
> 2026-10-02 (ADR 0001). Where a section became a requirement row or a decision record, it is
> now a pointer. **Covers:** `path`, `path`, ... . A change to the code this describes means
> this note is updated in the same task, or deleted with the decision it explains.

The Covers sentence: paths in backticks, relative to the project root or to a code or test
folder of `kit.json`; a folder only when the note describes its files; no "e.g." and no other
full stop before its end (the sentence ends at the first `. `); a class or method name is
allowed but is not a path. `check-design.ps1` fails on a path git does not have, on a note
missing from the index and on a note without the sentence.

| Note | Moves in, unchanged | Covers |
| --- | --- | --- |
| `pipeline.md` | spec §2 whole (the project table, the invariants, the chain, the stages, `AnalysisResult`, the timing) | `AstroPhotoAnalyzer.Core/Runs/AnalysisRun.cs`, `AstroPhotoAnalyzer.Core/Input/Raster.cs`, `AstroPhotoAnalyzer.Core/Input/RasterOps.cs`, `AstroPhotoAnalyzer.Core/Input/Stats.cs`, `AstroPhotoAnalyzer.Core/Input/ColorMath.cs` |
| `input.md` | spec §3 whole; impl-plan "Things measured about ImageSharp 3.1.11" (the decoder items; the encoder items go to `dependencies.md` by pointer) | `AstroPhotoAnalyzer.Core/Input/IFrameReader.cs`, `ImageLoader.cs`, `FitsReader.cs`, `ImageSharpReader.cs`, `IccProfileInfo.cs`, and the hand-built fixtures `AstroPhotoAnalyzer.Tests/FitsTestData.cs`, `TiffTestData.cs`, `PngTestData.cs`, `IccTestData.cs` |
| `measurements.md` | spec §5 whole (A1 to C3, every analyzer); impl-plan "Notes from phase 2", "phase 3", "phase 4" (the star threshold, the JPEG histogram); the acceptance's "FWHM in the centre region reads 4.27" and "The plateau tiles are scarce" | `AstroPhotoAnalyzer.Core/Analysis/` (the note describes every file of the folder) |
| `thresholds.md` | spec §7 whole (the table) | `AstroPhotoAnalyzer.Core/Thresholds.cs` |
| `recommendations-and-report.md` | spec §6 whole (the table and the slope formula), §8 whole | `AstroPhotoAnalyzer.Core/Report/Recommendation.cs`, `Recommendations.cs`, `ReportWriter.cs`, `Fmt.cs`, `RunReport.cs` |
| `console.md` | spec §9; §15 "A new parameter", "A new print medium"; impl-plan "Golden scenes are synthetic and deterministic"; the golden mechanics (`RunCli` scrubs the banner and the temp path, `APA_UPDATE_GOLDEN=1`) | `AstroPhotoAnalyzer/Program.cs`, `Options.cs`, `AnalyzerCli.cs`, `AstroPhotoAnalyzer.Core/Parameters.cs`, `AstroPhotoAnalyzer.Tests/GoldenOutputTests.cs`, `Scenes.cs` |
| `window.md` | spec §10 whole; §15 "An explanatory text in the window", "A new tab"; impl-plan "Notes from phases 5-7" whole (the explanation pass and the naming pass included; ADR 0013 and 0020 get pointers) | `AstroPhotoAnalyzer.Wpf/App.xaml.cs`, `MainWindow.xaml`, `Theme/Dark.xaml`, `Interop/DarkTitleBar.cs`, `Presentation/`, `ViewModels/`, `Views/`, and the harness `AstroPhotoAnalyzer.Wpf.Tests/StaFixture.cs`, `WindowSmokeTests.cs` |
| `extending.md` | spec §15 whole, as the one checklist (the per-area notes point at it; its "see CLAUDE.md" for a dependency becomes "see `dependencies.md` and ADR 0009"; its "§N here" become "R-NNN and the note of the area") | the places each kind of change touches: `AstroPhotoAnalyzer.Core/Runs/AnalysisRun.cs`, `AstroPhotoAnalyzer.Core/Report/Recommendations.cs`, `AstroPhotoAnalyzer.Core/Parameters.cs`, `AstroPhotoAnalyzer/Options.cs`, `AstroPhotoAnalyzer.Core/Input/ImageLoader.cs`, `AstroPhotoAnalyzer.Wpf/ViewModels/ParameterFields.cs`, `AstroPhotoAnalyzer.Wpf/ViewModels/ResultTabs.cs`, `AstroPhotoAnalyzer.Wpf/MainWindow.xaml` |
| `dependencies.md` | spec §13; CLAUDE.md "The dependencies" and "SixLabors.ImageSharp" whole (the rule sentences become a pointer to the kit's rule "Packages" and this project's "Package notices"; the licence reasoning a pointer to ADR 0009; the four measured behaviours and the hand-built-fixture reason stay in full); impl-plan "image library" row | `AstroPhotoAnalyzer.Core/AstroPhotoAnalyzer.Core.csproj`, `Directory.Packages.props`, `AstroPhotoAnalyzer.Core/ThirdParty.cs`, `AstroPhotoAnalyzer.Core/Input/ImageSharpReader.cs`, `AstroPhotoAnalyzer.Core/Analysis/JpegAnalyzer.cs`, `AstroPhotoAnalyzer.Tests/ImageFiles.cs` |
| `testing.md` | spec §11 whole (the table and the command) | `AstroPhotoAnalyzer.Tests/SyntheticRasters.cs`, `Scenes.cs`, `ImageFiles.cs`, `TempWorkspace.cs`, `AstroPhotoAnalyzer.Wpf.Tests/AstroPhotoAnalyzer.Wpf.Tests.csproj` (the linked fixtures) |
| `known-limitations.md` | spec §14 whole | `AstroPhotoAnalyzer.Core/Analysis/TonalAnalyzer.cs`, `StarAnalyzer.cs`, `BackgroundAnalyzer.cs`, `JpegAnalyzer.cs`, `QuantizationAnalyzer.cs`, `AstroPhotoAnalyzer.Wpf/ViewModels/ResultTabs.cs` |
| `code-map.md` | spec §16 whole; CLAUDE.md "Structure" bullets that say more than the new Layout does | `AstroPhotoAnalyzer.Core/`, `AstroPhotoAnalyzer/`, `AstroPhotoAnalyzer.Wpf/` (one line per file; the tests are listed by pointer only, so the test folders are not in the sentence) |

Spec §1 goes nowhere: its first paragraph is `CLAUDE.md`'s, its "What it is not" is ADR 0008
and R-015, its last paragraph ADR 0007. Spec §12 is `CLAUDE.md` Commands plus ADR 0022 and
0023. Spec §4 is R-020 to R-026 (the help-text paragraph moves to `window.md` as well, in
full, since it describes what the texts say). The spec's head paragraph ("every change to
behaviour updates this file") is now the kit's rules "Requirements first" and "Design notes";
quote that in `requirements.md`'s header.

"Read before touching" rows for the index (the ones most likely to catch a careless edit):
`measurements.md` for `StarAnalyzer` and `PrintSizeAnalyzer` (ADR 0016, 0017), `BackgroundAnalyzer`
(ADR 0014, 0018), `JpegAnalyzer` (ADR 0019), `NoiseAnalyzer` / `QuantizationAnalyzer` (ADR 0015);
`window.md` for any text in a view-model (ADR 0020); `dependencies.md` before touching the
ImageSharp version or the fixtures; `known-limitations.md` before "fixing" a limitation.

### 2.4 `docs/validation.md` and `docs/validation.json`

`validation.md`: the content of `acceptance-de-printat.md` whole (its table becomes "by hand |
tool on 2026-09-21 | tool on 2026-10-02 | note", the third column filled in layer 3 from the
`MEASURED` lines), under FlatCheck's structure: the head paragraph (what the file is, where the
image is, `APA_TESTDATA_LARGE`, the manifest, `Category=Validation`, ADR 0004); "Kinds" (one
kind, `analysis`: `AnalysisRun.Execute` on the image with the case's parameters); "Cases"
(V-001 to V-003 below); "What the tool said that the conversation also said" and "What the
real image taught the tool" as they are (the three lessons point at ADR 0016, 0017, 0018);
"History" with one line, 2026-10-02. Then `git rm docs/acceptance-de-printat.md`.

`validation.json`, `$comment` as FlatCheck's, three cases, `expected` left `null` in layer 2 and
filled in layer 3:

| ID | kind | image | parameters | what it guards |
| --- | --- | --- | --- | --- |
| V-001 | analysis | `de_printat.png` | defaults (full frame, natural distance, baryta) | the full-frame figures: means, % below 32/64, percentiles, L\* and the paper-black share, the simulation at 0.19, noise, plateau, rounding, stress, JPEG and TIFF sizes and errors, stars, background colour, the bottom-right corner, the verdict, no recommended row |
| V-002 | analysis | `de_printat.png` | `aspect` 50:35, `distanceM` 1.5 | the acceptance run: the crop 5554 x 3888 / 192 px, the 70 cm row (dpi, star mm, point distance), the recommended width 70, the verdict PROBLEMA |
| V-003 | analysis | `de_printat.png` | `dmax` 2.35 | the knife-edge measured on 2026-10-01: the share below paper black at Dmax 2.35 (1.3 %) and `PrintSafe` true; a guard on the paper-black maths rather than on the image |

Field names are the result records' (`MeanLevels`, `PctBelowPaperBlack`, `SigmaMedianLsb`,
`PlateauPx`, `FwhmMedian`, ...), values in the report's units and rounding (sizes in MB with
one decimal, levels to one decimal, percentages to one decimal, FWHM to two); `tolerance` per
case, 0.05 absolute by default; integer fields (`Stars.Count`, `SmoothTiles`,
`LevelsUsedFrom16`, `MissingFrom8`, `CutPerSidePx`) and strings (`Overall`, `Axis`) exact.

### 2.5 The two moves

```bash
git mv docs/implementation-plan.md docs/plans/2026-09-20-implementation-plan.md
```

Add two lines at its top: "Moved unchanged from `docs/implementation-plan.md` on 2026-10-02 (ADR
0001). A plan is history: its decisions are ADRs 0007 to 0024, its notes are in `docs/design/`,
its acceptance numbers in `docs/validation.md`." Nothing else changes in it.

`docs/specification.md`: when every row of the two mapping tables above is done, walk its
headings once more (§1 to §16 and every `###`) and tick each against its destination; then
`git rm docs/specification.md`. A heading with no destination is a checkpoint, not a deletion.

### 2.6 `CLAUDE.md`

Replace it with Appendix A. Target 120 to 140 lines; the hard limit is 200
(`RepoInvariantTests.ClaudeMd_StaysShort`). What leaves it and where it goes: "Structure" ->
Layout (short) and `code-map.md`; "Conventions" -> Rules (the project's own), the kit's rules
(cited by name) and ADRs 0011, 0012, 0013, 0008, 0023; "The dependencies" -> ADR 0009 and
`dependencies.md`; "The two applications" -> ADR 0007 and R-002; "Versioning" -> ADR 0022 and
the kit's "Version and year are the owner's"; "Licensing" -> ADR 0023; "Committing" -> the
kit's "Commits and merges" (the old "Do not commit" text is superseded: the assistant commits on
its `claude/` branch); "Build & test", "Run" -> Commands; "The window" -> ADR 0020, 0024 and
`window.md`.

### 2.7 `docs/skills.md`

Appendix C, verbatim.

### 2.8 Pointers that cite the old places

Every citation of a moved section is repointed in the same task (grep for `CLAUDE.md`,
`specification`, `implementation-plan`, `acceptance-de-printat` outside `bin/` and `obj/`;
the list on 2026-10-02):

- `AstroPhotoAnalyzer.Core/AstroPhotoAnalyzer.Core.csproj`, the comment "Regula e in CLAUDE.md,
  "The dependencies"" -> "Regula e a kit-ului ("Packages" in `.claude/rules/kit/process.md`) si
  a proiectului ("Package notices" in CLAUDE.md); motivul, in docs/decisions/0009 si
  docs/design/dependencies.md". Romanian, no diacritics.
- `AstroPhotoAnalyzer.Wpf/Mvvm/ObservableObject.cs` line 19, "regula dependentelor e in
  CLAUDE.md" -> the same two names.
- `AstroPhotoAnalyzer.Core/Input/Stats.cs` line 22, "Cifrele de acceptanta din
  docs/implementation-plan.md" -> "din docs/validation.md".
- `THIRD-PARTY-NOTICES.md`, "Cum se adaugă": "Vezi „The dependencies" din CLAUDE.md" -> the
  kit's rule "Packages", the project's rule "Package notices" and ADR 0009 (Romanian with
  diacritics, as the file is).
- `README.md`: the "Stare: complet" paragraph points at `docs/requirements.md` (the behaviour),
  `docs/design/` (the explanations) and `docs/validation.md` (the reference run) instead of the
  spec and the acceptance file; add the section "### Dezvoltare: gate-ul, documentația și
  Claude Code" from Appendix D.6 before "## Licență".
- `docs/plans/2026-09-20-implementation-plan.md` keeps its own old pointers (a plan is history).

### 2.9 Gate and commit 2

`scripts/verify.ps1` green: step 2 `decisions` now checks 24 headers, step 3 `design notes`
checks the index and every Covers path. Also run `scripts/check-design.ps1 -Since main` in the
worktree once, to see it list nothing (no code changed) and exit 0. Commit 2.

## Layer 3: requirements tied to tests (about an hour)

1. **`AstroPhotoAnalyzer.Tests/TestData.cs`**: FlatCheck's `TestData` with the names changed
   (`RepoDir()` from `[CallerFilePath]`, `LargeDir()` from `APA_TESTDATA_LARGE` or
   `AstroPhotoAnalyzer.Tests/testdata/large/`, `Large(name)` null when absent). GPL header
   (`LicenseHeaderTests` scans this project), Romanian or English summary? English: it is a
   process file, new (ADR 0005 keeps Romanian only for existing files; FlatCheck wrote its
   `TestData.cs` and `RepoInvariantTests.cs` in English for the same reason).
2. **`AstroPhotoAnalyzer.Tests/RepoInvariantTests.cs`**: FlatCheck's, with `TestProjects` =
   `AstroPhotoAnalyzer.Tests`, `AstroPhotoAnalyzer.Wpf.Tests`, `SourceProjects` = the five,
   without `OnlySirilScriptRunAndTheAboutLink_StartAProcess` and its `ProcessStart` regex.
   Keeps: the three requirement/test links, `RequirementIds_AreUnique`,
   `DesignNotes_HaveTheirIndex`, `EveryValidationCase_NamesAManifestFile` (the case key is
   `image`, not `master` / `raw`), `ClaudeMd_StaysShort` (200). The project's manifest is read
   from `AstroPhotoAnalyzer.Tests/testdata/manifest.json`.
3. **`AstroPhotoAnalyzer.Tests/ValidationTests.cs`**: trait `Category=Validation` on the class;
   `[Theory] [MemberData(nameof(Cases))]` over the IDs of `docs/validation.json`; a case runs
   `AnalysisRun.Execute(path, parameters, TextWriter.Null)` (or whatever the signature is; read
   `Runs/AnalysisRun.cs`) with the parameters the case names (`aspect`, `distanceM`, `dmax`,
   `medium`, `targetBg`, absent = the core's default) and collects the figures into a
   `Dictionary<string, object>` named like the case's `expected` fields; prints
   `MEASURED <id> <json>` to the test output and appends it to the file named by
   `APA_VALIDATION_DUMP` when set; returns early with `NOT MEASURED` when the image is absent;
   with `expected` null prints `UNMEASURED`; otherwise compares each field within the case's
   tolerance and fails with the full drift list. FlatCheck's `ValidationTests` is the model,
   kind by kind replaced by the one `analysis` kind.
4. **Measure.** With `APA_TESTDATA_LARGE` set:
   `dotnet test AstroPhotoAnalyzer.sln -c Release --filter "Category=Validation"` (three runs,
   about 35 s). Paste the three `MEASURED` lines in the session, copy them into
   `validation.json` as `expected` (the current build's figures, never the hand ones; the hand
   ones are the "by hand" column of `validation.md`), fill the third column of the table in
   `validation.md`, and run the filter again: three passes. Any figure that differs from the
   2026-09-21 column by more than its rounding is a finding to report, not to adjust.
5. **Rename the first batch of tests** to `R###_...` (the method name only; nothing else cites
   them, except the two names below). The mapping (test -> ID; the ID is the row the test
   pins; when a test pins two rows, the first):

   | File | Test | ID |
   | --- | --- | --- |
   | `GoldenOutputTests` | `Help` | R-004 |
   | | `DarkSkyTiff16WithEveryFlag` | R-100 |
   | | `CleanSkyOnCanvasAtTheNaturalDistance` | R-073 (the natural-distance row) |
   | | `MonoPng8WithoutProfile` | R-096 (Warnings) |
   | | `LinearFitsIsRefused` | R-015 |
   | | `MissingFileAndBadOption` | R-003 |
   | `CliTests` | `AProblemImageExitsWithThreeAndPrintsTheReport`, `ACleanMonoPngExitsWithAttentionForTheMissingProfile`, `AnUnreadableFileIsAnErrorNotACrash` | R-002 |
   | | `TheSavedReportIsTheBannerPlusExactlyWhatWasPrinted` | R-005 |
   | | `ALinearFitsIsRefusedWithExitOne` | R-015 |
   | | `APassepartoutNarrowerThanTwiceTheFrameIsRefusedBeforeAnyWork` | R-025 |
   | | `OptionsMapOntoParametersWithoutACopyOfTheDefaults` | R-024 |
   | `OptionsTests` | `TheImageIsTheOnlyPositionalArgument`, `FlagsMayComeBeforeOrAfterTheImage`, `ABadValueIsAnErrorNotAnException`, `AMissingValueIsReported`, `AnUnknownOptionIsReported`, `NoImageOrTwoImagesIsAnError` | R-003 |
   | | `NumbersAreParsedWithTheInvariantCulture` | R-024 |
   | | `SizesAreACommaSeparatedListOfWidths` | R-021 |
   | | `AspectAcceptsColonAndX` | R-022 |
   | | `NoArgumentsOrHelpAsksForUsage` | R-004 |
   | `RecommendationsTests` | `EveryRecommendationCarriesItsEvidenceAndTopicsAreInDecisionOrder`, `ACleanImageGetsAnOverallOk` | R-090 |
   | | `TheExitCodeFollowsTheVerdict` | R-002 |
   | | `TheReportBodyCarriesTheVerdictAndEverySection` | R-100 |
   | | `MonoHasNoColourToJudge` | R-093 (Colour) |
   | | `AnUpscaledFileAndAMissingProfileAreWarnings`, `ANonSrgbProfileIsNamedInTheWarning` | R-096 |
   | | `ALinearImageIsRefusedWithAMessage` | R-015 |
   | | `TheDarkSkyRaisesEveryFlagTheRealImageDid` | R-092 (Background; it is the dark scene's PROBLEMA) |
   | `ParametersTests` | `EachMediumHasItsDmax`, `AnExplicitDmaxBeatsTheMedium`, `MediumNamesRoundTripCaseInsensitively`, `TheThreeMediaAreDistinct`, `AnUnknownMediumNameIsAnArgumentError` | R-020 |
   | | `TheDefaultsAreTheOnesFromTheConversation` | R-021 |
   | | `ThePassepartoutMustBeAtLeastTwiceTheFrame`, `TheDefaultsValidate` | R-025 |
   | `VersionTests` | all | R-120 |
   | `LicenseHeaderTests` | all | R-121 |
   | `ThirdPartyTests` | all | R-122 |
   | `CultureTests` | `TheReportIsIdenticalUnderARomanianCultureWithoutForcing`, `ForcingInvariantNumbersFixesTheCurrentThread` | R-118 |
   | | `ParameterFieldsParseWithThePointRegardlessOfCulture` | R-024 |
   | `ParameterFieldsTests` | `TheFramingFieldsCarryHelpText`, `TheDistanceFieldExplainsTheEmptyCaseAndTheFactor`, `TheTargetBackgroundFieldExplainsTheSimulationAndTheDefault`, `TheHelpTextsAreParagraphs` | R-026 |
   | | `APassepartoutNarrowerThanTwiceTheFrameIsRefusedWithTheCoresMessage`, `ZeroMeansNoPassepartoutAndIsAllowed` | R-025 |
   | | `TheMediaListIsTheCoreOne` | R-020 |
   | | `TheDefaultsBuildTheDefaultParameters`, `EveryFieldIsHonoured` | R-024 |
   | `ViewModelTests` | `TheReportTabHasOneSectionPerTopicInDecisionOrder` | R-112 (Raport tab) |
   | | `ClearEmptiesEveryTab` | R-111 |
   | | `PreviewTilesSayWhatToCheckAndTheGuideHasTheStepBack` | R-117 (Preview tab) |
   | `WindowSmokeTests` | the tab-rendering test | R-110 |
   | | the About test | R-119 |

   The analyzer tests (`*AnalyzerTests.cs`, `StatsTests`, `ColorMathTests`, `RasterOpsTests`,
   the reader tests) keep their names; their rows stay `accepted` and get IDs when their area
   is next touched (the guide's layer 3 is gradual). Two renamed names are cited in the docs:
   `WindowSmokeTests.EveryTabRenders...` in `extending.md` ("A new tab") and
   `WhenTheSkyDoesNotDominateTheMedianIsNotTheSky` in the moved implementation plan (not
   renamed, so untouched); grep `docs/` for every renamed name and fix the one citation.
6. **Statuses**: every row with an `R###_` test moves to `implemented`; `RepoInvariantTests`
   fails on either direction being off.
7. **Gate.** `scripts/verify.ps1` green (now with `RepoInvariantTests`; the validation cases
   stay out through `testFilter`). Commit 3.

## Close

1. `/docs-health` once, through `general-purpose` with the text of
   `.claude/agents/docs-auditor.md` as the prompt (the agent type registers at the next
   session start). Relay the report in full; fix nothing; its gaps are the next tasks.
2. `/wrap-up`: verify, `CHANGELOG.md` gets one `Internal` line for the migration (process, not
   behaviour), requirements statuses, the 24 ADRs, this plan's "What deviated", README checked,
   `check-design.ps1 -Since main`. Then propose the merge: `git merge --no-ff
   claude/Adopt-the-starter-kit-process` in the main checkout, and ask who runs it. After the
   merge, `git branch -d` and `git worktree remove`.

## Verification

- `scripts/verify.ps1` green after each layer; the last ten lines pasted each time.
- `scripts/sync.ps1 -Check` exit 0; `scripts/check-decisions.ps1` and `check-design.ps1` exit 0
  on their own.
- `dotnet test ... --filter "Category=Validation"`: three cases pass against the recorded
  figures; the `MEASURED` lines are in the session.
- `wc -l CLAUDE.md` under 200; `RepoInvariantTests` green (the counts: `implemented` rows and
  `R###` IDs in the sources agree both ways).
- `git diff main...HEAD --stat`: no change under `AstroPhotoAnalyzer.Core/`, `AstroPhotoAnalyzer/`
  or `AstroPhotoAnalyzer.Wpf/` other than the format pass, the two csproj comment lines and
  the three repointed comments; the golden files unchanged.
- `scripts/release.ps1 -Version 1.0.1 -WhatIf` runs its checks up to the point it refuses (a
  task branch), proving `kit.json`'s publish list parses.
- `grep -rn "specification.md\|acceptance-de-printat\|implementation-plan.md"` over the project
  finds only the moved plan and the ADRs' "Source:" lines.

## What deviated

Implemented on 2026-10-02 in one session, on `claude/Adopt-the-starter-kit-process`, in three
commits, one per layer, each with the gate green (a3320a0, 695dee6 and the third).

- **The reference image is a copy, not reached through a variable** (the owner's instruction at
  approval). It sits in `AstroPhotoAnalyzer.Tests/testdata/large/` of the main checkout, where the
  owner asked for it, and of the task's worktree, where the cases were measured; the main
  checkout's copy shows as untracked until the merge brings the project's `.gitignore`.
  `kit.json` keeps `APA_TESTDATA_LARGE` because `kit-config.ps1` requires a name for the override;
  ADR 0004 says so. The manifest was written by hand with the SHA-256 and checked with
  `check-testdata.ps1`, instead of `-Update`.
- **ADRs 0001 to 0006 went into the first commit**, with the gate, not into layer 2: `kit.json`,
  `.editorconfig`, `Directory.Packages.props` and the CHANGELOG cite them.
- **The format pass touched 10 files**, not about 12: initialisers one member per line, the two
  `case` blocks of `IccProfileInfo` re-indented, four unused usings removed.
- **ADR 0003's counts were measured**: 129 warnings at `AnalysisLevel` latest-recommended, and 139
  with `Nullable` in the three application projects (85, 5, 49). The `Nullable` probe left a core
  built with annotations in `obj/`, and the first gate run failed on four CS8625 in the tests; a
  full rebuild gave 0 warnings again. Nothing in the code changed.
- **The design notes.** Twelve, as the table listed. Three moves differ from the table: the
  specification's §4 (the parameters) moved whole into `console.md`, instead of only into
  requirement rows; §15 lives only in `extending.md`, and the notes of the areas point at it
  instead of repeating "A new parameter" or "A new tab"; the acceptance record's lessons stay in
  `validation.md` and `measurements.md` points at them. `pipeline.md` covers `Runs/AnalysisRun.cs`
  and the solution; `Input/` is `input.md`'s, as §3 describes it. The header of each note says
  "moved unchanged" and does not claim that sections became pointers: the table's header text
  said so, against the ground rule not to de-duplicate in this task. `dependencies.md` was
  written by hand from §13 and the first `CLAUDE.md`.
- **The specification was checked line by line** before its removal: every line of it is in a
  note, except the numbered headings, the section-number references (replaced by note names),
  §1 and §12 (in `CLAUDE.md`, ADRs 0007, 0008, 0022, 0023 and R-015, R-123), the file map's three
  lines about the old documents, and §15's references; §15 was compared word by word.
- **Requirements**: 65 rows. R-006 states the console's and the core's invariant formatting
  without the test-host clause of the plan: the test project does not set
  `InvariantGlobalization`. `NumbersAreParsedWithTheInvariantCulture` carries R-006, not R-024.
- **Tests renamed**: 82 for 28 rows, more than the table's: `VersionTests` split between R-001
  (the banner), R-120 (the version) and R-004 (the warranty in `--help`); `ThirdPartyTests`' help
  test carries R-004; `WindowSmokeTests`' error card R-015 and its tooltip R-026;
  `CultureTests`' premise test R-118. `ViewModelTests.TonesExportResolutionAndBackgroundCarryTheirRows`
  and the two presentation tests keep their names (four rows, one name).
- **Validation**: one kind of case, `analysis`, with a fixed set of figures named after the
  result's records; V-001 guards all 168, V-002 36, V-003 9. Tolerance 0.01, not 0.05: the
  analysis is deterministic. The figures reproduce the acceptance record's "tool" column except
  the natural ratio, 1.0147 against the record's 1.018, left open for the owner
  (`docs/validation.md`, "The figures of 2026-10-02"); R-073 and ADR 0017 quote both. The
  acceptance table is kept unchanged, and the figures of 2026-10-02 have a section of their own
  instead of a third column.
- **`CLAUDE.md` is 162 lines**, not 120 to 140: the rules of the first `CLAUDE.md` that are this
  project's own were kept, each citing its ADR.
- **The audit corrected four statements of this task's own text** that were false: R-096's
  example (the mono PNG also gets an Attention for its size), R-002's example wording, ADR 0007's
  list of the core's friend assemblies (the window is one) and the test count in
  `docs/skills.md`. Everything else the audit found is below, for the owner to pick.

## The docs-health report of 2026-10-02

Run once at the end, through `general-purpose` with the text of `.claude/agents/docs-auditor.md`
(the agent type registers at the next session start). Its list of files opened holds nothing
under the code folders; the audit is valid. The small change it planned from the docs alone: a
fourth medium, a matte fine-art paper at Dmax 1.7, as `--medium mat`.

Verdict: **mostly**. The docs explain what the tool does, and why, well enough to understand it
and to make most changes; the planned change took ten guesses, most about which files list the
media.

Gaps (what is missing, where it should go, effort):

1. "A new print medium" in `docs/design/extending.md` names five places of about twenty: R-003
   and `Golden/help.txt`, R-016 with `ColorMathTests` and `input.md`'s paper-black list, R-042
   and the ladder, the Tonuri guide, `ParameterFieldsTests`, README "Consola", the Run line of
   `CLAUDE.md`, a golden scenario and `/validate` are missing. Minutes.
2. Where the window's combo, the ladder and the Tonuri guide take the media from, and whether
   `--help` and the medium error are built from `Options.Media` or typed. `console.md`,
   "Parameters", and `window.md`. Minutes.
3. The Dmax of each medium (2.0, 1.5, 2.2) has no recorded source, although baryta's 2.0 decides
   the reference image's verdict (V-001 PROBLEMA, V-003 at 2.35 ATENTIE). A new ADR, plus
   `console.md` or `thresholds.md`. An hour.
4. No procedure for changing an `implemented` row: the header says a change starts `draft`, and
   `RepoInvariantTests` then fails while its `R###_` tests exist. `docs/requirements.md` header.
   Minutes.
5. Seven rows without a numeric example: R-014, R-017, R-042, R-075, R-091, R-094, R-101; R-007's
   example is a timing, and no test carries R-007. `docs/requirements.md`. An hour.
6. The contract of `AnalysisRun.Execute` (arguments, the optional progress, the result, the
   exceptions) is not written down; `pipeline.md`'s moved text names the console and the two test
   projects as the core's friends and leaves out the window. `pipeline.md`. Minutes.
7. The golden mechanics: how a new scenario and file are made, the `--- stderr ---` / `--- exit N`
   framing, the `<VERSIUNE>` and `<TEMP>` substitutions. `console.md`, "Golden scenes". Minutes.
8. The naming scheme of the validation figures (`size.row70.dpi`, the region slugs, `"none"`, the
   values of `crop.axis`, how `medium` is written in a case). `docs/validation.md`, "How a case
   runs". An hour.
9. ADR 0007 records what the kit's ADR 0002 already decided for a project made from the kit
   (the core library has no UI) without citing it (the kit's rule "Decisions"). A citation in
   ADR 0007. Minutes.
10. No ADR records the golden tests on synthetic scenes (scrubbed, re-approved by the owner); the
    reason is only in `console.md`. `docs/decisions/`. Minutes.
11. `measurements.md` still says the natural ratio is 1.018; V-001 measures 1.0147. After the
    owner's answer. Minutes.
12. A row, a note and an ADR often state the same paragraph (ADR 0001 defers the cut, as
    FlatCheck's ADR 0051 did); R-073 and `measurements.md` already disagree on the natural ratio.
    `docs/design/`. A session.

Top three, in the auditor's words: the medium checklist with the open question of where the media
list comes from; the Dmax values without a source; R-096's wrong example (corrected above) and
the seven rows without examples.

What is good: the numbers in `requirements.md` match `validation.json` and the golden files to the
printed decimal (eleven rows spot-checked), and the 28 `implemented` rows map onto exactly 82
`R###_` tests; each risky area points to the ADR with its lesson; discrepancies are recorded, not
hidden.

None of the gaps is closed in this task; each is a task of its own (`/docs-health` step 4).

---

## Appendix A: the new `CLAUDE.md`

The text below is the target; the implementing session adjusts a path or a count that turned
out otherwise, and nothing else.

````markdown
# AstroPhotoAnalyzer

Print-readiness diagnostic for a *finished* astrophoto: a stretched TIFF, PNG or FITS as it
comes out of Siril. It measures the image and reports, in decision order, how to send it to
print: file format, whether the background survives paper, colour cast and gradient, the
largest size the real detail supports, how much denoise the print version needs. It **exports
nothing**: recommendations only, each with the number and the threshold behind it; exports are
made cleanly from Siril. Two applications, a console (`AstroPhotoAnalyzer.exe`) and a WPF
window, over one core library that hand-parses FITS and takes ImageSharp for TIFF/PNG/JPEG;
neither application computes a number or writes a sentence of diagnosis. .NET 8. A sibling of
FlatCheck (`../FlatCheck`) with the same conventions; where the two differ, this file says so.

## Commands

- Gate (what pre-commit, pre-merge-commit and the Claude commit hook run, about 1 min):
  `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`
- Fast loop, no tests: `... scripts/verify.ps1 -SkipTests`
- Build: `dotnet build AstroPhotoAnalyzer.sln -c Release --nologo` (Windows only: the WPF
  project; the console alone builds anywhere:
  `dotnet build AstroPhotoAnalyzer/AstroPhotoAnalyzer.csproj -c Release`)
- Test: `dotnet test AstroPhotoAnalyzer.sln -c Release --nologo --filter "Category!=Validation"`
- One test: `dotnet test AstroPhotoAnalyzer.sln --filter "FullyQualifiedName~R002"`
- Format: `dotnet format AstroPhotoAnalyzer.sln` (check only: `--verify-no-changes`)
- Run: `dotnet run --project AstroPhotoAnalyzer -- <image> [--medium baryta|canvas|acrilic]
  [--dmax n] [--distance m] [--sizes cm,cm,...] [--aspect w:h] [--target-bg 0..1]
  [--passepartout cm] [--frame cm] [-o file]`; flags before or after the image (R-003).
  Window: `dotnet run --project AstroPhotoAnalyzer.Wpf -- <image>`
- Golden console texts, re-approved only after the owner read the diff:
  `APA_UPDATE_GOLDEN=1 dotnet test AstroPhotoAnalyzer.sln`
- Window renders as PNGs without running it:
  `APA_DUMP_SCREENS=<dir> dotnet test AstroPhotoAnalyzer.Wpf.Tests --filter WindowSmokeTests`
- Figures on the reference image (seconds per case, not in the gate): `/validate`, or
  `dotnet test AstroPhotoAnalyzer.sln -c Release --filter "Category=Validation"` with
  `APA_TESTDATA_LARGE` naming the folder that holds `de_printat.png`
- Test data hashes after an intentional change: `scripts/check-testdata.ps1 -Update`
- Design notes a branch touched (the index and Covers paths are a gate step):
  `scripts/check-design.ps1 -Since main`
- Mutation score of one core file: `scripts/mutate.ps1 -Mutate "**/StarAnalyzer.cs"`
- Release (verify, version in `Directory.Build.props`, CHANGELOG, publish; no commit, no tag):
  `scripts/release.ps1 -Version X.Y.Z [-WhatIf]`
- Take the kit's shared folders and settings keys after a change there: `scripts/sync.ps1`
  (`-Check` only compares)

## Layout

- `AstroPhotoAnalyzer.Core/` - everything that computes and everything that formats. No
  Console, no UI, no entry point: a compile boundary. `Input/` reads (readers, `Raster`,
  colour maths, statistics), `Analysis/` decides numbers (one class per measurement family),
  `Report/` decides words (Romanian) and the recommendations, `Runs/` chains them; both
  applications call `Runs/AnalysisRun.Execute` only. `Thresholds.cs`, `Parameters.cs`.
- `AstroPhotoAnalyzer/` - the console: `Program.cs`, `Options.cs`, `AnalyzerCli.cs`. Nothing else.
- `AstroPhotoAnalyzer.Wpf/` - the window, `net8.0-windows`, dark theme: `ParameterFields` in,
  six tabs out. Formats, never decides; view-models are public and cannot expose a core type.
- `AstroPhotoAnalyzer.Tests/` (core + console, portable; `Golden/` holds the console
  snapshots; `testdata/manifest.json` hashes the reference image, which stays outside git) and
  `AstroPhotoAnalyzer.Wpf.Tests/` (window, Windows-only, one STA thread through `StaFixture`).
  Every fixture is generated or hand-built in the tests.
- `Icon/` - `make_icon.py` generates the icon both exes share. `Directory.Build.props` -
  `<Version>` and `<Copyright>`, the single place for both.
- `kit.json` - what the kit's scripts, skills and agent need to know about this project.
  `docs/skills.md` - this project's additions to the kit's skills. `scripts/`,
  `.claude/skills/`, `.claude/agents/`, `.claude/rules/kit/` and the process keys of
  `.claude/settings.json` are the kit's, the same here (the kit's ADRs 0017, 0018, 0028; ADR 0002).
- `docs/requirements.md` - the behaviour, with IDs. `docs/decisions/` - ADRs. `docs/validation.md`
  + `.json` - figures on the reference image. `docs/design/` - the explanations, per area, read
  on demand. `docs/plans/` - approved plans. `README.md` - the user documentation, Romanian.
- The process rules are the kit's, in `.claude/rules/kit/process.md`; this project's own are in
  this file. The reasons behind the process are in `GHID.md` in the kit folder
  `ClaudeCodeStarterKit` beside this project in the `photo` repository (for people, not loaded).

## Rules

The process rules every project made from the kit follows are in `.claude/rules/kit/process.md`,
loaded with this file (the kit's ADR 0028). These are AstroPhotoAnalyzer's own; one that extends
or varies a kit rule names it.

- **Golden files** (extends the kit's rule "No test edited to pass"). A golden file is never
  changed to make something pass either; `Golden/*.txt` are re-approved only after the owner has
  read the diff.
- **Package notices** (extends the kit's rule "Packages"). A package also needs an entry in
  `THIRD-PARTY-NOTICES.md` with its GPLv3 compatibility argued, one in `ThirdParty.cs` (what
  "Despre" shows; `ThirdPartyTests` enforces both) and a row in the README's table, by hand.
  ImageSharp stays pinned to 3.1.11 (ADR 0009); the FITS reader stays hand-written.
- **Measured on the reference image** (extends the kit's rule "Measured numbers"). The real
  data is `de_printat.png` (ADR 0004); a claim about a measurement on a real image comes with
  the pasted output, and the synthetic scenes in `Scenes.cs` are not a substitute for it.
- **Languages** (varies the kit's rule "Languages", ADR 0005). English in everything the model
  reads or writes about the process: docs, tests of invariants, scripts, commits, CHANGELOG.
  Romanian in everything the user sees (CLI text, `--help`, window labels, the report,
  `README.md`, `THIRD-PARTY-NOTICES.md`) and in the code comments, which stay as they are:
  without diacritics in code and messages, with diacritics in Markdown for people. Never leave a
  file in two languages.
- **Computed only in the core.** Nothing is computed outside `AstroPhotoAnalyzer.Core`: a new
  number, threshold or sentence of diagnosis goes in `Analysis/`, `Thresholds.cs` or `Report/`,
  never in a view-model or the console (ADR 0007).
- **Explanation is not diagnosis.** The window's explanatory texts (a row's `Description`, a
  table's `GuideParagraph`s, a tile's `Check`, a field's `*Help`) are the one kind of sentence
  a view-model writes; every number in them is interpolated from `Thresholds`, `Parameters` or
  the result, never typed. A literal a text needs is lifted into `Thresholds` first, with its
  comment, even one no rule reads (ADR 0012, 0020).
- **Fundal names one thing, the sky, and two measurements of it.** Tonuri measures its level
  (`BackgroundMedian`, "Nivelul fundalului"), Culoare its colour (`BackgroundResult.Levels`,
  "Culoarea fundalului"). Every label, row, appendix and `Because` that shows one says which;
  no tab is called Fundal (ADR 0013).
- **Every recommendation carries its evidence.** A `Recommendation` has a `Because`: the metric
  and the threshold. "Do X" without a number is not a recommendation here (ADR 0011).
- **Thresholds are empirical rules of thumb, not physical constants.** Each one in
  `Thresholds.cs` has a comment saying where it came from; most came from the reference image.
  Change one, change its comment and its requirement row (ADR 0012).
- **The image is display-referred and final.** The tool assumes a stretched image; linear data
  is refused with a message, never mis-measured; the print-background simulation is a curve on
  stretched data, labelled as a simulation (ADR 0008).
- **Formula changes re-validate.** A change in `Input/`, `Analysis/`, `Report/`,
  `Parameters.cs` or `Thresholds.cs` re-runs `/validate` on the reference image before the
  task ends, with the drift table pasted, and re-approves the goldens only after the owner
  read their diff.
- **License headers.** Every source file (`.cs`, `.xaml`, `.py`) starts with the GPL notice;
  `LicenseHeaderTests` fails otherwise, and its project list is explicit, so adding a project
  is a decision (ADR 0023). Scripts carry the SPDX line.
- **User documentation lives in `README.md`, and only there.** Code comments explain why the
  code is the way it is; the README explains how to use the tool; `docs/` explains what it
  does and how, for whoever maintains it. A second copy of the usage drifted once in FlatCheck.

## Workflow

The kit's workflow in `.claude/rules/kit/process.md` applies. AstroPhotoAnalyzer adds:

- `/wrap-up` also runs `/validate` when a formula moved (the rule "Formula changes
  re-validate"), and checks `README.md`, the user documentation.
- `R###_...` tests may live in either test project; `RepoInvariantTests` looks in both.
- A release is tagged with the name `scripts/release.ps1` prints (`astrophotoanalyzer-vX.Y.Z`).
- The main checkout of the kit's rule "Worktrees" is the shared checkout `E:\Photografie\Code`.

## Read before touching

`docs/design/README.md` maps every area to its note. The ones most likely to catch a careless
edit:

- `StarAnalyzer`, `PrintSizeAnalyzer`: `docs/design/measurements.md` and ADR 0016, 0017 (the
  candidate threshold is not the script's; the natural distance cannot pick a size).
- `BackgroundAnalyzer`: ADR 0014, 0018 (`<=` on the percentile; coloured versus neutral regions).
- `NoiseAnalyzer`, `QuantizationAnalyzer`: ADR 0015 (two tile criteria on purpose).
- `JpegAnalyzer`: ADR 0019 (mean error votes nothing).
- Any text in a view-model: `docs/design/window.md` and ADR 0020.
- The ImageSharp version or a 16-bit fixture: `docs/design/dependencies.md` (four measured
  behaviours the code depends on).
- A "bug" in `docs/design/known-limitations.md` is probably a known limit; read it first.

## Style

- `.editorconfig` holds whitespace only; `dotnet format` enforces it, `IDE0005` removes unused
  usings. Warnings are errors in the gate. Code style analyzers and `Nullable` in the three
  application projects are off on purpose (ADR 0003); enabling them is a task of its own.
- Fix, do not suppress, unless the suppression carries a comment with the reason.
````

## Appendix B: `kit.json`

```json
{
  "$comment": "What the shared scripts, skills and agent need to know about AstroPhotoAnalyzer. With docs/skills.md, the only file edited here to use them; scripts/, .claude/skills/, .claude/agents/ and .claude/rules/kit/ are byte-identical with ../ClaudeCodeStarterKit's (scripts/sync.ps1 keeps them so; the kit's ADRs 0017, 0018 and 0028; ADR 0002). The projects sit at the folder root, not under src/ and tests/, so the folders are listed. The validation cases on the reference image stay out of the gate (ADR 0004). See scripts/kit-config.ps1 for every key.",
  "solution": "AstroPhotoAnalyzer.sln",
  "testFilter": "Category!=Validation",
  "codeFolders": ["AstroPhotoAnalyzer.Core/", "AstroPhotoAnalyzer/", "AstroPhotoAnalyzer.Wpf/"],
  "codeFiles": [],
  "testFolders": ["AstroPhotoAnalyzer.Tests/", "AstroPhotoAnalyzer.Wpf.Tests/"],
  "testData": {
    "folder": "AstroPhotoAnalyzer.Tests/testdata/",
    "largeEnv": "APA_TESTDATA_LARGE"
  },
  "mutate": {
    "testProject": "AstroPhotoAnalyzer.Tests/"
  },
  "release": {
    "publish": [
      {
        "project": "AstroPhotoAnalyzer/AstroPhotoAnalyzer.csproj",
        "output": "console",
        "args": ["-r", "win-x64", "--self-contained", "-p:PublishSingleFile=true"]
      },
      {
        "project": "AstroPhotoAnalyzer.Wpf/AstroPhotoAnalyzer.Wpf.csproj",
        "output": "wpf",
        "args": ["-r", "win-x64", "--self-contained"]
      }
    ]
  },
  "audit": {
    "alsoRead": ["THIRD-PARTY-NOTICES.md", "AstroPhotoAnalyzer.Tests/Golden/"]
  },
  "kit": "../ClaudeCodeStarterKit"
}
```

The console's publish takes no `IncludeNativeLibrariesForSelfExtract`: ImageSharp is pure
managed, which is the single-copyable-binary promise (ADR 0009).

## Appendix C: `docs/skills.md`

````markdown
# Project additions to the kit's skills

The skills under `.claude/skills/` are the kit's, byte-identical in every project (the kit's
ADR 0018; ADR 0002). What AstroPhotoAnalyzer needs on top of them is here, one section per
skill; each skill reads this file before its first step, and each section says at which step it
applies. Values (the solution, the folders, the publish list, what the auditor may also read)
are in `kit.json`, not here.

## /new-feature

- At step 1: a console requirement also states the exit code (`docs/requirements.md` has the
  console contract block); a window requirement names the tab and the rows it adds.
- At step 2: the core and the console are tested in `AstroPhotoAnalyzer.Tests`, the window in
  `AstroPhotoAnalyzer.Wpf.Tests`. A change to the report or to an option also gets a golden
  scenario in `GoldenOutputTests` on a scene from `Scenes.cs` (`docs/design/console.md`); a
  new tab a `Render` line in `WindowSmokeTests`; a figure measured on the reference image a
  case in `docs/validation.json` as the skill says.
- At step 3: measure on the reference image before you design (`docs/validation.md` says where
  it is). On this project the first size rule, the first edge-region rule and the star
  candidate threshold were all wrong until the real image was run (ADR 0016, 0017, 0018).
- At step 5: `AstroPhotoAnalyzer.Core` first (`Analysis/` decides numbers, `Report/` decides
  words, `Runs/` chains them), then the console and the window, which only call `Runs/`
  (the rule "Computed only in the core"). `docs/design/extending.md` is the checklist per kind
  of change: a measurement, a recommendation, a parameter, a medium, an input format, a tab, a
  window text, a dependency.
- At step 6: numeric code is `Input/`, `Analysis/`, `Report/`, `Parameters.cs` and
  `Thresholds.cs` (the rule "Formula changes re-validate"). The golden files are re-approved
  with `APA_UPDATE_GOLDEN=1` only after the owner has read the diff and said yes.
- At step 8: `README.md` is in Romanian with diacritics; its "Consola" paragraph lists every
  console flag and its "Fereastra" paragraph every tab.

## /wrap-up

- At step 2: numeric code is `AstroPhotoAnalyzer.Core/Input/`, `Analysis/`, `Report/`,
  `Parameters.cs` and `Thresholds.cs` (the rule "Formula changes re-validate"); a
  `Golden/*.txt` that changed is a finding like a moved figure.

## /validate

- At step 1: the reference image is `de_printat.png` under `E:\Photografie\Claude`, the folder
  `APA_TESTDATA_LARGE` names; `AstroPhotoAnalyzer.Tests/testdata/manifest.json` lists it with
  its hash. A case runs the whole analysis on the 23 MP image, about 11 s (ADR 0004).
- At step 3: the table's second column is the image and the case's parameters, not a fixture.

## /release

- At step 3: ask for `/validate` whether or not a formula moved; a release is the moment a
  stale figure on the reference image gets noticed.
- At step 5: the console is published as the single copyable `AstroPhotoAnalyzer.exe` under
  `console/` and the window under `wpf/`; the tag is the one the script prints.
- At step 7: the README's "Compilare" section names the publish commands; `artifacts/` is what
  they produce.

## /mutate

- At step 1: pass a file filter on this project; `scripts/mutate.ps1 -Mutate "**/StarAnalyzer.cs"`
  is the shape. The whole core against 236 tests per mutant is an hour or more; measure once
  and write the figure here.

## /docs-health

(none; `audit.alsoRead` in `kit.json` lets the auditor read `THIRD-PARTY-NOTICES.md` and the
golden texts under `AstroPhotoAnalyzer.Tests/Golden/`, the console's expected output, not code)
````

## Appendix D: the small files

### D.1 `.claude/settings.json`, the `$comment`

FlatCheck's comment with "FlatCheck's" replaced by "AstroPhotoAnalyzer's" (read it in
`../FlatCheck/.claude/settings.json`; the rest of the file is the kit's and is not edited).

### D.2 `Directory.Packages.props`

```xml
<Project>
  <!-- Central Package Management: every package version lives here, once (ADR 0003). The
       rule: no new package without asking (the kit's rule "Packages"), and every package
       gets a "why" here, an entry in THIRD-PARTY-NOTICES.md and, for the core, one in
       ThirdParty.cs (the rule "Package notices", ADR 0023). ThirdPartyTests reads the
       PackageReference names from AstroPhotoAnalyzer.Core.csproj; the versions are here. -->
  <ItemGroup>
    <!-- Decodes TIFF, PNG and JPEG (the pixels every measurement runs on) and encodes JPEG
         and TIFF for the export table. Pinned to 3.1.x on purpose: 4.x adds a licence-key
         MSBuild task that errors on Release builds (ADR 0009, docs/design/dependencies.md). -->
    <PackageVersion Include="SixLabors.ImageSharp" Version="3.1.11" />
  </ItemGroup>
  <ItemGroup>
    <!-- Test host and runner (xUnit 2: no dynamic skip, which is why an absent reference
         image returns early with a message instead of skipping). -->
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageVersion Include="xunit" Version="2.5.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.5.3" />
    <PackageVersion Include="coverlet.collector" Version="6.0.0" />
  </ItemGroup>
</Project>
```

### D.3 `CHANGELOG.md`

```markdown
# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in
`Directory.Build.props`; the owner bumps it.

## [Unreleased]

### Internal

- The project follows the ClaudeCodeStarterKit process (ADR 0001 to 0006): `scripts/verify.ps1`
  as the gate, `docs/requirements.md` with IDs, `docs/decisions/` (24 ADRs, 18 retroactive),
  `docs/design/` (the former `docs/specification.md`, moved), `docs/validation.json` read by
  `ValidationTests` on `de_printat.png`, `RepoInvariantTests`, the kit's skills and hooks.
  No behaviour changed; one `dotnet format` pass over N files.

## [1.0.0] - 2026-09-21

The state of the tool when the changelog was started, built on 2026-09-20 and 2026-09-21 in
seven phases (`docs/plans/2026-09-20-implementation-plan.md`):

- The core: readers (hand-written FITS; TIFF, PNG and JPEG through ImageSharp 3.1.11), the
  raster, colour maths, eleven analyzers (file facts, bit depth, crop, tonal distribution, the
  print-background simulation, noise, background colour and edge regions, stars, print size,
  quantization, JPEG), the recommendations in decision order with their evidence, the report.
- The console `AstroPhotoAnalyzer.exe`: options, exit codes 0/1/2/3, `-o`, golden tests on
  synthetic scenes.
- The window `AstroPhotoAnalyzer.Wpf.exe`: parameters with help tooltips, six tabs (Raport,
  Tonuri, Export, Rezolutie, Culoare, Preview) with a description under every number,
  `Raport.txt` byte-identical to the console's output, "Despre".
- The acceptance run on `de_printat.png` against the hand-made numbers; the "Fundal" naming
  pass (the tab became Culoare).

No earlier version was released; the eleven commits are in `git log`.
```

### D.4 `AstroPhotoAnalyzer.Tests/testdata/README.md`

```markdown
# Test data

AstroPhotoAnalyzer has two kinds of test data and one manifest.

- **Generated or hand-built in the test.** Every raster the unit and golden tests use is
  written by `SyntheticRasters` and `Scenes` (a dark sky with every defect of the reference
  image, a clean sky, a mono sky); the 16-bit TIFF and PNG fixtures are hand-built by
  `TiffTestData` and `PngTestData`, because ImageSharp 3.1 cannot encode them, and that is
  the more honest test (`docs/design/dependencies.md`); FITS files by `FitsTestData`, ICC
  profiles by `IccTestData`, 8-bit files by `ImageFiles`. The golden texts under
  `AstroPhotoAnalyzer.Tests/Golden/` are compared byte for byte and re-approved only with
  `APA_UPDATE_GOLDEN=1` after the owner has read the diff. So `fixtures/` is empty.
- **Large, outside git.** The reference image `de_printat.png` (a Milky Way core, 5938 x 3888,
  16-bit RGB PNG, 103 MB) that the acceptance figures in `docs/validation.json` were measured
  on. `manifest.json` lists it with `"location": "large"`, a path relative to the folder named
  by `APA_TESTDATA_LARGE` (default `AstroPhotoAnalyzer.Tests/testdata/large/`, git-ignored),
  its SHA-256 and what it demonstrates. `scripts/check-testdata.ps1` reports it absent as a
  warning locally and as an error in CI only when `requiredInCi` is true. `ValidationTests`
  reach it through `TestData.Large(name)` and return early with a message when it is absent
  (xUnit 2 has no dynamic skip). They carry the trait `Category=Validation` and are not part
  of `scripts/verify.ps1`; `/validate` runs them (ADR 0004).

After the image is replaced on purpose:

```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/check-testdata.ps1 -Update
```

Then re-measure its cases (`/validate`) and record the change in `docs/validation.md`.
```

### D.5 `AstroPhotoAnalyzer.Tests/testdata/manifest.json`

```json
{
  "$comment": "Every test data file outside git: the reference image under E:/Photografie/Claude, the folder named by APA_TESTDATA_LARGE. Paths are relative to that folder. scripts/check-testdata.ps1 verifies presence and SHA-256 (a missing large file is a warning locally); -Update rewrites this file with PowerShell JSON layout. The cases in docs/validation.json name this file. AstroPhotoAnalyzer has no repo fixtures: they are generated or hand-built in the tests.",
  "files": [
    {
      "name": "de_printat.png",
      "location": "large",
      "requiredInCi": false,
      "sha256": "<written by check-testdata.ps1 -Update>",
      "source": "the owner's finished Milky Way core, exported from Siril, 2026-08-30; the image the tool was built to measure",
      "demonstrates": "docs/validation.json V-001 to V-003"
    }
  ]
}
```

### D.6 `README.md`, the new section (Romanian, with diacritics, before "## Licență")

```markdown
### Dezvoltare: gate-ul, documentația și Claude Code

Din 2026-10-02 proiectul urmează procesul din `ClaudeCodeStarterKit` (folderul de lângă acesta
în repo-ul `photo`; ghidul cu motivația fiecărei piese e `GHID.md` acolo), la fel ca FlatCheck.
Pe scurt, ce e impus mecanic:

- **`scripts/verify.ps1` e gate-ul**: scripturile identice cu ale kit-ului, manifestul datelor
  de test, antetele ADR-urilor, notele de design (fiecare în index, cu fișierele ei numite în
  propoziția `Covers:`), restore, `dotnet format` (doar verifică), build cu avertismentele ca
  erori, toate testele în afară de cele de validare. Cam un minut. Îl rulează hook-ul git
  `pre-commit` de la rădăcina repo-ului la orice commit care atinge proiectul, hook-ul
  `pre-merge-commit` la orice merge fără conflicte, hook-ul Claude Code înainte de orice
  `git commit` al modelului, și `scripts/release.ps1`. Fără el verde nu intră niciun commit.

  ```bash
  powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
  ```

- **Mesajul de commit** e o singură linie, în engleză, de cel mult 100 de caractere.
- **`scripts/`, `.claude/skills/`, `.claude/agents/` și `.claude/rules/kit/` sunt ale
  kit-ului**, byte cu byte, și nu se editează aici; ce e specific proiectului ca valori e în
  `kit.json`, pașii pe care îi adaugă la skill-uri sunt în `docs/skills.md`.
- **Comportamentul e în `docs/requirements.md`**, cu ID-uri `R-NNN` purtate în numele testelor;
  `RepoInvariantTests` cere ca fiecare cerință `implemented` să aibă un test cu ID-ul ei și
  invers. Specificația de dinainte (`docs/specification.md`) a fost împărțită pe 2026-10-02 în
  cerințe, decizii și note de design; nimic nu s-a pierdut.
- **Deciziile sunt în `docs/decisions/`**, un ADR per decizie, cu alternativele respinse; cele
  luate înainte de 2026-10-02 au fost scrise retroactiv din specificație și din planul de
  implementare.
- **Cifrele pe imaginea de referință** (`de_printat.png`, care nu e în repo) sunt în
  `docs/validation.json`, citit de `ValidationTests` (`--filter "Category=Validation"`, nu în
  gate); `APA_TESTDATA_LARGE` arată la folderul ei, iar
  `AstroPhotoAnalyzer.Tests/testdata/manifest.json` îi ține hash-ul. `docs/validation.md` spune
  de unde vine fiecare cifră, lângă numerele făcute de mână în conversația care a pornit unealta.
- **Explicațiile din spatele codului** sunt în `docs/design/`, cu indexul în
  `docs/design/README.md`; `CLAUDE.md` ține doar regulile proiectului, cele de proces sunt ale
  kit-ului, în `.claude/rules/kit/process.md`.
- **Versiunea** o crește omul, în `Directory.Build.props`. Un release se face pe `main`, cu
  `scripts/release.ps1 -Version X.Y.Z`, care rulează gate-ul, închide secțiunea din CHANGELOG
  și publică în `artifacts/X.Y.Z/` (consola ca fișier unic, fereastra separat). Commit-ul și
  tag-ul rămân ai omului.
- În Claude Code: `/new-feature`, `/wrap-up`, `/validate`, `/docs-health`, `/mutate`, `/release`.
```
