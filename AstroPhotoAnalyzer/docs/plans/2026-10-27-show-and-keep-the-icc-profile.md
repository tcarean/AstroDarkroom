# Show, clear and remember the ICC profile in the window's top bar

Branch `claude/Show-and-keep-the-ICC-profile`, worktree
`.claude/worktrees/Show-and-keep-the-ICC-profile`. Approved 2026-10-27.

## Context

The window takes the paper's Dmax from an ICC profile through "From ICC profile..." (R-028,
R-132), but once chosen the profile is visible nowhere: the Dmax field shows a number, and the
name appears only in Appendix F after the next "Analyse". The owner asked (2026-10-27) that the
profile in use be shown plainly in the top bar beside the image's name, with a small **Clear**
button that stops using it; that choosing another profile, or clearing it, re-runs the analysis
by itself; and that the chosen profile be saved and still be selected when the window is next
opened. Nothing is computed by this change: the window keeps formatting what the core already
reads (`IccPaperBlack.Read`), so no `/validate` run is needed.

## Design choices (the plan's approval settled them; ADR 0051 records them)

1. **The profile is a state of `ParameterFields`**, `Profile` (an `IccProfileRow`, public,
   notifying), beside the Dmax text it wrote; the bar binds `Parameters.Profile`. The name was
   dropped lazily (`TryBuild` named it only while the field still showed its Dmax); with a
   visible profile the drop is **eager**: typing another Dmax, or choosing a medium, sets
   `Profile` to null at once, so the bar never names a profile the next run would not use.
   Retyping the same Dmax does not bring it back (choose it again).
2. **Re-run rule**: "Use" in the profile window and **Clear** re-run the analysis when an image
   is open; a choice that changes nothing (the same profile, its Dmax still in the field) runs
   nothing. Both buttons are disabled while a run is on (as "Open image..." is). Typing a Dmax
   over the profile's does not re-run: it applies at "Analyse", as every field does.
3. **Remembered in `%LOCALAPPDATA%\AstroPhotoAnalyzer\settings.json`**, key `iccProfile` = the
   profile's full path, written whenever the bar's profile changes (chosen, dropped, cleared;
   cleared = the key removed), through a temporary file and one move, as AstroDarkroom's
   `LanguageSetting` does. A new class `WindowSettings` in the Wpf project (window state,
   nothing computed: not the core's); reading or writing that fails is ignored. The
   parameterless `MainViewModel` and `MainWindow` persist nothing, so no test touches the
   user's file; `App` passes the real path.
4. **At startup** the saved profile is read again from its file: readable, it is applied as if
   chosen; missing or unreadable, nothing is applied, the bar shows the file's name with the
   core's reason in red, and Clear removes it. The medium is not remembered.
5. **What the bar shows**: a faint label `ICC PROFILE` / `PROFIL ICC`; under it the profile's
   name and the core's sentence, or `(none: the medium's Dmax)` muted; the **Clear** / **Scoate**
   button to its right, small, only while a profile is set.

## Steps

1. Worktree and branch; this plan.
2. R-155 (the bar, Clear, the eager drop, the re-run rule) and R-156 (the remembered profile),
   `draft` until the tests pass; R-110 and R-132 reworded.
3. The failing tests in `AstroPhotoAnalyzer.Wpf.Tests`: `R155_*` in `IccPickerTests` and the
   new `ProfileBarTests.cs`, `R156_*` there too, a render in `WindowSmokeTests`, the bar in the
   English sweep of `WindowEnglishTests`.
4. The code, the window only: `ParameterFields` (`Profile`, `HasProfile`, `ProfileText`,
   `ProfileIsProblem`, `UseProfile` returning whether anything changed, `ClearProfile`,
   `RestoreProfile`, the eager drop in the `Dmax` and `Medium` setters), the new
   `WindowSettings`, `MainViewModel(WindowSettings)` with `ChooseProfile` and `ClearProfile`
   that re-run, `MainWindow(WindowSettings)`, the bar in `MainWindow.xaml`, `App` passing the
   real path.
5. ADR 0051, `docs/design/window.md`, `docs/design/console.md` "Parameters", the README's
   "The window", the CHANGELOG, the rows set to `implemented`.
6. The gate after a `--no-incremental -warnaserror` build; the renders looked at; `/wrap-up`.

## Deviations

- The bar's profile row is a two-column grid (text `Auto`, at most 700 px, and the button),
  not the horizontal panel first written: in a panel of infinite width the long reason of a
  missing profile pushed "Scoate" out of the column, seen in the test render.
- `UseProfile` sets `Profile` before the Dmax text, not after: the `Dmax` setter compares the
  text with the profile in use, and the new profile's own text must not drop it.
- An unreadable profile is dropped by a medium or "Scoate" only, not by a typed Dmax: it wrote
  no text to compare with.
