# 0030. The Dmax can be read from an ICC printer profile, with Wacton.Unicolour

Date: 2026-10-05
Status: accepted 2026-10-05

## Context

ADR 0028 took baryta's Dmax from the lab's ICC profile, read by hand. The same profile of the
same paper on another printer gives another black (2.41 on the lab's Canon iPF8300, 2.14 on an
Epson P900), so a medium's default is right for one printer only, and the owner asked for the
tool to read the Dmax from a profile itself: a button beside the window's Dmax field that lists
the installed profiles. A hand-written reader existed for the v2 `lut16` table; the owner asked
whether a free package covers every kind of profile, so that a new kind does not stop the tool
later.

Measured on 2026-10-05, the device black through the relative colorimetric table, the
hand-written reader against Wacton.Unicolour 8.0.0 (MIT, pure managed, no dependencies, one
724 KB assembly):

| profile | hand-written | Unicolour |
|---|---|---|
| Chromatone, Photo Rag Baryta, Canon iPF8300 (v2, lut16) | L\* 3.49, Dmax 2.41 | L\* 3.49, Dmax 2.41 |
| Harman Canvas (v2, lut16) | L\* 20.84, Dmax 1.49 | L\* 20.84, Dmax 1.49 |
| Hahnemühle, Photo Rag Baryta, Epson P900 (v4, lutAToB) | not read | L\* 6.54, Dmax 2.14 |
| a v4 lutAToB profile built by hand with black at L\* 30 | by construction | L\* 30.00, Dmax 1.21 |

Two behaviours of the package the code has to know: on a file that is not a profile it does
not throw, it records an error and computes a "naive" answer (L\* 100); and through the
perceptual intent it answers L\* 3.14 (Dmax 2.46, the ICC reference medium) for every profile,
the canvas included. The header of both lab profiles names the perceptual intent.

## Decision

1. The core reads a printer profile's Dmax in `IccPaperBlack`, with **Wacton.Unicolour** for the
   tables and its own `IccProfileInfo` for the name: the device black (every channel 0 on RGB,
   every ink at 1 on CMYK), through the **relative colorimetric** intent, asked for explicitly,
   relative to paper white (ADR 0028), rounded to the two decimals a Dmax is printed with.
2. Only profiles of class `prtr` with an RGB or CMYK device are read; a CMYK profile carries a
   note that a real printer lays less ink than "everything at maximum". Anything else, and any
   failure of the package, is a sentence for the user, never an exception.
3. The console takes `--icc <file>`, which excludes `--dmax`; the window has `Din profil
   ICC...` beside a narrower Dmax field, with the printer profiles of the Windows colour folder
   and a way to add a file. The report names the profile in Anexa F while the Dmax is the one
   read from it.
4. The package is the reader; no hand-written fallback is kept.

## Alternatives rejected

- **The hand-written reader.** It knows one of the three table types a v2 or v4 profile can
  carry for this; the owner's own machine has a v4 `lutAToB` profile installed.
- **lcms2 through `lcms2-dotnet` or `lcmsNET`.** The reference engine, but a wrapper over a
  native library: the console would stop being one copyable file (the rule "Package notices").
- **ImageSharp's ICC types, already a dependency.** 3.1.11 parses every tag and evaluates none;
  the evaluation would still be ours.
- **The profile's `bkpt` tag.** Absent from the v4 profile, and 0.05 away from the table on the
  v2 one (2.46 against 2.41 relative).
- **The absolute Dmax.** See ADR 0028: the image's white prints as paper white.

## Consequences

- A second package in the core: `Directory.Packages.props`, `ThirdParty.cs`, the notice in
  `THIRD-PARTY-NOTICES.md`, the README's table, the lock files of the five projects.
- Profiles of ICC version 5 (iccMAX) are not read; neither does the package read them.
- The list of installed profiles exists on Windows only; elsewhere it is empty and `--icc`
  with a path still works.
- A profile says what the profiling target measured on the day it was made (the lab's dates
  from 2012); it does not replace a test strip.
- Guards: `IccPaperBlackTests` on hand-built v2 `lut16`, v4 `lutAToB` and CMYK profiles whose
  perceptual table has another black (a reader on the wrong intent gives 2.46 and fails),
  `IccPickerTests`, `WindowSmokeTests.R132_TheProfileWindowLoadsAndRenders`; V-006 and V-007 in
  `docs/validation.json`, on the two real profiles, outside the gate.
