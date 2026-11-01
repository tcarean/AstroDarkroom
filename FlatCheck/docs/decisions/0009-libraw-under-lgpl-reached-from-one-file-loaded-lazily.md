# 0009. libraw (Sdcb.LibRaw) under LGPL-2.1, reached from one file, loaded lazily, nothing developed

Date: 2026-09-25
Status: accepted 2026-09-25; the message naming what is missing superseded by 0085

## Context

Retroactive: recorded on 2026-09-25 from docs/design/dependencies.md; the decision itself dates
from the addition of `--expo` on a camera raw, the last feature gap against `flatcheck.py`. A
Nikon NEF's pixels are Huffman-coded with a predictor and pass through a linearization curve
from the MakerNote; no pure-managed .NET decoder exists. `--expo` needs the undebayered mosaic
at the original ADU scale, so anything that demosaics destroys the measurement. The native
LibRaw packages are offered as LGPL-2.1-only OR CDDL-1.0, and the tool is GPLv3-or-later.

## Decision

Use `Sdcb.LibRaw` (MIT wrapper) with its `runtime.win64` / `runtime.linux64` native packages,
taking LibRaw under the LGPL-2.1 option. Name a libraw type in exactly one file, `RawReader.cs`;
keep the assembly lazily loaded; read only `raw_image` (visible area), `color.maximum`, the black
levels, `idata.filters` / `idata.cdesc` and the embedded preview bytes - no demosaic, no white
balance, no gamma.

## Alternatives rejected

- **`FC.SDK.Raw`.** Pure managed but Canon-only.
- **`NDCRaw`.** Shells out to `dcraw.exe`.
- **`HurlbertVisionLab.LibRawWrapper`.** C++/CLI, Windows-only.
- **Vintasoft / Accusoft / LEADTOOLS / Atalasoft.** Commercial, and they demosaic.
- **ImageSharp, SkiaSharp, Magick.NET.** No raw mosaic access at all.
- **A hand-rolled NEF decoder.** Huffman + predictor + MakerNote linearization curve; not honest
  work for this tool (ADR 0008).
- **The CDDL-1.0 option.** Not GPL-compatible; choosing it would break the tool's own licence.
  LGPL-2.1 §3 permits applying GPL v2 "or any later version", which brings it into GPLv3.
- **libraw-typed fields on `RawReader`.** Fields are resolved when the type is loaded, and
  `ImageLoader` loads the type on every run to ask `Handles`; a static field of a libraw
  reference type is deferred by the CLR, a field of a libraw value type breaks every FITS run.
  Measured, not assumed.

## Consequences

- Numbers are bit-identical to `rawpy`, which binds the same libraw: on a real D3300 NEF the
  same pixel sum (35598956557), the same mean to twelve decimals, the same four CFA medians, the
  same report text apart from the scale-source label (`libraw (exact)` vs `rawpy (exact)`).
- Everything downstream still sees an `Img`; `ShootingInfo` reads focal, f-number, make/model
  and the Nikon lens ID as locals only, for the lazy-loading reason.
- Deleting `Sdcb.LibRaw.dll` itself, not only the native DLLs, leaves a FITS run at exit 0; a
  raw run exits 1 naming what is missing, because `RawReader.Load` catches
  `DllNotFoundException` and friends rather than letting `0x8007007E` reach the user.
- Publishing the single binary needs `-p:IncludeNativeLibrariesForSelfExtract=true`; only
  `win-x64` and `linux-x64` have a libraw binary, other RIDs are FITS-only.
- `ThirdParty.cs` carries LibRaw's copyright line from LibRaw 0.21.1's `COPYRIGHT` file, shown
  in "Despre" per LGPL-2.1 §6.
- Pinned by `HoldsNoLibrawTypedFields_SoTheAssemblyStaysLazilyLoaded` (nothing else would
  notice) and `LibRawIsTakenUnderTheLgplAndCarriesItsCopyright`.

Source note: docs/design/dependencies.md (`Sdcb.LibRaw`).
