# 0077. The Soft proof tab and its window are in plain words

Date: 2026-11-02
Status: accepted 2026-11-02

## Context

The seventh and last tab under the rule "Plain words" (ADR 0071). The Soft proof tab shows the
image through the paper's ICC profile; under it, the profile's facts (its name, the paper's
white and black) and the share of colours the paper cannot reproduce; above it, the three
intents, two boxes, the margin and the full-screen button, each with its help; the full-screen
window repeats the controls with a key hint. These texts were written in the method's words:
the profile's tables by their tags ("tabela lui perceptuala B2A0, tabelele lui colorimetrice
B2A1 si A2B1"), "albul mediului", "negrul dispozitivului prin tabela colorimetrica", "drumul
dus-intors prin profil", "gamut hartie", "intentul"; and the margin's help called the margin
"Fundalul", a word that names the sky (ADR 0013). On 2026-11-02 the owner approved 18 rewrites,
from an original / proposal list.

## Decision

Every text of the tab and of its full-screen window is in plain words, in both languages, with
every figure and threshold it carried before. R-163 carries the clause, checked by
`R163_TheSoftProofTextsAreInPlainWords` in both languages: the core's sentences and the texts
the three views write themselves, read from their XAML. In particular:

- the label before the three intents is "Mod de redare (intent):" ("Rendering (intent):"); the
  intents keep their names, Perceptual, Relativ and Relativ fara compensarea negrului;
- each intent's help says what it does to the colours, without the profile's table tags; the
  help of Relativ adds what happens to the colours the paper cannot reproduce, which is what
  sets it apart from Perceptual;
- the box is "Culorile pe care hartia nu le poate reda" ("Colours the paper cannot
  reproduce") and its legend "culori pe care hartia nu le poate reda (gamutul)", the words of
  the Editare Curbe tab (ADR 0076);
- the paper's white and black give L\* "din 100", and the white's line says that 0 for a\* and
  b\* means no tint;
- the profile's line gives the profile's name alone; the table tags stay only in the reasons a
  profile is refused, in brackets after what is missing;
- the margin's help says "Culoarea din jurul imaginii".

## Alternatives rejected

- **Renaming the tab and the intents.** "Soft proof", "Perceptual" and "Relative" are the names
  Lightroom and Photoshop show; a reader who meets them there should recognise them here. The
  help says what each does.
- **"De 8 ori" for the zoom limit.** The proof is a reduced copy, so eight times the copy's
  pixels is not eight times the image's; the notes keep 8:1 and say what it means.

## Consequences

- R-161, R-162 and R-163 say so; the README's soft proofing section names the two renamed
  controls.
- The reason given for a profile without a table towards the printer changed in
  `GamutAnalyzer` too, so the Editare Curbe tab's gamut note gives the same reason.
- Every tab of the window is now in plain words (ADRs 0071 to 0077). The map and preview
  windows, the parameter card with Appendix F of the report, and the README's prose are not
  tabs and remain.
