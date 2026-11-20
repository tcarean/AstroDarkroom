# 0070. A dark off by temperature is subtracted as it is; no verdict names -opt

Date: 2026-11-20
Status: accepted 2026-11-20
Supersedes: 0004 in part (what a ratio outside 0.9 .. 1.1 and inside 0.4 .. 1.4 tells the owner to do); 0030 in part (the words of the -opt and cosmetic-only verdicts); 0034 in part (the chart's yellow and red lines and the sentence above them); 0055 in part (the advice, the verdict's word and the chart with darks at other settings)

## Context

A ratio of the lights' hot pixels over the dark's (R-062) read, outside the green band:
`subtract the dark with -opt` within 0.7 .. 1.4, `use the dark for the cosmetic correction
only ... subtracted it would take out too much and leave black dots` from 0.4 under 0.7. Both
were advice given on 2026-10-10 from how Siril's options are described, never tried on the
owner's frames. On 2026-11-19 and 20 they were tried, on copies, with Siril 1.4.4 (Nikon D3300,
ISO 1600, 120 s, L-eNhance filter): every third light of the Veil nights of 2026-10-06,
2026-11-06 and 2026-11-08 against six library masters (idx13, idx18, idx20, idx33, idx47,
idx67), with the flow `-opt` needs (the level 150 taken out of the stacked master and off the
lights, no master bias). The result files are in `docs/validation/2026-11-20-siril-opt/`.

**Siril's factor.** "Right factor" is the median over the lights of the hot pixels' ratio,
measured apart from DarksCheck (`pairs.json`); DarksCheck read 5 to 20 % lower on the same
copies (`darkscheck-on-the-copies.csv`). `-opt` prints the factor it chose for each light:

| Lights | idx13 | idx18 | idx20 | idx33 | idx47 | idx67 |
|---|---|---|---|---|---|---|
| Veil 2026-11-08, right factor | 0.97 | 0.79 | 0.73 | 0.45 | 0.32 | 0.23 |
| Siril's, 28 lights | 0.002 | 0.002 | 0.002 | 0.002 | 0.002 | 0.002 .. 0.13 |
| Veil 2026-11-06, right factor | 1.20 | 0.97 | 0.90 | 0.53 | 0.38 | 0.27 |
| Siril's, 28 lights | 0.002 | 0.002 | 0.002 | 0.002 | 0.002 | 0.004 .. 0.17 |
| Veil 2026-10-06, right factor | 2.32 | 1.83 | 1.66 | 0.98 | 0.68 | 0.48 |
| Siril's, 29 lights | 0.002 | 0.002 | 0.002 | 0.002 | 0.002 | 0.19 .. 0.42 |

0.002 is the lower end of Siril's search: the dark is in effect not subtracted. It is the
answer on every light of 15 of the 18 pairs, the matched ones included, and under the right
factor on the other three. The one pair DarksCheck itself sent to `-opt` (Veil 2026-11-06 with
idx20, 0.72 by DarksCheck) got 0.002. Read in Siril's source: the factor is the one that leaves
the smallest spread of values in the central 512 x 512 pixels of the raw frame, all colours
together; on these frames the difference between the colours of the sky decides it, not the
hot pixels. (Added 2026-11-20, later: more exactly, the master's red pixels sit highest where
the red sky is lowest, so light and dark vary against each other and Siril's search ends at
its lower end; a measure written again apart from Siril gave Siril's factor on 390 of 390
lights. This, what Siril's own FAQ and manual advise, the D3300's dark as measured, the other
ways of running `-opt` that were tried and a lower threshold for the cosmetic correction are
in `docs/validation/2026-11-20-siril-opt/conclusions.md`.)

**What the factor is worth in the stack.** Two stacks of 28 lights, one shared registration,
the cosmetic correction from the dark on in each, the dark subtracted three ways: as it is
(Siril's stock script), multiplied by the right factor, and with `-opt`. Figures against the
stack made with the right factor, on the sky, per colour:

| Stack | Dark, by DarksCheck | As it is: difference over the stack's noise | Dots above 5 times the local noise, as it is / right factor / `-opt` |
|---|---|---|---|
| Veil 2026-11-06 with idx33 | 0.44, warmer by 7 degrees: "cosmetic only" | 0.12 .. 0.17 | dark 358 / 360 / 368; bright 2696 / 2633 / 2601 |
| Veil 2026-10-06 with idx18 | 1.64, colder by 4 degrees: "they do not match" | 0.15 .. 0.20 (`-opt`: 0.23 .. 0.32) | dark 303 / 311 / 274; bright 13526 / 13542 / 13661 |

With the dark 7 degrees warmer, subtracting it as it is left no more dark dots than the right
factor did: the black dots the verdict warned of did not show. A single light does keep holes
at its hottest pixels (of the master's pixels above 50 ADU, 0.4 % are left more than 5 times
the noise floor away, against 0.06 % with the right factor, `pairs.json`); dithering and the
stack's rejection take them out.

Also found: the band 0.7 .. 1.4 is -3.1 and +2.9 degrees by the rule of 6 degrees per doubling
the program reckons with; the comment in `Thresholds.cs` and ADR 0017 called it ±5 °C.

## Decision

The owner decided in chat on 2026-11-20, each from options with these figures:

1. **No verdict and no text names `-opt`**, for every camera, not only the D3300.
2. **0.7 .. 1.4 outside the green band stays a WARNING and says to subtract the dark as it
   is**, `though it is not at the same temperature`, with the ratio, what the subtraction
   leaves (R-067) and the degrees as before. The verdict is `MatchVerdict.Near` (was
   `WithOpt`).
3. **0.4 under 0.7 stays a WARNING and says to subtract the dark as it is, with the cosmetic
   correction on** (`-cc=dark`), and that the holes of one light do not show in a dithered
   stack. The verdict is `MatchVerdict.MuchWarmer` (was `CosmeticOnly`).
4. **No limit moves**: 0.9 .. 1.1, 0.7 .. 1.4, 0.4, the widening of ADR 0044 and check 4's
   band for the frames of a series are as they were; under 0.4 and over 1.4 still read `they
   do not match` at the lights' settings. `Th.MatchOptLow`, `MatchOptHigh` and `MatchCosmeticLow` are renamed
   `MatchNearLow`, `MatchNearHigh` and `MatchWarmLow`.
5. **Darks at other settings than the lights** (ADR 0055) are still never subtracted as they
   are, and the one advice is darks made at the lights' settings. "The cosmetic correction
   only", which stood beside `-opt` there, is gone too: Siril's manual (Commands, 1.4.4, read
   2026-11-20) says of `-cc=dark` that "a masterdark must be given with the -dark= option",
   and `calibrate` subtracts the dark it is given, so the advice could not be followed with
   the switch it named. A ratio from 0.4 under 0.7 at other settings takes these words too:
   its own would say to subtract a dark of another ISO as it is.
6. **Check 4** says of a frame outside 0.7 .. 1.4 of the master that it is too far from the
   master and gives the band; it no longer says what Siril could match.
7. **Darks at other settings are `REFA`** (`REDO`), not `ATENTIE`: with `-opt` and "cosmetic
   only" gone, the one advice left is to make the darks again, and the owner asked that the
   verdict say so in red. A ratio that would read `Direct`, `Near` or `MuchWarmer` is
   `MatchVerdict.OtherSettings` (`LightsAnalysis.AtOtherSettings`, which was `NeverDirect`),
   and every measured ratio takes the `REFA` words, a ratio under 0.4 or over 1.4 included:
   a dark at another ISO would otherwise be `REFA` when the temperatures are close and only
   `ATENTIE` when they are far. In the window every point of the lights' chart is red, the
   chart draws no guide line (`0.9 as it is` would say the opposite of the verdict) and the
   sentence above it says that every point is red, that the dark is not subtracted as it is
   and to make darks at the lights' settings (the review had found the plain sentence saying
   "subtract it as it is all the same" beside a verdict that says not to). Check 1's row
   on the settings stays `ATENTIE`: it states the difference; the `REFA` is said once.

The texts are R-052's, R-062's, R-063's, R-023's and R-086's, shown to the owner original
beside proposal in the plan of 2026-11-20.

## Alternatives rejected

- **Keep `-opt` with a caution** (the factor Siril should find, and what to do when its log
  shows one near 0). The owner would have to read Siril's log after every run, and on this
  camera the check fails nearly every time.
- **Green from 0.7 to 1.4.** A dark 3 degrees off would read like a matched one, and the rule
  of ADR 0044, which already turns the band green where the leftover is under the noise of a
  light, would have nothing left to decide.
- **The new words only for the D3300.** Two sets of texts and tests, and every other camera
  would keep advice nobody measured there.
- **Keep "cosmetic correction only" from 0.4 under 0.7.** Its reason, the black dots, was not
  seen in the stack, and without the subtraction the pattern the camera leaves along the edges
  stays in the lights.
- **Move the limits to what the two stacks allow** (a dark colder by 4 degrees, at 1.64, also
  gave the same picture). One stack on each side, from one camera with one filter.

## Consequences

R-023, R-025, R-052, R-054, R-055, R-062, R-063, R-064, R-067, R-086, R-101, R-122 and R-124
amended; ten
lines of eight golden files changed, each one of the texts above. No formula and no constant's
value changed. `docs/validation.json` recorded the verdict's name `CosmeticOnly` in two cases.
At the owner's word of 2026-11-20 V-012 reads `MuchWarmer`, the name alone. V-005's partner,
the Smida darks (ISO 400) against the library (ISO 1600), is a pair at other settings, which
item 7 turns into `REFA`: the owner called it no real case and took the pair out of the
validation, V-005 keeping the Smida lights against their own darks. `/validate` was run after
it (`docs/validation.md`, History). The measurement in Context does not lean on that pair:
its eighteen pairs are all ISO 1600 at 120.2 s on both sides
(`darkscheck-on-the-copies.csv`, where check 1 reads `OK` on the settings).
Guarded by `R052_TheAmplitudeRatioOfTheLightsOverTheMasterGivesTheVerdict`,
`R052_ADarkMuchWarmerThanLightsAtOtherSettingsIsNotSubtractedAsItIs`,
`R052_LightsAtOtherSettingsThanTheDarksAreNeverSubtractedAsTheyAre`,
`R063_APartnerAtOtherSettingsThanTheSessionIsNeverSubtractedAsItIs`,
`R064_TheLightsAreComparedWithThePartnerOnTheSettingsThoseTwoGive`,
`R086_TheChartSentenceForLightsAtOtherSettingsSaysWhatTheVerdictSays` and
`R082_Check2AndTheIndexAreInPlainWords`, which also holds that no text of check 2 or of the
index names `-opt`.

Not measured, and the texts lean on it: another camera, lights without a filter, lights that
were not dithered (the leftover then stays on the same pixel of the stack, as the row `what
is left` already says), and a dark at another ISO or exposure, where nothing was tried and the text advises only
new darks. Repairing with such a dark without subtracting it would go through `find_hot` and
`-cc=bpm`, by the manual; not tried, and not advised. The yellow verdicts now ask nothing of the
owner beyond what the stock script does; they stay yellow because they say the darks were not
taken at the night's temperature, which is what the next set of darks can do better.
