# 0026. The pixel size comes from the raw's 35 mm-equivalent focal length over its mosaic's diagonal when no file states it and none was typed; 3.92 is the last fallback

Date: 2026-10-27
Status: accepted 2026-10-27
Supersedes: 0002 in part (the D3300's 3.92 um as the default pixel size)

## Context

The series run converts pixels to arcseconds with a pixel size: a FITS may carry `XPIXSZ`, a
raw never says it, and until now the fallback was the constant 3.92 um of the owner's Nikon
D3300 (`SeriesOptions.D3300PixelUm`), which `--pixel` and the window's field replaced. Right for
one camera, wrong for every other unless the user types it. On 2026-10-26 the owner asked for a
measurement of a default derived from the camera's crop, with the options for the crop's source,
and chose on 2026-10-27, after the measurement, the raw's own figure ("mergem pe optiunea C").

Measured on 2026-10-27 with a throwaway libraw 0.21.1 probe on the first light of each real
session (`docs/validation.md`), both D3300 raws:

- The visible mosaic is 6016 x 4016 with no margins; today's 3.92 is Nikon's 23.5 mm over the
  6000 pixels of the JPEG, and 23.5 mm over 6016 is 3.906.
- libraw's `MakerNotes.CameraFormat` is `Unknown` (0) on the D3300: no source.
- libraw's `LensInfo.FocalLengthIn35mmFormat` (EXIF FocalLengthIn35mmFilm) is 202 for the 135 mm
  Samyang (a lens without CPU, its data entered in the camera) and 30 for the Tokina at 20 mm:
  the camera's nominal crop of 1.5, rounded to an integer.
- The lensfun camera table FlatCheck ships (1053 bodies, 5.1 MB, CC BY-SA 3.0) gives the D3300
  a crop of 1.523.
- The pitches: lensfun 3.93 um (+0.3 % on 3.92), the raw's 35 mm figure 3.99 to 4.00 (+1.8 to
  +2.0 %), Nikon's width 3.91 (−0.3 %). The crop is a ratio of sensor diagonals, so the right
  formula is the full-frame diagonal (43.2666 mm) over the crop, over the mosaic's diagonal in
  pixels; width-based, it agrees on a 3:2 sensor and is 4 % off on a 4:3 one (17.3 x 13 mm,
  4592 x 3448: 3.77 against 3.92).

## Decision

- **The order** (`PixelPitch.Choose`, R-066): the first file's `XPIXSZ`; else the value typed
  (`--pixel`, or the window's field); else the first raw whose focal length, 35 mm-equivalent
  focal length and size give one (`PixelPitch.FromCrop`); else `D3300PixelUm`. The typed value
  stays below `XPIXSZ`, as it was: a FITS from a converted raw states the pitch exactly.
- **The formula**: `43.2666 mm x focal / (focal35 x sqrt(W² + H²))`, in microns; null when any
  of the four is missing or not positive, so a FITS and a raw of a body with no lens data
  entered fall through.
- **None typed is null**: `SeriesOptions.PixelSizeUm` is nullable, the console's option absent
  by default, the window's field blank by default and not marked invalid when blank; a wrong
  value is null too, and marked.
- **The words**: the `scale` line names the source, `from the raw (35 mm-equivalent focal
  length)` / `din raw (focala echivalenta 35 mm)` and `default, Nikon D3300` / `implicit, Nikon
  D3300` beside the two it had.
- **The figures move**: on the Veil session the scale becomes 6.11 "/px instead of 5.99, so the
  median move `13.29 px` reads 81.2" instead of 79.6" and the polar error 4.6'–6.8' instead of
  4.5'–6.7'; pixel-unit figures and every verdict stay. `docs/validation.json` changes only on
  the owner's word (the kit's rule "No test edited to pass"), with the `/validate` drift pasted.

## Alternatives rejected

- **FlatCheck's lensfun camera table, shipped beside the exe.** 0.3 % and 1053 bodies, at the
  price of a second 5 MB CC BY-SA copy in the monorepo, a notices entry and a camera reader
  copied from `LensfunReader`; the owner chose the raw's own figure, and the table can come
  later if 0.3 % ever matters.
- **An own list of bodies with their sensor size.** Exact where listed, nothing for an unlisted
  body, stale at the first new model.
- **libraw's `CameraFormat`.** Unknown on the owner's camera, measured.
- **The width form `36 / crop / W`.** The owner's wording; equal on a 3:2 sensor, 4 % off on a
  4:3 one, since the 35 mm equivalence is a ratio of diagonals.
- **The typed value above `XPIXSZ`.** A behaviour change nobody asked for; a FITS that states
  its pitch states it exactly.
- **Deriving the pitch in the reader into `PixelSizeUm`.** It would say the raw states the pixel
  size, which it does not; the derivation is a number, so it lives in `Analysis/`.

## Consequences

- `Analysis/PixelPitch.cs` (`FromCrop`, `Choose`, `PixelPitchSource`); `LoadedFrame` and
  `FrameInfo` carry `FocalLength35Mm`, `Width`/`WidthPx`, `Height`/`HeightPx`; `SeriesOptions`,
  `Options` and `MainViewModel` carry a nullable pixel size; the help text and the window's
  tooltip say the order.
- Guards: `PixelPitchTests` (`R066_`: the two sessions' values, the 4:3 case, every missing
  input, the order on fabricated frames, the source words), `MainViewModelTests` (`R029_`: blank
  is none typed and valid), the gated real-raw test (the size and the 35 mm figure agree between
  the two reads). The goldens' FITS fixtures carry `XPIXSZ 3.92` and do not move; the help
  goldens change their `--pixel` lines.
- The owner's D3300 sessions now run at 4.00 um instead of 3.92: 2 % more arcseconds in every
  figure the report gives in arcseconds, from an integer the camera rounded. `--pixel 3.92`
  restores the old figures.
