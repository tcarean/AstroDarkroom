# Say in red on the Structure tab when Adobe's lens profiles are missing

Plan for the task branch `claude/Say-when-Adobe-profiles-are-missing`, worktree
`FlatCheck/.claude/worktrees/Say-when-Adobe-profiles-are-missing`, cut from `main` (829e9bb).
Approved by the owner on 2026-10-28.

## Context

The owner asked (2026-10-28): when Adobe's lens profiles are missing, the Structure tab's
section "VIGNETTING against the lens profile" shows a red message saying so, followed by
instructions on what the user can install to get them.

Today nothing on the card says Adobe is missing. FlatCheck ships lensfun but not Adobe's `.lcp`
files (they are read where Adobe installed them, never redistributed), so on a machine without
an Adobe product the comparison silently runs on lensfun alone, and its figures and even the
lenses it picks change (measured below).

## Measured facts (2026-10-28)

1. **This machine has Adobe**: 3531 `.lcp` in `C:\ProgramData\Adobe\CameraRaw\LensProfiles\1.0`
   (DNG Converter), 0 in `%APPDATA%\Adobe\CameraRaw\LensProfiles`, 3633 in each of Lightroom CC's
   and Lightroom Classic's `Resources\LensProfiles\1.0`. The message never shows here: the tests
   and the window render use hand-made sources without Adobe (`LensProfileTestData`), and the
   console's `--profiles <folder holding only lensfun/>` reproduces such a machine on real masters.
2. **Tokina 11-20 sky master (V-037) + `flats/DSC_0001.NEF`**, the console built on main
   2026-10-27 (`FlatCheck/bin/Release/net8.0/flatcheck.exe`, run, not built):
   - with Adobe: 4 rows, nearest `Adobe Tokina AT-X 11-20mm F2.8 PRO DX`, -0.39 stops = 31.5 % REDO;
   - lensfun only: 2 rows, nearest `lensfun Tokina AT-X 11-20 F2.8 PRO DX`, -0.58 stops = 49.3 %
     REDO, and the note "the lens ID 139 matches no profile: a manual lens or one unknown to the
     databases". Nothing says Adobe is missing;
   - neither: "cannot compare: no lens profile on this machine - neither Adobe (...) nor the
     lensfun/ folder beside the program" (`StructureReport.GapText`, `ComparisonGap.NoProfiles`).
3. **Nikkor 35 DX f/2.8 sky master + `flats/DSC_0031.NEF`, lensfun only**: 6 rows found by range,
   among them Nikon Z / Sony E / Leica L lenses (MEKE SL 35mm F1.8, Nikkor Z 35mm f/1.8 S, Viltrox
   AF 35mm f/1.8 Z), and the verdict (ACCEPTABLE, +0.20 stops = 14.7 %) is the Viltrox Z's, a lens
   that does not fit a D3300. Cause: `LensIdentification.CandidatesByRange` keeps a lens whose
   "mount + maker" contains the camera's first word (`NIKON`). Not in `known-issues.md`; with Adobe
   the lens ID decides and it never shows on the owner's machine. **A finding outside this task.**
4. The window's `ProfileCatalog` locates the profiles once per window; a search that finds no
   Adobe file is not a failure, so it is not repeated: after installing, FlatCheck must restart.
5. Without a raw in `flats/` the Structure tab does not load the catalog (it compares only with a
   raw), so it does not know whether Adobe is there.
6. Adobe's DNG Converter page, checked in the browser: the old helpx URL redirects to
   `https://helpx.adobe.com/camera-raw/desktop/dng-and-file-formats/adobe-dng-converter.html`,
   which offers "Download latest DNG Converter for Windows x64" and "... Windows ARM64".
7. The window's red for a message is `#F0857E` (`MessagePanel`, `MainWindow`, this tab's
   `SaveError`). Its only links are in About: `Hyperlink.RequestNavigate` caught by the window
   (`AboutWindow.OpenLink`), styled by `Dark.xaml`'s `Hyperlink` style.
8. The unmerged `claude/Fix-the-diagnostic-log-review-findings` holds ADRs 0071 and 0072 and
   touches `StructureReport.cs`, `SyntheticReport.cs`, `StructureTabViewModel.cs` in other hunks:
   this task takes **ADR 0073** and **R-113**, and checks main's latest ADR before the merge.

## Decisions

D1, D2, D3 and D6 were put to the owner with their alternatives on 2026-10-28 and the owner chose
the recommended option of each; D4, D5 and the colours are stated assumptions that the plan's
approval accepts. ADR 0073 records them with the rejected alternatives (D1: also when Adobe has no
file for this lens; D2: a line in the console's report too; D3: the product's name without a URL;
D6: fixing the mount filter in this task).

- **D1 When**: only when the search found no Adobe `.lcp` in any folder
  (`ProfileSources.AdobeFiles == 0`), whatever the card then holds (lensfun rows, a gap, no
  profile at all). Not when Adobe is installed but has no file for this lens: the install
  instructions would be false there.
- **D2 Where**: the window only, as asked. The texts are composed in the core (`StructureReport`),
  so a later console line is one call; the console's report, the saved `Test result.txt`
  (identical to the console's, R-081 / R-090) and every golden stay unchanged.
- **D3 Link**: a clickable link to Adobe's DNG Converter page, opened in the browser as About's
  links are.
- **D4 No raw**: no message (nothing was searched; the card's "no raw" comes first, and in the
  Siril workflow the raws are in `flats/`, ADR 0031).
- **D5 After installing**: the text says to restart FlatCheck; `ProfileCatalog` is unchanged.
- **D6 The Nikkor 35 finding**: a separate task after this one.
- **Colours** (stated assumption): the statement in red; the instructions in the card's muted
  text, the link in the theme's accent, the folders searched in faint text.

## Requirement (draft, `docs/requirements.md`, section "The window")

| R-113 | draft | When the window's profile search finds no Adobe `.lcp` file in any of its folders
(`ProfileSources.AdobeFiles` is 0), the Structure tab's card under `VIGNETTING against the lens
profile` opens with a red paragraph (`#F0857E`, the window's colour for a message) saying that
Adobe's lens profiles were not found on this computer and what that leaves (lensfun alone; a raw
that names no lens matched by its focal range alone), then, in the card's own colours, what to
install (the free Adobe DNG Converter, or Lightroom Classic, Lightroom or Photoshop's Camera Raw)
and to restart FlatCheck, a link to Adobe's DNG Converter page that opens in the browser, and the
folders searched. It sits above whatever the card holds (lensfun rows, a gap, no profile at all)
and shows only when the tab searched, that is with a raw in `flats/`; with any Adobe file found it
is absent. The texts come from `StructureReport`, in both languages; the console's report, the
saved `Test result.txt` and the golden files do not change. Example: the Tokina 11-20 sky master
with `DSC_0001.NEF`, against a catalog holding only lensfun/, shows the paragraph above the two
lensfun rows (the nearest -0.58 stops = 49.3 %, REDO); against the machine's 3633 Adobe files, it
is absent. |

## The texts (draft; Romanian without diacritics)

- Red (`StructureReport.AdobeMissing(ProfileSources)`, "" when `AdobeFiles > 0`):
  - EN: "Adobe's lens profiles were not found on this computer. Without them the lens is looked
    for in lensfun alone, the database that ships with FlatCheck: one database instead of two,
    and a raw that names no lens (a Nikon writes only an ID) is matched by its focal range alone."
  - RO: "Profilele de obiectiv Adobe nu au fost gasite pe acest calculator. Fara ele obiectivul e
    cautat doar in lensfun, baza livrata cu FlatCheck: o singura baza in loc de doua, iar un raw
    care nu numeste obiectivul (un Nikon scrie doar un ID) e potrivit doar dupa gama de focale."
- Instructions (`StructureReport.AdobeInstall()`):
  - EN: "To get them, install the free Adobe DNG Converter, from the page below; Lightroom
    Classic, Lightroom and Photoshop's Camera Raw install them too. Then restart FlatCheck: it
    looks for them only once while it runs."
  - RO: "Ca sa le ai, instaleaza Adobe DNG Converter, gratuit, de pe pagina de mai jos; le
    instaleaza si Lightroom Classic, Lightroom si Camera Raw din Photoshop. Apoi reporneste
    FlatCheck: le cauta o singura data cat timp ruleaza."
- Link: `StructureReport.AdobeDngConverterUrl`, shown as the URL itself.
- Folders (`StructureReport.AdobeSearched(ProfileSources)`): "Searched in: " / "Cautate in: " +
  `ProfileSources.AdobeDirectories`, comma-separated.

## Steps

1. **Worktree and records.** From `E:\Photografie\Code\FlatCheck`:
   `git worktree add .claude/worktrees/Say-when-Adobe-profiles-are-missing -b claude/Say-when-Adobe-profiles-are-missing main`.
   R-113 in `docs/requirements.md` (accepted), `docs/decisions/0073-...md` from
   `0000-template.md` (accepted, with the owner's answers and the rejected options), this plan
   in `docs/plans/`.
2. **Tests first, failing** (stubs returning "" so they compile and fail on their assertions;
   the failing output pasted):
   - `FlatCheck.Tests/StructureReportTests.cs`: `R113_...` - `AdobeMissing` is "" with one Adobe
     file and the paragraph with none; English and Romanian differ, no diacritics; `AdobeInstall`
     names the DNG Converter and the restart; `AdobeSearched` lists every searched folder; the URL
     is https on helpx.adobe.com.
   - `FlatCheck.Wpf.Tests/StructureTabTests.cs`: `R113_...` - with a raw and lensfun-only sources
     the tab shows the paragraph, the instructions, the link and the folders, the rows stay the
     lensfun ones, and the saved report is still character for character the console's; with
     Adobe files nothing; without a raw nothing; with no profile at all the paragraph above the
     NoProfiles gap; `Reset` clears it. Lensfun-only sources: `ProfileLocator.Locate` on an empty
     Adobe folder and the fixture's `lensfun/` (a `withAdobe: false` option on
     `LensProfileTestData.Sources`).
   - `FlatCheck.Wpf.Tests/WindowSmokeTests.cs`: a render `tab-structura-fara-adobe` with no
     binding errors.
3. **Core** (`FlatCheck.Core/Report/StructureReport.cs`, beside `ProfilesHeading` / `GapText`):
   `AdobeDngConverterUrl`, `AdobeMissing`, `AdobeInstall`, `AdobeSearched`, each with its XML
   documentation comment. The "is Adobe missing" test lives here, not in the view-model.
4. **Window.**
   - `FlatCheck.Wpf/ViewModels/StructureTabViewModel.cs`: `AdobeMissing`, `AdobeInstall`,
     `AdobeLink`, `AdobeSearched` (all "" unless the core says so); `Populate` takes the sources
     the run searched (null without a raw, so nothing shows); `Reset` clears them.
   - `FlatCheck.Wpf/Views/StructureView.xaml`: a block at the top of the profile card, visible
     when `AdobeMissing` is not empty: the red `TextBlock` (`#F0857E`), the muted instructions, a
     `Hyperlink` bound to `AdobeLink`, the faint folders line.
   - Links: `AboutWindow.OpenLink`'s body moves to one static helper in
     `FlatCheck.Wpf/Presentation/` (new file, GPL header), called by About and by a
     `Hyperlink.RequestNavigate` handler on `MainWindow`, which catches the tab's link as it
     bubbles; `StructureView.xaml.cs` stays code-free.
5. **Documents.** `docs/design/two-applications.md` (the Structure tab paragraph),
   `docs/design/lens-profiles.md` (Adobe's absence is now said on the Structure card),
   `docs/design/code-map.md` (the new members and the link helper), `README.md` (the Structure
   tab's card, near "With no raw, the card says why"), `CHANGELOG.md` (Added).
6. **Verify.** `scripts/verify.ps1 -SkipTests` while editing; the window render
   (`FLATCHECK_DUMP_SCREENS=<scratch> dotnet test FlatCheck.Wpf.Tests --filter WindowSmokeTests`)
   looked at and shown to the owner; `dotnet build FlatCheck.sln -c Release --no-incremental
   -warnaserror --no-restore`; the full gate; `/validate` (a `Report/` file changed) with its
   drift table pasted (expected: no drift, no figure moved); `GoldenOutputTests` and
   `EnglishOutputTests` pass with no golden regenerated.
7. **Wrap-up.** R-113 `implemented`, `/wrap-up`, one-line commits on the branch, the subagent
   review offered (then a second one on the fix commit), the merge proposed.

## Out of scope

- The Nikkor 35 finding (fact 3): its own task (D6).
- The Sintetic tab's sources panel already says "no profile found in ..." (`SourcesSummary`) and
  where Adobe's profiles come from (`AdobeWhere`); it is not touched.
- No change to `ProfileCatalog`, `ProfileLocator`, the identification or any figure.

## Outcome (2026-10-28)

- Done as planned, with three differences. `docs/design/lens-profiles.md` got only a pointer
  and the fixture's new option: no file in `LensProfiles/` changed, and the new
  `StructureReport` members are described in `docs/design/modes-structure.md`, which covers that
  file in full. The view-model's
  `AdobeInstall` and `AdobeLink` are constant properties from the core, and only `AdobeMissing`
  and `AdobeSearched` depend on the search. Moving the link's code into
  `Presentation/ExternalLink.cs` failed `RepoInvariantTests.OnlySirilScriptRunAndTheAboutLink_StartAProcess`
  (ADR 0055): the owner chose to list `ExternalLink.cs` in place of `AboutWindow.xaml.cs` over
  keeping `Process.Start` in the About window or dropping the clickable link. The test keeps its
  name, which ADR 0055 cites; ADR 0073 records the choice.
- Tests first: against stubs returning "", 7 of the 10 R-113 tests failed on their assertions;
  the 3 that check the message's absence passed, as they must.
- The window render `tab-structura-fara-adobe.png` shows the red paragraph, the instructions, the
  link and the folders searched at the top of the card, above the lensfun row.
- Gate green (1123 + 334 tests); no golden regenerated. `/validate` on the real masters: the 36
  large files present, 48 validation tests passed, every figure of V-001 to V-040 equal to the
  recorded one (drift 0), V-041 to V-048 unchanged.

## Code review (2026-10-28)

A subagent reviewed the branch (6553cda) against `main`; its six findings were checked in the code
and all held. Applied in the second commit:

- Fact 5 and D4 rested on a false premise: the window does search the profiles without a raw,
  because the Sintetic tab's prefill loads the shared `ProfileCatalog` at every opening. Put back
  to the owner, who kept D4; ADR 0073's reason (the search's cost) was corrected, and the README,
  the CHANGELOG, R-113, the window note and the no-raw test's summary now say "when the tab's run
  searched", with a raw in `flats/`.
- With no profile at all, the red paragraph said the lens is looked for in lensfun while the gap
  under it said lensfun's folder is missing too: without `lensfun/` the paragraph now keeps its
  first sentence only (a failing test first, in each test project).
- The link's opening had no test: a new render test raises `RequestNavigate` on the card's link
  and checks that it carries Adobe's URL out of the tab, and a text check pins the main window's
  handler (raising it in a real window would open the browser). The owner chose this over a
  launcher seam with a `MainWindow` test.
- The tests named "Above" checked only the view-model: the render test now finds the red block,
  visible and red, above the first row; the other test was renamed.
- `modes-structure.md` said the console's report is the same with or without Adobe (its rows
  differ); it now says R-113 adds no line to it. The About window's link handler, `ExternalLink`'s
  header and `Dark.xaml`'s `Hyperlink` style comment named too few of the links they serve.
- Gate green after the fixes (1124 + 336 tests). `/validate` again, since `Report/` changed: 48
  passed, every measured line identical to the first run's.

## Second code review (2026-10-28)

A second subagent reviewed the fix commits alone (046f3e3, a2e29b5); its six findings held.
Applied in the third fix commit:

- The block's place above a gap, and its collapse when Adobe's files are found (the constant
  instructions and link included), had no rendered test: the render test now covers both.
- Two test names still said "nothing searched"; they now say the run searched nothing.
- The one-sentence paragraph had not been put to the owner: the owner confirmed it.
- It keyed on `LensfunPresent` (the folder holds an `.xml`), so a folder whose files cannot be
  read gave the contradiction again: it now keys on the lenses read, `LensfunLenses == 0`, the
  gap's own condition (a failing test first). The core test's hand-made sources, which claimed a
  lensfun folder with no lens in it, now hold the fixture's lensfun lenses, on the owner's yes.
- The smoke tests' header, this plan's line on the link test (it is a separate test), and three
  ragged lines were corrected.
- Per the owner's earlier limit, no third review round.

## Third code review (2026-10-29)

At the owner's request, a third subagent reviewed all five commits against `main`. It found no
bug in the branch's own code; its five findings held:

- Fact 8 understated the overlap with `claude/Fix-the-diagnostic-log-review-findings`, checked
  with `git merge-tree`:
  - two textual conflicts, `AboutWindow.xaml.cs` (both rewrite `OpenLink`'s catch) and
    `docs/validation.md` (both add a History entry at the top);
  - that branch's eight-parameter `ProfileSources` constructor (no `adobeSkipped`), which the two
    hand-made sources in `StructureReportTests` must follow after a merge;
  - its `LoggingTests.NoLogLineGoesThroughTheTranslation`, which rejects the inline
    `NLog.LogManager.GetLogger("Window")` moved into `ExternalLink`. `ExternalLink` now declares
    a `static readonly Logger Log = LogManager.GetLogger("Window")` field, the form that test
    accepts; the other three are resolved by whichever merge comes second.
- The no-raw test ran on a catalog never loaded, while in the window it is: a new test runs the
  tab without a raw on a loaded lensfun-only catalog.
- No golden scenario runs without an Adobe file, so "the golden files do not change" guarded
  nothing: R-113 and ADR 0073 now say the console's report carries none of the texts, and the
  window test checks that the saved report holds neither the paragraph nor the folders line.
- The owner's choice of the light link test over a launcher seam is now among ADR 0073's
  alternatives.
- The CHANGELOG said "when it compares"; a long line in `modes-structure.md` was reflowed.
- Out of scope, reported to the owner: a `lensfun/` folder that cannot be listed throws in
  `ProfileLocator.Locate`, so the Structure run fails with a raw in `flats/` (as on `main`).

## Merge (2026-10-30)

The owner asked for the merge after `claude/Fix-the-diagnostic-log-review-findings` and the UI
Automation branch had reached `main` (173567c). `main` was merged into this branch first, in its
worktree:

- `AboutWindow.xaml.cs`: kept `OpenLink => ExternalLink.Open(e)` and dropped `main`'s logger
  field, which only the moved catch used.
- `docs/requirements.md`: R-113 beside `main`'s R-114.
- `docs/validation.md`: both History entries, this task's above the log fixes'.
- Two calls in `StructureReportTests` follow `main`'s eight-parameter `ProfileSources`.
- `ExternalLink` takes its logger from `Loggers.Window`, `main`'s convention.

Gate green on the result (1151 + 347 tests), the build clean with warnings as errors. No figure
moved on either side, so `/validate` was not run again.
