# 0031. The About window fits its screen, as AstroPhotoAnalyzer's does

Date: 2026-11-01
Status: accepted 2026-11-01

## Context

"About" sized to its content and had no scroll bar: one `StackPanel`, with the Close button last.
ADR 0030 adds three rows. With them the content is 978 DIP high in either language, at the
window's client width of 584 DIP. With the title bar and the frame, 39 DIP at 100 % as
AstroPhotoAnalyzer measured the same window chrome on this PC (its ADR 0070), the window is
about 1,017 DIP high. Before the three rows it was about 750, from 710 DIP of content measured on
main's build by the second review of this branch.

The launcher worked out the work areas of a 1080-line screen: 1040 DIP at 100 %, about 825 at
125 % and about 690 at 150 % (AstroDarkroom's ADR 0013). A 1366x768 laptop at 100 % leaves about
728. So the window fit up to 125 % before the three rows and fits only at 100 % with them; it
never fit a 1366x768 laptop. Past the area, WPF keeps the title bar on the screen and the bottom
falls off, with the Close button; Esc still closes the window.

The branch's first review found it on 2026-11-01. The owner chose to fix it in this branch,
before the merge, as AstroPhotoAnalyzer did for the same regression from its NLog row.

## Decision

- **AstroPhotoAnalyzer's fit, taken as it is.** That is its ADR 0070, itself the launcher's
  design (AstroDarkroom's ADR 0013 and R-016):
  - The window's height is capped at the work area of the screen the main window is on
    (`MaxHeight`). The area comes from `ScreenArea.WorkAreaOf`, or is the primary screen's when
    Windows gives none.
  - The cards are in a `ScrollViewer` with the theme's dark scroll bar. It is transparent, so the
    wheel scrolls it anywhere over it.
  - The arrow, page, Home and End keys scroll it from the window's `PreviewKeyDown`, and only
    while the cards can scroll. Where they fit, the arrows still move the focus between the links
    and Close.
  - The Close button is in a row of its own, so it stays in view.
  - At `ContentRendered`, `ScreenArea.TopInside` keeps the window's top inside the area, and one
    Debug line gives its size and place, the area and whether its cards scroll (R-071, R-076).
    It runs there, not at `Loaded`, where a window sized to its content still has its first
    height.
  - Where the window fits, the margins add up to the same layout as before.
- **One difference: `LibraryImport`, as `DarkTitleBar` uses here.** AstroPhotoAnalyzer chose
  `DllImport` to keep `AllowUnsafeBlocks` off. This window project has it on already, for its
  title bar.
- **The arithmetic stays in the window project**, beside the query (`ScreenArea.cs`), and the
  window's tests test it, as in AstroPhotoAnalyzer. A window's place is not a figure of the
  analysis, which the rule "Computed only in the core" and ADR 0008 keep in the core.
- **Measured on 2026-11-01.** The content was laid out at the client height a capped window
  leaves it, the window's height less 39 DIP, at the work areas of the launcher's arithmetic. The
  figures are the same in English and in Romanian, and in every case the Close button ends 20 DIP
  above the bottom of the client area.

  | work area | window | the cards scroll by |
  | --- | --- | --- |
  | 1040 DIP (1080 lines at 100 %) | 1017 DIP | nothing, as before |
  | 825 DIP (125 %) | 825 | 207 DIP |
  | 728 DIP (1366x768 at 100 %) | 728 | 304 |
  | 690 DIP (150 %) | 690 | 342 |

## Alternatives rejected

- **The fit as a task of its own after the merge.** The merge would have shipped the regression.
- **A shorter window**, with fewer rows or smaller text. The rows are what R-075 asks "About" to
  list, and a 1366x768 laptop had no room before them either.
- **The primary screen's area for the cap.** It is the wrong screen when the main window is on
  another one (the launcher's reason).
- **A scroll bar over the whole window, Close included.** Close would scroll out of view, which
  is the problem.
- **The fit at `Loaded`, as in the launcher.** AstroPhotoAnalyzer measured the first height
  there: 436 DIP for a window of 862.
- **A test that shows the window.** None of this project's window tests shows one. The layout at
  a capped height, the keys, the arithmetic, the conversion of Windows' pixels by the DPI scale
  (`ScreenArea.ToDips`, at 125 % and 150 %), and the fit with its log line are tested apart. That
  the query asks Windows at all is seen only in its not falling back. On this one-screen PC at
  100 %, Windows' answer and WPF's are the same.

## Consequences

- On a low screen, "About" opens inside it with its Close button in view. On a 1080-line screen
  at 100 % it is as before, 1,017 DIP high.
- `AboutWindowFitTests` holds R-076's tests and R-071's Debug line.
- The three tests that build the window give it `SystemParameters.WorkArea`. `WindowSmokeTests`
  and `WindowLanguageTests` render it 1000 DIP high: the whole content, no scroll bar.
- The 39 DIP of title bar and frame are AstroPhotoAnalyzer's measurement of the same chrome, with
  a window shown on this PC; no window was shown here.
