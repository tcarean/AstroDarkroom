# 0029. A Romanian count of twenty and up takes "de"

Date: 2026-11-02
Status: accepted 2026-11-02

## Context

Romanian puts `de` between a count and the word it counts from 20 up, unless the count's last two
digits are 01 to 19: `20 de cadre`, `101 cadre`, `120 de cadre`. DarksCheck's Romanian texts never
wrote it: `darks\ (20 cadre .NEF)`, `443 pixeli peste 20 ADU-eq`, `stack-ul celor 32 lights`,
`masterul dark, facut din 20 cadre`. The missing word showed while the ADU scale tab was rewritten
in plain words (ADR 0028). AstroPhotoAnalyzer writes the rule as `Fmt.RoCount`. On 2026-11-02 the
owner approved the changes of `docs/plans/2026-11-02-add-the-romanian-de-to-counts.md`, keeping the
word `lights` ("Da, aplica-le").

## Decision

1. **One rule, beside `T`.** `Ui.RoCount(n)` writes a count and a space, with `de ` after it when
   the count is not 0 and its last two digits are 00 or 20 to 99. `Ui.RoCount(figure)` does the
   same for a whole number already formatted (`F(x, 0)`) and leaves a figure with decimals, or any
   text that is not a whole number, without `de`.
2. **Every Romanian count goes through it**: `RunReport.Frames`, `DarkReport.Pixels` and the counts
   written inline in `Report/` and `Analysis/` (the frames skipped above the cap). The English
   wordings are unchanged.
3. **What takes none**: figures with decimals (`2.0 grade`, `de 1.5 ori`) and units (`120 s`,
   `65535 ADU`, `24 Mpx`), as the owner's list said.
4. **`lights` stays the counted word**, `32 de lights`, as the program names the folder and the
   frames everywhere.

## Alternatives rejected

- **A copy of AstroPhotoAnalyzer's `Fmt.cs`.** DarksCheck picks its wordings in `Ui`; the rule is
  four lines beside `T`, and a copied file would bring APA's other helpers with it.
- **The whole part of a decimal deciding**, as APA's string helper does (`20.5 de grade`). The
  owner's list kept decimals as they are, and DarksCheck's decimals are measurements, not counts.
- **`light-uri`.** The program says `lights` for the folder, the frames and the tab.

## Consequences

- R-095 is new, and the examples of R-002, R-003, R-023, R-027 and R-080 that quote a count of
  20 and up take `de`. `R095_ARomanianCountOfTwentyAndUpTakesDe` pins the rule's edges and a
  session of 20 darks in both languages.
- The three Romanian goldens that count 20 and more (`darks.txt`, `lights.txt`,
  `lights-only.txt`) were recaptured: each changed line gains a `de`, and one paragraph of the
  scale section wraps one word later. The English goldens did not change.
- A new Romanian text that counts calls `Ui.RoCount`.
