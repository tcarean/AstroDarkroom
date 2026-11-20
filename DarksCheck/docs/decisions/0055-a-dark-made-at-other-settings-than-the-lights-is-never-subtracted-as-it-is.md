# 0055. A dark made at other settings than the lights is never subtracted as it is, and the window says so in red

Date: 2026-11-14
Status: accepted 2026-11-14; other settings needing both settings on both sides, and where a set stands in the chained row superseded by 0060; the advice, the verdict's word and the chart with darks at other settings superseded by 0070

## Context

The match of a set against a master is a ratio of hot-pixel heights with both brought to one
setting (ADR 0004, R-052): it says how the temperatures compare, so lights at ISO 800 against
darks at ISO 400 read 1.00 at the same temperature. The verdict was taken from that ratio
alone. The code review of 2026-11-13 (CR-006) showed the outcome: such lights read `OK: the
dark can be subtracted as it is` and `after the subtraction nothing is left to see`, while in
the files each hot pixel is twice as high in the light and a plain subtraction leaves half of
it. Check 1 said WARNING on the settings in the same report, as one yellow chip among the
session's rows. The same held for a partner (`--against`) made at other settings.

The owner answered on 2026-11-14, in chat: another ISO between darks and lights is not
normally accepted; show the mismatch in red, large; if such a dark can be used with `-opt`,
go on with the analysis and give the advice.

Siril's manual (Calibration, read 2026-11-14) asks for darks "at the same exposure time and
ISO/Gain" as the lights. Its dark optimisation (`-opt`) multiplies the dark by one number, is
described for a dark of another exposure, and needs the bias master as well as the dark. Its
cosmetic correction (`-cc=dark`) only looks for the pixels that stand out in the dark.

## Decision

1. **When the rule applies.** A set is "at other settings" than its master when its ISO is
   another, or its exposure differs by more than `Th.ExposureTolerance`, by the rule that
   decides whether a frame belongs to a stack (`FrameStack.SameSettings`), both sides saying
   their settings (a silent set is compared as made at the same, ADR 0053). For lights: more
   than half of the measured lights are (`LightFigures.OtherSettings`), the rule the scale
   section's note already follows. For a partner: its first decoded dark against the
   session's first stacked dark, and against more than half of the measured lights for the
   chained row. In the chained row a set that does not say both its settings stands where
   the set it was compared with stands (ADR 0053): a light taken at the darks' settings and a
   partner that does not say its own at the session's darks', the session's darks at the
   partner's. Two sets that each give one setting and not the other are not at other
   settings, even when that one differs: nothing was brought from one set to the other. For
   the lights against the darks check 1 says that difference (ADR 0058); for the darks against
   a partner nothing does, which the review list holds as CR-076.
2. **The ratio stays.** It still compares the temperatures, and the degrees, the lights'
   equivalent index and the label are read from it as before.
3. **Never `Direct`.** With other settings a ratio that would be `Direct` is `WithOpt`
   (`LightsAnalysis.NeverDirect`), for the night, for each light's point on the window's chart
   and for the partner. The traces of R-067 relax nothing there.
4. **What a subtraction leaves is read from the files.** The trace is `|ratio - c| x k`, `c`
   being the lights' factor to the darks' ADU (`TraceFigures.Centre`; 1 at the darks'
   settings, so no figure of an ordinary session moves).
5. **The words.** The verdict says not to subtract the dark as it is, names both settings and
   how many times higher the hot pixels are in the files, then gives the advice in this
   order: best make darks at the lights' settings; with the darks at hand, `-opt` (which needs
   the biases too) or the cosmetic correction only. The texts are R-052's and R-063's, shown
   to the owner in the plan of 2026-11-14.
6. **Red and large in the window.** The core marks the rows (`ReportRow.Alert`): check 1's
   match when it is a WARNING, check 2's verdict and the partner's verdict at other settings.
   The window draws a marked row with a red chip and red text at 16 px (R-124). The console
   has no colour anywhere and gets none: its two lines open with WARNING.

## Alternatives rejected

- **Leave it to check 1.** The owner's call; the report then holds a WARNING on the settings
  and, a card lower, an OK to subtract.
- **Stop the analysis at such a session.** The hot pixels, the leaks, the noise and the index
  of the darks do not depend on the lights' settings, and the owner asked for the analysis to
  go on.
- **Judge the verdict on the ratio in the files.** Twice the ISO at the same temperature
  would read "they do not match: it is too cold", which is false; the bounds of R-062 are
  about temperature.
- **`-opt` as plain advice, without "best make darks at the lights' settings".** Siril's
  manual describes `-opt` for another exposure only; for another ISO one multiplier brings the
  hot pixels to height, but nothing in the manual says the rest of the dark follows.
- **Colour in the console.** No line of the console has one; the goldens compare plain text,
  and a redirected output would carry escape codes.

## Consequences

A session whose lights are at the darks' settings reads as before: no golden file changed.
The mismatch is said three times in a report (check 1, the row `lights against dark`, the
verdict), once of them in red twice over in the window. Whether `-opt` gives a clean result
at another ISO was not measured on real frames: the owner has no such session, and the text
puts new darks first for that reason. Guarded by
`R052_LightsAtOtherSettingsThanTheDarksAreNeverSubtractedAsTheyAre`,
`R067_AtOtherSettingsWhatASubtractionLeavesIsWorkedOutOnTheFiles`,
`R063_APartnerAtOtherSettingsThanTheSessionIsNeverSubtractedAsItIs`,
`R064_LightsTakenAtTheDarksSettingsFollowTheDarksAgainstAPartnerAtOtherSettings`,
`R064_APartnerThatDoesNotSayItsSettingsStandsWhereTheDarksStandForTheLights`,
`R052_ASettingBothSetsGiveIsUsedAndOneASetDoesNotGiveIsTakenFromTheOther` and
`R124_DarksThatDoNotHaveTheLightsSettingsStandOutLargeAndRed`.
