# 0024. The reader refuses a file that is not FITS at its first card, and reads 1,000 header blocks at most

Date: 2026-11-18
Status: accepted 2026-11-18

## Context

`FitsHeader.ReadFrom` read block after block until an `END` card or the end of the stream,
keeping every 80 bytes as a string, and looked at the first card only after the loop. CR-007 of
the review of 2026-11-16 showed a 200 MB file of random bytes read whole, 4,588 ms and the peak
working set from 65 MB to 611 MB, then called truncated, never "not FITS"; a file of several GB
would end in an `OutOfMemoryException` that neither the run nor the window's facts catch. The
case is a TIFF, an XISF, a SER or a video of the same night picked through "All files", pasted,
or given on the command line; the window reads every path it is given (R-030). On 2026-11-18,
the 5,200 FITS files under `E:\Photografie` all had an END card, in a header of 1 block (4,924
files), 2 (12), 3 (255) or 4 (9): 11,520 bytes at most. The review left a ceiling on the header
to the owner.

## Decision

The first card is looked at as soon as its 80 bytes are read: a file whose first card is not
`SIMPLE` is refused with the reader's "not FITS" message after its first block, 2,880 bytes at
most. A header with no END in its first 1,000 blocks (2,880,000 bytes, 250 times the largest of
the owner's) is refused there, with the message a file that ends before END gets, the owner's
answer of 2026-11-18; no new text (R-015). A file shorter than one card has no first card and
keeps that message too.

## Alternatives rejected

- **The first-card check alone, no ceiling.** A file that begins as FITS and never reaches END,
  a damaged END or a writer's bug, would still be read to its end: seconds, 2.4 times its size
  in memory, a crash for one of several GB. The owner chose the ceiling.
- **A message of its own for the ceiling.** "the END card is missing" is true of the first
  1,000 blocks, and the owner has not met such a file; a new text would have to be approved for
  a case none of the 5,200 files shows.

## Consequences

Measured on 2026-11-18 on the same 200 MB file of random bytes as the review's case, with the
Release console and the log off: main's build took 2,153 and 2,241 ms with a peak working set of
585 MB and said `Truncated file, or not a valid FITS (the END card is missing).`; this one took
161 to 299 ms at 26 to 27 MB and said `The file does not begin with SIMPLE - it does not look
like FITS.` A text file shorter than 2,880 bytes but longer than one card now gets the "not FITS"
message where it got "truncated". A header of more than 1,000 blocks cannot be copied; should one
appear, the constant `MaxHeaderBlocks` is the one place to raise. Guarded by the R-015 tests of
`FitsHeaderTests` (a 10 MB text file read for 2,880 bytes at most, a header refused after
exactly 1,000 blocks, one of exactly 1,000 blocks read) and
`CopyWcsCliTests.R015_Run_ALargeSourceThatIsNotFits_IsRefusedAsNotFits`.
