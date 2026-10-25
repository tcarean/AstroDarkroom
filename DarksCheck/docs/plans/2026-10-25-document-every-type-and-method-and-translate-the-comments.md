# Document every type and method, and translate every comment to English

## Context

The kit's rule "Documentation comments" (the kit's ADR 0035) asks for an XML documentation
comment on every type and method in the code. Code written before the rule gets them in a task
of its own, and this is DarksCheck's. The owner asked on 2026-10-25 for the XML comments to be
added, for every other comment in the code to be made English too, for comments to be English
only from now on, and for no behaviour to change. Answers given in planning:
- **The copies:** translate the copied files' comments here, using FlatCheck's English text
  word for word wherever the code is the same as FlatCheck's, and my own translation elsewhere.
  No code changes.
- **One branch**, with one commit per area.

The outcome: every type and method under `src/` documented, every comment in `src/` and
`tests/` English (C#, XAML, csproj), the code token-identical to `main`, and the gate green.
FlatCheck's `docs/plans/2026-10-24-document-every-type-and-method-and-translate-the-comments.md`
is the pattern. This plan only says where DarksCheck differs from it.

## Facts measured (2026-10-25, `main` at 3197e36)

Counter: the backfill scratch tool `doccount` (Roslyn; `count`, `romanian`, `width`, `tokens`,
`facts`), copied from FlatCheck's session into this session's scratchpad and built there.

- **Code** (60 files, 9,769 lines): 131 types, 89 to touch; 375 methods, 370 to touch (199
  undocumented, 171 incomplete). Per area (types/methods): Imaging 9/71, Analysis 45/88,
  Report 5/102, Runs 5/6, Session 1/5, Thresholds+ThirdParty 1/0, Version 0/2, Cli 2/5,
  Wpf 21/91. Properties: 140 undocumented, not required.
- **Romanian:** 218 comment lines, all in 8 copied files: `Imaging/` BitmapFont, CfaPattern,
  FitsReader, IFrameReader, Img, Mosaic, PngWriter, Stats. `Theme/Dark.xaml` has 12 comments.
  The tests `FitsTestData.cs` (10 lines) and `FitsReaderTests.cs` (4 lines) have some too.
  Everything else (the other XAML, the csproj files, `Rows.xaml`, `Presentation/`) is already
  English.
- **Against FlatCheck `main`** (ignoring namespace, usings and `#nullable`): BitmapFont,
  CfaPattern, IFrameReader, Img and Mosaic have identical code. FitsReader (FlatCheck's ADR 0057
  is not taken here), PngWriter, Stats (FlatCheck added members), ImageLoader and RawReader
  (LightsCheck's adaptations) differ. The 10 files are byte-identical to LightsCheck's, except
  `RawReader.cs` (namespace, `darkscheck.exe` in one string).
- The documentation file is already on (`Directory.Build.props`: `GenerateDocumentationFile`,
  CS1591 muted), so a malformed `///` fails the gate build.

## Approach

Branch `claude/Document-and-translate-every-comment`, with its worktree at
`E:\Photografie\Code\.claude\worktrees\Document-and-translate-every-comment`.

**Area commits**, in this order:
1. Imaging copies + `Dark.xaml` + the two test files.
2. `Analysis/`.
3. `Report/`, `Runs/`, `Session/`, `Thresholds.cs`, `ThirdParty.cs`, `Version.cs`, the console.
4. The window (`DarksCheck.Wpf`).
5. The decision and records: the ADR, `CLAUDE.md`, `.editorconfig` comment, CHANGELOG, this plan.

Areas 2 to 4 are written by parallel fork subagents in the same worktree, each touching only its
own files, with no commits from them. Area 1 is mine. I read every diff before committing.

**Writing rules** (as FlatCheck's plan):
- Every type and method, private ones and constructors included, gets a `<summary>`, a `<param>`
  for each parameter, a `<typeparam>` for each type parameter, and `<returns>` for a value. A
  plain `Task` needs no `<returns>`.
- `<inheritdoc/>` is used only where an interface of the project states the whole contract.
  Framework overrides get their own text.
- The comment says the unit, the range and what null means, and checks the claim against the
  code. A comment found false is corrected and listed in the plan's "What deviated".
- Lines stay at most 100 columns (`wrap.py`).
- Existing `//` comments are kept. A `///` goes under an existing `//` line so the diff stays
  made of comment lines only.

**The copies (area 1):**
- In the 5 identical files, each comment is FlatCheck's English text as it stands in FlatCheck
  `main`.
- In the other 5, FlatCheck's text is used for every member that is the same, and my own
  translation for the rest, in FlatCheck's terms.
- In `Dark.xaml`, FlatCheck's text where the comments match, otherwise a translation.
- The two test files take FlatCheck's translated test comments.
- The `// copied from FlatCheck ... unchanged` marker lines stay; their wording is checked.

**The decision (area 5):**
- ADR 0019, "Every comment is English; the copies take FlatCheck's English comments ahead of
  LightsCheck". It supersedes in part ADR 0005 (comment zone) and ADR 0003 (copies changed only
  at the source). Until LightsCheck's backfill recopies FlatCheck, DarksCheck's copies differ
  from LightsCheck's in comments only. The next refresh from LightsCheck replaces them.
- ADRs 0003 and 0005 each get the status pointer the kit's ADR 0024 allows.
- `CLAUDE.md` "Languages" drops the Imaging exception: every comment is English, in every file.
  "Copied files stay copies" mentions that ADR 0019 is the exception for comments.
- The `.editorconfig` comment that says "Romanian comments" is reworded (comment only).
- CHANGELOG `Internal`: one line.
- The approved plan is saved to `docs/plans/2026-10-25-document-every-type-and-method-and-translate-the-comments.md`
  and a "What deviated" section is added at wrap-up.

## Verification

Run before each commit, on the area's files:
- `doccount count`: 0 types and 0 methods to touch.
- `doccount romanian`: 0 lines.
- `doccount width`: 0 lines past 100.
- `doccount tokens <main checkout> <worktree>`: identical code in every file.
- `doccount facts`: every number and ID of an old comment is found again.
- XAML: the files compared with their comments removed are identical. The csproj files are
  unchanged.
- No `Process.Start(` / `new Process(` text in comments (`RepoInvariantTests`).
- The gate, `scripts/verify.ps1`, is green after a `--no-incremental -warnaserror` build.
  Golden texts don't move, since no output changes.
- No `/validate`: no formula or constant changes, and the token check proves it.
- At wrap-up, `/wrap-up`: commit on the branch, then propose the merge.

## What deviated

Done on 2026-10-25 in one session. Area 1 was mine, and areas 2 to 4 were written by three
parallel subagents in the worktree. I read every diff back and checked a sample of the claims
against the code. There is one commit per area (c44332e, 0b13cd6, 2d6eeb0, b846c33), each
through the gate, which ran 247 + 69 tests. Code counted at the end: 131 types and 375 methods,
0 to touch. Romanian comment lines: 0. Lines past 100 columns: 0. Code token-identical to `main`
in all 60 files. Dark.xaml is identical with its comments removed.

**The copies:**
- `RawReader.cs` and `ImageLoader.cs` kept their own comments, which were already English
  (LightsCheck's adaptation), instead of taking FlatCheck's. FlatCheck's text describes what
  these copies dropped (the embedded preview, `--expo`). They only gained the missing docs.
- `FitsReader.cs` took FlatCheck's text except where it describes FlatCheck's ADR 0057, which
  this copy does not have. `LoadHeader`, `LoadInfo`, `LoadFull`, `Load`, `ReadSample` and
  `FillShootingInfo` now describe this copy: no SIMPLE check, InvalidDataException only,
  EXPTIME / ISOSPEED / DATE-OBS / XPIXSZ read.
- In `PngWriter.cs`, the file note and the class summary were cut to what this copy has; no
  MapRange, no mosaic. In `Stats.cs`, the class summary no longer names verdicts.
- The two ADR numbers `BitmapFont.cs` took from FlatCheck are named as FlatCheck's.
- Layout: FlatCheck's blank lines between documented members came with its text (in `Img.cs`
  and others), and six blank lines separate the documented members of two enums (`Verdict`,
  `LeakCause`). No code moved.

**Wrapping:** `wrap.py` broke 259 existing one-line comments past 100 columns into block form.
Where it left a fragment in the middle of a paragraph, the paragraph was reflowed
(`reflow2.py`, scratchpad).

**False comments corrected:**
- `Planes.MedianFilter`: the window is 2·(window/2)+1 values, and the ends are padded, as scipy's
  `nearest`.
- `FrameStack.Load`: up to `threads` rasters in flight, not four.
- `DarkSeriesAnalyzer.PlateauFit`: NaN also under three points and for a span that is not
  positive.
- `DarkOutputs`: `--out` writes the lights CSV too, and the map PNG is 8-bit RGB with equal
  components.
- `InventoryReport.Settings`: the example says "expuneri sau ISO diferite", as the code prints.
- `IndexView`: four buttons, not three.
- `MainViewModel.OpenAsync`: a cancelled run leaves no result and says it was stopped; only the
  core's folder errors become the problem card.
- `docs/design/window.md`: `SeriesChart` draws five y ticks, not four.
- One new comment was wrong at first and was caught in review: `DarkReport.Sections` returns
  nine sections, not ten.

**Left as it is:** `MatchAnalyzer`'s `ModeExposure` comment says "Dictionary keeps insertion
order". That holds when nothing is removed, as here, but .NET does not document it. No code
defect was found.
