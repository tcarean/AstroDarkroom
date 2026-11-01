# 0070. The About window fits its screen, as the launcher's does

Date: 2026-10-31
Status: accepted 2026-10-31

## Context

"Despre" sized to its content and had no scroll bar: one `StackPanel`, the Close button last.
With NLog's card (ADR 0069) the window is 862 DIP high in Romanian and 847 in English, its title
bar and frame included (39 DIP at 100 %). That was measured on 2026-10-31 with the window shown,
its client area 584 DIP wide. ADR 0069 first gave 819 and 804 DIP "of content", laid out at the
window's outer 600 DIP and without the frame. When the window is taller than the work area, WPF
keeps its title bar on the screen and its bottom falls off, the Close button with it; Esc still
closes it.

The launcher measured a 1080-line screen's work area at 1040 DIP at 100 % and worked out about
825 at 125 % and about 690 at 150 % (AstroDarkroom ADR 0013). A 1366x768 laptop at 100 % leaves
about 728. By that arithmetic the window fit up to 125 % before the NLog card, and with it fits
only at 100 %: a regression of the log's branch at a common laptop setting. On 2026-10-31 the
owner chose to fix it on that branch, before its merge, with the launcher's design. The plan,
`docs/plans/2026-10-31-let-the-about-window-fit-its-screen.md`, was approved as written.

(Restated 2026-11-01: "a window sized to its content still has its first height" at `Loaded`,
in the Decision below, holds for this window, not for every one. The launcher's About, whose
texts are set in code before it is shown, already has its final height at `Loaded`. It was
measured off the screen, not activated, at work areas of 1040, 825 and 690 DIP: it was 1037, 825
and 690 DIP high at `Loaded` as at `ContentRendered`, and its own log lines gave those heights
and the right scroll state. The fit here stays at `ContentRendered`, where this window's height
is final.)

## Decision

- **The launcher's design** (AstroDarkroom ADR 0013, its R-016):
  - The window's height is capped at the work area of the screen the main window is on
    (`MaxHeight`).
  - Its cards are in a `ScrollViewer`, with the theme's dark scroll bar. The wheel scrolls it
    anywhere over it, since its background is transparent. The arrow, page, Home and End keys
    scroll it from the window's `PreviewKeyDown`, since the focus stays on the Close button or
    a link; they do so only while the cards can scroll (below).
  - The Close button is in a row of its own under the cards, so it stays in view.
  - Once the window is shown, `ScreenArea.TopInside`, the launcher's `WindowFit.Top`, keeps its
    top inside the area, as a guard behind WPF's own placement.
  - Where the window fits, the margins add up to the same layout as before.
- **Four differences from the launcher**, each this project's own:
  - **The arithmetic and the Windows query live in the window project**,
    `Interop/ScreenArea.cs`, and the window's tests test them. The launcher put `WindowFit.Top`
    in its core because its window has no tests (its ADR 0001). This window has tests, and this
    core holds no window concept: the rule "Computed only in the core" is about the analysis's
    figures (ADR 0007).
  - **`DllImport`, as `DarkTitleBar` uses**, not the launcher's `LibraryImport`. That is
    source-generated as unsafe code and would need `AllowUnsafeBlocks` in the project.
  - **The fit runs at `ContentRendered`, not at `Loaded`.** At `Loaded`, a window sized to its
    content still has its first height: 436 DIP, measured here for a window of 862. The guard and
    the log line would read that wrong one; the log line said "cards scroll: no" on a 690 DIP
    area where they scroll by 172. The launcher fits at `Loaded`.
  - **The keys are taken only while the cards can scroll.** Where they fit, the window takes no
    key, so the arrows move the focus between its five links and its Close button, as before: a
    window's directional navigation cycles through them. The launcher's cards hold nothing that
    takes the focus, and it takes the keys always (the review of 2026-10-31).
- **The constructor takes the area**, `AboutWindow(Rect workArea)`, as the launcher's does.
  - The main window passes its own screen's area, `ScreenArea.WorkAreaOf(this)`.
  - The primary screen's `SystemParameters.WorkArea` stands in when the window has no handle or
    Windows gives no answer.
- **One Debug line** under `Window` once the window is shown: its size and place, the area, and
  whether its cards scroll (R-171).
- **Measured on 2026-10-31.** This machine at 100 %, the window shown off the screen and not
  activated, at the work areas of the launcher's arithmetic:

  | work area | Romanian | English |
  | --- | --- | --- |
  | 1040 DIP (1080 lines at 100 %) | 862 DIP high, nothing scrolls | 847, nothing scrolls |
  | 825 DIP (125 %) | 825, the cards scroll by 37 | 825, by 22 |
  | 728 DIP (1366x768 at 100 %) | 728, by 134 | 728, by 119 |
  | 690 DIP (150 %) | 690, by 172 | 690, by 157 |

  - The Close button ends 20 DIP above the client area's bottom in every case.
  - On the 1040 DIP area the window has today's size and places.
  - Its render differs from today's in about 4 % of the pixels, by at most 3 of 255 in a
    channel, on text edges only: the anti-aliasing of text drawn inside a scroll viewer.

## Alternatives rejected

- **The primary screen's area**, `SystemParameters.WorkArea`, for the cap: it is the wrong
  screen when the main window is on another one (the launcher's reason).
- **A shorter window**, with smaller text or a card dropped. The card is how the tool names
  NLog and its licence (ADR 0069), and the 150 % screen had no room before the card either.
- **A scroll bar over the whole window**, the Close button included: Close would scroll out of
  view, which is the problem.
- **The fit at `Loaded`**, as in the launcher: measured wrong, above.
- **A test that shows the window.** No window test of this project shows one
  (`WindowSmokeTests`). The content is laid out at the height a capped window leaves it, and the
  arithmetic and Windows' answer are tested apart. The figures above were measured once, with a
  shown window.

## Consequences

- The known limitation that ADR 0069 recorded is gone: on a low screen the window opens inside
  it, its Close button in view.
- `AboutWindowFitTests` hold the `R158_...` tests and R-171's Debug line:
  - the arithmetic;
  - the low area, with End, Home and Enter raised as key events through a stand-in presentation
    source;
  - the area where the window fits: no scroll bar, the old gaps around Close, no key taken;
  - Windows' answer on the primary screen, through a hidden window's handle, with no fallback
    line;
  - the fit and its line, through `Window.OnContentRendered`.
- The three existing tests that build the window pass `SystemParameters.WorkArea` to its new
  constructor. `WindowSmokeTests` renders it at 860 DIP, the whole window, since a 720 DIP render
  now shows the scroll bar.
- The launcher's own fit runs at `Loaded`. Whether its window reads its first height there too is
  not measured; that is a task for AstroDarkroom.
  (Restated 2026-11-01: measured, it reads its final height there; see the note before
  "Decision". Nothing changes in the launcher.)
