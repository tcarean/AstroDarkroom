# 0057. A metadata read refuses a file that does not begin with SIMPLE, instead of answering with an empty header

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

`FitsReader.LoadHeader` reads the header blocks of a FITS file alone, for the callers that want
`FOCALLEN` or `INSTRUME` without the pixels (ADR 0030): the lens identification, the window's
Sintetic prefill, the reason card of its Structure tab. Since it was written it answered a file
that does not begin with the `SIMPLE` card with an empty dictionary, by design: "Dictionar gol
daca fisierul nu incepe cu SIMPLE". The full load, `FitsReader.Load`, fails on the same file
(`FITS: header incomplet.` on a 100-byte file), so the two reads of one file disagreed: one
threw, the other said the file had nothing to say.

LightsCheck copies `FitsReader.cs` unchanged (its ADR 0003). Its review of 2026-10-09 found
that its inventory had to recognise a non-FITS `.fit` by `IsFitsPath && Header.Count == 0`
after `LoadInfo`, a detour that every new caller of the empty header would have to repeat, and
that a FITS with a header that is empty for another reason would be reported with the wrong
message. A copied file is fixed in FlatCheck first, then recopied, so the finding came here.

## Decision

`LoadHeader`, and so `LoadInfo` on the FITS side, throws `InvalidDataException` with the
message `FITS: fisierul nu incepe cu SIMPLE.` when the first 8 bytes of the file are not the
keyword `SIMPLE`: an empty file, one shorter than a keyword, and a whole block whose first card
is something else are all refused. The message carries no path; the callers that name the file
already do (`StructureRun.Compare`, the Sintetic prefill).

## Alternatives rejected

- **Keep the empty header and document it.** The callers already caught an exception from
  `LoadInfo` for a raw libraw cannot open; a FITS it cannot read taking a different route, a
  count of zero, is the detour LightsCheck had to write, and the message for it has to be
  invented at every caller instead of coming from the reader that knows why.
- **Also check `SIMPLE` in the full load.** `Load` already fails on such a file, with a
  message the console's goldens and R-004 ("any other extension is parsed as FITS and fails as
  one") describe; changing its words was not asked for and would touch the console's output
  for no caller's benefit. A header that begins with `SIMPLE` but ends before `END` still
  returns what it read, as before: that case was not part of the finding.

## Consequences

The window's Sintetic tab says `preumplerea a esuat: FITS: fisierul nu incepe cu SIMPLE.` on a
master that is not a FITS, where it used to leave every field empty without a word; its
`Structure` tab is unaffected, because its run fails on the full load first. LightsCheck can
drop its detour and let the message come from the exception (its R-003). The guard is
`R008_LoadInfoRefusesAFileThatDoesNotBeginWithSimple` in `ImageLoaderTests`; the row is
R-008.
