# 0007. The window is a thin adapter over the core and computes no line of its own; the theme, the title bar and the MVVM pieces are copies of the siblings'; the window's tests are a second project

Date: 2026-10-26
Status: accepted 2026-10-26

## Context

The owner asked on 2026-10-26 for the console's functionality in a WPF window: dark, with an
About window, built on the kit, the interface in English and Romanian chosen by `--lang` as
FlatCheck does. Task 1 of the plan `docs/plans/2026-10-26-adopt-the-kit-and-build-the-window.md`
put the whole run into the core as `WcsCopy.Run(WcsCopyRequest)`, which returns a
`WcsCopyOutcome` of lines already worded (ADR 0001); the console prints them. The siblings
(FlatCheck, LightsCheck, DarksCheck, AstroDarkroom) each have a dark WPF window over their own
core, and DarksCheck's ADR 0009 states the rule their windows follow: the window shows, it does
not compute. The plan's questions Q8 (the icon) and Q9 (the command line) were answered with
the recommended options.

## Decision

- **The window computes nothing and composes no sentence about the copy.** `MainViewModel` may
  do three things: build a `WcsCopyRequest` from the boxes and the two check boxes, read two
  facts from each header before any run (`NAXIS1 x NAXIS2`, whether `CTYPE1` is present), and
  show the `WcsCopyOutcome` as the core gives it: `Error` under the heading `Refused`,
  `Warnings` in the warning colour, `Cards` trimmed in a monospace list, `Notes` under them.
  It never touches a card, never reads a value out of one and never words a refusal or a
  warning of its own. The one consequence accepted: the core's refusal of a dimension mismatch
  names the console's flag (`Use --force if you know what you are doing.`) while the window
  offers a check box for the same thing; the sentence is the core's, pinned by the console's
  tests, and the window shows it verbatim rather than reword it.
- **The copied pieces stay copies**, each marked `copied from` in its header, a fix going to
  its source first: `Presentation/DarkTitleBar.cs` (AstroDarkroom's, LightsCheck's before it),
  `Presentation/TExtension.cs` and `Presentation/Mvvm.cs` (DarksCheck's, FlatCheck's
  originally), `Theme/Dark.xaml` (AstroDarkroom's palette and button style, with the text
  field, check box, list and scroll bar styles of LightsCheck's `Theme/Dark.xaml`, trimmed to
  the controls this window uses, plus a radio button drawn after the check box, which no
  sibling has), `Views/AboutWindow` and `ViewModels/AboutViewModel` (DarksCheck's shape without
  the third-party card: this project has no package), and the test fixture `StaFixture`
  (DarksCheck's). The comments and the language pairs are this project's, as DarksCheck's ADR
  0019 and 0021 allow.
- **The icon** (Q8): `Icon/make_icon.py` draws `Icon/copywcs.ico` size by size, as every
  sibling does: a dark rounded square, a solved frame with a thin celestial grid and a few
  stars, a short arrow to a second, empty frame; the sizes up to 24 px keep the grid and the
  arrow only. Both executables carry it through `ApplicationIcon`; the windows through the
  `Resource`.
- **The command line** (Q9): `--lang` as the console reads it, in `App`'s constructor so
  `Ui.Default` is set before the first view loads (FlatCheck's `ApplyCommandLine`); a bad value
  is said in an English message box once the window is up; the first two existing files among
  the other arguments fill the source and the destination; anything else is ignored. No live
  language switch: the owner asked for the command line, FlatCheck's way (AstroDarkroom's
  switch, its ADR 0006, is a follow-up if ever wanted).
- **The warranty paragraph lives in the core** as `LicenseInfo.WarrantyLines` (the five lines
  `--help` prints, a pair) and `LicenseInfo.Warranty` (the lines joined by a space, for the
  About window), with `LicenseInfo.LicenseUrl`, so the console and the window cannot say
  different words about the same licence.
- **The window's tests are a project of their own**, `tests/CopyWcs.Wpf.Tests`, because they
  must target `net8.0-windows` to load WPF while `tests/CopyWcs.Tests` stays portable; they link
  `FitsTestData`, `TempWorkspace` and `TestData` from `tests/CopyWcs.Tests` rather than copy
  them (one generator), and `RepoInvariantTests` reads their sources for the `R###_` names. Two
  local deviations: the window project allows unsafe blocks for the one `LibraryImport` that
  darkens the title bar, and the window tests silence CA1305 for the linked `FitsTestData` (a
  WPF project cannot have the `InvariantGlobalization` switch; `App.ForceInvariantNumbers` pins
  the culture by hand, FlatCheck's ADR 0037).

## Alternatives rejected

- **The view-model reading the cards and showing keyword, value and comment in columns.** A
  second parser of a card, and a second place that could disagree with what the file holds; the
  trimmed card is what the console shows and what Siril shows.
- **The window rewording the refusal of a mismatch to name its check box.** The sentence would
  differ from the console's and would be a sentence of the window's about the copy; the rule
  above is worth more than the one awkward flag name.
- **A shared WPF library with the siblings.** Rejected in every sibling (DarksCheck's ADR 0009,
  the plan's decision 1 there): five copies of three small files cost less than a package
  with a version.
- **The window's tests inside `tests/CopyWcs.Tests`.** That project would then target
  `net8.0-windows` and the core's tests would need a Windows build host.
- **A live language switch.** Out of the owner's request; AstroDarkroom's exists if wanted.

## Consequences

- `MainViewModelTests` prove R-031 against the console's dry run and R-032 byte for byte
  against the console's copy; `WindowSmokeTests` render both windows in both languages with
  no binding error and no Romanian word in the English pass (R-036, R-037); `AboutTests` bind
  the About window to `VersionInfo` and `LicenseInfo` (R-035); `AppTests` cover `--lang` and
  the files from the arguments (R-034); `IconTests` tie both executables to `Icon/copywcs.ico`.
- The design note `docs/design/window.md` holds the mechanics; `kit.json` publishes the window
  as `CopyWcs.Wpf.exe` beside the console (`"output": "wpf"`).
- A change to a wording of the core shows in both applications at once; a new text of the
  window is a pair in XAML or in the view-model, never a literal.
