# Recommendations and the report

> Moved unchanged on 2026-10-02 (ADR 0001) from `docs/specification.md` §6 and §8. The
> behaviour is stated with IDs in `../requirements.md` (R-090 to R-101); the evidence rule and
> the verdict order are ADR 0011, the invariant formatting ADR 0021. **Covers:**
> `AstroPhotoAnalyzer.Core/Report/`. A change to the code this describes means this note is
> updated in the same task, or deleted with the decision it explains.

## Recommendations (`Report/Recommendations.cs`)

`Recommendation(Topic, Verdict, Text, Because)`; the constructor refuses an empty
`Because`. A `Because` may hold `Fmt.Strong(part)`, `**part**`, the core's one piece of
markup, for a figure the eye must catch; `ReportWriter` and `CurveTexts.EvidenceRows` (the
curves tab's paper checks) write it through `Fmt.Plain`, which strips the marks, and the
window's Raport tab renders them bold (`Emphasis`). Verdicts are monotone `Ok < Attention < Problem`; a section's verdict is its
maximum, the overall verdict the maximum of all. Topics in decision order: Format,
Background, Colour, Size, Denoise, Warnings.

| topic | condition | verdict | text (gist) |
|---|---|---|---|
| Format | source > 8 bits | Ok | send TIFF 8-bit LZW (size); 16 -> 8 is rounding (mean/max) |
| Format | source 8 bits | Ok | send TIFF 8-bit LZW; nothing to convert |
| Format | q100 row exists | Ok, or Attention when not `Acceptable` or > `JpegOverThresholdWarnPct` (5 %) samples over 2 | if only JPEG: q100 4:4:4 (size, errors, where they sit; "in the dark sky" when the mean level of affected samples < 64) |
| Format | plateau known and the share of the frame with a visible step at the reference row's dpi over `PlateauFlatFrameWarnPct` (0.1 %, ADR 0060) | Attention | banding risk: send 16-bit or dither (the share and the threshold in `Fmt.Strong` marks, then the typical plateau and where it was measured) |
| Format | plateau known and the share at or under it | Ok | no visible banding: flat steps on too little of the frame (the same because) |
| Format | plateau unknown and `BandingRisk` | Attention | banding risk from sigma alone |
| Background | the as-is dust at or above paper black and the as-is sky at or above L\* 15 | Ok | print-safe as is; "poti cobori pana la <row>" when the recommended row is lower, or the row just below and what fails there (the dust under paper black, or the sky under L\* 15) when the as-is row is the recommended one |
| Background | the as-is sky under paper black | Problem | the sky and the dust would print the same black; re-stretch to the recommended row (its simulated dust and sky, the exact target), or "no target up to 0.20" with the exact target or "nici 0.60" |
| Background | only the dust under paper black | Attention | the clouds would print pure black; same fix text |
| Background | the dust at or above paper black, the as-is sky under L\* 15 (ADR 0029) | Attention | the paper separates the sky from black, the eye in room light barely; re-stretch to the recommended row (its simulated dust and sky, the target at which the sky reaches L\* 15) |
| Colour | mono | Ok | nothing to measure |
| Colour | `HasCast` | Attention | green/magenta/warm/cool cast (whichever metric is larger); neutralise on linear |
| Colour | no cast | Ok | neutral background |
| Colour | each flagged **coloured** region | Attention | warm (R > B + 3) / cool (B > R + 3) / greenish / other; fix on linear or keep as composition |
| Colour | flagged **neutral** regions | Ok (one note) | most likely the object; check Preview |
| Size | stars `Insufficient` | Attention | no real resolution: the pixel-grid rule (physical floor from the given distance, else the 180 dpi convention), how far the file reaches or that the smallest width misses it; a file limit, not a detail one; no size recommended; asks for a distance when none |
| Size | explicit distance, recommended row passes | Ok | recommended size, outer size; FWHM, effective detail, star mm, point distance, the given distance |
| Size | explicit distance, no row passes | Attention | "discs at any listed size from that distance; the smallest minimises it; try smaller widths or a larger distance" |
| Size | natural distance, rows pass | Ok | "no size is recommended without a distance: up to <largest> cm the image is equally sharp at its natural distance, choose by the wall; give a distance if viewed from close" |
| Size | natural distance, rows fail | Attention | "slightly discs at any size; the smallest minimises it; give a distance" |
| Size | `SoftCorners` | Attention | do not enlarge the whole frame; consider a crop |
| Size | crop `Needed` | Attention | what the target cuts, each side named when they differ (`ReportWriter.CropCutText`) |
| Size | any oversize row passes | Ok | above 100 cm glass/weight/transport are the limit |
| Denoise | noise `Insufficient` | Attention | not enough flat sky |
| Denoise | `BandingRisk` | Attention | denoise pressed too hard for print; target 0.8-1.5; sigma after the recommended re-stretch (x the curve's slope at the background) |
| Denoise | `UnderDenoised` | Attention | visible at 100 % on screen, not on paper |
| Denoise | `GoodForPrint` | Ok | exactly where it should be |
| Denoise | otherwise | Ok | acceptable |
| Warnings | `IsUpscaled` | Attention | 8 bits in disguise |
| Warnings | no ICC | Attention | export with sRGB embedded |
| Warnings | ICC not sRGB | Attention | tell the lab; L\* assumed sRGB |
| Warnings | reader note | Ok | the note |

The noise-after-restretch figure: slope = gamma x bg^(gamma - 1) at the measured
background, sigma_after = sigma x slope.

## The report (`Report/ReportWriter.cs`, `Report/Fmt.cs`)

`WriteFull` = banner (`AstroPhotoAnalyzer <version>`, copyright) + body; `WriteBody`
alone is what the console prints after its own banner. `SaveFull` puts `WriteFull` in a file
whole or not at all through `AtomicFile.Write` (`input.md`, "The writer": the probe of an
existing target, the temporary file beside it, the move, the delete); the console's `-o` and
the window's save both go through it, and each tells the exception its own way. Body layout:

```
Fisier: <path>
  <format> <bits> <RGB|mono>, W x H (MP), <compression>, <MB>, <ICC>
  Mediu: <name> (Dmax, negrul hartiei L*). Distanta de privire: <m | 1.5 x diagonala>.

VERDICT: <OK|ATENTIE|PROBLEMA>

1. Formatul de trimis  [verdict]
   <-|!|!!> <text>
     fiindca: <because>
... sections 2-6, "(nimic de semnalat)" when empty

Anexa A. Tonuri        means, % below 32/64, percentiles, L* line
Anexa B. Simularea     table: target (as-is among the grid, "(dat)", "*"), gamma, L* praf, L* cer, negru pur (% patches), % below 15, % above 50, print-safe;
                       the footer, the definition line (dust, sky, method), the exact-target line (at Dmax, Dmax -/+ 0.1, the Dmax from which the as-is dust reaches paper black, with the sky's caveat under L\* 15: `ReportWriter.DustDmaxText`),
                       the cloud table (name, patches, L*, target), the darkest DustCloudReportRows of them;
                       every target goes through ReportWriter.TargetText: the value, or a bound at the ladder's
                       ends, "<= 0.05" (even the lowest step already reaches paper black) and "> 0.60" (none does)
Anexa C. Export        TIFF size; JPEG table; rounding; stress; plateau
Anexa D. Rezolutie     stars, regions, size table with "*" on the recommended row, or a note on why there is none
Anexa E. Culoarea fundalului  background colour (R/G/B), the two level figures, L*, a*, b* and the chroma (the cast's verdict, ADR 0057), and which pixels gave it (`BackgroundResult.SourceText`); the eight regions with deltas, "!" when flagged
Anexa F. Parametri     every parameter as used
```

Every number with decimals goes through `Fmt` with `CultureInfo.InvariantCulture` spelled out
(`F0..F4`, `S1` signed, `Level`, `Dmax`, `Pct`, `Mb`); NaN prints as `n/a`. Integers go through
`Int` and `Dim`, or are interpolated as they are (some counts, thresholds in levels or pixels,
the seed), which relies on both applications running in the invariant culture. This is what
makes the window's saved file byte-identical to the console's output on any Windows culture
(`CultureTests`). Every text is a pair, English and Romanian without diacritics, picked by the
interface language (`language.md`); the numbers are the same in both. The format is pinned by
the golden tests, in both languages: change it on purpose, re-approve, read the diff.

### The Editare Curbe tab's sentences (`Report/CurveTexts.cs`, ADR 0032)

Not part of the report: the console prints none of it. The file holds every sentence the
Editare Curbe tab shows, so that the window writes none: the evidence lines of a suggestion
(`EvidenceLines`: the points, the five L\* before and after, the noise gain, the clipped
share, the paper checks, the gamut figure or the note that explains its absence), the two
paper checks as `Recommendation`s with a `Because` (`PaperChecks`: the background too low for
paper, which sends to the stretch; the noise after the gain over `NoisePrintHiLsb`), where the
anchors came from, why nothing is suggested, the proposed file name and the `HISTORY` text of
the saved FITS (one piece per card, at most 70 characters, the points as `x y` pairs).
