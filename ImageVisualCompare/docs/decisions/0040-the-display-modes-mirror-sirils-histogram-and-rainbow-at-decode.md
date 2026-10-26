# 0040. The display modes mirror Siril 1.4.4's Histogram display and its rainbow, computed at decode over the 16-bit row; a switch decodes the pair again

Date: 2026-10-26
Status: accepted 2026-10-26
Supersedes: 0016 in part (the moment of the release: after the new pair is shown and rendered once, not before)

## Context

A linear stack shows nearly black (ADR 0029: "nothing stretches ... and that is accepted"), so
two variants of one processing cannot be told apart by eye, only by the readout. On 2026-10-26
the owner asked for an option, for a FITS in its linear state, to compare the pair through
Siril's "Histogram" display mode and through its false colours, "implemented as in Siril", and
pointed at FlatCheck's implementation. ADR 0029 had deferred exactly this: "a stretch on a key:
a feature of its own (requirements, a decision on which stretch)".

What Siril 1.4.4 does, read in its source for FlatCheck (its ADR 0025 and 0063,
`docs/design/siril-histeq-source.md`): one histogram per layer of 65 536 bins on the 16-bit
value (for a float, `computeHisto` bins it at `floor(v * 65535)` and the remap looks it up at
`round(v * 65535)`), zeros skipped but counted in the denominator, `index[0] = 0`, with the
cumulative started from bin 0, `index[i] = round_to_BYTE(cumulative(i) / pixels * 255)` with bin `i` included and
`round_to_BYTE(x) = (byte)min(255, (int)(x + 0.5))`; the RGB view composited from the three
remapped layers. The rainbow is a hue from 300° at level 0 to 0° at level 255, saturation and
value 1, through Siril's `hsv_to_rgb`, applied to the remapped byte of one layer; its RGB tab
takes the R byte of R's colour, the G byte of G's and the B byte of B's, which leaves the
palette, so FlatCheck never colours a composite. FlatCheck bins on the layer's own min..max and
averages a three-plane FITS into one plane before anything.

What the code holds: after a load only the frozen 8-bit Bgra32 survives (`LoadedImage`), and
`FitsRowDecoder` turns each sample straight into a level through `FitsScale.Level`. Equalising
those levels would be useless: a linear stack has nearly all of them in 0 to 3. Measured for
ADR 0029 on `result_3813s.fit` (5938 × 3888, three float planes, 277 MB): 0.59 to 0.61 s a decode
pass; the planes as floats would be 277 MB an image, a 16-bit copy 138 MB, a second Bgra32
bitmap 89 MB.

Four choices with more than one reasonable answer were put to the owner on 2026-10-26, each
with a recommendation; the owner took the recommendation each time, and the plan approved the
same day (`docs/plans/2026-10-26-add-the-histogram-and-false-colour-modes.md`) settled the
rest.

## Decision

- **Three display modes, `Linear`, `Histogram` and `FalseColours`** (`DisplayMode`), asked for
  per load through `IImageLoader.LoadAsync`, and available only for a pair whose two files are
  shown as stored (a FITS, or a float TIFF, ADR 0029 and 0035): a new `ImageInfo.ShownAsStored`,
  known from the probe. No linearity detection (the owner's answer): no FITS keyword says it,
  and a threshold on the median would be a guess. Integer PNG and TIFF files keep WIC's
  conversion and the linear display whatever is asked; ADR 0022 stands, since what is read is
  what the loader did, not the pixel format.
- **The equalisation is Siril's, per channel, over the 16-bit value the level is already
  computed from.** `FitsRowDecoder.ReadPlaneRow` and `FloatSamples.ToSixteenBit` produce the
  16-bit row (`FitsScale.SixteenBit`), `SampleHistogram` counts it in 65 536 absolute bins, bin 0
  skipped and counted among the pixels, `LevelLookup` is the inclusive CDF times 255 with
  Siril's rounding and `index[0] = 0`, and `DisplayRenderer` applies one lookup per channel.
  Siril's absolute bins, not FlatCheck's min..max: Siril is the display the tool is compared
  against (ADR 0039), and on the 16-bit value the two are the same thing.
- **The false colours are Siril's table on one layer: the mean of the three 16-bit planes,
  `(r + g + b + 1) / 3`, for an RGB file, the plane itself for a gray one** (the owner's
  answer), equalised as one layer and then `RainbowTable`. The composite is never coloured.
- **A switch decodes both files again** (the owner's answer) behind the progress overlay, with
  the padding answered yes again, and `CompareView.ReplacePair` swaps the bitmaps under the
  unchanged `Viewport` and `DividerModel`. A mode other than `Linear` costs one counting pass
  (two when the file counts to 65 535) and one rendering pass over the file.
- **Two toolbar toggles and the keys `H` and `F`** (the owner's answer), the mode in the pair
  line of the status bar, and in `FalseColours` the readout shows the level the colour stands
  for, through the inverse of the table, which is one to one.
- **The outgoing pair is released after the incoming one is shown and one render pass has
  run**, for a switch and for an open alike; ADR 0016 released it before the new pair was
  shown. Found on the first switches in the application (the measurements below): the
  surface's `Image` elements keep the outgoing bitmaps in their render content until the next
  render pass, so a collection before it frees nothing, and the process carried the outgoing
  pair until the next open. `MainWindow.ShowPair` now lets one render pass through
  (`Dispatcher.Invoke` at `Render` priority) and releases after it.

## Alternatives rejected

- **A 16-bit copy of each image for an instant switch.** +138 MB an image, +277 MB a pair, kept
  for the life of the pair, against 1 to 2 s a switch; ADR 0022 priced the same copy for a
  difference mode and did not take it.
- **One Bgra32 bitmap per mode, kept after the first switch.** +89 MB an image a mode, up to
  +534 MB a pair, for a switch that the owner does a few times a session.
- **Equalising the 8-bit levels, with no change to the decoder.** A linear stack has nearly all
  of them in 0 to 3; the lookup would map four levels to four and show nothing new.
- **FlatCheck's bins on the layer's own min..max.** Finer on a float image whose values sit in
  a narrow range, and not what Siril shows: a pattern Siril's display hides or reveals would
  differ here.
- **Colouring each channel and compositing, as Siril's RGB tab does.** Colours outside the
  palette wherever the channels' shapes differ (FlatCheck's ADR 0063, measured on a NEF).
- **A channel picker (R, G or B) for the false colours**, as Siril's tabs. Three more states and
  texts for a comparison the owner reads on the whole image; the mean of the planes is what
  FlatCheck shows for a three-plane FITS.
- **A linearity heuristic**, offering the modes only below some median. A guess on no
  measurement; a stretched file equalised is merely useless, not wrong.
- **A stretch the tool chooses** (asinh, autostretch). The owner asked for Siril's Histogram
  display, the one FlatCheck already mirrors; another stretch is another decision.

## Consequences

- `FitsScale.Level` is `SixteenBit(v) >> 8`, so the linear display did not move: the FITS
  loader tests (R014 to R017) and R091 pin the same bytes as before. The linear path now
  produces a 16-bit row and looks it up, one more pass over each row in cache; the cost is
  measured on `result_3813s.fit` and recorded below.
- A FITS and its float TIFF export equalise to the same bytes, as they show the same levels:
  both go through the same 16-bit row and the same renderer.
- A float sample is counted and looked up at the same rounded 16-bit value, where Siril bins it
  at the floor and looks it up at the rounded value, and counts bin 0 into the cumulative: on a
  float file the CDF here sits half a bin from Siril's, at most one bin's share of 255 on any
  level, and a sample exactly 0 or positive below 1/131 070 is skipped where Siril skips only
  the exact zeros. On integer data the two are the same thing. Measured on `result_3813s.fit`
  and recorded below: no sample in bin 0 at all (the code review of 2026-10-26 found the
  floor-versus-round half, which the first text of this ADR did not state).
- The readout in `Histogram` shows the equalised levels, which is what is on screen (R-051
  reads the shown bitmap); the file's own levels are one `H` away.
- Guards: `LevelLookupTests` (R-111's two examples, the rounding, a
  uniform ramp, the share per level), `RainbowTableTests` (R-112's ten colours, the inverse),
  `DisplayRendererTests`, `FitsRowDecoderTests` (`ReadPlaneRow` per BITPIX),
  `FitsImageLoaderTests` and `WicImageLoaderTests` (R110 to R112 on fixtures),
  `ImagePairLoaderTests` (the fallback to `Linear`), `MainWindowTests` (R113, R114 on the WPF
  thread), and V-005 and V-006 of `docs/validation.json` on the real FITS pair.

## Measurements

On `result_3813s.fit` (5938 × 3888, three planes of 32-bit float) in Release, 2026-10-26, one
process, the first load of each series warm from the second on; private memory is the delta
of one load:

- Linear, this code, the high byte taken by a shift: 0.52, 0.53, 0.54 s (0.96 s the first,
  cold), +88.4 to +90.4 MB. Linear, `main`'s code the same day: 0.56, 0.57, 0.59 s (0.87 s the
  first), +88.6 to +90.6 MB. With the linear render going through the 64 KB table instead of a
  shift, 0.67 to 0.69 s: the table does not fit the first-level cache, and that is why
  `DisplayRenderer.RenderLinear` shifts.
- Histogram: 1.12, 1.13 s, +89.9 to +90.4 MB. False colours: 1.19, 1.19 s, +88.6 to +88.8 MB.
  Two passes over the file, so about twice the linear load; no memory beyond the bitmap.
- Bin 0: of the file's 69 260 832 samples, none is exactly 0, none is negative and none is
  positive below 1/131 070; the difference from Siril's rule is empty on this file.
- In the application, on `result_3813s.fit` against `result_3813s_Graxpert_AI.fit`, the status
  bar's process figure read 8 s after each step, the keys posted to the window: with the
  release before the render pass, 393 MB after the open, 518 MB after three zoom steps to
  50 %, then 870 MB after `H` and 920 MB after `F` and `F`; the same window never shown, on
  the WPF test thread, read +330 MB after the open and +311 to +331 MB after each switch, so
  nothing was retained by the code and everything by the render content. With the release
  after one render pass: 392 MB after the open, 529 MB zoomed, then 617, 567 and 657 MB
  after the three switches, the zoom plateau R-080 describes plus the render thread's caches
  at the new scale.
