# Explain the Rezumat tab in plain words

Plan for the task branch `claude/Explain-the-darks-Summary-tab-plainly` (LightsCheck's session
holds `claude/Explain-the-Summary-tab-plainly`), worktree
`.claude/worktrees/Explain-the-darks-Summary-tab-plainly`, from `main` at 1c902159. Written on
2026-11-02. The owner picked the Rezumat tab after the ADU scale tab (ADR 0028) and approves its
texts in three parts, each shown with the real texts of the Smida session and of the light-leak
set before any edit; each part is committed on the branch, and the merge is proposed after the
third. The version is not changed.

## Context

The Rezumat tab shows the report's rows: the session card, the `=== DARK-URI` line and its rows,
then one card per check (2 to 9) and INDEX. The console prints the same rows, so the words change
in both at once. The parts:

1. the session card, the header line and its rows, checks 2 and 3;
2. checks 4 to 8;
3. check 9 and INDEX.

The labels `master dark`, `master bias` and `dark - bias` stay: the window finds the maps' rows by
them. The window finds a check's card by the number its title starts with, so the titles may
change.

## Part 1, approved as proposed ("Da, toate", 2026-11-02)

| # | Text | Before | After |
| --- | --- | --- | --- |
| 1 | interval | `20 de cadre la 120.0 s` | `20 de cadre, cate unul la 120.0 s` |
| 2 | mixed settings | `(17 din 20 de cadre; expuneri sau ISO diferite)` | `(17 din 20 de cadre; celelalte au alta expunere sau alt ISO)` |
| 3 | potrivire | `OK: dark-urile au expunerea (119.2 s) si ISO-ul (400) lights-urilor`; `ATENTIE: dark-uri de 120.2 s la ISO 1600, lights de 119.2 s la ISO 400` | `OK: dark-urile au aceeasi expunere (119.2 s) si acelasi ISO (400) ca lights-urile`; `ATENTIE: dark-urile (120.2 s, ISO 1600) nu au setarile lights-urilor (119.2 s, ISO 400)`; the same for the partner's darks |
| 4 | bias | `OK: 0.05 s`; `ATENTIE: 2.0 s nu e un bias (peste 1 s)` | `OK: 0.05 s, destul de scurt pentru un bias`; `ATENTIE: 2.0 s e prea lung pentru un bias (cel mult 1 s)` |
| 5 | header line | `=== DARK-URI: 20 de cadre decodate din 20, 6016 x 4016 px, scara 150 .. 4095 ADU (libraw (exact)), CFA RGGB ===`; FITS `(FITS (presupus din BITPIX 16), negru 200 estimat din mediana cadrului 1)` | `=== DARK-URI: cadre citite 20 din 20, 6016 x 4016 px, valori de la 150 (negru) la 4095 ADU (alb) citite exact din raw, filtre de culoare RGGB ===`; FITS `(FITS pe 16 biti, albul dedus din fisier, negrul luat din valoarea obisnuita a cadrului 1)` |
| 6 | `normalizare`, renamed `1 ADU-eq` | `ISO 400, 119.2 s -> x0.99 fata de ISO 400 si 120 s (1 ADU-eq = 0.99 ADU)`; `; castig x1.80 pe treapta de ISO` | `0.99 ADU din fisier la ISO 400, 119.2 s (ADU-eq e ADU-ul recalculat ca pentru ISO 400 si 120 s)`, the ADU tab's words; `; o treapta de ISO inmulteste semnalul cu 1.80 (setarea ta)` |
| 7 | `cuantizare`, renamed `pasul valorilor` | `pas 2 ADU langa negru; 44 de valori distincte in primul cadru (G1)` | `langa negru, valorile din fisier cresc din 2 in 2 ADU; primul cadru are 44 de valori diferite (pixelii verzi G1)` |
| 8 | bias warnings | `... nu s-au putut decoda (primul: ...); vezi darkscheck-biases.csv cu --out`; `10 din 40 (plafonul de cadre; folosite 30)` | `... nu s-au putut citi (primul: ...); lista e in darkscheck-biases.csv, scris cu --out`; `10 din 40 (peste numarul maxim de cadre; s-au folosit 30)` |
| 9 | check 2's title | `2. SCURGERI DE LUMINA SI GRADIENT` | `2. SCURGERI DE LUMINA SI DIFERENTE PE CADRU` |
| 10 | the three maps' rows | `(SS / DS / SJ / DJ)`, `[G1, ..., blocuri de 32 px]`; `; mediana 0.14, 0.05 .. 0.27 (p1 .. p99)` | `(stanga-sus / dreapta-sus / stanga-jos / dreapta-jos)`, `[pixelii verzi G1, ..., pe patrate de 32 px]`; `; pixelul obisnuit 0.14, aproape toti (98 %) intre 0.05 si 0.27` |
| 11 | lumina pe cadre | `niciun cadru peste 5 x abaterea tipica (0.10 ADU-eq) si peste 0.1 ADU-eq fata de dreapta mediei pe incalzire`; `5 cadre: 26 +0.17, ... ADU-eq peste dreapta mediei pe incalzire (abaterea tipica 0.02)`; `NEMASURAT: sub 6 cadre cu pixeli fierbinti` | `niciun cadru nu e mai luminos decat ar explica incalzirea (prag: peste 0.1 ADU-eq si de peste 5 ori variatia obisnuita a cadrelor, 0.10 ADU-eq)`; `5 cadre sunt mai luminoase decat ar explica incalzirea: 26: +0.17, ... ADU-eq (variatia obisnuita 0.02)`; `NEMASURAT: sunt mai putin de 6 cadre cu pixeli fierbinti`, wherever it stands |
| 12 | glow / constant | `glow +0.32 (creste cu incalzirea), constant +0.17, din care in bias -0.01, ramane +0.18 ADU-eq (brut: ...)`; `(fara bias: lumina sau offset de citire)`; the two `NEMASURAT` reasons | `glow +0.32 (creste cand senzorul se incalzeste), constant +0.17, din care -0.01 vine din bias, raman +0.18 ADU-eq (in fisier: ...)`; `(fara bias-uri nu se stie daca e lumina sau o diferenta de nivel din citirea senzorului)`; `NEMASURAT: seria s-a incalzit prea putin ca sa se poata desparti glow-ul de lumina constanta (pixelii fierbinti variaza doar cu 0.05 din master, trebuie cel putin 0.1)`, `NEMASURAT: fara cadrele luminate raman prea putine (4, trebuie cel putin 5)` |
| 13 | lumina constanta | `dupa bias, fata de centru: sus +0.04 (±0.05), ...; niciuna peste 0.15 si peste 3 x eroarea; o lumina uniforma pe tot cadrul, in toate cadrele, nu are reper`; found `...; peste 0.15 si peste 3 x eroarea la sus; ...`; no biases `fara bias nu se desparte de offsetul de citire: constant ...` | `fata de centru, dupa scaderea bias-ului: sus +0.04 (+/-0.05), ...; nicio latura nu trece de 0.15 si de 3 ori cat poate gresi masuratoarea; o lumina egala pe tot cadrul nu se vede aici, pentru ca nu are cu ce fi comparata`; `...; latura de sus trece de 0.15 si de 3 ori cat poate gresi masuratoarea; ...`; `fara bias-uri nu se poate desparti de diferenta de nivel din citire: constant ...` |
| 14 | check 2's verdict | `OK: nicio margine sau colt la mai mult de 1.0 ADU-eq de centru (cel mai departe +0.09 la stanga-sus, pe dark - bias); niciun cadru cu lumina peste incalzirea seriei; nicio latura cu lumina constanta peste 0.15 si 3 x eroarea`; `lumina constanta in toate cadrele: +0.20 ADU-eq (±0.05) pe latura de sus fata de centru, nu e in bias: scurgere de lumina (ocular, montura)`; `stanga-sus e la +1.20 ADU-eq de centru (pe dark - bias); peste 1.0: <cause>`; the causes; `NEMASURAT: harta nu are valori (cadre mai mici de un bloc)` | `OK: nicio margine si niciun colt nu difera de centru cu peste 1.0 ADU-eq (cel mai mult +0.09, la stanga-sus, pe dark minus bias); niciun cadru nu are lumina in plus; nicio latura nu are lumina constanta peste 0.15 ADU-eq`; `in plus, in toate cadrele latura de sus are +0.20 ADU-eq (+/-0.05) lumina constanta fata de centru, care nu vine din bias: lumina intra in aparat (prin ocular sau prin montura obiectivului)`; `stanga-sus difera de centru cu +1.20 ADU-eq (pe dark minus bias), peste pragul de 1.0: <cause>`; the causes in plain words; `NEMASURAT: cadrele sunt prea mici pentru harta (sub un patrat de 32 px)` |
| 15 | check 3's title | `3. DERIVA TERMICA IN SERIE` | `3. INCALZIREA CAMEREI IN TIMPUL SERIEI` |
| 16 | `amplitudine`, renamed `pixeli fierbinti` | `pixelii fierbinti la 0.83 din master in primul cadru, 1.07 in ultimul (443 de pixeli peste 20 ADU-eq)` | `in primul cadru sunt la 0.83 din nivelul din master, in ultimul la 1.07 (443 de pixeli peste 20 ADU-eq); cresc cu temperatura, deci arata cat de cald era senzorul` |
| 17 | platou | `seria se apropie de un platou la 1.10 din master (constanta de timp 15 min); masterul, mediana seriei, e cu 0.8 grade sub platou; la ultimul cadru mai are +0.2 grade pana la platou`; at the grid's top; flat | `seria se incalzeste spre o temperatura stabila (platou), la 1.10 din master; face doua treimi din drum in 15 min; masterul e cu 0.8 grade sub platou; ultimul cadru mai are +0.2 grade pana la platou`; `seria e prea scurta ca sa se vada cat de repede se incalzeste (doua treimi din drum ar lua cel putin 40 min)`; `seria nu s-a incalzit: nivelul a stat la 1.00 din master, iar primul cadru e la mai putin de 1.0 grade de el` |
| 18 | de aruncat | `2 cadre (1, 2) la peste 2.0 grade de platou (amplit. sub 0.87 sau peste 1.38; platoul 1.10); ...`; `niciun cadru la peste 2.0 grade de platou (amplit. intre 0.87 si 1.38)` | `2 cadre (1, 2) sunt la mai mult de 2.0 grade de platou (pixelii fierbinti sub 0.87 sau peste 1.38 din master; platoul 1.10); ...`; `niciun cadru nu e la mai mult de 2.0 grade de platou (pixelii fierbinti intre 0.87 si 1.38 din master)` |
| 19 | check 3's verdict | `OK: seria s-a asezat (...) si toate cadrele stau in 0.7 .. 1.4 din master; incalzirea de 2.2 grade in 38 min e la inceput (regula dublarii la 6 grade), cadrele de aruncat ...`; the two `ATENTIE`s; the short series | `OK: seria s-a stabilizat (...) si toate cadrele sunt destul de aproape de master ca Siril sa le potriveasca cu -opt (intre 0.7 si 1.4); incalzirea de 2.2 grade din 38 min e la inceput (gradele socotite cu regula: semnalul termic se dubleaza la fiecare 6 grade); cadrele de aruncat ...`; `... platoul e ghicit din curba): masterul amesteca temperaturi diferite; ...`; `... sunt prea departe de master ca Siril sa le potriveasca cu -opt (in afara 0.7 .. 1.4; ...)`; `OK: toate cadrele sunt destul de aproape de master ca Siril sa le potriveasca cu -opt (intre 0.7 si 1.4); sunt mai putin de 6 cadre, deci fara platou` |

The rest of the session card stays, and so do the three maps' labels and `glow / constant`.

## Steps, per part

1. Requirements: the rows that quote the part's texts take the new words; R-082, new, names the
   method's words the part's texts must not use, and grows with each part.
2. Tests: the tests that quote the texts take the new words; `R082_...` reads every variant the
   fixtures reach, in both languages.
3. Code: the texts in `Report/` and the scale's source in `Analysis/FrameStack.cs`, both languages,
   the figures untouched.
4. Goldens recaptured; every changed line is the part's, every figure kept or dropped on purpose.
5. Gate, commit; after part 3, ADR 0030 completed, CHANGELOG, `/validate`, the merge proposal.

## What deviated

- Part 1: variants the list did not show take the same shape: a FITS with decimals,
  `FITS pe 32 biti cu zecimale`; the black from the biases, `negrul luat din bias-uri`; a series
  that cools towards its plateau, `seria se raceste spre ...`; one frame to discard,
  `1 cadru (5) e la mai mult de ...`; the `1 ADU-eq` row for frames that state no ISO or exposure,
  as in the ADU tab. The verdict's parts are joined by `; in plus, `, with the comma of the
  approved text.
- Part 1: the `1 ADU-eq` row said `setul e facut chiar la setarile de referinta` whenever the factor
  was 1, which a set at ISO 800 and 60 s also has; it now says so only at ISO 400 and 120 s, and
  the ADU tab's row, approved with the same words on the ADU branch, takes the same test.
