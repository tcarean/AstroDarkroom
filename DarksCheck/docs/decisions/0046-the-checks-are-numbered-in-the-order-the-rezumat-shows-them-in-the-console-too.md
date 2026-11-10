# 0046. The checks are numbered in the order the Rezumat shows them, in the console too

Date: 2026-11-10
Status: accepted 2026-11-10
Supersedes: 0041 in part (the console keeps the hand analysis's order and numbering, the view-model moves check 9 first)

## Context

ADR 0041 put the check of the lights against the dark right under the session card of the
Rezumat, since it is the first question of a dark set, and kept its title `9.`: the console
kept the hand analysis's order and numbers. The owner, reading the window on 2026-11-10, found
the Rezumat's cards numbered 9, 2, 3 ... 8 and asked for numbers that follow the order shown,
the same on the tabs that show a check. Asked where the new numbers apply, the owner chose the
console and the window together over the window alone (the Raport tab, the console's text,
would otherwise disagree with the Rezumat beside it), and the session card left unnumbered as
check 1, then 2 to 9, over `1. SESIUNE` and over 1 to 8.

## Decision

1. **One numbering, the Rezumat's order.** Check 1 is the session's lines (the frames, their
   settings, the `potrivire` row); then 2, how well the dark fits the lights; 3, light leaks
   and differences across the frame; 4, the camera's warming; 5, the noise of each frame; 6,
   hot pixels; 7, the fixed pattern; 8, dead and saturated pixels; 9, how many darks are
   enough; then INDEX. The old numbers move as 9 to 2 and 2 .. 8 to 3 .. 9.
2. **The core orders and numbers the sections** (the rule "Computed only in the core"):
   `DarkReport.Sections` returns them in that order with the new titles, and `DarkReport.Write`
   prints them so; the view-model shows them as they come. The Pixeli fierbinti tab's headings
   read `6.` and `9.`, the Potrivire cu lights tab's `2.`, and the hints that name a check
   (`(verificarea 4)` for the frames to discard, `(verificarea 3)` and `(verificarea 5)` on the
   Cadre tab) say the new numbers.
3. **A section carries its check**: `ReportSection.Check`, a `ReportCheck`. The window and the
   tests find a section by it, never by the number in its title, so a later renumbering touches
   the titles alone.
4. **The living documents say the new numbers**: `README.md`, `CHANGELOG.md` (all of it under
   `[Unreleased]`), `docs/requirements.md`, `docs/design/`, `docs/validation.md` above its
   History, `CLAUDE.md`, the code's comments and the tests' names. The history keeps the
   numbers it was written with: the ADRs before this one, `docs/plans/`, the History of
   `docs/validation.md`, `prototype/` and the Smida report of the hand analysis
   (`docs/validation/2026-08-12-smida/`). Read there, old 2 .. 8 are new 3 .. 9 and old 9 is
   new 2.

## Alternatives considered

- **The window alone renumbers.** The Raport tab and the console would say 9 for what the
  Rezumat calls 2. Rejected by the owner.
- **`1. SESIUNE` on the session card**, or **1 to 8** after it. The console already counts the
  session's lines as check 1; the owner kept it unnumbered, then 2 to 9.
- **Living documents keep the hand analysis's numbers, with a table.** Two numberings at once,
  one of them never shown to the owner, who speaks of the checks by what they see. Rejected.

## Consequences

- Ten golden texts move (all but the help and the inventory ones): check 2's section is printed
  right under the darks' header and every title is renumbered, and the frames table's note says
  `(verificarea 4)` / `(check 4)`; a scratchpad script checked each one equal, byte for byte, to
  its old text with only those moves made. No figure and no verdict word changes, so no
  validation case moves.
- The plans, the memory and old chats speak of the old numbers: check 9 there is check 2 here.
