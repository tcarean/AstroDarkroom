# 0015. Both headers are probed before either decode, the decodes never overlap, a file failure is a message, never a crash

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from `CLAUDE.md` ("Structure", the bullet for
`Imaging/ImagePairLoader.cs`), the comments in `ImagePairLoader.LoadAsync` and
`IsExpectedLoadFailure`, the `IImageLoader.Probe` summary and the `Probe` comment in
`WicImageLoader.cs`; the decision itself dates from a6e5f10 (the base version, 2026-09-08),
where the probes served to refuse a mismatched pair before any decode, and 861f5c1 (2026-09-09)
turned that refusal into the question of ADR 0014. Decoding a pair costs "several seconds and
~170 MB" (the `LoadAsync` comment); two files of about 108 MB take about 2.7 s (README
"Memorie"). The same comment calls the transient decode buffer the largest single allocation in
the application. A file the user picks can be missing, locked, truncated, in a format no codec
handles, or too large for the machine (the `IsExpectedLoadFailure` comment).

## Decision

- `ImagePairLoader.LoadAsync` probes A, then B, and settles the canvas (ADR 0014) before any
  pixel is decoded. `IImageLoader.Probe` reads only the header: `WicImageLoader.Probe` opens the
  decoder with `BitmapCreateOptions.DelayCreation`, which reads the header and stops there.
- The two decodes are awaited one after the other, A then B, never both at once. Progress is
  reported as each starts: 0 with A's file name, 0.5 with B's, 1 with none at the end.
- An `IOException`, `UnauthorizedAccessException`, `NotSupportedException`,
  `FileFormatException`, `ArgumentException` or `OutOfMemoryException` from a probe or a decode
  becomes `ImagePairLoadResult.Failure`: `Could not read <file name>.`, then the exception's
  message on the next line, naming the file that was being read. `OperationCanceledException` is
  rethrown; any other exception is not caught.

## Alternatives rejected

- **Decode first, compare the sizes afterwards.** A pair that needs a decision would reach the
  user after "several seconds and ~170 MB on pixels that may be about to be thrown away";
  probed first, it has allocated nothing (the `LoadAsync` comment).
- **Decode the two in parallel.** Overlapping them "would double the peak for no gain the user
  would notice" (the same comment).
- **A crash dialog for a file that cannot be read.** None of those failures is a defect in this
  program (the `IsExpectedLoadFailure` comment); the user gets the file's name and the reason,
  and picks again.

## Consequences

- Until the user answers, a mismatched pair costs two header reads; refused or declined, it
  never reaches a decode (ADR 0014).
- `MainWindow.OpenPairAsync` shows a failure in a `MessageDialog` titled
  `Cannot compare these files` (ADR 0018) and reopens the file dialogs.
- An exception type not on the list is not caught; turning it into a message means adding it to
  `IsExpectedLoadFailure`.
- The measurement of 2026-09-08 found that WIC streams the Rgb48 to Bgra32 conversion instead of
  materialising a 126 MB intermediate, so a load has no transient spike above the steady state
  (`CLAUDE.md`, "Memory, measured"); the loader's comment still names a transient decode buffer
  as the largest allocation. No figure exists for two decodes at once.
- Guards: `ImagePairLoaderTests` (two probes and no decode for a mismatched pair; at most one
  decode running at a time; a missing and a corrupt file become messages naming them; progress
  as each image starts), `WicImageLoaderTests` (the probe reports what a full load does).

Source: CLAUDE.md ("Structure", "Memory, measured"); README.md ("Memorie");
src/ImageVisualCompare.Wpf/Imaging/ImagePairLoader.cs;
src/ImageVisualCompare.Wpf/Imaging/IImageLoader.cs;
src/ImageVisualCompare.Wpf/Imaging/WicImageLoader.cs; src/ImageVisualCompare.Wpf/MainWindow.xaml.cs.
