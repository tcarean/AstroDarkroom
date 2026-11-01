# 0083. The About window fits its screen, as the launcher's and AstroPhotoAnalyzer's do

Date: 2026-11-01
Status: accepted 2026-11-01

## Context

"Despre" sized itself to its content and had no scroll bar: one `StackPanel`, with the Close
button last.

**Measured on 2026-11-01.** The content was laid out at the client width of a 600 DIP window,
584 DIP, after the dispatcher pass that resolves its bindings and styles, without showing the
window. It came to 880 DIP: a window of about 919 DIP with the title bar and the frame (39 DIP at
100 %, AstroPhotoAnalyzer's ADR 0070).

**Where it fit.** The launcher worked out these work areas (AstroDarkroom's ADR 0013):

| screen | work area | the window |
| --- | --- | --- |
| 1080 lines at 100 % | 1040 DIP | fit |
| 1080 lines at 125 % | about 825 DIP | ran past it |
| 1366x768 at 100 % | about 728 DIP | ran past it |
| 1080 lines at 150 % | about 690 DIP | ran past it |

Where it ran past the area, its Close button was off the screen; Esc still closed it.

**The new rows.** The three rows of ADR 0082 and the link under the lead bring the content to
1146 DIP in English and 1161 in Romanian. That is a window of about 1185 and 1200 DIP, past the
1040 DIP area too.

**The same problem elsewhere.** AstroPhotoAnalyzer met it with its NLog card and took the
launcher's design: its ADR 0070, after the launcher's ADR 0013 and R-016.

**The options**, put to the owner on 2026-11-01:

- the fit in this branch (recommended);
- a separate task, with the window running past the screen until then;
- shorter rows, which could not bring 1200 DIP under 1040.

The owner chose the fit here.

## Decision

FlatCheck takes AstroPhotoAnalyzer's design, including its four differences from the launcher's.

- **The cap.** The window's height is capped at the work area of the screen the main window is
  on (`MaxHeight`).
  - The constructor takes the area, `AboutWindow(Rect workArea)`, and the main window passes
    `ScreenArea.WorkAreaOf(this)`.
  - The primary screen's `SystemParameters.WorkArea` stands in when the window has no handle or
    Windows gives no answer.
- **The scrolling cards.** The cards are in a `ScrollViewer` named `Cards`, with the theme's dark
  scroll bar.
  - Its background is transparent, so the wheel scrolls it over its gaps too.
  - The arrow, Page Up, Page Down, Home and End keys scroll it from the window's
    `PreviewKeyDown`, but only while the cards can scroll. Where they fit, the arrows still move
    the focus between the links.
- **The Close button** is in a row of its own under the cards. Where the window fits, the margins
  add up to the layout before.
- **At `ContentRendered`:**
  - `ScreenArea.TopInside` keeps the window's top inside the area, as a guard behind WPF's own
    placement;
  - one Debug line under `Window` gives the window's size and place, the area, and whether its
    cards scroll (R-110).
- **`Interop/ScreenArea.cs`** is in the window project, where its tests are, and uses
  `DllImport` as `DarkTitleBar` does. It is a window's geometry, not a figure of the analysis.
- **Computed on 2026-11-01** from the content's measured height and the 39 DIP frame at 100 %,
  not with a shown window:

  | work area | English | Romanian |
  | --- | --- | --- |
  | 1400 DIP (1440 lines at 100 %) | 1185 DIP high, nothing scrolls | 1200, nothing scrolls |
  | 1040 DIP (1080 lines at 100 %) | 1040, the cards scroll by 145 | 1040, by 160 |
  | 825 DIP (125 %) | 825, by 360 | 825, by 375 |
  | 728 DIP (1366x768 at 100 %) | 728, by 457 | 728, by 472 |
  | 690 DIP (150 %) | 690, by 495 | 690, by 510 |

## Alternatives rejected

- **A separate task**, with the rows merged first: in the meantime the window would have lost its
  Close button on the most common screen.
- **Shorter rows**, with the licence and the copyright on one line, for example. This was not
  measured: three rows of about 90 DIP each could not come back under 1040 DIP that way, and the
  window already ran past 825 DIP.
- **The fit at `Loaded`**, as in the launcher. AstroPhotoAnalyzer measured its window, sized to
  its content as this one is, still at its first height there. This window takes that design
  whole, `ContentRendered` included.
- **The primary screen's area for the cap.** It is the wrong screen when the main window is on
  another one (the launcher's reason).
- **A scroll bar over the whole window, Close included.** Close would scroll out of view.

## Consequences

- **On a 1080-line screen at 100 %**, the window is 1040 DIP high and its cards scroll by about
  150 DIP. The first view holds the header, the licence and the first rows; the rest is a
  scroll away.
- **`AboutWindowFitTests`** holds the `R123_` tests:
  - the arithmetic;
  - the low area, with End, Home and Enter raised as key events through a stand-in presentation
    source;
  - the area where the window fits: a 1440-line screen's, since the window no longer fits a
    1080-line screen;
  - Windows' answer on the primary screen, through a hidden window's handle.

  It also holds R-110's Debug line, tested through `OnContentRendered`.
- **No test shows the window.** The heights above were computed, not measured on a shown window.
- **The other window tests.** `WindowSmokeTests` renders the content at its own height after a
  dispatcher pass, so the render is the whole window. `WindowLanguageTests` passes the primary
  screen's area.
