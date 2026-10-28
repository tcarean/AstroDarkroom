# 0073. Without Adobe's lens profiles the Structure card says so in red, and how to get them

Date: 2026-10-28
Status: accepted 2026-10-28

## Context

FlatCheck ships lensfun but reads Adobe's `.lcp` files where Adobe's software installed them,
never redistributing them (ADR 0028, ADR 0066). On a computer without Adobe software the
Structure tab compares the master with lensfun alone, and nothing on the card says so. On
2026-10-28 the owner asked for a red message on that card, followed by what to install.

Measured on 2026-10-28 with the console built on `main` and `--profiles` pointing at a folder that
holds only `lensfun/`, which is what a computer without Adobe looks like:

- Tokina 11-20 sky master with `flats/DSC_0001.NEF`: with Adobe, 4 rows, the nearest
  `Adobe Tokina AT-X 11-20mm F2.8 PRO DX` at -0.39 stops = 31.5 % (REDO); with lensfun alone, 2
  rows, the nearest at -0.58 stops = 49.3 % (REDO), and a note that the lens ID 139 "matches no
  profile: a manual lens or one unknown to the databases". Nothing says Adobe is missing.
- Nikkor 35 DX f/2.8 sky master with `flats/DSC_0031.NEF`, lensfun alone: 6 rows found by the
  focal range, lenses of other mounts among them, and the verdict is a Nikon Z lens's. That is a
  separate finding (`LensIdentification.CandidatesByRange`), left to a task of its own.
- The author's computer holds 3531 Adobe files in `C:\ProgramData\Adobe\CameraRaw\LensProfiles\1.0`
  and 3633 in each Lightroom folder, so the message never shows there.
- The window's `ProfileCatalog` locates the profiles once per window; a search that finds no
  Adobe file is not a failure and is not repeated. The Sintetic tab's prefill loads it at every
  opening of a folder with a master; the Structure tab waits for it only when it compares, that
  is with a raw in `flats/` (found by the code review, 2026-10-28; the plan had said nothing was
  searched without a raw).
- Adobe's DNG Converter page, checked in a browser:
  `https://helpx.adobe.com/camera-raw/desktop/dng-and-file-formats/adobe-dng-converter.html`,
  where the older helpx address redirects, with downloads for Windows x64 and ARM64.

Four choices went to the owner, each with a recommendation: when the message shows, whether the
console says it too, a link or the name alone, and the Nikkor 35 finding. The owner took every
recommendation the same day. The rest were stated in the plan the owner approved
(`docs/plans/2026-10-28-say-when-adobe-profiles-are-missing.md`).

## Decision

- **When**: only when the search found no Adobe `.lcp` file in any of its folders
  (`ProfileSources.AdobeFiles == 0`), whatever the card then holds: lensfun rows, a gap, no
  profile at all. `StructureReport.AdobeMissing` decides it and returns "" otherwise.
- **Where**: the Structure tab only. The texts are composed in `StructureReport`
  (`AdobeMissing`, `AdobeInstall`, `AdobeSearched`, `AdobeDngConverterUrl`); the console's
  report, and so the saved `Test result.txt` and the golden files, do not change.
- **What**: at the top of the card, a red paragraph (`#F0857E`, the window's colour for a
  message) saying the profiles were not found and, when lensfun's lenses were read, what that
  leaves; then, in the card's muted text, to install the free Adobe DNG Converter, or Lightroom
  Classic, Lightroom or Photoshop's Camera Raw, and to restart FlatCheck; the link to Adobe's
  DNG Converter page, opened in the browser; the folders searched, in faint text.
- **With no lensfun lens read** (`ProfileSources.LensfunLenses == 0`: the folder missing, or
  none of its files readable) the paragraph is its first sentence alone: the card's gap then
  says no profile is on the machine, which the second sentence would contradict. The code
  reviews found that contradiction, first with the folder missing, then with its files
  unreadable; the owner confirmed the one-sentence wording and keying it on the lenses read,
  with the test fixture that change needed (2026-10-28).
- **The link** opens through one `Hyperlink.RequestNavigate` handler on the main window, which
  shares its code with the About window's: `Presentation/ExternalLink.Open`, moved out of
  `AboutWindow.xaml.cs`. It is now the one place besides `SirilScriptRun` that starts a
  process, so `RepoInvariantTests.OnlySirilScriptRunAndTheAboutLink_StartAProcess` (ADR 0055)
  lists it in place of `AboutWindow.xaml.cs`. The gate found that collision after the plan was
  approved; the owner chose this answer over keeping `Process.Start` in the About window for
  both links, or a link that cannot be clicked (2026-10-28). The test keeps its name, which ADR
  0055 cites.
- **Without a raw** in `flats/` the tab's run does not search the profiles, so it says nothing
  about Adobe. The owner kept this answer after the review found that the window loads the
  catalog anyway (2026-10-28).
- **After installing**, FlatCheck is restarted; `ProfileCatalog` is unchanged.

## Alternatives rejected

- **Also when Adobe is installed but has no file for this lens.** The install instructions
  would be false there; that case would need a text of its own, and it was not asked for.
- **A line in the console's report too.** Not asked for, and it would change the saved report
  and every golden whose fixture holds no Adobe file; the texts sit in the core, so it stays one
  call away.
- **The product's name without a URL.** Nothing to go stale, but the user would have to search
  for it; Adobe kept a redirect from the older address, and the URL is one constant.
- **Showing it without a raw too, to warn earlier.** The card's problem is then the missing raw,
  and in the Siril workflow the raws are in `flats/` (ADR 0031). It would cost no search, since
  the Sintetic tab's prefill loads the catalog anyway, but the tab would wait for that load
  before showing its result (a second or two warm; the first cold start of `--lenses` took
  11 s, ADR 0066).
- **Searching again on every folder opened while Adobe is missing, so that no restart is
  needed.** A change to `ProfileCatalog` for a one-time event.
- **Fixing the range-only identification in this task.** It is a design of its own, with its
  own measurements and choices; the owner chose a separate task.

## Consequences

- A user without Adobe software learns why the comparison holds lensfun alone and how to get
  Adobe's profiles, and where FlatCheck looked for them.
- The URL is a fact about Adobe's site that can go stale; it lives in one constant, whose form
  (https, `helpx.adobe.com`) a test checks.
- Guarded by the R-113 tests in `FlatCheck.Tests` (`StructureReportTests`) and
  `FlatCheck.Wpf.Tests`: `StructureTabTests`, and in `WindowSmokeTests` the render (the block
  visible, red, above the first row or above the gap, collapsed with Adobe's files), the link's
  route out of the tab, and a text check that the main window declares its handler (raising the
  event there would open the browser).
