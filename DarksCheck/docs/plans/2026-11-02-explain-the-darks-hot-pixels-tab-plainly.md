# Explain the Pixeli fierbinti tab in plain words

Plan for the task branch `claude/Explain-the-darks-Hot-pixels-tab-plainly`, worktree
`.claude/worktrees/Explain-the-darks-Hot-pixels-tab-plainly`, from `main` at 74b34f0c. Written on
2026-11-02. The owner picked the Pixeli fierbinti tab after Harti (ADR 0032) and approved its four
texts in one list ("Da, toate"). The version is not changed.

## Context

The Pixeli fierbinti tab shows check 5's rows, already in plain words (ADR 0030), with a text under
them, and the noise curve of check 8 with a title, a sentence and a hint on each point. Those four
live in the window: `WindowTexts.HotTabLead`, the two texts of `HotPixelsView.xaml` and the tips
`MainViewModel` gives `NoisePoints`.

## The list, approved as proposed ("Da, toate", 2026-11-02)

| # | Text | Before | After |
| --- | --- | --- | --- |
| 1 | under the rows | `Pixelii fierbinti ai masterului median la cinci niveluri, pe planuri la 20 ADU-eq, cu amplitudinile lor si cat de stabili sunt de la un cadru la altul; candidatii RTS sunt pixelii a caror valoare sare intre cadre. Informativ: cati are senzorul, nu un verdict.` | `Pixelii fierbinti din master: cati sunt peste fiecare din cele cinci praguri (de la 10 la 500 ADU-eq), cati sunt peste 20 pe fiecare culoare, cat de fierbinti sunt, daca se vad in toate cadrele si cati sar intre doua niveluri de la un cadru la altul (RTS). Doar de informare: verificarea asta nu da niciodata avertizare.` |
| 2 | the chart's title | `8. ZGOMOTUL MEDIEI A n CADRE (ADU)` | `8. CAT SCADE ZGOMOTUL CU MAI MULTE CADRE (ADU)` |
| 3 | the chart's sentence | `Punctele: zgomotul masurat al mediei a n cadre; linia punctata: sigma / sqrt(n), cat ar da cadre independente. Punctul n = 1 e sigma temporala, cadru fata de cadru, fara modelul fix. Tine mouse-ul pe un punct ca sa vezi cifrele.` | `Fiecare punct arata cat zgomot ramane dupa media mai multor cadre; din cate cadre, scrie jos, sub grafic. Linia punctata arata cat ar trebui sa ramana: zgomotul unui cadru, care scade cum scade un zgomot la intamplare (de 4 ori mai multe cadre, zgomot de 2 ori mai mic). Punctele se masoara intre doua grupuri diferite de cadre, deci fara ce e la fel in toate cadrele; de aceea pot sta putin sub linie. Tine mouse-ul pe un punct ca sa vezi cifrele.` |
| 4 | a point's hint | `n=1: 1.426 masurat, 1.510 asteptat` | `1 cadru: masurat 1.426, se astepta 1.510` |

The English says the same, with the same figures. The list's 1.510 was an example: Smida's first
point expects 1.514, the median noise of one of its frames (`docs/validation.md`).

## Steps

1. Requirements: R-085, new, names the texts and the words they must not use.
2. Tests: `R085_...` renders the tab in both languages with the word list of R-083 and R-084,
   which takes `temporal`, `candidat...`, `planuri` / `planes` and `n=`, and checks each point's
   hint.
3. Code: `WindowTexts.cs`, `HotPixelsView.xaml`, `MainViewModel.cs`; no core text and no figure
   changes, so no golden changes and no `/validate`.
4. Gate, ADR 0033, CHANGELOG, README, the design note, commit, the review offer, the merge
   proposal.

## What deviated

- The list's hint showed 1.510, an example; the requirement, the ADR and the CHANGELOG quote
  Smida's 1.514 (`docs/validation.md`), and the owner is told.
- `docs/design/window.md` said that `LanguageWindowTests` look for the method's words on the Cadre
  tab only, as the Harti task had left it; it now names the three tabs.
- The new chart titles are about 300 px wide in the heading's font (Segoe UI, 12 px, semibold),
  measured against the narrowest right card, about 415 px at the window's 960 px minimum: they
  stay on one line.
