# Read the Dmax from an ICC profile

Date: 2026-10-05. Approved by the owner in chat, with the first task
(`2026-10-05-source-the-dmax-of-each-medium-and-show-it-in-the-window.md`); the decisions are ADR
0030. The branch starts from the first task's branch, not from `main`: the main checkout held
another session's unfinished merge when the first task was to be merged.

## Why

A medium's Dmax is right for one printer. The same paper measures 2.41 on the lab's Canon and
2.14 on an Epson P900, by their profiles; the owner wants the tool to read the Dmax of the
paper and printer at hand from the profile the lab publishes.

## Decided by the owner

1. The package Wacton.Unicolour (MIT, pure managed) instead of a hand-written reader, after a
   probe on the installed profiles matched the hand-read figures.
2. The Dmax relative to paper white, from the relative colorimetric table.
3. A narrower Dmax field with a button beside it that lists the installed ICC profiles, lets
   one be chosen and writes its Dmax into the field.
4. `--icc <file>` in the console as well.

## Steps

1. Requirements: R-028 (the reading, the console flag, the installed list), R-132 (the window).
2. The package: `Directory.Packages.props`, the core's csproj, `ThirdParty.cs`,
   `THIRD-PARTY-NOTICES.md`, the README's table, the lock files.
3. The core: `Input/IccPaperBlack.cs`; `Parameters.DmaxProfile` and `WithDmaxFromProfile`;
   `ReportWriter.DmaxOrigin` for Anexa F.
4. The console: `--icc`, refused together with `--dmax`; `--help` and its golden.
5. The window: `IccPickerViewModel`, `IccPickerWindow`, the button in the parameter card,
   `ParameterFields.UseProfile`.
6. Tests on hand-built printer profiles (`IccPrinterTestData`: v2 `lut16` on RGB and CMYK, v4
   `lutAToB`), whose perceptual table has another black; the picker's view-model; a smoke
   render of the window.
7. Validation: the lab's two real profiles in `testdata/large/`, cases V-006 and V-007 of a
   new kind, `icc`.
8. ADR 0030; `input.md`, `console.md`, `window.md`, `dependencies.md`, `testing.md`,
   `code-map.md`; README; CHANGELOG.

## What deviated

- The branch was stacked on the first task's branch, as said above; both were merged into `main`
  on 2026-10-05, in order, once the other merge there was concluded.
- After the code review: the CMYK note goes into Anexa F, not only into the window's list; a
  profile whose black gives a Dmax outside (0, `Thresholds.DmaxMax`] is refused as damaged,
  where the first version analysed with it; the MIT text is reproduced in the notices; the
  level-L\* ladder labels a Dmax read from a profile; a folder that cannot be listed is an
  empty list.
- Left as known limits (`docs/design/known-limitations.md`): the installed profiles are read on
  the UI thread, and the chosen medium keeps its name when a profile replaces its Dmax.
- Not done: a single-file publish of the console with the new package was not run.
