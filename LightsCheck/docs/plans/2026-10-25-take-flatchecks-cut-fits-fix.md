# Take FlatCheck's cut-FITS fix into the LightsCheck copy

## Context

The defects task of 2026-10-25 found that a FITS whose data is shorter than its header says
throws `IndexOutOfRangeException` from `FitsReader.LoadFull`, a file copied from FlatCheck. The
fix went to FlatCheck first (its R-009, b9b20a6 and 469da9b, merged 2026-10-25). The owner then
asked to take it into every project that uses the reader: LightsCheck and DarksCheck carry the
copy; AstroPhotoAnalyzer's own stream reader already refuses a cut data area (its
`FitsReaderTests` asserts `trunchiat`) and takes nothing. One branch per project:
`claude/Refuse-a-cut-FITS-in-LightsCheck`, `claude/Refuse-a-cut-FITS-in-DarksCheck`. The owner's
message is the plan; no plan mode.

## Steps, one commit

1. Requirement first: R-008 gains the cut-data refusal; R-017 says `--frame` on a light no reader can decode exits 1 with `EROARE: <message>`.
2. Tests first, FlatCheck's four: `Load_ThrowsOnATruncatedFile` tightened from `ThrowsAny` to
   `InvalidDataException`, and `Load_RefusesAFitsWhoseDataIsCut`,
   `Load_ReadsAFitsWhoseLastPaddingIsMissing`, `Load_RefusesACutHduWithoutA2DImage`, plus `AcceptanceTests.R017_FrameOnAFitsWhoseDataIsCutEndsWithAnErrorLineNotACrash`.
   Run before the fix: 4 failed (the three refusals and the `--frame` test), the unpadded one passed, as it guards against refusing too much.
3. The fix: FlatCheck's hunk (the check before the sample loop, `DataCut` with the Romanian text
   of 469da9b), the marker and `LoadFull`'s comment; ADR 0021, and a dated note on ADR 0019, which said the crash stayed until FlatCheck's fix.

## Verification

`scripts/verify.ps1` green at the commit; the tests above pass after the fix.

## What deviated

Nothing. No numeric code changed, so `/validate` was not run.
