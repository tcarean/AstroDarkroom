# 0013. A folder already named with a label loses its old suffix before the new one

Date: 2026-10-20
Status: accepted 2026-10-20

## Context

The label (R-061, ADR 0002) exists to be put in the folder's name: the tool shows it, the owner
renames the folder. On 2026-10-20 the owner did exactly that to all three libraries under
`Flats and Darks/Darks` (`2026-10-10-ISO1600-120s-15.8celsius-idx67`,
`2026-10-11-ISO1600-120s-13.3celsius-idx47`, `2026-10-11-ISO1600-120s-9.5celsius-idx33`). R-061
as written appended the tag to whatever the folder was called, so the first library then read
`2026-10-10-ISO1600-120s-15.8celsius-idx67-idx67`, and the validation cases V-005, V-006 and
V-007, whose expected label stayed the single-suffix one, failed on the label alone
(`docs/validation.md`, History 2026-10-20). Two readings were possible, so the owner was asked
(the kit's ADR 0030): strip an existing suffix (recommended), or leave R-061 and keep the folders
unrenamed. The owner answered "A", strip.

## Decision

1. Before the tag is appended, one trailing tag of an earlier label is cut from the folder's
   name: `-idx` followed by at least one ASCII digit, or `-idx-`, the tag of a master with no
   unsaturated pixel (R-060), whatever the case of `idx` (Windows folder names are
   case-insensitive and the `darks` subfolder is already matched that way). Only the end of the
   name is looked at: `night-idx67-b`, `night-idx` and `night-idx-60` are kept whole, because
   there the `-idx` is not a tag the tool itself wrote. A name that is nothing but a tag
   (`-idx67`) is kept whole too, so a name always stands in front of the new tag. One tag, not
   every one: a name that ends in `-idx60-idx67` is stripped to `...-idx60`, then `-idx67`
   appended; such a name can only come from the doubled label of the old rule, and the owner
   renames it once. The rule is one anchored pattern, `(?<=.)-idx(?:[0-9]+|-)$`, ignoring
   case, generated at compile time.
2. The expected labels of V-005, V-006 and V-007 are not changed: the rule restores them. The
   same renamed library and the same index give the label the folder already carries, which is
   the point: a renamed folder keeps a stable label, and a drift of the index (another
   measurement, a changed formula) shows as a changed number, `...-idx65`, never as a second
   suffix.

## Alternatives rejected

- **Leave R-061 as it was and keep the folders unrenamed.** It contradicts ADR 0002's purpose
  for the label and the owner's practice of the same day; the validation cases would need new,
  doubled expected labels that encode a quirk rather than a figure.
- **Strip every `-idx<digits>` anywhere in the name.** It would eat a date-like or note-like
  token the owner wrote by hand; the tool only ever appends at the end, so only the end is its.
- **Strip only `-idx` and digits, not `-idx-`.** The first version did that; the code review
  of 2026-10-20 noted that a folder renamed on the unmeasured tag would then read
  `name-idx--idx-` on the next run, the same doubling the rule is meant to remove, so the
  unmeasured tag is a tag like any other.
- **Hand-written scan of the name's tail.** The first version walked back over the digits and
  compared the marker in twelve lines; the review preferred the one-line pattern, which states
  the rule itself and lets the source generator check it at compile time.

## Consequences

- R-061 amended; `IndexAnalyzer.Label` calls `WithoutLabelSuffix`, covered by the R061 theory
  on ten names and a fact on the unmeasured tag; `docs/design/lights-and-index.md` describes
  the cut.
- V-005, V-006 and V-007 pass again on the renamed library without a change to
  `docs/validation.json` (History 2026-10-20 in `docs/validation.md`).
- The window's `Copiaza eticheta` button copies the stripped label, so the owner can rename a
  library again after a re-measurement without editing the name by hand.
