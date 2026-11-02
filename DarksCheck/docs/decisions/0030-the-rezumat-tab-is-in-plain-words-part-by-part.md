# 0030. The Rezumat tab is in plain words, approved part by part

Date: 2026-11-02
Status: accepted 2026-11-02

## Context

ADR 0028 put every text the user reads in plain words, one tab at a time, starting with the ADU
scale tab. The owner picked the Rezumat tab next. It shows the report's rows, the same the
console prints: the session card, the `=== DARK-URI` line and its rows, then checks 2 to 9 and
INDEX, about forty-five rows with their variants. One list of every text would be too long to
read, so the owner approves it in three parts, each shown with the texts of a real run on the
Smida session and on the light-leak set: the session card, the header and checks 2 and 3; checks
4 to 8; check 9 and INDEX. The plan `docs/plans/2026-11-02-explain-the-darks-summary-tab-plainly.md`
records each part's list.

## Decision

1. **The rule of ADR 0028 holds**: what a figure means for the frames, in short sentences and
   everyday words; a term only with its meaning beside it; the figures as the result gives them.
2. **Part 1** (approved "Da, toate"): the words of the plan's first list.
   - The header line names what it shows: `cadre citite 20 din 20`, `valori de la 150 (negru) la
     4095 ADU (alb)`, `citite exact din raw` for a raw, the bits and where the white and the black
     came from for a FITS, `filtre de culoare RGGB`. The copied reader's `libraw (exact)` stays in
     the copy; `FrameStack` words it for the report.
   - The header's rows `normalizare` and `cuantizare` become `1 ADU-eq` and `pasul valorilor`, in
     the ADU tab's words; check 3's `amplitudine` becomes `pixeli fierbinti`.
   - The `±` of an error becomes `+/-`, which every console shows.
   - The labels `master dark`, `master bias` and `dark - bias` stay, since the window finds the
     maps' rows by them.
3. **R-082 guards it**, growing with each part: `R082_...` reads every variant the fixtures reach,
   in both languages, and fails on a word of the method.

## Alternatives rejected

- **The whole tab in one list.** About forty-five rows with their variants; the owner could not
  read it at one sitting.
- **Rewording the labels the window looks up** (`master dark`, `master bias`, `dark - bias`). The
  Harti tab finds its rows by them; renaming them breaks the window for no gain to the reader.

## Consequences

- The requirements that quote part 1's texts take the new words (R-003, R-006, R-007, R-020,
  R-021, R-023, R-025, R-028, R-029, R-041, R-071, R-072, R-076, R-095); R-082 is new.
- The `1 ADU-eq` row named a set at the reference settings whenever its factor was 1, which ISO
  800 at 60 s also gives; it now names the reference settings only at ISO 400 and 120 s, in the
  header and in the ADU tab alike (`DarkReport.AtReference`).
- Parts 2 and 3 add their lists to the plan and their words to R-082 before the merge.
