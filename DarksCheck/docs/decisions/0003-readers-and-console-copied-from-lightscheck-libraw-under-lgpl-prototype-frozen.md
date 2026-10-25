# 0003. The readers and the console skeleton are copied from LightsCheck, libraw is taken under LGPL-2.1, the Python prototype is frozen

Date: 2026-10-11
Status: accepted 2026-10-11; a copy changes only after its source superseded by 0019

## Context

LightsCheck's `Imaging/` holds FlatCheck 2.3.0's readers (`RawReader` through libraw,
undebayered, at the exact ADU scale; the hand-rolled `FitsReader`, `PngWriter`, `BitmapFont`;
`CfaPattern`, `Img`, `Mosaic`, `Stats`) with two files adapted for the shot metadata the
inventory needs (exposure, ISO, time of capture, camera; LightsCheck's ADR 0003). DarksCheck
needs exactly that, plus LightsCheck's session, cadence, inventory and console code, which
is the same for darks with other folders and other checks. The plan's decision 1 copies rather
than shares. FlatCheck's `FitsReader` has since changed (its ADR 0057: a file that does not
start with `SIMPLE` throws), a refresh LightsCheck has not taken yet. The nine checks were
done with five Python scripts (rawpy, numpy, scipy, matplotlib) on 2026-10-11, whose figures
on the Smida session and on the library are the reference.

## Decision

`src/DarksCheck.Core/Imaging/` is LightsCheck's `src/LightsCheck.Core/Imaging/` at `main`
6cc0078, changed only by the namespace rename, with its tests (`CfaPatternTests`,
`FitsReaderTests`, `RawReaderTests`, `FitsTestData`, `SyntheticFrame`). The copies stay
copies: a fix goes to its source first, FlatCheck for the files LightsCheck copied unchanged
and LightsCheck for `RawReader.cs` and `ImageLoader.cs`, then the copy is refreshed here. The
`FrameInfo.Read` detour for a FITS that does not start with `SIMPLE` (an empty header from the
current reader) is kept until LightsCheck takes FlatCheck's ADR 0057, when the copy is
refreshed and the detour goes. `.editorconfig` relaxes the style rules for the copied folder
and each copied file keeps its `#nullable disable`. The session, cadence, inventory, report
and console files are LightsCheck's too, adapted to darks and English-commented as
DarksCheck's own (no copy marking: they diverge from the first day). LibRaw is taken under
LGPL-2.1 for the reasons FlatCheck's ADR 0009 gives, with its notice in `ThirdParty.cs` and
`THIRD-PARTY-NOTICES.md`; the WPF pieces (theme, title bar, Mvvm, converters, palette, charts,
"Despre") are copied from LightsCheck at plan step 4 under the same rule. The five Python
scripts are committed as `prototype/`, frozen, with the GPL header added: not maintained, not
run by the gate, the numeric oracle of `docs/validation.json` (ADR 0006); their report, JSON,
CSV and figures are `docs/validation/2026-08-12-smida/`.

## Alternatives rejected

- **FlatCheck's readers directly.** LightsCheck's adaptation (the shot metadata, `AduScale`,
  the lazy libraw load guarded by a test) would have to be redone.
- **A shared `Photo.Imaging` library.** Rejected by the owner on 2026-10-09 (LightsCheck's
  ADR 0003); restated here so DarksCheck cites its own decision.
- **Taking FlatCheck's newer `FitsReader` now.** It would make DarksCheck's copy differ from
  LightsCheck's, the source it refreshes from; the detour costs three lines.
- **Maintaining the Python scripts beside the C#.** Two implementations drift; the scripts
  stay as the record of what was measured, as FlatCheck's ADR 0047 froze `flatcheck.py`.

## Consequences

The rule "Copied files stay copies" in `CLAUDE.md`; `ThirdPartyTests` binds the package, the
notices file and `ThirdParty.cs`; `LicenseHeaderTests` scans `prototype/` too. The validation
figures trace to `prototype/` and `docs/validation/2026-08-12-smida/results.json`.
