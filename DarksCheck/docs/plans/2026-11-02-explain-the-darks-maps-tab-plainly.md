# Explain the Harti tab in plain words

Plan for the task branch `claude/Explain-the-darks-Maps-tab-plainly`, worktree
`.claude/worktrees/Explain-the-darks-Maps-tab-plainly`, from `main` at 954162f6. Written on
2026-11-02. The owner picked the Harti tab after Cadre (ADR 0031) and approved its three texts in
one list ("Da, toate"). The version is not changed.

## Context

The Harti tab shows the three maps of check 2 (the dark master, the bias master, dark minus bias)
as the PNG of `--out` holds them, each with a title and the Rezumat's row of the map under it.
Those rows are ADR 0030's, already in plain words. The tab's own texts live in the window:
`WindowTexts.MapsLead`, `WindowTexts.NoMap` and the titles `MainViewModel` gives the maps.

## The list, approved as proposed ("Da, toate", 2026-11-02)

| # | Text | Before | After |
| --- | --- | --- | --- |
| 1 | the lead | `Hartile planului de referinta in blocuri de 32 px, fiecare intinsa intre percentilele 1 si 99 ale ei: masterul dark (media cadrelor), masterul bias si diferenta lor, curentul de intuneric. O margine sau un colt mai luminos pe dark - bias e o scurgere de lumina sau glow; unul care apare si in bias e citire, nu lumina.` | `Cele trei harti arata cadrul in patrate de 32 px: masterul dark (media cadrelor), masterul bias si diferenta lor, adica ce aduna senzorul din caldura lui (curentul de intuneric). Fiecare harta are contrastul ei: cel mai intunecat 1 % din patrate e negru, cel mai luminos 1 % e alb, ca sa se vada si diferentele mici. O margine sau un colt mai luminos pe dark minus bias e lumina intrata in aparat sau glow; unul care apare si in bias vine din citirea senzorului, nu e lumina.` |
| 2 | the third map's title | `DARK - BIAS` | `DARK MINUS BIAS`; `MASTER DARK` and `MASTER BIAS` stay |
| 3 | a missing map | `Harta nu are blocuri (cadre mai mici de 32 px) sau grupul lipseste.` | `Nu e harta: cadrele sunt mai mici de 32 px, sau sesiunea nu are bias-uri.` |

The English says the same, with the same figures.

## Steps

1. Requirements: R-084, new, names the texts and the words they must not use.
2. Tests: `MainViewModelTests` takes the new title; `R084_...` renders the tab in both languages
   with the word list of R-083, which takes `percentila` and `planul... de referinta`.
3. Code: `WindowTexts.cs`, `MainViewModel.cs`; no core text and no figure changes, so no golden
   changes.
4. Gate, ADR 0032, CHANGELOG, README, commit, the review offer, the merge proposal.

## What deviated

- The review found the one missing-map message wrong under the dark master (it named the biases)
  and for biases that cannot be used ("no biases"); the owner approved two messages, `Nu e harta:
  cadrele sunt mai mici de 32 px.` and `Nu e harta: sesiunea nu are bias-uri care sa se poata
  folosi, sau cadrele sunt mai mici de 32 px.`, and kept the lead, which speaks of the tab in
  general. R084 now reaches tiny frames too.
- R-083's word list lives in `tests/DarksCheck.Tests/PlainWords.cs`; it now serves both tabs, so
  its two patterns are named `RomanianTabWords` and `EnglishTabWords`.
