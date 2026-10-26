# Validation

The project's memory of "how well it works, measured on what". Every case lives in
`validation.json` (read by `ValidationTests`, trait `Category=Validation`, not in the gate, ADR
0004) and is explained here. A number changes only after a re-measurement, with a dated line
under History saying old value, new value and why. The masters and raws are the ones under
`E:\Photografie\Astro\Flats and Darks`, listed with their SHA-256 in
`FlatCheck.Tests/testdata/manifest.json`; `FLATCHECK_TESTDATA_LARGE` names that folder.

Four kinds of case, one per run the figures come from:

| Kind | What runs | What is recorded | Tolerance |
| --- | --- | --- | --- |
| `structure` | `StructureRun` on the master, no profiles | the five deviation tests, the vignetting depth, the local spot, the verdict | 0.05 points |
| `profile` | `StructureRun` with the first raw of `flats/` as `--raw` | the master's depth, the nearest profile and the difference in stops, the depth verdict, the run's verdict | 0.02 stops |
| `smooth` | `SmoothRun` at 300 px | what was removed (p95 - p5 on tiles, worst tile, deepest spot with sign), input and output texture, verdict and depth before and after | 0.05 points |
| `against` | `SyntheticRun` from the lens's Adobe profile at 20 mm f/2.8, crop 1.523, then `AgainstRun` | the two depths, the difference in stops and as corner error, the ratio's residual, shape / depth / overall verdict | 0.02 stops |

The `nearest` lens name and the verdict words are compared exactly; positions (`spotAt`) are
information, not compared.

## Cases

Recorded on 2026-09-25 from the build of that day (2.2.0 plus the process migration, no formula
changed), 40 cases in 2 min 30 s. Where a session note of 2026-09-16, -18 or -23 quotes the same
figure from an earlier build or from a numpy bench, it is given in the last column; agreement to
the second decimal is the check that the port and the notes describe the same thing.

### Structure on every master (V-001 to V-015)

| ID | Master | Verdict | corners / azimuthal / residual / tilt / worst tile (%) | depth (stops) | earlier figure |
| --- | --- | --- | --- | --- | --- |
| V-001 | Nikkor 35 DX ISO400 sky | BUN | 4.00 / 3.75 / 4.05 / 1.75 / 3.24 | 0.63 | corner spread 4.00 % (2026-09, `thresholds.md`) |
| V-002 | Nikkor 35 DX ISO800 sky | ACCEPTABIL | 10.96 / 5.27 / 7.17 / 7.97 / 5.79 | 0.62 | |
| V-003 | Samyang ISO1600 f/2 sky | ACCEPTABIL | 10.53 / 6.39 / 8.38 / 11.14 / 5.44 | 0.60 | |
| V-004 | Samyang ISO1600 sursa_neuniforma f/2 lcd | BUN | 3.49 / 2.67 / 2.98 / 4.04 / 2.36 | 0.63 | |
| V-005 | Samyang ISO1600 sursa_neuniforma f/2.8 | BUN | 6.09 / 7.11 / 6.61 / 7.82 / 4.15 | 0.18 | |
| V-006 | Samyang ISO400 f/2 sky | BUN | 7.59 / 6.83 / 6.33 / 8.03 / 4.17 | 0.61 | |
| V-007 | Samyang ISO400 f/2.8 sky | ACCEPTABIL | 12.62 / 7.05 / 9.99 / 12.36 / 6.51 | 0.15 | tilt 12.36 %, 12.62 / 7.05 / 6.05 (the 1.2.0 note in `thresholds.md`; the worst tile did not exist then) |
| V-008 | Tokina monitor, diffuser close | BUN | 6.67 / 8.73 / 7.10 / 6.71 / 6.65 | 0.85 | |
| V-009 | Tokina monitor, diffuser on the monitor | ACCEPTABIL | 16.41 / 10.74 / 12.27 / 11.77 / 12.60 | 0.78 | local spot +6.19 % (`smooth-validation.md`) |
| V-010 | Tokina sky, no diffuser | ACCEPTABIL | 10.28 / 7.25 / 10.06 / 0.49 / 8.81 | 0.27 | residual 10.06 after the 4θ correction (2026-09-23 note); 0.27 stops |
| V-011 | Tokina tablet, 15 frosted covers | BUN | 2.17 / 2.00 / 2.28 / 2.55 / 1.54 | 1.24 | 1.24 stops (2026-09-18 note) |
| V-012 | Tokina tablet, 7 frosted covers | PERFECT | 1.58 / 1.60 / 1.42 / 1.64 / 0.95 | 1.26 | 1.26 stops, PERFECT |
| V-013 | Tokina tablet, 3 sheets of paper, rotated in place | BUN | 1.18 / 2.03 / 2.86 / 0.58 / 2.65 | 0.76 | corners 1.2 %, tilt 0.6 %, 0.76 stops (2026-09-23 note); the note also gives residual 2.93 / worst tile 2.94 from the bench, against 2.86 / 2.65 here; not investigated on 2026-09-25 |
| V-014 | Tokina tablet, 3 sheets of paper | BUN | 4.69 / 3.68 / 3.50 / 3.88 / 2.77 | 0.81 | 3.8 % residual, 0.81 stops |
| V-015 | Tokina tablet, 4 sheets of paper | BUN | 2.46 / 5.79 / 4.98 / 3.10 / 3.93 | 0.80 | 5.1 % residual, 0.80 stops |

### The sixth test with the raw beside the master (V-016 to V-030)

| ID | Master | Nearest profile | diff (stops) | depth | run | earlier figure |
| --- | --- | --- | --- | --- | --- | --- |
| V-016 | Nikkor 35 DX ISO400 | Adobe, Nikkor 35/1.8G DX (D90) | +0.110 | BUN | BUN | +0.11 BUN |
| V-017 | Nikkor 35 DX ISO800 | same | +0.103 | BUN | ACCEPTABIL (shape) | +0.10 BUN |
| V-018 | Samyang ISO1600 f/2 sky | Adobe, Nikkor 135 f/2 DC (by range: manual lens) | +0.104 | BUN | ACCEPTABIL (shape) | +0.10 |
| V-019 | Samyang sursa_neuniforma f/2 lcd | same | +0.131 | BUN | BUN | +0.11 / +0.10 (which of the two f/2 masters the note meant is not recorded) |
| V-020 | Samyang sursa_neuniforma f/2.8 | lensfun, Samyang 135 f/2 | +0.034 | BUN | BUN | |
| V-021 | Samyang ISO400 f/2 sky | Adobe, Nikkor 135 f/2 DC | +0.109 | BUN | BUN | |
| V-022 | Samyang ISO400 f/2.8 sky | lensfun, Samyang 135 f/2 | +0.006 | PERFECT | ACCEPTABIL (shape) | +0.01 PERFECT |
| V-023 | Tokina monitor, diffuser close | lensfun, AT-X 11-20 Aspherical (7D2) | +0.004 | PERFECT | BUN | 0.00 PERFECT |
| V-024 | Tokina monitor, on the monitor | same | -0.069 | BUN | ACCEPTABIL (shape) | -0.07 BUN |
| V-025 | Tokina sky, no diffuser | Adobe, AT-X 11-20 | -0.395 | REFA | REFA | -0.39 REFA |
| V-026 | Tokina tablet, 15 covers | lensfun, AF 11-20 (D7100) | +0.332 | REFA | REFA | +0.33 REFA, exit 3 |
| V-027 | Tokina tablet, 7 covers | same | +0.345 | REFA | REFA | +0.34 REFA |
| V-028 | Tokina paper, rotated in place | lensfun, Aspherical (7D2) | -0.085 | BUN | BUN | |
| V-029 | Tokina paper, 3 sheets | same | -0.036 | BUN | BUN | |
| V-030 | Tokina paper, 4 sheets | same | -0.046 | BUN | BUN | |

The library on 2026-09-25 holds 15 sessions with a raw beside every master, so every session is
here; the ten quoted in `modes-structure.md` are a subset.

### What `--smooth` removes at 300 px (V-031 to V-035)

| ID | Master | removed ptp / worst tile / spot (%) at | texture in -> out (%) | verdict before -> after | earlier figure (`smooth-validation.md`, 2026-09-16) |
| --- | --- | --- | --- | --- | --- |
| V-031 | Nikkor ISO400 | 0.375 / 0.404 / -0.321 at (117, 225) | 0.051 -> 0.014 | BUN -> BUN | 0.37 / 0.40 / -0.32 at (117, 225) |
| V-032 | Nikkor ISO800 | 0.393 / 0.449 / -0.320 | 0.060 -> 0.014 | ACCEPTABIL -> ACCEPTABIL | 0.39 / 0.45 / -0.32 |
| V-033 | Tokina diffuser close | 0.378 / 0.469 / +0.342 | 0.044 -> 0.020 | BUN -> BUN | 0.38 / 0.47 / +0.34 |
| V-034 | Tokina on the monitor | 3.683 / 4.208 / +6.038 at (1071, 639) | 0.687 -> 0.023 | ACCEPTABIL -> ACCEPTABIL | 3.68 / 4.21 / +6.04 at (1071, 639); texture 0.69 -> 0.02 |
| V-035 | Samyang sursa_neuniforma lcd | 0.121 / 0.714 / +0.400 | 0.036 -> 0.008 | BUN -> BUN | 0.12 / 0.71 / +0.40 |

Depth before and after agrees within 0.006 stops on all five, which is the "verdicts and
vignetting depth unchanged" claim of the smooth note, re-measured.

### The master against its lens's Adobe synthetic (V-036 to V-040)

Synthetic from `Tokina AT-X 11-20mm F2.8 PRO DX` at 20 mm f/2.8, crop 1.523: 0.664 stops at the
corner.

| ID | Master | diff (stops) | corner error (%) | depth | shape | run | earlier figure (2026-09-18, `modes-synthetic.md`) |
| --- | --- | --- | --- | --- | --- | --- | --- |
| V-036 | Tokina diffuser close | +0.186 | 13.8 | ACCEPTABIL | BUN | ACCEPTABIL | +0.19, 13.8 %, ACCEPTABIL |
| V-037 | Tokina sky | -0.395 | 31.5 | REFA | ACCEPTABIL | REFA | -0.39, 31.5 %, REFA |
| V-038 | Tokina on the monitor | +0.115 | 8.3 | BUN | ACCEPTABIL | ACCEPTABIL | +0.12, 8.3 %, BUN |
| V-039 | Tokina tablet, 7 covers | +0.592 | 50.8 | REFA | PERFECT (ratio residual 1.5 %) | REFA | +0.57 (before the crop correction of 2026-09-18) |
| V-040 | Tokina paper, rotated in place | +0.099 | 7.1 | BUN | BUN | BUN | +0.14 vs Adobe in the 2026-09-23 note (its figure was on the bench, "not bad but not perfect") |

### The Split tab on two real sessions (measured by hand, 2026-09-28)

The part masters exist only while the tab runs (the temp sessions are deleted after the
analysis, ADR 0054), so there is no `validation.json` case: these are the tables the tab
saved as `Rezultat split.txt`, driven headless through `MainViewModel` on the build of that
day (banner 2.2.0, before the bump). Five tests as corners / azimuthal / residual / tilt /
worst tile, in %; the reference profile is the whole's nearest, the same for every column.

`Tokina tablet, 3 sheets of paper, rotated in place` (V-013), 4 x 10 frames at 90°, 185 s
end to end (copy, four Siril runs, analysis); reference lensfun AT-X 11-20 Aspherical (7D2),
20 mm f/2.8, 0.85 stops:

| column | five tests (%) | verdict | depth (stops) | vs profile |
| --- | --- | --- | --- | --- |
| part 1 (0°) | 7.58 / 6.95 / 7.79 / 5.48 / 7.16 | BUN | 0.73 | -0.12 = 8.4 % BUN |
| part 2 (90°) | 4.25 / 7.57 / 8.27 / 2.85 / 6.95 | BUN | 0.79 | -0.06 = 3.9 % BUN |
| part 3 (180°) | 8.02 / 6.75 / 7.84 / 5.79 / 6.51 | BUN | 0.73 | -0.11 = 8.2 % BUN |
| part 4 (270°) | 4.19 / 7.97 / 8.23 / 3.72 / 7.57 | BUN | 0.80 | -0.05 = 3.7 % BUN |
| whole (40) | 1.18 / 2.03 / 2.86 / 0.58 / 2.65 | BUN | 0.76 | -0.08 = 6.1 % BUN |

The shared colour scale of the mosaic came out at -6.4 / +6.5 %. In the picture parts 1 and 3
share one blotch field and parts 2 and 4 the same field turned by 90°, and the whole is the
average of both: the world-fixed blotches of `flat-sources.md` §4, seen four times and once
cancelled. The whole's five tests agree with V-013 to the second decimal.

`Tokina sky, no diffuser` (V-010), 15 + 15 frames at 180°, 108 s end to end; reference Adobe
AT-X 11-20, 20 mm f/2.8, 0.66 stops:

| column | five tests (%) | verdict | depth (stops) | vs profile |
| --- | --- | --- | --- | --- |
| part 1 (0°) | 22.55 / 13.63 / 16.05 / 17.49 / 12.01 | REFA | 0.27 | -0.39 = 31.0 % REFA |
| part 2 (180°) | 22.97 / 13.81 / 17.15 / 18.44 / 12.44 | REFA | 0.26 | -0.40 = 31.9 % REFA |
| whole (30) | 10.28 / 7.25 / 10.06 / 0.49 / 8.81 | REFA (depth) | 0.27 | -0.39 = 31.5 % REFA |

The tilt of 17-18 % on each orientation and 0.49 % on the whole is the 180° cancellation of
`flat-sources.md` §1 seen through the tool; the corner spread halves but stays, the even
saddle; and the -0.39 stops against the profile are identical on every column, the radial
excess no rotation touches. The whole's row agrees with V-010 and V-025. Shared colour scale
-11.4 / +12.7 %.

### The lens of six raws of other makes and two Nikon raws (V-041 to V-048, measured 2026-10-26)

Not a kind of `validation.json`: the figures are libraw's lens metadata, not a run's, and
`RawSampleTests` (trait `Category=Validation`) holds them as its theory rows and prints what it
read. The files are CC0 samples of raw.pixls.us, downloaded 2026-10-26 with the owner's approval,
their SHA-256 identical to the site's, kept under `Samples/raw.pixls.us/` in the large folder with
the site's file names (`:` written `-`), listed in the manifest. They are what ADR 0068 was
designed on; `LensMountTests` fabricates frames with the same values for the gate.

| ID | File (raw.pixls.us sample) | Camera | `LensId` | `LensMount` | `LensName` | range (mm, mm, f) | focal / f-number |
| --- | --- | --- | --- | --- | --- | --- | --- |
| V-041 | `Canon - EOS 7D Mark II - RAW (3-2).cr2` (1064) | Canon EOS 7D Mark II | 198 | Canon EF | `EF50mm f/1.4 USM` | 50 / 50 / 1.414 (from `MaxAperture`) | 50 / 4 |
| V-042 | `Canon - EOS 6D - RAW.CR2` (799) | Canon EOS 6D | 155 | Canon EF | `EF85mm f/1.8 USM` | 85 / 85 / 1.834 | 85 / 5.6 |
| V-043 | `Sony - ILCE-7M3 - 14bit compressed (3-2).ARW` (2414) | Sony ILCE-7M3 | 32831 (in no Adobe file) | Sony E | `FE 16-35mm F2.8 GM` | 16 / 35 / 2.8 | 32 / 4 |
| V-044 | `Pentax - K-70 - 14bit (3-2).PEF` (1141) | Pentax K-70 | 2263 (Adobe's `8 215`) | Pentax K | none | none | 18 / 5.6 |
| V-045 | `Canon - EOS R6 - 3-2.CR3` (4659) | Canon EOS R6 | 266 (Canon's RF type; the Adobe RF files carry 61182) | Canon RF | `RF70-200mm F2.8 L IS USM` | 70 / 200 / none (`MaxAperture` 2.828 is the aperture at 200 mm, not taken) | 200 / 25 |
| V-046 | `Canon - EOS R5 Mark II - CRAW (3-2).CR3` (7884) | Canon EOS R5m2 (libraw 0.21.1 is older than the body) | 303 | Canon RF (the lens's mount; the camera's read Canon EF) | `RF135mm F1.8 L IS USM` | 135 / 135 / 1.834 | 135 / 3.2 |
| V-047 | `Nikkor 35mm DX/ISO400/f2.8-sky/flats/DSC_0031.NEF` (the owner's) | Nikon D3300 | 159 (`Nikon.LensIDNumber`; `MakerNotes.LensID` is a signature) | Nikon F | none, a lens with CPU too | 35 / 35 / 1.8 | 35 / 2.8 |
| V-048 | `Tokina/ISO400/f2.8-sky-no-difuser/flats/DSC_0001.NEF` (the owner's) | Nikon D3300 | 139 | Nikon F | none | 11 / 20 / 2.8 | 20 / 2.8 |

Run 2026-10-26 (`FLATCHECK_TESTDATA_LARGE` on the masters' folder, `--filter RawSampleTests`):
8 passed. The console's identification on the same files is quoted in
`docs/design/lens-profiles.md`, section 10.

## Not automated

Figures that exist in the design notes but have no case here, and why:

- The `--light` series on `Milkyway-Core/2026-08-12-Smida` (32 frames sweeping from +3.13 %
  through -19.20 % to +35.73 %, correlation -0.94): a 32-frame run of 24 MP lights, and the
  frames are not in the manifest. The figures are in `modes-light.md`; the behaviour is pinned
  by the synthetic series goldens.
- The per-orientation numpy analyses of the flat sources (`flat-sources.md`): benches, not the
  tool.

## How to re-measure

```bash
dotnet test FlatCheck.sln -c Release --filter "Category=Validation"
```

with `FLATCHECK_TESTDATA_LARGE` set to the masters' folder, or `/validate` in Claude Code, which
prints expected versus measured for every case and stops on drift instead of editing the figures.
Every case also prints its measured figures as a JSON line, and appends it to the file named by
`FLATCHECK_VALIDATION_DUMP` when that is set; that is how the expected values were first filled.

## History

- 2026-10-26: re-measured at the wrap-up of the lens-on-every-mount task after its merge (876782d):
  48 cases (V-001 to V-040 and the eight raw rows) passed, no drift, 6.9 min; `Analysis/` and
  `Report/` were touched only in the lens identification and its wording.

- 2026-10-26: V-041 to V-048 added, the lens metadata of six raws of other makes and two of the
  owner's Nikon raws (ADR 0068); no
  figure of V-001 to V-040 touched, no formula moved.

- 2026-09-28: the Split tab's two real runs recorded above as dated measurements (no JSON
  case; the part masters are transient). No formula moved; the 40 cases were re-measured at
  wrap-up after the `StructureRun.Analyze` refactor: 40 passed, no drift, 2 min 10 s.

- 2026-09-25: all 40 cases recorded from the current build (the process migration, ADR 0001;
  no formula changed). Earlier figures from the session notes are quoted in the tables above
  where they exist; V-013's residual and worst tile differ from the 2026-09-23 note (2.86 /
  2.65 here against 2.93 / 2.94 there) and were not investigated.
