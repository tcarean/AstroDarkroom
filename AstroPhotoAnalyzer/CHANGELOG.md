# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in
`Directory.Build.props`; the owner bumps it.

## [Unreleased]

### Added

- The interface speaks English as well as Romanian, in the console and in the window: every
  text, the report, `--help` and the errors included, is chosen by `--lang en|ro` (also
  `--lang=ro`), **English when it is not given**; an unknown value is an error in the console
  (exit 1) and a message in the window, which then opens in English. The figures and the exit
  codes are the same in both languages; the Romanian texts are unchanged, with `--lang ro`
  (R-150 to R-154; ADR 0048).

### Fixed

- The paper-black line of the Tonuri histogram stood half a bin (about 5 px) left of its value:
  the bins are centred on whole L\* values, and the line now falls at the centre of its bin,
  as does the new threshold line (R-113; the code review of ADR 0046's task).
- "NaN" and "Infinity" typed in a number parameter no longer reach the analysis: the core's
  `Parameters.Validate` refuses them in both applications, one Romanian message per parameter
  (`Dmax trebuie sa fie un numar; a primit NaN, care nu e un numar`), the console with exit 1
  before reading, the window in its error card; the aspect ratio must also be positive, since
  `1:Infinity` arrived as 0. Until now `--dmax NaN` printed a report of `n/a` figures and exited
  2, and the window ran with NaN in its parameters (R-024; ADR 0045; the backfill's area 4 and
  area 5 questions).
- "Salveaza raportul..." no longer closes the window when the file cannot be written (held open
  by another program, a folder that refuses writing): the reason shows in red under the top
  bar's buttons, and a save that works says `raport salvat: <path>` there (R-110, R-111; the
  backfill's area 5 review, question 1).
- The report file, the console's `-o` and the window's `Raport.txt` alike, is written whole or
  not at all: into a temporary file beside the target, moved over it at the end, so that a
  write that fails midway (a full disk) leaves the old file or none instead of a truncated
  report (R-005, R-111; this branch's code review).
- `--aspect` with a side that reads as NaN or infinity (`1:Infinity`) is refused by the
  console's and the window's own aspect message, which quotes the text typed, instead of the
  core's message on the ratio it would give (`a primit 0`) (R-024; ADR 0045; this branch's code
  review).
- The gamut figure of the Curbe tab no longer undercounts near the paper's gamut edge: a pixel
  whose interpolated displacement lies within 1.0 of the threshold takes the exact round trip of
  its 8-bit colour, once per colour, the rows measured in parallel (R-144; ADR 0042). The grid
  alone read the dust class of `de_printat.png` on baryta at 0.0 % where the exact figure is
  2.4 %, and the plate-solved FITS at 1.02 % against 1.35 %; the hybrid is within 0.2 points of
  the exact figure on every class (the code review of area 2, item 9).
- `Maxima` no longer rises over white: its slope factor is capped at the largest value whose
  spline stays monotone and within 0..1, and every suggestion is judged on the unclamped spline,
  as R-140 and R-142 always said (ADR 0039). On `de_printat.png`, Rosetta and the Veil the
  uncapped Maxima mapped 21 to 41 % of the input range to white; capped, its peaks land at 0.892,
  0.899 and 0.828 instead of 0.92 (the code review of area 2, item 2).
- `Pastreaza master-ul de 16 biti` is advised when the 8-bit path of the shadow-lift stress test
  misses more than 20 levels over the 16-bit one, not over 20 in all: an 8-bit file saved as 16
  bits, which misses the same levels either way, now gets the upscale warning alone (R-082,
  R-096; ADR 0043; item 4).
- A crop is reported only when the target aspect removes at least one pixel (4961 x 3508 at
  297:210 no longer gets an Attention that cuts 0 px), and the two sides are named when they
  differ (`0 px la stanga si 1 px la dreapta din latime`), in the report and in the Preview tile
  (R-032; ADR 0043; item 5).
- The target background (`--target-bg`, the window's "Fundal-tinta") is rounded to the three
  decimals the report prints and refused, with one message from the core in both applications,
  when it is NaN, infinite or not strictly inside (0, 1) once rounded: no more run that ends in an
  error without a report at 1 or at a value that rounds to 0, and a target typed with more
  decimals keeps its `(dat)` row and the Preview tile (R-023; ADR 0040; items 6 and 8).
- The exact targets of Anexa B (the dust's, its two Dmax neighbours, the visibility target,
  each cloud's) are read on the regular ladder 0.05..0.60 alone: a `--target-bg` above 0.60 no
  longer extends it, so a crossing the ladder does not reach prints `> 0.60` instead of a value
  interpolated across the gap (R-043; ADR 0040; item 7).
- `Sub nivelul 32` and `sub 64` in Anexa A and the Tonuri tab count the samples whose 8-bit
  level, the one an export writes, is under 32 or 64, on every format: on an 8-bit TIFF, PNG or
  JPEG the level 32 itself no longer counts (it added 2 points on the reference image's 8-bit
  version), and on 16-bit and float sources the shares move by tenths (R-040; ADR 0043; item 3).
- The catalogued clouds are found by angular distance alone: every patch centre is tested
  against every cloud, after a pre-filter on the frame's centre, instead of inside a pixel box
  of the cloud's radius at the central plate scale, which far from the tangent point lost the
  outer patches of a disc, or the whole cloud (R-044; ADR 0044; item 10).
- A flat two-pixel star peak is measured once, at its first pixel in scan order, not twice
  (R-070; ADR 0041; item 14); the star figures move a little where such peaks occur.
- The Dmax figure of the background section and of Anexa B is named for what it measures: the
  Dmax from which the as-is dust reaches paper black (`praful ajunge pe negrul hartiei de la Dmax
  2.28`), no longer `trece asa cum e`, which was false whenever the as-is sky was under L\* 15;
  there the sentence now adds that as it is no paper makes the image print-safe and that the fix
  is lifting the background at the stretch (R-043, R-092; ADR 0043; the code review of area 2,
  items 1 and 13).

### Added

- The Tonuri histogram also marks the visibility threshold L\* 15, an amber dashed line beside
  the red paper-black line, the colours the preview window gives the same two classes; the
  heading and the note under the chart name both lines and hold on canvas too, where paper
  black is right of the threshold (R-113; ADR 0046).
- No curve is suggested when the peaks are already at or over the target of 0.92 (R-142; ADR
  0033): the Curbe tab shows the reason in red, instead of four curves that change nothing.
- The Curbe tab (R-140 to R-147; ADR 0032): contrast curves suggested for a finished image,
  `Blanda`, `Medie`, `Puternica` and, when the peaks have room left, `Maxima`, built on five
  anchors (the dust and the sky of the Tonuri tab; the band and the bright clouds from the upper
  part of their classes and the peaks from the median of theirs, on either path of the classes,
  ADR 0033). A gallery
  of previews with `Inapoi` / `Inainte` and the arrow keys; each curve with its evidence (the
  five L\* before and after, the noise gain at the sky, the clipped share, and for print the
  paper checks and the share outside the paper's gamut when the Dmax came from an ICC profile);
  a window with the control points and two charts, to redo the curve in Siril's Curves tool;
  and `Salveaza ca...`, which writes a new FITS with the curve applied, keeping every header
  card of a FITS input. The first image the tool writes; the input is never modified.

### Internal

- The rule "Languages" in `CLAUDE.md` says what holds after the documentation backfill: every
  comment in the code's C# files is English (the kit's ADR 0035); the tests' Romanian comments
  stay until a task of their own, the csproj, `Directory.Build.props` and XAML comments stay
  Romanian; ADR 0005's Status and a dated note say the same.
- The map legend takes each class's share from the core (`DustMap.FractionOf`, the figure of
  the report's class fractions) instead of counting the patches in the window: the same figure,
  and `MapViewModel`'s "it computes nothing" is true (R-130; the backfill's area 5 review,
  question 2).
- `StarAnalyzer` reads its window from `Thresholds.StarWindow` instead of a literal 12 (item 12).
- The medians and percentiles of unsorted vectors are found by selection on one copy
  (`Stats.Percentile`, `Stats.Percentiles`, `Stats.Median`), not by sorting the whole vector: the
  same values to the bit (R-017; the 1689 validation figures identical), about 0.5 s less per
  run on the reference images (the code review of area 2, item 15).
- The print simulation's ladder stops once every series it reads (the dust at the three paper
  blacks, the sky at L\* 15, every row cloud) has crossed its level and every table row is
  built, instead of lifting all 56 steps: the same figures to the bit (R-043; the 1689
  validation figures identical), about 1 s less per run on the reference images (item 15).
- The build writes the XML documentation file (ADR 0038), so the gate now fails a malformed
  doc comment, a `<param>` or `<paramref>` naming no parameter, a parameter left untagged beside
  tagged ones, a `cref` that does not resolve or is ambiguous, and a `///` on something that is
  not a member; CS1591 stays muted, and no publish ships the XML files.
- The code's comments are English and every type and method is documented, done one area at a
  time (the kit's ADR 0035; the plan in
  `docs/plans/2026-10-22-document-every-type-and-method-and-translate-the-comments.md`): the
  core, the console and the window, all 71 files, the 126 of their 159 types and the 398 of
  their 419 methods that lacked a full comment, and the comments that said what the code does
  not do, found in the translation, in probes on the build and in the reviews of areas 1 to 4.
  No code changed.
- NuGet's audit advisories (NU1902, NU1903) stay warnings under the gate's `-warnaserror`, and
  each one is judged in `docs/design/dependencies.md` (ADR 0036): the five published against
  ImageSharp 3.1.11 on 2026-10-07 are patched in 4.1.2 only, the line ADR 0009 rejects, and none
  applies to a tool that reads the owner's own files.

### Changed

- The print-background simulation is about six times faster, with every figure unchanged (ADR
  0035): each lift of the ladder is linearised through one 16-bit table instead of a lifted
  raster and a power function per sample, and a grid row no longer sorts the L\* plane for a
  median it does not show. On the plate-solved FITS the simulation takes 2.6 s instead of 17 s
  and the console run 15 s instead of 27 s; the window leaves the "praf" stage as fast as the
  others.
- Every frame gets the five classes of the Tonuri tab (R-044, R-113, R-130; ADR 0033). With a
  WCS and the Milky Way band in the frame they are the band's, as before; otherwise they are
  relative to a sky modelled from the whole frame, with the thresholds taken from the image
  (the sky's own patch noise for the nebula, the Otsu split for the bright nebula), named
  `praf, cer, nebuloasa, nebuloasa stralucitoare, varfuri`, and the map shows all five with
  the image's thresholds in its legend. The verdict measures the same way on both paths: the
  sky is the median of the sky class, the dust the darkest extended area, the dust class's
  25 % percentile when the class is at least 5 % of the frame or else the darkest 1 % of the
  sky; the report says which. The whole-frame rule (the darkest 1 % of all patches, the median
  of all patches) is gone. On `de_printat.png` the sky moves from L\* 9.4 to 8.9 and the
  recommended row from 0.17 to 0.16.
- The simulation grid starts at 0.05 instead of 0.10 (R-023, R-043; ADR 0034): on baryta the
  dust reaches paper black under 0.10, and those rows were missing from the table.
- The curves' anchors come from the classes on either path (R-141, R-146, R-147; ADR 0033), the
  points carry the classes' names, and the sentence under the gallery names the path; the
  histogram percentiles of a frame without a WCS are gone.

- Baryta's Dmax is 2.4, not 2.0 (R-016, R-020; ADR 0028): measured, 2.41 relative to paper white,
  on the ICC profile of the lab Chromatone for Hahnemühle Photo Rag Baryta on a Canon iPF8300.
  Paper black on baryta is L\* 3.6 instead of 9.0, so a sky at 0.08 to 0.10 is no longer under
  it; canvas (1.5) and acrylic (2.2) keep their values, each with its source recorded.
- The background verdict asks a second question (R-043, R-092; ADR 0029): a sky under L\* 15, the
  visibility threshold, is an Attention even when the dust is above paper black, and the
  recommended row is the lowest that has the dust on paper black and the sky at L\* 15 or above.
  On baryta at Dmax 2.4 the reference image is told to re-stretch to 0.15 instead of "print-safe
  as it is; you can go down to 0.10". Anexa B gives the level at which the sky reaches L\* 15.
- The window's Dmax field shows the Dmax in use: choosing a medium writes its Dmax into the
  field, and a Dmax equal to the medium's is no longer reported as `(explicit)`.
- The print-background verdict (R-041, R-043, R-044, R-092; ADR 0025). The image is no longer
  judged by the share of pixels under paper black: the tool measures the *dust*, the darkest
  extended structure, on 32 px patches, and recommends the lowest background of the grid
  0.10..0.20, the as-is level included, at which the dust lands on paper black, downwards as
  readily as upwards. On a plate-solved FITS (`CTYPE RA---TAN`, `CD`, `PC` + `CDELT` or
  `CDELT` + `CROTA2`; SIP ignored) the patches are classed relative to the galactic band
  (ADR 0026): band (|b| < 10°), sky (modelled from outside the band), dust (5 L\* under the
  band's local level or under the sky model), bright clouds, peaks; the dust's L\* is the 25 %
  percentile of the dust patches, the sky's the median of the sky patches, and the embedded
  Lynds catalogue of dark nebulae names the clouds in the frame, every cloud with 9 or more
  patches getting a row with its L\* and the target that puts it on black. Without a WCS, or on
  a frame without the band or without sky outside it, the dust is the darkest 1 % of the frame's
  patches and the sky the median, and the report says why. Anexa B lists the grid, the
  exact target (also at Dmax -0.1 and +0.1), the Dmax from which the image passes as is, and
  the darkest 15 clouds by name; the `Because` of the background recommendation names them.
- `--target-bg` is optional and only adds a row (marked `(dat)`) to the simulation; the Preview
  tab's print tile follows the recommended row when none is given.
- The Tonuri tab shows the simulation as rows with a `Preview` button each, the definition of
  the dust and the sky in use, a red sentence when the file has no WCS, and the cloud table.

### Added

- The Dmax of the paper can be read from its ICC printer profile (R-028, R-132; ADR 0030):
  `--icc <file>` in the console, and in the window the button `Din profil ICC...` beside the Dmax
  field, which lists the printer profiles installed in Windows with the Dmax of each and takes
  a file from anywhere. The Dmax is the device black through the relative colorimetric table,
  relative to paper white; Anexa F names the profile. Wacton.Unicolour (MIT) evaluates the
  tables, with its notice in `THIRD-PARTY-NOTICES.md` and in "Despre".
- `--wcs <file.fit>` (the window: `WCS din FITS`): a TIFF or PNG borrows the WCS of a
  plate-solved FITS of the same size, since the export carries no header (R-027).
- The map window (`Harta praf / cer`): the image with the five classes of patches coloured and
  the catalogued clouds circled and named, each layer, the clouds and each cloud behind a check
  box, with `Toti` / `Niciunul` (R-130).
- The preview window of a simulation row: the image lifted to that row's target with the pixels
  under paper black and under L\* 15 coloured, each behind a check box (R-131).
- The Lynds catalogue (`VII/7A`, 1787 clouds) as an embedded resource, with its notice in
  `THIRD-PARTY-NOTICES.md` and in "Despre" (R-019, R-122).
- A second validation image, the plate-solved FITS, as case V-004.

### Fixed

- A deep-sky frame inside the galactic band (the Veil at a 10 degree field) is no longer classed
  on the band path, with a band map over the nebula and the curves anchored on a "banda" that
  was plain sky: the band classes now also need the frame to span at least 20 degrees of
  galactic latitude, the band's width, and a narrower frame takes the sky-relative classes with
  the reason named in the report (R-044; ADR 0037).
- The level-L\* ladder of the Tonuri tab marks the paper in use: an explicit Dmax that is another
  medium's (canvas with Dmax 2.2) no longer marks that other medium as the chosen one (R-042).
- The report's header line and Anexa F print a Dmax with its second decimal: `--dmax 2.35` read
  `Dmax 2.4`, the Dmax of another paper (R-101).
- A target at the ends of the simulation's ladder is written as a bound: `<= 0.05` when even the
  lowest step already puts the dust or a cloud on paper black (it read `0.05`, as if measured),
  `> 0.60` when no step does, in the cloud table, the exact-target line and the map (R-043).
- A NaN sample in a float FITS is clipped to 0 and counted like an out-of-range value (R-013);
  it used to reach the raster and, with the simulation's lookup table, would have stopped the
  run.

### Internal

- `Directory.Build.props` declares the `win-x64` runtime the release publishes for, on every
  project but the two test projects, and turns off the single-file analyzer, so a release's
  publish no longer rewrites the lock files and stops the release on them (ADR 0031).
- The golden console texts are re-approved after the assistant has read their whole diff and
  tied every changed line to the change the task meant to make; a line the task did not mean to
  change, even with a known cause, is a finding that stops the task (the kit's ADR 0033, which
  supersedes ADR 0027).
- The project follows the ClaudeCodeStarterKit process (ADR 0001 to 0006): `scripts/verify.ps1`
  is the gate (the kit's scripts, skills, agent and rules synced, `kit.json`, the test data
  manifest, `dotnet format`, warnings as errors, the tests), package versions live once in
  `Directory.Packages.props` with lock files, and the reference image is a git-ignored copy in
  `AstroPhotoAnalyzer.Tests/testdata/large/`. `docs/specification.md` was split into
  `docs/requirements.md` (65 rows with IDs), `docs/decisions/` (24 ADRs, 18 of them retroactive)
  and `docs/design/` (12 notes, moved unchanged); the acceptance record became
  `docs/validation.md`, and `CLAUDE.md` holds only the project's rules. `RepoInvariantTests`
  ties `implemented` rows to tests named `R###_...` (82 tests renamed for 28 rows), and
  `ValidationTests` checks three cases on the reference image outside the gate. No behaviour
  changed; one `dotnet format` pass over 10 files.

## [1.0.0] - 2026-09-21

The state of the tool when the changelog was started, built on 2026-09-20 and 2026-09-21 in
seven phases (`docs/plans/2026-09-20-implementation-plan.md`):

- The core: readers (hand-written FITS; TIFF, PNG and JPEG through ImageSharp 3.1.11), the
  raster, colour maths, eleven analyzers (file facts, bit depth, crop, tonal distribution, the
  print-background simulation, noise, background colour and edge regions, stars, print size,
  quantization, JPEG), the recommendations in decision order with their evidence, the report.
- The console `AstroPhotoAnalyzer.exe`: options, exit codes 0/1/2/3, `-o`, golden tests on
  synthetic scenes.
- The window `AstroPhotoAnalyzer.Wpf.exe`: parameters with help tooltips, six tabs (Raport,
  Tonuri, Export, Rezolutie, Culoare, Preview) with a description under every number,
  `Raport.txt` byte-identical to the console's output, "Despre".
- The acceptance run on `de_printat.png` against the hand-made numbers; the "Fundal" naming
  pass (the tab became Culoare).

No earlier version was released; the eleven commits are in `git log`.
