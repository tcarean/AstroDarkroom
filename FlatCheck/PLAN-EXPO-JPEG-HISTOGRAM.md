# Plan: a second set of histograms, from the camera's own preview (window app)

Read `CLAUDE.md` first. Everything below obeys it. The dependency rule and the licence
analysis in Part 0 are **already applied** to `CLAUDE.md`, `THIRD-PARTY-NOTICES.md` and
`README.md`; they are restated here so the plan stands on its own. Everything else still
holds: nothing is computed or worded outside `FlatCheck.Core`, no version bump, no commit,
GPL header on every new file, user-facing text in Romanian, code documentation in English.

Builds on `PLAN-EXPO-HISTOGRAM.md`, which is implemented.

## Review amendments

Reviewed against the code and the libraw/ImageSharp APIs by reflection and source, not
against the plan's own text. Everything below is folded into the sections it concerns; this
list is the changelog.

1. **The headline numbers mixed two windows.** "raw 29 % -> ecran 80 %" paired the raw
   *centre* median with the JPEG *whole-frame* median. Re-measured on matching windows:
   centre-to-centre it is **29.0 -> 81.2** on `DSC_0091` and **32.1 -> 98.0** on `DSC_0061`.
   The comparison table must pair like with like, and the rule is now written down (1.8).
2. **Pairing keyed off `r.Cfa` breaks on a raw judged `!IsCfa`.** A raw under very neutral
   light can have a CFA spread under 1.15, which makes `Channels` a single "mono" entry while
   `Cfa` is still non-null. Pair on `Channels[i].Colour`, never on `Cfa` (1.8).
3. **ImageSharp can silently transform pixels through an embedded ICC profile.**
   `DecoderOptions.ColorProfileHandling` has a `Convert` member; a converted preview is no
   longer what the screen showed. Set `Preserve` explicitly, do not rely on the default (1.3).
4. **Select the preview from metadata, while the file is already open.** `LibRawData`
   carries `ThumbnailList` (`ThumbCount`, and per item `Format`, `Width`, `Height`, `Flip`,
   `Length`) and `RawReader.Decode` already marshals that struct. So the selection rule runs
   without unpacking anything, the chosen preview's bytes are captured during `Load`, and
   the file is not reopened. `TryReadPreview(path)` and `ImageLoader.LoadPreview` are gone;
   what stays opt-in is the *decode* (1.4, 1.5, 1.7).
5. **`ProcessedImage` reports `Bits = 0` for JPEG entries.** A generic `Bits == 8` check
   would reject every JPEG preview; the check belongs to the bitmap path only, and
   `Channels == 3` joins it (1.4).
6. **`ChannelNote` on a preview channel would say "decide expunerea".** Preview rows get no
   note and no brightest-emphasis; nothing on that side decides (2.2).
7. **`ScreenReading` overclaimed.** "Peaks near raw 25 %" is one camera and one Picture
   Control; "always in that direction" is not a law but a consequence of two conditions.
   Reworded to mechanism plus measurement (1.8).
8. **ImageSharp encodes too.** The muddled decoder test becomes a real round trip, which also
   gives the WPF tests a preview without a NEF; the "seam" the plan hedged about is one
   `internal` modifier, since `InternalsVisibleTo` is already there (3.2, 3.4).
9. **Stale steps.** The rule change and the licence write-up are already in the repo; the
   plan no longer lists them as work (Part 4, Part 5).
10. **The 0.4 timings are StbImageSharp's.** Re-measure with ImageSharp in step 1 and put
    the number in the code comment (0.4, Part 5).

## Why

The Exposure tab measures raw levels. The camera's screen shows something else, and the gap
is large enough to make people act on it. Measured on real files from this library:

| fișier | raw B, centru 20 % | pe ecran B, același centru 20 % | vârf pe ecran |
|---|---|---|---|
| `Nikkor 35 DX/f2.8-sky/ISO800/DSC_0091.NEF` | 29.0 % | 81.2 % | 84.7 % |
| `Samyang/ISO1600/f2.0-sky/DSC_0061.NEF` | 32.1 % | **98.0 %** | **100.0 %** |

Same window on both sides, deliberately - a raw centre against a JPEG whole-frame median
looks similar and is not the same measurement (amendment 1).

The second one is the whole argument. On the camera that flat had blue pixels sitting at
255, the reading that says "you are burning it". In the file it is at 32 %, inside the
sky-flat target band, with 1.6 stops of headroom and **zero** saturated pixels. Anyone
trusting the screen there would pull the exposure back and underexpose a correct set.

The tool already knows the raw number. The camera's number is in the same file. Putting the
two side by side turns a recurring confusion into something the tool settles once.

**The gap is not a constant to memorise.** On this camera and Picture Control it peaks near
raw 25 % and shrinks upward, because the transfer curve is compressive; the peak's exact
place will differ elsewhere, its existence will not. Measured on a D3300 frame (Standard
Picture Control, G channel) spanning the full range:

| raw | ecran | diferență |
|---|---|---|
| 3 % | 25 % | 22 |
| 12 % | 59 % | 47 |
| **25 %** | **80 %** | **55** |
| 50 % | 92 % | 42 |
| 70 % | 99 % | 29 |
| 90 % | 99.6 % | 10 |

A flat lives at the top of that hump. That is exactly why flats look alarming on screen and
are not, and it is why a fixed "add 50 points" rule of thumb would be wrong everywhere else.

## Part 0 - The dependency rule, and the dependency

### 0.1 The rule, already changed

`CLAUDE.md` used to say the project had **exactly one** NuGet dependency and that a second
needed the same standard of evidence `Sdcb.LibRaw` met. That was not the author's position.
It now reads, in "The dependencies":

> **Dependencies are discussed before they are added, not avoided.** The tool still leans
> toward hand-rolled where hand-rolled is honest - the FITS reader and the PNG encoder stay
> as they are. But a package is a normal thing to reach for when it does a real job better
> than we would. Bring the candidate, its licence, its size and its alternatives to the
> author, and let them decide. Do not add one silently.

Two constraints do **not** bend, and are stated in the same section:

- **Licence compatibility is not negotiable.** The tool is GPLv3-or-later, so anything
  linked in has to be GPLv3-compatible, and the reason it is has to be written down.
  `THIRD-PARTY-NOTICES.md` is the file that records those decisions - it already covers the
  two live ones, LibRaw's LGPL-vs-CDDL choice and ImageSharp's conditional grant.
- **The single-copyable-binary promise.** Pure-managed packages keep `PublishSingleFile`
  simple. Per-RID native binaries cost the same care `Sdcb.LibRaw` already needs, so it is a
  point against, not a veto.

**A correction worth keeping visible**, because an earlier draft of this plan got it wrong
and could have cost the author a package for no reason: the Six Labors Split License is
**not** a blanket restriction. It is a conditional dual licence, and a GPLv3 project
qualifies for its Apache-2.0 branch with nothing to change. The detail is in 0.3.

### 0.2 Why a JPEG decoder is the honest route

The camera's rendering exists in exactly one place: the preview image embedded in the raw
file. There is no way to reconstruct it from the sensor data, and pretending otherwise
would be worse than not doing the feature:

| alternative | why it is rejected |
|---|---|
| libraw's `dcraw_process()` | develops the raw with *libraw's* white balance, gamma and curve. Close to a camera render, but not this camera's, and the whole point is to show what the screen showed. Confidently wrong is the one outcome to avoid. |
| libraw's already-decoded 160x120 thumbnail | works, and costs nothing, but it is an incidental artefact: libraw happens to hand that one index back as a bitmap. It is not what the screen showed, its availability is camera-specific, and 160x120 is a coarse basis for a claim. **Rejected on the author's instruction, correctly.** |
| a hand-rolled JPEG decoder | Huffman, DCT, chroma subsampling, YCbCr, progressive mode. An order of magnitude beyond the PNG encoder, which only writes. Not honest work for this. |

So: extract the real embedded preview with libraw, decode it with a real JPEG decoder.

### 0.3 The package, and the evidence

**Chosen by the author: `SixLabors.ImageSharp` 4.1.1.**

| criterion | value |
|---|---|
| licence | Six Labors Split License 1.0 - a *conditional dual* licence that grants **Apache-2.0** terms to software "licensed under an Open Source or Source Available license". FlatCheck is GPLv3, so it qualifies with nothing to change, and Apache-2.0 flows into GPLv3 one-directionally. Written up in `THIRD-PARTY-NOTICES.md`. |
| dependencies | one, `System.IO.Hashing` (>= 8.0.0), which is a Microsoft BCL-adjacent package |
| target | `net8.0` and later |
| kind | pure managed. No native binaries, so `PublishSingleFile` is untouched. |
| maintenance | actively developed, by a team |
| why this one | robustness on the awkward cases a camera preview can genuinely be - progressive JPEG, CMYK/YCCK, unusual chroma subsampling - plus scaled decoding as a first-class feature |

**Pin the version.** The licence text is "Version 1.0, June 2022" and does not say which
licence version governs which release; the grant travels with the version taken. Pinning is
legal hygiene here as well as reproducibility.

**The alternative, measured and kept on the shelf.** `StbImageSharp` (Unlicense OR MIT, zero
transitive dependencies) was tested against the real previews in this library and produced
medians identical to an independent decoder (Python/PIL) on all three channels:

| canal | StbImageSharp | PIL | diferență |
|---|---|---|---|
| R | 64.7 % | 64.7 % | 0 |
| G | 69.0 % | 69.0 % | 0 |
| B | 80.0 % | 80.0 % | 0 |

It carries no licence footnote at all. If the Six Labors licence ever moves, this is the
drop-in replacement, and the seam is one file wide (`PreviewReader.cs`). Keep that sentence
in the code, not only here.

Also rejected, for the record: **System.Drawing.Common** (Windows-only on .NET 8, already
rejected once in this repo for the PNG decoder in the tests) and **SkiaSharp / Magick.NET**
(per-RID native binaries, tens of megabytes, for one decode).

### 0.4 Which preview to decode, and what it costs

libraw exposes several previews per file. Measured on the D3300:

| index | ce e | timp | memorie | median R / G / B |
|---|---|---|---|---|
| 0 | Bitmap 160x120 | - | - | 63.1 / 68.6 / 78.0 |
| 1 | JPEG 6000x4000 | 555 ms | 68 MB | 64.7 / 69.0 / 80.0 |
| 2 | JPEG 1620x1080 | 46 ms | 5 MB | 64.3 / 69.0 / 79.6 |
| 3 | JPEG 640x424 | 8 ms | 0.8 MB | 64.3 / 69.4 / 79.6 |

The 1620x1080 preview agrees with the full-size one to **0.4 points**, at a twelfth of the
time and a thirteenth of the memory. The timings were taken with StbImageSharp; the ratio is
what matters, and the absolute numbers get re-measured with ImageSharp in step 1. Against a raw-to-screen gap of 51 points, that is free
accuracy.

**Selection rule**, in order. Indices are not stable across cameras, so the rule runs over
`LibRawData.ThumbnailList` - `ThumbCount` entries, each with `Format`, `Width`, `Height`,
`Flip` and `Length` - which is metadata already marshalled in `RawReader.Decode`. Nothing is
unpacked to make the choice; only the winner is (amendment 4). Asking for an index past
`ThumbCount` throws `LibRawException` code -9, so the count is the loop bound, not a
try/catch.

1. the largest JPEG preview whose long side is at most 3000 px;
2. failing that, the largest JPEG preview of any size, decoded with
   `DecoderOptions.TargetSize` set to bring the long side near 2000 px - ImageSharp scales
   during decode rather than after, so this costs a fraction of a full decode;
3. failing that, an already-decoded `Bitmap` preview, whatever its size;
4. failing that, `null` - the file carries no preview, and the switch stays off.

Rule 3 is what keeps the 160x120 path alive as a *fallback* rather than as the design.
Rule 2's `TargetSize` is the concrete payoff of choosing ImageSharp over the alternative,
so it should not be skipped as an optimisation for later.

## Decisions already taken (do not reopen)

| decision | value | why |
|---|---|---|
| where the pixels come from | libraw extracts, ImageSharp decodes | see Part 0 |
| where the histogram is computed | `FlatCheck.Core`, the existing `LevelHistogram` | nothing is computed outside Core; the shared type means both sides are binned identically and are therefore comparable |
| axis | 0..100 % of 255, same 200 bins | the raw side is % of `(white - black)`. Both are "% of the available range", the only basis on which the two can be read against each other. |
| **the preview side has no verdict and no bands** | `TooLow`/`BandLo`/`BandHi`/`TooHigh` all `NaN` | **the most important decision here.** Two independent reasons, see "No target on the preview axis" below. `HistogramCanvas` already skips `NaN` zones, so this needs no drawing code at all. |
| the clipping strip stays on the preview view | 98-100 % | honest there: the JPEG really is pinned near 255, and on `DSC_0061` it genuinely is. The caption must say it means "aici se satureaza ecranul, nu senzorul". |
| channels | 3 (R, G, B) | the preview is debayered. No mosaic, so no `CfaPattern`, no `G1`/`G2`. |
| window | the same central 20 % as the raw side, plus the whole frame | comparability |
| the *bytes* are captured on every raw load, the *decode* is **opt-in** | `LoadedFrame.Preview` (compressed, a few MB at most); `ExposureRun.Execute(..., withPreview)` decodes | capturing is a memcpy while the file is already open; decoding costs tens to hundreds of ms and only the window shows the result. The console never decodes; the structure/light/ratio modes never decode. |
| the switch | two radio chips, same idiom as `MapScaleViewModel` | the established pattern for "which view of the same thing" |
| chip selection | survives `Reset()` | a viewing preference, like the map scale chips |
| the comparison table | visible on **both** views whenever a preview exists | it is the payoff; hiding it behind one view hides the point |
| no preview, or a decode failure | chips disabled, raw view forced, one sentence saying why | not an error. The mode works exactly as it does today. |
| console | **unchanged** | same reasoning as the raw histograms. Goldens and `crosscheck.py` stay untouched, and `flatcheck.py` needs no mirror. |
| `--expo` verdict, thresholds, exit codes | **untouched** | nothing here measures the flat |

## No target on the preview axis

The question comes up naturally - the raw chart has a target band, so why not the preview
chart? Two reasons, and they are independent, so neither has to carry the argument alone.

**1. It would teach the wrong thing.** A band on the preview axis means "expose until the
*screen* reads here". That is aiming with the very instrument this feature exists to
discredit. The tab has one figure that decides and it lives on the raw side; a second target
on the preview side would be a second source of truth, and the false one. This reason holds
even if the number could be computed perfectly.

**2. It cannot be computed anyway - measured, not assumed.** Mapping raw 50-65 % onto the
screen axis needs the transfer curve, and a flat does not contain it. Taking a frame with
full tonal range, keeping only pixels inside a flat-like window, fitting a power law and
extrapolating into the target zone:

| interval used for the fit | predicted at raw 57 % | measured | error |
|---|---|---|---|
| 23-31 % (what a flat offers) | 90.5 % | 95.7 % | -5.1 pt |
| 20-35 % | **100.9 %** | 95.7 % | +5.2 pt |
| 10-60 % | 98.2 % | 95.7 % | +2.5 pt |

The second fit predicts a physically impossible value, and moving the fitting window by a
few points flips the sign of the error. That is an ill-posed extrapolation, not a
calibration problem: the real curve saturates asymptotically at 100 and no power law does,
so the model is wrong by construction. Same failure mode as extrapolating `radPct` to zero
sky gradient in the light series, which `CLAUDE.md` already records as tried and rejected.

And the curve is not a per-camera constant to be shipped as a table either. It moves with
the camera model, the Picture Control and its adjustments, the white balance - which is
**per channel**, so one raw target would land in three different places on the three charts
(on `DSC_0091.NEF`, R is multiplied by 2.355 and B by 1.086) - the colour matrix, which
mixes channels, and Active D-Lighting, which makes it scene-dependent.

**What is shown instead**, all of it measured on the file in hand rather than modelled:

- the comparison table, `raw 29.0 % -> ecran 81.2 %` per channel, centre against centre;
- the median marker, which really is the preview's own central median;
- the clipping strip at 98-100 %, the one reference that is camera-independent because 255
  is 255, captioned so it reads as "here the screen saturates, not the sensor";
- `ScreenReading`, which states the usable rule: the screen saturates before the sensor
  does, always in that direction, so "not touching the edge" is a guarantee and "touching
  the edge" is not information.

## Part 1 - Core

### 1.1 `FlatCheck.Core/FlatCheck.Core.csproj`

```xml
<PackageReference Include="SixLabors.ImageSharp" Version="4.1.1" />
```

Update the comment block above the existing `Sdcb.LibRaw` reference: it currently argues
that this is the one and only dependency. Rewrite it to say what each package is for and
why it was chosen, per the new rule.

### 1.2 `FlatCheck.Core/PreviewImage.cs` (new file, GPL header)

The camera's own rendering, as plain pixels. A Core type with nothing third-party in it, so
nothing downstream knows where it came from - the same discipline as `Img`.

```csharp
/// <summary>
/// Previzualizarea developata de APARAT, adica exact ce a stat pe ecran: debayerizata, cu
/// balans de alb, gama si curba de ton din Picture Control. NU e o masuratoare a
/// senzorului si nu are voie sa decida nimic. Exista ca sa poata fi pusa langa nivelul
/// real, fiindca diferenta dintre ele e de zeci de puncte si induce in eroare pe toata lumea.
/// </summary>
internal sealed class PreviewImage
{
    internal readonly int H, W;
    internal readonly byte[] Rgb;     // H*W*3, intretesut
    internal readonly string Source;  // "previzualizare 1620x1080 din fisier" - pt raport
}
```

Beside it, in the same file, the *undecoded* form that travels on `LoadedFrame`:

```csharp
/// <summary>
/// Previzualizarea asa cum sta in fisier: ori octeti JPEG, ori un bitmap RGB pe care
/// libraw l-a decodat deja. Se captureaza cat timp fisierul e deschis si se decodeaza
/// numai la cerere - vezi ExposureRun.
/// </summary>
internal sealed class EmbeddedPreview
{
    internal readonly bool IsJpeg;
    internal readonly byte[] Data;     // JPEG comprimat, sau RGB intretesut
    internal readonly int W, H;        // din metadate; pentru JPEG, inainte de decodare
    internal readonly int Flip;        // orientarea din libraw; informativ, vezi 1.6
    internal readonly string Source;   // "previzualizare JPEG 1620x1080 din fisier"
}
```

### 1.3 `FlatCheck.Core/PreviewReader.cs` (new file, GPL header)

The only file that names `SixLabors.ImageSharp`, exactly as `RawReader.cs` is the only file
that names `Sdcb.LibRaw`. Same containment, same reason - and here it doubles as the swap
point if the licence ever moves, so say so in the header comment.

```csharp
/// <summary>
/// Previzualizarea ca pixeli. Un bitmap trece direct; un JPEG trece prin ImageSharp.
/// Null la orice esec - o previzualizare care nu se decodeaza nu e un motiv sa pice
/// rularea, inseamna doar ca comutatorul ramane stins.
/// </summary>
internal static PreviewImage Decode(EmbeddedPreview p)
```

Four things the decode has to get right, verified against ImageSharp's source rather than
its docs:

- **`ColorProfileHandling = Preserve`, set explicitly.** The enum has a `Convert` member
  that transforms pixels through an embedded ICC profile to sRGB. A converted preview is a
  different image from the one on the camera's screen, and the difference would not show up
  in any test. Do not rely on the default (amendment 3). `SkipMetadata = true` as well - EXIF
  is of no use here and skipping it is cheaper.
- **`DecoderOptions.TargetSize`** exists (`Size?`, "scaling should use an operation
  equivalent to `ResizeMode.Max`") and the JPEG decoder feeds it to its spectral converter,
  i.e. it scales during IDCT rather than after. Use it only on rule 2 of 0.4 (a lone
  oversized preview); on rule 1 decode at native size, so no resampling touches the pixels.
  A resample narrows a distribution slightly; medians on a smooth flat are unaffected, and
  the caveat is the same as using the 1620 px preview at all.
- **Decode into `Rgb24`** - `Image.Load<Rgb24>(options, bytes)` - so CMYK/YCCK previews
  arrive as RGB and the `PreviewImage` layout is one thing.
- **The catch list is the one `RawReader.Load` uses**, plus ImageSharp's own
  `UnknownImageFormatException`/`InvalidImageContentException`: `DllNotFoundException`,
  `FileNotFoundException`, `BadImageFormatException`, `TypeInitializationException`. A
  publish that somehow lacks `SixLabors.ImageSharp.dll` must degrade to "no preview", not
  crash the tab.

No ImageSharp-typed field on `PreviewReader`, for the same reason as `RawReader`; pinned by
`HoldsNoImageSharpTypedFields` (3.2).

### 1.4 `RawReader.cs`

Inside `Decode`, after the pixels are copied, while `ctx` and the marshalled `d` are still
in scope (amendment 4):

```csharp
// Alegerea se face din metadate - d.ThumbnailList - fara sa se despacheteze nimic.
// Se despacheteaza numai castigatorul, si numai octetii lui se copiaza.
EmbeddedPreview preview = TryCapturePreview(ctx, d.ThumbnailList);
```

```csharp
/// <summary>
/// Cea mai potrivita previzualizare, dupa regula din plan, sau null. Se alege din lista
/// de metadate (ThumbCount, Width, Height, Format), deci fara niciun cost pe ce nu se
/// alege. Niciodata nu arunca: o previzualizare lipsa sau ciudata lasa comutatorul
/// stins, nu strica citirea raw-ului.
/// </summary>
static EmbeddedPreview TryCapturePreview(RawContext ctx, LibRawThumbnailList list)
```

Only JPEG items (`InternalThumbnailFormat.Jpeg`) are candidates for rules 1 and 2; anything
else that `ExportThumbnail` returns as `Bitmap` is the rule-3 fallback. `Flip` is carried
along, informatively.

Four traps, all load-bearing:

- **`ProcessedImage` is `IDisposable` and wraps native memory.** Copy the bytes out inside
  the `using`; never let a span escape.
- **JPEG entries report `Bits = 0`** (measured), so `Bits == 8` is a check for the bitmap
  path only. There, require `Bits == 8`, `Channels == 3` and `DataSize == W*H*3`
  (amendment 5). Anything else falls through to the next candidate, it is not reinterpreted.
- **Everything in `TryCapturePreview` is inside a try/catch returning null.** `Load` has
  worked for months without a preview; a thumbnail oddity on some camera must not start
  failing it.
- **No new field on `RawReader`, of any libraw type.**
  `HoldsNoLibrawTypedFields_SoTheAssemblyStaysLazilyLoaded` must keep passing untouched.

The cost added to every raw load - including the structure, ratio and light modes, which
never look at it - is one `unpack_thumb` of the chosen entry (a read at a known offset) and
one memcpy of at most a few MB. Next to the 190 MB `Img` of a 24 MP frame it does not
register, and it is what lets the file be opened once.

### 1.5 `ImageLoader.cs` - `LoadedFrame`

```csharp
/// <summary>Previzualizarea inglobata, nedecodata, sau null cand formatul n-o poarta (FITS, mereu).</summary>
internal EmbeddedPreview Preview;
```

Fourth field of the family `Scale` / `Cfa` started: null is the right answer for a format
that does not carry one. `ImageLoader` itself does not change - the dispatch stays in one
place because nothing new needs dispatching (amendment 4).

### 1.6 `Analysis/ExposureAnalyzer.cs`

```csharp
/// <summary>Nivelurile din previzualizarea aparatului. Trei canale, fara verdict.</summary>
internal sealed class PreviewLevels
{
    internal ChannelLevels[] Channels;   // exact 3: R, G, B
    internal string Source;
}

/// <summary>
/// Histogramele previzualizarii, pe axa 0..100 % din 255. NU primeste praguri si NU
/// produce verdict: ce se vede aici a trecut prin balans de alb, gama si curba de ton,
/// deci nu mai are nicio legatura cu cata lumina a cazut pe senzor.
/// </summary>
internal static PreviewLevels AnalyzePreview(PreviewImage p)
```

Reuses `ChannelLevels` and `LevelHistogram` unchanged. `ExposureResult` gains
`internal PreviewLevels Preview;` and `Analyze` a trailing optional
`PreviewImage preview = null`. **Every existing figure stays untouched.** Additive only, and
the golden files are what prove it.

The central 20 % window is taken on the preview exactly as on the raw: rows `0.4H..0.6H`,
columns `0.4W..0.6W`. That window is invariant to the two orientation cases a preview can
be in - a 180° flip maps it onto itself, and a 90° one swaps `W` and `H` but the fractions
do not care - so `Flip` is recorded and not acted on. The preview is also a few pixels
narrower than the raw's visible area (6000 vs 6016 on the D3300); at 20 % of the frame that
is noise. Both facts go in the comment above the window, because both look like bugs to a
reader who has not measured them.

`PreviewImage` is transient: `Analyze` consumes it, `ExposureResult` keeps only the
`PreviewLevels` (six histograms of 200 ints). Nothing of the decoded raster survives the
call.

### 1.7 `Runs/ExposureRun.cs`

```csharp
internal static RunOutcome<ExposureResult> Execute(
    string path, double? sat, double? black, bool sky, TextWriter report,
    bool withPreview = false)
```

`withPreview` defaults to false, so the console and every existing caller are unaffected.
When true and `f.Preview != null`, decode with `PreviewReader.Decode(f.Preview)` and pass the
result on; a null from the decoder is the same as no preview.

### 1.8 `Report/ExposureReport.cs`

Nothing new is printed. Three new values, window-only, beside the three that already exist:

```csharp
/// <summary>Perechea raw -> ecran pe fiecare canal, pentru tabelul de comparatie.</summary>
internal static IReadOnlyList<(string Channel, string Raw, string Screen, string Gap)> ScreenComparison(ExposureResult r)

/// <summary>De ce difera atat de mult, si la ce mai e bun ecranul. Patru propozitii.</summary>
internal static string ScreenReading(ExposureResult r)

/// <summary>De ce nu se poate arata comparatia pe fisierul asta.</summary>
internal static string NoPreviewReason(ExposureResult r)
```

`ScreenComparison` pairs **by colour, not by index**, and reads the colour off
`r.Channels[i].Colour` - **never off `r.Cfa`** (amendment 2): a raw judged `!IsCfa` has a
single "mono" channel with no colour while `Cfa` may still be set, and indexing by the
pattern there would pair the wrong things or throw. Rules:

- every raw channel with a colour pairs with the preview channel of that colour; the two
  greens are averaged and the row is labelled `G (media G1, G2)`;
- when no raw channel has a colour - unknown order, or the mono case - one row pairs the
  brightest raw channel against the brightest preview channel and says so in `Channel`.

**The figures are centre-window medians on both sides**, `CentreMedianPct` against
`CentreMedianPct` (amendment 1). The whole-frame medians are not compared: the raw one is
pulled down by vignetting in a way the preview's is not, so that pairing would report a gap
that is partly the lens.

`ScreenReading` says, in order, and only what the result supports:

1. the gap on the deciding channel, in points, centre against centre;
2. that gamma is most of it and cannot be switched off, because a JPEG is gamma-encoded by
   definition - Neutral Picture Control and minimum contrast remove the *extra* curve, not
   the encoding;
3. where a flat sits on the curve: in the lower-middle range, where a compressive curve
   stretches levels the most, so a flat is the least favourable case there is. **Not** "the
   gap peaks at 25 %": that figure is one camera and one Picture Control (amendment 7);
4. what the screen is still good for. The JPEG saturates *before* the sensor whenever two
   things hold - the white balance multiplies the channel by 1 or more, and the tone curve
   reaches 255 - and on every file measured here it did, by a third of a stop to a full stop.
   So "not touching the edge" is a strong guarantee, while "touching the edge" carries no
   information, because from there it is pinned. Phrase it as that mechanism plus "on the
   cameras measured", not as a law.

Point 4 is the one to get right. The tool must not leave the reader with "the camera lies,
ignore it". It leaves them with "it is a one-sided alarm, not a meter".

**The `!IsRaw` case.** A raw that trips the sanity gate can only do so through
`LooksDebayered` (spread under 1.02), which also makes it `!IsCfa`. The preview still exists
and the switch stays enabled; `ScreenReading` opens with the sentence the raw reading
already uses for that case - the file is not a raw flat, so neither curve is an exposure -
and then the comparison, which is still true of the two renderings.

## Part 2 - Window

### 2.1 `ViewModels/HistogramSourceViewModel.cs` (new file, GPL header)

Modelled on `MapScaleViewModel`: translates a choice, decides nothing.

```csharp
public sealed class HistogramSourceViewModel : ObservableObject
{
    public IReadOnlyList<HistogramSourceOption> Options { get; }  // exact doua
    public HistogramSourceOption Selected { get; set; }
    public bool IsPreviewSelected { get; }
    public bool IsEnabled { get; set; }      // false cand fisierul n-are previzualizare
    public event Action Changed;
}
```

Labels name what the thing is, not its file format:

- `"Date brute (ce masoara unealta)"`
- `"Ce a aratat aparatul pe ecran"`

`IsEnabled = false` forces the selection back to raw.

### 2.2 `ViewModels/ExposureTabViewModel.cs`

- `public HistogramSourceViewModel Source { get; }`, subscribed in the constructor.
- `RecomputeAsync` calls `ExposureRun.Execute(..., withPreview: true)`.
- **Keep the last `ExposureResult` in a field**, so flipping the switch rebuilds rows without
  re-running anything. Re-reading a 24 MP NEF to redraw a chart would be absurd.
- `FillHistograms` splits into `RawRows(r)` (today's behaviour) and `PreviewRows(r)`.
  `PreviewRows` sets the four band fields to `double.NaN`, `BandLabel` to null, **`Note` to
  `""` and `IsBrightest` to `false`** - `ExposureReport.ChannelNote` is for raw channels and
  would say "decide expunerea" about a curve that decides nothing (amendment 6). The
  clipping share still shows, because a preview pinned at 255 is the very thing worth
  seeing. **No change to `HistogramCanvas`** - it already skips `NaN`. Worth a comment
  saying so, because it is exactly the kind of thing someone later "fixes" by adding a flag.
- The `Source.Changed` handler rebuilds from the retained result and must tolerate its
  absence: before the first run, and after `Reset()`, there is nothing to rebuild.
- Flipping "Sky" re-runs the analysis and therefore re-decodes the preview. That is tens of
  milliseconds on a mid-size preview and is not worth a cache keyed on path; say so in a
  comment so nobody adds one.
- `public IReadOnlyList<ScreenPairRow> Comparison`, from `ExposureReport.ScreenComparison`.
- `public string SourceNote` - `ScreenReading` on the preview view, `NoPreviewReason` when
  there is none, `""` otherwise.
- The existing caption must switch with the source: the raw one talks about the target band,
  which is not drawn on the preview view.
- `Reset()` clears rows and comparison; `Source.Selected` survives.

### 2.3 `ViewModels/Rows.cs` - `ScreenPairRow`

```csharp
/// <summary>Un rand din tabelul de comparatie: acelasi canal, cele doua cifre.</summary>
public sealed class ScreenPairRow
{
    public string Channel { get; init; }   // "R" / "G" / "B", sau "canalul cel mai luminos"
    public string Raw { get; init; }       // "29.0 %"
    public string Screen { get; init; }    // "80.0 %"
    public string Gap { get; init; }       // "+51 puncte"
    public Brush Colour { get; init; }
}
```

### 2.4 `Theme/Dark.xaml` - `ScreenPairRowTemplate`

Colour chip, raw figure, an arrow glyph, screen figure, then the gap in `Faint`. The `Number`
style everywhere so the columns line up.

### 2.5 `Views/ExposureView.xaml`

Chips above the charts, inside the histogram card. **`GroupName` must be unique across the
window** (`ExposureHistogramSource`), like `StructureMapScale` / `RatioMapScale`: WPF groups
radio buttons by name across the whole window, and a shared name makes one tab's chips
uncheck another's. That bug has been paid for once in this codebase already.

Below the charts: the comparison table, then `SourceNote`, then the caption and the guide.

### 2.6 `Session/SessionMessages.cs`

Two more rows in `HistogramGuideRows()`, and a sentence in the intro about the switch:

| row | meaning |
|---|---|
| comutatorul de sus | which of the two you are looking at; only the raw view has a target band, because it is the only one that says anything about light on the sensor |
| de ce ecranul e mult mai la dreapta | gamma plus the tone curve, which no camera setting removes; a compressive curve stretches the lower-middle levels the most, which is exactly where a flat lives; and on every camera measured here the screen saturated before the sensor, so it is a one-sided alarm, not a meter. Stated as observation and mechanism, not as a law (amendment 7). |

Mirrored by hand in the README, like the rest of the guide. No test binds the two.

## Part 3 - Tests

### 3.1 `FlatCheck.Tests/PreviewLevelsTests.cs` (new)

`AnalyzePreview` is pure and takes a `PreviewImage`, so it is fully testable with synthetic
rasters and no raw file.

- `EveryChannelCountsEveryPixel` - totals equal `W*H` and the centre window count.
- `AConstantPreviewLandsInOneBin`.
- `TheBrightestChannelIsMarked`.
- `PixelsAt255AreCountedAsClippingAndLandInTheLastBin`.
- `TheCentreWindowIsTheSameFractionAsTheRawSide` - guards the comparability claim.
- `ThePreviewNeverChangesTheVerdict` - identical `Verdict`, `CentreMedianPct` and
  `CentreClipPct` with and without a preview attached to the same pixels. **The test that
  matters most**: it is the mechanical form of "the preview side never votes".

### 3.2 `FlatCheck.Tests/PreviewReaderTests.cs` (new)

ImageSharp encodes as well as decodes, so the decoder seam gets a real round trip with no
raw file involved (amendment 8). A test helper `TestImages.JpegOf(w, h, pixel)` builds an
`Image<Rgb24>`, runs `JpegEncoder` at quality 95, and returns the bytes wrapped as an
`EmbeddedPreview`. The same helper is what the WPF tests use in 3.4.

- `ARoundTrippedFlatComesBackAtItsLevel` - a constant 128/160/200 RGB field decodes to
  per-channel medians within 1/255 of the input, and `W`/`H` match.
- `AGradientKeepsItsOrder` - the decoded centre median sits between the decoded corner
  medians, which is the property `AnalyzePreview` leans on.
- `ABitmapPreviewPassesThroughUntouched` - `IsJpeg = false` bytes come back byte-identical,
  no ImageSharp involved.
- `DecodeRefusesGarbageInsteadOfThrowing` - random bytes return `null`; so does an empty
  buffer; so does a truncated JPEG (cut the round-tripped bytes in half).
- `HoldsNoImageSharpTypedFields` - the mirror of the libraw test, on `PreviewReader`.

### 3.3 `FlatCheck.Tests/RawReaderTests.cs`

Extend the `FLATCHECK_NEF`-gated tests, which is where a real JPEG is actually available:

```csharp
LoadedFrame f = ImageLoader.Load(path);
Assert.NotNull(f.Preview);                      // captured from metadata, no reopen
Assert.True(f.Preview.W <= 3000, "rule 1 should have picked the mid-size entry on the D3300");

PreviewImage p = PreviewReader.Decode(f.Preview);
Assert.NotNull(p);
Assert.True(p.W >= 640 && p.H >= 424, $"{p.W}x{p.H}");
Assert.Equal(p.W * p.H * 3, p.Rgb.Length);
```

Plus a gated test asserting the thing that motivated the feature: the preview's brightest
median sits well above the raw's. Record the two measured pairs in the comment
(29 -> 80, and 32 -> 95).

`HoldsNoLibrawTypedFields` must still pass unchanged.

### 3.4 `FlatCheck.Wpf.Tests`

`SessionFixture` writes FITS, so it covers the **absent-preview** case naturally. The present
case does not need a NEF either: `FlatCheck.Wpf` already has `InternalsVisibleTo` for its
tests, so make `ExposureTabViewModel.Populate` `internal`, build an `ExposureResult` with
`ExposureAnalyzer.Analyze(img, ..., preview: PreviewReader.Decode(TestImages.JpegOf(...)))`,
and hand it in (amendment 8). No new seam, one keyword.

- `WithoutAPreviewTheSwitchIsDisabled` - `Source.IsEnabled` false, raw selected, and
  `SourceNote` explains why.
- `TheSwitchDoesNotRerunTheAnalysis` - flip it, assert `Centre` and `Verdict` unchanged.
- `ThePreviewRowsCarryNoBands` - every band field `NaN`, `BandLabel` null.
- `ResetKeepsTheChosenSource`.
- `TheComparisonPairsCentreWithCentre` - with a preview whose centre is brighter than its
  corners, the `Raw`/`Screen` strings equal the two `CentreMedianPct` figures formatted, not
  the whole-frame ones.
- `AMonoRawGetsOneComparisonRow` - `!IsCfa` result plus a preview: one row, labelled as the
  brightest-vs-brightest fallback, no exception.
- `WindowSmokeTests` - render with the preview view selected (via the same `Populate` seam)
  and with the switch disabled. Two distinct paths through the template.

### 3.5 `LicenseHeaderTests`

Picks up the new files automatically. Nothing to do beyond writing the headers.

### 3.6 Golden and crosscheck

Unchanged, both. That is the check that the console really was left alone. If
`FLATCHECK_UPDATE_GOLDEN` seems necessary, something leaked - stop and find it.

## Part 4 - Documentation

**`CLAUDE.md`**

- The dependency rule and the ImageSharp licence analysis are **already in** "The
  dependencies" and "Licensing"; nothing to add there. But that section already names
  `PreviewReader.cs` as if it existed, so the *Structure* list must gain it in the same
  commit or `CLAUDE.md` describes a file that is not there.
- *Structure*: `PreviewImage.cs` (with `EmbeddedPreview`), `PreviewReader.cs`,
  `LoadedFrame.Preview`, `PreviewLevels` / `AnalyzePreview`, `HistogramSourceViewModel`.
- *4. Exposure*: a subsection on the preview - where it comes from, the selection rule and
  why indices are enumerated, the measured 0.4-point agreement between the 1620 and 6000 px
  previews, and **in bold** that the preview side carries no bands and no verdict, with the
  reason.
- *Known issues*: decoding a full-size preview costs ~68 MB transiently on cameras that
  offer no mid-size one.

**`README.md`** - in the Expunere tab section, after the histogram table: what the switch is,
the measured pair from `DSC_0091.NEF`, the shape of the curve, and the one-sided-alarm rule.
Two more rows in the mirrored guide table. A line in the dependencies section if one exists.

## Part 5 - Order of work

1. **`PreviewImage` / `EmbeddedPreview` + `PreviewReader` + the package** - 1.1-1.3, tests
   3.2. Re-measure the decode of the 1620 and 6000 px previews with ImageSharp, with and
   without `TargetSize`, and put the numbers in the `PreviewReader` header comment.
2. **`RawReader.TryCapturePreview` + `LoadedFrame.Preview`** - 1.4, 1.5, tests 3.3. Verify
   the selection rule picks the 1620x1080 entry on the D3300 from metadata alone.
3. **`AnalyzePreview`** - 1.6, 1.7, tests 3.1. Console still prints nothing new; goldens
   prove it.
4. **Report values** - 1.8.
5. **Window** - Part 2, tests 3.4. Then `FLATCHECK_DUMP_SCREENS` and *look at it* on a
   session with real NEFs in `flats/`. The smoke test proves it draws, not that it reads well.
6. **Docs** - Part 4.
7. `dotnet test FlatCheck.sln`, the `FLATCHECK_NEF` run, `crosscheck.py`. Hand over the diff
   and the commit message; do not commit.

## Risks worth naming

- **Progressive JPEG and other awkward variants.** ImageSharp handles them, which is why it
  was chosen; nothing in this library was verified to be one, so this is coverage bought
  rather than a problem solved. The catch-all in `PreviewReader.Decode` stays regardless: a
  file that will not decode disables the switch instead of failing the run.
- **A raw with no preview at all.** Some DNG converters strip them. Handled by the same
  path, and it is worth one sentence to the user rather than a silent empty chart.
- **Memory on a camera with only a full-size preview.** With `TargetSize` on rule 2 this is
  a fraction of the 68 MB a full decode costs; measure it in step 1 and put the real number
  in Known issues rather than this estimate.
- **An embedded ICC profile.** With `ColorProfileHandling.Convert` the decoded pixels would
  differ from the screen's and nothing would flag it. `Preserve` is set explicitly, and the
  round-trip helper in 3.2 should embed a deliberately non-sRGB profile so
  `ARoundTrippedFlatComesBackAtItsLevel` would catch a regression to `Convert`.

## Commit message to hand over (adjust to what was actually done)

```
al doilea set de histograme in tabul de expunere: ce a aratat aparatul

Tabul comuta acum intre nivelurile reale din raw si previzualizarea developata
de aparat - aceeasi din care si-a calculat histograma de pe ecran. Sub ele, un
tabel pune cele doua cifre cap la cap pe fiecare canal. Masurat pe biblioteca:
un flat la 29 % in date arata 81 % pe ecran, iar altul la 32 % arata 98 %, cu
pixeli la 255 - aceeasi fereastra centrala de 20 % pe amandoua partile. A doua citire e exact cea care te face sa scazi expunerea pe o
serie care era corecta.

Previzualizarea se extrage cu libraw si se decodeaza cu SixLabors.ImageSharp,
fixat la 4.1.1. Se alege previzualizarea de sub 3000 px cand exista - pe D3300
aia de 1620x1080 - fiindca difera de cea de 6000x4000 cu 0.4 puncte si costa a
douasprezecea parte din timp; cand nu exista, se decodeaza cea mare direct la
dimensiune redusa, prin DecoderOptions.TargetSize. Indecsii se enumereaza, nu se
presupun: nu sunt stabili intre aparate.

Partea din previzualizare NU are benzi si NU are verdict. Banda tinta desenata
peste curba aia ar spune omului sa expuna pana cand ECRANUL arata 50-65 %,
adica exact greseala pe care functia asta exista s-o repare. HistogramCanvas
sarea deja peste zonele NaN, deci desenul n-a avut nevoie de nicio modificare.

Octetii previzualizarii se aleg din metadatele libraw si se copiaza cat timp
fisierul e oricum deschis; decodarea e optionala (withPreview) fiindca numai
fereastra o arata. Consola si celelalte trei moduri nu decodeaza niciodata.
Profilul ICC se pastreaza, nu se aplica: o previzualizare convertita n-ar mai
fi ce a stat pe ecran.

Regula "exact o singura dependinta" din CLAUDE.md a fost inlocuita cu cea
adevarata: dependintele se discuta inainte, nu se evita. Compatibilitatea de
licenta cu GPLv3 ramane neneogciabila, dar se DEMONSTREAZA, nu se presupune:
Six Labors Split License nu e o restrictie generala, ci o licenta duala
conditionata care acorda termenii Apache 2.0 proiectelor open source, iar
Apache 2.0 intra in GPLv3. Motivul, si alegerea LGPL in locul CDDL pentru
LibRaw, sunt scrise in THIRD-PARTY-NOTICES.md.

Consola nu tipareste nimic nou, deci niciun golden nu se schimba si
flatcheck.py n-are ce oglindi.
```
