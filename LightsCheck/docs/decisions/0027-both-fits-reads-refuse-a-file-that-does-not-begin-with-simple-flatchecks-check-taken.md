# 0027. Both FITS reads refuse a file that does not begin with SIMPLE and a header cut before END: FlatCheck's check is taken, and the frame record stops guessing

Date: 2026-10-27
Status: accepted 2026-10-27
Supersedes: 0018 in part (the SIMPLE check left for a task of its own); 0021 in part (the same); 0025 in part (the SIMPLE check left out)

## Context

FlatCheck's ADR 0057 (its R-008, 2026-10-09) gave its `FitsReader` one keyword reader, `KeyOf`,
and one magic check, `RequireSimple`, run by both reads before any card is parsed: a file whose
first card is not `SIMPLE` is refused with `FITS: the file does not begin with SIMPLE.`, a
header that begins well but ends before `END` with `FITS: incomplete header.`, from the
metadata read and the full load alike. The finding that led to it came from LightsCheck's own
review: its inventory had to recognise a non-FITS `.fit` by an empty header after `LoadInfo`,
a detour every caller of the empty header would repeat.

LightsCheck's copy kept the old behaviour. ADR 0018 translated its comments and took no
behaviour change; ADR 0021 took the cut-data check of R-009 and named this one as a task of its
own; ADR 0025 took one sentence more. Until today the copy's metadata read gave a non-FITS an
empty header, `FrameInfo.Read` turned the emptiness into `not a FITS file (it does not begin
with SIMPLE)`, a header cut before `END` passed silently with the cards of the whole blocks
before the cut, and the full load ended on a non-FITS with `incomplete header` or `no HDU with
a 2D image found`. The owner asked on 2026-10-27, after an explanation of the gap, to take it.

## Decision

- **The hunk, token for token**: `NotSimple`, `HeaderIncomplete`, `KeyOf` and `RequireSimple`
  as FlatCheck wrote them; `LoadHeader` runs the magic check on the first block and refuses a
  short block; `LoadFull` runs it on the whole file and names its message through the property;
  both reads parse a keyword through `KeyOf`. The marker names the hunk.
- **The frame record stops guessing**: `FrameInfo.Read` passes the reader's exception on as the
  frame's error, as it does for every other unreadable file, and the empty-header branch is
  gone with its invented message.
- **The words are the reader's**, in both languages, FlatCheck's pairs word for word (ADR 0022).

## Alternatives rejected

- **Keep the guess and the empty header.** The copy would go on inventing the reader's message
  outside the reader, and a header cut before `END` would stay a silent half-read.
- **Check SIMPLE on the metadata path only.** FlatCheck's first fix, which its review rejected:
  the two reads of one file would disagree.
- **Keep the old row text for the goldens' sake.** The golden is the output; the output changes
  on purpose, and the two rows are read and tied to this change.

## Consequences

- `FitsReader.NotSimple`, `HeaderIncomplete`, `KeyOf`, `RequireSimple`; `LoadHeader` and
  `LoadFull` refuse the same files with the same words; `FrameInfo.Read` is a plain try.
- R-008 is `implemented`: `FitsReaderTests` (`R008_`) refuse an empty file, 4 bytes, 100 bytes
  of zeros, 100 of spaces, a block starting with `NAXIS` and a text file with the SIMPLE message, in both
  languages, from both reads; 100 bytes after `SIMPLE` and one block without `END` with the
  incomplete-header message; the frame record carries the reader's message.
- The inventory's error row for a junk `.fit` reads `error: FITS: the file does not begin with
  SIMPLE.` (`eroare: FITS: fisierul nu incepe cu SIMPLE.`); the two inventory goldens change
  that row.
- The copy's code is FlatCheck 2.3.0's with the edits ADR 0003 lists plus the hunks of ADRs 0021,
  0025 and this one; a whole refresh from FlatCheck is one step nearer.
