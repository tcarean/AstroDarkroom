# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in
`Directory.Build.props`; the owner bumps it.

## [Unreleased]

### Added

- The Curbe tab (R-140 to R-147; ADR 0032): contrast curves suggested for a finished image,
  `Blanda`, `Medie`, `Puternica` and, when the peaks have room left, `Maxima`, built on five
  anchors (the dust and the sky of the Tonuri tab; the band, the bright clouds and the peaks
  from the upper part of their classes, or from the frame's histogram without a WCS). A gallery
  of previews with `Inapoi` / `Inainte` and the arrow keys; each curve with its evidence (the
  five L\* before and after, the noise gain at the sky, the clipped share, and for print the
  paper checks and the share outside the paper's gamut when the Dmax came from an ICC profile);
  a window with the control points and two charts, to redo the curve in Siril's Curves tool;
  and `Salveaza ca...`, which writes a new FITS with the curve applied, keeping every header
  card of a FITS input. The first image the tool writes; the input is never modified.

### Changed

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

- The level-L* ladder of the Tonuri tab marks the paper in use: an explicit Dmax that is another
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
