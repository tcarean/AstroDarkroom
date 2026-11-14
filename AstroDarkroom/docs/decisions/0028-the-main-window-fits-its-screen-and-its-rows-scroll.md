# 0028. The main window fits its screen, and its rows scroll

Date: 2026-11-14
Status: accepted 2026-11-14

## Context

The main window was as tall as its content, could not be resized and scrolled nothing. ADR 0013
made "Despre" fit its screen (R-016) and left the main window as it was; ADR 0017 then made it
seven lines taller. The code review of 2026-11-13 (CR-005, `to measure`) read that it no longer
fits the screen of a 1080p laptop at 125 % or 150 % scaling, where the bottom bar, with the
language selector and the About button, and at 150 % the last row too, would be off the screen.

Measured on 2026-11-14, the real `MainWindow` shown off the screen by a throw-away program, at
96 DPI, in both languages:

```
window 680 x 848, client 664 x 809                      (no warning line)
window 680 x 884, client 664 x 845                      (with the Visual C++ warning, R-018)
content laid out at 100 %: 809 DIP high; at 125 %: 814.6; at 150 %: 815          (no warning)
content laid out at 100 %: 845 DIP high; at 125 %: 852.6; at 150 %: 854.33       (warning)
```

The work area of a 1920 x 1080 screen is about 825 DIP high at 125 % and 690 at 150 %
(ADR 0013), and 1040 at 100 %, measured here. So the window, 848 DIP or more, does not fit
either scaled screen. The same run laid out the texts at the three scales: every description
takes two lines in its 416 DIP column at 100 %, 125 % and 150 %, the summary line one and the
subtitle two, which ADRs 0017 and 0024 had measured at 100 % only.

Two facts about WPF came out of the work, each measured the same day on .NET 8 with a window
sized to its content, off the screen:

- `Loaded` is raised before `SourceInitialized`, during the first layout, and a `Top` set in
  `Loaded` is replaced by the place WPF computes; a `MaxHeight` or a `Top` set in
  `SourceInitialized` holds, and the height changes at once.
- With the default `ScrollViewer` template the scroll bar takes 17 DIP from the content: the
  description column goes from 416 to 399 DIP, where the Romanian description of DarksCheck
  takes three lines at 125 %, the scale at which the bar shows.

## Decision

The main window fits the work area of the screen it opens on, as "Despre" does (R-020):

1. When it gets its handle (`SourceInitialized`), it asks `ScreenArea.WorkAreaOf` for its
   screen's work area, sets its `MaxHeight` to the area's height and passes its `Top` through
   `WindowFit.Top`, the core's function "Despre" uses.
2. Its rows sit in a scroll viewer in the one row that stretches; the top bar and the bottom
   bar keep rows of their own height, so they stay in view.
3. The scroll bar is drawn over the rows' right margin, by a template of the viewer's own, so
   the cards keep their width and every description its two lines when the bar shows.
4. The log's line about the window is written at the first rendering, with the work area of the
   window's screen and whether the rows scroll (R-012).

## Alternatives rejected

- **A window the user can resize.** It would still open taller than the screen, and the fixed
  width is what the two-line descriptions were measured for (ADR 0017).
- **Shorter rows or one-line descriptions on a small screen.** Two layouts to measure and keep;
  the owner set two lines (ADR 0017).
- **The whole content in one scroll viewer.** The bottom bar, with the language and About,
  would scroll out of view, which is the defect.
- **The fit on `Loaded`, as "Despre" has it.** Measured: for this window `Loaded` comes before
  the handle and WPF's placement, so the cap would come after the log's line and the move would
  be undone.
- **The default scroll viewer, with the bar beside the rows.** Measured: one description goes
  to three lines at 125 %.
- **Following the window to another screen.** A window dragged to a lower screen keeps its
  height; the launcher is opened, used for a click and left where it is. It can be added if
  someone meets it.

## Consequences

Measured after the change, the same way, in both languages, with and without the warning line,
the window given a work area of 825 and of 690 DIP:

```
area 1040: window 680 x 848, rows viewport 658 of 658, bar not shown, descriptions 416 wide, 30 high
area 825:  window 680 x 825, opened at 11.5 above the area, moved to its top; About button's bottom 776 of client 786;
           rows viewport 635 of 658, scroll bar 17 wide from x 647, cards end at 644; descriptions 416 wide, 30 high
area 690:  window 680 x 690, opened at 79 above the area, moved to its top; About button's bottom 641 of client 651;
           rows viewport 500 of 658; one wheel notch scrolls 48; the last row's button, focused, comes into view
log:       window shown 680x690 at (...); work area 1536x690 at (...); rows scroll: yes; primary work area ...
```

The areas were handed to the window's own `FitInto` by the measuring program: this PC has no
screen that small, so the window was not seen on a real screen at 125 % or 150 %, and the Tab
key was not pressed by hand. `R020_TheRowsScrollBetweenTheTwoBars` reads the window's XAML and
fails when the rows leave the scroll viewer or a bar joins it; the rest of the window has no
test (ADR 0001), so a change of the fit is measured again. The same measurement shows that the
guard "Despre" sets on `Loaded` (ADR 0013) is undone by WPF's placement; it is a new entry of
the review list, not changed here.
