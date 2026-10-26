# Validation

The project's memory of "how well it works, measured on what". Every case lives in
`validation.json`, read by `ValidationTests` (trait `Category=Validation`, not in the gate,
ADR 0005), and is explained here. A number changes only after a re-measurement, with a dated
line under History saying the old value, the new value and why; never by editing the expected
values to match (the kit's rule "No test edited to pass").

The data is three exports of one frame, the owner's Milky Way core, whose originals are in
`E:\Photografie\Claude`: `de_printat.png` (5938 x 3888, 16-bit RGB PNG, 103 387 067 bytes),
`de_printat_PRINT_preview_16bit.tif` (the same frame after the print-preview stretch, 5938 x
3888, 16-bit RGB TIFF, 130 166 332 bytes) and `de_printat_50x35_TIFF8.tif` (cropped to 50:35,
5554 x 3888, 8-bit RGB TIFF, 20 099 182 bytes). Since 2026-10-05 two FITS files of the same
frame, from `E:\Photografie\Claude\Processed_WithFlats_RBF_light`, are beside them:
`result_3813s.fit` (the linear stack, 5938 x 3888, three planes of 32-bit float, 277 053 120
bytes) and `result_3813s_Graxpert_AI.fit` (the same after GraXpert's background extraction,
277 047 360 bytes), both written by Siril 1.4.4 with `ROWORDER = 'BOTTOM-UP'`. Copies made by hand sit in
`tests/testdata/large/`, which git ignores, and `tests/testdata/manifest.json` holds their
SHA-256. On a machine or in a worktree without the copies, the cases print `NOT MEASURED` and
pass.

## How a case runs

One kind of case, `pair`, which does what the application does when it opens a pair, without a
window:

1. `RoutingImageLoader.Probe` on both files (the loader the application uses: the project's
   reader for a FITS, WIC for the rest; until 2026-10-05 `WicImageLoader`) and
   `ImagePairValidator.Inspect` on the two answers: what the status bar would say about each
   file, and the canvas.
2. A forced release (`MemoryHousekeeping.ReleaseImageBuffers`) and a reading of the process's
   private bytes, the baseline; then `ImagePairLoader.LoadAsync` with the padding question
   answered yes, timed.
3. The pair dropped, `ReleaseImageBuffers` again, and a second reading.

`ValidationTests` prints the figures as one JSON line, `MEASURED <id> {...}`, in the test output
and in the file `IVC_VALIDATION_DUMP` names when it is set. The names: `before.*` and `after.*`
for each file (`width`, `height`, `bitsPerChannel`, `channels`, `format`, `displayBytes`,
`fileBytes`); `pad.*` for the canvas (`width`, `height`, `requiresPadding`, `dimensions`);
`load.*` for the loaded pair (`success`, `displayBytes`, `beforeIsBgra32`, `afterIsBgra32`,
`frozen`, and `beforeFormat` and `afterFormat`, the format the loaded image reports, which for
a FITS can say more than the probe's, R-018); `memory.privateDeltaMb`, what the load added to the baseline, and
`memory.privateAfterReleaseDeltaMb`, what is left after the release, in megabytes of 1 048 576
bytes with one decimal; and `decode.seconds`. A case's `expected` lists the figures it guards:
integers, booleans and strings exactly, the memory figures within the case's `tolerance` (60 MB:
private bytes move with the collector, the runtime and what else ran in the test host).
`decode.seconds` depends on the machine and is never guarded. A case whose `expected` is `null`
is unmeasured: it prints what it found and passes.

```bash
dotnet test ImageVisualCompare.sln -c Release --filter "Category=Validation"
```

Or `/validate` in Claude Code, which prints the expected and measured figures of every case and
stops on a drift instead of editing them.

## Cases

| ID | Before | After | What it guards |
| --- | --- | --- | --- |
| V-001 | `de_printat.png` | `de_printat_PRINT_preview_16bit.tif` | a matched 16-bit pair, PNG against TIFF: what the probe reports for both (5938 x 3888, 16 bits, RGB, `16-bit RGB`, 92 347 776 display bytes, the file sizes), no padding, a load whose two bitmaps are Bgra32 and frozen, 184 695 552 display bytes, the private memory the load adds and what the release leaves |
| V-002 | `de_printat.png` | `de_printat_50x35_TIFF8.tif` | a mismatched pair, 16-bit against 8-bit: the probe of the second file (5554 x 3888, 8 bits, RGB, `8-bit RGB`, 86 375 808 display bytes), a 5938 x 3888 canvas with `RequiresPadding` true and `Dimensions` `5938 × 3888`, a load once the question is answered yes, 178 723 584 display bytes, the memory figures |
| V-003 | `result_3813s.fit` | `result_3813s_Graxpert_AI.fit` | a matched pair of two float FITS files: what the probe reports for both (5938 x 3888, 32 bits, RGB, `32-bit float RGB`, 92 347 776 display bytes, the file sizes), no padding, a load whose two bitmaps are Bgra32 and frozen and still say `32-bit float RGB` (neither file was rescaled, R-016), 184 695 552 display bytes, the memory figures |
| V-004 | `result_3813s.fit` | `de_printat.png` | a FITS against a PNG, one loader each behind `RoutingImageLoader`: `32-bit float RGB` against `16-bit RGB` on one 5938 x 3888 canvas with no padding, the load, the memory figures |

## The figures of 2026-10-05

Measured twice on the branch `claude/Add-FITS-support`, Release, in one test host where the
cases ran in the order V-003, V-002, V-001, V-004 both times. `validation.json` holds the first
run for V-003 and V-004; V-001 and V-002 passed against their figures of 2026-10-02, which were
not touched. Every exact figure was the same in both runs.

| Case | Decode, s | Private memory the load adds, MB | Left after the release, MB | The two Bgra32 buffers, MB |
| --- | --- | --- | --- | --- |
| V-003 | 2.75, 2.55 | 204.3, 204.3 | 28.1, 29 | 176.1 |
| V-004 | 2.7, 3.14 | 175.6, 175.8 | -0.6, -0.5 | 176.1 |
| V-001 | 3.14, 3.64 | 176.3, 175 | -1.2, -2.6 | 176.1 |
| V-002 | 2.06, 2.44 | 172.5, 173.2 | 1.1, 1.7 | 170.4 |

V-003 carries about 28 MB that is not the pair's: it is the first FITS load of the process. The
same pair loaded and released five times in a process of its own left +29.7, +29.5, +29.8, +29
and +30.1 MB over the baseline, with +206 to +207 MB while loaded: paid once, not once a load.
V-004, which runs after it, shows a FITS and a PNG costing their two buffers and giving them
back. Two float FITS files of 277 MB each decode in about the time two 16-bit files of 103 and
130 MB take through WIC.

## The figures of 2026-10-02

Measured twice on the build of 2026-10-02 (layer 3 of
`docs/plans/2026-10-02-adopt-the-starter-kit.md`), Release, in one test host where V-002 ran
before V-001 both times. `validation.json` holds the first run. Every exact figure (sizes,
formats, the canvas, the load) was the same in both runs.

| Case | Decode, s | Private memory the load adds, MB | Left after the release, MB | The two Bgra32 buffers, MB |
| --- | --- | --- | --- | --- |
| V-001 | 2.74, 2.82 | 180.2, 181 | 2.4, 3.1 | 176.1 |
| V-002 | 1.91, 2.04 | 169, 169 | -2, -2.2 | 170.4 |

A load costs its two Bgra32 buffers and about 4 MB more, and the release gives all of it back,
as the 2026-09-08 figures below found: there, 173 MB private after the load and 12 MB after the
release, about 161 MB for two buffers of 80.4 MB each. The decode of V-001, two files of 103 and
130 MB, matches the 2.7 s that R-080 gives for two files of about 108 MB.

## The figures of 2026-09-08

Until 2026-10-02 the only figures measured on real files were the section "Memory, measured" of
`CLAUDE.md`, taken by hand on a 5794 x 3637 16-bit pair of about 108 MB each that is not among the
files above. They are kept here as they were written, as the history of the figures;
`docs/design/imaging.md` keeps the same section beside what it explains.

> Measured on a 5794×3637 16-bit pair, ~108 MB each on disk:
>
> | | private | working set |
> |---|---|---|
> | headless load, both images | 173 MB | 199 MB |
> | after release | 12 MB | 38 MB |
> | loaded and rendered, at fit | 283 MB | 290 MB |
> | plateau after visiting several zoom levels | ~570 MB | ~300 MB |
>
> The plateau is bounded, not a leak: five zoom cycles left it flat and it drifts down when
> idle. The extra over the headless figure is WPF's render thread keeping scaled intermediates
> for the Fant resampling path.
>
> Two things follow. WIC **streams** the Rgb48 to Bgra32 conversion rather than materialising a
> 126 MB intermediate, so there is no transient spike above the steady state. And the managed
> heap stays near zero because WIC pixels are unmanaged — which is why `ReleaseImageBuffers`
> forces a finaliser pass and does *not* bother with large object heap compaction.

## History

- 2026-10-02: the file created (ADR 0001, ADR 0005); V-001 and V-002 measured the same day and
  recorded as `expected`, the decode time left out.
- 2026-10-05: FITS support (ADR 0029). V-003 and V-004 added and measured the same day; the
  cases now probe and load through `RoutingImageLoader`, as the application does, and print
  `load.beforeFormat` and `load.afterFormat`. No figure of V-001 or V-002 changed.
- 2026-10-05, later: after the review fixes to the FITS reader (the header validated, the scale
  by a share of the samples, one decode loop per sample type), all four cases ran again and
  passed against the figures above, which were left as they are: V-003 decoded in 1.55 s (2.75
  and 2.55 before), +201.5 MB and 25.5 MB after the release; V-004 in 2.06 s, +172.4 and -4.2 MB;
  V-001 in 2.71 s, +178.3 and 0.7 MB; V-002 in 1.82 s, +171.3 and -0.3 MB.
- 2026-10-21: after the documentation comments of every type and method (only `///` lines
  changed), all four cases ran again in the task's worktree and passed against the figures
  above, left as they are: V-001 decoded in 2.63 s, +177.9 MB and 0.2 MB after the release;
  V-002 in 1.78 s, +171.4 and 0.2 MB; V-003 in 1.67 s, +201.4 and 25.3 MB; V-004 in 2.10 s,
  +173.3 and -2.9 MB.
- 2026-10-22: after the fixes of the documentation review (WIC's failures as messages, the
  FITS header's two edge cases, the float TIFF shown as stored, ADRs 0031 to 0035), all four
  cases ran again in the task's worktree and passed against the figures above, left as they
  are; no case has a float TIFF. V-001 decoded in 2.63 s, +178.0 MB and 0.6 MB after the
  release; V-002 in 1.82 s, +172.9 and 1.5 MB; V-003 in 1.61 s, +201.4 and 25.4 MB; V-004 in
  2.09 s, +172.9 and -3.3 MB.
- 2026-10-25: after the move to .NET 8 (ADR 0037), all four cases ran again in the task's
  worktree and passed against the figures above, left as they are: V-001 decoded in 3.44 s,
  +178.4 MB and 0.3 MB after the release; V-002 in 2.24 s, +171.2 and -0.3 MB; V-003 in 6.03 s,
  +201.9 and 24.8 MB; V-004 in 2.37 s, +175.2 and -1.1 MB. The memory figures sit within 3.3 MB
  of the .NET 9 ones. The decode times are longer than on 2026-10-22, V-003's most; the time is
  printed and never guarded, and what made it longer was not looked for.
