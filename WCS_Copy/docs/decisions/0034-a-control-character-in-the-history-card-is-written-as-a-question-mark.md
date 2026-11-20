# 0034. A control character in the HISTORY card is written as a question mark

Date: 2026-11-19
Status: accepted 2026-11-19
Supersedes: 0010 in part (a control character kept in the HISTORY card)

## Context

The HISTORY card is the one card the tool composes, `HISTORY WCS copied from <source file
name>`, and ADR 0010 made it ASCII through `AsciiText.Of`: a letter with a diacritic becomes its
base letter, any other character outside ASCII one `?`. Everything below 0x80 was kept as it was,
control characters included. A FITS header may hold the bytes 0x20 to 0x7E only, and R-011 and
ADR 0010 mean the tool's own card to be a standard one. CR-022 of the review of 2026-11-16 showed
`a`, a tab, `b`, 0x7F, `c` reaching the card as the bytes `61 09 62 7F 63`. Windows' naming
rules refuse the characters 0x01 to 0x1F in a file name, its documentation says, but allow
0x7F: a test of 2026-11-19 created `Veil` + 0x7F + `1.fit` and copied from it, and the byte
reached the card. A read-only scan of
the owner's 5,206 FITS files under `E:\Photografie` that day found no keyword with a control
character; how many file names hold one was not counted.

## Decision

Every control character of the text, below 0x20 or 0x7F, becomes one `?` in the card, as a
character with no ASCII form does; the space stays (R-011). `AsciiText.Of` keeps only printable
ASCII, 0x20 to 0x7E, as it is, and returns the same instance when the text is all printable.
`HISTORY WCS copied from Veil?1.fit` for the file above. The owner chose this on 2026-11-19,
shown the card before and after, the recommended option.

## Alternatives rejected

- **Drop the control character.** The card would read as if the name had none, and two names
  that differ by one would give the same card.
- **A space in its place.** It reads as a space the name holds.

## Consequences

The copy never puts a byte outside 0x20 to 0x7E in a card it composes; the cards it copies stay
byte for byte as they are (R-010). The table test `AsciiTextTests` starts at 0x80 and does not
move. Guarded by `FitsHeaderTests.R011_AddHistory_AControlCharacter_IsOneQuestionMark` and
`CopyWcsCliTests.R011_Run_ASourceNamedWith0x7F_WritesAQuestionMarkInHistory`.
