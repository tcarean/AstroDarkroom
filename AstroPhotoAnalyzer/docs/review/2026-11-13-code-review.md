# Code review of the whole project, 2026-11-13

The list of what the review found, to be fixed one entry at a time in the order of the summary
table. The decision to hold it, the severity scale and how an entry is closed are ADR 0092.

## How it was done

The first code was written by an earlier model and had almost no review; the reviews since look
only at one task's change. On 2026-11-13 the code measured about 23,500 lines in 115 files
(`AstroPhotoAnalyzer.Core/` 14,200, `AstroPhotoAnalyzer/` 570, `AstroPhotoAnalyzer.Wpf/` 8,800),
too much for one careful reading, so it was split into nine areas. Each area was read whole by a
reviewer with a clean context (a subagent on this session's model), which first read `CLAUDE.md`,
`docs/design/known-limitations.md`, the area's design notes and their ADRs, so that what is
deliberate was not reported. A reviewer changed nothing in the repository and built nothing of
it. Every finding it returned was then checked by the session that wrote this list: the cited
lines were opened and the case followed through the callers. Last, the seams between areas were
read (what `AnalysisRun.Execute` hands the console and the window, the parameters by the
console's flags and by the window's fields).

| # | Area | Reported | In this list as |
|---|------|---------:|-----------------|
| 1 | Input: readers, raster, writers | 5 | CR-004, 007, 010, 012, 023, 037, 045 |
| 2 | Tones, dust, the sky's geometry | 3 | CR-007, 041, 042 |
| 3 | Detail, noise, colour of the sky, the file | 10 | CR-003, 006, 008, 011, 042, 044, 063 |
| 4 | Colour for the paper | 7 | CR-001, 002, 037, 046, 047, 048, 066 |
| 5 | Report, recommendations, thresholds, language | 19 | CR-006, 015, 016, 017, 025 to 030, 039, 041 to 044, 052, 054, 059 |
| 6 | The run, the log, the console | 11 | CR-010, 018, 025, 036, 038, 059, 060, 065, 067 |
| 7 | Window shell and its view-models | 13 | CR-005, 009, 013, 014, 018, 035, 038, 040, 049, 054, 056, 062 |
| 8 | The tabs' view-models | 10 | CR-016, 019, 024, 028, 030, 050, 051, 054, 055, 061 |
| 9 | Views, presentation, theme | 15 | CR-020, 021, 022, 031 to 034, 053, 054, 057, 058, 064 |
| - | Seams, read by the session | 2 | CR-010, 059 |

93 findings were reported and none was rejected at the check; two observations the reviewers
kept out of their own lists were taken in (CR-004, CR-023), because the owner should see them.
The same defect seen from two areas became one entry, and small related ones were grouped,
which gives the 67 entries below. That no finding was rejected says the reviewers were careful,
not that every entry is proved: the check was a reading, not a run of the tool. What a reading
cannot settle is marked.

Areas 2 and 8 returned no wrong figure and no crash. Area 2 (the tones, the dust, the WCS, the
catalogue) had the review of 2026-10-23 behind it; its reviewer lists what it checked line by
line and found sound, as the reviewers of areas 4, 5, 6, 7 and 9 do.

Out of scope: `scripts/` and `.claude/` (the kit's), `Icon/make_icon.py`, and the tests line by
line (a reviewer opened a test only to see whether a suspected defect was covered).

No reviewer ran the project. Several replicated a few lines outside the repository to get a
figure: float32 arithmetic in Python (areas 1, 2, 3, 4), small F# or C# programs for number
formats, the file calls of `AtomicFile` and NLog's rolling (areas 5, 6, 7, 8; some on .NET 10,
not the .NET 8 the tool targets), and off-screen WPF layouts in Windows PowerShell 5.1 (area 9,
so on the .NET Framework's WPF). Their figures are quoted as theirs.

## How to read an entry

- **Severity.** 1 = a wrong figure, a wrong verdict, or a wrongly written output image. 2 = a
  crash, a hang, or a file overwritten or lost. 3 = a wrong text or a wrong display. 4 =
  robustness and cleanliness.
- **Reach.** Who meets it: *common* (an ordinary run), *some setups* (another kind of file,
  paper, screen or scaling than the ones this project is tested on), *slip* (a mistyped
  argument or field), *rare*, *crafted* (a broken or hostile file).
- **Evidence.** *confirmed*: the lines were read and the case follows from them. *to measure*:
  the code was read, but the case, or its size, rests on something only a run or a look at the
  window can show; the task starts by showing it. *owner*: the code does what is described, and
  whether that is a defect, or which wording replaces it, is the owner's call.
- **Status.** `open`, then `fixed <commit>` or `rejected: <reason>`, set by the task that
  closes the entry.

An entry gives the direction of a fix, never the fix. A task takes one entry (or the group one
entry names), starts from the requirement and a failing test (the kit's rules "Requirements
first" and "Test before fix"), re-validates on the reference image when a formula moved, and
shows the owner the old and the new wording side by side when a text changes (the rule "Plain
words"). Line numbers are those of 2026-11-13; a task finds its lines again by the names the
entry gives. Paths are relative to the project folder.

## Summary

| ID | Sev | Reach | Evidence | Title | Status |
|----|----:|-------|----------|-------|--------|
| CR-001 | 1 | common | to measure | The paper fit never asks the Soft proof judge about a pixel it does not move | fixed 98bfe6d2 |
| CR-002 | 1 | some setups | confirmed | A grey pixel whose tone the paper cannot reach is counted nowhere by the paper fit | fixed da46a627 |
| CR-003 | 1 | some setups | to measure | On an 8-bit source three thresholds on a whole level are decided by float rounding | fixed ae70ca1d |
| CR-004 | 1 | some setups | owner | A float FITS that is not on a 0..1 scale is analysed as a white frame | fixed 4e0f4da4 |
| CR-005 | 1 | slip | confirmed | Opening another image while a field is wrong leaves the old result under the new name | fixed 5c0cf426 |
| CR-006 | 1 | rare | confirmed | Soft corners are reported on a frame whose stars are declared too few | fixed 19f667e9 |
| CR-007 | 1 | rare | confirmed | A FITS number that does not parse is read as its default: BZERO, BSCALE, the WCS | fixed 5a1707b4 |
| CR-008 | 1 | rare | confirmed | An 8-bit image saved as 16 bits by a shift passes as real 16 bits | fixed 5d00aa35 |
| CR-009 | 1 | rare | confirmed | A second image chosen from the keyboard during a run gets the running image's result | fixed c075e9ae |
| CR-010 | 2 | slip | confirmed | `-o` naming the analysed image replaces the image with the report | fixed de089c54 |
| CR-011 | 2 | rare | to measure | An image over 65 535 px on a side ends the whole analysis in the JPEG stage | open |
| CR-012 | 2 | crafted | confirmed | A FITS header with a negative axis length hangs the reader | open |
| CR-013 | 2 | crafted | confirmed | A settings file with a broken escape stops the window at every start | open |
| CR-014 | 3 | common | confirmed | "Save the report..." never becomes enabled | open |
| CR-015 | 3 | common | owner | One report says "no visible steps" in section 1 and warns of banding in section 5 | open |
| CR-016 | 3 | common | confirmed | One of something is written as a plural, and the tab's star counts are not the report's | open |
| CR-017 | 3 | common | owner | "shrinks by ~0.82 times" says the opposite of the factor | open |
| CR-018 | 3 | common | confirmed | Three texts predate the files the tool now writes | open |
| CR-019 | 3 | common | owner | "Real detail across the width" shows a count of stars with the unit px | open |
| CR-020 | 3 | common | confirmed | A section heading reads "1 .  Title", an About component "Name  -  role" | open |
| CR-021 | 3 | common | confirmed | In the tab, the tooltip of `Original` promises a key only the full screen has | open |
| CR-022 | 3 | common | confirmed | A disabled check box looks enabled | fixed 24933a57 |
| CR-023 | 3 | common | owner | Every FITS gets the "no colour profile" caution, and its profile is never looked for | open |
| CR-024 | 3 | some setups | confirmed | The description of "Under L* 15" is false on canvas | open |
| CR-025 | 3 | some setups | confirmed | Parameters typed with decimals are reported rounded | open |
| CR-026 | 3 | some setups | confirmed | A width over 100 cm gets three texts that contradict the table | open |
| CR-027 | 3 | some setups | confirmed | "with the passepartout and the frame" when there is none | open |
| CR-028 | 3 | some setups | confirmed | On a mono image five texts still speak of colour and of three channels | open |
| CR-029 | 3 | some setups | confirmed | A neutral edge darker than the sky is explained as "the Milky Way entering a corner" | open |
| CR-030 | 3 | some setups | confirmed | Three tables lose their columns | open |
| CR-031 | 3 | some setups | confirmed | The red sentence of two tabs is cut at the right edge | fixed 24933a57 |
| CR-032 | 3 | some setups | to measure | The map, the preview and the full screen open on the primary screen | open |
| CR-033 | 3 | some setups | to measure | A cloud's name on the map is cut to the width of its circle | open |
| CR-034 | 3 | some setups | to measure | The map window's cloud list has no room on a low screen | open |
| CR-035 | 3 | some setups | to measure | The window is not per-monitor DPI aware | open |
| CR-036 | 3 | some setups | to measure | A redirected report is in the console's code page, the `-o` file in UTF-8 | open |
| CR-037 | 3 | some setups | confirmed | A mono image saved as a TIFF from the paper fit carries an RGB profile | open |
| CR-038 | 3 | some setups | owner | The window reports a bad WCS file with the console's flag, and a quoted path as missing | open |
| CR-039 | 3 | some setups | owner | Three texts still name the method: "grid", "median" | open |
| CR-040 | 3 | slip | confirmed | A field's error is drawn behind the tabs when a result is on screen | open |
| CR-041 | 3 | slip | confirmed | A Dmax of 0.1 or less prints a zero or negative neighbouring Dmax | open |
| CR-042 | 3 | rare | confirmed | A figure rounded at a boundary contradicts the word beside it | open |
| CR-043 | 3 | rare | confirmed | A caution on the JPEG line gives neither the threshold it failed nor what to do | open |
| CR-044 | 3 | rare | confirmed | An embedded profile whose name cannot be read gets no warning | open |
| CR-045 | 3 | rare | confirmed | A TIFF's reader notes overwrite one another | open |
| CR-046 | 3 | rare | confirmed | The name of a v4 profile is lost when its first record has no language code | open |
| CR-047 | 3 | rare | confirmed | A failed write onto a file of the input's length is told as a save over the input | open |
| CR-048 | 3 | rare | owner | With the dust at black the shadow chart runs to the bright clouds, not the band | open |
| CR-049 | 3 | rare | confirmed | With no sky patch the map's dust line still says "the darkest 1 % of the sky" | open |
| CR-050 | 3 | rare | confirmed | A curve save confirmed after a recompute landed writes nothing | fixed 24933a57 |
| CR-051 | 3 | rare | to measure | A soft-proof intent the core refuses is shown with " (Parameter 't')" and can take the controls away | open |
| CR-052 | 3 | rare | to measure | The hand recipe can read "saturation at 100 %; it would leave 100.0 %" | open |
| CR-053 | 4 | common | to measure | Four theme defects: tab headers, the report's corner, the progress bar, the focus | open |
| CR-054 | 4 | common | confirmed | Numbers typed into texts, and decisions made a second time outside the core | open |
| CR-055 | 4 | common | to measure | The two galleries build every bitmap and red layer on the window's thread | open |
| CR-056 | 4 | common | confirmed | The previous analysis, with its whole image, is kept in memory during the next run | open |
| CR-057 | 4 | some setups | confirmed | At 125 % or 150 % scaling the galleries enlarge an image they promise at its own size | open |
| CR-058 | 4 | some setups | confirmed | The curve window opens taller than the screen at 150 % and on a 768-line laptop | open |
| CR-059 | 4 | slip | confirmed | The console and the window do not hold the same argument rules | open |
| CR-060 | 4 | slip | confirmed | `-o` naming a folder and a missing `--wcs` file are told late and badly | open |
| CR-061 | 4 | rare | confirmed | A recompute for a larger area that fails throws away the gallery on screen | open |
| CR-062 | 4 | rare | confirmed | A relative path on the window's command line stays relative | open |
| CR-063 | 4 | rare | confirmed | Two widths the star measure should not return | open |
| CR-064 | 4 | rare | confirmed | The save message's tooltip never shows | open |
| CR-065 | 4 | rare | to measure | The log's rolling takes any `<executable>_<digits>.log` for its own | open |
| CR-066 | 4 | rare | to measure | Under the two soft variants the chroma mapping jumps where the exact edge takes over | open |
| CR-067 | 4 | crafted | confirmed | Arguments and paths go into the log as typed | open |
| CR-068 | 4 | rare | to measure | The noise tile's range of exactly 6000 steps is decided by float rounding | open |
| CR-069 | 4 | rare | confirmed | Two more level figures of an 8-bit source fall by float rounding: an edge folded into its corner, a JPEG percentile | open |
| CR-070 | 3 | rare | confirmed | Two texts of the dust move can print a figure equal to the one it is compared with | open |
| CR-071 | 4 | common | to measure | The curves tab's second gallery is built as the first: its bitmaps on the window's thread, its gamut table again | open |
| CR-072 | 4 | rare | confirmed | A failed computation of one gallery of the curves tab takes both away, and the box cannot be unticked to go round it | open |
| CR-073 | 3 | rare | to measure | A curve can be missing from the gallery with the dust move: its strengths are judged with other points than the ones it gets | open |
| CR-074 | 4 | common | confirmed | Two figures of the paper fit are not the same from one run to the next | open |
| CR-075 | 3 | rare | owner | On a frame with too few stars a zone reads n/a under a description that promises its width from 5 stars | open |
| CR-076 | 3 | rare | owner | "You lose nothing going to 8 bits" is also said of a file caught by its levels alone | open |

9 entries of severity 1, 4 of severity 2, 39 of severity 3, 15 of severity 4; 45 confirmed, 14
to measure, 8 for the owner. (As counted on 2026-11-13; CR-068 and CR-069 were added on 2026-11-14, CR-070 to
CR-074 on 2026-11-16 with the dust move of the suggested curves (ADR 0101), CR-075 and CR-076
on 2026-11-16 by the review of the fixes of CR-006 to CR-010, and the
Status column says what has been closed since.) After the entries: what the owner may want to
look at though no rule is broken, then the cleanup list (dead code, duplicates, work done twice).

## Severity 1: a wrong figure, a wrong verdict, or a wrongly written output image

### CR-001 The paper fit never asks the Soft proof judge about a pixel it does not move

- Where: `AstroPhotoAnalyzer.Core/Analysis/GamutFit.cs:250-252`, `:264-265`, `:272`
  (`GamutFit.Apply`), `:322` (`Edge`), `:355` (`ExactEdge`).
- What happens: whether a pixel is outside is `c > cmax`, and `cmax` is the table's own edge
  whenever the pixel sits more than `GamutFitEdgeMargin` inside it; the judge is not called for
  the pixel itself. A pixel that is not moved keeps `after[i]` false, since the judge is asked
  only about a moved pixel. The table is built from the exact Lab rule; the Soft proof tab's
  judge (`GamutAnalyzer.Outside`) decides near the threshold on the pixel's 8-bit colour, a step
  function, so the two can disagree by more than the margin in the dark tones.
- Case (the reviewer's reading of `docs/validation.json`, both on the same subsample of the
  plate-solved FITS through the baryta profile): V-008 `curves.before.gamut.all` 1.9427 % by the
  judge against V-016 `gamutfit.Exacta.before` 1.9073 % by the fit, with `after` 0.0008 %. The
  fit never sees 0.035 points, about 8 000 pixels of 22.6 MP: not counted before, not moved, not
  counted after. The mirror case is in the same lines: a pixel beyond `guess + margin` that the
  judge keeps is counted outside and cut.
- Effect: the saved file, opened in the Soft proof tab, still shows those pixels in red, and the
  evidence says 0.0008 % are left. On this pair the gap is inside the 0.05 points R-175 allows;
  on the canvas profile, where the table is further from the exact edge, it was not measured,
  and the fit's own "after" share cannot see it.
- Should: R-175, "no pixel the chroma alone can bring in is left outside, by the judge of the
  Soft proof tab itself".
- Test: none; `R175_TheTableIsReadBetweenItsNodesAndTheExactRuleDecidesNearTheEdge` uses a
  monotone rule as its own judge, where the two cannot disagree.
- Evidence: to measure. The mechanism is read; its size needs `GamutAnalyzer.Measure` on the
  fitted subsample beside `PctOutsideAfter`, on the three pairs of `paper-fit.md`.
- Status: fixed 98bfe6d2 (ADR 0093)

### CR-002 A grey pixel whose tone the paper cannot reach is counted nowhere by the paper fit

- Where: `Analysis/GamutFit.cs:249` (`Apply`, the grey exemption), `:253-263` (the out-of-reach
  branch), `:232-236` (the mono shortcut); the same shortcut in `GamutAnalyzer.cs:285` and
  `Runs/ProofRun.cs:140`.
- What happens: `if (c < Thresholds.GamutFitMinChroma) { ...; continue; }` comes before the
  judge, so a pixel with a chroma under 0.05 is in none of "before", "after" and "tone the paper
  cannot make", whatever the judge would say of its tone. The out-of-reach branch is reached
  only by a pixel with some chroma.
- Case: an RGB image with pixels R = G = B at a tone the judge rejects (under the paper black of
  a canvas, `known-limitations.md`). The reviewer's probe gives an exact grey a chroma of at most
  0.025. The Soft proof tab paints the pixel red and counts it; the Culori pentru hartie tab
  counts it nowhere and leaves its red layer clear; a neighbour with a chroma of 0.06 at the same
  tone is counted in all three. A grey image stored as RGB reads 0 % where the Soft proof tab
  reads the whole dark area.
- Should: R-175, such a pixel "stays as it is and is counted".
- Owner's part: a mono raster gives 0 in all three places on purpose ("a mono image has no
  chroma"); that reason dates from the chroma-only rule, and under ADR 0081 the same grey image
  reads 0 % as a mono file and its real share as an RGB file.
- Test: none; `R175_TheFitReducesTheChromaAloneToWhatThePaperHoldsAndCountsTheToneOutOfReach`
  has one out-of-reach pixel, with a chroma of 10.
- Evidence: confirmed (the code path; how many pixels on a real pair needs a run with the canvas
  profile on an 8-bit copy of the reference image).
- Status: fixed da46a627 (ADR 0094; the mono image stays at 0, the owner's answer of 2026-11-14)

### CR-003 On an 8-bit source three thresholds on a whole level are decided by float rounding

One family, the one ADR 0043 fixed for the dark shares of `TonalAnalyzer`: a threshold that sits
on a whole 8-bit level is compared in floating point against values that, on an 8-bit source
(or a 16-bit file that is an 8-bit upscale), are whole levels too, so the tie falls by the last
bit. The 16-bit reference image is not touched.

- Where and what happens:
  - `Analysis/QuantizationAnalyzer.cs:189` and `:250` (`Plateau`, `Accept`):
    `hi - lo < maxRange` with `maxRange = (float)(PlateauTileMaxRangeLevels / 255.0)`. A tile
    whose range is exactly 6 levels is accepted or rejected by its darkest level.
  - `Analysis/BackgroundAnalyzer.cs:283`, `:345-346`, `:92` (`EdgeRegion.Flagged`), `:105`
    (`Coloured`): the medians times 255 in double, then `> CornerGradientLevels`. The same
    compare in `Report/Recommendations.cs:335-336` (`ColourOf`).
  - `Analysis/JpegAnalyzer.cs:261-266` (`ErrorAccumulator.Add`): `refLevel` is a float product;
    the same tie on `MaxError <= JpegMaxErrorReject` at `:66`.
- Case (the reviewer's float32 replicas in Python, no C# run):
  - Plateau: of 250 darkest levels, 218 accept a range of exactly 6; a tile spanning 17..23 is
    clean sky, one spanning 11..17 is not. With 0.9 levels of noise on an 8-bit sky, about 65 %
    of the 5000 positions pass at sky level 20 and none at level 14 or 30, so `PlateauTiles`,
    `PlateauFramePct`, `PlateauKnown` and the line section 1 prints change with the sky's level.
  - Edge region: a corner exactly 3 levels over the sky is flagged at sky 30 (the delta reads
    3.0000017) and not at sky 24 (2.9999998); Appendix E prints `+3.0` either way.
  - JPEG: at the reference levels 1, 2, 4, 8, 16, 32, 64, 128 an error of exactly 2 counts as
    "over 2".
- Should: R-080 "range < 6 levels", R-061 "more than 3 levels", "over 2 levels": the same answer
  at every sky level, the compare made on whole levels as ADR 0043 did.
- Test: none; the analyzers' tests build float rasters, the `mono-png8` golden pins whatever the
  rounding gives (a fix moves that golden: read its diff).
- Evidence: to measure. The lines were read; the figures are the replicas'. The task starts with
  a C# test: a raster built as the reader builds an 8-bit level (`(ushort)(n * 257) *
  (1f / 65535f)`) through each analyzer.
- Status: fixed ae70ca1d (ADR 0095; the second half of `Coloured` in ac1fd396, found by the review of the branch)

### CR-004 A float FITS that is not on a 0..1 scale is analysed as a white frame

- Where: `AstroPhotoAnalyzer.Core/Input/FitsReader.cs:308-310` (`ReadData`), `:184-185` (the
  note); `Input/RasterOps.cs:209-210` (`LooksLinear`); `Report/Recommendations.cs:553-554`.
- What happens: every sample over 1 is clipped to 1 and counted, with no limit on the count
  (R-013 says exactly that).
- Case: a 32-bit float FITS on a 0..65535 scale, as other stackers write. Every sample is 1, the
  median is 1.0 so the linear refusal does not fire, and the whole report is about a white image;
  the count appears as an Ok-level "note from reading the file".
- Should: the owner's call. The code follows R-013 to the letter; a refusal (or a rescale) when
  most samples are clipped is a requirement change.
- Test: none for a file wholly out of range.
- Evidence: owner.
- Status: fixed 4e0f4da4 (ADR 0096; refused, the owner's answer of 2026-11-14)

### CR-005 Opening another image while a field is wrong leaves the old result under the new name

- Where: `AstroPhotoAnalyzer.Wpf/ViewModels/MainViewModel.cs:204-208` (`OpenAsync`), `:282-288`
  (`RunCoreAsync`); the button `MainWindow.xaml:100`.
- What happens: `OpenAsync` replaces `Path` before the fields are checked; a field that fails
  returns before `HasResult = false` and `ClearTabs()`, and `_result` stays the old one.
- Case: image A is open with its result. Type `1,5` in "Viewing distance" (or empty "Widths to
  try"), then "Open image..." in the top bar (enabled: the window is idle) and choose B. The bar
  reads B's path; every tab still shows A's figures and A's verdict; the field's message is on
  the error card, behind the tabs (CR-040). `DefaultReportPath` is beside B while the result is
  A's, so the save would propose `Report.txt` in B's folder for A's report.
- Should: B is not taken until the fields are valid, or A's result is cleared when the path
  changes.
- Test: none; `ViewModelTests.ABadParameterStopsTheRunWithAMessage` has no earlier result.
- Evidence: confirmed.
- Status: fixed 5c0cf426 (ADR 0097; the result is dropped, the owner's answer of 2026-11-14)

### CR-006 Soft corners are reported on a frame whose stars are declared too few

- Where: `Analysis/StarAnalyzer.cs:211-218` (`Analyze`), `:101` (`StarResult.SoftCorners`);
  `Report/Recommendations.cs:455-458` (`Size`); `Report/ReportWriter.cs:444-445`.
- What happens: the corner / centre ratio is computed before the stars are found insufficient
  and is kept on the insufficient result; `SoftCorners` does not ask `Insufficient`, and
  `Recommendations.Size` tests it outside the `if (st.Insufficient)`.
- Case: a starless 23 MP frame on which the denoise left 60 blobs (2.6 per megapixel), 10 of
  them in the centre at 5 px and 6 in one corner at 8 px. Section 4 says "Without stars it cannot
  be measured how much detail the image holds ... only 60 stars measured" and then, as an
  Attention, "The stars in the corners are wider than those in the centre ... consider a crop",
  1.60 against the threshold 1.4. Appendix D prints "too few to measure the stars' width" and on
  the next line a zone's width (`Golden/mono-png8.txt` lines 75-76 show the pair today).
- Should: no ratio and no soft-corner verdict from detections the frame's own rule says are not
  stars (ADR 0062).
- Test: none; `SaturatedAndFaintStarsAreNotMeasured` asserts it with zero stars only.
- Evidence: confirmed.
- Status: fixed 19f667e9 (ADR 0102; the zones read n/a too, the owner's answer of 2026-11-16)

### CR-007 A FITS number that does not parse is read as its default: BZERO, BSCALE, the WCS

- Where: `Input/FitsReader.cs:409-410` (`HDbl`), `:398-399` (`HInt`); read at `:177-178`
  (`BZERO`, `BSCALE`), by `Input/FitsWriter.cs:202` and by `Input/Wcs.cs:95-132`
  (`Wcs.TryFrom`).
- What happens: `double.TryParse(v, NumberStyles.Any, InvariantCulture)`, with the default when
  it fails. .NET does not parse the `D` exponent the FITS standard allows (the member's own
  comment says so), and `NumberStyles.Any` accepts forms FITS does not have: "0,5" reads as 5,
  "1,024" as 1024, "(5)" as -5. `Wcs.TryFrom` tests presence with `ContainsKey`, reads with a
  default of 0 and checks only the determinant.
- Case: a 16-bit FITS with `BZERO = 3.2768D+04`: BZERO reads 0, the samples are taken as signed,
  everything at or above mid-scale is clipped to 0 and the report is about another image, with
  an Ok-level note. A header with `CDELT1 = -1.158D-02`: the determinant is 0 and the file is
  "no WCS" (`--wcs` refuses a header that holds a complete TAN WCS). A `CRVAL1` that is present
  but unreadable (`18:06:22`): the WCS is accepted at RA 0, Dec 0 and the report says "no
  galactic band in the frame"; `271,5922` reads as 2 715 922.
- Should: the `D` exponent read as the standard writes it; a WCS number that does not parse
  means no WCS (R-018), and a BZERO or BSCALE that does not parse is an error, not a default.
- Test: none; `FitsReaderTests` and `WcsTests` have no `D` exponent and no unreadable value.
- Evidence: confirmed (cfitsio, so Siril, writes `E`; that a given writer emits `D` was not
  checked on a file).
- Status: fixed 5a1707b4 (ADR 0103; a size, BZERO or BSCALE refuses the file, a WCS number means no WCS)

### CR-008 An 8-bit image saved as 16 bits by a shift passes as real 16 bits

- Where: `Analysis/BitDepthAnalyzer.cs:84` (the count), `:51` (`IsUpscaled`), `:44`
  (`DistinctLevels`).
- What happens: only the form `v == (v >> 8) * 257` counts as an upscale. `DistinctLevels` is
  computed and reaches no rule; it is printed only inside the warning `IsUpscaled` decides.
- Case: a 16-bit TIFF whose samples are `n << 8` (a byte copied into the high byte). `n * 257`
  differs from `n * 256` for every n but 0, so the file reads as real 16-bit data with at most
  256 distinct levels per channel, and R-096's "only 8 bits of information" warning is not
  given. Siril, GIMP and ImageMagick multiply by 257.
- Should: the distinct levels (or the low byte being constant) decide too.
- Test: none; `AnEightBitUpscaleIsCaught` builds the x 257 form only.
- Evidence: confirmed.
- Status: fixed 5d00aa35 (ADR 0104; at most 256 levels a plane, the owner's answer of 2026-11-16)

### CR-009 A second image chosen from the keyboard during a run gets the running image's result

- Where: `AstroPhotoAnalyzer.Wpf/MainWindow.xaml:212` (the placeholder's button, no
  `IsEnabled`), `:194-208` (the placeholder is visible while `HasResult` and `HasError` are
  false, so during a run); `Views/BusyOverlay.xaml:28-29`; `ViewModels/MainViewModel.cs:204-208`
  and `:276`.
- What happens: the busy overlay is a `Border` with a background, which stops the mouse and not
  the keyboard. `OpenAsync` has no busy guard (`ChooseProfile` and `ClearProfile` have one): it
  sets `Path`, and `RunCoreAsync` then returns at `if (!HasPath || IsBusy) return;`.
- Case: open image A with the placeholder's "Open image..." button, so the focus is on that
  button when the dialog closes. While A is analysed, Space or Enter opens the dialog again;
  choose B. The bar shows B at once, no run starts for B, and when A's run ends its tabs are
  filled under B's path, with no message.
- Should: the button disabled while a run lasts, as the top bar's is, and `OpenAsync` refusing a
  path while busy. Same root as CR-005.
- Test: none; `ProfileBarTests.R155_ChoosingTheSameProfileAgainRunsNothing` covers the two
  profile actions.
- Evidence: confirmed (the enabled button and the missing guard; that the focus returns to the
  button after the file dialog is WPF's usual behaviour, to see once in the window).
- Status: fixed c075e9ae (ADR 0105; the test that needed this defect was replaced, the owner's answer of 2026-11-16)

## Severity 2: a crash, a hang, or a file overwritten or lost

### CR-010 `-o` naming the analysed image replaces the image with the report

- Where: `AstroPhotoAnalyzer/AnalyzerCli.cs:154-168` (`RunInLanguage`), through
  `Report/ReportWriter.cs:63-68` (`SaveFull`) and `Input/AtomicFile.cs:54-77` (`Write`).
- What happens: nothing compares `o.Out` with `o.Image`, `o.Wcs` or `o.Icc`. By the time the
  report is saved the image is in memory and no handle holds it; `AtomicFile.Write` probes the
  target for writing, writes a temporary file and ends with `File.Move(temp, full, overwrite:
  true)`. The guard of the input lives only in `CurveExport.Guarded` (`Runs/CurveRun.cs:314`),
  which the report does not pass through.
- Case: `AstroPhotoAnalyzer m31.tif -o m31.tif`. The report prints, then `-> report saved:
  m31.tif`, the exit code is the verdict's, and `m31.tif` is a text file of a few kilobytes. The
  same with `-o stack.fit` beside `--wcs stack.fit`, and with the `--icc` profile. The reviewer
  ran the same file calls by hand: a 100 000-byte image became 39 bytes of text.
- Should: `ERROR: ...`, exit 1, the file untouched ("the input never touched", `CLAUDE.md`).
  `known-limitations.md` accepts the missing guard for the WCS file and the profile only for the
  window's exports, because the save dialog asks; the console has no dialog.
- Test: none; `CliTests.R005_*` save to a fresh name, `CurveTests.R145_SavingOverTheInputIs...`
  covers the FITS export.
- Evidence: confirmed.
- Status: fixed de089c54 (ADR 0106; refused before the analysis, the inputs held open during the save)

### CR-011 An image over 65 535 px on a side ends the whole analysis in the JPEG stage

- Where: `Analysis/JpegAnalyzer.cs:195` and `:227` (`img.SaveAsJpeg`), called unguarded from
  `Runs/AnalysisRun.cs:273`.
- What happens: the whole raster is encoded as a JPEG at each quality; JPEG stores a dimension in
  16 bits, and the size is not checked beforehand.
- Case: a 66 000 x 3 000 px panorama. The encoder is expected to throw as the last stage but
  one, with the tones, the dust, the stars and the sizes already measured and lost.
- Should: the JPEG rows left out with a note, or a refusal at load that says why.
- Test: none.
- Evidence: to measure. The format's limit is certain; that ImageSharp 3.1.11 throws rather than
  writes a broken file was not run (a flat `Raster` of 65 536 x 8 through `JpegAnalyzer.Analyze`
  settles it).
- Status: open

### CR-012 A FITS header with a negative axis length hangs the reader

- Where: `Input/FitsReader.cs:161-166` and `:196` (`LoadFrom`), `:127-130` (`ReadImageHeader`,
  so `--wcs` too), `:371-378` (`SkipExact`); the same walk in `Input/FitsWriter.cs:169-178`.
- What happens: nothing requires an axis length, or the byte count, to be non-negative.
- Case: a one-block header with `BITPIX = 8`, `NAXIS = 2`, `NAXIS1 = -2`, `NAXIS2 = 2880`. It is
  not an image, so it is skipped: the data size is -2880 bytes, `SkipExact` seeks 2880 bytes
  back, to offset 0, and returns true; the same header is read again, for ever. The console never
  returns; the window's run never ends. The same family, each from one impossible number:
  `NAXIS = 2000000000` loops two billion times before anything is refused; 50000 x 50000
  overflows `int` in `Raster.cs:63`; 40000 x 40000 x 3 in a 3 KB file allocates 19 GB
  (`FitsReader.cs:180`) before the file's length is looked at; `BITPIX = -2147483648` throws from
  `Math.Abs` (`:163`).
- Should: "FITS: ..." refused with a message; the header's numbers checked before they are used,
  in the one place the three walks share.
- Test: none; `ATruncatedFileIsAnErrorNotGarbage` cuts the data of a sound header.
- Evidence: confirmed.
- Status: open

### CR-013 A settings file with a broken escape stops the window at every start

- Where: `AstroPhotoAnalyzer.Wpf/ViewModels/WindowSettings.cs:77-89` (`Read`), called from
  `App.xaml.cs:86` (`OnStartup`).
- What happens: the catch names `IOException`, `UnauthorizedAccessException` and
  `JsonException`. `JsonDocument.Parse` accepts a string whose `\ud800` escape has no low
  surrogate; `GetString()` then throws `InvalidOperationException`.
- Case: `%LOCALAPPDATA%\AstroPhotoAnalyzer\settings.json` holds an `iccProfile` value with a lone
  high-surrogate escape. `WindowSettings.InLocalAppData()` throws inside `OnStartup`, before the
  window is built, at every start until the file is deleted by hand. The reviewer ran the body
  of `Read` on eight damaged files; this is the one shape not caught.
- Should: no profile and the Warn line, as `Read`'s summary and R-156 say ("never an exception").
- Test: `ProfileBarTests.R156_AMissingOrBrokenSettingsFile...` covers other broken shapes.
- Evidence: confirmed.
- Status: open

## Severity 3: a wrong text or a wrong display

### CR-014 "Save the report..." never becomes enabled

- Where: `AstroPhotoAnalyzer.Wpf/ViewModels/MainViewModel.cs:129-133` (`IsBusy`), `:162-168`
  (`HasResult`, `CanSave`), `:304`, `:333`; the button `MainWindow.xaml:101`.
- What happens: `CanSave => HasResult && !IsBusy`, but only `HasResult` announces it.
  `HasResult = true` (line 304) runs while `IsBusy` is still true, so the binding re-reads
  `CanSave` and gets false; `IsBusy = false` in the `finally` announces `IsIdle` and `CanRun`
  only. The button's `IsEnabled="{Binding CanSave}"` is never told the value became true.
- Case: open any image and wait for the tabs. "Open image..." and "Analyse" are enabled again;
  "Save the report..." stays grey, on every run. The reviewer rebuilt the three properties and
  two bound buttons outside the project: after the run `CanSave` is true and the button's
  `IsEnabled` false.
- Should: enabled whenever a result is shown and no run lasts (R-111).
- Test: `ViewModelTests.RunningOnAFileAndSavingGivesTheConsolesReport` asserts the getter and
  calls `SaveReport` directly; no test reads the button or listens for the change.
- Evidence: confirmed (read, and reproduced on a replica; not seen in the real window, where one
  look settles it).
- Status: open

### CR-015 One report says "no visible steps" in section 1 and warns of banding in section 5

- Where: `Report/Recommendations.cs:111-114` (`Format`, the plateau's Ok branch) and `:508-511`
  (`Denoise`, `n.BandingRisk`).
- What happens: the two sections judge banding on two measurements and neither reads the other.
- Case: `de_printat.png` with the defaults (`docs/validation.json`, V-001:
  `noise.sigmaMedianLsb` 0.4518, `noise.bandingRisk` true,
  `quantization.plateauVisibleFramePctAtReference` 0). Section 1 says "At 8 bits no visible steps
  appear in the sky: the flat areas cover too little of the frame to show"; section 5 says
  "Denoise is too strong for print: too little noise is left to hide the steps between levels
  (banding)".
- Should: the owner's call. Each rule does what its requirement says (R-091, R-095), and ADR 0060
  made the plateau share the banding verdict; what is missing is the Denoise sentence taking that
  verdict into account when the plateau is known, or saying that the two speak of different
  things.
- Test: none on the pair.
- Evidence: owner.
- Status: open

### CR-016 One of something is written as a plural, and the tab's star counts are not the report's

- Where: `Report/Recommendations.cs:415-416` (stars), `:105-106` and `Report/ReportWriter.cs:419`
  (the plateau's small areas), `Recommendations.cs:487` (the noise's small areas),
  `Report/CurveTexts.cs:74-76` (`ValuesDoNotFit`); in the window
  `AstroPhotoAnalyzer.Wpf/ViewModels/ResultTabs.cs:791-792`.
- What happens: the sentences interpolate a count before a fixed plural noun; `Fmt.RoCount` adds
  "de" from 20 on and has no singular. `ReportWriter.Clouds` and `Stars` (`:188`, `:426`,
  private) do handle one. The Rezolutie tab writes `$"{reg.Count} stele"` itself, and builds
  "coltul ..." with `reg.Name.Replace("colt ", "coltul ")` on a name the core wrote.
- Case: the two starless validation images measure exactly one star (V-010, V-013): "only 1 stars
  measured, 0.0 per megapixel" / "doar 1 stele masurate". With one clean tile: "in the 1 small
  areas of smooth sky". In the Romanian window the zone rows read "1 stele", "64 stele" where the
  report behind the same tab says "1 stea", "64 de stele".
- Should: one helper for a count with its noun, singular for one, in both languages, used by the
  report and by the tab.
- Test: `RomanianCountsTakeDeWhereTheLanguageDoes` has no case for 1.
- Evidence: confirmed.
- Status: open

### CR-017 "shrinks by ~0.82 times" says the opposite of the factor

- Where: `Report/Recommendations.cs:500` (`Denoise`).
- What happens: one sentence for both directions, with the slope as the factor: "the noise
  grows / shrinks by ~{slope} times", "zgomotul creste / scade de ~{slope} ori".
- Case: `Golden/mono-png8.txt` line 30: "zgomotul scade de ~0.82 ori ..., la ~0.86 niveluri"
  (1.05 x 0.82). "Scade de 0.82 ori" reads as divided by 0.82, which would be more noise.
- Should: for the shrinking case, the factor as what is left, or its inverse. The wording is
  pinned by the golden and by `R095_TheDenoiseFigureAfterTheReStretchSaysWhether...`: the owner
  sees the proposal first.
- Evidence: owner.
- Status: open

### CR-018 Three texts predate the files the tool now writes

- Where: `AstroPhotoAnalyzer.Wpf/MainWindow.xaml:211` (the first screen);
  `AstroPhotoAnalyzer.Core/ThirdParty.cs:86-87` (the role of `Wacton.Unicolour`); `README.md:553`.
- What happens: the first screen says "The tool measures and recommends; it writes no file to
  send to the lab." Since ADR 0032 and ADR 0083 it writes a FITS with a curve and a FITS or TIFF
  fitted to the paper ("the file is what the lab takes"). "About" and the README say Unicolour
  serves the Dmax, the Curves Editing tab and the Soft proof tab; the Colours for paper tab uses
  it too (`Runs/GamutFitRun.cs:136`, `:152`).
- Should: the three texts match what the tool does, in both languages; the first is a rewrite
  for the owner to approve.
- Test: none on the wording.
- Evidence: confirmed.
- Status: open

### CR-019 "Real detail across the width" shows a count of stars with the unit px

- Where: `AstroPhotoAnalyzer.Wpf/ViewModels/ResultTabs.cs:780-786`.
- What happens: the value is `~{EffectiveDetailPx} px`; `EffectiveDetailPx` is the width in
  pixels divided by the stars' width, a number of star widths.
- Case: a 6000 px image with stars of 4 px reads "~1500 px" over a description that says it is
  how many stars fit side by side; the report gives the same figure as "~1500 stars fit side by
  side across the width".
- Should: no "px", or "stars"; whether "px" was meant as "effective pixels" is the owner's to
  say.
- Evidence: owner.
- Status: open

### CR-020 A section heading reads "1 .  Title", an About component "Name  -  role"

- Where: `AstroPhotoAnalyzer.Wpf/Theme/Dark.xaml:460-464` (`SectionTemplate`), `:542-546`
  (`ComponentRowTemplate`).
- What happens: three `Run` elements on three lines; in a `TextBlock` the line break between two
  runs is kept as a space. The theme knows it: `GuideParagraphTemplate` (`:528`) writes its runs
  on one line.
- Case: every section heading of the Raport tab has a space before its full stop and two after
  it (the reviewer's off-screen layout: `[1 .  Title]`).
- Should: `1. Title`, `Name - role`.
- Test: none reads these inlines.
- Evidence: confirmed (one look at the Raport tab shows it).
- Status: open

### CR-021 In the tab, the tooltip of `Original` promises a key only the full screen has

- Where: `AstroPhotoAnalyzer.Wpf/Views/SoftProofControls.xaml:45` against `:42`; the keys are
  handled in `Views/OverlayWindows.xaml.cs:107-113` only.
- Case: on the Soft proof tab, hover `Original`: "Key O." Press O: nothing happens. The other
  box says "In full screen: key G."
- Should: "In full screen: key O.", in both languages.
- Evidence: confirmed.
- Status: open

### CR-022 A disabled check box looks enabled

- Where: `AstroPhotoAnalyzer.Wpf/Views/MapWindow.xaml:58` (`IsEnabled="{Binding HasWcs}"`),
  `:71` (the cloud list); `Theme/Dark.xaml` has no `CheckBox` or `RadioButton` style.
- What happens: the system template dims only the little square; the text keeps its colour and
  nothing changes opacity (the theme's `Button` and `ComboBox` go to 40 %).
- Case: open the map of a PNG (no WCS). The box "the catalogued dust clouds: circles and names
  ..." is disabled and reads exactly like the five enabled boxes above it; a click does nothing.
- Should: a disabled box dimmed as the disabled buttons beside it.
- Evidence: confirmed (the template was probed off screen; a look at the map confirms it).
- Status: fixed 24933a57

### CR-023 Every FITS gets the "no colour profile" caution, and its profile is never looked for

- Where: `Input/FitsReader.cs:95` (`Icc = null`), `Input/Raster.cs:92`;
  `Report/Recommendations.cs:541-544` (`Warnings`).
- What happens: a FITS never has a profile for the tool, so section 6 always says "The file has
  no colour profile (ICC): the lab will assume sRGB ..." and the exit code is at least 2
  (`Golden/dark-sky-wcs*.txt`). R-096 states the rule without an exception for the format nobody
  sends to a lab.
- To measure with it: one reviewer recalls that Siril 1.2 and later writes the working profile
  into an extension HDU named `ICCProfile`; not checked on a file. If so, a FITS in a wider space
  than sRGB is measured as sRGB and its paper-fit TIFF gets `IccProfiles.Srgb`.
- Should: the owner's call on the caution; the HDU, if it exists, read.
- Evidence: owner.
- Status: open

### CR-024 The description of "Under L* 15" is false on canvas

- Where: `AstroPhotoAnalyzer.Wpf/ViewModels/ResultTabs.cs:303-309`.
- What happens: a fixed text: "The paper still tells these tones apart ... The share includes
  the pixels under paper black."
- Case: `canvas` (Dmax 1.5), or any Dmax under 1.72: paper black is L* 20.7, above 15. Every
  tone under L* 15 is under paper black, so the paper does not tell them apart, and the share
  holds only part of the pixels under paper black. The histogram's note beside it was written to
  hold on canvas (`:183-189`, R-113).
- Should: a text that says nothing about which of the two thresholds is lower.
- Test: `R113_TheTonesTextsAreInPlainWords` checks the words on the default medium.
- Evidence: confirmed.
- Status: open

### CR-025 Parameters typed with decimals are reported rounded

- Where: `Report/ReportWriter.cs:107` (`Header`), `:467` (`SizeTableRow`), `:530-535`
  (`AppendixParameters`); `Report/Recommendations.cs:402-408`, `:415-416`, `:423-435`, `:448`;
  accepted by `AstroPhotoAnalyzer/Options.cs:155-164` and
  `AstroPhotoAnalyzer.Wpf/ViewModels/ParameterFields.cs:427-438`.
- What happens: the given distance is printed with one decimal, the widths, the passepartout and
  the frame with none; the figures are computed on the values as given. `Fmt.Dmax` was written
  against the same defect ("--dmax 2.35 showed as Dmax 2.4").
- Case: `--distance 1.25`: "Viewing distance: 1.3 m", and without stars "the threshold is 70 dpi
  = 25.4 / (0.29 mm/m x 1.3 m)", a formula that gives 67. `--sizes 59.4,84.1`: the rows `59` and
  `84`. `--sizes 100.4`: a row `100 (over the limit)`. `--sizes 60.2,60.4`: two rows `60`.
  `--passepartout 7.5 --frame 2.5`: Appendix F, "every parameter as used", says 8 cm and 3 cm.
- Should: the values as given.
- Test: none (`R101_ADmaxKeepsItsSecondDecimal...` covers the Dmax alone).
- Evidence: confirmed.
- Status: open

### CR-026 A width over 100 cm gets three texts that contradict the table

- Where: `Report/Recommendations.cs:465-468`, `:404-406`, `:429-432` (`Size`); the reference row
  is chosen in `Analysis/PrintSizeAnalyzer.cs:219-220`.
- Case: a starless frame 6000 px wide, `--sizes 80,100,120 --distance 3`: section 4 says
  "Without stars it cannot be measured how much detail the image holds" and then "the sizes over
  100 cm have enough detail, but are not recommended"; it says "the file can be printed up to
  100 x 67 cm" and calls that a limit of the file, when the 120 cm row passes the same test. A
  frame with stars, `--sizes 120,150 --distance 5`: "Recommended: a 120 x 80 cm image", the row
  marked `120 *` with `(over the limit)`, and the next line "the sizes over 100 cm ... are not
  recommended".
- Should: without stars the note speaks of pixels, not of detail; "up to" names the cap as a
  cap; with every candidate oversize nothing is called recommended.
- Test: `AnOversizeRowIsNeverTheReferenceEvenIfItPasses` covers the analyzer, not these texts.
- Evidence: confirmed.
- Status: open

### CR-027 "with the passepartout and the frame" when there is none

- Where: `Report/Recommendations.cs:431`, `:448`.
- Case: `--passepartout 0 --frame 2`: "~74 x 53 cm with the passepartout and the frame". With
  both at 0 the sentence gives the image's own size twice.
- Should: name only what is there; drop the outer size when it is the image's.
- Evidence: confirmed.
- Status: open

### CR-028 On a mono image five texts still speak of colour and of three channels

- Where: `Report/Recommendations.cs:298-310` (`Colour`, the neutral note);
  `Report/ReportWriter.cs:507-509` (`AppendixBackground`);
  `AstroPhotoAnalyzer.Wpf/ViewModels/ResultTabs.cs:270-276` and `:317-321`.
- Case: a mono H-alpha frame with a nebula in a corner, +6 levels. Section 3 holds "A
  black-and-white image: it has no colour tint to measure" and then "Some edges are brighter ...
  but without a colour of their own", because "all the channels change alike, so it is not a
  colour". Appendix E's first line on mono is "fundalul, un singur canal (0..255): 64.0", a
  third figure under the bare name, against the rule "'Fundal' names one thing". In the Tonuri
  tab the row "Landmarks on the only channel" is explained as shares "of the green channel's
  pixels", and "Channel means", with one value, as "each channel (red, green, blue)".
- Should: the mono wording the neighbouring rows already have.
- Test: `R093_MonoHasNoColourToJudge` covers the first line; the golden pins Appendix E.
- Evidence: confirmed.
- Status: open

### CR-029 A neutral edge darker than the sky is explained as "the Milky Way entering a corner"

- Where: `Report/Recommendations.cs:303-309` (`Colour`).
- What happens: `brighter` changes only the first words; the explanation is one.
- Case: four corners at -6 / -6 / -6 (a flat that under-corrects): "Some edges differ from the
  rest of the sky ...: most likely it is the object itself (the Milky Way entering a corner,
  say)". The coloured branch of the same method says of a darker region that it may be dust or
  vignetting (`:286`). ADR 0018 decided the note for a neutral brightening.
- Should: the darkening's own explanation.
- Evidence: confirmed.
- Status: open

### CR-030 Three tables lose their columns

- Where: `Report/ReportWriter.cs:387-388` (`CloudLine`, `{c.Name,-28}`), `:466-467`
  (`SizeTableRow`, a dpi column of 3); `AstroPhotoAnalyzer.Wpf/ViewModels/ResultTabs.cs:829-832`
  (the dpi scale).
- Case: three catalogue names are longer than 28 characters (the Taurus cloud's has 54); such a
  row sits up to 26 columns right of its header. A 6000 px frame with `--sizes 13,15,20` has
  1172 and 1016 dpi on its first two rows. In the Rezolutie tab the title "pixel mm" (8
  characters) stands over a column 6 wide, so every title after it is shifted, by a different
  amount in each language.
- Should: a name cut or a column as wide as the longest name shown; a dpi column of four; the
  scale's widths as `language.md` asks.
- Evidence: confirmed.
- Status: open

### CR-031 The red sentence of two tabs is cut at the right edge

- Where: `AstroPhotoAnalyzer.Wpf/Views/GamutFitView.xaml:37-52`, `Views/CurvesView.xaml:37-55`.
- What happens: the `Message` text block (`TextWrapping="Wrap" MaxWidth="900"`) sits in a
  horizontal `StackPanel`, which offers unlimited width: the text wraps at 900 whatever room is
  left, and the part past the tab's edge is clipped. `SoftProofView.xaml:40-53` holds the same
  message in a vertical panel and wraps.
- Case: no ICC profile, the Culori pentru hartie tab, the window at its default width of 1400:
  the reviewer's layout puts 211 px of the first line past the edge. The same at Full HD with
  125 % scaling.
- Should: the sentence wrapped inside the tab.
- Test: none asserts it (`WindowSmokeTests` renders at 1400 x 860, where the PNG would show it).
- Evidence: confirmed.
- Status: fixed 24933a57

### CR-032 The map, the preview and the full screen open on the primary screen

- Where: `AstroPhotoAnalyzer.Wpf/Views/MapWindow.xaml:31`, `PreviewWindow.xaml:32`,
  `SoftProofWindow.xaml:36`; opened from `MainWindow.xaml.cs:55`, `:56`, `:63`.
- What happens: the three windows are maximised in XAML with no start-up location (the About,
  profile and curve windows say `CenterOwner`); an owner does not move them.
- Case: two screens, the main window on the second (the calibrated one). "Full screen" on the
  Soft proof tab likely covers the primary screen, and has no title bar to drag it back with.
- Should: the screen the main window is on.
- Evidence: to measure (a two-screen run; known WPF behaviour, not provable by reading).
- Status: open

### CR-033 A cloud's name on the map is cut to the width of its circle

- Where: `AstroPhotoAnalyzer.Wpf/Views/MapWindow.xaml:116-121`.
- What happens: the name is a child of a grid as wide as the circle; WPF clips a child larger
  than its slot, so only the middle `Diameter` pixels of the name are drawn.
- Case: the reviewer's layout: a 69 px name in a 12 px circle is clipped to 12 x 10.
- Should: the whole name, centred over the circle.
- Evidence: to measure (the clip is certain; how many names it cuts on the plate-solved FITS
  needs a look at its map).
- Status: open

### CR-034 The map window's cloud list has no room on a low screen

- Where: `AstroPhotoAnalyzer.Wpf/Views/MapWindow.xaml:43-82`.
- What happens: everything but the cloud list (five legends, the clouds box, the definition, the
  heading, the note, All / None) is docked to the top with no scroll bar; the list gets what is
  left.
- Case: the reviewer estimates the top block at 750 to 800 DIP from the text lengths. A
  maximised window has about 790 DIP at 125 % scaling and 650 at 150 % on Full HD: the list, "the
  one place the window lists them" (R-130), is left a few pixels or none.
- Should: the whole column scrolls, or the legends do.
- Evidence: to measure (open the map of a plate-solved frame at 125 % and 150 %).
- Status: open

### CR-035 The window is not per-monitor DPI aware

- Where: `AstroPhotoAnalyzer.Wpf/AstroPhotoAnalyzer.Wpf.csproj:24`; the project has no manifest.
- What happens: `<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>` is read by the
  Windows Forms bootstrap only. The reviewer measured a `UseWPF` project with the same property
  and no manifest: system-DPI aware.
- Case: two screens with different scaling. On the second the window is stretched by Windows as
  a bitmap: soft text, and a view at the image's own size is no longer one image pixel per
  screen pixel.
- Should: what the csproj line means to give. Making the window per-monitor aware changes how
  `ScreenArea` and the About window's fit must be checked: a task with a measurement.
- Evidence: to measure (the awareness is measured on a like project; the blur needs two screens).
- Status: open

### CR-036 A redirected report is in the console's code page, the `-o` file in UTF-8

- Where: `AstroPhotoAnalyzer/Program.cs:57` (`Console.Out`; the output encoding is never set).
- Case: in `cmd`, a path or a profile name with a letter outside ASCII and `> r.txt`: the `File:`
  line holds OEM bytes; the same run with `-o` gives UTF-8. The core's own texts are ASCII.
- Should: one encoding for both.
- Evidence: to measure (the built console in `cmd` on such a path, the bytes looked at).
- Status: open

### CR-037 A mono image saved as a TIFF from the paper fit carries an RGB profile

- Where: `Runs/GamutFitRun.cs:222-223` (`GamutFitExport.Save`); `Input/TiffWriter.cs:73`, `:92`.
- What happens: `a.Raster.Source?.Icc ?? IccProfiles.Srgb` is embedded beside `Photometric` 1.
- Case: a mono FITS, `Salveaza ca...` to `x.tif`: a one-channel TIFF with a three-channel RGB
  profile in tag 34675, which a colour-managed reader refuses or ignores. (The fit changes
  nothing in a mono image, so there is little reason to save one: see the owner's look.)
- Should: no profile, or a grey one.
- Test: `R177_*` write colour rasters only.
- Evidence: confirmed (the bytes written; what a given reader does was not run).
- Status: open

### CR-038 The window reports a bad WCS file with the console's flag, and a quoted path as missing

- Where: `Runs/AnalysisRun.cs:142-163` (`BorrowWcs`, four messages that start with `--wcs: `);
  `AstroPhotoAnalyzer.Wpf/ViewModels/ParameterFields.cs:238`; shown through
  `MainViewModel.Describe`.
- Case: in the window, a FITS of another size in the WCS field: the error card reads `--wcs: the
  FITS is 5837 x 3867 px, the image 5938 x 3888 ...`, a flag the window does not have. Explorer's
  "Copy as path" gives the path in quotes: `--wcs: the file does not exist: "E:\stack\x.fit"`,
  for a file that exists (the field is trimmed of spaces only, and has no browse button).
- Should: the owner's call. R-027 asks for "an error naming `--wcs`" with the console's example,
  and `WcsBorrowTests.R027_...NamingTheFlag` pins the prefix in the core.
- Evidence: owner.
- Status: open

### CR-039 Three texts still name the method: "grid", "median"

- Where: `Report/ReportWriter.cs:272-279` (`SimulationFooter`: "no target of the grid" / "nicio
  tinta din grila", three wordings); `Report/ReportWriter.cs:166-167` ("median L* (the sky)") and
  `AstroPhotoAnalyzer.Wpf/ViewModels/ResultTabs.cs:291` ("Median L* (the sky)");
  `Runs/AnalysisRun.cs:197-199` (the linear refusal, "median").
- What happens: the rule "Plain words" names both words. The recommendation of the same case was
  rewritten to "No target of the simulation (up to 0.20)", so one report says "simulation" in
  section 2 and "grid" under the table. The label "Median L* (the sky)" is the median of every
  pixel, while the sky of R-044 is the median of the sky patches, printed a few lines away with
  another value (18.0 against 17.6 on the plate-solved FITS).
- Should: the owner's wording. `PrintStretchSimulatorTests.cs:299` pins "grila", the
  `linear-fits` goldens pin "mediana"; `R112_...` bans the words in the recommendations only.
- Evidence: owner.
- Status: open

### CR-040 A field's error is drawn behind the tabs when a result is on screen

- Where: `AstroPhotoAnalyzer.Wpf/ViewModels/MainViewModel.cs:282-288`; `MainWindow.xaml:215-224`
  (the error card, then the tab control, in one grid cell).
- What happens: a refused field sets `Error` and returns with `HasResult` still true; the card
  and the tabs are both visible, the tabs declared later and so drawn on top.
- Case: with a result shown, type `1,5` in "Viewing distance" and press "Analyse". The tabs stay;
  the card that says the field is not a number lies behind them. To the user the button did
  nothing. The same when "Use" or "Clear" re-runs with a bad field.
- Should: R-110, "an error card when a run failed, otherwise the tabs": one or the other. Same
  root as CR-005.
- Test: none renders the two together.
- Evidence: confirmed (how much of the card shows between the sections needs a look).
- Status: open

### CR-041 A Dmax of 0.1 or less prints a zero or negative neighbouring Dmax

- Where: `Analysis/PrintStretchSimulator.cs:426` (`Simulate`); printed by
  `Report/ReportWriter.cs:352-353` (`ExactLine`).
- What happens: `PaperBlackLstar(p.Dmax - Thresholds.DmaxSensitivityStep)` with no floor; R-020
  accepts 0 < n <= 4.
- Case: `--dmax 0.05`: the lower neighbour is Dmax -0.05, a paper whiter than white (L* 104.5);
  all 56 steps of the ladder are lifted and Anexa B reads "(> 0.60 if the Dmax is -0.05, ...)".
  `--dmax 0.1` prints "if the Dmax is 0.0".
- Should: no lower-neighbour figure when Dmax - 0.1 is not positive, or a floor on the
  parameter that leaves room for the step.
- Evidence: confirmed (the printed line is read from `ExactLine`, not run).
- Status: open

### CR-042 A figure rounded at a boundary contradicts the word beside it

ADR 0029 point 5 settled this for the sky and L* 15 (`PrintStretchSimulator.cs:107` compares the
figure as printed). The other comparisons are still made on the exact value and printed rounded.

- Where and case (rule tested at, printed at, what comes out):
  - `Analysis/DustAnalyzer.cs:489`, `Analysis/PrintStretchSimulator.cs:312-313`: a span of 19.97
    degrees prints "a field too narrow for the band: 20.0 degrees of galactic latitude, under
    20".
  - `DustAnalyzer.cs:819`, `PrintStretchSimulator.cs:277-279`: "too little dust: 5.0 % of the
    frame, under 5 %".
  - `Analysis/BackgroundAnalyzer.cs:331`, `:89`: a region with 24.8 % sky reads "(only 25 % sky:
    measured on every pixel)"; the same in `Report/Recommendations.cs:290-291`.
  - `PrintStretchSimulator.cs:90`, `:174`, `:177`, `Recommendations.cs:142-144`, `:181-183`:
    "The dust is darker than paper black", both printed `L* 9.0`; the same for the sky.
  - `BackgroundAnalyzer.cs:176`, `Recommendations.cs:369-370`: "(chroma): 3.0, threshold 3.0,
    passed".
  - `BackgroundAnalyzer.cs:92`, `Recommendations.cs:290-291`, `ReportWriter.cs:515`: "R +3.0 ...
    (threshold 3.0)" with a caution.
  - `Analysis/StarAnalyzer.cs:101`, `Recommendations.cs:458`: "1.40 (threshold 1.4)" with a
    caution.
  - `Analysis/NoiseAnalyzer.cs:68`, `Recommendations.cs:119`, `:505-506`: "0.50 levels, under
    0.5".
  - `Recommendations.cs:427-428`: "looks like a point from 1.50 m; the given distance 1.5 m" with
    a caution.
  - `Report/CurveTexts.cs:216-222`: a warning because "= 1.50 levels; the print target 0.8-1.5".
  - `Report/GamutFitTexts.cs:73-75`: a red row "0.0 % of the pixels have a tone the paper cannot
    make".
  - `AstroPhotoAnalyzer.Core/Parameters.cs:290-297`: `--passepartout 2.5 --frame 1.27` is refused
    with "the passepartout (2.5 cm) is under twice the width of the frame (1.3 cm) ... Widen the
    passepartout to at least 2.5 cm".
- Should: compare as printed, or print one more decimal when the figure rounds onto its
  threshold. The owner's look below has the related row R-043 itself shows (the dust printed
  equal to paper black with no mark).
- Test: none on these edges.
- Evidence: confirmed.
- Status: open

### CR-043 A caution on the JPEG line gives neither the threshold it failed nor what to do

- Where: `Report/Recommendations.cs:82-86` (`Format`).
- What happens: the text and the `Because` are the same for Ok and Attention; the `Because`
  names the errors but neither `JpegMaxErrorReject` (10) nor `JpegOverThresholdWarnPct` (5 %),
  the two thresholds the verdict tested.
- Case: q100 with a largest error of 12.0 levels: "! If the lab takes only JPEG: quality 100
  ..." with section 1 at `[CAUTION]`, the advice still "quality 100".
- Should: the threshold passed in the `Because` (the rule "Every recommendation carries its
  evidence"), and a text that says the JPEG is not recommended here.
- Evidence: confirmed.
- Status: open

### CR-044 An embedded profile whose name cannot be read gets no warning

- Where: `Analysis/FileFacts.cs:48-50` (`IccLooksSrgb`), `:77` (`HasIcc`);
  `Report/Recommendations.cs:541-548` (`Warnings`).
- What happens: `IccLooksSrgb` is null when the profile has no readable description, and the
  warnings test `!f.HasIcc`, then `f.IccLooksSrgb == false`: neither branch is taken.
- Case: a TIFF with an Adobe RGB profile whose `desc` the reader cannot decode (CR-046 is one
  way): the header prints "colour profile (ICC): (no name)", section 6 says nothing, and every L*
  was measured assuming sRGB. The same profile named gets the caution.
- Should: "cannot tell whether it is sRGB", said as such.
- Evidence: confirmed (the branch; whether a real export produces such a profile needs a file).
- Status: open

### CR-045 A TIFF's reader notes overwrite one another

- Where: `Input/ImageSharpReader.cs:185-191` (`Facts`).
- What happens: three plain assignments to `f.Note` (palette, pages, bits per sample);
  `FitsReader` joins its two notes with "; ".
- Case: a two-page 32-bit TIFF: the note says only the bits; that a second page was not read is
  not said (R-012, R-014).
- Should: both remarks.
- Evidence: confirmed.
- Status: open

### CR-046 The name of a v4 profile is lost when its first record has no language code

- Where: `Input/IccProfileInfo.cs:98-104` (`ReadTextTag`, `mluc`).
- What happens: the record size is read at offset 16, where the first record's language and
  country are; it is at offset 12. With "enUS" the value is far over 12, so the check passes by
  accident.
- Case: a v4 profile whose `desc` has one record with language 0x0000 and country 0x0000: the
  size reads 0, the name is null, and the profile is shown under its file name (or "profile
  without a name") in the profile window, Anexa F, the Soft proof caption and the saved file.
- Should: the size from offset 12.
- Test: none; both `mluc` fixtures use `enUS`.
- Evidence: confirmed.
- Status: open

### CR-047 A failed write onto a file of the input's length is told as a save over the input

- Where: `Runs/CurveRun.cs:334-347` (`CurveExport.Guarded`), used by both exports.
- What happens: any `IOException` or `UnauthorizedAccessException` on a target that exists and
  has the input's length becomes "Not saving over the input image ...". A FITS saved from a FITS
  has the input's length whenever the added `HISTORY` cards fit the header's padding.
- Case: save `x_Puternica.fit`; later save to that name again while another program holds it
  open, or after it was made read-only. The message says the target is the input; it is not.
- Should: the system's message alone for a target that is another file.
- Test: `R145_TheInputIsProtectedWhateverSpellingTheTargetReachesItBy` covers the true case.
- Evidence: confirmed.
- Status: open

### CR-048 With the dust at black the shadow chart runs to the bright clouds, not the band

- Where: `Analysis/CurveAdvisor.cs:350` (`ShadowChartLimit`), `:304` (`PointsOf`).
- What happens: the limit is `CurveShadowChartFactor` times the fourth point's input; with the
  dust at 0 the curve has six points and the fourth is the bright clouds'.
- Case: anchors 0, 0.169, 0.306, 0.543, 0.720: a "shadow" chart from 0 to 0.68 where R-147 says
  1.25 times the band's input, 0.38.
- Should: the owner's call. The method's comment and the one on `CurveShadowChartFactor`
  describe the six-point case as it behaves; R-147 and `CurveTexts.cs:110-111` say the band.
- Evidence: owner.
- Status: open

### CR-049 With no sky patch the map's dust line still says "the darkest 1 % of the sky"

- Where: `AstroPhotoAnalyzer.Wpf/ViewModels/MapViewModel.cs:207-210` (`LegendOf`), against
  `Analysis/PrintStretchSimulator.cs:267-287` (`DustDescription`) and
  `Analysis/DustAnalyzer.cs:801`.
- What happens: the view-model words by itself which rule gave the dust's L*, in two cases. The
  core's sentence has the third: with an empty sky class the percentile is taken over every
  patch, and `DustDescription` says "the darkest 1 % of all the patches (no sky found; ...)".
- Case: a frame with no sky patch whose dust comes from the darkest patches: the dust legend
  says "the darkest 1 % of the sky", the sky legend under it "there is no sky patch".
- Should: the core's own sentence (a second wording of a sentence the core owns is how the two
  drifted).
- Evidence: confirmed (which real frame gives an empty sky class needs an image).
- Status: open

### CR-050 A curve save confirmed after a recompute landed writes nothing

- Where: `AstroPhotoAnalyzer.Wpf/ViewModels/CurvesTab.cs:405-434` (`Show`, `_index = 0` at
  `:412`), `:503-517` (`Save`, `Current.Suggestion` at `:507`); `Runs/CurveRun.cs:291`.
- What happens: a recompute for a larger area replaces the gallery and goes back to the first
  item, the original. `Save` takes whatever is current when the dialog returns; on the original
  the suggestion is null.
- Case: open the tab while the recompute for the real area still runs, press `Urmatoarea`, then
  `Salveaza ca...`. The recompute lands under the modal dialog; confirm it: no file is written
  and the red line reads "Object reference not set to an instance of an object."
- Should: the curve the dialog was opened for is saved; keeping the position across a recompute,
  as the paper fit does (`GamutFitTab.cs:346`), removes the jump too.
- Test: none saves across a recompute.
- Evidence: confirmed (the path; the timing, one to three seconds, needs a run).
- Status: fixed 24933a57

### CR-051 A soft-proof intent the core refuses is shown with " (Parameter 't')" and can take the controls away

- Where: `AstroPhotoAnalyzer.Wpf/ViewModels/SoftProofTab.cs:463-478` (`RenderAsync`),
  `:415-424` (`ComputeCore`); the throw `Analysis/SoftProof.cs:412`.
- What happens: the core throws its reason as `new ArgumentException(t.Problem, nameof(t))` and
  the tab shows `e.Message`, which .NET ends with " (Parameter 't')". The first computation has
  another catch, which resets the set, so the controls go.
- Case: a printer profile one of whose tables is refused. Choosing that intent shows the red
  sentence with the English tail. Opening an image while that intent is selected shows the
  sentence in grey with no controls, so the two intents that would work cannot be chosen (R-163
  says the controls stay).
- Should: the core's reason alone; the first computation keeps the prepared set when only the
  render was refused.
- Test: none; `R163_TheControlsStayWhileARenderRuns...` says the refusal is read, not tested.
- Evidence: to measure (the text and the lost controls are read; that a real profile produces a
  refused table is not shown).
- Status: open

### CR-052 The hand recipe can read "saturation at 100 %; it would leave 100.0 %"

- Where: `Report/GamutFitTexts.cs:86-92` (`RecipeLine`), from `Analysis/GamutFit.cs:428-447`
  (`Recipe`).
- What happens: when no pixel outside can be brought in by a factor, the loop stops at its first
  step with a factor of 1 and 100 % left; `RecipeLine` treats only `LstarCeiling <= 0` as
  "nothing to do".
- Case: a canvas profile and a frame whose only pixels outside are dark tones under the paper's
  black: the tab's last row gives a mask "with the saturation at 100 %; it would leave 100.0 %".
- Should: the sentence that no mask helps.
- Evidence: to measure (the code path is read; whether a real image reaches it needs a run).
- Status: open

### CR-070 Two texts of the dust move can print a figure equal to the one it is compared with

Added on 2026-11-16, with the dust move of the suggested curves (R-184, ADR 0101). The same kind
as CR-042, in texts that did not exist when that entry was written.

- Where: `Report/CurveTexts.cs` (`PaperChecks`, the branch of a curve with the dust move;
  `DustLines`); `Analysis/CurveAdvisor.cs` (`AimDust`, where `StoppedShort` is decided).
- What happens: both are decided on the exact L\* against `CurveDustAimTolerance` (0.05) and
  printed with one decimal.
- Case: a move that stops with the dust 0.05 to 0.1 L\* from paper black. Under it (a lift that
  stopped short): the check reads "the dust at L\* 9.0 after the curve, paper black is L\* 9.0".
  Either way: the evidence reads "from L\* 7.7 to 3.4 (paper black is L\* 3.4 at Dmax 2.43);
  further than that the curve would no longer rise smoothly".
- Should: as CR-042: compare as printed, or print one more decimal when the figure rounds onto
  the other.
  `AimDust` also returns a move as stopped short, whatever its distance from paper black, when
  the end of the bisection's range does not reach it (the two early returns): the tolerance of
  ADR 0101, decision 12, is applied to the cut moves only, and the evidence would then give the
  spline as the reason where the limit is the range. No valid move was found on that path.
- Test: none on these edges.
- Evidence: confirmed (by reading; no image has shown it: the owner's profile, Dmax 2.43, cuts
  the move by less than the tolerance, which reads as arrived).
- Status: open

### CR-073 A curve can be missing from the gallery with the dust move: its strengths are judged with other points than the ones it gets

Added on 2026-11-16, by the review of the whole branch of the dust move (R-184, ADR 0101).

- Where: `Analysis/CurveAdvisor.cs` (`AimDust`: `Valid`, over `CurveStrengths`, on the outputs of
  the plain set's strongest curve; `Curves` with a move: `Cap` through `Points`, then
  `if (c.IsSuggestible)`); `Runs/CurveRun.cs` (the fallback to the plain set, only when no curve
  is left).
- What happens: the move is validated against the plain set's points above the sky, but the set
  with the move computes its two caps again with the move's points in place. Where the full
  curve needs a cap, the bisection can end on another factor, so the curves above the sky
  differ from the ones the move was judged with, and a strength whose spline is then not
  suggestible is left out silently. `Cap` also takes the factor 0 as suggestible, which holds
  without the move and is not shown with it. `Maxima` is in neither judgement, so it can exist
  in one gallery and not in the other.
- Case: none found. On the four reference images the full curve needs no cap; it takes an image
  whose plain `Puternica` is capped.
- Should: the move judged on the set it ends in, or a set with the move that loses a curve
  falling back to the plain one with the reason beside the box.
- Test: none.
- Evidence: to measure (by reading; an image with a capped full curve would show it).
- Status: open

### CR-075 On a frame with too few stars a zone reads n/a under a description that promises its width from 5 stars

Added on 2026-11-16, from the fix of CR-006 (ADR 0102) and the review of its branch.

- Where: `AstroPhotoAnalyzer.Wpf/ViewModels/ResultTabs.cs`, the description of the centre's row
  of the Rezolutie tab ("A zone is shown only when it has at least 5 stars, otherwise n/a").
- What happens: since ADR 0102 a frame whose stars are too few has no width in any zone, however
  many detections the zone holds; the description names the zone's own count as the only reason.
- Case: the scene of `Golden/mono-png8.txt` (16 detections, 6 in the centre): the row reads
  `n/a` beside "6 stars", under that sentence. The row "Stars measured" above it does say the
  stars are too few.
- Should: the sentence names both reasons. A text of the window changes only on the owner's yes
  (the rule "Plain words"); a wording was put to the owner on 2026-11-16.
- Evidence: owner.
- Status: open

### CR-076 "You lose nothing going to 8 bits" is also said of a file caught by its levels alone

Added on 2026-11-16, by the review of the fix of CR-008 (ADR 0104).

- Where: `Report/Recommendations.cs` (`Warnings`), the sentence of the 8-bit upscale.
- What happens: the sentence was written for the form level x 257, which an 8-bit copy holds
  exactly. Since ADR 0104 it is also given when no plane uses more than 256 levels, where the
  levels need not sit on the 8-bit steps.
- Case (arithmetic, not run): a file saved by a shift, level x 256, rounded to 8 bits
  (`RasterOps.Level8`): level 128 gives 127.50 and level 129 gives 128.50, both 128, so one pair
  of neighbouring levels merges. A file whose 256 levels are packed closely (20000 to 20255 of
  65535) keeps one or two levels in an 8-bit copy.
- Should: "only 8 bits of information" stays true in both; the half about losing nothing holds
  for the first form only, so the levels' case needs a sentence of its own, or one that holds
  for both. The owner's call on the wording.
- Evidence: owner.
- Status: open

## Severity 4: robustness and cleanliness

### CR-053 Four theme defects: tab headers, the report's corner, the progress bar, the focus

- Where: `AstroPhotoAnalyzer.Wpf/Theme/Dark.xaml:56-61` (the implicit `TextBlock` style),
  `:240`, `:251-256` (`TabItem`), `:163-178` (`LogBox`), `:344-362` (`ProgressBar`);
  `Views/TonesView.xaml:63`, `Views/MapWindow.xaml:67-68`, `Views/ReportView.xaml:38-40`,
  `Views/BusyOverlay.xaml:34`.
- What happens:
  - The implicit `TextBlock` style, merged at application level, beats the `Foreground` and
    `FontSize` a `TabItem`, a `Button` or a `CheckBox` sets: unselected tab headers are as bright
    as the selected one and do not answer the mouse; the `Preview` and `All` / `None` buttons
    are drawn at 13, not the 11 they ask for; the Raport tab's check box is not muted.
  - The text report's scroll viewer is the system's: an 11 x 11 near-white square where the two
    bars meet.
  - The `ProgressBar` template has no animated part, so the busy overlay's indeterminate bar is
    a full, still line for the 10 to 15 s of an analysis (FlatCheck's review found the same
    template, its CR-044).
  - No `FocusVisualStyle` anywhere: the system's black dotted rectangle on a dark theme, so the
    keyboard focus cannot be seen.
- Should: what the setters say; the theme's background in the corner; a moving indicator or no
  bar; a focus rectangle in a colour of the theme.
- Evidence: to measure (each was probed off screen on the .NET Framework's WPF; one look at the
  window per item).
- Status: open

### CR-054 Numbers typed into texts, and decisions made a second time outside the core

The rules "Explanation is not diagnosis" and "Computed only in the core". Every figure is right
today; each goes wrong the day its constant or its rule moves, and no test would see it.

- Typed numbers:
  - `AstroPhotoAnalyzer.Wpf/ViewModels/ParameterFields.cs:400-418`: "twice" (which is
    `PassepartoutMinFrameRatio`), "10-15 %", "~1 cm", "60-80 cm", "3-4 cm". The test
    `R026_TheFramingFieldsCarryHelpText` pins the words.
  - `ViewModels/ResultTabs.cs:904-905`, `:910-911`: "About 2 ... begins to show at 3 to 5",
    beside the interpolated `CastWarnChroma`; `:680`, `:684`: "costs 1-2 levels".
  - `Views/ResolutionView.xaml:42`, `:44`, `:52`: 0.29 mm, 180 dpi, 0.14 mm, 58 dpi at 1.5 m,
    300, 2.54; `Views/SoftProofView.xaml:87`, `Views/SoftProofWindow.xaml:97`: "8:1", "8 x 8"
    (`ZoomPanViewer.MaxScale`, which `GamutFitTab.cs:168` interpolates).
  - `Report/Recommendations.cs:403`: "~0.5 m", in the sentence whose `Because` computes 0.49 m
    from the constants; `:505-506`: "the smoothest 10 %".
  - `Report/GamutFitTexts.cs:68-69`: "a scale where 3 is just visible" (no constant holds it;
    the comment of `CastWarnChroma` says two, `thresholds.md` says about one), "half of them",
    "99 of 100"; `:38`, `:46`: `knee >= 1` where `GamutFitKneeExact` exists.
  - `AstroPhotoAnalyzer.Core/Parameters.cs:292`, `:295`: "under twice the width", three words
    before the interpolated ratio.
  - `AstroPhotoAnalyzer/AnalyzerCli.cs:250-260`: the defaults of `--help` (1.5, the widths, the
    grid, 7, 3), as the comment at `:199-204` admits; the golden `help.txt` would keep passing.
  - `Analysis/PrintSizeAnalyzer.cs:256`, `:258-259`: the ladder's 240, 100 and 60 dpi.
- Decided or computed twice:
  - `ViewModels/ResultTabs.cs:896-899`: the name of the cast, by the rule
    `Recommendations.cs:242-244` already holds; `:98`, `:106`: a section's verdict and its mark,
    beside `ReportWriter.cs:127` and `:142`.
  - `ViewModels/PreviewWindowViewModel.cs:104`: the amber share, `PctUnderLowSeparation -
    PctUnderPaperBlack`, exists only as this difference in the window.
  - `ViewModels/ResultTabs.cs:544-547`, `:928-930`: the JPEG table's and the region row's formats,
    second copies of `ReportWriter.cs:403-406` and `:515`.
- Should: the literals lifted into `Thresholds` with their source and interpolated; the three
  decisions returned by the core.
- Evidence: confirmed.
- Status: open

### CR-055 The two galleries build every bitmap and red layer on the window's thread

- Where: `AstroPhotoAnalyzer.Wpf/ViewModels/CurvesTab.cs:425-427` (`Show`),
  `ViewModels/GamutFitTab.cs:337-339`, `:362-378`.
- What happens: the core's run is on a pool thread, but its continuation converts four or five
  previews, and up to four overlays (a delegate call per pixel), on the dispatcher.
  `SoftProofTab.cs:28-30` says why the soft proof does the same work on the pool thread.
- Case: a 23 MP image with a profile on a 4K screen, where the previews reach 5.6 to 8 MP each:
  the window stops answering while the curves land, and again for the paper fit.
- Should: the bitmaps, which are frozen, made inside the `Task.Run`.
- Evidence: to measure (a stopwatch around the two `Show` at an area of 3800 x 1900).
- Status: open

### CR-056 The previous analysis, with its whole image, is kept in memory during the next run

- Where: `AstroPhotoAnalyzer.Wpf/ViewModels/MainViewModel.cs:290-302` (`RunCoreAsync`), the
  field `_result`.
- What happens: `ClearTabs()` makes every tab drop its reference, but `_result` is replaced only
  when the new result arrives.
- Case: after `de_printat.png` (three float planes of 22.6 MP, 271 MB), the next run holds that
  raster for nothing until it ends.
- Should: `_result = null` beside `ClearTabs()`.
- Evidence: confirmed (the sizes are arithmetic, not a measured working set).
- Status: open

### CR-057 At 125 % or 150 % scaling the galleries enlarge an image they promise at its own size

- Where: `AstroPhotoAnalyzer.Wpf/Views/ResultViews.xaml.cs:134-139`, `:159-164`
  (`GallerySizeChanged`); `Presentation/ZoomPanViewer.cs:320-325` (`FitScaleFor`).
- What happens: the area is reported in screen pixels, but "its own size" is decided in
  device-independent units: one unit per pixel, which is 1.25 or 1.5 screen pixels.
- Case: Full HD at 150 %, an image 1200 px wide in a gallery area of 1000 DIP: drawn at 1500
  screen pixels. ADR 0084: "a small export blown up would mislead about its detail". The soft
  proof's texts say "with Windows display scaling at 100 %"; the galleries say nothing.
- Should: a cap of one screen pixel per image pixel, or the same caveat.
- Test: the tab tests run at 96 dpi.
- Evidence: confirmed (arithmetic; a run at 150 % with a small image shows it).
- Status: open

### CR-058 The curve window opens taller than the screen at 150 % and on a 768-line laptop

- Where: `AstroPhotoAnalyzer.Wpf/Views/CurveWindow.xaml:31` (`Height="760"`).
- Case: Full HD at 150 % has a work area of 1280 x 680 DIP; the window's bottom edge, with the
  input axis of both charts, is under the taskbar. ADR 0070 fixed the same for "Despre".
- Should: a first height no larger than the work area.
- Evidence: confirmed (arithmetic).
- Status: open

### CR-059 The console and the window do not hold the same argument rules

- Where: `AstroPhotoAnalyzer/Options.cs:140-195` (`Parse`), `:152`, `:178`, `:182`, `:256`;
  `AstroPhotoAnalyzer.Wpf/ViewModels/ParameterFields.cs:232`, `:236-237`, `:451`;
  `AstroPhotoAnalyzer/AnalyzerCli.cs:96-109`; `AstroPhotoAnalyzer.Core/Parameters.cs:276-299`
  (`Validate`).
- What happens:
  - `Validate` holds no range, so each application has its own: the console takes any positive
    distance and any passepartout or frame from 0 up, the window stops all three at 100;
    `--distance 250` runs in the console and is an error in the window. The window's aspect
    takes `/`, the console's does not.
  - A flag given twice is taken silently, the last one winning: `--medium canvas --dmax 1.8
    --medium baryta --dmax 2.4` runs with baryta and says nothing. R-150 states the rule for
    `--lang` only.
  - `--help --lang de` prints the `--lang` error and exits 1; R-004 says "`-h` or `--help` wins
    over every other argument", R-152 asks for the error. Which row holds is the owner's.
- Should: the ranges in `Validate`, once; a repeated flag refused or the rule written in R-003.
- Evidence: confirmed.
- Status: open

### CR-060 `-o` naming a folder and a missing `--wcs` file are told late and badly

- Where: `Input/AtomicFile.cs:56-77`, `AstroPhotoAnalyzer/AnalyzerCli.cs:119-131`, `:143-149`,
  `:162-167`; `Runs/AnalysisRun.cs:119-125`, `:144`.
- Case: `-o D:\reports`, a folder: after the whole analysis, "the report could not be written:
  Access to the path is denied.", with no name (`File.Exists` is false for a folder, so the
  probe that gives the target's own message is skipped). `--wcs stak.fit`, a typo: the 23 MP
  image is read first, then the error; the log has `ERROR Cli run failed` with a stack where a
  missing image is a `WARN ... refused`; a folder is told as "the file does not exist".
- Should: both checked with the other checks before any reading, as refusals.
- Evidence: confirmed (the exact words of the first message on .NET 8 were not run).
- Status: open

### CR-061 A recompute for a larger area that fails throws away the gallery on screen

- Where: `AstroPhotoAnalyzer.Wpf/ViewModels/CurvesTab.cs:384-393` with `Reset` (`:331-342`);
  `ViewModels/GamutFitTab.cs:292-301` with `Reset` (`:245-257`), `:402`.
- What happens: the catch resets the tab and sets the asked size to 0, which `SetGallerySize`
  reads as "nothing to redo".
- Case: the first gallery is on screen; the recompute for a large screen throws (out of memory
  on a large image). The working gallery is replaced by the exception's message and only
  "Analyse" brings one back. In the paper fit a save dialog open at that moment, confirmed, ends
  in a null `Current`.
- Should: R-146, "the one on screen staying until the new one is there".
- Evidence: confirmed (what the catch does; that a recompute fails on real images is not shown).
- Status: open

### CR-062 A relative path on the window's command line stays relative

- Where: `AstroPhotoAnalyzer.Wpf/App.xaml.cs:162-168` (`FileFromArgs`);
  `ViewModels/MainViewModel.cs:192-193` (`DefaultReportPath`).
- Case: `AstroPhotoAnalyzer.Wpf.exe poza.tif` from the image's folder: the report dialog does not
  open beside the image (R-111). Plausibly, a later "Analyse" reads `poza.tif` against a current
  directory a file dialog has moved.
- Should: `Path.GetFullPath` once.
- Evidence: confirmed (the dialog's folder; the re-run in another folder needs a run).
- Status: open

### CR-063 Two widths the star measure should not return

- Where: `Analysis/StarAnalyzer.cs:335-345` (`WidthAtHalf`), `:205-207`;
  `AstroPhotoAnalyzer.Core/Thresholds.cs:480-482` (`StarFwhmMin`).
- What happens: when a side of the profile stays over the half level to the window's edge and
  rises towards it, the "extrapolated" crossing lands inside the window (the reviewer's replica:
  9.73 px for a star of FWHM 8 on a slope). A single hot pixel measures 1.00 px on both axes,
  over the floor of 0.8 whose comment says a hot pixel falls outside.
- Should: a side with no crossing gives no width; the floor, or its comment, corrected.
- Evidence: confirmed (the arithmetic; how many stars of a real frame end at the window's edge
  needs a count on the reference image).
- Status: open

### CR-064 The save message's tooltip never shows

- Where: `AstroPhotoAnalyzer.Wpf/Views/CurvesView.xaml:144-148`, `Views/GamutFitView.xaml:133-137`,
  `MainWindow.xaml:109-112`.
- What happens: two text blocks share one grid cell; the empty error block is on top and takes
  the mouse, so the tooltip that gives the full, untrimmed "saved: ..." line is never shown.
- Evidence: confirmed (the hit test was probed off screen).
- Status: open

### CR-065 The log's rolling takes any `<executable>_<digits>.log` for its own

- Where: `AstroPhotoAnalyzer.Core/Logging/LogSetup.cs:272-285` (`Build`: `ArchiveAboveSize`,
  `MaxArchiveFiles`).
- Case: a copy the user kept in the `log` folder as `AstroPhotoAnalyzer_20261101.log`: the next
  runs append to it, roll on from it, and delete it once five newer files exist (the reviewer's
  run of NLog 6.2.2 with the settings copied by hand, on .NET 10).
- Should: a sentence in the README may be enough; the behaviour is NLog's.
- Evidence: to measure.
- Status: open

### CR-066 Under the two soft variants the chroma mapping jumps where the exact edge takes over

- Where: `Analysis/GamutFit.cs:322` (`Edge`) with `:379-386` (`Knee`).
- What happens: a pixel under `cmax - GamutFitEdgeMargin` is compressed towards the table's
  edge, one just over it towards the exact edge, so the output is not continuous there (the
  reviewer's probe, edges 10 and 9.5: a chroma of 9.39 comes out 9.323, one of 9.41 comes out
  9.116). R-176: "a gradient of growing saturation keeps its steps".
- Evidence: to measure (the code path is common; on the baryta profiles the two edges differ by
  a few hundredths and the jump with them; the canvas was not measured).
- Status: open

### CR-067 Arguments and paths go into the log as typed

- Where: `AstroPhotoAnalyzer.Core/Logging/LogSetup.cs:432` (`StartupLine`);
  `AstroPhotoAnalyzer/AnalyzerCli.cs:112`, `:118`, `:183`; `Runs/AnalysisRun.cs:306`.
- What happens: `logging.md` says every stored text goes through `LogText.Str`, "so a damaged
  file breaks no line"; the command line goes in raw, and an argument with a line break splits a
  log line into one that reads as the log's own.
- Should: the control characters as `?`, as for a FITS card or a profile's name.
- Evidence: confirmed.
- Status: open

### CR-068 The noise tile's range of exactly 6000 steps is decided by float rounding

- Where: `Analysis/NoiseAnalyzer.cs` (`Analyze`, `IsSmooth`): `hi - lo <= maxRange` with
  `maxRange = (float)(NoiseTileMaxRangeLevels / 255.0)`, in single precision.
- What happens: the family of CR-003 on a 16-bit source. The threshold is 6000 of 65535, a whole
  16-bit step, so a tile whose range is exactly 6000 steps is smooth or not by the float's last
  bit. On an 8-bit source there is no tie: the threshold is 23.35 levels.
- Case: not built. One tile of the noise sample changes sides; the medians it feeds move by less
  than their printed precision unless the sample is small.
- Should: the compare of ADR 0095 (`RasterOps.LevelsOver`, on the range in levels).
- Test: none.
- Evidence: to measure. Met on 2026-11-14 while CR-003 was fixed; its three places were the
  review's, and this one was left for its own task.
- Status: open

### CR-069 Two more level figures of an 8-bit source fall by float rounding: an edge folded into its corner, a JPEG percentile

- Where: `Report/Recommendations.cs` (`Colour`, the fold of ADR 0058):
  `edge.MaxAbsDelta < corner.MaxAbsDelta`; `Analysis/JpegAnalyzer.cs` (`ErrorAccumulator.Add`):
  `_hist[(int)Math.Min(Bins - 1, e * Scale)]++`.
- What happens: the family of CR-003, in two places that compare no threshold. On an 8-bit
  source an edge and the corner beside it that depart from the sky by the same whole level read
  a hair apart, so the edge is folded into the corner's line or keeps its own by the float's
  last bit. An error of exactly 2 levels reads 1.9999962 or 2.0000038, so it lands in the
  histogram's bin 199 or 200 and the 99th and 99.9th percentiles can print 1.99 for 2.00.
- Case: not built. The first changes how many lines section 3 prints for one glow; the second is
  a hundredth of a level on a printed percentile.
- Should: the fold compared with the tie of ADR 0095; the bin taken from the error rounded to
  the tie.
- Test: none.
- Evidence: confirmed by reading. Found by the review of the branch that fixed CR-001 to CR-005
  (2026-11-14), which also found the same tie in the second half of `EdgeRegion.Coloured`; that
  one was fixed on the branch, with CR-003.
- Status: open

### CR-071 The curves tab's second gallery is built as the first: its bitmaps on the window's thread, its gamut table again

Added on 2026-11-16, with the two galleries of R-185 (ADR 0101). The same kind as CR-055, with
one more occasion.

- Where: `AstroPhotoAnalyzer.Wpf/ViewModels/CurvesTab.cs` (`Show`, which files a set as the
  gallery with the dust move or the plain one); `Runs/CurveRun.cs` (`GamutAnalyzer.Build`, once
  per run).
- What happens: the stall of CR-055 now also comes at the first untick of the box and at the
  first change to `Ecran`, when the other gallery lands and its four or five previews are
  turned into bitmaps on the dispatcher. With a profile that run builds the gamut table again:
  the cleanup list's "three times" per analysis is four once both galleries exist.
- Should: with CR-055, the bitmaps made inside the `Task.Run`; the table kept for the image.
- Evidence: to measure (as CR-055).
- Status: open

### CR-072 A failed computation of one gallery of the curves tab takes both away, and the box cannot be unticked to go round it

Added on 2026-11-16, with the two galleries of R-185 (ADR 0101). The same kind as CR-061, with a
new way in and a harder way out.

- Where: `AstroPhotoAnalyzer.Wpf/ViewModels/CurvesTab.cs` (the catch of `ComputeCore`, `Reset`,
  `DustBoxEnabled`, `AskMoved`).
- What happens: as in CR-061 the catch resets the tab. Since R-185 a computation also starts
  from the box and from the purpose, and `Reset` drops both galleries. After it nothing is known
  of the move, so the box is disabled, while the owner's choice stays on: the next computation
  asks for the move again.
- Case: the gallery with the dust move fails (out of memory on a large image): the plain
  gallery that was on screen is replaced by the exception's message. "Analyse" computes the
  gallery with the move again, and fails again. The way round is to choose `Ecran` and analyse,
  which computes the plain gallery.
- Should: with CR-061, the gallery on screen kept; a gallery with the move that fails falls
  back to the plain one, with the reason beside the box.
- Evidence: confirmed (the path; that such a computation fails on a real image is not shown).
- Status: open

### CR-074 Two figures of the paper fit are not the same from one run to the next

Added on 2026-11-16, when the validation records were made exact (`docs/validation.md`,
History). Not from the dust move: the paper fit is not touched by that branch.

- Where: `Runs/GamutFitRun.cs`, `Analysis/GamutFit.cs` (the figures of a variant: the chroma
  given up at the 99 % mark, the share left after the fit), measured with the rows in parallel.
- What happens: on the plate-solved FITS through the baryta profile (V-016), two runs of the
  same build one after the other gave `gamutfit.Lina.lostP99` 3.6885 and 3.6887, every other of
  the 1950 figures of the 17 cases being the same; `gamutfit.Exacta.after` read 0.0005 in both
  against the 0.0006 recorded on 2026-11-14.
- Case: run V-016 twice and compare the two `MEASURED` lines.
- Should: the same figures from the same image; where the rows are measured in parallel, a
  result that does not depend on which finishes first. Until then V-016's record cannot be made
  exact, and it keeps the figures of 2026-11-14.
- Test: V-016 passes inside its tolerance of 0.01 either way.
- Evidence: confirmed (two runs; the cause was not looked for).
- Status: open

## For the owner's look

No rule is broken here; each is a behaviour a requirement or a note already carries, which a
reviewer thought the owner may want to see again.

- The dust is compared with paper black exactly, the sky with L* 15 as printed
  (`PrintStretchSimulator.cs:90`, `:107`): a row whose dust is 3.55 to 3.59 on baryta prints
  `3.6` beside paper black `3.6` and carries no mark. R-043's own example has it.
- Banding is judged at the smallest candidate width whenever no row passes the size test, since
  the plateau is read at the reference row's dpi (R-091): the most lenient banding verdict goes
  with the softest image.
- "Going from 16 to 8 bits" on a 32-bit float FITS (`Recommendations.cs:73`,
  `ReportWriter.cs:409`), pinned by `Golden/dark-sky-wcs*.txt`.
- A mono image with a profile gets a full Culori pentru hartie gallery: three variants that are
  the same copy, each with "0.0 %" evidence, and `Salveaza ca...` writes a copy of the input.
- With `Originalul` ticked the soft proof's caption still reads "<profile>: <intent>; paper and
  ink simulated".
- After `Scoate` on an image with a comparison, the comparison's file is kept silently and comes
  back by itself when a profile is chosen again (it follows from ADR 0091).
- The EXIF or TIFF orientation tag is not applied (`IFrameReader.cs:61-63` says so;
  `known-limitations.md` does not).
- A FITS keyword met twice keeps its last value (`FitsReader.cs:261`); cfitsio returns the
  first. A string value with a doubled quote is cut at it (`:252`).
- The English list of what you print on reads `baryta, canvas, acrilic` (`MainWindow.xaml:132`),
  the console's flag values.
- Whether `Wacton.Unicolour` falls back to another table when a profile lacks A2B1 or B2A1 was
  not settled: `ProofProfile.Open` checks B2A0 and B2A1 by name, `GamutAnalyzer.Build` none.

## Cleanup

Not defects. Reported by the reviewers; read by the session only where an entry above cites the
same lines.

Comments and notes that describe code that is gone:
- "seven tabs" where there are eight: `MainWindow.xaml:20-22`, `MainViewModel.cs:18`, `:365`,
  `:379`, `ResultTabs.cs:35`, `ResultViews.xaml.cs:18-20`, and R-110, which omits Culori pentru
  hartie; `ResultTabs.cs:18-26`, `:44-46`, `:837`; `MainWindow.xaml.cs:41-44`;
  `Loggers.cs:24-26` ("the two tabs", against `window.md:127` four and `logging.md:103` five);
  `AstroPhotoAnalyzer.Wpf.csproj:3-6`.
- `AtomicFile.cs:18-19` ("its two files"), `FitsWriter.cs:18-19`, `:52-53` ("the one image the
  tool writes"), `IccProfileInfo.cs:22-23` ("the tool does not convert colours").
- `PrintStretchSimulator.cs:63-68`, `:367-371` (a target rounded to 6 decimals; ADR 0040 rounds
  to 3); `DustAnalyzer.cs:549` ("the low percentile per cell"; the median since ADR 0085);
  `Wcs.cs:68` (`HasSip`, "for the report"); `Thresholds.cs:273-288` (the summary of `DmaxMax`
  stands between `ExactTargetLo` and `ExactTargetHi`); `GamutFitTexts.cs:120` ("each under 72
  characters"); `SoftProof.cs:351-354` (`NoB2A` lists A2B1); `GamutAnalyzer.cs:271-273`.
- `docs/requirements.md:86` (R-044 quotes a sentence the code no longer writes);
  `docs/design/console.md:32-33` (a comma decimal "says to use the point": the window only; both
  applications check ranges, not "syntax only"); `docs/design/measurements.md:491` ("the two
  warnings"), and B3 (the noise's sigmas are NaN only with 0 tiles, not under 10);
  `docs/design/window.md:84-95` (`MetricRowTemplate` as in use); `docs/design/thresholds.md`
  against `Thresholds.cs:559-572` ("about 1" against "two").
- `AnalysisRun.cs:128` against the comment at `:57-61`: `FullPath` is computed after the chain,
  not "at the moment it was read".
- `ParameterFields.cs:271-272` ("one place for every field"; `SizesOf` and `AspectOf` parse by
  themselves).

Dead or unused:
- `Options.cs:93`, `:129`, `:136` (`BoolFlags` inside the loop); `Language.cs:55` (`Tr.Default`).
- `Stats.cs:292-317` (`Min`, `Max`, `StdDev`), `:355-363` (`NextGaussian`, `NextDouble`);
  `ColorMath.cs:99`; `Raster.cs:73`, `:80`; `Wcs.cs:59`, `:69`, `:151`; `Astrometry.cs:70`;
  `LiftTable.cs:72`; `PrintPreview.cs:58`: members only tests call.
- `DustAnalyzer.cs:173`, `:178`, `:264`; `PrintStretchSimulator.cs:160`, `:243`;
  `StarAnalyzer.cs:108`; `PrintSizeAnalyzer.cs:124`; `NoiseAnalyzer.cs:40`, `:50`, and `:61`,
  `:123` (`PctTilesBelowOne`, which R-050 lists as reported and nothing shows).
- `GamutFit.cs:61`, `:69`, `:108`, `:118` (`GreyOutOfReach`, `Rule`); `SoftProof.cs:131`, `:247`
  (`ProofProfile.Path`), `:367`; `CurveAdvisor.cs:376`, `:392` (the parameter `sub`);
  `CurveRun.cs:149`, `GamutFitRun.cs:124` (`progress`, which no caller passes: seven reports and
  four text pairs never run, and `CurveRun.cs:200` reports the one unpaired literal, "gamut").
- `ResultTabs.cs:941`; `CurvesTab.cs:59`, `:475`; `Rows.cs:80`; `WindowSettings.cs:95`;
  `CurvesView.xaml:101`.
- `Dark.xaml:475-494` (`MetricRowTemplate`), `:32` with `Converters.cs:72-89`, `:34` with
  `Converters.cs:115-135`, `:133`; the setters the implicit `TextBlock` style defeats (`:183`,
  `:186-187`, `:240-242`, `:252`, `:255`, `:288-290`, `:323-325`, and `Foreground` on eleven
  check boxes and radio buttons of the views).
- `RasterOps.cs:124` (a 65537th table entry never read).

Duplicated:
- The HDU walk three times (`FitsReader.cs:119-130`, `:154-172`, `FitsWriter.cs:167-175`), so a
  bound added for CR-012 goes in three places; `ReadExact` twice; `Handles` twice
  (`FitsReader.cs:63-68`, `ImageSharpReader.cs:61-66`); `LoadColor` and `LoadMono`
  (`ImageSharpReader.cs:224-270`); `MeasureColor` and `MeasureMono` (`JpegAnalyzer.cs:192-241`).
- `IsSmooth` and `Accept` (`NoiseAnalyzer.cs:135-149`, `QuantizationAnalyzer.cs:236-251`); the
  insufficient-stars rule (`StarAnalyzer.cs:87`, `:217`); the corner names as language pairs
  (`BackgroundAnalyzer.cs:308-311`, `StarAnalyzer.cs:365-368`); the acuity floor
  (`PrintSizeAnalyzer.cs:181`, `:262`, `Recommendations.cs:408`); "reduced pixel to its patch"
  (`PrintPreview.cs:136-141`, `MapViewModel.cs:142-146`, `BackgroundAnalyzer.ClassAt`).
- "The profile the Dmax came from" (`CurveRun.cs:196`, `ProofRun.cs:94`, `GamutFitRun.cs:127`);
  "the profile cannot be read: " five times; the colorants and the D50 white typed twice
  (`IccProfiles.cs:49-51`, `SoftProof.cs:129`, `ColorMath.cs:162-170`); `Clouds` and `Stars`
  (`ReportWriter.cs:188`, `:426`); `ProposedFileName` and `IsTiff`
  (`GamutFitTexts.cs:103-115`); the image's existence checked twice (`AnalyzerCli.cs:119-122`,
  `ImageLoader.cs:66`).
- In the window: the two catch blocks of `MainViewModel.cs:315-330`; `SaveCurve` and
  `SaveGamutFit` (`MainWindow.xaml.cs:205-251`); `GallerySizeChanged` twice
  (`ResultViews.xaml.cs:134-164`); fifty lines of evidence and notes (`CurvesView.xaml:97-149`,
  `GamutFitView.xaml:87-138`); the red-on-problem style four times; the pair of viewers
  (`SoftProofView.xaml:63-75`, `SoftProofWindow.xaml:45-57`); the zoom sentence
  (`GamutFitTab.cs:164-173`, `SoftProofView.xaml:87`); `BadBr` and `WarnBr` against
  `VerdictPalette.cs:35-36`; Esc handled twice (`IccPickerWindow.xaml.cs:41`, `.xaml:103`).

Work done twice:
- The stride-4 subsample of the whole image, four times in one analysis (`TonalAnalyzer.cs:191`,
  `:194`, `PrintStretchSimulator.cs:381`, `BackgroundAnalyzer.cs:214`); `Rounding` and `Stress`
  each subsample again (`QuantizationAnalyzer.cs:263`, `:291`); `LooksLinear` subsamples three
  planes to read one (`RasterOps.cs:209`).
- With a profile, one analysis builds the same `GamutTable` three times (`CurveRun.cs:201`,
  `ProofRun.cs:110`, `GamutFitRun.cs:136`, about 0.9 s each) and reads and parses the profile
  file again (`SoftProof.cs:246-253`, `GamutFitRun.cs:144`); the fit computes every pixel's Lab
  and edge seven times per run (`GamutFitRun.cs:170-182`).
- With `-o` the report's body is rendered twice (`AnalyzerCli.cs:158`, `ReportWriter.cs:63-68`).
- `FitsWriter.cs:284-301`: a value that does not fit is counted, but the whole file is written
  before the save is refused. `GamutFitRun.cs:228-229`: the fit's FITS goes through an identity
  curve, so untouched samples pass the 65 536-step table.
- `DustAnalyzer.cs:371`, `:374`, `:640`, `:647`, `:703`; `PrintPreview.cs:112`, `:118`;
  `GamutFit.cs:350`, `:353`.

Small things:
- Texts that are not language pairs (the same word in both languages today):
  `MainWindow.xaml:173`, `CurvesView.xaml:39`, `TonesView.xaml:63`; `FileFacts.cs:67` ("memorie",
  reached by tests only).
- `ResultTabs.cs:776`, `:800`: the Romanian "de" typed or left out by today's constants;
  `:317-321`: the description says "p50", the value column prints "p50.0".
- `Language.cs:140`, `:177`: `StartsWith("-")` is the culture-sensitive overload.
  `Parameters.cs:204`: `--target-bg 1e300` is quoted as 301 digits.
- `Recommendations.cs:260`: `Take(4)` after the filter, meant as "the four corners";
  `:193`, `:197`, `:202`; `:554` (a filler `Because`).
- `WindowSettings.cs:119`: the temporary file has one fixed name. `DarkTitleBar.cs:67-68`: the
  second call's result is dropped. `PreviewWindowViewModel.cs:102`: `Replace("   ", "; ")`
  depends on the core's mark. `SoftProofTab.cs:94`: the whole proofed raster is kept for its
  size and its intent. `InputFolder` asks `File.Exists` on the window's thread
  (`CurvesTab.cs:272`, `GamutFitTab.cs:195`, `SoftProofTab.cs:235`). `Show` clears a "saved:"
  line when a recompute lands (`CurvesTab.cs:407`, `GamutFitTab.cs:316`).
- `ColorMath.cs:170`: the D50 white is not the row sums of the Bradford matrix beside it (a
  white pixel gets a* 0.0035, b* -0.0251). `TiffWriter.cs:117`: a stream never disposed.
  `RasterOps.cs:52`, `:88`: a subsample carries the full-size WCS.
- Colours typed as literals: `BusyOverlay.xaml:29`, `BeforeAfterViewer.xaml:60`, `:63`,
  `Dark.xaml:368`, `:429`.
