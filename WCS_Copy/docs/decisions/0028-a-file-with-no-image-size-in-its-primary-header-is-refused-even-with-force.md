# 0028. A file with no image size in its primary header is refused, even with force

Date: 2026-11-18
Status: accepted 2026-11-18

## Context

The run compares the two files' `NAXIS1` and `NAXIS2` (R-005). `FitsHeader.GetInt` gives -1 for a
card that is missing or not a whole number, and the run compared those -1 as sizes. CR-011 of the
review of 2026-11-16 showed the case: a file whose primary HDU holds no image (`NAXIS = 0`, the
image in an extension, as astropy and fpack write it) against a normal one was refused as
`Different dimensions: source (-1, -1) vs destination (100, 80).` with the advice to use
`--force`, and forcing wrote the WCS into a header no program reads the image from. Two files
that both lacked the keys passed as equal sizes and were copied; `docs/design/fits-header.md`
called that deliberate, "such a file is not an image". The new line was the owner's to approve.

## Decision

A file whose primary header has no `NAXIS1` or no `NAXIS2` (as `GetInt` reads them) is refused
before the sizes are compared, after the same-file check (R-019), a dry run too, whatever the
force says (R-060), with a line that names the file and says why: `The source has no image size in
its main header (NAXIS1 or NAXIS2 is missing): its image may be in an extension, which this tool
does not read.`, or `The destination has ...`; the source is checked first. The owner chose,
on 2026-11-18, the refusal that force does not lift, and approved the line in both languages with
the plan `docs/plans/2026-11-18-fix-cr-011-to-cr-018.md`.

## Alternatives rejected

- **The refusal, with force letting it through as for different sizes.** A forced copy writes the
  WCS where no reader looks for the image; there is no case where that is the copy meant.
- **Read the first image extension.** The tool reads and writes the primary HDU alone (R-013); an
  extension is a new feature, not this fix.
- **Keep passing two files without sizes as equal.** Neither has an image in its primary header,
  so the copy would be the same wrong write twice over.

## Consequences

A file with `NAXIS1 = 0` (a legal empty axis) is not this case and is compared as before. The
log writes a Warn line with the size as read (`-1x80`) and the done line `refused: no image size
in the source` (or `in the destination`). The window shows the core's line as any refusal; its
fact line was already empty for such a file (R-030). Guarded by
`WcsCopyTests.R060_Run_ASourceWithNoImageSize_IsRefusedForceOrNot`,
`R060_Run_ADestinationWithNoImageSize_IsRefusedWithItsLine`,
`R060_Run_TwoFilesWithNoImageSize_AreRefusedWithTheSourcesLine` and
`CopyWcsCliTests.R060_Run_ASourceWithNoImageSize_ReturnsOneWithTheLineOnStderr`.
