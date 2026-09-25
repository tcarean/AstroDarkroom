# 0024. The camera's embedded preview is shown beside the raw histograms and never votes

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Retroactive: recorded on 2026-09-25 from docs/design/modes-expo.md; the decision itself dates
from the original design of the preview view. The camera's own histogram is computed from the
JPEG preview embedded in the raw, not from the sensor data, and the gap between the two is what
misleads people at the camera. Measured on the centre window of the brightest channel of two
library sky flats: Nikkor 35 DX at ISO 800, raw 29.0 % against 81.2 % on the camera screen
(screen peak 84.7 %); Samyang f/2.0 at ISO 1600, raw 32.1 % against 98.0 % (screen peak
100.0 %, pixels at 255). The second frame looked burnt on the camera and had 1.6 stops of
headroom in the file.

## Decision

The window can switch the exposure charts to the preview. `RawReader.Decode` enumerates
libraw's thumbnail list and picks the largest JPEG whose long side is at most 3000 px; failing
that the largest JPEG, decoded scaled; failing that a bitmap libraw already decoded; failing
that null. The bytes are copied while the file is open. Decoding is opt-in
(`ExposureRun.Execute(..., withPreview)` defaults to false; only the window passes true), goes
through ImageSharp with `SkipMetadata` and no ICC transform. The preview side has no bands and
no verdict: `PreviewRows` sets the four band fields to NaN and `HistogramCanvas` skips them.
The comparison table pairs the raw centre median with the preview's, per colour read off
`Channels[i].Colour`, the two greens averaged; whole-frame medians are not compared.

## Alternatives rejected

- **A target band on the screen's axis.** It would say "expose until the screen reads
  50-65 %", the exact mistake the view exists to cure, and it cannot be computed: on a D3300
  frame with full tonal range, a power law fitted on a flat-like slice and asked for the screen
  value at raw 57 % (measured 95.7 %) predicts 90.5 % from 23-31 %, 100.9 % from 20-35 % and
  98.2 % from 10-60 %; the real curve saturates at 100 and no power law does, and the curve is
  per channel anyway (white balance x2.355 on R, x1.086 on B on that file).
- **Fixed thumbnail indices.** They are not stable across cameras; hence the enumeration.
- **Always decoding the largest preview.** On the D3300 the 1620 x 1080 entry agrees with the
  6000 x 4000 one to 0.4 points at a twelfth of the time (16 ms / 5 MB against 460 ms / 69 MB
  native, 485 ms / 9 MB with `TargetSize` - which buys memory, not time).
- **Decoding in every mode.** Structure, ratio and light never look at the preview; the console
  never decodes.
- **Applying the embedded colour profile.** That is a different image from the one on the
  screen; on ImageSharp 4.x `ColorProfileHandling` would have to be set to `Preserve`
  explicitly.
- **Comparing whole-frame medians.** The raw one is pulled down by vignetting in a way the
  preview's is not, so the gap would be partly the lens.

## Consequences

- `ThePreviewNeverChangesTheVerdict` pins that attaching a preview leaves every verdict figure
  bit-identical.
- `ScreenReading` states a mechanism and a measurement, not a law: the screen saturates before
  the sensor when the white balance multiplies the channel by 1 or more and the tone curve
  reaches 255, which every camera measured here did, by a third of a stop to a stop. "Not
  touching the edge" is a guarantee; "touching the edge" is not information.
- libraw facts: JPEG entries report `Bits = 0`; asking past `ThumbCount` throws error -9.
- Every raw load copies the chosen preview's bytes (a few MB), including modes that never look
  at them (known issue 8).
- Source note: docs/design/modes-expo.md ("The camera's preview").
