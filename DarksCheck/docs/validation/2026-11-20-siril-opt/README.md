# Siril's -opt on mismatched darks, 2026-11-19 and 20

The result files of the measurement ADR 0070 rests on. The frames were copies, deleted after
the run; the originals were hashed before and after and did not change. Siril 1.4.4, Nikon
D3300, ISO 1600, 120 s, L-eNhance filter; every third light of the Veil nights of 2026-10-06
(V1006), 2026-11-06 (V1106) and 2026-11-08 (V1108) against the library masters idx13 to idx67
(D13 .. D67).

- `pairs.json` - the measurement's `results/summary.json` under another name, byte for byte:
  the 18 pairs: the right factor (median, lowest, highest over the lights), the
  factor Siril's `-opt` printed (`k0_*`), and what is left at the master's hot pixels per light,
  by class of the master's level in ADU, for `opt` (Siril's factor), `stk` (the dark as it is)
  and `fix` (the dark multiplied by the right factor); `*_beyond5` is the share (0 to 1) of a
  class's pixels left more than 5 times the noise floor (`floor`, in ADU) away.
- `stack-veil-2026-11-06-idx33.json`, `stack-veil-2026-10-06-idx18.json` - the two stacks of 28
  lights on one shared registration, the three ways compared: the stack's noise, the difference
  of `stk` and `opt` from `fix`, the dots left, per colour.
- `darkscheck-on-the-copies.csv` - what DarksCheck itself read on twelve of the pairs: ratio,
  degrees, what the subtraction leaves and the verdict as it was worded then.

`conclusions.md` is what the investigation found beyond ADR 0070: what Siril itself advises, the
D3300's dark as measured, why Siril chose 0.002, the other flows tried and a lower threshold for
the cosmetic correction. The files it cites were copied on 2026-11-20 from the measurement's
working folder, a temporary one, under the paths they had there, byte for byte:

- `critic/bias_pattern.json` - per master, how its smooth pattern follows the 1/6 s master
  bias's (`slope_on_bias`, `corr_with_bias`) and the share of its central pixels exactly at
  the raw floor of 147, per colour (`box_floor_share_per_site`). `critic/mnoise.json` - how
  much of three masters' pixel-to-pixel spread is their own noise.
- `results/ratios_extract.json` - made by `results/make_ratios_extract.py` from the
  measurement's three `V*_ratios.json`, which were not copied whole: for every light and every
  master, the light's own hot-pixel ratio (`ratio`, the right factor), the factor Siril's
  measure asks for (`k_siril_criterion`) and the same with each colour's average taken out
  first (`k_sites_removed`).
- `emptiness/measures.json`, `measures2.json` - figures another session worked out the same day
  from the measurement's saved files alone, to see what could predict Siril's factor and the
  stack: per master (`emptiness`: its median and colour averages above 150, the share of pixels
  that do not stand out, the pixels that do), per pair, per light and for the two stacks.
- `skeptic/results/all_pairs_opt_k0.json` - for each of the 510 lights of the 18 pairs, the
  factor Siril logged (`siril_k0`), the right factor, and the covariance of light and dark over
  the dark's variance in Siril's central box (`cov_var`).
- `skeptic/results/b_V1006_D18.json`, `b_V1106_D33.json`, `b_V1106_D67_interior.json` - the
  measure taken apart on a few lights: Siril's search step by step, the colours' averages of the
  light and of the dark (`light_sites`, `dark_sites`), and the two parts of the covariance
  (`cov_site` between the colours, `cov_within` inside them).
- `skeptic/results/flows_V1006_D18.csv` and `.json` - five ways of running `-opt` and the
  colours calibrated apart, light by light, with the re-written measure beside Siril's
  (`python_vs_siril`).
- `skeptic/results/g5_V1106_D33_split.json` - the colours calibrated apart by Siril on a second
  pair; `predict_split_V1006_summary.json` and `predict_split_V1106_V1108.json` - the same
  predicted by the re-written measure for all pairs.
- `skeptic/results/classes_V1108_D13.json`, `classes_V1006_D18.json`, `classes_V1106_D33.json` -
  three masters' pixels by how far they stand above the median of their eight neighbours of the
  same colour, with their count in the frame (not a level above 150);
  `within_groups_V1006_D18.txt` - which of those groups carry the dark's spread and its
  covariance with a light.
- `skeptic/tools/crit.py`, `repro.py` - Siril's measure written again in Python; the
  measurement's tools as they were, no part of the program (they read frames that were not
  kept, so they do not run from here).
- `impact/summary_impact.json` - the summary of the two stacks above;
  `impact/cc2/analysis_D18.json`, `analysis_D33.json` - the stack with the cosmetic
  correction's threshold at 2 instead of 3, against the stack at 3.
- `research-older-vs-newer-excerpt.md` - the points of the research on what Siril and others
  advise that `conclusions.md` cites, word for word.

## Asked afterwards: should DarksCheck show how empty the darks are, or whether -opt is worth it?

The owner asked; the session that made the measurement recommended no new figure. No decision
is recorded here. The facts that recommendation rests on:

- A share of "empty" pixels reads 97 to 99.5 % on every master measured (`conclusions.md`,
  section 3), so it tells no dark from another. What does change with warmth, the pixels that
  stand out, the hot-pixel counts of check 6 and the index (R-060) already show.
- The master's level in these files follows the camera's cut at 147, not the heat: the median
  goes from +0.49 ADU above 150 (idx13) to -2.65 (idx67) while the share of pixels exactly at
  147 grows (`emptiness/measures.json`, `critic/bias_pattern.json`).
- A row "what is left in the stack" was tried as a prediction. It held on green and on the two
  stacks only: 0.084 ADU predicted against 0.060 measured (V1106 with D33) and 0.121 against
  0.142 (V1006 with D18), `emptiness/measures.json`, `stacks`. On red it came out about twice
  too high, by that session's report; no file here holds the red figures.
- A prediction of Siril's factor would bring `-opt` back into the report, against decision 1
  of ADR 0070. It would read 0.002 on 15 of the 18 pairs, and Siril writes the factor it chose
  into its own log anyway.

Some of these files name the temporary folders they were made in. The pictures the stack files
name (`png/...`) were not kept. Nothing here is read by a test.
