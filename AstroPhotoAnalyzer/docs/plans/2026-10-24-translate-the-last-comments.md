# Translate the last Romanian comments to English, and make every comment English

## Context

The documentation backfill (2026-10-22..24) translated the comments of the code's C# files;
the task that closed its questions (merged into `main` as 6207c9f) left three places Romanian
on purpose, as the rule "Languages" in `CLAUDE.md` and ADR 0005 say: the tests' comments, the
XAML comments, and the csproj and `Directory.Build.props` comments. On 2026-10-24 the owner
asked to translate the comments to English and to set a rule that comments are English from
now on, and, asked in this session, chose to include the XAML and project files, so that the
rule has no exceptions: every comment, in every file of the project, is English.

Measured on `main` at 6207c9f (a function-word scan of comment lines, skipping the GPL
headers and quoted strings; the translation pass reads every comment line anyway):

| Where | Romanian comment lines | Files |
|---|---|---|
| `AstroPhotoAnalyzer.Tests/`, `AstroPhotoAnalyzer.Wpf.Tests/` (`//`, `///`) | about 208 | 35 of 55 |
| XAML (`<!-- -->`, both the window and its views) | about 90 | 15 of 16 |
| csproj files and `Directory.Build.props` (`Directory.Packages.props` is already English) | about 130 | 6 |
| `Icon/make_icon.py` (`#`) | 5 | 1 |

`.editorconfig`, `.gitattributes`, `.gitignore` and `Directory.Packages.props` are English
already; `scripts/` and `.claude/` are the kit's and English. No test reads a comment's text:
`LicenseHeaderTests` checks the GPL header, which stays as it is, and `ThirdPartyTests` and
`VersionTests` read the csproj and props elements, not their comments.

## Branch and worktree

From the main checkout `E:\Photografie\Code` (on `main`, clean apart from the untracked
`AstroPhotoAnalyzer/.github/`):

```
git worktree add AstroPhotoAnalyzer/.claude/worktrees/Translate-the-last-comments -b claude/Translate-the-last-comments main
```

Save this plan as `docs/plans/2026-10-24-translate-the-last-comments.md` in the first commit.

## How each comment is translated

The backfill's rules, from its plan (`docs/plans/2026-10-22-...`, "Glossary" and
"Assumptions"): one English word per Romanian term (its glossary: background, sky, dust,
patch, tile, paper black, lift, ladder ...); quoted output and every string literal stay
Romanian as the user sees them (`"nu e o imagine"`, verdict words, labels, assertion
messages); "omul" is "the user", "proprietarul" "the owner"; British spelling as the docs
write it; ASCII only; lines within 100 columns; nothing but comments changes, which a
token-for-token comparison with `main` checks (the backfill's scratch tool idea, rebuilt in
this session's scratchpad: strip comments, compare the token streams per file). A comment
found false while translating is translated as it stands and listed in "What deviated", not
corrected, unless the fix is a word.

## Commits, each with the gate green

1. **The tests' comments**, both test projects (the bulk). The `///` summaries of test helpers
   (`TempWorkspace`, `Scenes`, `FitsTestData` ...) and the file headers included.
2. **The XAML, csproj, props and icon-script comments.** The XAML comments of
   `AstroPhotoAnalyzer.Wpf/` (section markers like `bara de sus`, explanations like the save
   place's); the long headers of the csproj files and `Directory.Build.props`, which carry the
   reasons behind the build settings (ADR 0005 kept them Romanian for that; the translation
   keeps every reason and figure); `Icon/make_icon.py`.
3. **The rule.** `CLAUDE.md`, rule "Languages": every comment in every file of the project is
   English (C#, XAML, csproj, props, Python, config), a comment added to any file included;
   Romanian stays only in what the user sees (CLI text, `--help`, window labels and texts, the
   report, `README.md`, `THIRD-PARTY-NOTICES.md`) and in quoted output inside comments and
   tests. The sentence on the three Romanian places goes. ADR 0047 records the decision (the
   owner's word of 2026-10-24, the measured counts, the alternative of keeping the three
   places) and supersedes ADR 0005 in part (the Romanian comments of the tests, the XAML, the
   csproj and the props); ADR 0005's Status gets `; <part> superseded by 0047` as the kit's
   ADR 0024 grammar wants, and its parenthetical note of 2026-10-24 is completed to agree.
   `README.md` and the kit's `GHID.md` say nothing about comment languages that this changes
   (checked at wrap-up).

No CHANGELOG line: comments and a process rule only (the kit's rule "CHANGELOG": a comment
fix gets none). No requirement row: no behaviour changes.

## Verification

- `scripts/verify.ps1` green before each commit (the commit hook runs it too).
- The token comparison against `main`: every C# file's code identical; for the XAML, csproj,
  props and Python files, the same check on the text with comments stripped.
- A re-scan with the function-word list: no Romanian comment line left outside quoted output,
  each remaining hit read and judged.
- Every changed line ASCII and within 100 columns.
- `/validate` is not run: no code in `Input/`, `Analysis/`, `Report/`, `Parameters.cs` or
  `Thresholds.cs` changes (comments only, shown by the token comparison).

## Wrap-up

`/wrap-up`: the plan's "What deviated" with the gate times, the counts before and after, the
comments found false; the merge proposal, suggesting `/code-review` first.

## What deviated

Run on 2026-10-24 in one session, on `claude/Translate-the-last-comments`, cut from `main` at
d99d53f (not 6207c9f: `main` had moved by one LightsCheck merge, nothing under
`AstroPhotoAnalyzer/`). Three commits as planned, then this wrap-up.

- **The translation was split between three agents on disjoint files**, run in parallel: the
  core test project A-N with its csproj, O-Z with the window test project, and the XAML, the
  application csproj files, `Directory.Build.props` and `Icon/make_icon.py`. Each worked to this
  plan's rules, the backfill glossary and two scratch tools (`tokens.py`, a token comparison
  with `main` with the comments stripped; `roscan.py`, a function-word scan of comment lines).
  The checks below were run again over the whole branch after all three were done, and every
  file's diff was read.
- **Counts.** The scan found 371 Romanian comment lines in 54 files before (the plan's table,
  made with a looser word list, said about 430); after, 9 lines in 2 files, both quoted report
  output inside English comments of `PrintStretchSimulator.cs` and `ReportWriter.cs`, as they
  should be. 59 files changed, +669/-468 lines: 34 test files, 15 XAML files, 5 csproj files,
  `Directory.Build.props`, the icon script, `CLAUDE.md` and the two ADRs.
- **Code unchanged:** `tokens.py` finds every C#, XAML, csproj and props file identical to
  `main` with the comments stripped. `Icon/make_icon.py` shows a difference only because the
  tool keeps Python docstrings, which were translated too; its diff is the two docstrings and
  four `#` comments, read line by line.
- **ASCII, widths:** every added line is ASCII. Five added lines are over 100 columns: trailing
  comments after test code that is already 94 to 97 columns wide (`PrintSizeAnalyzerTests`,
  `ViewModelTests`), over 100 on `main` too; moving them to their own line would change
  whitespace outside comments.
- **Quoted text:** quoted output and labels stayed Romanian ("asa cum e", "punct de la",
  "Despre", "Calculeaza", the tab names); three quoted phrases that were not output were
  translated ("fundal" in `BackgroundAnalyzerTests`, "fundal sub negrul hartiei" and the image
  nicknames in `Scenes.cs`).
- **Two stale comments fixed by a word,** as the plan allowed, each checked against the file:
  `MainWindow.xaml`'s header said six tabs (there are seven `TabItem`s), and `Dark.xaml`'s
  `MonoRowTemplate` said five tables (six views use it).
- **Comments found stale or false, translated as they stood** (questions for the owner):
  1. `Directory.Build.props`: the version "is incremented MANUALLY, by hand, at every
     feature/fix", against the kit's rule "Version and year are the owner's" (the version
     changes at the owner's word, through `scripts/release.ps1`).
  2. `LicenseHeaderTests.cs` and `VersionTests.cs`: the WPF projects "appear in stage 5 of the
     plan"; they exist and are listed.
  3. `GoldenOutputTests.cs`: a version bump "must not fail 6 tests"; there are 7 tests and 8
     golden files carry the version placeholder.
  4. `PrintSizeAnalyzerTests.cs`: an orphaned `<summary>` on the dpi-distance ladder, above a
     second `<summary>` of the same test.
  5. `SyntheticRasters.cs`: "rounded to 255 levels"; `RasterOps.Level8` gives 256 (0..255).
  6. `ColorMathTests.cs`: the summary of `AGreyLevelAndItsLstarAreOneCurve` names pairs the
     test does not check (32 and 64) and not the one it does (L* 15.0 -> 37.7).
  7. `BackgroundAnalyzerTests.cs`: "the bottom band is excluded"; the scene has a bottom-right
     corner, not a band (not checked against `BackgroundAnalyzer`).
  8. `AstroPhotoAnalyzer.Wpf.csproj`: the window "calls AnalysisRun.Execute"; it also calls
     `CurveRun` (ADR 0032).
  9. `AstroPhotoAnalyzer.Core.csproj`: ImageSharp "encodes/decodes JPEG for the compression
     table"; `Directory.Packages.props` says it encodes JPEG and TIFF for the export table.
- **`README.md` and the kit's `GHID.md`** say nothing about comment languages that this changes.
  The design notes `check-design.ps1 -Since main` listed (`code-map`, `console`,
  `dependencies`, `extending`, `input`, `testing`, `window`) describe code, not its comments,
  and hold. No golden changed.
- **Measured on 2026-10-24:**

  | Commit | What | Gate | Tests |
  |---|---|---|---|
  | 283279a | the tests' comments | `== OK in 37s` | 403 + 72 |
  | 4e61639 | the XAML, csproj, props and icon comments | `== OK in 39s` | 403 + 72 |
  | f713489 | the rule "Languages", ADR 0047, ADR 0005's Status | `== OK in 39s` | 403 + 72 |

### The stale comments, fixed (2026-10-24)

At the owner's word, in one commit, each claim checked against its code first. Eight were
stale and are now true: the version comment of `Directory.Build.props` says the version
changes only at the owner's word, at a release; the two "stage 5" comments say a missing
project is skipped and that all exist; the golden header says a bump must not fail every
golden test, without a count; the orphaned dpi-ladder `<summary>` moved onto
`TheDpiLadderSaysFromHowFarTheGridVanishes`; `Upscaled8` says the 256 levels 0..255; the
grey-level summary names the pairs its test checks (L* 9, 15, 50) and points at the next
test for levels 32 and 64; the WPF csproj names `CurveRun`; the core csproj says ImageSharp
decodes TIFF, PNG and JPEG, and for the export table encodes JPEG and the 8-bit LZW TIFF and
decodes the JPEG back (`JpegAnalyzer`, `ImageSharpReader`). One was true after all:
`BackgroundAnalyzerTests`' "the bottom band is excluded" holds, since the analyzer drops the
bottom `BackgroundExcludeBottomFraction` (25 %) of the rows and the test's corner is the last
15 %. `tokens.py` finds the code of the nine files unchanged; the gate `== OK in 58s`,
403 + 72 tests.

### Code review (2026-10-24)

`/code-review main...claude/Translate-the-last-comments` at high effort: five findings, all
fixed at the owner's word in one commit.

- **The tests' own failure messages were still Romanian** (30 strings in 13 files: "erori de
  legare", "Directory.Build.props nu are <Version>", the `ThirdPartyTests` claims, the
  quantization and JPEG figures ...), which the new rule puts on the English side, since they
  are not output of the tool. They are English now; the expected output the tests compare
  against (`InlineData` messages, `Contains`/`Equal` arguments) stays Romanian, as the user
  sees it. One fixture string, the ICC description `IccProfileInfoTests` truncates, was
  translated at the same 36 characters, so the truncation point stays where it was. Fixture
  file names (`curat.tif`, `nu-exista`, `calea-lactee.tif`) are data and stay.
- **Two tests skipped a missing project:** `LicenseHeaderTests.SourceFiles` and
  `VersionTests.R120_NoProjectOverridesTheSharedVersionOrCopyright`, crutches from before the
  window existed. Both now assert that each listed project exists, so a renamed project fails
  instead of leaving the check.
- The core csproj said ImageSharp decodes 16-bit JPEG; JPEG is 8 bits now said apart. A
  "ViewModels" in `StaFixture.cs` is "view-models", the backfill's spelling.
- The gate after the fixes: `== OK in 46s`, 403 + 72 tests.

### Second code review (2026-10-24)

`/code-review` again before the merge, as the owner asked: two findings, both fixed. ADR 0047
said the branch changed comments only and nothing in the tests; its Decision now says a
test's own failure messages are English like its comments, and its Consequences list the 30
translated messages and the two project-existence assertions, the tool's code and output
unchanged. The last prose "ViewModel" in a comment (`Dark.xaml`, `MonoRowTemplate`) is
"view-model". The branch was then squashed into one commit and merged, at the owner's word.

