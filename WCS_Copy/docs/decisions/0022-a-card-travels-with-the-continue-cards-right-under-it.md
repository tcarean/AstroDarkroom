# 0022. A card travels with the CONTINUE cards right under it

Date: 2026-11-18
Status: accepted 2026-11-18

## Context

The FITS standard writes a string longer than the 68 characters a card holds as a first card
whose string ends in `&` and `CONTINUE` cards that carry it on. The copy took one card per key
(`GetCard`, `SetCard`): the review of 2026-11-16 showed (CR-005) a source's long `TELESCOP`
arriving as its first card alone, a value cut with a stray `&`, and a destination's long
`TELESCOP` replaced by a short one keeping its old `CONTINUE  'old tail'`, which astropy reads as
the new value with the old tail glued on. The Python prototype copied the whole string through
astropy. R-010 asks for the source's bytes, verbatim. Shown before the fix by the R-010 tests:
the `CONTINUE` card was missing under the copied `OBJECT`, and the old one stayed under the new
`TELESCOP`.

## Decision

A value is a card and every `CONTINUE` card right under it, up to the first card of another
keyword. The copy takes a key's value whole, verbatim and in order, replaces the destination's
value of that key where it stands, its `CONTINUE` cards with it, and drops a key with its
`CONTINUE` cards (R-010). The outcome lists every card, so the dry run and the window show the
`CONTINUE` cards under their card; its counts stay counts of keys (R-009).

## Alternatives rejected

- **Take only the `CONTINUE` cards after a string that ends in `&`.** A `CONTINUE` card under a
  short value, as CR-005's second case leaves, would stay, and astropy reads it into the value
  above it anyway.
- **Rebuild the long string.** It reparses and reformats the value, which R-010 forbids; the
  source's own split and comments are what the copy keeps.

## Consequences

`FitsHeader` has `ValueCards` and `SetValueCards` beside `GetCard`, which stays the first card
alone for the value readers; `SetCard`, `RemoveCards` and `CardsOf` take the `CONTINUE` cards
with each card. The rule "A card is copied verbatim" in `CLAUDE.md` names the new calls. The log's
`card` line joins a value's cards with ` + `. The outcome's `Cards` may hold more cards than keys,
so the heading and the closing line count `present`, the keys. Siril's two headers in the
fixtures hold no `CONTINUE` card, so V-001 and V-002 do not move. Guarded by the `R010_` and
`R009_` tests in `WcsCopyTests`, `FitsHeaderTests` and `CopyWcsCliTests`, and the log's joined
line in `LoggingTests`.
