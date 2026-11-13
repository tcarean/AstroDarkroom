# Review the whole code and list what to fix

## Context

ImageVisualCompare's first code was written by an earlier model and had almost no code review.
The owner wants the whole project read once, critically, and one documented list of problems at
the end, so that later sessions fix them one at a time in the order of their severity. Measured
on 2026-11-13, blank lines included: 8,864 lines of code in 57 files
(`src/ImageVisualCompare.Core/` 3,217, `src/ImageVisualCompare.Wpf/` 5,647) and 8,081 of tests.
That would fit one context, but a long reading is done well at the start and thinly at the end,
so the code is read by area, each area by a reviewer with a clean context, and the findings are
merged into one list. The owner approved this in chat on 2026-11-13.

This task **changes no code**. It writes one findings document, one ADR, one line in
`CLAUDE.md` and this plan.

## Scope

In: `src/` (the folder `kit.json` lists as code).

Out:
- `scripts/`, `.claude/` - the kit's; a problem there is fixed in the kit.
- The tests, line by line. A reviewer opens a test only to see whether a suspected defect is
  already covered. Test strength is `/mutate`'s job for Core.

## The six areas

Five were announced; with the file list in hand the window is too much for one reader (about
3,100 lines), so the comparison surface and the shell are read apart. Every file under `src/`
belongs to exactly one area.

| # | Area | Files | Lines | Notes and ADRs |
|---|------|-------|------:|----------------|
| 1 | Zoom, pan, divider, layout records | Core: `Viewport`, `ZoomLadder`, `DividerModel`, `DividerLayout`, `Geometry`, `PointD`, `RenderPlan`, `MinimapGeometry`, `ScrollBarState`, `PairLabelLayout`, `ImagePairValidator`, `ImageSide`, `ImageInfo` | 1,272 | `rendering`, `shell` (pair label); ADR 0009 to 0014, 0019, 0048 |
| 2 | FITS and the display modes | Core: `FitsHeader`, `FitsRowDecoder`, `FitsScale`, `FloatSamples`, `DisplayMode`, `DisplayRenderer`, `SampleHistogram`, `LevelLookup`, `RainbowTable` | 1,471 | `imaging`; ADR 0029, 0035, 0039, 0040 |
| 3 | Loading the images | Wpf: `Imaging/` | 1,568 | `imaging`; ADR 0008, 0014 to 0016, 0022, 0029, 0035, 0047 |
| 4 | The comparison surface and the theme | Wpf: `Controls/`, `Theme.xaml`, `Interop/DarkTitleBar` | 1,608 | `rendering`, `shell`; ADR 0009 to 0013, 0018, 0019, 0040, 0044, 0048 |
| 5 | The main window and the dialogs | Wpf: `MainWindow`, `MessageDialog`, `AboutWindow`, `AboutViewModel`, `Services/FileDialogService`, `Version`, `ThirdParty`, `AssemblyInfo`, the csproj, `Directory.Build.props` | 1,478 | `shell`, `versioning-and-licence`; ADR 0004, 0017, 0018, 0023, 0030, 0042, 0044, 0045, 0047, 0048 |
| 6 | Start-up, the log and the texts | Wpf: `Program`, `App`, `Logging/`; Core: `UiText`, `UiLanguage`, `StartupArguments` | 1,467 | `logging`, `language`; ADR 0038, 0041, 0043 |

## Steps

1. **Worktree and branch**, from the ImageVisualCompare folder of the main checkout:
   `git worktree add .claude/worktrees/Review-the-whole-code -b claude/Review-the-whole-code main`.
   Everything below happens in that worktree.

2. **Six reviewers in parallel** (general-purpose subagents on this session's model, read-only:
   no edit, no build, no test run). Each brief gives the area's files, its notes and ADRs, what
   to look for in that area, and these instructions:
   - Read `CLAUDE.md`, `docs/design/code-map.md`, the notes, the ADRs and the area's
     requirements first; something they say is deliberate is not a finding, and code that does
     not do what they say is one.
   - Read every file of the area whole; follow callers as far as a case needs, and check a case
     is reachable from what the window lets a user do.
   - A finding needs a concrete case: which input or sequence of actions, what comes out, what
     should. No case, no finding.
   - Report each as: title; severity; reach; `file:line`; what the code does; the case; whether
     a test covers the spot; confidence (read and certain / plausible, needs a run); the
     direction of a fix. Cleanup items in a separate short list.

3. **I verify every finding in the code** before it enters the list: open the cited lines,
   follow the callers, check the note and the requirement. Outcome per finding: confirmed,
   rejected (with the reason, counted but kept out of the list), or "to measure" (something only
   a run of the application or an outside source can settle; the task that takes the entry
   starts by showing it). The same defect seen from two areas becomes one entry.

4. **The seams, read by me**: what `ImagePairLoader` returns against how `MainWindow` and
   `CompareView` use it (a failed half, a cancelled question, the padded canvas), and the texts
   of `UiText` against the places that fill their placeholders.

5. **Write `docs/review/2026-11-13-code-review.md`**: how the review was done and its scope; how
   to read an entry; a summary table (ID, severity, reach, evidence, title, status `open`); one
   section per entry, ordered by severity and then by reach, with an ID `CR-NNN` that never
   changes, the file and lines, what happens, the case, what should happen, the direction of a
   fix (not the fix), the test that would reproduce it; a last section "Cleanup".

   Severity, by the kind of harm in a tool whose purpose is to show a small difference truly:
   1. The comparison lies: a wrong pixel shown, the two images out of step, a wrong value read,
      or one file named while another is shown.
   2. A crash, a hang, a file left locked, or memory never given back.
   3. A wrong text or a wrong layout.
   4. Robustness and cleanliness.

6. **ADR 0051** (number checked again at commit time): the whole-code review, its list in
   `docs/review/`, the severity scale, one entry per task. One line in `CLAUDE.md`'s Layout for
   `docs/review/`.

7. **Gate, commit, merge proposal**: `scripts/verify.ps1` in the worktree, read the diff, commit
   on `claude/Review-the-whole-code`, propose the merge and ask. No CHANGELOG line (a document,
   no behaviour); each fix adds its own.

## Verification

- Every entry of the list cites a `file:line` that I opened myself; none is copied from a
  reviewer unread.
- Every file under `src/` belongs to exactly one area (listed against the table before the
  reviewers started).
- The findings document says how many reports each area returned and how many were rejected, so
  the yield is visible.
- `scripts/verify.ps1` green before the commit; `git diff main --stat` shows documents only.
