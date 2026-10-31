# Let the About window fit its screen

Plan for AstroPhotoAnalyzer, written on 2026-10-31, to be saved as
`docs/plans/2026-10-31-let-the-about-window-fit-its-screen.md` in the first commit of the work.

## Context

"Despre" sizes to its content and has no scroll bar (`AstroPhotoAnalyzer.Wpf/Views/AboutWindow.xaml`:
`SizeToContent="Height"`, one `StackPanel`, the **Close** button last). The diagnostic log's branch
(`claude/Log-AstroPhotoAnalyzer-with-NLog`, ADR 0069) added NLog's card. The content went from
731 DIP to 804 in English and 819 in Romanian, measured on that branch, plus the title bar and the
frame. When the window is taller than the work area, WPF keeps the title bar on the screen and
the bottom falls off, Close with it. Esc still closes it. The branch recorded this as a known
limitation (`docs/design/known-limitations.md`).

The launcher measured a 1080-line screen's work area at 1040 DIP at 100 % and worked out about
825 at 125 % and about 690 at 150 % (AstroDarkroom ADR 0013). By that arithmetic, today's window
on main fits up to 125 %. With the NLog card it no longer fits at 125 %, a common laptop setting,
so the branch would merge a regression. At 150 %, and on a 1366x768 laptop at 100 % (about
728 DIP), it never fit.

The owner chose on 2026-10-31 to do this task in this session ("Do it here" on the task card),
before the log's branch is merged. The launcher solved the same problem on 2026-10-30
(AstroDarkroom ADR 0013, R-016, `ScreenArea.cs`, `WindowFit.cs`, its `AboutWindow`). This plan
takes its design, adapted to this project.

## Decisions

Each comes with a recommendation; approving the plan as written takes every recommendation.

| # | Decision | Recommendation and why |
| --- | --- | --- |
| D1 | Where the work goes | **On `claude/Log-AstroPhotoAnalyzer-with-NLog`, in commits of its own.** The card that pushes the window off a 125 % screen is that branch's, so the limitation never reaches main. A separate branch cut from it would also have to take its pending commits (the second review's fixes, the merge of main) through more merges. Alternative: `claude/Let-the-About-window-scroll`, cut from that branch's tip in its own worktree, merged after it. |
| D2 | Where the fit's arithmetic lives | **In the window project, `AstroPhotoAnalyzer.Wpf/Interop/ScreenArea.cs`**, beside the Windows query, tested by the window's tests. The launcher put `WindowFit.Top` in its core because its window has no tests (its ADR 0001). This window has tests, and this core holds no window concept: "Computed only in the core" is about the analysis's figures (ADR 0007). Alternative: the core, as the launcher has it. |
| D3 | Which screen's work area | **The screen the main window is on**, which Windows gives (`MonitorFromWindow`, `GetMonitorInfoW`), as in the launcher's `ScreenArea`, but declared with `DllImport` as `DarkTitleBar` is. The launcher's `LibraryImport` needs `AllowUnsafeBlocks`; this project then needs none. The primary screen's `SystemParameters.WorkArea` stands in when the window has no handle or Windows gives no answer. The primary screen's area alone was rejected by the launcher: it is the wrong screen when the main window is on another one. |
| D4 | How the window gets the area | **A constructor `AboutWindow(Rect workArea)`**, as in the launcher; `MainWindow` passes its own screen's area. The three tests that build the window today (`R119_TheAboutWindowLoadsAndRenders`, `R153_EveryWindowTextIsEnglishWithoutLang`, `R171_ALinkThatCannotBeOpenedIsADebugLineAndTheWindowStays`) pass `SystemParameters.WorkArea`. That is a call-site change forced by the new signature; nothing they assert changes. |
| D5 | Separate from this work, found before the merge: the suite's notices | AstroDarkroom's `SuiteTests.R017_...` binds the suite's `THIRD-PARTY-NOTICES.md` to the windows' lock files. Main plus this branch's `AstroPhotoAnalyzer/` failed it on a scratch copy, on the NLog section alone: its `Programs:` line lacks AstroPhotoAnalyzer. With the name added, all 105 suite tests passed. The merge hook gates only the projects a merge brings in, so merging this branch into main would leave AstroDarkroom red there unnoticed. **Recommendation: at the end of this work, run `git merge --no-ff --no-commit main` in this worktree.** That merge commit also adds `AstroPhotoAnalyzer` to the NLog section's `Programs:` line, to the suite README's "Shipped by" cell for NLog, and to R-017's example in AstroDarkroom's requirements. The hook then verifies AstroDarkroom and FlatCheck on that merge, and refuses it without the fix. Approving the plan approves that one command. Alternative: merge this branch as it is and fix the suite in a branch of its own right after, with AstroDarkroom red on main in between. |

## The design

1. **`AstroPhotoAnalyzer.Wpf/Interop/ScreenArea.cs`** (new), `internal static class ScreenArea`,
   in `DarkTitleBar`'s style: block namespace, the GPL header, an XML comment on every member,
   and `static readonly Logger Log = LogManager.GetLogger(Loggers.Window)`.
   - `WorkAreaOf(Window window)`: the launcher's method. It asks for the screen nearest the
     window's handle and divides its work rectangle by `VisualTreeHelper.GetDpi(window)`. It falls
     back to `SystemParameters.WorkArea` with a Debug line, "the work area of the window's screen
     could not be read; the primary screen's is used", logged with the exception for a missing
     DLL or entry point.
   - `TopInside(double top, double height, double areaTop, double areaHeight)`: the launcher's
     `WindowFit.Top`, the same formula: `height >= areaHeight ? areaTop : Math.Clamp(top, areaTop,
     areaTop + areaHeight - height)`.
   - The `RECT` and `MONITORINFO` structs, private.
2. **`AboutWindow.xaml`**, the launcher's layout around today's bindings.
   - A `Grid` of two rows.
   - Row 0 is a `ScrollViewer x:Name="Cards"`, with `VerticalScrollBarVisibility="Auto"`,
     `Focusable="False"` and `Background="Transparent"`, so the wheel works over the margins
     too. It holds today's `StackPanel` with the margin `24,20,24,0`.
   - Row 1 is the Close button, with the margin `24,16,24,20`.
   - The margins add up as today, so a window that fits lays out as it does now. The file's
     comment says how it fits.
   - The dark scroll bar is the theme's implicit `ScrollBar` style (`Theme/Dark.xaml`), already
     there.
3. **`AboutWindow.xaml.cs`**:
   - The constructor `AboutWindow(Rect workArea)` sets `MaxHeight = workArea.Height` and hooks
     `Loaded += (_, _) => FitInto(workArea)` and `PreviewKeyDown += ScrollCards`.
   - `internal void FitInto(Rect workArea)` sets `Top = ScreenArea.TopInside(...)`. It writes one
     Debug line under `Window`: `About shown <w>x<h> at (<left>,<top>); work area <W>x<H> at (<L>,<T>);
     cards scroll: yes|no`, its numbers through `LogText.Num`.
   - `internal bool Scroll(Key key)` maps Up, Down, Page Up, Page Down, Home and End onto the
     `ScrollViewer` and returns whether the key scrolled. `ScrollCards` calls it and marks the
     event handled, which lets the test call it without raising key events. Enter and Esc still
     reach the button.
4. **`MainWindow.xaml.cs`**: `new AboutWindow(ScreenArea.WorkAreaOf(this)) { Owner = this }.ShowDialog();`.

**R-158** goes in "The window", after R-157. It is the next free number of the 150s, which
R-155 to R-157 already took for the window. The soft-proofing plan once drafted R-158 to R-161,
but they were never assigned, as its own header says. The text adapts the launcher's R-016:
- "Despre" is never taller than the work area of the main window's screen, and opens inside it,
  its title bar and Close on the screen.
- When its content is taller, the cards scroll with the wheel and with Up, Down, Page Up, Page
  Down, Home and End. Close stays at the bottom, and Enter and Esc close the window as before.
- Where it fits, nothing changes.
- The example uses the figures of step 3.

## Steps

Every commit runs `scripts/verify.ps1` green. Messages are one English line of at most 100
characters. All commands run in this worktree.

0. **The second review of `340ccd47`.** It returned five findings, and all were checked in the
   code. No user-visible text changes. The fixes go in one commit, and there is no third round
   (the owner's limit of two):
   1. **A profile's name can still break a log line.**
      - The fix: the recommendation's text and because (`Runs/AnalysisRun.cs:275`) go through
        `LogText.Str`, and so do the four `Problem` texts the curve and proof runs log
        (`Runs/CurveRun.cs:163`, `Runs/ProofRun.cs:95-104`), which can quote a parser's message.
      - The cause: the "profile is not sRGB" recommendation quotes the profile's name, and
        `IccProfileInfo.Clean` trims only its ends.
      - `LogText.IsControl` also catches the C1 controls (`char.IsControl`, NEL among them) and
        U+2028/U+2029. A v4 profile's name is UTF-16 and can hold them.
      - The tests: a TIFF whose embedded profile is named `Wide\nGamut` gives a recommendation
        line with `Wide?Gamut` and no line feed, and `Printable` turns C0, DEL, C1, U+2028 and
        U+2029 into `?`.
   2. **R-171 misfiles the removal of a temporary file after a failed save.**
      - It sits among the probes "whose outcome a line shows", but `AtomicFile` swallows a
        refused delete and no line shows it.
      - A line there would put a logger in `Input/AtomicFile.cs`, against ADR 0069's D4 and its
        invariant test, so the requirement is reworded instead. The clean-up is best effort and
        not logged; the save's failure is.
      - The README's "What is in it" sentence is made to say the same.
   3. **The rule on reworded exceptions is stated for every case**, but `AtomicFile.Write` keeps
      the original as the inner exception. Its caller logs both stacks, and `Describe` skips an
      inner message the outer one holds. `docs/design/logging.md`, ADR 0069 and the CHANGELOG
      limit the sentence to the three sites that drop the original, and name `AtomicFile` as the
      exception.
   4. **The levels of `AnalysisRun`'s row in `docs/design/logging.md`** become Info, Debug, Warn:
      the unreadable `--wcs` line is a Warn.
   5. **`CurveRun.cs:280` logs a guess as a fact.** The line will say "probably the input by
      another spelling". The heuristic (same length, open refused) can also match a read-only
      earlier save. The test's expected message changes with it; that is an intended change,
      reported at wrap-up.
1. **The plan and the failing test.** Save this plan.
   - Write `R158_OnALowWorkAreaTheCardsScrollAndCloseStaysInView` against today's window and
     run it. It lays the content out at 600x560 and finds Close below 560: it must fail. Keep the
     failure's output for the wrap-up (the kit's rule "Test before fix").
   - Then the code of the design, the other tests, and R-158 as `implemented`, in one commit.
     `RepoInvariantTests` refuses a tested row that is not `implemented`.

   The tests go in `AstroPhotoAnalyzer.Wpf.Tests/AboutWindowFitTests.cs`, collection `fereastra`:
   - `R158_TheWindowsTopKeepsItInsideTheWorkArea`: a theory over the launcher's seven cases,
     on `ScreenArea.TopInside`.
   - `R158_OnALowWorkAreaTheCardsScrollAndCloseStaysInView`, with a work area of 1280x680:
     - `MaxHeight` is 680.
     - In a second `_sta.Invoke`, the content laid out at 600x560 has
       `Cards.ScrollableHeight > 0`, and the Close button lies inside 0..560.
     - `Scroll(Key.End)` reaches the end and `Scroll(Key.Home)` the top.
     - With `APA_DUMP_SCREENS`, the render is saved as `despre-ecran-mic`.
   - `R158_WhereTheWindowFitsNothingScrolls`: a work area of 1920x1040, and the content laid out
     at its own desired height has `ScrollableHeight == 0`.
   - `R158_TheWorkAreaIsTheOneOfTheWindowsScreen`:
     - A hidden window given a handle with `WindowInteropHelper.EnsureHandle()`, placed inside the
       primary work area, gets `SystemParameters.WorkArea` from `WorkAreaOf`.
     - A window without a handle gets the same, through the fallback.
     - The window is closed afterwards.
   - `R171_TheAboutWindowLogsItsSizeTheWorkAreaAndWhetherItsCardsScroll`: `FitInto` under a
     `LogCapture` writes one Debug line under `Window` that holds
     `work area 1280x680 at (0,0); cards scroll: yes`.
2. **The documents**, in one commit:
   - **ADR 0070** "The About window fits its screen, as the launcher's does": D1 to D4 and the
     figures of step 3. The launcher's ADR 0013 is cited, not repeated.
   - `docs/design/window.md`: a paragraph on the fit.
   - `docs/design/code-map.md`: `Interop/ScreenArea.cs`.
   - `docs/design/logging.md`: the About line and `ScreenArea`'s fallback, in the row of the
     window's quiet lines.
   - `docs/design/known-limitations.md`: the About bullet removed.
   - ADR 0069's sentence "it has no scroll bar (`known-limitations.md`)" points to ADR 0070
     instead. ADR 0069 is not on main yet, so it may still change.
   - `CHANGELOG.md`, under Fixed: on a screen lower than "About", its cards scroll and Close
     stays in view.
   - The README is unchanged: "**About**, or F1, shows the version, the licence and the
     components" still holds.
3. **Measure**, before step 1's commit, with a scratch test that is not committed.
   - Show the window off the screen, not activated, with no input, at work areas of 1040, 825
     and 690 DIP, in both languages.
   - Record the window's height, the cards' scroll range and Close's place.
   - Render the content at its natural height before and after the change and compare the two
     pixel for pixel, which checks that a window that fits is unchanged.
   - The figures go into R-158's example, in step 1's commit, and into ADR 0070, in step 2's.
4. **A subagent review of the About commits alone**, briefed with the intent and the
   conventions. Apply its verified findings in one commit.
5. **D5**: the merge of main into this branch with the suite's three lines, and the hook's
   output read in full. Then propose the merge into main: the owner runs it or asks me to.

## Verification

- `scripts/verify.ps1` green at every commit. Before the last one, run
  `dotnet build AstroPhotoAnalyzer.sln -c Release --no-incremental -warnaserror --no-restore`.
- The failing run of step 1, pasted at wrap-up, then the new tests green.
- `git diff --stat main...HEAD -- AstroPhotoAnalyzer.Tests/Golden/` empty: the console is untouched.
- `/validate` is not due: nothing in `Input/`, `Analysis/`, `Report/`, `Parameters.cs` or
  `Thresholds.cs` changes (the rule "Formula changes re-validate").
- `scripts/check-design.ps1 -Since main` green.
- After D5, AstroDarkroom's and FlatCheck's gates green in the hook's output.

## Traps

- **Bindings.** A never-shown window's bindings resolve in a later dispatcher pass. Lay the
  content out in a second `_sta.Invoke`, or after `Dispatcher.Yield(ContextIdle)` as
  `TextsOfAsync` does, or it measures short and the low-area test passes for the wrong reason.
- **Language.** The test assemblies run in Romanian by default, where the content is 819 DIP.
- **The scroll viewer.** It keeps a transparent background, so the wheel works over the margins.
  It stays `Focusable="False"`, so Close keeps the focus and Enter and Esc behave as before. Tab
  can put the focus on a card's link; the arrow keys still scroll the cards, as in the launcher.
- **The log line.** It goes through `LogText`, never `Tr.Pick`. The logger is declared in the
  form the invariant test scans for.
- **Hidden windows.** `EnsureHandle` creates a real window that is not shown; close it at the end
  of the test.
- **ADR number.** It is 0070, after the branch's 0069; check main's latest number before the
  merge.
- **The Stop hook.** It refuses to end a turn after a code edit until verify is green.

## What deviated

- **The fit runs at `ContentRendered`**, not at `Loaded` as planned and as the launcher has it.
  Measured: at `Loaded` the window still had its first height, 436 DIP for a window of 862. The
  guard and the log line read that one; the line said "cards scroll: no" on a 690 DIP area where
  they scroll by 172.
- **The window's heights.** It is 847 and 862 DIP with its title bar and frame (39 DIP here), and
  808 and 823 DIP of client area at its 584 DIP inner width. The 804 and 819 "of content" of
  ADR 0069 were laid out at 600 DIP without the frame, and that ADR is corrected.
- **`WindowSmokeTests` renders "Despre" at 860 DIP** instead of 720: at 720 the render now shows
  the scroll bar.
- **Not pixel for pixel identical where it fits.** The render differs from today's by at most 3
  of 255 in a channel, in about 4 % of its pixels, on text edges only: the anti-aliasing of text
  drawn inside a scroll viewer. The sizes and places are the same.
- **Step 1's test failed on today's window**, as planned: "the Close button lies at 724..755 DIP
  of 560".
- **The low-area test's renders** are 600 DIP wide, as `WindowSmokeTests` renders; the window's
  client area is 584.
