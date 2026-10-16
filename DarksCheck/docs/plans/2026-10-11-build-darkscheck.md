# 2026-10-11 Build DarksCheck

## Goal

A C# application (console `darkscheck.exe` and a WPF window, kit process, like FlatCheck and
LightsCheck) that opens a Siril session folder or a bare folder of darks and repeats the nine
checks made by hand on the Smida session of 2026-08-12 (`docs/validation/2026-08-12-smida/`,
the Python scripts in `prototype/`): metadata and cadence, light leaks, thermal drift inside
the series, noise per frame, hot pixels and their stability, fixed pattern, dead and saturated
pixels, how many darks are needed, and the lights against the dark. On top of them it computes
the **index**, a number that says how warm the sensor ran, on darks, on a library of darks and
on lights, so that a library master can be matched to a session, and it shows the label the
owner puts in the library folder's name. Any camera; the raws are only read.

Agreed in chat on 2026-10-11: no saved reference set (the index is a rank statistic of the
master, the matching is a ratio on the pixels two sets share); console and window; the label
is shown, the owner renames the folder; the window, licence, versioning, dark theme and
"Despre" window as in FlatCheck.

## Decisions taken with the plan

1. **Copied, not shared.** The readers (`RawReader`, `FitsReader`, `ImageLoader`,
   `CfaPattern`, `Img`, `Stats`, `PngWriter`, `BitmapFont`) and the WPF pieces (theme, title
   bar, Mvvm, converters, palette, charts, "Despre") are copied from LightsCheck, which holds
   FlatCheck's copies plus the shot metadata LightsCheck added to `RawReader`; a fix goes to
   the source first (ADR, as LightsCheck's ADR 0003).
2. **Input.** A session folder with `darks/` (required), `biases/` and `lights/` (optional,
   `flats/` ignored), or a folder holding the raws directly (a library). `--against <folder>`
   names a second darks folder or session whose master is the partner for matching (ADR).
3. **No reference file.** The index of a master is the median amplitude above black of the
   pixels ranked 5 to 20 per megapixel (the 120th to the 483rd strongest pixel of a 24 MP
   sensor), divided by the ISO factor (ISO/400, linear, a setting) and by the exposure factor
   (exposure/120 s). Measured on 2026-10-11: Smida 25, the 2026-10-10 library 66.5 (266 ADU at
   ISO 1600). The matching of two sets is the median ratio of amplitude over the pixels the
   partner master has above 20 ADU-equivalent, both unsaturated, with the quartiles; a lights
   set is measured at those positions as the excess over the median of the eight same-colour
   neighbours, per light and over the lights. The temperature offset is
   `D * log2(ratio)` with `D = 6 °C` (a setting). The rank index sorts a library; the ratio
   matches two sets (on Smida vs the library they differ: 2.66 vs 2.2-2.4, since the rank band
   of a warmer master takes in weaker pixels), so the report always shows which one it is (ADR).
4. **The label.** `idx<NN>` with the index rounded to an integer, proposed as
   `<folder name>-idx<NN>` and shown with a copy button; nothing is written into the input
   folder (ADR, as LightsCheck's ADR 0002).
5. **Verdicts** for lights against a dark master, ratio r = lights/dark: 0.9..1.1 plain
   subtraction; 0.7..1.4 subtraction with `-opt`; r under 1 down to 0.4 cosmetic correction
   only (the dark is warmer and sees every hot pixel); otherwise unmatched. Thresholds in
   `Thresholds.cs` (ADR); the owner's margin rule of 2026-10-10 (−3/+8 °C for `-cc=dark`).
6. **Version** 0.x during the build, 1.0.0 at the first release; GPL-3.0-or-later; the version
   in `Directory.Build.props`.
7. **Oracle.** `docs/validation/2026-08-12-smida/results.json` and the CSV files of the Python
   analysis are the numeric oracle, with tolerances; the validation cases run outside the gate
   with `DARKSCHECK_TESTDATA_LARGE=E:\Photografie\Astro` (the Smida session and the library
   both sit under it).
8. **The darks are held in memory** as 16-bit mosaics for the median master (20 frames of
   24 MP = 1 GB); step 2 measures time and memory on 20 and 30 frames and sets a cap as a
   setting above which every k-th frame feeds the median (ADR after the measurement).

## What is measured (the nine checks as requirement areas)

- **R-001..** console contract and inventory (check 1): banner, folders and counts, per frame
  the time, exposure, ISO, camera; the cadence and the gaps; darks against lights on exposure
  and ISO; a frame that cannot be read exits 1.
- **R-010..** reading: raws through libraw undebayered at the ADU scale, FITS by the copied
  reader; the quantisation step of the file near black and at the signal level (the D3300
  writes steps of 2, 3-4 and 7 ADU), so every noise figure is a clipped standard deviation.
- **R-020..** the dark master and the frame checks: median and mean master per CFA position,
  per-frame mean above black, clipped sigma, residual sigma against the master (check 4),
  row and column banding against the white-noise expectation, the master's structured banding
  (check 6), the regions of the binned master (centre, corners, edges) and the same of the bias
  master and of dark minus bias (checks 2 and 6), dead and saturated counts (check 7), the
  noise of the mean of n frames against sigma/sqrt(n) (check 8), the warming curve of the
  series read from the hot pixels (check 3).
- **R-030..** hot pixels: counts above 10, 20, 50, 100, 500 ADU-equivalent and saturated,
  the amplitude percentiles, the stability over the frames, flickering and random-telegraph
  candidates, clusters (check 5).
- **R-040..** biases: read noise from frame pairs, the master, the low-end clipping check.
- **R-050..** lights (check 9): isolated hot pixels per light (above 6 sigma of the local
  excess, no neighbour above 3 sigma), the persistent set (80 % of the lights), the overlap
  both ways with the dark master at several thresholds, the amplitude ratio per light and
  over the night.
- **R-060..** the index and the matching: the rank index of a master, the pairwise ratio and
  quartiles, the temperature offset, the verdict, `--against`, the label.
- **R-070..** reports and exports: the Romanian text report, the CSV files per frame, the
  binned maps as PNG (dark, bias, dark minus bias, the partner), `--out DIR`.
- **R-080..** the window.

## Steps

Each step is a task on its own `claude/` branch in its own worktree under
`DarksCheck/.claude/worktrees/`, built on the previous step's branch; the owner merges them in
order with `--no-ff`. `/code-review high` is asked at every step and what makes sense is fixed
before the commit. This session runs in the empty project folder, so the kit's hooks are not
loaded until the first merge; the gate is run by hand before every commit.

1. **Bootstrap from the kit** (`claude/Bootstrap-DarksCheck-from-the-kit`): copy the kit,
   `rename.ps1 -Name DarksCheck`, `kit.json`, `CLAUDE.md`, licence, packages (libraw only), the
   readers and `Thresholds.cs` skeleton, the five Python scripts frozen in `prototype/`, the
   Smida report, JSON and CSV files and figures in `docs/validation/2026-08-12-smida/`, this
   plan, ADRs 0001 to 0006 (kit adoption; read-only input and the session shapes; copies from
   LightsCheck; the index as a rank statistic and no reference file; Romanian for the user;
   the oracle), the first real feature so no sample code stays: the console inventory with the
   cadence and consistency checks (check 1), the goldens, V-001 on Smida and V-002 on the
   library. Gate green.
2. **Measure the dark series** (`claude/Measure-the-dark-series`): `Analysis/` for the stack,
   the masters, the per-frame figures, banding, regions, the noise curve, the warming curve,
   the hot-pixel classes and stability, the bias figures and dark minus bias; `Report/` rows in
   Romanian; `--out`; synthetic darks with known answers in the tests; V cases on Smida
   against the oracle (433 / 107 / 24 hot pixels, sigma 1.47..1.55, read noise 1.26, regions,
   the 0.83 → 1.08 warming) and on the library (0.74 → 1.09). Time and memory measured (decision 8).
3. **Lights, the index and the matching** (`claude/Match-lights-and-compute-the-index`): the
   isolated hot-pixel detector on lights, persistence, overlap, the ratio per light; the rank
   index; `--against`; the verdicts; the label. V cases: 86 persistent, 85 of 86 in the dark,
   light/dark 0.72 at the first light and 1.0 ± 0.05 from minute 15, library over Smida 8.9
   (2.2 after the ISO factor), index 25 and 66.5.
4. **Add the window** (`claude/Add-the-window`): `DarksCheck.Wpf` with the tabs Rezumat,
   Cadre (the darks frame by frame), Harti (the binned maps), Pixeli fierbinti, Lights,
   Index (the label with its copy button, the partner folder, the verdict), Raport, and
   Despre; the settings (threads, the ISO factor, the doubling constant, the frame cap);
   parallel decoding with progress and cancel; the charts drawn as in LightsCheck; the
   export. XAML smoke tests with `DARKSCHECK_DUMP_SCREENS`. Own icon.
5. **Validate, document, release 1.0.0** (`claude/Validate-and-release-1-0-0`): the V cases
   re-run, the design notes, `/docs-health`, CHANGELOG, `release.ps1 -Version 1.0.0` from
   `main` after the merge.

## Verification

`scripts/verify.ps1` green at every step; `/validate` with `DARKSCHECK_TESTDATA_LARGE` set to
`E:\Photografie\Astro` reproduces the figures of `docs/validation.md`; on the Smida session
the console prints the same verdicts as `docs/validation/2026-08-12-smida/report.md` and the
label `idx25`, on the library `idx67`; `darkscheck <smida> --against <library>` reports the
ratio 2.2 ± 0.2 after the ISO factor and about +7 °C.

## What deviated

(filled at wrap-up of each step)

- Step 1: the project was not built by copying the kit and re-copying pieces from FlatCheck
  but by extracting LightsCheck's tree (`main` 6cc0078), pruning and renaming it, then
  syncing the kit's shared folders (ADR 0001): the readers, the console skeleton and the
  repository tests came with it. The kit's own script tests were dropped as in LightsCheck.
  The console's default run is the inventory until step 2 (R-004). Check 1 grew two verdict
  lines (`potrivire`, `bias`) and the long-pause report (R-006, R-007) that the plan's list
  only implied. The copy of `FitsReader` is LightsCheck's current one, with the `SIMPLE`
  detour in `FrameInfo.Read`, since LightsCheck has not yet taken FlatCheck's ADR 0057
  (ADR 0003). The step's review (`/code-review high`) found six points, all fixed before
  the commit: an unknown ISO counted as a match, an unmeasured timing in the README, the
  wording of mixed groups, a `pauza` header on the biases table, a plural for one frame, a
  duplicated banner test. A second review by a subagent after the commit found twelve more,
  fixed in a follow-up commit: a pause of -0.2 s from the 1 s EXIF granularity printed as
  `-0 s` on the library (now clamped to 0, R-006), the dominant exposure of a group taken from
  the frame that opened the cluster instead of the most frequent one (R-007), a wrong `0.0002`
  for a 1/4000 s exposure in the design note, LightsCheck's Veil timing cited in `imaging.md`,
  a Romanian assertion in an English test, missing tests for several console texts, and
  wording of the unmeasured verdicts; the unused lens fields of `FrameInfo` were left as they
  came from LightsCheck.
- Step 2: the darks are held in memory as planned, but the readers' doubles made the working
  set reach 14 GB on 50 frames until a forced collection per decoded frame and the runtime
  knob `System.GC.ConserveMemory=9` brought it to 4.5 GB (ADR 0007); the cap is 60 frames.
  The biases are decoded before the darks, not after, because a FITS carries no black level
  and the biases' median is the pedestal (a Siril-converted FITS keeps the camera's 150). The
  hot-pixel levels are in ADU equivalent, so the Smida master counts 443 pixels above 20
  ADU-eq (19.9 ADU at 119.2 s) where the prototype's raw threshold gave 433; the banding
  profiles clamp the hot pixels, which the prototype did not (one 600 ADU pixel lifted a row of
  a small synthetic frame by 4.7 ADU). Every other figure reproduces the prototype (V-003,
  V-004). The step's own review (`/code-review high`) found eight points, fixed before the
  commit; a subagent review then found sixteen more, the serious ones being check 8 saying OK
  for a single dark with the spatial sigma of a mean master (now at least 10 frames and the
  median master's noise from the temporal sigma), the dead limit and the biases' levels scaled
  by the normalisation (now raw), skipped frames counted as errors, `--out` accepted inside the
  session, a crash on frames under one block, and a corner over the threshold passing check 2
  (the fifth rule is gone); `--max-frames` became an option. A third review (Fable, 2026-10-16)
  found thirteen more, fixed in a follow-up commit: skipped biases counted as errors and failed
  biases invisible in the console, a single dark printing zero noise as measured, the settings
  check comparing ISO x exposure instead of each setting, float FITS converted blind, `--out`
  refused only after the analysis, a PNG of an empty map, unit labels and doc figures; the
  median factor sqrt(pi / 2) is kept knowingly under the 1.35-1.40 measured at 10 frames.
- Step 3: the lights are decoded one at a time and dropped, not stacked, since no figure of
  check 9 needs two lights at once (ADR 0008). The matching of two masters is read on the
  pixels hot in both masters, not on the partner's pixels above 20 ADU-eq as ADR 0004 said:
  measured on Smida against the library, the partner's weak pixels alone gave 3.1 (-9.8
  degrees) where the pixels hot in both give 2.4 (-7.5), the prototype's 2.2 on the strongest
  (ADR 0004 carries a dated note). The lights' figures reproduce the prototype exactly (86
  persistent, 85 in the master, 1.00 over the night, 0.72 at the first light); the library's
  index reads 67.4 (`idx67`) against the prototype's 66.5, by the median master. The report
  gained the overlap of the two hot-pixel sets at 50 ADU-eq, the lights' equivalent index and
  the chained lights-over-partner ratio, which the plan only implied; the label takes the
  parent's name when the darks sit in `darks\`. One hot pixel in a hundred per light fails the
  isolation test by noise in a neighbour, as in the prototype; the tests allow it. The step's
  own review (`/code-review high`) found seven points, five fixed before the commit: a partner
  whose frames all fail reported as `0 x 0 px` instead of by its file errors, the lights'
  equivalent index and the chained lights-over-partner ratio computed in the report (now in the
  run, carried in the result for the window), the overlap level 50 as a literal (now
  `Th.PartnerOverlapAdu`), the frame cap on lights without a stated reason (now in ADR 0008: a
  bound on time, not memory), a redundant count in the overlap record; the full-size excess
  raster kept per light in flight (96 MB at 24 MP) was left as is. A Fable subagent review
  then traced every numeric path against the prototype and found no divergence; its eleven
  points were documentation and wording, seven fixed before the commit (the `--out` help text
  without the lights CSV, the rank band said three ways, three XML references, the partner's
  mechanics described in the wrong design note, two tests pinned to a count the isolation noise
  can move, an example in R-064 that no run shows, a README phrase), the rest noted: the
  saturation and amplitude edges differ from the prototype by one ADU (0.977 x 4095 = 4000.8
  against `a < 4000`; 0.73 x 4095 = 2989 against `< 3000`), the band's median is the upper
  middle element, the partner stack lives until the overlap loop ends.
