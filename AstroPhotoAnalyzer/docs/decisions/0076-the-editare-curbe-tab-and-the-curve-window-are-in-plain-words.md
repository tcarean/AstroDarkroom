# 0076. The Editare Curbe tab and the curve window are in plain words

Date: 2026-11-02
Status: accepted 2026-11-02

## Context

The sixth tab under the rule "Plain words" (ADR 0071). The Editare Curbe tab shows a gallery of
suggested contrast curves; under each, its evidence (the curve's points, the L\* of the five
kinds of patches before and after, how much the noise of the sky grows, the burnt-out pixels)
and, for print, the paper checks and the share of colours the paper cannot reproduce; when no
curve is suggested, the reason, in red. The curve window shows the points' table and the curve,
and a save writes a FITS with `HISTORY` lines. These texts were written in the method's words:
"ancora", "percentila", "mediana", "LSB", "panta curbei la cer", "clasa", "clase relative la
cerul modelat", "pragul de vizibilitate", "plafon", "crescatoare", "codarea", "gamutul
hartiei", "puncte de control". On 2026-11-02 the owner approved 20 rewrites, from an original /
proposal list; one of them is the sentence V-012 records, and approving it approved the change
of that recorded figure.

## Decision

Every text of the tab, of the curve window and of the saved FITS's `HISTORY` lines is in plain
words, in both languages, with every figure and threshold it carried before. R-146 carries the
clause, checked by `R146_TheCurvesTextsAreInPlainWords` in both languages on five scenes, the
messages of every refusal included. In particular:

- the anchors are "punctele curbei" and the classes are "felurile de dale" of the Tonuri tab;
  where the points come from says "la pragul sub care stau 80 % din dalele ei" for the
  percentile and "la valoarea din mijloc" for the median;
- the curve's slope at the sky is "cat de tare urca curba in dreptul cerului", and the noise is
  counted in "niveluri", with "(sigma)" where the figure is the noise itself;
- the gamut is "culorile pe care le poate reda hartia (gamutul)": the plain name first, the term
  in brackets;
- the peaks over the target read "punctul "varfuri" e deja la 0.971, la sau peste 0.92, unde
  ajung varfurile dupa o curba", ending "imaginea e deja cat de luminoasa poate fi";
- the spline is "o curba lina (spline cubic natural)"; the `HISTORY` text gives the kind of
  curve and the Siril tool on two cards.

V-012 records the new sentence of the Orion frame; its figures, 0.971 and 0.92, are unchanged,
and so is every other recorded figure (the run of 2026-11-02 in `docs/validation.md`).

## Alternatives rejected

- **The kind of curve and the Siril tool on one `HISTORY` card.** Joined, they take 85
  characters in Romanian and 92 in English, over the 72 a card holds; the writer would cut them.
- **Keeping LSB for the noise.** The Raport and Export tabs already count it in levels (ADRs 0073
  and 0074); the curves' evidence would be the one place left with the method's unit.

## Consequences

- R-142, R-145 and R-146 say so; `docs/validation.md` records the change of V-012's sentence.
- The Soft proof tab remains, the last one.
