# Code review of the whole project, 2026-11-13

The list of what the review found, to be fixed one entry at a time in the order of the summary
table. The decision to hold it, the severity scale and how an entry is closed are ADR 0046.

## How it was done

The code was written by an earlier model and had almost no review. On 2026-11-13 it measured,
blank lines included, 16,507 lines (`src/LightsCheck.Core/` 11,492, `src/LightsCheck.Cli/` 567,
`src/LightsCheck.Wpf/` 4,448) and 10,912 of tests: too much for one careful reading, so it was
split into seven areas. Each area was read whole by a reviewer with a clean context (a subagent
on this session's model), which first read `CLAUDE.md`, the area's design notes and its
requirements, so that what is deliberate was not reported, and read the area's tests for one
question: can this test fail, and does it check what its name says. A reviewer changed nothing
and built nothing. Every finding it returned was then checked by the session that wrote this
list: the cited lines were opened and the case followed through the callers. The seams between
areas (what `Runs/` hands the console and the window, the language scope, the output folder,
the help text against the requirements) were read by the session itself, before the reports
came in.

| # | Area | Reported | In this list as |
|---|------|---------:|-----------------|
| 1 | One frame | 8 | CR-002, 007, 013, 019, 021, 030, 051, 053 |
| 2 | The series | 8 | CR-004, 005, 006, 010, 011, 012, 015, 036, 041, 052 |
| 3 | The series report and the language | 21 | CR-001, 014, 017 to 021, 029, 030, 034, 038 to 041, 048, 057 |
| 4 | Session, console, log | 11 | CR-003, 010, 014, 018, 021, 031, 032, 033, 039, 045, 049 |
| 5 | Imaging | 10 | CR-008, 009, 028, 044; CP-1 to CP-6 |
| 6 | Window: view-models and shell | 9 | CR-014, 016, 022, 023, 037, 043, 046, 047, 050 |
| 7 | Window: views, presentation, theme | 17 | CR-024 to 027, 035, 042, 054, 055, 056 |
| - | Seams, read by the session | 3 | CR-014, 015, 031 |

84 findings were reported and none was rejected at the check; one (a chart that does not guard
a value the window never gives it) went to the cleanup list. The same defect seen from two
areas became one entry and small related ones were grouped, which gives the 61 entries below,
and six more for the files copied from FlatCheck. The three seam findings were each found by a
reviewer too. That no finding was rejected says the reviewers were careful, not that every
entry is proved: the check was a reading, not a run. What a reading cannot settle is marked.
The test notes inside CR-058 to CR-060 and the cleanup list were spot-checked, not opened line
by line.

Out of scope: `scripts/` and `.claude/` (the kit's), `prototype/` (frozen, ADR 0003), and how
strong the tests are line by line (`/mutate` measures that).

Four reviewers worked outside the repository, read-only. Areas 1 and 2 replicated a few
functions in throw-away Python (the star moments, the pattern's noise estimate, the
registration vote) to put figures on a finding; area 4 ran two small scripts (number formatting,
a folder listing); area 5 read the public sources of LibRaw 0.21.1 and of Sdcb.LibRaw, and the
field types of the shipped `Sdcb.LibRaw.dll`. Their figures are quoted as theirs.

Line numbers are those of commit `d2345bf7`. A task finds its lines again by the names the
entry gives.

## How to read an entry

- **Severity.** 1 = a wrong figure or a wrong verdict: a frame left out or kept wrongly, a
  number shown that is not the session's. 2 = a crash, a hang, or a file written where it must
  not be. 3 = a wrong text or a wrong display. 4 = robustness and cleanliness.
- **Reach.** Who meets it: *common* (an ordinary run), *some setups* (another camera, lens,
  mount, sky or session shape than a Nikon D3300 with a 135 mm lens on a Star Adventurer GTi
  under a dark sky), *slip* (a mistyped argument or a wrong click), *rare*, *crafted* (a broken
  or hostile file).
- **Evidence.** *confirmed*: the lines were read and the case follows from them. *to measure*:
  the code was read, but the case rests on something only a run, a screenshot or an outside
  source can show; the task starts by showing it. *owner*: the code does what is described and
  follows a requirement or a note; whether that is a defect is the owner's call.
- **Status.** `open`, then `fixed <commit>` or `rejected: <reason>`, set by the task that
  closes the entry.

An entry gives the direction of a fix, never the fix. A task takes one entry (or the group one
entry names), starts from the requirement and a failing test (the kit's rules "Requirements
first" and "Test before fix"), and a formula change re-runs `/validate` (the rule "Measured on
the sessions"). A text an entry would change is shown to the owner first, original and proposal
side by side (the rule "Plain words"); a golden line it changes is reported at wrap-up (the
rule "Golden files").

## Summary

| ID | Sev | Reach | Evidence | Title | Status |
|----|----:|-------|----------|-------|--------|
| CR-001 | 1 | some setups | confirmed | The session's exposure verdict can come from a rejected frame | fixed 9a7b8234 |
| CR-002 | 1 | some setups | confirmed | Saturated stars vote on focus and elongation under a bright sky | fixed a487d963 |
| CR-003 | 1 | some setups | confirmed | With FITS lights in the folder itself, `result.fit` is counted as a light | fixed 6267276e |
| CR-004 | 1 | some setups | to measure | After a meridian flip no frame is aligned, and a weak fit is accepted as good | fixed c8d9a7df |
| CR-005 | 1 | some setups | to measure | The sensor pattern reads stars as hot pixels when the frames barely moved | fixed 47f3e89f |
| CR-006 | 1 | some setups | to measure | The pattern's strength is too high on a short session | fixed 65dadaae |
| CR-007 | 1 | some setups | owner | A star wider than about 5 px is read too narrow, so a soft frame can pass | fixed 49db7b96 |
| CR-008 | 1 | some setups | confirmed | A Fujifilm X-Trans raw is measured as a monochrome frame | fixed 191549ab |
| CR-009 | 1 | some setups | to measure | A DNG whose black level is a 2 x 2 pattern is read with black 0 | fixed 78033d84 |
| CR-010 | 1 | rare | confirmed | Frames are read in file-name order as if it were time order | fixed 804e4a14 |
| CR-011 | 1 | rare | confirmed | The corner shift uses the first aligned frame's exposure | open |
| CR-012 | 1 | rare | owner | A trailed frame with a short exposure votes for the RA axis | open |
| CR-013 | 1 | rare | confirmed | A thin mark of 251 to 400 px is counted as a trail and as a star | fixed e0ec7248 |
| CR-014 | 2 | slip | confirmed | The report files can be written into `lights/` or `biases/` | open |
| CR-015 | 2 | rare | confirmed | One late failure loses the whole run, after every frame was measured | open |
| CR-016 | 3 | common | confirmed | A wrong pixel size is not marked in the window | open |
| CR-017 | 3 | common | confirmed | The exposure verdict and the figure printed beside it disagree at the limit | open |
| CR-018 | 3 | common | confirmed | The Romanian tables do not sit under their headers | open |
| CR-019 | 3 | common | confirmed | A count of one gets a plural word | open |
| CR-020 | 3 | common | owner | The `marginal (elongation between ...)` row lists frames that are not | open |
| CR-021 | 3 | common | owner | A small negative figure prints as a negative zero | open |
| CR-022 | 3 | common | confirmed | Stop shows nothing until the frames in flight finish | open |
| CR-023 | 3 | common | owner | Changing an input after a result changes nothing on screen | open |
| CR-024 | 3 | common | to measure | The Frames tab cuts the `same spot` cell | open |
| CR-025 | 3 | common | to measure | The Dithering tab cuts its table cells at 100 px | open |
| CR-026 | 3 | common | confirmed | The charts' tick labels are rounded more coarsely than their gridlines | open |
| CR-027 | 3 | common | confirmed | The nearest-frame chart carries the step chart's `move >= 5 px` line | open |
| CR-028 | 3 | common | to measure | The CSV carries single-precision noise in the exposure and the f-number | open |
| CR-029 | 3 | some setups | confirmed | The summary can say `the read noise adds NaN %` | open |
| CR-030 | 3 | some setups | confirmed | On a float FITS every level prints as 0 | open |
| CR-031 | 3 | some setups | confirmed | `--help` says `--pixel` comes before the file's `XPIXSZ` | open |
| CR-032 | 3 | some setups | confirmed | Hidden and sidecar files are taken as frames | open |
| CR-033 | 3 | some setups | confirmed | A frame without a focal length prints `- mm` | open |
| CR-034 | 3 | some setups | confirmed | `that is 0 s of exposure` when the frames carry no exposure | open |
| CR-035 | 3 | some setups | to measure | Bars hang outside the plot on a short session; two guide labels overlap | open |
| CR-036 | 3 | some setups | owner | With the camera turned 25 to 65 degrees no trail reads `on RA` | open |
| CR-037 | 3 | some setups | to measure | Restore Down can put the title bar above a low screen | open |
| CR-038 | 3 | rare | confirmed | `the biases are missing` and `the sky could not be measured` when they are there | open |
| CR-039 | 3 | rare | confirmed | The series header has no `WARNING` line for a light whose pixels cannot be read | open |
| CR-040 | 3 | rare | confirmed | A figure rounded at a limit contradicts the word beside it | open |
| CR-041 | 3 | rare | to measure | One light, or nothing aligned: sentences that do not fit | open |
| CR-042 | 3 | rare | confirmed | The tooltips say `frame 1` when another frame is the reference | open |
| CR-043 | 3 | rare | confirmed | The progress line can go backwards and stay on `86 / 87` | open |
| CR-044 | 3 | rare | to measure | A raw stamped in the hour the clocks skip comes back an hour off | open |
| CR-045 | 4 | slip | confirmed | Options that do not apply are dropped in silence; `--out` takes an option as its folder | open |
| CR-046 | 4 | slip | to measure | The thread count has no upper bound | open |
| CR-047 | 4 | common | to measure | Every row selected in Frames starts a full decode that is never cancelled | open |
| CR-048 | 4 | some setups | confirmed | A failed export leaves a mixed set of files | open |
| CR-049 | 4 | common | to measure | After Ctrl+C the console stays on UTF-8 and the log has no last line | open |
| CR-050 | 4 | slip | confirmed | A relative folder on the window's command line stays relative | open |
| CR-051 | 4 | common | confirmed | The star moments are summed in one window and divided by another's flux | open |
| CR-052 | 4 | rare | confirmed | The series: a zero DEC row, a rejected reference frame, a one-sided jump test | open |
| CR-053 | 4 | rare | confirmed | One frame: a noise of zero, a NaN pixel, a cut-out skipped | open |
| CR-054 | 4 | - | confirmed | Thresholds typed as numbers in texts | open |
| CR-055 | 4 | common | to measure | The Frames tab: scroll bar out of sight, long names, an empty tooltip, the minimum width | open |
| CR-056 | 4 | common | to measure | The theme and the automation tree | open |
| CR-057 | 4 | - | owner | Figures and verdicts decided in the report, not in the analysis | open |
| CR-058 | 4 | - | confirmed | No gate test has a black level above zero, or decodes a raw | open |
| CR-059 | 4 | - | confirmed | Core tests that cannot fail or do not check what their name says | open |
| CR-060 | 4 | - | confirmed | Window tests that cannot fail or do not check what their name says | open |
| CR-061 | 4 | - | confirmed | Design notes and comments that describe other code | open |
| CR-062 | 4 | - | confirmed | The fixtures' star grid repeats, so frames with no shift register one column over | open |

After the entries: the six findings in the files copied from FlatCheck (CP-1 to CP-6), fixed
there first, and the cleanup list, which holds what is not a defect.

## Severity 1: a wrong figure or a wrong verdict

### CR-001 The session's exposure verdict can come from a rejected frame

- Where: `src/LightsCheck.Core/Analysis/SeriesAnalyzer.cs:826-838` (the sample);
  `src/LightsCheck.Core/Report/SeriesReport.cs:1392-1401` (`Exposure`).
- What happens: the shot/read ratio is measured on three frames, the first, the middle and the
  last measured, whatever their class. The summary's verdict is `GOOD` or `MARGINAL` by the
  weakest channel of the three. R-085 says a frame with a short exposure "counts for nothing";
  the sample was not changed with it.
- Case: 85 lights of 120 s whose red channel is at shot/read 11, and a last frame of 0.2 s,
  cut when the intervalometer was stopped. Frame 85 is `Bad` (short exposure) and is the third
  sample; its sky is about 0, so its ratio is about 0. The summary reads `exposure : MARGINAL`
  for the session, and its figures are the short frame's (CR-029 is the text that follows).
- Should: the sample taken among the frames that stay in the stack.
- Test: none; `R024_ReadNoiseFromTheBiasesGivesTheSwampingRatios` asserts only a ratio above 3.
  A series fixture with biases and a short last frame, asserting the summary's verdict.
- Evidence: confirmed (the logic was read; the exact figures need the test).
- Status: fixed 9a7b8234 (the sample leaves out the frames too short to stack and keeps the trailed ones, the owner's answer of 2026-11-13: ADR 0047; the `NaN %` of CR-029 stays open)

### CR-002 Saturated stars vote on focus and elongation under a bright sky

- Where: `src/LightsCheck.Core/Analysis/FrameAnalyzer.cs:118-120` (`Analyze`), with
  `Th.ShapePeakMaxFraction` (`Thresholds.cs:71`).
- What happens: the stars measured for shape are those with
  `c.Peak < Th.ShapePeakMaxFraction * top`. `Peak` is the peak after the sky was subtracted
  (`Components.cs:166`) and `top` is white minus black, so a clipped pixel has
  `Peak = top - sky` and is left out only while the sky is under 20 % of the scale. The
  constant's comment says "so a saturated star does not vote on shape". `Component.PeakRaw`,
  which holds the peak before subtraction and already counts the saturated stars (line 114), is
  not asked.
- Case: a D3300 frame with `top` 3945 and a green sky of 986 ADU above black (25 %: light
  pollution, the moon, a long exposure). A clipped star has `Peak` 2959, under 3156, and
  passes; clipped stars are the brightest, so they fill the 400 measured places first. The
  reviewer's replica of `StarShapes.Measure`, true FWHM 3.06 px: a star clipped at twice the
  scale reads 3.27, at five times 3.73, at twenty times 4.47.
- Should: no saturated star among the shapes, whatever the sky.
- Test: `R016_TheRegistrationListHoldsTheBrightestStarsByFluxSaturatedIncluded` asserts the
  saturated star is not in `Shapes`, at a sky of 150 of 4095; the same test with
  `BackgroundG = 900` reproduces it.
- Evidence: confirmed. The prototype has the same formula (`prototype/analyze.py:136`) and the
  Veil sky is 128, so the validation figures do not move.
- Status: fixed a487d963 (a star with a raw pixel at the top of the scale is left out of the shapes, the test of the saturated count)

### CR-003 With FITS lights in the folder itself, `result.fit` is counted as a light

- Where: `src/LightsCheck.Core/Session/SessionFolder.cs:103-109` (`Open`), `:143-145`
  (`FramesIn`).
- What happens: without a `lights\` subfolder the folder itself is the lights folder, and every
  file with a FITS extension in it is a light. ADR 0004 and the summary of `Open` say the
  stacked results at the root "must never pass for lights"; that holds only when `lights\`
  exists or the lights are raws.
- Case: a folder with `light_0001.fit` to `light_0050.fit` and Siril's `result.fit`, no
  `lights\`; the README allows both ("or the frames directly in the folder", `result.fit` at
  the root). Out: 51 lights. Frame 51 is the stack: its exposure, time, stars and sky enter the
  total exposure, the pauses and the series, and the same file is the plate-solve source.
- Should: 50 lights.
- Test: none; `R002_WithoutALightsSubfolderTheFolderItselfHoldsTheLights` has no other FITS at
  the root. `SessionFixture.WithLights(2, inSubfolder: false)` plus a `result.fit`, asserting
  two lights.
- Evidence: confirmed for the listing. How far the figures move depends on what Siril writes in
  the stack's header.
- Status: fixed 6267276e (a stack is told by its header, `STACKCNT` or `NCOMBINE` above 1, the owner's answer of 2026-11-13: ADR 0048)

### CR-004 After a meridian flip no frame is aligned, and a weak fit is accepted as good

- Where: `src/LightsCheck.Core/Analysis/Registration.cs:108-111` (the only acceptance test),
  `:135-164` (`Vote`).
- What happens: a frame is aligned by a vote for one shift, within 2000 px, then a fit. A fit
  is good when 10 stars of 600 match; the residual and the share of stars matched are never
  judged.
- Case 1, read: a frame turned by 180 degrees (a meridian flip on an equatorial mount) has no
  single shift, so the vote finds none and the frame is `could not be aligned`. Every frame
  after the flip is so, and the dithering, the drift, the pattern and the walking-noise verdict
  describe the first part of the night only. No document mentions the flip.
- Case 2, the reviewer's replica (600 random stars on 6016 x 4016, 8 seeds): up to 0.5 degrees
  of rotation all correct; at 0.7 degrees one of 8 returned `Ok` with 14 matches and a wrong
  rotation; at 1.0 degree five failed and one was `Ok` and wrong. A wrong `Ok` frame carries a
  wrong position: two moves that did not happen.
- Should: a fit that matches a small share of the stars fails and says why; a flipped frame is
  aligned, or the report says the session was flipped and what it covers.
- Test: `R018_RecoversShiftAndRotationWithinATenthOfAPixel` stops at 0.2 degrees. A theory at
  0.7 and 1.0 degrees asserting "correct or failed, never `Ok` and wrong", and one at 180.
- Evidence: to measure (case 1 follows from the reading; case 2 is the replica's, whose
  tie-breaking differs from the C#).
- Status: fixed c8d9a7df (a fit on under a fifth of the stars fails; the frames after a meridian flip are said, in their reason and in a `meridian flip` row, not measured, the owner's answer of 2026-11-13: ADR 0049, R-087; aligning them is a feature of its own)

### CR-005 The sensor pattern reads stars as hot pixels when the frames barely moved

- Where: `src/LightsCheck.Core/Analysis/PatternAnalyzer.cs:180` (`Analyze`).
- What happens: the pattern is the median of the frames' central crops, pixel by pixel. The
  only guard against stars is that fewer than half the frames sit within 3 px of one place
  (R-046, ADR 0015: "so no pixel sees a star in half the frames"). That holds for a star no
  wider than 3 px.
- Case: 40 frames without dithering, drifting 0.17 px a frame, 6.6 px in all. The 3 px clusters
  hold 18, 18 and 4 frames, so the pattern is measured. A star 4.2 px wide covers its centre
  pixel in 24 of the 40 frames, and the median there is the star. The reviewer's replica (a
  256 px crop, 25 stars, no hot pixel planted): 806 pixels above the hot threshold, about
  13,000 "hot pixels per million"; none without the stars.
- Should: refused as `FramesDidNotMove`, or the spread judged against the stars' width, which
  the session knows.
- Test: the two R046 tests move a 3 px star by 4 px a frame, or do not move at all. Forty crops
  with a 7 px block stepping 0.17 px.
- Evidence: to measure (the guard was read; the figures are the replica's).
- Status: fixed 47f3e89f (the pattern is refused when half the frames sit within three times the stars' width, the owner's answer of 2026-11-13: ADR 0050; V-013's pattern is no longer measured)

### CR-006 The pattern's strength is too high on a short session

- Where: `src/LightsCheck.Core/Analysis/PatternAnalyzer.cs:188-197`, `:213-215`.
- What happens: the noise left in the median is estimated from the difference between the
  median of the odd frames and that of the even ones, by
  `noiseShare = 1 / n / (1 / odd + 1 / even)`, whose comment assumes the median of any count has
  the same constant. `Raster.Median` averages the two middle values on an even count, so an
  even count's median is tighter than an odd one's, and on few frames the estimate is off.
- Case, the reviewer's replica on pure noise, where the truth is 0 % of one frame's noise:
  7 frames read 16.8 %, 8 read 13.6 %, 12 read 10.1 %, 20 read 6.6 %, 32 read 3.9 %, 87 read
  1.6 %; 10 frames read 0.0 % because both halves hold 5 and the subtraction is clamped. A
  perfect sensor on 12 frames would print about 10 %, the figure of the Veil session's real
  pattern.
- Should: about 0 % on noise alone, whatever the count.
- Test: the R046 test accepts a pattern sigma of 0 to 2 on 12 frames and explains the rest by
  "the pattern's few pixels". Pure-noise crops, 8 and 10 frames, asserting about 0.
- Evidence: to measure (the formula was read; the figures are the replica's). No verdict reads
  this figure; the validated sessions (87, 82 and 32 frames) barely move.
- Status: fixed 65dadaae (the noise left in the frames' median is scaled by the variance of a Gaussian median for each count of frames, even counts included: ADR 0051; noise alone reads 0 to 7 % on 7 frames and 0 to 2 % on 87)

### CR-007 A star wider than about 5 px is read too narrow, so a soft frame can pass

- Where: `src/LightsCheck.Core/Analysis/StarShape.cs:85-148` (`Measure`); `Thresholds.cs:77`
  (`MomentRadius = 6`), `:86` (`MaxFwhm = 15`); the filter at `FrameAnalyzer.cs:123`.
- What happens: the width is taken from the moments inside a circle of 6 px, whatever the star.
  The reviewer's replica on clean Gaussians: a true 3 px reads 3.00, 5 reads 4.81, 7 reads
  5.78, 10 reads 6.41, 15 reads 6.74. The most the formula can give is 9.99 px, so the test
  `s.Fwhm <= Th.MaxFwhm` is always true.
- Case: a session at 5 px (a longer focal length, smaller pixels) with one frame at 7 px, 1.4
  times wider and so soft by `Th.SoftFwhmFactor` (1.3). It reads 5.78 / 4.81 = 1.20 and is not
  flagged.
- Should: the owner's call. The radius is R-014's and the prototype's; the comment in
  `FrameMeasurement.cs:78-79` ("nothing here depends on the camera, the lens or the session")
  does not hold for it.
- Test: `R014_ARoundGaussianGivesItsFwhmAndElongationOne` measures one width, 3.06 px. A star
  of sigma 3 (7.06 px) expecting 7 within 0.3.
- Evidence: owner (the arithmetic is certain; the window is a requirement).
- Status: fixed 49db7b96 (the 6 px window stays, since on real frames a wider one reads the sky and not the star, and FOCUS says the limit in a `wide stars` row when the session's stars read wider than 4.5 px, the owner's answer of 2026-11-14: ADR 0052, R-088; the 4.5 px, in place of the 5 px of that commit, came with the fixes after the review of the branch; a width that is right above 4 px needs another estimator, a feature of its own, which the owner asked for after the merge and ADR 0056 records)

### CR-008 A Fujifilm X-Trans raw is measured as a monochrome frame

- Where: `src/LightsCheck.Core/Imaging/RawReader.cs:302-317` (`Decode`, an adapted file);
  `Imaging/CfaPattern.cs:106`; `Analysis/Raster.cs:84-87`.
- What happens: `Decode` refuses only a raw without a 16-bit mosaic. For an X-Trans sensor
  libraw gives a mosaic and `CfaPattern.FromLibRaw` gives no colour order, which
  `Raster.InterpolateGreen` reads as "a mono or debayered frame".
- Case: `lights\DSCF0001.RAF` from an X-T3 (`.raf` is in the extension list). The stars, the
  width and the sky noise are measured on pixels that alternate red, green and blue, and the
  four "channels" are mixes of colours named `CFA[0,0]` and so on.
- Should: refused, naming the sensor, as a Foveon raw is; or read for what it is.
- Test: none for the consequence; `CfaPatternTests.RefusesTheNonBayerMasks` pins the null order.
- Evidence: confirmed for the path; the figures need a run on such a raw. FlatCheck's CR-033 is
  the same gap in its reader, with another symptom.
- Status: fixed 191549ab (a raw with a colour mosaic that has no Bayer order is refused, naming X-Trans: ADR 0053; shown and tested on a generated DNG with a 6 x 6 mosaic, no camera's X-Trans raw was at hand)

### CR-009 A DNG whose black level is a 2 x 2 pattern is read with black 0

- Where: `src/LightsCheck.Core/Imaging/RawReader.cs:336-341` (`Decode`, an adapted file).
- What happens: the black level is `color.black` plus `color.cblack[0..3]`. By the reviewer's
  reading of LibRaw 0.21.1 (`tiff.cpp`, `identify.cpp`, `unpack.cpp`), a DNG with
  `BlackLevelRepeatDim 2 2` keeps its four values in `cblack[6..9]`, with `black` and
  `cblack[0..3]` at 0; folding them is `adjust_bl()`, which `unpack()` does not call. The
  comment "the sum is what rawpy reads" is then not the whole of what rawpy does.
- Case: a DNG with `BlackLevel 64 64 64 64` and no masked area (Pentax, Leica, Ricoh, a phone,
  a file from Adobe DNG Converter). Out: `scale 0 .. 16383 ADU (libraw (exact))`, and every
  level "above black", the sky, the saturation margin, carries a pedestal of 64.
- Should: black 64.
- Test: none; no gate test decodes a raw (CR-058). The black as a function of
  `(black, cblack[])`, asserted on `cblack[4] = cblack[5] = 2, cblack[6..9] = 64`.
- Evidence: to measure: `--frame 1` on such a DNG with the Debug log on prints the black libraw
  gave; exiftool shows the file's. FlatCheck's reader most likely has the same lines.
- Status: fixed 78033d84 (the repeat pattern of blacks is summed with libraw's common and per-colour blacks, per position; ADR 0055; shown and tested on generated DNG files, the first raws the gate decodes; FlatCheck's reader was not looked at)

### CR-010 Frames are read in file-name order as if it were time order

- Where: `src/LightsCheck.Core/Session/SessionFolder.cs:146` (`FramesIn`, the sort);
  `Analysis/CadenceAnalyzer.cs:87-97`; `Analysis/SeriesAnalyzer.cs:598-604` (trends),
  `:644-651` (rotation), `:574` (jump neighbours).
- What happens: the frames are sorted by name (R-002). The cadence takes its start from the
  first frame of the list and its end from the latest, and each pause against the frame before
  in the list; the trends, the rotation and the dithering read the same order.
- Case: a camera counter that passes from 9999 to 0001 during the night, or names not padded
  (`Light_999.fits`, `Light_1000.fits`). Three 120 s frames 150 s apart, named `DSC_9999`,
  `DSC_0001`, `DSC_0002`: the span is 270 s for 360 s of exposure, the line reads `the camera
  was exposing 133 % of the time`, and a row shows the pause `-420 s`. On a whole session the
  "first 10" good frames are the last of the night, so a width that rose 20 % reads as a fall
  and dew is not suspected; the step across the wrap is a large move without a pause.
- Should: read in the order of the times when they contradict the names, or a line that says
  the names and the times disagree.
- Test: none; every fixture is in time order. `CadenceAnalyzer.Analyze` on the three frames
  above; `R023_DewIsSuspectedWhenFwhmAndHaloRiseTogether` with the late frames first by name.
- Evidence: confirmed.
- Status: fixed 804e4a14 (the order stays the names' and the session lines carry a `WARNING` naming the first two files out of order, the owner's answer of 2026-11-14: ADR 0054, R-089; taking the frames in the order of their times is a feature of its own)

### CR-011 The corner shift uses the first aligned frame's exposure

- Where: `src/LightsCheck.Core/Analysis/SeriesAnalyzer.cs:653-657`.
- What happens: `double exposure = firstReg.Info.ExposureSeconds ?? 0;` where every other
  figure of the series uses the session's median exposure.
- Case a: FITS lights with a time and no `EXPTIME`: the row reads `a star in the corner moves
  0.00 px over one exposure` while the rate beside it is known. Case b: the first frame is a
  0.2 s test shot among 120 s frames: the figure is 600 times too small.
- Should: the median exposure, and no figure when none is known.
- Test: `CornerShiftPerExposurePx` is asserted in no unit test.
- Evidence: confirmed (the record's own comment says "0 when unknown"; R-023 does not).
- Status: open

### CR-012 A trailed frame with a short exposure votes for the RA axis

- Where: `src/LightsCheck.Core/Analysis/SeriesAnalyzer.cs:667-669`.
- What happens: the frames that vote for the direction of the trails are those flagged
  `Trailed` with a coherent angle; a frame left out for its short exposure keeps its `Trailed`
  flag (lines 509-513) and votes.
- Case: the fixture `SeriesAnalyzerTests.ShortAndTrailed()`: a 120 s frame and a 0.2 s frame,
  both at elongation 1.7 and 88 degrees. Two votes reach `Th.TrailAxisMinFrames`, so the RA
  axis is Y "from the direction of the trails", one vote being the 0.2 s frame's.
- Should: the owner's call: R-085 can be read as taking the short frame out of every session
  figure, or as leaving a trailed one its trail.
- Test: `R085_ATrailedShortFrameIsListedOnceAndAMarginallyTrailedShortOneInNeitherRow` uses the
  fixture and never asserts the axis.
- Evidence: owner.
- Status: open

### CR-013 A thin mark of 251 to 400 px is counted as a trail and as a star

- Where: `src/LightsCheck.Core/Analysis/FrameAnalyzer.cs:95-111` (`Analyze`).
- What happens: a trail is a long thin component of more than `Th.TrailMinArea` (250) pixels; a
  star is any component of 5 to `Th.MaxStarArea` (400) pixels. Nothing takes the trails out of
  the stars. R-015 says of a trail "it is not a star".
- Case: a piece of a faint satellite trail 3 px wide and 100 px long at 45 degrees, area about
  300. It is a trail, and also one star too many in the count, a point in the registration
  list, and, when bright enough, a shape of very high elongation that votes on the angle.
- Should: a trail and nothing else.
- Test: `R015_ALongThinStreakIsATrailNotAStar` draws a 671 px streak, which the area limit
  alone keeps out. A streak of about 90 px, with the star count asserted.
- Evidence: confirmed (the prototype does the same, `analyze.py:132`); the effect is small.
- Status: fixed e0ec7248 (a trail is taken out of what may be a star before the star list, whatever its area, so it is not counted, measured or kept for registration: R-015, ADR 0059; two such pieces on each of frames 1, 5 and 23 of the Smida session, and one on frame 3, were both until then)

## Severity 2: a crash, a hang, or a file written where it must not be

### CR-014 The report files can be written into `lights/` or `biases/`

- Where: `src/LightsCheck.Core/Report/SeriesOutputs.cs:49-66` (`WriteAll`); the callers
  `src/LightsCheck.Cli/Program.cs:210-214` and `src/LightsCheck.Wpf/MainWindow.xaml.cs:104-116`
  with `ViewModels/MainViewModel.cs:533-551` (`Export`).
- What happens: `WriteAll` writes into the folder it is given. Its comment says "Never inside
  lights/ or biases/ (ADR 0002): the caller names the folder", and no caller checks. R-028 and
  R-032, both `implemented`, say "nothing is written into the session's `lights/` or
  `biases/`", and the rule "Read-only input" says it of the tool.
- Case 1: `lightscheck S --out S\lights` leaves `lightscheck-report.txt`,
  `lightscheck-frames.csv` and `lightscheck-rejected.txt` among the raws.
- Case 2: in the window the dialog opens at the session root; a double click on `lights` and
  the three files are there.
- Case 3: a session whose frames sit in the folder itself: the window proposes that folder, the
  one that holds the frames.
- Should: refused with a line that says why, in the console and in the window. No frame is
  touched and nothing is lost, which is why this is here and not in severity 1; it is the one
  rule the owner gave with the photos.
- Test: `R028_TheOutputsAreWrittenWhereAskedAndNeverIntoLights` and
  `R032_ExportWritesTheThreeFilesIntoTheChosenFolderOnly` write elsewhere and then check that
  `lights\` did not change, which cannot fail. `vm.Export(f.LightsDir)` and `--out` into
  `lights\`, expecting the error and no new file.
- Evidence: confirmed.
- Status: open

### CR-015 One late failure loses the whole run, after every frame was measured

- Where: `src/LightsCheck.Core/Runs/SeriesRun.cs:228-234` (the registration loop), `:408-441`
  (`ReadWcs`); `Analysis/Registration.cs:196-200`; `src/LightsCheck.Cli/Program.cs:224`;
  `src/LightsCheck.Wpf/ViewModels/MainViewModel.cs:433-449`.
- What happens: the loop that measures the frames catches whatever one frame throws and goes
  on. What comes after it does not. The registration loop has no catch, so one throw there (the
  fit dereferences the null `Stats.Solve` gives for a system it cannot solve) leaves as an
  `AggregateException`. `ReadWcs` catches `IOException` and `InvalidDataException` only, so an
  access refused on one FITS at the session root ends the run, where the read noise beside it
  catches everything.
- Case: a session folder with a `.fit` the user cannot open, of any content. After the minutes
  of measuring, the console prints `ERROR:` and no report. For an exception outside the five
  types both applications catch, the console ends with a stack trace, and the window shows the
  folder's name over an empty area with no message (the design note says so; the log has the
  line).
- Should: a file that cannot be read as a plate solve is skipped; a frame that cannot be
  aligned is `could not be aligned`; the report is written from what was measured.
- Test: none. A FITS at the root that throws on open; the report expected.
- Evidence: confirmed for `ReadWcs`. No input was found that makes the fit throw.
- Status: open

## Severity 3: a wrong text or a wrong display

### CR-016 A wrong pixel size is not marked in the window

- Where: `src/LightsCheck.Wpf/ViewModels/MainViewModel.cs:253` (`PixelIsValid`);
  `src/LightsCheck.Wpf/MainWindow.xaml:75` (the field).
- What happens: `PixelIsValid` is computed and raised, and nothing binds it: a search of `src/`
  finds it in the view-model alone. R-029 says "a blank field is not marked invalid, a wrong
  value is", and ADR 0026 says a wrong value is "marked".
- Case: `3,76` typed with a comma, the decimal sign of a Romanian keyboard. The field looks as
  it always does and the run takes another size; the only trace is the source named on the
  `scale` row.
- Should: the field marked as wrong, as R-029 says.
- Test: `R029_ThePixelInputIsParsedInvariantAndBlankMeansNoneTyped` asserts the property nobody
  binds. The window rendered with `PixelText = "4,5"`, asserting a visible difference.
- Evidence: confirmed.
- Status: open

### CR-017 The exposure verdict and the figure printed beside it disagree at the limit

- Where: `src/LightsCheck.Core/Report/SeriesReport.cs:1398-1401` (`Exposure`), `:481-482`
  (`NoiseRows`).
- What happens: the verdict is taken on the ratio as measured and the text prints it with one
  decimal. The sentence under it says a ratio from 10 up "adds under 5 %", the 5 typed and not
  worked out from `Th.SwampingRatioOk`.
- Case, in the goldens today: `Golden/en_series.txt:17` reads `GOOD - ... shot/read 10.0 (good
  from 10), the read noise adds 5 %`, and `Golden/en_series-classes.txt:18` reads `MARGINAL -
  ... shot/read 10.0 (good from 10), the read noise adds 5 %`: the same figures, two verdicts.
  The `GOOD` line says "adds 5 %" where the explanation says "under 5 %".
- Should: a figure that does not contradict its verdict.
- Test: the two goldens pin both lines.
- Evidence: confirmed.
- Status: open

### CR-018 The Romanian tables do not sit under their headers

- Where: `src/LightsCheck.Core/Report/SeriesReport.cs:127` and `:131` (`Write`);
  `Report/InventoryReport.cs:91` (`WriteTable`).
- What happens: the English header is built with the rows' widths; the Romanian one is a
  literal counted by hand. And three cells can be wider than their column.
- Case 1: `Golden/series-classes.txt:76` against row 77: `FWHM` ends one column before its
  figures, and so do `stele`, `fond`, `pauza`, `pas`; `dX`, `dY`, `dithering`, `acelasi loc`
  and `motive` are two off.
- Case 2: `Golden/inventory.txt:16` is 78 characters and its rows 79: `ISO`, `f/`, `focala` and
  `pauza` end one column early.
- Case 3: `la limita` has 9 letters in a column of 8, so every marginal row is pushed one
  column right (`Golden/series-classes.txt:79-81`). A pause of 1000 s or more (a wait for
  clouds) has 6 characters in a column of 5. `10/10 de la 100` has 15 in a column of 14.
- Should: the Romanian headers built as the English ones; columns as wide as their widest cell.
- Test: `SeriesTests.cs:614` and `:722` pin the Romanian header's own literal, never the header
  against a row.
- Evidence: confirmed (counted on the goldens).
- Status: open

### CR-019 A count of one gets a plural word

- Where: `src/LightsCheck.Core/Report/SeriesReport.cs:371` (`SummaryRows`), `:395`
  (`FocusRows`), `:979` (`PatternRow`); `Runs/FrameRun.cs:58`; `Report/InventoryReport.cs:73`.
- Cases:
  - `Golden/series-classes.txt:20`: `cadre miscate : 2 slabe, 3 la limita, 7 bune, 1 necitite`;
    one bad or one good frame gives `1 slabe`, `1 bune`. ADR 0036 fixed this in the REJECTED
    heading; the summary row kept it.
  - With two or three good frames: `first / last 1 good frames`, `primele / ultimele 1 cadre
    bune`; with one: `first / last 0 good frames`.
  - One dark: `1 darks in darks\`, `1 dark-uri in darks\`.
  - `--frame 2` on a session of one light: `the session has 1 frames`, `sesiunea are 1 cadre`.
  - Twenty unreadable frames in Romanian: `20 cadre nu s-au putut citi`, without the `de` of
    R-082.
- Should: the singular for one, and `de` from twenty.
- Test: `SeriesTests.cs:179` pins `1 bune`.
- Evidence: confirmed.
- Status: open

### CR-020 The `marginal (elongation between ...)` row lists frames that are not

- Where: `src/LightsCheck.Core/Report/SeriesReport.cs:528` (`TrailRows`).
- What happens: the row's label states a bound on the elongation and its list is every marginal
  frame, whatever made it marginal. R-085 says the row lists them all on purpose; the label
  still states the bound.
- Case: `Golden/en_series-classes.txt:45`: `marginal (elongation between 1.35 and 1.60): 3, 4,
  5`. Frame 4 is marginal for wide stars, at elongation 1.00, and frame 5 for too few stars, at
  1.01; both stand under TRAILED FRAMES.
- Should: the owner's call: the label, or the list.
- Test: the golden pins it.
- Evidence: owner.
- Status: open

### CR-021 A small negative figure prints as a negative zero

- Where: `src/LightsCheck.Core/Report/RunReport.cs:76` (`F`), the one formatter of the reports.
- Cases, in the goldens today: `halo : -0.000` (`Golden/en_frame.txt:11`); `(-0 %): the focus
  held the whole session`, `the stars' width -0 %`, `the image turned by -0.0' in all`
  (`Golden/en_series.txt:16`, `:18`, `:50`); `Y -0.00 px/min = +1.8 / -0.0 "/min`
  (`Golden/en_series-classes.txt:49`). By the same formatter: a pause of -0.2 s prints `-0 s`
  (a raw's time is in whole seconds, its exposure is not), and stars trailed along the image's
  X axis print `angle -0 degrees`.
- Should: the owner's call. R-036 and R-078 quote `(-0 %)` in their examples and
  `SeriesTests.cs:356` and `:639` assert it; FlatCheck settled the same question for itself in
  its ADR 0092.
- Evidence: owner.
- Status: open

### CR-022 Stop shows nothing until the frames in flight finish

- Where: `src/LightsCheck.Wpf/ViewModels/MainViewModel.cs:517-526` (`CancelPending`),
  `:404-415` (the progress callback).
- What happens: Stop only cancels the token. The wait screen, its line and the two Stop buttons
  stay as they are until the run returns, and the run lets the frames already started finish,
  whose progress still reaches the window.
- Case: Stop pressed at `Measuring the frames: 12 / 87` with 4 threads on raws. For the seconds
  a frame takes, the line goes on to 13, 14, 15, 16 / 87 and Stop looks as if it did nothing.
- Should: a line that says the run is stopping, at once.
- Test: none; the three stop tests cancel before the run starts (CR-060).
- Evidence: confirmed.
- Status: open

### CR-023 Changing an input after a result changes nothing on screen

- Where: `src/LightsCheck.Wpf/ViewModels/MainViewModel.cs:285-307` (the arrows' setters save
  the setting and nothing else), `:544` (`Export` writes the run's result).
- Case: a result is shown with EAST `→`; the user picks `←` and presses `Write the files...`.
  The top bar shows `←`; the Frames column, the bars' tips and the CSV's `move_arrows` still
  use `→`. The arrow applies only when the folder is chosen again and the whole run repeated,
  though the tip itself says the figures do not change. The same holds for the pixel and
  thread fields.
- Should: the owner's call. R-041 says the selectors are "read when a folder is opened", so
  the timing is deliberate; what is missing is any sign of it.
- Test: none changes an input after a result.
- Evidence: owner.
- Status: open

### CR-024 The Frames tab cuts the `same spot` cell

- Where: `src/LightsCheck.Wpf/Views/FramesView.xaml:54` (the column, 100 px), `:71` (the cell),
  `:28-33` (the cell style's 8 px margin).
- What happens: the cell has 92 px, which holds 13 letters of Consolas 12 px in the window's
  Display mode (7 px a letter, R-079's own figure). It has no trimming mark and no tooltip.
- Case: R-034's own example, the Veil session's frames 61 to 70: frame 70's cell is `10/10 de
  la 61`, 14 letters, so its last digit is cut and the run seems to start at frame 6. In
  English the cell is cut from 14 letters too (a first frame of 100 or later).
- Should: the whole cell, as the console prints it (padded to 14 there, CR-018).
- Test: none; `R079_EveryTrailReasonFitsTheReasonsColumn` measures the reasons column alone.
- Evidence: to measure (a screenshot of the Frames tab on such a session; the arithmetic holds
  if 7 px a letter does).
- Status: open

### CR-025 The Dithering tab cuts its table cells at 100 px

- Where: `src/LightsCheck.Wpf/Views/DitherView.xaml:124`.
- What happens: every cell of the dithering tables is 100 px wide, without wrapping, trimming
  or a tooltip: 14 letters.
- Case: the first row of the "how far" table on the Veil session, `13.29 px = 81.2"`, has 16
  letters: the last decimal and the arcsecond mark are cut. In Romanian a session with one move
  shows `o singura miscare` (17 letters) cut.
- Should: the whole cell.
- Test: none; `R037_...` compares strings.
- Evidence: to measure (the text is wider than the cell by reading; a screenshot shows what is
  lost).
- Status: open

### CR-026 The charts' tick labels are rounded more coarsely than their gridlines

- Where: `src/LightsCheck.Wpf/Presentation/Charts.cs:118-122` (`Num`), `:268-289`.
- What happens: the five gridlines stand at equal parts of the range, which is padded by 8 %,
  so they are not at round values; the label's decimals are chosen from the step's size alone.
- Case A, elongation: the lines at 1.000, 1.162, 1.324, 1.486 and 1.648 are labelled `1.0 1.2
  1.3 1.5 1.6`, so the red line at 1.60 is drawn under the gridline labelled 1.6, and a frame
  at 1.62 looks under the limit.
- Case B, steps and nearest frame: the lines at 0, 2.7, 5.4, 8.1 and 10.8 are labelled `0 3 5 8
  11`; the 5 px guide sits under the gridline labelled 5.
- Case C, a float FITS: a background of 0.0100 to 0.0110 gives `0.010 0.010 0.011 0.011 0.011`;
  `Num`'s comment, "so two ticks never read the same", is false there.
- Should: gridlines at the values their labels say.
- Test: `TheChartsRenderWithGapsAndGuides` checks only that nothing throws.
- Evidence: confirmed (arithmetic on the code).
- Status: open

### CR-027 The nearest-frame chart carries the step chart's `move >= 5 px` line

- Where: `src/LightsCheck.Wpf/Views/DitherView.xaml:84`: `Points="{Binding NearestPoints}"
  Guides="{Binding StepGuides}"`.
- Case: every session. The chart of distances to the nearest other frame shows an amber dashed
  line labelled `move >= 5 px`, `miscare >= 5 px`, over figures that are not moves. Its caption
  explains the 10 px line only, and R-030 puts the move line on the step chart.
- Should: the 10 px line alone.
- Test: `MainViewModelTests.cs:250-254` checks `StepGuides` only.
- Evidence: confirmed.
- Status: open

### CR-028 The CSV carries single-precision noise in the exposure and the f-number

- Where: `src/LightsCheck.Core/Imaging/RawReader.cs:423-426`, `:453-455`
  (`FillShootingInfo`, an adapted file); `Report/SeriesOutputs.cs:159`.
- What happens: libraw's shutter, aperture, focal length and ISO are single-precision (checked
  by the reviewer on the shipped library) and are widened to double as they are; the CSV writes
  a number with every digit it needs to read back.
- Case: a raw session at 120.2 s and f/2.8. The CSV's cells read `120.19999694824219` and
  `2.799999952316284`, and the pause column inherits it (`4.8000031` for 4.8). The console and
  the window round and hide it; a FITS session is clean, which is why no golden shows it.
- Should: `120.2` and `2.8`.
- Test: none; no gate test decodes a raw (CR-058). The conversion as a helper: `120.2f` must
  give exactly `120.2`.
- Evidence: to measure (one `--out` run on a raw session shows the cells).
- Status: open

### CR-029 The summary can say `the read noise adds NaN %`

- Where: `src/LightsCheck.Core/Report/SeriesReport.cs:1398` (`Exposure`);
  `Analysis/NoiseAnalyzer.cs:257`.
- What happens: the share the read noise adds is not a number when a channel's shot noise is
  not above its read noise, while the ratio stays a number. `NoiseRows` writes `-` for it (line
  474); `Exposure` does not.
- Case: CR-001's short frame, or float FITS lights with raw biases: `shot/read -0.3 (good from
  10), the read noise adds NaN %`.
- Should: no figure where there is none.
- Test: `SeriesReport.SummaryRows` on a result whose sample holds a ratio of -0.3.
- Evidence: confirmed.
- Status: open

### CR-030 On a float FITS every level prints as 0

- Where: `src/LightsCheck.Core/Report/FrameReport.cs:44-56`; `Report/SeriesReport.cs:179`,
  `:455`, `:469`, `:474`, `:1385`.
- What happens: a FITS without an integer `BITPIX` is given the scale 0..1 (R-017, as Siril
  writes a calibrated light), and the levels are printed with no decimal.
- Case: the frame of `R012_AFloatScaleFrameHasNoSpuriousSaturation` through `--frame`: `R 0
  (saturated 0, min 0) G1 0 ...`, `green background : median 0, centre 0, corners 0 .. 0`,
  `noise sigma 0.0`. In a series the `bg` column is `0` on every row and the sky reads `0 -> 0
  ADU`. The measurement is right; the text shows nothing of it.
- Should: decimals that follow the scale.
- Test: none writes a report for a float frame.
- Evidence: confirmed.
- Status: open

### CR-031 `--help` says `--pixel` comes before the file's `XPIXSZ`

- Where: `src/LightsCheck.Cli/Program.cs:294-295` (`WriteUsageEnglish`), `:325-326`
  (`WriteUsageRomanian`).
- What happens: the help reads `--pixel UM the pixel size in microns; without it, the files'
  XPIXSZ, else ...`. `PixelPitch.Choose` takes the file's `XPIXSZ` first and the typed value
  second, as R-066 and the README say.
- Case: `lightscheck S --pixel 4.30` on FITS lights that carry `XPIXSZ = 3.92`: every
  arcsecond figure uses 3.92, and only the source on the `scale` line says so.
- Should: the help in the order of R-066.
- Test: the goldens `help.txt` and `en_help.txt` pin the sentence.
- Evidence: confirmed.
- Status: open

### CR-032 Hidden and sidecar files are taken as frames

- Where: `src/LightsCheck.Core/Session/SessionFolder.cs:143-145` (`FramesIn`).
- What happens: every file of the folder with a raw or FITS extension is a frame; neither the
  name nor the attributes are looked at.
- Case: a card a Mac touched leaves `._DSC_0001.NEF` beside every file. `lights\` with 87 raws
  gives 174 lights, the first 87 unreadable: a `WARNING` line, exit code 1, `--frame 1`
  pointing at a junk file, the junk names in the rejected list. In `biases\` the first two
  "biases" are junk, so the read noise is not measured and the exposure verdict has none.
- Should: 87 lights.
- Test: none. A `._DSC_0001.NEF`, or a file with the Hidden attribute, in the folder of
  `R002_RawsWinOverFitsInTheSameFolderAndSortByName`.
- Evidence: confirmed (the reviewer's listing returned such a file, first).
- Status: open

### CR-033 A frame without a focal length prints `- mm`

- Where: `src/LightsCheck.Core/Report/InventoryReport.cs:104` (`WriteTable`).
- Case: a telescope, or a lens without a chip: the row ends `   - mm`, the unit written with
  no value. R-003 and the design note say a missing field prints `-`.
- Should: `-`.
- Test: none; every fixture carries a focal length.
- Evidence: confirmed.
- Status: open

### CR-034 `that is 0 s of exposure` when the frames carry no exposure

- Where: `src/LightsCheck.Core/Report/SeriesReport.cs:1571-1584` (`Keep`).
- Case: FITS lights without `EXPTIME`: `65 frames stay, that is 0 s of exposure`, where the
  header's first line says `unknown (the frames carry no time or no exposure)`.
- Should: no duration where none is known.
- Test: none.
- Evidence: confirmed.
- Status: open

### CR-035 Bars hang outside the plot on a short session; two guide labels overlap

- Where: `src/LightsCheck.Wpf/Presentation/Charts.cs:275`, `:300`, `:309` (the bars);
  `:336-343` (the guides' labels).
- Case 1: the first frame's bar is centred on the y axis and the last on the right edge, so
  half of each is outside the plot. With 8 frames that half is about 25 px: the first bar is
  painted over the tick labels and the last runs past the chart. With 2 frames each bar is a
  third of the plot wide.
- Case 2: on the step chart, with a largest step above about 58 px (a re-centring, a flip) the
  5 px and 10 px lines are 4 px apart and the second label is painted over the first.
- Should: bars inside the plot; labels that do not cover each other.
- Test: `TheChartsRenderWithGapsAndGuides` asserts nothing about position.
- Evidence: to measure (the geometry was read; a screenshot settles the look; the smoke tests'
  `tab-dither.png` has 8 frames).
- Status: open

### CR-036 With the camera turned 25 to 65 degrees no trail reads `on RA`

- Where: `src/LightsCheck.Core/Analysis/SeriesAnalyzer.cs:887-897` (`TrailAxisOf`);
  `Analysis/Wcs.cs:49` (`RaAxis`).
- What happens: a trail's angle is compared with the image axis nearest to RA, within a
  tolerance, though the plate solve read a moment before holds the true direction of RA.
- Case: a solve turned by 35 degrees. A frame trailed exactly along RA has a trail at 35
  degrees: 35 from one image axis and 55 from the other, so it reads `oblique`, and so does
  the `trail direction` row.
- Should: the owner's call: R-025 and R-033 state the rule on the image's axes, and the code
  follows them.
- Test: `R033_TheTrailAxisIsTheImageAxisWithinTheTolerance` pins the rule.
- Evidence: owner.
- Status: open

### CR-037 Restore Down can put the title bar above a low screen

- Where: `src/LightsCheck.Wpf/MainWindow.xaml:28-31` (`Height="820"`, `WindowState="Maximized"`,
  `WindowStartupLocation="CenterScreen"`).
- Case: a work area lower than 820 units (1366 x 768, or 1920 x 1080 at 150 %). The window
  opens maximised; Restore Down gives 1360 x 820 centred, so its top is above the screen and
  the title bar cannot be reached with the mouse.
- Should: restored bounds inside the work area, as the About window is fitted (R-076).
- Test: none.
- Evidence: to measure (a run on such a display; it rests on the centring not clamping).
- Status: open

### CR-038 `the biases are missing` and `the sky could not be measured` when they are there

- Where: `src/LightsCheck.Core/Report/SeriesReport.cs:1384-1390` (`Exposure`), `:455-467`
  (`NoiseRows`); `Runs/SeriesRun.cs:364-390` (`ReadNoise`).
- Cases:
  - One bias: the header reads `biases : biases\ (1 frame .NEF)` and the summary `the read
    noise could not be measured (the biases are missing)`.
  - Twenty biases whose first is broken or of another size: the same sentence, and `at least
    two biases are needed in biases\`; both false.
  - Fewer than two good frames (a windy night): `exposure : GOOD - the sky could not be
    measured`, and `sky background ...: - -> - ADU`, while every frame's sky is in the table.
    The sentence reads the trend of the good frames.
- Should: the sentence that names what is missing.
- Test: only the fixture without biases.
- Evidence: confirmed.
- Status: open

### CR-039 The series header has no `WARNING` line for a light whose pixels cannot be read

- Where: `src/LightsCheck.Core/Report/InventoryReport.cs:68-70` (`WriteHeader`);
  `Runs/InventoryRun.cs:32`; `Runs/SeriesRun.cs:213-215`; called from
  `Report/SeriesReport.cs:105`.
- What happens: the line is written when a frame's metadata carries an error. A series keeps
  the measurement's error on the frame, not on its metadata, so a light whose header reads and
  whose pixels do not has none there.
- Case: a FITS cut in half during a copy (R-008's own example). The run exits 1 and the table
  lists the frame as unreadable, but the session lines carry no `WARNING : 1 frame could not be
  read`, and its 120 s are inside the total exposure. With 100 junk bytes the line is there.
  The window's session card is made of the same lines.
- Should: the line whenever the run counts a frame as unreadable.
- Test: `R071_AFrameThatCannotBeMeasuredButWhoseMetadataIsReadSaysSo` checks the log alone.
- Evidence: confirmed.
- Status: open

### CR-040 A figure rounded at a limit contradicts the word beside it

- Where: `src/LightsCheck.Core/Report/SeriesReport.cs:1264-1267` (`Reason`), `:1514`
  (`StreakClause`), `:1541-1544` (`PatternClause`), `:712-715` (`RaDriftRow`).
- Cases:
  - Elongation 1.597: `elongation 1.60 (from 1.35 the frame is marginal)`, beside "From 1.60
    the frame is bad"; 1.347 shows `1.35` in the table with the class good.
  - `too few stars: 599, under 599`.
  - A streak of 2.96 px: `streaks of at most 3.0 px (under 3 px they do not show)`.
  - A pattern kept at 1.74 % against 1.72 %: `the pattern adds up in place: 1.7 % ... (fine up
    to 1.7 %)`.
  - An RA drift of 1.34 against 1.31: `1.3"/min; at most 1.3"/min comes from the alignment, the
    rest comes from the tracking`.
- Should: a figure that does not contradict its word.
- Test: none at a limit. `SeriesReport.Reason(new FrameFlag(FlagKind.MarginalTrail, 1.597,
  1.35))`.
- Evidence: confirmed.
- Status: open

### CR-041 One light, or nothing aligned: sentences that do not fit

- Where: `src/LightsCheck.Core/Report/SeriesReport.cs:1454-1457` (`DitherVerdict`), `:601`
  (`PolarReason`), `:1161-1168` (`LongestRow`), `:1597-1600` (`HowOften`);
  `Analysis/Dithering.cs:293-309`.
- Case 1: a session of one light prints `the frames are aligned on frame 1` and `frames
  aligned: 1 of 1`, then `dithering : UNDETERMINED - the frames could not be aligned` and
  `polar alignment : - (the frames could not be aligned)`.
- Case 2: twelve lights of which only the reference aligned print `how often : no move` and
  `longest stretch without a move: frames 1-12 (12 frames in a row, 24 min)`, which reads as
  "the mount never moved". A frame without a position is not a move, so ten clouded frames
  also lengthen the longest stretch, though nothing is known of them.
- Should: sentences that say there was too little to align.
- Test: none.
- Evidence: to measure (a run on one light confirms the first; the lines were read).
- Status: open

### CR-042 The tooltips say `frame 1` when another frame is the reference

- Where: `src/LightsCheck.Wpf/Views/FramesView.xaml:120-121`; `Views/DitherView.xaml:60`.
- Case: `DSC_0001` cannot be read, so the frames are aligned on frame 2 and the session card
  says so. The dX and dY tooltips still read `where it sat relative to frame 1`, `fata de
  cadrul 1`, and the map's caption `relative to the first frame`.
- Should: the frame the card names.
- Test: none.
- Evidence: confirmed.
- Status: open

### CR-043 The progress line can go backwards and stay on `86 / 87`

- Where: `src/LightsCheck.Wpf/ViewModels/MainViewModel.cs:413-414`; `Runs/SeriesRun.cs:218`.
- What happens: each worker counts a frame and then reports the count; the two are not one
  step, and the window applies whatever arrives.
- Case: the last two frames end together and (87, 87) is posted before (86, 87). The line goes
  back to `Measuring the frames: 86 / 87`, the bar under full, for the whole of the alignment
  and the analysis.
- Should: a count that never goes down.
- Test: none.
- Evidence: confirmed for the ordering; it is not frequent.
- Status: open

### CR-044 A raw stamped in the hour the clocks skip comes back an hour off

- Where: `src/LightsCheck.Core/Imaging/RawReader.cs:427-429` (`FillShootingInfo`, an adapted
  file).
- What happens: the raw's time goes through libraw's seconds since 1970 and back to local time.
  The method's comment and `docs/design/imaging.md:111-114` say this gives back the camera's
  clock to the second.
- Case: a camera left on winter time, a session through 03:00 to 04:00 on the last Sunday of
  March. `2026:03:29 03:30:00` does not exist on the PC's clock, so it comes back as 02:30 or
  04:30; the frames of that hour overlap their neighbours, with one pause of about -1 h and
  one of about +1 h.
- Should: the time the camera wrote.
- Test: none. A raw whose time is in the gap (a copy edited with exiftool).
- Evidence: to measure.
- Status: open

## Severity 4: robustness and cleanliness

### CR-045 Options that do not apply are dropped in silence; `--out` takes an option as its folder

- Where: `src/LightsCheck.Cli/Options.cs:126-134`, `:184-192` (`Parse`);
  `src/LightsCheck.Cli/Program.cs:191-202`.
- Cases:
  - `lightscheck S --inventory --out D:\rep`, `--frame 3 --out ...`, `--inventory --pixel 4`:
    the run exits 0, writes no file and says nothing. `--inventory` with `--frame` is refused
    by name.
  - `lightscheck S --out --inventory` runs the whole series and writes into a folder named
    `--inventory`.
  - An option given twice keeps the last.
- Should: refused, naming the option.
- Test: `R004_ABadOptionValueNamesTheOption_Exit1` covers `--out ""` only.
- Evidence: confirmed.
- Status: open

### CR-046 The thread count has no upper bound

- Where: `src/LightsCheck.Wpf/ViewModels/MainViewModel.cs:311`; `src/LightsCheck.Cli/Options.cs:147`;
  `Runs/SeriesRun.cs:186`, `:196`.
- Case: `88` typed for `8`, an 87-frame raw session. As the pool grows, more frames are
  decoded at once than memory holds (a frame takes hundreds of MB); one that fails for lack of
  memory is caught as that frame's error and becomes an unread row, so good frames would be
  listed as unreadable.
- Should: capped, or refused.
- Test: `R029_TheThreadsInputIsAPositiveCountOrTheDefault` pins 3, 0, blank and `x`.
- Evidence: to measure (the pool may grow too slowly to matter).
- Status: open

### CR-047 Every row selected in Frames starts a full decode that is never cancelled

- Where: `src/LightsCheck.Wpf/ViewModels/MainViewModel.cs:351-361`, `:661-709`
  (`LoadCropsAsync`).
- Case: the Down arrow held in the Frames list of an 87-frame raw session. Each row starts a
  decode of its whole raw, with no cancel and no wait; the result of a row no longer selected
  is thrown away only after its decode. The cut-outs of the row the user stopped on arrive
  last.
- Should: the last selected frame decoded, the others not.
- Test: `R031_ALaterSelectionWinsOverAnEarlierDecode` covers which result is shown, not the
  work done.
- Evidence: to measure (the mechanism was read; the wait and the memory need a run).
- Status: open

### CR-048 A failed export leaves a mixed set of files

- Where: `src/LightsCheck.Core/Report/SeriesOutputs.cs:55-65`; `src/LightsCheck.Cli/Program.cs:214-219`.
- Case: the CSV is open in Excel. The report is replaced, the CSV and the reject list stay
  those of the run before, perhaps of another session when the folder is reused. The console
  prints `ERROR:` and no `-> written:` line for the report it did write; only the log has it.
- Also, for the owner: the CSV is UTF-8 without a mark and holds the arrows, which Excel shows
  as broken letters when the file is opened by a double click (the comment states "without a
  BOM" as meant); the report ends its lines the Windows way, the CSV and the reject list the
  Unix way.
- Should: all three files or none; the written ones named.
- Test: `LoggingTests.cs:632` pins the log lines of this case, not the files left.
- Evidence: confirmed (the Excel part needs the file opened there).
- Status: open

### CR-049 After Ctrl+C the console stays on UTF-8 and the log has no last line

- Where: `src/LightsCheck.Cli/Program.cs:73-104` (`Main`).
- What happens: the code page is put back, and the log closed, in `Main`'s `finally`; no
  handler exists for Ctrl+C, which ends the process without running it. The comment above says
  the code page "outlives the process otherwise".
- Case: a series started in cmd.exe on code page 852, Ctrl+C at frame 20: `chcp` answers 65001,
  and the log ends without its `exit` line.
- Should: the code page put back.
- Test: none.
- Evidence: to measure (Ctrl+C during a series, then `chcp`).
- Status: open

### CR-050 A relative folder on the window's command line stays relative

- Where: `src/LightsCheck.Wpf/App.xaml.cs:149-165` (`FolderFromArgs`);
  `ViewModels/MainViewModel.cs:392`; `MainWindow.xaml.cs:107-110`.
- Case: `LightsCheck.Wpf.exe .` started in the session folder. The top bar reads `SIRIL SESSION
  FOLDER  .`, the log `session .: run started`, and the export dialog is given `.` to open at.
  The report and the frames' paths are right, since the core makes the path whole itself.
- Should: the whole path, everywhere.
- Test: `FolderFromArgs` is pinned with whole paths only.
- Evidence: confirmed for the display; what the dialog does with `.` needs a run.
- Status: open

### CR-051 The star moments are summed in one window and divided by another's flux

- Where: `src/LightsCheck.Core/Analysis/StarShape.cs:87-137` (`Measure`);
  `Analysis/FrameAnalyzer.cs:123`.
- What happens: the last pass of the centroid sums the flux in the circle around the centroid
  before its update; the moments are then summed in the circle around the updated centroid and
  divided by that earlier flux. The prototype uses one window for both
  (`prototype/analyze.py:76-80`). And `s.Fwhm <= Th.MaxFwhm` can never be false (CR-007).
- Case, the reviewer's replica on 4000 noisy stars of 3.06 px: at a peak of 21 sigma, 344
  stars differ from the prototype, by up to 0.23 px of width and 0.09 of elongation; at a
  bright peak 18 differ. The medians do not move, which is why the validation figures agree.
- Should: one window, as the oracle.
- Test: `StarShapeTests` have no noise, where the two windows hold the same pixels.
- Evidence: confirmed.
- Status: open

### CR-052 The series: a zero DEC row, a rejected reference frame, a one-sided jump test

- Where: `src/LightsCheck.Core/Runs/SeriesRun.cs:425`, `:223`; `Analysis/Wcs.cs:91`;
  `Analysis/SeriesAnalyzer.cs:726`, `:748`, `:573-594`.
- Cases:
  - A solved header with `PC1_1`, `PC1_2` and no `PC2_*` (what
    `R025_TheRaAxisComesFromAPlateSolvedFitsAtTheRootWhenThereIsOne` writes): only a zero first
    row is refused, so the DEC scale is 0 and every DEC move is 0 / 0. Crafted.
  - The reference every frame is aligned on is the first measured frame (R-018, written before
    R-085): a 0.2 s first frame would be it. Needs a run with such a frame first.
  - The sky's jump is judged against the frames on both sides, and at the two ends of the
    session there is one side only: under a dawn sky rising 10 % a frame, the last two frames
    are flagged and no inner one is. A flag only.
- Evidence: confirmed (the lines were read; the second needs the run).
- Status: open

### CR-053 One frame: a noise of zero, a NaN pixel, a cut-out skipped

- Where: `src/LightsCheck.Core/Analysis/FrameAnalyzer.cs:86-91`; `Analysis/Components.cs:86`;
  `Analysis/StarCrops.cs:103-108`.
- Cases:
  - A frame with over half its pixels clipped to 0 has a noise of 0: the threshold is 0, every
    positive pixel is a component and every star is "bright". No crash.
  - A NaN pixel of a float FITS is not under the threshold, and the smoothing spreads it over
    9 x 9 pixels: an 81 px "star" in the count. Crafted.
  - A corner's cut-out is skipped when its brightest star is 8 to 12 px from the edge, though
    other measured stars of that corner would fit. The comment says so; R-031 does not.
- Evidence: confirmed.
- Status: open

### CR-054 Thresholds typed as numbers in texts

- Where:
  - `src/LightsCheck.Wpf/Views/DitherView.xaml:60`, `:65`, `:72`, `:82` and
    `Views/FramesView.xaml:123`: `10 px`, `at least 3 frames in a row`, `at least 5 px`,
    repeating `Th.CoverageLoose`, `Th.RunMinFrames` and `Th.DitherStep`.
  - `src/LightsCheck.Wpf/Presentation/Charts.cs:391-394`: the map's 10 px grid, "the
    walking-noise distance".
  - `src/LightsCheck.Core/Report/FrameReport.cs:74-75`: `ring 5-10 px / core 3 px`, beside a 40
    that is read from `Th`. (Settled on 2026-11-15 by the change of ADR 0059: the line reads its
    rings, now in star widths, from `Th`. The other items stay open.)
  - `src/LightsCheck.Core/Report/SeriesReport.cs:481-482`: `adds under 5 %` (CR-017); `:817`:
    `never 2 frames`.
  - `src/LightsCheck.Core/Session/SessionFolder.cs:114`: `.fit, .fits, .fts`, beside
    `FitsReader.FitsExtensions`.
- What happens: they agree with `Thresholds.cs` today. The guide lines beside the captions read
  `Th`, so `DitherStep = 6` would give a `move >= 6 px` line under a caption that says 5.
  R-080 made the Quality tab's captions free of figures for this reason.
- Evidence: confirmed.
- Status: open

### CR-055 The Frames tab: scroll bar out of sight, long names, an empty tooltip, the minimum width

- Where: `src/LightsCheck.Wpf/Views/FramesView.xaml:84`, `:126-128`, `:58`, `:72`, `:79-80`;
  `Views/RejectedView.xaml:61`; `MainWindow.xaml:30`.
- Cases:
  - The table is 1680 px of fixed columns inside a horizontal scroller, so the list's own
    vertical bar is at the table's far right: on a 1920 px screen an 87-frame list has no
    scroll bar in sight until the user scrolls right to the end. The wheel and the keys work.
  - A file name over 17 letters with its number at the end (`Light_Veil_120.0s_Bin1_0001.fit`)
    is cut with dots on every row, without a tooltip; on the Rejected tab it is cut with no
    mark. The name is what the user needs in Siril.
  - The reasons cell of a frame without reasons carries an empty tooltip, which shows as a
    small empty box.
  - The two columns need 980 px with their margin; the window's minimum width is 960.
- Evidence: to measure (the sums were read; each needs a look at the window).
- Status: open

### CR-056 The theme and the automation tree

- Where: `src/LightsCheck.Wpf/Theme/Dark.xaml:59-64`, `:243-267`, `:50`, `:112-115`, `:469`;
  `Views/FramesView.xaml:126`; `Presentation/Charts.cs:53`.
- Cases:
  - A tab header's text takes the theme's general text style, which outweighs the header's own
    colours: the unselected headers are as bright as the selected one, and the mouse over one
    changes nothing. FlatCheck's list has the same finding in the same theme (its CR-058).
  - The theme restyles the scroll bars and not the scroller, so the Report tab shows a light
    square where its two bars meet.
  - The faint text (`#6E6E77`, 11 px, on `#232326`) is at about 3.1 to 1: the column headers,
    the cut-outs' captions, the top bar's labels.
  - Each row of the Frames list is named `LightsCheck.Wpf.FrameRowVm` to a screen reader, and
    the charts and the map are not in the tree at all (R-067 is about that tree).
- The theme is a copy of FlatCheck's; a fix there is worth taking to both.
- Evidence: to measure (a screenshot for the first two; the automation peers in a test).
- Status: open

### CR-057 Figures and verdicts decided in the report, not in the analysis

- Where: `src/LightsCheck.Core/Report/SeriesReport.cs`: the exposure verdict and its weakest
  channel (`:1392-1399`); the focus verdict (`:1366`); "the rest comes from the tracking"
  (`:713`); the zones' averages over the good frames (`:1676-1686`); the average distance to
  the nearest frame (`:1064`, `:1710`); the exposure kept (`:1571-1578`); the arcsecond
  conversions (`:544`, `:663`, `:702`, `:914-915`); the pattern's ideal and limit (`:936`,
  `:1541`); the usual exposure worked back from a threshold (`:1273`).
- What happens: all of it is inside the core, so the rule "Computed only in the core" holds.
  But `Report/` "decides words" and `Analysis/` "decides numbers" (`CLAUDE.md`), and these are
  numbers. One consequence shows in a text: the FOCUS rows 1 and 3 both say `(on average, on
  the good frames)` over two different sets of frames.
- Should: the owner's call, entry by entry as the texts are touched.
- Evidence: owner.
- Status: open

### CR-058 No gate test has a black level above zero, or decodes a raw

- Where: `tests/LightsCheck.Tests/SyntheticFrame.cs` (`Scale` is always white and 0);
  `src/LightsCheck.Core/Runs/FrameRun.cs:116-122` (`AssumedScale`, black 0);
  `tests/LightsCheck.Tests/RawReaderTests.cs:100-106`.
- What happens: every fixture frame has its black at 0, so the subtraction in `Raster.FromImg`
  (`Raster.cs:63`) and the `- scale.Black` of `FrameAnalyzer.cs:66` can be removed with the
  gate green; `R012_ChannelLevelsAreTheMosaicsBackgroundsAboveBlack...` says "above black" and
  does not test it. And `R007_ARealRawDecodesToAnUndebayeredMosaicWithItsScaleAndMetadata`
  returns at once without `LIGHTSCHECK_NEF` and counts as passed, so `ReadInfo`, `Decode` and
  `FillShootingInfo` run in no gate test: CR-009 and CR-028 sit there. The note says the raw
  test is gated; the run shows green, not skipped.
- Should: a fixture with a black level; the raw's conversions testable without a raw.
- Evidence: confirmed.
- Note of 2026-11-14 (the task that fixed CR-006 to CR-010): `tests/LightsCheck.Tests/DngTestData.cs`
  writes a small uncompressed DNG, and `RawReaderTests` now decodes three of them in the gate (a
  black of 64, a 2 x 2 pattern of blacks, a 6 x 6 mosaic), so `Decode` runs there; the black's
  subtraction in the analysis and `FillShootingInfo`'s conversions are still untested.
- Status: open

### CR-059 Core tests that cannot fail or do not check what their name says

In `tests/LightsCheck.Tests/`:

- `SeriesTests.cs:541` `R028_...NeverIntoLights`: the "never" is true by the fixture (CR-014).
- `SessionFolderTests.cs:25`, `:38`: the two sort tests pass without the sort on NTFS, which
  lists the names in order already; the first never compares two names that differ in case
  alone.
- `AcceptanceTests.cs:283` `R005_NumbersUseTheDotWhateverTheCulture` sets no culture, in an
  assembly built with `InvariantGlobalization`.
- `RasterTests` `R013_TileMediansReadEveryFourthPixelAndDropPartialTiles`: every tile is
  constant, so any sampling step gives the same medians.
- `FrameAnalyzerTests.cs:47`: `c.Min >= 0` is true by the fixture, which clamps every pixel.
- `SeriesAnalyzerTests.cs:487`: `Assert.Null(s.RotationTotalDeg is double rt && rt != 0 ? rt :
  null)` passes for null and for 0.
- `SeriesAnalyzerTests` `R043_NoStretchIsOkAndTwoRegisteredFramesAreNeeded` checks 0 aligned
  frames, not 1.
- `SeriesAnalyzerTests.Frame()` puts a 21 s pause before every frame, so every walking-noise
  test runs on the approximate drift and never on a clean one.
- `SeriesTests.cs:523`: `SwampingRatio > 3` is the only check on the ratios the exposure
  verdict is made from.
- `CfaPatternTests.cs:56-68`: pins the colour order for odd margins, which the shipped libraw
  never passes (see CP-5's note on FlatCheck's CR-032).
- `VersionTests.cs:57-60` returns early when a project file is missing;
  `LicenseHeaderTests.cs:127` skips a missing folder: a renamed window project would drop out
  of both unnoticed. `LicenseHeaderTests.cs:138` looks for three phrases where R-009 says
  "verbatim".
- `EnglishGoldenOutputTests.cs:109`: the pattern for "the same numbers" drops the signs.
- The caps `Th.ShapeStars`, `Th.RegistrationStars` and `Th.HaloStars` are reached by no
  fixture; `FrameRun.AssumedScale` is reached on BITPIX 16 only.
- Never asserted: `CornerShiftPerExposurePx`, the value of `RotationPerHourDeg`,
  `TrailAngleDeg`, `WithinMid`, `WithinLoose`.
- `ValidationTests.cs:119` throws on a session without a run, instead of failing by name.
- `GoldenOutputTests.cs:102`: its failure message is in Romanian.

Each is its own small fix; a task may take several of one file.

- Evidence: confirmed (spot-checked: the first three, `SeriesAnalyzerTests.cs:487` and
  `RawReaderTests` were opened; the rest are the reviewers').
- Note of 2026-11-14 (the same task): the crops of `PatternAnalyzerTests.Crop` are drawn from one
  `Random` a crop, seeded 0, 1, 2 and so on; .NET's seeded generator gives streams that are not
  independent for neighbouring seeds, and on such crops noise alone read 5 % of one frame's noise on
  7 frames where independent noise read 17 % (CR-006). The new noise test draws every crop from one
  stream; the older tests were left as they are.
- Status: open

### CR-060 Window tests that cannot fail or do not check what their name says

In `tests/LightsCheck.Wpf.Tests/`:

- `MainViewModelTests.cs:64-77`, `:97-108` and `WindowLoggingTests.cs:149-163`: the three stop
  tests cancel right after `OpenAsync` returns, before the run starts. A stop in the middle of
  the measuring is never run, nor a run that returns a result after Stop.
- `MainViewModelTests.cs:167-183` pins `PixelIsValid`, which nothing binds (CR-016).
- `MainViewModelTests.cs:427`: `lights\` unchanged after an export elsewhere (CR-014).
- `CultureTests.cs`: its comment says it stops the removal of the `ForceInvariantNumbers` call,
  and every test calls it itself; removing it from `App.OnStartup` fails none. Line 94 sets a
  culture and never puts it back.
- `AboutWindowFitTests.cs:195-213`: "inside the area" holds because a window never shown is 0
  high.
- `WindowSmokeTests.cs:85`: `Assert.Equal(w, bmp.PixelWidth)` on a bitmap just made `w` wide.
- `WindowSmokeTests` `TheChartsRenderWithGapsAndGuides` fails on an exception alone; a chart
  that drew nothing would pass. `SeriesChart.PointAt` is called by no test.
- `AboutTests.cs:62`: the role is checked not blank, the other four fields against the core.
- `StaFixture.cs:58`: `ready.Wait()` has no time limit; a theme that fails to load takes the
  test host down instead of failing with a message.
- Not covered at all: `App.OnStartup`, `MainWindow.Start`, `Choose`, `ExportFiles`, `Stop`.

- Evidence: confirmed (spot-checked: `MainViewModelTests.cs:427` and `WindowSmokeTests.cs:85`
  were opened; the rest are the reviewers').
- Status: open

### CR-061 Design notes and comments that describe other code

- `docs/design/frame-analysis.md:15-16`: "four rasters live at the peak" counts one twice and
  leaves three out; the peak is about five. `:75-77` explains the 397 to 399 measured stars
  against the prototype's 400 by a border guard that is the same in both.
- `docs/design/series-analysis.md:266-267`: the rejected tab's explanation is "built once";
  it is built at each read. `:302-303` lists four reasons where the code has five. `:315`: a
  cell is quoted on a line break too.
- `docs/design/window.md:176`: `StaFixture` is the thread "the view-models' awaits need"; most
  view-model tests run without it.
- `docs/design/session-and-console.md:73-75`: a missing field prints `-` (CR-033).
- `docs/design/imaging.md:31-35` lists `PngWriter`, `BitmapFont` and the `Img` transforms under
  "What each file does here"; nothing here calls them.
- `src/LightsCheck.Core/Thresholds.cs:216-223`: the comment of `WalkingStreakVisible` describes
  the dash "inside the longest run" and "the 11 px of Veil"; since ADR 0039 it is judged per
  stretch.
- `src/LightsCheck.Core/Analysis/CadenceAnalyzer.cs:63`: "in capture order"; every caller
  passes the order of the names (CR-010). Corrected on 2026-11-14 by the task that fixed CR-010.
- `src/LightsCheck.Core/Report/SeriesReport.cs:1518`: "a share just under the limit never
  prints as the limit"; 24.95 to 24.99 % prints `25.0 %`.
- `src/LightsCheck.Core/Imaging/RawReader.cs:336-337` (CR-009), `:431-432` (the example
  contradicts `imaging.md:44-46`), `:314-317` (the margins, see CP-5's note).
- `src/LightsCheck.Wpf/UserSettings.cs:97-99`: the arrows "are asked again next time"; nothing
  asks.
- `src/LightsCheck.Wpf/Presentation/Charts.cs:110-111`: `Num`'s summary (CR-026).

- Evidence: confirmed (the reviewers'; the lines of the code files named in other entries were
  opened there).
- Status: open

### CR-062 The fixtures' star grid repeats, so frames with no shift register one column over

- Added 2026-11-13 by the task that fixed CR-001 to CR-005 (ADR 0046, point 7); not one of the
  61 entries of the review.
- Where: `tests/LightsCheck.Tests/SyntheticFrame.cs` (`WithStarGrid`);
  `tests/LightsCheck.Tests/SessionFixture.cs` (`WithLights(withStars: true)`, `StarLight`);
  `src/LightsCheck.Core/Analysis/Registration.cs` (`Vote`).
- What happens: the 40 stars of every synthetic light sit on rows and columns 137 px apart,
  each moved by a few pixels. The vote for the coarse shift counts the offsets in bins of 8 px.
  A true shift of exactly 0 px on an axis sits on a bin's edge, the centroids' noise splits its
  votes between two bins, and the offset of the grid laid one column over, which pairs 22 of
  the 40 stars, wins. Turned by 180 degrees the grid falls on itself the same way.
- Case: `SessionFixture.WithLights(3, withStars: true)`, three lights of one field with no
  shift: the console's table reads `dX 136.7` on frames 2 and 3, a move of 137 px that did not
  happen. Eight frames drawn at (0.5 i, 0): frames 2, 3 and 5 register at (137.2, -2.1) with 22
  stars matched of 40. `SessionFixture.Pattern` escapes by its 0.2 px on Y.
- Should: a fixture field that does not repeat (the `scattered` option added for R-087 draws
  one), or no frame drawn at a shift of exactly 0 on an axis; and the tests that draw identical
  frames read for what they assert under those positions.
- Test: which tests built on `WithLights(withStars: true)` assert a position, a move or a
  dithering figure was not looked at.
- Evidence: confirmed on the fixtures (a console run and a test's own output, 2026-11-13). No
  defect on a sky, which does not repeat: the recorded sessions align every frame on 62 % of
  the stars or more.
- Status: open

## The files copied from FlatCheck

The files of `src/LightsCheck.Core/Imaging/` marked "copied from FlatCheck unchanged" are fixed
in FlatCheck first, then the copy is refreshed (the rule "Copied files stay copies"). Each
entry names FlatCheck's (`FlatCheck/docs/review/2026-11-13-code-review.md`) and what it means
here. A task in LightsCheck takes one after FlatCheck closed its own.

| ID | Sev | Reach | FlatCheck | Title |
|----|----:|-------|-----------|-------|
| CP-1 | 2 | crafted | CR-013 | The FITS reader uses `NAXISn` and `BITPIX` before checking them |
| CP-2 | 1 | rare | CR-008 | A table HDU is read as an 8-pixel-wide image |
| CP-3 | 3 | rare | CR-029 | The header-only read and the full read parse the same FITS differently |
| CP-4 | 4 | crafted | CR-048 | The header-only read gives up after 1000 blocks |
| CP-5 | 4 | crafted | CR-051 | `CfaPattern.Parse` accepts orders that are not Bayer |
| CP-6 | 4 | rare | not listed | Header numbers are parsed with `NumberStyles.Any` |

- **CP-1** `FitsReader.cs:338-349`, `:353`, `:364`, `:378`. A header with `NAXIS1 = -2880`
  moves the read position back onto the header: the loop never ends, and in a series one
  worker never returns, so the run never finishes. A missing `BITPIX` with large sides throws
  out of memory, which neither application catches. Not in FlatCheck's entry: `NAXIS = 3,
  NAXIS3 = 0` throws `ArgumentException` at line 364 where a "FITS: ..." message should come.
  Reached by every full load of a FITS light or bias.
- **CP-2** `FitsReader.cs:322-351`. Narrower here: only a compressed or table FITS that keeps
  a plain FITS extension is listed as a frame at all.
- **CP-3** `FitsReader.cs:111-147` against `:297-320`. Here: `INSTRUME= 'ASI294MC/Pro'` shows
  as `ASI294MC/Pro` in the inventory and `ASI294MC` in the series; a file with an empty primary
  and an image extension has no camera, exposure or time in the inventory, and no plate solve
  for `SeriesRun.ReadWcs`.
- **CP-4** `FitsReader.cs:116`, `:146`. `LoadInfo` and `ReadWcs` answer with a partial header
  where `Load` refuses; R-008 and ADR 0027 say both refuse.
- **CP-5** `CfaPattern.cs:71-87`. `BAYERPAT = 'GGRB'` passes; no crash here, but
  `Raster.InterpolateGreen` then fills the "green" from red and blue neighbours.
- **CP-6** `FitsReader.cs:435-443` (`HInt`, `HDbl`). `XPIXSZ = 3,92` reads 392, the comma
  taken as a thousands separator, so the pixel scale is 100 times off where the next source
  should be taken; `BZERO = 3.2768D+04`, valid FITS, is not parsed and falls back to 0. Worth
  an entry in FlatCheck's list.

Two of FlatCheck's entries do not apply here. Its CR-007 (a verdict on a white level at or
below black): the raw reader here refuses such a scale and there is no `--sat`. Its CR-032
(the colour order shifted by libraw's margins, `to measure` there): by the reviewer's reading
of LibRaw 0.21.1, `open_datastream` makes any odd margin even and rebuilds the mask before the
reader sees it, so the margins added in `CfaPattern.FromLibRaw` are always even and change
nothing; a Canon raw with the Debug log on would confirm it in one run. That reading is worth
taking back to FlatCheck's CR-032.

## Cleanup

Not defects: dead code, duplicates, small things. One line each, as the reviewers gave them.

Dead or test-only code:

- `Imaging/PngWriter.cs` and `Imaging/BitmapFont.cs`: no caller in `src/` or `tests/`.
  `Img.cs:63-241`, `Mosaic.cs:55-135`, `Stats.cs:53-71`, `:99-108`: transforms no one calls.
  `ImageLoader.cs:182` `LoadImage`, `FitsReader.cs:56` `FitsExtensions`: no caller. Copied
  files; listed so the list knows.
- `FrameAnalyzer.cs:39` `Analyze(Img, ...)`, `Raster.cs:45` `Raster.At`, `SeriesRun.cs:398`
  `WcsRaAxis`, `FitsReader.cs:257`, `:272`: called by tests only.
- `SeriesReport.cs:102-103`, `:358`: locals never read; `:1382`: a parameter not read;
  `:1287`: a test on a constant, whose other branch cannot run; `:1278`: an enum name that
  would print in English, not reachable today.
- `Theme/Dark.xaml`: never used here are `Number`, `LogBox`, `MetricRowTemplate`, `Disclosure`,
  the `CheckBox` style, `BadBr`, the `Not` converter and the implicit `ListBoxItem` style;
  `RowVm.Note`, `HasNote` and `IsKey` exist for the unused template. `Converters.cs:72`
  `InverseBoolConverter`: no XAML uses it.
- `VerdictPalette.cs:51-53`, `:68`: `DA`/`YES` and `EROARE`/`ERROR`, words no row produces.
- `MainViewModel.cs:519-526`: a catch that cannot be reached on the UI thread. The branches
  for a run replaced by a newer one (`:440-443`, `:492-495`) guard a path no user reaches: the
  Choose button is disabled during a run.

Duplicates:

- `Opt(double?, int)` three times (`SeriesReport.cs:1720`, `InventoryReport.cs:253`,
  `FrameReport.cs:93`).
- `SummaryView.xaml:30` and `DitherView.xaml:32`: two templates under one key, `ReportRow`.
  `RejectedView.xaml:33-46` and `DitherView.xaml:137-150`: the same template twice.
  `FramesView.xaml:39-56` and `:91-108`: the sixteen column widths twice.
- `StarCrops.cs:87-92`: the centre region written out twice.
- In the tests: `Render` twice, `VisualDescendants` twice.
- `SeriesReport.cs:395` and `:1705`: the same trend written two ways (`3.25 px / 3.25 px` and
  `3.25 / 3.25 px`).

Small things:

- `FrameReport.cs:55-56`: `green background` also heads a frame without a colour order.
- `MainWindow.xaml:74` (`PIXEL (um)`) and `:163` (`Dithering`): single literals where the rule
  asks for a pair; the same text in both languages. `SeriesAnalyzer.cs:665`: `"plate solve
  (WCS)"` likewise, and composed in `Analysis/`.
- `WindowTexts.cs:63`: `Registering the frames`, a word the rule "Plain words" lists; the
  glossary says "until its rewrite". `:76`: `the frame has no measured stars` is also shown
  when it has stars, none in the regions cut out.
- `MainViewModel.cs:398`: `Opening the session...` stays up until the first frame is done.
  `:433`: a core bug of the two caught framework types would show as "The folder cannot be
  analysed". `:630`, `:634`: two chart colours typed as numbers.
- `UserSettings.cs:92`: the file is rewritten from its two keys, so a key a later version adds
  is dropped; `:42-43`: an empty local-data folder makes the path relative.
- `Options.cs:114-126`: option names are case-sensitive, `--lang` is not.
- `Program.cs:208`, `:224-229`: the progress line is cleared on success only; no input was
  found that prints `ERROR:` after it.
- `NoiseAnalyzer.cs:208`: an exception text in the user's language that only the log gets.
- `SeriesAnalyzer.cs:646`: two records compared by value where "not the same frame" is meant.
  `:507`: a frame with no measurable star is good by elongation.
- `Components.cs:178-179`: products in single precision before a double sum.
  `FrameAnalyzer.cs:148`: `> Th.AngleVoteMin - 1` for `>=`. `Raster.cs:172-173`: a division by
  zero for a one-row target, not reachable.
- `Charts.cs:254`: `SeriesChart.OnRender` does not guard a NaN x, which `PointAt` does; the
  view-model never passes one. `:288`, `:326`, `:329`, `:338-339`: a new pen or brush for every
  tick, point and guide on every render.
- `AboutWindow.xaml.cs:134`: the started process is not disposed.
- `LightsCheck.Wpf.csproj:15`: `ApplicationHighDpiMode` is a Windows Forms property (not
  verified); FlatCheck's cleanup has the same line.
- `Directory.Packages.props:15`: a Linux runtime of libraw is referenced, and a load failure
  there would send the user to Microsoft's runtime. Nothing ships for Linux.
- `FitsReader.cs:353-364`: a second full-size array on a mono file, 193 MB more for a 24 MP
  frame, per thread (a copied file; in FlatCheck's cleanup).
- Comments in the copied files that point at FlatCheck's code and ADR numbers
  (`BitmapFont.cs:23-24`, `FitsReader.cs:52-54`, `Mosaic.cs:24-31`, `Img.cs:112-120`): they stay,
  by the rule.
- `DitheringTests.cs:164-165`: two summaries on one test. `PatternAnalyzerTests.cs:97-98`: a
  comment that explains by planted pixels what is mostly CR-006.
