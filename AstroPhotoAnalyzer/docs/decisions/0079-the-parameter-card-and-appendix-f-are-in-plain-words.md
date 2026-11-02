# 0079. The parameter card and Appendix F are in plain words

Date: 2026-11-02
Status: accepted 2026-11-02

## Context

After the seven tabs and the two windows the Tonuri tab opens (ADRs 0071 to 0078), the
parameter card on the left of the window, with the help each field shows under the mouse, the
messages of a field typed wrong, and Appendix F of the text report, which prints the same
parameters. They were written in the method's words: "Mediu de print", "Latimi candidate",
"Raport de aspect tinta", "grila", "mediana canalului verde", "gamma = ln(tinta) / ln(fundal)",
"e in afara intervalului (0.0, 100.0]", "strict intre 0 si 1", "(explicit)", "naturala",
"samanta". The distance's help also typed its numbers (0.29 mm, 180 dpi, 1.5) where the rule
"Explanation is not diagnosis" wants them from `Thresholds`. On 2026-11-02 the owner approved 19
rewrites, from an original / proposal list.

## Decision

Every text of the card and every line of Appendix F is in plain words, in both languages, with
every figure and threshold it carried before. R-026 carries the clause, checked by
`R026_TheCardTextsAreInPlainWords` in both languages: the help texts, the profile line, the
messages of seven bad fields and the core's aspect check, the card's labels read from the main
window's XAML, and Appendix F with every field empty and with each one given. In particular:

- the medium is "Pe ce printezi" ("What you print on"), and Appendix F says "printat pe
  baryta"; the Dmax is "Cat de negru poate tipari (Dmax; il poti schimba)";
- the help texts say what a field changes for the print; the distance's takes its numbers from
  `AcuityMmAt1m`, `FallbackMinDpi` and `ViewingDistanceFactor`, and says why 1.5 in the words
  of that threshold's comment, in place of the framers' 1-2 x rule and the 37 degrees;
- a number outside its limits "trebuie sa fie mai mare decat 0.0 si cel mult 100.0" (or "intre
  0.0 si 100.0" where 0 is allowed); the target background "mai mare decat 0 si mai mic decat
  1", in the core's message the console prints too;
- Appendix F marks a given Dmax "(dat)", the mark the simulation table already gives a given
  target, writes the natural distance as "1.5 x diagonala fiecarei marimi" and names the seed
  as "numarul de pornire al alegerii dalelor (seed)".

The goldens `clean-sky-canvas`, `dark-sky`, `dark-sky-wcs` and `mono-png8`, in both languages,
were recaptured on their two Appendix F lines alone; their measured figures are unchanged, and
the one new number is the 1.5 that now names the natural distance.

## Alternatives rejected

- **Keeping "Mediu de print".** "Mediu" is the printers' word; a reader who has never sent a
  print to a lab does not know it means the paper or the canvas.
- **Keeping the formula of the gamma in the help.** It explains how, not what the field changes;
  the Tonuri tab already says the simulation is a simple curve.
- **Lifting the passepartout's 10-15 % and the frame's 3-4 cm into `Thresholds` now.** Those
  two help texts were not rewritten, being plain already; their typed numbers stay a known gap
  of the rule "Explanation is not diagnosis", for a task of their own.

## Consequences

- R-020, R-023, R-024, R-026, R-027 and R-155 quote the new texts; the README names the
  renamed fields; `docs/design/console.md` names the window's fields anew.
- The console's `--help` still uses some of the method's words; it is the console's own text,
  not the card's, and remains with the README's prose.
