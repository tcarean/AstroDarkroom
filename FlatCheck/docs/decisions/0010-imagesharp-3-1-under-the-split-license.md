# 0010. ImageSharp 3.1.x decodes the camera preview, under the Six Labors Split License

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Retroactive: recorded on 2026-09-25 from docs/design/dependencies.md; the decision itself dates
from the Exposure tab's camera-preview comparison. The window shows the histograms of the JPEG
preview embedded in a raw - the image the camera's own screen histogram was computed from - next
to the raw data's. That needs a JPEG decoder: Huffman, DCT, chroma subsampling, YCbCr,
progressive mode, an order of magnitude beyond the hand-rolled PNG encoder that only writes, for
a picture that never votes on a verdict. ImageSharp 3.x and later are under the Six Labors Split
License 1.0, a conditional dual licence.

## Decision

Use `SixLabors.ImageSharp`, pinned to 3.1.x, reached from exactly one file, `PreviewReader.cs`.
The Split License grants Apache-2.0 terms to software under an Open Source or Source Available
licence; FlatCheck is GPLv3, so it qualifies with nothing to change, and Apache-2.0 flows into
GPLv3 one-directionally ("Apache 2 software can therefore be included in GPLv3 projects", ASF).

## Alternatives rejected

- **A hand-rolled JPEG decoder.** The scope above, for a picture with no vote.
- **`StbImageSharp`** (Unlicense/MIT, zero dependencies, ~identical output measured against an
  independent decoder on real previews). It would have carried no licence footnote at all;
  ImageSharp was preferred for robustness on the awkward previews - progressive, CMYK/YCCK,
  unusual sampling - and for scaled decoding. It remains the drop-in fallback.
- **ImageSharp 4.x.** From 4.0.0 an MSBuild task validates a licence key at compile time: a
  warning on Debug, an error on Release without a `sixlabors.lic` or `$(SixLaborsLicenseKey)`.
  The key is free to open-source projects but on application, so anyone cloning the repo would
  need their own to publish. 3.1.x carries the identical Split License 1.0 text and no task.
  Moving up would also require `ColorProfileHandling.Preserve` explicitly.
- **An unpinned version.** The licence text is "Version 1.0, June 2022" and does not say which
  licence version governs which release; the grant travels with the version taken.

## Consequences

- The seam is one file wide: `PreviewReader.cs` is the only file naming an ImageSharp type, as
  `RawReader.cs` is for libraw. `Decode(EmbeddedPreview)` returns null on any failure, including
  a missing assembly, so a preview that will not decode only disables the switch.
- No ICC transform on decode: 3.1.x never applies one, which is the behaviour wanted (the preview
  on screen is the untransformed one); `SkipMetadata` is set so the profile is not even read.
- Because GPLv3 forces every derivative to stay GPLv3, everyone downstream qualifies on the same
  licence condition; the eligibility does not break as the tool is passed on.
- The version pin is legal hygiene as well as reproducibility; a bump is a licence re-read.
- If the licence ever moves, `StbImageSharp` replaces it behind the same file.
- The package is listed in `THIRD-PARTY-NOTICES.md` and `ThirdParty.cs`, bound by
  `ThirdPartyTests` (ADR 0008).

Source note: docs/design/dependencies.md (`SixLabors.ImageSharp`).
