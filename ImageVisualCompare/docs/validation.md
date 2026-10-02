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
5554 x 3888, 8-bit RGB TIFF, 20 099 182 bytes). Copies made by hand sit in
`tests/testdata/large/`, which git ignores, and `tests/testdata/manifest.json` holds their
SHA-256. On a machine or in a worktree without the copies, the cases print `NOT MEASURED` and
pass.

## How a case runs

One kind of case, `pair`, which does what the application does when it opens a pair, without a
window:

1. `WicImageLoader.Probe` on both files and `ImagePairValidator.Inspect` on the two answers: what
   the status bar would say about each file, and the canvas.
2. A forced release (`MemoryHousekeeping.ReleaseImageBuffers`) and a reading of the process's
   private bytes, the baseline; then `ImagePairLoader.LoadAsync` with the padding question
   answered yes, timed.
3. The pair dropped, `ReleaseImageBuffers` again, and a second reading.

`ValidationTests` prints the figures as one JSON line, `MEASURED <id> {...}`, in the test output
and in the file `IVC_VALIDATION_DUMP` names when it is set. The names: `before.*` and `after.*`
for each file (`width`, `height`, `bitsPerChannel`, `channels`, `format`, `displayBytes`,
`fileBytes`); `pad.*` for the canvas (`width`, `height`, `requiresPadding`, `dimensions`);
`load.*` for the loaded pair (`success`, `displayBytes`, `beforeIsBgra32`, `afterIsBgra32`,
`frozen`); `memory.privateDeltaMb`, what the load added to the baseline, and
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

## The figures of 2026-10-02

Not measured yet: the cases are measured when `ValidationTests` exists, in layer 3 of
`docs/plans/2026-10-02-adopt-the-starter-kit.md`.

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

- 2026-10-02: the file created (ADR 0001, ADR 0005), with V-001 and V-002 unmeasured.
