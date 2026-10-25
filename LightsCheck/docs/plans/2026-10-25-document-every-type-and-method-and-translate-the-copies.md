# Document every type and method, and make every comment English

## Context

The owner asked for two things (2026-10-24): backfill the XML documentation comments in the
code folders (the kit's rule "Documentation comments", the kit's ADR 0035), and make every
other comment in the code English, with English as the only language for comments from now on.
The owner also said: change no functionality. The task was paused on 2026-10-24 until
FlatCheck's backfill was done, because LightsCheck's only Romanian comments are in files copied
from FlatCheck. That backfill is now merged (FlatCheck `504b5d1`, ADRs 0059/0060), so the
copies can take FlatCheck's English text instead of a second translation.

## Facts measured (2026-10-25, `main` at 504b5d1, `doccount` from APA's backfill)

- `src/`: 57 files. 119 types (26 undocumented, 45 missing `<param>`s, mostly positional
  records) and 300 methods (164 undocumented, 131 incomplete): **71 types and 295 methods to
  touch**. Properties: 160, 134 undocumented (optional under the rule).
- Romanian comment lines: 226 in `src/`, all in the 8 `Imaging/` files "copied unchanged"
  from FlatCheck. Plus `Theme/Dark.xaml` (23 comments), 14 lines in the tests
  (`FitsTestData.cs`, `FitsReaderTests.cs`, also copies), and two `pe RA` quotes of report
  output in `SeriesAnalyzerTests.cs`. The other hits in `SeriesReport.cs` and
  `VerdictPalette.cs` quote Romanian output and stay.
- The code of the copies compared with FlatCheck's current files, by code tokens (no comments,
  no whitespace; namespace and `using` lines aside):
  - **Same code:** `BitmapFont`, `CfaPattern`, `IFrameReader`, `Img`, `Mosaic`.
  - **Different code:** `FitsReader` (FlatCheck's later FITS-magic check, ADR 0057, plus
    LightsCheck's keywords), `PngWriter` and `Stats` (the parts ADR 0003 cut), the adapted
    `RawReader` and `ImageLoader` (already English), and the window's copies `AppIcon`,
    `Converters`, `RasterBitmaps`, `VerdictPalette`, `DarkTitleBar`, `Dark.xaml` (cut or
    varied).
- 161 comment lines are over 100 columns. `GenerateDocumentationFile` is on and CS1591 is muted
  (`Directory.Build.props`); CS1573 and CS1574 are active, so the build checks partial
  `<param>` sets and every `cref`.

## Decisions taken in this plan (stated assumptions)

1. **Scope:** "the rest of the comments" covers `src/` (C#, XAML, `Icon/make_icon.py`) and the
   tests' comments. Tests stay exempt from the XML tags (kit rule); their comments are only
   translated.
2. **The copies:** a file with the same code takes FlatCheck's file whole, keeping LightsCheck's
   GPL header, the `#nullable disable` line, the namespace and its usings. A file with different
   code keeps its own code and takes FlatCheck's comment member by member where the code
   matches. Where it does not match (LightsCheck's additions, the cut parts), I translate or
   write the comment. **FlatCheck's later code (the FITS-magic check) is not brought over**:
   that would change behaviour. It stays the follow-up task it already was.
3. **Tags:** FlatCheck's assumption 2 (its plan of 2026-10-24): always a `<summary>`, a
   `<param>` per parameter (a positional record's on the type), `<typeparam>`, and `<returns>`
   for a value. A plain `Task` gets no `<returns>`. No new `<exception>` or `<remarks>` tags.
   `<inheritdoc/>` only where `IFrameReader` states the whole contract. Framework overrides,
   converters, constructors and event handlers get their own text.
4. **Content:** what a caller needs: units (ADU, px, arcsec, degrees, fraction, percent,
   seconds), what null or NaN means. A threshold is named by a `cref` into `Thresholds`, never
   quoted as a number. A `cref` is fully qualified rather than adding a `using`, so the code
   tokens stay identical. Every comment line ends within 100 columns, and added lines are
   ASCII only.
5. **A comment found false** is corrected in a commit of its own, with its evidence, and listed
   at wrap-up. Code that looks wrong is reported, never changed.
6. **Nothing else changes:** no requirement, no test logic, no golden, no README, no version.
   No `/validate`: the token proof shows that no formula moved.
7. **One branch, one session:** about a quarter of FlatCheck's size. LightsCheck's own folders
   are documented by four fork subagents in parallel, on disjoint files, in the same worktree:
   (A) `Core/Analysis/`; (B) `Core/Report/`; (C) `Core/Runs/`, `Core/Session/`, the core's
   root files and `Cli/`; (D) the window apart from the copies. The forks do not build and do
   not commit. I do the copies and the tests myself, then build, read every fork's diff and
   commit.

## Steps

Branch `claude/Document-and-translate-LightsCheck`, worktree
`E:\Photografie\Code\.claude\worktrees\Document-and-translate-LightsCheck`, cut from `main`.

1. Save this plan as `docs/plans/2026-10-25-document-every-type-and-method-and-translate-the-copies.md`.
2. ADR 0017: every comment is English, including the copies, which take FlatCheck's English
   text. The header says `Supersedes: 0003 in part (the copies keep Romanian comments); 0005 in
   part (the copies' comments Romanian)`, and the Status lines of 0003 and 0005 get `; ...
   superseded by 0017` (the kit's ADR 0024). Also update `CLAUDE.md` (the rules "Languages" and
   "Copied files stay copies", and the Style line), the `.editorconfig` comment ("Romanian
   comments" removed, the section kept), and the marker of each copy (`copied from FlatCheck
   unchanged: code of 2.3.0, comments of 504b5d1`). Commit.
3. The copies: the 5 files with the same code, then `FitsReader`, `PngWriter`, `Stats`,
   `RawReader`, `ImageLoader`, the window's copies, `Dark.xaml`, and the two test copies.
   Commit.
4. Forks A-D on LightsCheck's own files; meanwhile I translate the `pe RA` quotes and check
   `make_icon.py` and the window's XAML. Build, fix every CS1574/CS1573, read the diffs. Commit
   per area: core, then console and window.
5. False comments, if any: a separate commit.
6. Design notes: `scripts/check-design.ps1 -Since main`. Each listed note is read against the
   change (none is expected to describe the comments).
7. `CHANGELOG.md` `[Unreleased]` gets an `Internal` line. The plan gets a "What deviated"
   section. Then `/wrap-up` and the merge proposal.

## Verification

- `scripts/verify.ps1` green at every commit (the hook's `== OK`), with the same test counts as
  `main`.
- `doccount count` on `src/`: 0 types and 0 methods to touch.
- `doccount tokens` against a `git archive` export of `main`: every `.cs` file under `src/`
  and `tests/` has identical code (string literals included). This is the proof that no
  functionality changed. XAML: the files compared with their comments stripped, which must be
  identical. `make_icon.py`: diff read.
- `doccount facts`: every number, ID and identifier of a translated old comment is found again
  in the new one.
- `doccount romanian` on `src/` and `tests/`: only quoted output remains, each line listed.
- `doccount width`: no comment line over 100 columns. Added lines are ASCII.
- The whole diff read back before each commit.

## Follow-up, not in this task

- Copy FlatCheck's FITS-magic check (ADR 0057) into `FitsReader`, since it changes behaviour.
  Recorded in "What deviated" and offered at wrap-up.

## What deviated (2026-10-25, `claude/Document-and-translate-LightsCheck`)

- **The ADR is 0018, not 0017.** 0017 was already taken by the .NET 8 target, merged into
  `main` while this plan was being written. The plan's text above keeps 0017; every file of the
  branch says 0018.
- **The false comments are corrected in the same commits, not in one of their own.** The forks
  fixed them inline. Splitting the hunks out afterwards would have been error-prone, so each
  one is listed here with its evidence:
  - `FitsReader.LoadHeader`/`LoadInfo`: "an empty dictionary if the file does not begin with
    SIMPLE". The copy checks no SIMPLE card. A non-FITS file gives whatever cards with `=` in
    column 9 it holds, and a header cut before END gives the cards read so far.
  - `Stats.cs` header: "verdicts". ADR 0003 cut them from the copy.
  - `Options` summary: it listed the options without `--est` and `--nord`, which `Parse`
    accepts.
  - `MainViewModel.OpenAsync`: "a cancelled run leaves the window as it was". `Clear()` runs
    first, so the window is left empty with `Status` saying the run stopped.
  - `MainViewModel` class summary: "every sentence comes from the core". The view-model writes
    a few labels of its own (see the questions below).
  - `ParagraphVm`: it named only the Respinse explanation (R-037). The Dithering one (R-047)
    uses it too.
  - `DitherMap.OnRender` and `docs/design/window.md`: the grid is 10 px, and 50 px once the span
    passes 400 px.
  - `SeriesChart.Minimum`: without a minimum the axis starts 8 % of the range under the smallest
    value or guide line, not at the smallest value.
  - `Raster.TileMedians`: a raster smaller than a tile gets square tiles of its shorter side,
    not one tile (`tile = min(tile, min(H, W))`).
  - `FrameInfo.Read`: the error case is a FITS-named file with an empty header, not strictly
    "one that does not begin with SIMPLE".
  - `Th.HotPixelRunMax`: the run is consecutive frames within CoverageTight of the first of them,
    not of each other (`Dithering.Runs`).
- **`/validate` ran** (the wrap-up skill asks for it when numeric files are touched), though the
  token proof already showed no code moved: `Passed: 13, Failed: 0` (7 min 54 s) on the
  sessions under `E:\Photografie\Astro`.
- **Checks, at the end:** `doccount count` on `src/`: 119 types and 300 methods, 0 to touch.
  `tokens`: 57 code files and 35 test files with identical code. `facts`: 0 missing. `romanian`:
  19 lines in `src/`, all quoted output or verdict words (`SeriesReport.cs`, `VerdictPalette.cs`),
  0 in `tests/`. `width`: 0 comment lines over 100 columns. `Dark.xaml` with its comments
  stripped is identical to `main`'s. Two added lines are not ASCII (`§5(d)`, `←, ↓`); both are
  existing text that was only rewrapped. Gate: 246 + 76 tests.

### Questions for the owner (code, not changed)

1. `--pixel NaN` and `--pixel Infinity` pass `Options.Parse` (only `p <= 0` is refused), and a
   NaN or infinite pixel size reaches the arcsec-per-pixel figure. This is APA's gap. The
   finding comes from reading the code; it was not run.
2. `MainViewModel.LoadCropsAsync`: the error path writes `CropsNote` without the
   `ReferenceEquals(_selectedFrame, row)` check the success path has. A decode that fails after
   another frame was selected overwrites the newer frame's note (R-031: a later selection wins).
3. The view-model writes Romanian words of its own: the guide labels (`miscare >= ... px`,
   `(walking noise)`, `la limita`, `SLAB`), the `scara` label and the `EROARE: ` prefix; the
   map control (`DitherMap.OnRender`) writes `grila la ... px`. That may go against the rule
   "Computed only in the core".
4. `RelayCommand` (`Presentation/Mvvm.cs`) and `ChartElement.AxisBrush` are not used anywhere.
5. R-021 says the hot-pixel runs are frames "within 3 px of each other". The code measures
   within 3 px of the run's first frame, as for the other runs.
6. Edge cases reported by the forks, unreachable today: `Registration.FitSimilarity`
   dereferences a null `Stats.Solve` result on singular equations. `Raster.BilinearResize`
   divides by zero for an output of 1 px on a side. `SeriesReport.Signed` writes `+-0` for
   negative zero. `SeriesReport.Minutes` assumes the frame indices are the 1-based positions in
   `Frames`. `FrameMeasurement.SaturatedStars` counts every saturated component
   (`FrameAnalyzer`), not only the star-sized ones.

### Review of the backfill (2026-10-25, a fresh subagent, read-only)

A reviewer with none of this session's context read every changed comment against the code it
describes, the copies also against main's Romanian and FlatCheck's text. Fixed, comments only:
- `Dark.xaml`: the LogBox comment translated the old, false Romanian line ("the only TextBox";
  there is no Siril run window, and the style is not used) where FlatCheck had already fixed
  it; and the verdict order is `VerdictPalette.Severity`, not `Stats.Severity` (cut here).
- `FrameInfo.Read`'s inline comment still said the reader answers a non-SIMPLE file with an
  empty header; `FitsReader.LoadHeader` keeps only the cards of the whole blocks before a cut.
- `NoiseAnalyzer.WindowHalf`: the window is 600 x 600 of the position (1200 x 1200 of the
  frame), not 300 x 300 (`n = 2 * half`; `noise2.py`, R-024 agree).
- `Thresholds`: `ElongationMarginal` is "at or above" (`>=`), `ElongatedRatio` and
  `AngleVoteRatio` are "above" (`>`).
- `FrameReport.Zone`: a zone without a figure prints its star count, `- (3)`, not `- (0)`.
- `DitherAnalysis.MovesOnXOnly`/`MovesOnYOnly`: the other component is under `Th.AxisStep`, not
  merely smaller.
- `SeriesOutputs.Csv`: only the file, the reasons, the arrows and the error are quoted.
- `Program.Main`: --help also gives 0.
- `SeriesAnalysis.ReadNoise`: null also when a bias cannot be read or the two differ in size.
- The markers of `FitsReader`, `PngWriter` and `Stats`, ADR 0018 and `CLAUDE.md` now name the
  edits ADR 0003 lists (FitsReader's keywords and exception type, PngWriter's MemoryStream).
- Question 3 above: `grila la` is written by the map control, not by the view-model.
Left as they are, by ADR 0018: the copies' English text still names FlatCheck's own parts
(the Synthetic and histogram tabs, ExposureAnalyzer, --light, FitsWriter, the attribution
mode), as their Romanian did; they change when FlatCheck's text does.

New code questions from the review (not changed):

7. `lightscheck --frame N` on a raw libraw cannot open ends in an unhandled exception:
   `RawReader.Load` lets `LibRawException` (a plain `Exception`) through, and `Program.Run`
   catches only IO, InvalidData, InvalidOperation, Argument and UnauthorizedAccess exceptions.
   The same for a FITS whose data is shorter than its header says (IndexOutOfRangeException in
   `LoadFull`). The series and inventory runs catch per frame, so only `--frame` is hit.
8. `MainViewModel.OpenAsync`: the progress callback does not check that its run is still the
   current one (`finally` does, with `ReferenceEquals(_cts, cts)`), so posts queued by a
   cancelled run may write `Progress` and `BusyMessage` over the new run's (plausible, not
   traced to the end).
