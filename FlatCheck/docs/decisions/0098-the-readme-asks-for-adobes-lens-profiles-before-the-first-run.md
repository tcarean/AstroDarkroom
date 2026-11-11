# 0098. The README asks for Adobe's lens profiles before the first run

Date: 2026-11-11
Status: accepted 2026-11-11

## Context

The README listed Adobe's lens profiles among what FlatCheck works with as "optional": without
them the lens list holds lensfun's profiles. On 2026-11-11 the owner, reading FlatCheck's manual
in the suite's wiki, found no instructions for installing them and said that without them an
important part of FlatCheck does not work, so they belong in the first part of the manual. What
is lost without them (ADR 0090, ADR 0073): the depth of the vignetting is judged against
lensfun's profiles alone, which on the Tokina 11-20 sat 0.19 to 0.25 stops deeper than Adobe's
one measurement and turned the PERFECT master of the window's screenshots into ACCEPTABLE; and a
raw that names its lens only by a number, as a Nikon's does, is matched by its focal range alone,
as the red paragraph of ADR 0073 tells the user. FlatCheck itself starts and runs every tab
without them. Measured on 2026-11-11: Adobe's page links the converter for Windows x64 through
`https://www.adobe.com/go/dng_converter_win`, which redirects straight to the installer on
`download.adobe.com`, 1,823,404,160 bytes, with no sign-in; on the test computer the converter
put 3,531 `.lcp` files into `C:\ProgramData\Adobe\CameraRaw\LensProfiles\1.0`. The manual's
first part gained a section, "Installing Adobe's lens profiles", the same day (wiki 551f1c3), and
the owner asked for the READMEs to say the same.

## Decision

The README no longer calls Adobe's lens profiles optional: it asks for them to be installed
before the first run, says in plain words what the vignetting check loses without them, and
links the manual's section for the steps
(`https://github.com/tcarean/AstroDarkroom/wiki/FlatCheck#installing-adobes-lens-profiles`).

## Alternatives rejected

- **Keeping "optional".** True in that FlatCheck runs without them, but it hides that one of its
  main checks then rests on less reliable profiles.
- **Calling them required.** FlatCheck starts and every tab works without them; the window says
  in red what is missing (ADR 0073).
- **Writing the steps in the README too.** The manual already has them, with how to check that
  FlatCheck found the profiles; two copies of the steps would drift.

## Consequences

The README's line depends on the manual's anchor `#installing-adobes-lens-profiles`: renaming
that section in the wiki breaks the link. The launcher's prerequisites text (AstroDarkroom
R-009) still says "Optional, for FlatCheck"; it changes only with a requirement of its own there.
