# 0037. A Romanian count of twenty and up takes "de"

Date: 2026-11-02
Status: accepted 2026-11-02

## Context

Romanian puts `de` between a count and the word it counts from 20 up, unless the count's last two
digits are 01 to 19: `20 de cadre`, `101 cadre`, `120 de cadre`. LightsCheck's Romanian texts never
wrote it: on the Veil session `lights\ (87 cadre .NEF)`, `din 87 cadre`, `22 cadre slabe`, `din 39
perechi de cadre`, the dithering table's `22 ori`, and `dara pe RA (-90 grade)` on every trailed
frame. DarksCheck wrote the rule the same day (its ADR 0029, `Ui.RoCount`), as AstroPhotoAnalyzer
does with `Fmt.RoCount`. After the plain-words tabs (ADRs 0032 to 0036) the owner asked for it
here and approved the list of changes in chat on 2026-11-02 ("Da, aplica-le"), V-010's recorded
word `22 ori` included.

## Decision

1. **One rule, beside `T`.** `Ui.RoCount(n)` writes a count and a space, with `de ` after it when
   the count is not 0 and its last two digits are 00 or 20 to 99; `Ui.RoCount(figure)` does the
   same for a whole number already formatted and leaves a figure with decimals, or any text that
   is not a whole number, without `de`. It is LightsCheck's own, four lines as DarksCheck's.
2. **Every Romanian count goes through it**: the helpers that count (`SeriesReport.Count`,
   `FramesWord`, `TimesWord`, `PairsWord`, `SharingPairsWords`) and the counts written inline in
   `Report/` and `Runs/`: the inventory's frames, the trailed frames' classes (`22 de slabe`,
   `48 de bune`), the darks, the hot pixels, the degrees of a trail, a cut-out's star and the
   trail direction, and the stars of `--frame`. The English is unchanged.
3. **What takes none**: figures with decimals (`1.3 ori`, `45.2 grade`) and the units (`px`, `s`,
   `min`, `h`, `%`, `ADU`), as DarksCheck's ADR 0029 decided.

## Alternatives rejected

- **A copy of DarksCheck's `Ui.cs` or APA's `Fmt.cs`.** Each project picks its wordings in its own
  `Ui`; the rule is four lines, and a copied file would bring the other helpers with it.
- **Only the counts the owner's list showed.** The helpers serve every caller; a count left out
  would read wrong the day it reaches 20.

## Consequences

- R-082 is new, and the Romanian quotes of the requirements with a count of 20 and up take `de`.
  `R082_ARomanianCountOfTwentyAndUpTakesDe` pins the rule's edges and a 24-frame session in both
  languages.
- The Romanian goldens that count 20 and more change on those lines only; the English goldens do
  not.
- V-010's cell `22 ori` reads `22 de ori`, its figure unchanged (`docs/validation.md`).
- A new Romanian text that counts calls `Ui.RoCount`.
