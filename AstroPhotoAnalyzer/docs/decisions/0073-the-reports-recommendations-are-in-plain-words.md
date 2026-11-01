# 0073. The report's recommendations are in plain words

Date: 2026-11-01
Status: accepted 2026-11-01

## Context

The third tab under the rule "Plain words" (ADR 0071). The Raport tab shows the overall verdict
with a caption, one section per topic with its title, and every recommendation with its
"fiindca" line; under a check box, the whole text report with its appendices A to F. The
recommendations are composed once in the core (`Recommendations.cs`, `VerdictText`) and printed
by both applications, so every change here is the console's too. They were written in the
method's words: "Re-stretch din liniar", LSB, "sigma median", Q1 / Q3, "dale netede", "palier",
"dither-eaza cuantizarea", "gradient", "aplatizare", "pragul de vizibilitate", "esantioane",
"octet_inalt". On 2026-11-01 the owner approved 31 rewrites for the part the tab shows directly,
from an original / proposal list; the appendices of the text report are the second part.

## Decision

Every recommendation and its "because", the section titles and the verdict's caption are in
plain words, in both languages, with every figure and threshold they carried before (the rule
"Every recommendation carries its evidence"). R-112 carries the clause, and
`R112_TheReportTextsAreInPlainWords` checks nine scenes in both languages against the method's
words. In particular:

- the noise is given in levels ("niveluri"), which an 8-bit LSB is, with "sigma" kept as the
  term in brackets;
- the fix reads "Refa stretch-ul din datele liniare pana la fundalul X (in simulare: ...)";
- section 3 is "Culoarea fundalului si marginile cadrului";
- the background's "fiindca" gives each L\* before the definition it belongs to.

Four details go beyond the approved list, each to keep a sentence true on every image:

- "(lista lor e in Anexa B)" is said only when the catalogue named clouds, with a WCS; without
  them Appendix B has no list, and the sentence says "(vezi Anexa B)".
- "se vede ca punct abia de la X m" keeps "abia" only when the stars are discs at that size;
  where the size passes, "abia" would say the opposite.
- With a given distance the "fiindca" gives the point distance without "abia", for the same
  reason.
- A Romanian count takes "de" where the language does, through `Fmt.RoCount` ("64 de stele",
  "16 stele", "419 zone"). The list wrote "64 de stele" and "~167 de stele"; a fixed "de" would
  be wrong under 20.

Two figures that were typed are now read, with the same output: the eye's 0.29 mm at 1 m
(`Thresholds.AcuityMmAt1m`) and the 1.5 x diagonal (`Thresholds.ViewingDistanceFactor`).

The goldens `dark-sky`, `dark-sky-wcs`, `clean-sky-canvas` and `mono-png8`, in both languages,
were recaptured. Counting every number in them before and after, the only ones gone are the
digits of labels that went (Q1, Q3, "(R+B)/2"). The README's excerpt of the report on
`de_printat.png` was taken again from a run of this branch; its figures are unchanged.

## Alternatives rejected

- **Keeping the method's words in the "fiindca" lines, as the expert's evidence.** The evidence
  is the figure and its threshold, and both stay; the method's name for them adds nothing a
  reader can act on.
- **A fixed "de" in the Romanian counts, as the list wrote it.** It reads wrong for every count
  under 20 ("16 de stele").
- **Promising a list of clouds in Appendix B on every image.** Without a WCS there is none.

## Consequences

- R-090 to R-096 and R-112 quote the new sentences. R-092's example had a slip from ADR 0072's
  task ("praful ramane deasupra negrul hartiei de la Dmax 2.06", the "in sus" lost); it now
  quotes the sentence as the program writes it.
- The tests that quoted the old sentences name the new ones, with the same figures.
- Not changed here: the appendices of the text report and the reader's notes (the next part of
  this tab), and the Editare Curbe tab's evidence, which still writes LSB (its own task).
