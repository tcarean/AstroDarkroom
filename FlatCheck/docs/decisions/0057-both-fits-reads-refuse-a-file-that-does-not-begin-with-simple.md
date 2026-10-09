# 0057. Both FITS reads refuse a file that does not begin with SIMPLE, instead of one answering with an empty header

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

`FitsReader.LoadHeader` reads the header blocks of a FITS file alone, for the callers that want
`FOCALLEN` or `INSTRUME` without the pixels (ADR 0030): the lens identification, the window's
Sintetic prefill, the reason card of its Structure tab. Since it was written it answered a file
that does not begin with the `SIMPLE` card, or one cut before `END`, with an empty dictionary,
by design: "Dictionar gol daca fisierul nu incepe cu SIMPLE". The full load, `FitsReader.Load`,
never checked `SIMPLE` either: it failed on a 100-byte file with `FITS: header incomplet.`,
parsed a longer non-FITS as cards until it found `END` or ran out, and accepted a header
without `SIMPLE` that carried `NAXIS` and `END`. The two reads of one file could disagree in
both directions.

LightsCheck copies `FitsReader.cs` unchanged (its ADR 0003). Its review of 2026-10-09 found
that its inventory had to recognise a non-FITS `.fit` by `IsFitsPath && Header.Count == 0`
after `LoadInfo`, a detour that every new caller of the empty header would have to repeat, and
that a FITS with a header that is empty for another reason would be reported with the wrong
message. A copied file is fixed in FlatCheck first, then recopied, so the finding came here.
The code review of the first fix, which checked `SIMPLE` on the metadata path only, found the
two remaining gaps: a header that begins with `SIMPLE` but ends before `END` still gave an
empty header, and the full load still accepted what the metadata read refused.

## Decision

The reader has one definition of a card's keyword, `KeyOf`, and one magic check,
`RequireSimple`, that both reads run before parsing anything: a file whose first 8 bytes are
not the keyword `SIMPLE` (an empty file, one shorter than a keyword, a whole block whose first
card is something else) throws `InvalidDataException` with `FITS: fisierul nu incepe cu
SIMPLE.`, from `LoadHeader` and from `LoadFull` alike. A header that begins with `SIMPLE` but
ends before `END` throws `InvalidDataException` with `FITS: header incomplet.` in both, where
the full load already did (as a plain `Exception`) and the metadata read returned an empty
dictionary. The messages carry no path; the callers that name the file already do
(`StructureRun.Compare`, the Sintetic prefill, the console's `eroare:` line). The owner chose
the full-load check too, on the review's finding, after the first fix had left it out.

## Alternatives rejected

- **Keep the empty header and document it.** The callers already caught an exception from
  `LoadInfo` for a raw libraw cannot open; a FITS it cannot read taking a different route, a
  count of zero, is the detour LightsCheck had to write, and the message for it has to be
  invented at every caller instead of coming from the reader that knows why.
- **Check `SIMPLE` on the metadata path only.** The first fix. It left the full load accepting
  a header without `SIMPLE` that the metadata read refused, so the window's Structure tab
  could run the analysis on a master and then fail on the reason card of the same file; and a
  header cut before `END` still gave the empty answer. Two checks that can drift are the
  fault the fix was meant to remove.
- **Refuse a truncated header in the metadata read with its own message.** The full load
  already says `header incomplet`; a second wording for the same defect would make the two
  reads disagree in words where they now agree in fact.

## Consequences

- The window's Sintetic tab says `preumplerea a esuat: FITS: fisierul nu incepe cu SIMPLE.` on
  a master that is not a FITS, where it used to leave every field empty without a word.
- The console names the real fault on a non-FITS given by mistake (`eroare: FITS: fisierul nu
  incepe cu SIMPLE.`, exit 1) instead of `header incomplet` on a short file or a parse error
  further in on a long one; R-004's "any other extension is parsed as FITS and fails as one"
  still holds, with a clearer first failure. No golden carried the old message.
- `FITS: header incomplet.` is an `InvalidDataException` now, not a plain `Exception`; every
  caller catches `Exception`, so none changes.
- LightsCheck can drop its detour and let the message come from the exception (its R-003).
- The guards are `R008_BothReadsRefuseAFileThatDoesNotBeginWithSimple` and
  `R008_BothReadsRefuseAHeaderCutBeforeEnd` in `ImageLoaderTests`; the row is R-008.
