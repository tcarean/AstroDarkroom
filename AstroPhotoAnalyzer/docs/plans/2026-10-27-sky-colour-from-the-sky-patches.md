# The sky's colour comes from the sky patches, not from the darkest quarter

Branch `claude/Sky-colour-from-the-sky-patches`, worktree
`.claude/worktrees/Sky-colour-from-the-sky-patches` (made from the monorepo root
`E:\Photografie\Code`, base `main`). The approved plan is saved to
`docs/plans/2026-10-27-sky-colour-from-the-sky-patches.md`.

## Context

Section 3 of the report judges the cast and the eight edge regions against "the background",
which ADR 0014 defined as the darkest quarter of the pixels above the bottom band. On the
owner's Milky Way core of 2026-08-12 (`starless_denoised_syqon_studio_in_ultra0.75_starless_stretch0.15.fit`,
GraXpert-flattened, visibly clean but for the horizon in the bottom-right corner) the darkest
quarter is the Great Rift's dust, brown (R 30.5, G 32.8, B 27.2), while the plain sky reads
36 / 39 / 38 everywhere: every edge comes out 4 to 13 levels "bluer than the background" and
six of the eight regions are flagged coloured, "cool". The row and column profiles of the
per-channel median are flat to 1-2 levels outside the Rift. The reference image passed the same
rule only because its dust differs from its sky almost neutrally.

Measured on 2026-10-27 with a probe test (not committed) on the five real images, the eight
regions' deltas against four candidate references, with the flag the rule would give (`!C`
coloured, `!N` neutral):

| Image | Reference | Flagged regions |
|---|---|---|
| `de_printat.png` | darkest quarter (today) | all eight: bottom-right `!C`, bottom `!C`, six `!N` |
| | frame median above the bottom band | bottom-right `!C`, bottom `!C`, top-left `!C`, top `!N`, bottom-left `!N` |
| | sky patches, all rows | bottom-right `!C`, bottom `!C`, top-left `!C`, top `!N` |
| | sky patches above the bottom band | bottom-right `!C` (+4.6 / -3.6 / -17.0), bottom `!C` (+8.5 / -0.7 / -7.6), top-left `!C` (+6.5 / +5.0 / +8.8), top `!N` |
| the 2026-08-12 core | darkest quarter (today) | all eight: six `!C`, two `!N` |
| | frame median above the bottom band | bottom-right `!C`, bottom `!C`, top-left `!C` |
| | sky patches, all rows | bottom-right `!C`, bottom `!C`, top-left `!C`, top `!N` (+3.4 in B) |
| | sky patches above the bottom band | bottom-right `!C` (+1.8 / -3.3 / -6.7), bottom `!C` (+6.2 / -1.8 / -8.6), top-left `!C` (-3.4 / -2.9 / -6.5, the Rift's dust) |
| `Rosetta_starless_no_WCS.fit` | darkest quarter (today) | all eight |
| | frame median above the bottom band | six (the median holds nebula: 29.5 / 25.7 / 25.1 against the sky's 26.3 / 23.8 / 23.3) |
| | sky patches above the bottom band | bottom-left `!C`, top `!C`, left `!C` |
| `Veil_Starless.fit` | darkest quarter (today) | all eight |
| | sky patches above the bottom band | top-right `!C`, bottom `!N`, left `!N` (+3.1) |

The sky references agree on the two warm horizon regions of the reference image (ADR 0018's
finding) and remove the false flags; among them the sky patches above the bottom band are the
cleanest on the new core and on Rosetta. The cast moves with the reference: the reference image's
green excess +1.6 -> +0.9 (under `CastWarnLevels`) while red minus blue goes +0.3 -> -1.9 (a cool cast), the new core's green +3.9 -> +2.2.

## Decision

The sky's colour (E1) is the per-plane median of the pixels of the sky patches of R-044's
classification (`DustMap.Classes == Sky`) that lie above the bottom
`BackgroundExcludeBottomFraction` of the rows; when no sky patch lies there, of every patch
above that band; when the frame holds no patch (under one patch tall), of every pixel. The
cast and the edge regions (E2) are judged against it. `BackgroundDarkestFraction` goes; the
regions keep the stride-3 subsample and their rule (ADR 0018). ADR 0055 records it and
supersedes the colour part of ADR 0014.

## Steps

1. `docs/requirements.md`: R-060 restated (the sky patches, the fallbacks, the new examples),
   R-061's example and R-093's example re-measured; both R-060 and R-061 `implemented`.
2. Tests first: `BackgroundAnalyzerTests` - a brown dust lane darker than the sky over a third of
   the frame leaves the background at the sky's colour and the sky edges unflagged (R-060, the
   case of the new core); the bright core over half the frame still stays out (renamed); the
   fallbacks; R-061 on a flat sky beside the lane. `RecommendationsTests` on the dark scene.
3. `BackgroundAnalyzer.Analyze(Raster, DustMap)`; `AnalysisRun` passes `res.Simulation.Map`;
   an overload without the map builds one for the tests. `BackgroundResult` says which tier
   gave the reference, and Appendix E prints it.
4. Texts: `Thresholds` comments, the Culoare tab's row description, the delta paragraph and the
   coloured-or-neutral guide with the new figures of the reference image; the report's
   Appendix E.
5. Goldens with `APA_UPDATE_GOLDEN=1`, every changed line tied to this change; `/validate` with
   the drift table (V-001, V-004, V-010, V-013 carry background figures).
6. Docs: ADR 0055, ADR 0014's Status line, a dated note on ADR 0018's figures,
   `measurements.md`, `thresholds.md`, `known-limitations.md`, `window.md`, `validation.md`,
   CHANGELOG, README's Colour row if it names the reference.

## Not in this task

- Measuring each region on its own sky patches (leaving the band and the dust out of the
  region's median): it would also stop the band entering a corner from reading as a coloured
  departure (the reference image's top-left, +6.5 / +5.0 / +8.8 against the sky), but a horizon
  glow bright enough to be classed nebula would vanish from the regions with it. To measure
  before deciding.
