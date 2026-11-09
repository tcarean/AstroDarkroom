# Validation

The project's memory of "how well it works, measured on what". Every case lives in
`validation.json` (read by `ValidationTests`, trait `Category=Validation`, not in the gate, ADR
0004) and is explained here. A number changes only after a re-measurement, with a dated line
under History saying old value, new value and why. The masters and raws are the ones under
`E:\Photografie\Astro\Flats and Darks`, listed with their SHA-256 in
`FlatCheck.Tests/testdata/manifest.json`; `FLATCHECK_TESTDATA_LARGE` names that folder.

Three kinds of case, one per run the figures come from:

| Kind | What runs | What is recorded | Tolerance |
| --- | --- | --- | --- |
| `structure` | `StructureRun` on the master, no profiles | the five deviation tests, the vignetting depth, the local spot, the verdict | 0.05 points |
| `profile` | `StructureRun` with the first raw of `flats/` as `--raw` | the master's depth, the profile that decides (R-128) and the difference in stops, the depth verdict, the run's verdict | 0.02 stops |
| `against` | `SyntheticRun` from the lens's Adobe profile at 20 mm f/2.8, crop 1.523, then `AgainstRun` | the two depths, the difference in stops and as corner error, the ratio's residual, shape / depth / overall verdict | 0.02 stops |

The `decides` lens name and the verdict words are compared exactly; positions (`spotAt`) are
information, not compared.

## Cases

Recorded on 2026-09-25 from the build of that day (2.2.0 plus the process migration, no formula
changed), 40 cases in 2 min 30 s. Where a session note of 2026-09-16, -18 or -23 quotes the same
figure from an earlier build or from a numpy bench, it is given in the last column; agreement to
the second decimal is the check that the port and the notes describe the same thing.

### Structure on every master (V-001 to V-015)

| ID | Master | Verdict | corners / azimuthal / residual / tilt / worst tile (%) | depth (stops) | earlier figure |
| --- | --- | --- | --- | --- | --- |
| V-001 | Nikkor 35 DX ISO400 sky | BUN | 4.00 / 3.75 / 4.03 / 1.82 / 3.10 | 0.63 | corner spread 4.00 % (2026-09, `thresholds.md`); residual 4.05 and local spot -0.38 % until 2026-11-06 (ADR 0091); 4.00 / 3.75 / 4.00 / 1.75 / 3.24, 0.63 stops until 2026-11-09 (ADR 0094) |
| V-002 | Nikkor 35 DX ISO800 sky | ACCEPTABIL | 10.96 / 5.25 / 7.23 / 7.88 / 5.72 | 0.62 | 10.96 / 5.26 / 7.17 / 7.97 / 5.79, 0.62 stops until 2026-11-09 (ADR 0094) |
| V-003 | Samyang ISO1600 f/2 sky | ACCEPTABIL | 10.53 / 6.44 / 8.40 / 11.26 / 5.46 | 0.60 | 10.53 / 6.39 / 8.38 / 11.14 / 5.44, 0.60 stops until 2026-11-09 (ADR 0094); local spot -0.66 % until 2026-11-09 (ADR 0094, another spot of the same size) |
| V-004 | Samyang ISO1600 sursa_neuniforma f/2 lcd | BUN | 3.49 / 2.72 / 3.04 / 4.15 / 2.38 | 0.63 | 3.49 / 2.67 / 2.98 / 4.04 / 2.36, 0.63 stops until 2026-11-09 (ADR 0094) |
| V-005 | Samyang ISO1600 sursa_neuniforma f/2.8 | BUN | 6.09 / 7.11 / 6.61 / 7.82 / 4.15 | 0.18 | |
| V-006 | Samyang ISO400 f/2 sky | BUN | 7.59 / 6.93 / 6.41 / 8.15 / 4.17 | 0.61 | 7.59 / 6.83 / 6.33 / 8.03 / 4.17, 0.61 stops until 2026-11-09 (ADR 0094) |
| V-007 | Samyang ISO400 f/2.8 sky | ACCEPTABIL | 12.62 / 7.05 / 9.99 / 12.36 / 6.51 | 0.15 | tilt 12.36 %, 12.62 / 7.05 / 6.05 (the 1.2.0 note in `thresholds.md`; the worst tile did not exist then) |
| V-008 | Tokina monitor, diffuser close | BUN | 6.67 / 8.83 / 7.14 / 6.90 / 6.52 | 0.85 | residual 7.10 until 2026-11-06 (ADR 0091); 6.67 / 8.73 / 7.03 / 6.71 / 6.65, 0.85 stops until 2026-11-09 (ADR 0094) |
| V-009 | Tokina monitor, diffuser on the monitor | ACCEPTABIL | 16.41 / 10.59 / 12.26 / 11.67 / 12.44 | 0.78 | local spot +6.19 % (2026-09-16); 16.41 / 10.73 / 12.27 / 11.77 / 12.60, 0.78 stops until 2026-11-09 (ADR 0094); local spot +6.19 % until 2026-11-09 (ADR 0094, another spot of the same size) |
| V-010 | Tokina sky, no diffuser | ACCEPTABIL | 10.28 / 7.24 / 10.05 / 0.50 / 8.47 | 0.27 | residual 10.06 after the 4θ correction (2026-09-23 note) and until 2026-11-06 (ADR 0091); 0.27 stops; 10.28 / 7.25 / 9.99 / 0.49 / 8.81, 0.27 stops until 2026-11-09 (ADR 0094); local spot -0.29 % until 2026-11-09 (ADR 0094, another spot of the same size) |
| V-011 | Tokina tablet, 15 frosted covers | BUN | 2.17 / 1.99 / 2.27 / 2.64 / 1.57 | 1.24 | 1.24 stops (2026-09-18 note); 2.17 / 2.00 / 2.28 / 2.55 / 1.54, 1.24 stops until 2026-11-09 (ADR 0094) |
| V-012 | Tokina tablet, 7 frosted covers | PERFECT | 1.57 / 1.79 / 1.58 / 1.93 / 1.02 | 1.26 | 1.26 stops, PERFECT; 1.57 / 1.60 / 1.42 / 1.64 / 0.95, 1.26 stops until 2026-11-09 (ADR 0094) |
| V-013 | Tokina tablet, 3 sheets of paper, rotated in place | BUN | 1.18 / 2.18 / 2.84 / 0.73 / 2.69 | 0.76 | corners 1.2 %, tilt 0.6 %, 0.76 stops (2026-09-23 note); the note also gives residual 2.93 / worst tile 2.94 from the bench, against 2.86 / 2.65 here; not investigated on 2026-09-25; 1.18 / 2.03 / 2.86 / 0.58 / 2.65, 0.76 stops until 2026-11-09 (ADR 0094) |
| V-014 | Tokina tablet, 3 sheets of paper | BUN | 4.69 / 3.80 / 3.54 / 4.05 / 2.87 | 0.81 | 3.8 % residual, 0.81 stops; 4.69 / 3.68 / 3.50 / 3.88 / 2.77, 0.81 stops until 2026-11-09 (ADR 0094) |
| V-015 | Tokina tablet, 4 sheets of paper | BUN | 2.46 / 5.91 / 5.07 / 3.26 / 4.09 | 0.80 | 5.1 % residual, 0.80 stops; 4.98 / 3.93 until 2026-11-06 (ADR 0091); 2.46 / 5.79 / 4.93 / 3.10 / 4.07, 0.80 stops until 2026-11-09 (ADR 0094) |

### The sixth test with the raw beside the master (V-016 to V-030, V-049)

| ID | Master | Profile that decides | diff (stops) | depth | run | earlier figure |
| --- | --- | --- | --- | --- | --- | --- |
| V-016 | Nikkor 35 DX ISO400 | Adobe, Nikkor 35/1.8G DX (D90) | +0.110 | BUN | BUN | +0.11 BUN |
| V-017 | Nikkor 35 DX ISO800 | same | +0.103 | BUN | ACCEPTABIL (shape) | +0.10 BUN |
| V-018 | Samyang ISO1600 f/2 sky | Adobe, Nikkor 135 f/2 DC (by range: manual lens) | +0.104 | BUN | ACCEPTABIL (shape) | +0.10 |
| V-019 | Samyang sursa_neuniforma f/2 lcd | same | +0.131 | BUN | BUN | +0.11 / +0.10 (which of the two f/2 masters the note meant is not recorded) |
| V-020 | Samyang sursa_neuniforma f/2.8 | lensfun, Samyang 135 f/2 | +0.034 | BUN | BUN | |
| V-021 | Samyang ISO400 f/2 sky | Adobe, Nikkor 135 f/2 DC | +0.109 | BUN | BUN | |
| V-022 | Samyang ISO400 f/2.8 sky | lensfun, Samyang 135 f/2 | +0.006 | PERFECT | ACCEPTABIL (shape) | +0.01 PERFECT |
| V-023 | Tokina monitor, diffuser close | Adobe, atx-i 11-20 CF (own ID 139, the raw's) | +0.188 | ACCEPTABIL | ACCEPTABIL | +0.004 PERFECT, run BUN, against the lensfun Aspherical (7D2) entry until 2026-11-02 |
| V-024 | Tokina monitor, on the monitor | same | +0.115 | BUN | ACCEPTABIL (shape) | -0.069 BUN against the lensfun 7D2 entry until 2026-11-02 |
| V-025 | Tokina sky, no diffuser | same | -0.395 | REFA | REFA | -0.39 REFA (the AT-X PRO DX file, same figures, until 2026-11-02) |
| V-026 | Tokina tablet, 15 covers | same | +0.579 | REFA | REFA | +0.332 REFA, exit 3, against the lensfun AF 11-20 (D7100) entry until 2026-11-02 |
| V-027 | Tokina tablet, 7 covers | same | +0.593 | REFA | REFA | +0.345 REFA against the lensfun D7100 entry until 2026-11-02 |
| V-028 | Tokina paper, rotated in place | same | +0.099 | BUN | BUN | -0.085 BUN against the lensfun 7D2 entry until 2026-11-02 |
| V-029 | Tokina paper, 3 sheets | same | +0.148 | ACCEPTABIL | ACCEPTABIL | -0.036 BUN against the lensfun 7D2 entry until 2026-11-02 |
| V-030 | Tokina paper, 4 sheets | same | +0.138 | ACCEPTABIL | ACCEPTABIL | -0.046 BUN against the lensfun 7D2 entry until 2026-11-02 |
| V-049 | Nikkor 35 DX ISO400, lensfun alone (`sources: lensfun`, no Adobe folder: a computer without Adobe's profiles) | lensfun, Nikkor 35/1.8G DX (full frame 1), 3 profiles | +0.207 | ACCEPTABIL | ACCEPTABIL | before ADR 0076: 6 profiles, +0.20 ACCEPTABIL from the Viltrox AF 35mm f/1.8 Z (2026-10-28) |

The library on 2026-09-25 holds 15 sessions with a raw beside every master, so every session is
here; the ten quoted in `modes-structure.md` are a subset. Since 2026-11-02 (R-128, ADR 0090) the
profile that decides is the Adobe file of the raw's lens ID where the ID found the lens (the
Tokina and the Nikkor 35); by range alone (the Samyang) and without Adobe (V-049) it is the
nearest profile, as before.

### The master against its lens's Adobe synthetic (V-036 to V-040)

Synthetic from `Tokina AT-X 11-20mm F2.8 PRO DX` at 20 mm f/2.8, crop 1.523: 0.664 stops at the
corner.

| ID | Master | diff (stops) | corner error (%) | depth | shape | run | earlier figure (2026-09-18, `modes-synthetic.md`) |
| --- | --- | --- | --- | --- | --- | --- | --- |
| V-036 | Tokina diffuser close | +0.186 | 13.8 | ACCEPTABIL | BUN | ACCEPTABIL | +0.19, 13.8 %, ACCEPTABIL; ratio residual 7.05 % until 2026-11-09 (ADR 0094), now 7.10 |
| V-037 | Tokina sky | -0.395 | 31.5 | REFA | ACCEPTABIL | REFA | -0.39, 31.5 %, REFA |
| V-038 | Tokina on the monitor | +0.115 | 8.3 | BUN | ACCEPTABIL | ACCEPTABIL | +0.12, 8.3 %, BUN; ratio residual 12.25 % until 2026-11-09 (ADR 0094), now 12.23 |
| V-039 | Tokina tablet, 7 covers | +0.592 | 50.8 | REFA | PERFECT (ratio residual 1.6 %) | REFA | +0.57 (before the crop correction of 2026-09-18); ratio residual 1.50 % until 2026-11-09 (ADR 0094), now 1.56 |
| V-040 | Tokina paper, rotated in place | +0.099 | 7.1 | BUN | BUN | BUN | +0.14 vs Adobe in the 2026-09-23 note (its figure was on the bench, "not bad but not perfect"); ratio residual 2.83 % until 2026-11-09 (ADR 0094), now 2.78 |

### The Split tab on two real sessions (measured by hand, 2026-09-28)

The part masters exist only while the tab runs (the temp sessions are deleted after the
analysis, ADR 0054), so there is no `validation.json` case: these are the tables the tab
saved as `Rezultat split.txt`, driven headless through `MainViewModel` on the build of that
day (banner 2.2.0, before the bump). Five tests as corners / azimuthal / residual / tilt /
worst tile, in %; the reference profile was the whole's nearest then (since 2026-11-02 the row
that gives the whole's verdict, R-128), the same for every column.

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
- The `--light` series on `Veil/2026-10-06-3h` (87 frames, Samyang 135 f/2.8 on the D3300: mean
  +1.13 %, SEM 0.15, sky span 6.6 points, verdict OK; measured 2026-11-01 with the 2.3.0
  console): the positive counterpart of the Smida run, an 87-frame run of 24 MP lights whose
  frames are not in the manifest either. The figures are in `modes-light.md`, "The validity
  limit".
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

- 2026-11-09: V-001, V-002, V-003, V-004, V-006, V-008, V-009, V-010, V-011, V-012, V-013,
  V-014, V-015 and the ratio residual of V-036, V-038, V-039 and V-040 re-recorded after R-130 /
  ADR 0094 (a FITS is read with the file's first row last, as Siril shows it), on
  `claude/Show-every-FITS-as-Siril-shows-it` from main cdd7ffe6, with the 36 large files present
  with their hashes: 44 run, 27 unchanged within tolerance, 17 drifted by 0.05 to 0.58 points,
  no verdict changed, no depth moved past the thousandth. The figures move because the analysis
  geometry is anchored on pixel indices (the radial model's centre at index `(w/2, h/2)`, half a
  pixel below the geometric centre; the tile grid from row 0, its leftover rows at the far edge),
  so the same frame read the other way up sits one analysis pixel (four sensor pixels)
  differently against them. The largest moves: V-010's local spot is another spot of the same
  size (-0.287 -> +0.290), its worst tile 8.809 -> 8.472, V-012's tilt 1.643 -> 1.930. The old
  figures stay in each row's last column. A geometry symmetric under the flip (the geometric
  centre, a centred tile grid) would end the dependence; it is a formula change of its own.
- 2026-11-06: re-measured at the wrap-up of the signed-zero fix (R-129), on
  `claude/Fix-the-signed-zero-format` from main 28d2c226, which moved the signed formats of
  `Report/` to `Numbers.Signed` without touching a formula: the 36 large files present with
  their hashes, 44 passed in 2 min 47 s (the 36 cases and the 8 raw samples), and with the
  measured lines compared to `validation.json` case by case, 23 cases equal figure for figure
  and 13 within tolerance by at most 0.038 points (V-014's residual 3.499 -> 3.461, the largest),
  the same residual, worst-tile and local-spot moves the entry below left unrecorded on the day
  of ADR 0091 ("38 cases unchanged within tolerance"). No figure changed; no formula moved.
- 2026-11-06: V-001, V-008, V-010, V-015, V-036 and V-037 re-recorded after R-014 / ADR 0091 (the
  even-harmonic correction of the radial model on every ring, the whole rings included), on
  `claude/Divide-the-harmonics-out-on-every-ring` from main c3141954, with the 36 large files
  present with their hashes (44 run in 1 min 58 s; 38 cases unchanged within tolerance). The
  residual: V-008 7.104 -> 7.034, V-010 10.062 -> 9.993, V-015 4.981 -> 4.930, V-001 4.045 ->
  4.003 (within tolerance, re-recorded with its case); the ratio's residual of the `against`
  cases V-036 7.136 -> 7.055 and V-037 10.067 -> 10.028. Two figures the plan's "under 0.1
  points" did not cover: V-015's worst tile 3.931 -> 4.065, the same tile (the panel's blotch at
  0.8 of the inscribed radius, read 0.1 deeper now that the profile of its whole ring carries
  the correction), and V-001's local spot -0.384 -> +0.406, a flip at a tie: the dark spot was
  the printed one before, so the bright spot by the top edge was under 0.384 then and reads
  +0.406 now; in the Python replica of that day the dark one is unchanged (-0.396) and the
  bright one grew from +0.389 to +0.407, so the flip is decided by 0.02. No verdict changed. The local spot of V-008 (-0.401
  -> -0.384) and V-015 (-0.604 -> -0.616) moved within tolerance and were re-recorded with
  their cases.
- 2026-11-02: V-023 to V-030 re-recorded after R-128 / ADR 0090 (the Adobe profile gives the depth
  verdict when the raw's lens ID found the lens), on `claude/Let-Adobe-give-the-depth-verdict`
  from main 472bfb7b, with the 36 large files present with their hashes (44 run in 1 min 50 s).
  The key `nearest` became `decides` in all 16 profile cases. The Adobe file of the raw's ID 139,
  `Tokina atx-i 11-20mm f2.8 CF [NIKON CORPORATION]`, now decides on every Tokina case: V-023
  diffStops 0.004 -> 0.188, depth PERFECT -> ACCEPTABIL, verdict BUN -> ACCEPTABIL; V-024 -0.069
  -> 0.115 (BUN, the run ACCEPTABIL by its shape as before); V-025 the deciding file's name only
  (the AT-X PRO DX file before, the same figures, -0.395); V-026 0.332 -> 0.579 and V-027 0.345
  -> 0.593 (REFA as before); V-028 -0.085 -> 0.099 (BUN as before); V-029 -0.036 -> 0.148 and
  V-030 -0.046 -> 0.138, depth and verdict BUN -> ACCEPTABIL. These are the figures the owner's
  approved plan predicted from the rows of the old output; the other 28 cases and the 8 raw
  samples did not move.

- 2026-11-02: re-measured at the wrap-up of the task that merged the Structure tab's two scales
  into one (R-126, ADR 0088), on `claude/Merge-the-Structure-tab-scales` before its commit, over
  `main` at d41bf12e. The task removed the depth scale's four meaning texts from
  `Report/StructureReport.cs` (`DepthScaleRows`) without touching a formula: the 36 large files
  present with their hashes, 44 passed in 3 min 39 s (the 36 cases and the 8 raw samples), and
  with the dump compared to `validation.json` case by case, every figure and verdict of the 36
  cases equal to the recorded one. No figure changed; no formula moved.

- 2026-11-01: re-measured at the wrap-up of the plain-words series (ADRs 0082 to 0084), on `main`
  at 91bd006e. The series reworded the explanations in `Report/StructureReport.cs`,
  `LightReport.cs`, `LightSeriesReport.cs`, `ExposureReport.cs`, `HistEqReport.cs`,
  `SyntheticReport.cs`, `SplitReport.cs` and `Runs/AgainstRun.cs` without touching a formula:
  the 36 large files present with their hashes, 44 passed in 2 min 4 s (the 36 cases and the 8
  raw samples), and with the dump compared to `validation.json` case by case, every figure and
  verdict of the 36 cases equal to the recorded one. No figure changed; no formula moved.

- 2026-10-31: V-031 to V-035, the `smooth` kind, removed with the `--smooth` mode (ADR 0080);
  their last figures stay in this file's git history and in ADR 0026. The other cases are
  untouched: no formula moved and nothing was re-measured.
- 2026-10-30: V-049 added, the Nikkor 35 DX master against the shipped lensfun copy alone, the
  case of a computer without Adobe's profiles (R-116, ADR 0076): the nearest profile is the
  lens's own full-frame calibration (+0.207 stops, ACCEPTABIL, 3 profiles) where the first-word
  mount rule had let three Nikon Z lenses in and the Viltrox AF 35mm f/1.8 Z give the verdict
  (+0.20, 6 profiles, measured 2026-10-28 and again on 2026-10-30 before the fix). The 48
  recorded cases re-measured with the fix in `LensProfiles/` (no formula moved; the 36 large
  files present): 48 passed, no drift, 3 min 40 s; V-049 measured in a second run, 15 s.

- 2026-10-30: re-measured at the wrap-up of the task that leaves an unlistable `lensfun/` folder
  out of the profile search (R-115, ADR 0075), which added a text suffix to
  `Report/SyntheticReport.cs` (`SourcesSummary`) and a `try` to `ProfileLocator.Locate`: the 36
  large files present with their hashes, 48 passed in 2 min 26 s, and with the dump compared to
  `validation.json` case by case, every figure and verdict of V-001 to V-040 equal to the recorded
  one (3 min 12 s, the second run). Again after the code review's fixes to the same files (the
  version file skipped for an unreadable folder, a comment): 48 passed, every measured line
  identical to the first run's, 2 min 56 s. No figure changed; no formula moved.

- 2026-10-28: re-measured at the wrap-up of the task that says on the Structure tab when Adobe's
  lens profiles are missing (R-113, ADR 0073), which added texts to `Report/StructureReport.cs`
  that the console does not print: the 36 large files present with their hashes, 48 passed, no
  drift (every figure of V-001 to V-040 equal to the recorded one), 5.0 minutes. Again after the
  code review's fix to the same file (046f3e3): 48 passed, every measured line identical to the
  first run's, 4.3 minutes; and after the second review's (4589022): the same, 4.4 minutes. No
  figure changed; no formula moved.

- 2026-10-27: all 48 cases re-measured at the wrap-up of the diagnostic log's review fixes
  (`claude/Fix-the-diagnostic-log-review-findings`, after 97808e8), which touched `Report/` with
  a new refusal text (`SyntheticReport.NoProfile`, ADR 0071), a dead `(N skipped)` branch
  removed and `StructureReport.CornerName` written through `EnglishCorner`: 48 passed, every
  figure and verdict of V-001 to V-040 equal to the recorded one, 2 min 48 s. No formula moved.

- 2026-10-26: all 48 cases re-measured on `main` at 876782d (the merge of ADR 0068) while the
  README was rewritten in English (ADR 0069), whose tables quote V-016 to V-040: 48 passed, no
  drift, 5.8 minutes. Again after the note under the illumination offset was reworded (R-013, a
  text in `Report/`): 48 passed, no drift, 4.1 minutes. No figure changed; no formula moved.

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
