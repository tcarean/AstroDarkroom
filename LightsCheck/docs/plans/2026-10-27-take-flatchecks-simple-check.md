# Take FlatCheck's SIMPLE check into the FITS reader copy

Branch `claude/Take-FlatChecks-SIMPLE-check`, worktree
`LightsCheck/.claude/worktrees/Take-FlatChecks-SIMPLE-check` (from main afa12df). The owner
asked for it in chat on 2026-10-27 ("fa-l chiar aici") after the explanation below; that
explanation is the plan, no plan mode.

## Context

FlatCheck's ADR 0057 (its R-008) made both FITS reads refuse a file that does not begin with
the `SIMPLE` card (`FITS: the file does not begin with SIMPLE.`) and a header cut before `END`
(`FITS: incomplete header.`), with one `KeyOf` and one `RequireSimple` for the two reads.
LightsCheck's copy never took it: ADR 0018 translated the comments and took no behaviour
change, ADR 0021 took only the cut-data check and named this one as a task of its own. Today
the copy's metadata read answers a non-FITS with an empty header, and `FrameInfo.Read` guesses
the error from that emptiness (`not a FITS file (it does not begin with SIMPLE)`); a header
cut before `END` is accepted silently with the cards of the whole blocks before the cut; the
full load ends on a non-FITS with `incomplete header` or `no HDU with a 2D image found`.

## Steps, one commit

1. R-008 gains the two refusals and becomes `implemented` (its new tests carry `R008_`); R-003
   names the message.
2. Tests first, FlatCheck's two theories adapted (`FitsReaderTests`, `R008_`): both reads refuse
   an empty file, 4 bytes, 100 bytes of zeros and a block whose first card is `NAXIS` with the
   SIMPLE message; both refuse 100 bytes after `SIMPLE` and one block without `END` with the
   incomplete-header message; `FrameInfo.Read` carries the reader's message. Red before the code.
3. The hunk, token for token: `NotSimple`, `HeaderIncomplete`, `KeyOf`, `RequireSimple`, the two
   lines in `LoadHeader`, the one in `LoadFull`, the doc comments; the marker names it. The guess
   in `FrameInfo.Read` goes.
4. ADR 0027, dated notes on ADRs 0018 and 0021, `imaging.md`, `session-and-console.md`,
   CHANGELOG; the two inventory goldens change their error row (read whole).

## Verification

`scripts/verify.ps1` green; the new tests red before step 3, green after; no numeric change, so
no `/validate`.

## What deviated

- Four goldens changed, not two: the series-classes scenario also holds a 100-byte junk `.fit`
  (`DSC_0009.fit`), whose full load used to fail on the block length (`incomplete header`) and
  now fails on the magic first (`does not begin with SIMPLE`), as FlatCheck's R-008 example
  says; its two rows per language changed with the inventory's one. Six lines, all this change.
- The tests and the code were written in one pass and the suite was not run red in between;
  the three `R008_` tests assert messages and refusals no earlier code produced.
