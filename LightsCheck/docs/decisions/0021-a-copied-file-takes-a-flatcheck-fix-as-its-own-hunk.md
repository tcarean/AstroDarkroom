# 0021. A copied file takes a FlatCheck fix as its own hunk, without the rest of FlatCheck's later code

Date: 2026-10-25
Status: accepted 2026-10-25; the SIMPLE check left for a task of its own superseded by 0027

## Context

`FitsReader.LoadFull` read `NAXIS1 x NAXIS2 x ... x |BITPIX| / 8` bytes of samples without
checking that the file held them, so a FITS cut mid-transfer threw `IndexOutOfRangeException`
out of the sample loop. On LightsCheck, `lightscheck --frame N` on such a light ended in an unhandled exception, and the inventory and the series listed it with the bare `Index was outside the bounds of the array.` The file is a copy (ADR 0003, the rule "Copied files
stay copies"), so the fix went to FlatCheck first: its R-009, commits b9b20a6 and 469da9b,
merged 2026-10-25. FlatCheck's current `FitsReader.cs` also carries code LightsCheck's copy does
not have: the FITS-magic check of FlatCheck's ADR 0057, which changes what a non-FITS file gives,
and its English-or-Romanian messages (`T(...)`), which LightsCheck's core has no language for. The
owner asked on 2026-10-25 to take the fix into every project that uses the reader.

## Decision

The copy takes FlatCheck's fix alone: the check before the sample loop
(`pos + dataBytes > raw.Length`, every HDU) and its message `DataCut`, the Romanian text of
469da9b, as FlatCheck wrote them, with FlatCheck's four tests (the tightened truncated-file test
and the three new ones, named without FlatCheck's `R009_`). The rest of FlatCheck's later code
stays out until a task of its own refreshes the copy whole. The file's marker names the hunk and
its commit.

## Alternatives rejected

- **Refreshing the copy whole from FlatCheck now.** It would bring in the FITS-magic check and
  FlatCheck's language switch, two behaviour changes the owner did not ask for in this task.
- **Writing the check here in LightsCheck's own words.** A second version of the same fix would
  drift from its source; the hunk is taken token for token so the next refresh diffs cleanly.

## Consequences

`FitsReaderTests` pins the refusal, both counts in the message, the unpadded last HDU that still
loads, and the cut HDU without an image. The copy's code is now FlatCheck 2.3.0's with the edits
ADR 0003 lists plus this one hunk; its comments stay as ADR 0018 says. AstroPhotoAnalyzer's
reader, a stream reader of its own started from FlatCheck's, already refuses a cut data area
(`FITS: date trunchiate.`, its `FitsReaderTests`) and took nothing.

Note 2026-10-27: the one sentence of 469da9b the hunk had left out, on `LoadHeader`'s doc
comment (the header read does not check the data area), is in the copy (ADR 0025); the SIMPLE
check followed the same day (ADR 0027).
