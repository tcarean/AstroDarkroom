# 0041. The Rezumat puts the lights' match second, every light is read, and the partner is not kept

Date: 2026-11-08
Status: accepted 2026-11-08; the console's order and numbering superseded by 0046
Supersedes: 0008 in part (the frame cap on the lights), 0009 in part (the partner in the settings file, the tab named `Lights`), 0036 in part (the partner's sentence), 0039 in part (the partner stays remembered)

## Context

The owner, after reading the window on real sessions (2026-11-08): the first question of a
dark set is whether it fits the lights, but check 9's card came eighth on the Rezumat; the
darks' and the lights' indexes sat in INDEX, far from it; the overlap rows of check 9, the
noise curve of check 8 and the stability of check 5 were one long line each, hard to read; the
three count rows of check 5 repeated what the Pixeli fierbinti tab shows; a session of 87
lights read 44, the rest `lights sarite`, since the frame cap of the darks (ADR 0007) also
applied to the lights (ADR 0008); the `pixeli comuni` row of the Index tab held two statements
in one line; the tab `Lights` did not say what it showed; and the partner, remembered from one
run of the program to the next (ADR 0009), attached a library to the next session opened,
whether or not the owner meant it.

## Decision

1. **The Rezumat's order and content are the window's arrangement** (R-071). The view-model
   puts the section whose title starts with `9.` first, right under the session card, keeps
   the console's order for the rest, and shows in each card only the rows the core marks
   `ReportRow.Summary`: check 5's three count rows are marked false and stay on the Pixeli
   fierbinti tab. The console keeps the hand analysis's order and numbering, so the goldens
   keep theirs, and the card keeps its `9.` title, since the console and the Potrivire cu
   lights tab say 9.
2. **The new content is the core's**, so the console and the window share it (the rule
   "Computed only in the core"): `ReportRow.Lines`, the statements of a row for the window to
   show one under the other (the stability's four, R-031; the shared pixels' two, R-063), which
   the console prints joined by `; `; `ReportRow.Table`, a row's figures as a small table with
   the console's bracket hint as its note (the overlap at each level, R-051; the noise against
   the frames, R-024), which the console does not print; and two rows of check 9 before its
   verdict, `index dark-uri` (`index partener` when the darks are the partner's) and
   `index lights` (R-056), the INDEX rows unchanged. The window draws a row's lines as one
   `TextBlock` each and its table as a three-column grid under the label.
3. **Every light is read** (R-050). The frame cap of R-027 bounds the darks and the biases,
   which are held in memory; a light is decoded, measured and dropped, so the cap bought only
   time (ADR 0008 put a light at about a second), and the owner wants the night's figures on
   every light. `LightFigures` has no skipped state, the lights table and `darkscheck-lights.csv`
   no `skipped` column; `SkippedReason` stays the darks' and the biases'.
4. **The partner is the lights folder's twin** (R-073, R-075). It is not stored in
   `settings.json`, which now holds the four settings alone; it holds for the open folder, so a
   rerun and the problem card's run keep it, and it is dropped when another folder is opened.
   The owner chose this on 2026-11-08 between keeping the partner across the folders of one run
   (recommended first: one library compared with every session) and dropping it like the lights
   folder of R-122.
5. **The tab is `Match with lights` / `Potrivire cu lights`** (R-071), since it holds check 9,
   the chart of every light against the dark and the lights table.

## Alternatives considered

- **Reordering the sections in the core.** The console would renumber the checks or print 9
  before 2; the numbers are the hand analysis's (`docs/validation/2026-08-12-smida/report.md`)
  and every golden and requirement names them. Rejected.
- **The console printing the tables too.** Its one-line rows are readable in a terminal, and
  every golden with lights would move on four rows; the window, which has the room, draws them.
  Rejected.
- **The window splitting a row's text on `; `.** The window would parse prose, which ADR 0009
  forbids, and `; ` occurs inside a statement (the RTS clause). Rejected: the core gives the
  lines.
- **A cap of the lights' own (`--max-lights`).** The owner asked for every light, and nothing
  else bounds the lights' loop (ADR 0008). Rejected.
- **The partner kept across the folders of one run.** ADR 0039's argument against remembering
  the lights folder holds for the partner too: a folder silently attached to the next session
  opened gives a verdict the owner did not mean. The owner chose the twin.

## Consequences

- The goldens move only where the task meant: the stability line's first `, ` becomes `; ` in
  the ten report goldens (the four statements are now joined alike), the two index rows appear
  in the six goldens with lights, and the two help goldens say the cap is a group's and every
  light is measured.
- V-007 and V-012 (the Veil session, 87 lights against the library) re-measured on every
  light, 2026-11-08: 87 lights used where 44 were, 70 needed for persistence where 36 were,
  237 persistent pixels (236), 226 / 225 / 212 / 117 of them above 10 / 20 / 50 / 100 ADU-eq
  in the master (225 / 224 / 214 / 117), the misses (12 at 3 ADU-eq), the night's ratio 0.43
  (0.41 at the first light, 0.37 at the last, on 3933 pixels), the label `idx29` and the
  verdict unchanged. The figures are recorded at the owner's word (`docs/validation.md`,
  History 2026-11-08).
- A `settings.json` written before this change keeps its `partner` key until the next save;
  the deserializer ignores it.
- The Rezumat's check 5 card shows two rows; the Pixeli fierbinti tab shows all five, the
  stability as four lines there too.
