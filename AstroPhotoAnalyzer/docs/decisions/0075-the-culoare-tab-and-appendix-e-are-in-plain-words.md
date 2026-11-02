# 0075. The Culoare tab and Appendix E are in plain words

Date: 2026-11-02
Status: accepted 2026-11-02

## Context

The fifth tab under the rule "Plain words" (ADR 0071). The Culoare tab shows the sky's colour
with three figures for its tint, each with a description, a table of the four corners and four
edges against the sky, and a five-paragraph guide; Appendix E of the text report prints the same
figures. They were written in the method's words: "mediana", "sqrt(a*^2 + b*^2)", "delta",
"abatere", "regiune", "gradient", "stretch-uita", "Pe mono". Two of them sent the reader to "tabul
Fundal", a tab that does not exist (ADR 0013: no tab is called just Fundal; the sky's classes are
the Tonuri tab's). On 2026-11-02 the owner approved 13 rewrites, from an original / proposal
list.

## Decision

Every text of the tab and every line of Appendix E is in plain words, in both languages, with
every figure and threshold it carried before. R-116 carries the clause and is now implemented,
checked by `R116_TheColourTextsAreInPlainWords` in both languages on the dark scene and the mono
sky, the appendix included. In particular:

- the chroma row is "Cat de departe e de un gri (croma; a*, b*)", the two level figures "Verde
  fata de rosu si albastru (G - (R+B)/2)" and "Rosu fata de albastru (R - B)": the plain name
  first, the term in brackets;
- a zone's difference is "fata de cer" in the table and in the appendix, and a zone measured on
  every pixel says "(doar 14 % cer: masurat pe toti pixelii)" (`EdgeRegion.PixelsNote`, shared by
  the two);
- the guide's leads are "Zonele.", "Fata de cer.", "Semnul !.", "Colorat sau neutru.", "La
  alb-negru";
- the texts name the Tonuri tab where they named a Fundal tab.

The goldens `dark-sky`, `dark-sky-wcs`, `clean-sky-canvas` and `mono-png8`, in both languages,
were recaptured for Appendix E alone; their measured figures are unchanged.

## Alternatives rejected

- **Keeping "delta" as the column's word.** It is the method's name for a difference; "fata de
  cer" says what the figures are measured against, in the same width.
- **Dropping the reference image's figures from the guide.** They show the one case that matters,
  the corner the Milky Way enters, which a reader cannot reproduce on a synthetic example.

## Consequences

- R-061, R-112 and R-116 say so; the design note `window.md` names the guide's paragraphs anew.
- The guide still quotes the reference image's figures as typed text, as before this ADR; they
  are the owner's measured values, not thresholds.
- Appendix F remains, with the parameter card.
