# 0107. The console refuses a report over a file the run reads

Date: 2026-11-16
Status: accepted 2026-11-16

## Context

`-o` writes the report whole or not at all: a temporary file beside the target, moved over it
(R-005). Nothing compared the target with the files the run reads, and by the time the report
is saved no handle holds them. The code review of 2026-11-13 (CR-010) gave the case:
`AstroPhotoAnalyzer m31.tif -o m31.tif` prints the report, then `-> report saved: m31.tif`, and
the image is a text file of a few kilobytes; the same for the `--wcs` file and the `--icc`
profile. The exports' guard (`CurveExport.Guarded`, R-145) does not cover the report. The
window's save asks through a dialog before it replaces a file; the console asks nothing.

## Decision

In the console, `-o` naming the image, the `--wcs` file or the `--icc` profile is an error before
the analysis starts: exit 1, one line that names the file and which input it is, nothing read
and nothing written. The paths are compared in full, without regard to case, as the exports'
guard compares. For another spelling of the same file, the three inputs are held open for
reading while the report is saved, so the system refuses the write and the existing line "the
report could not be written" tells its reason.

## Alternatives rejected

- **Put the guard in `ReportWriter.SaveFull`, for the window too.** The window's dialog already
  asks before a file is replaced, and the refusal would come after the whole analysis.
- **Compare paths only.** The long-path form of the image's own path is another string.
- **Tell the other spelling with a sentence of its own.** The system's reason and an untouched
  file are enough for a case that takes a deliberate spelling to reach.

## Consequences

- The error comes before any wait, so a slip in the command costs nothing.
- `-o` naming a folder, and a missing `--wcs` file told late, are CR-060, still open.
- Guards: `R005_OutNamingAFileTheRunReadsIsRefusedBeforeTheAnalysis`,
  `R005_OutNamingTheImageByAnotherSpellingLeavesItUntouched`.
