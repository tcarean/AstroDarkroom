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

## Part 2, approved as proposed ("Aprobat", 2026-11-02)

The texts of the Smida session, or of the light-leak set where Smida has no example; an example
the two sets do not reach is made up, with the same figures before and after.

| # | Text | Before | After |
| --- | --- | --- | --- |
| 1 | check 4's title | `4. ZGOMOT PE CADRU` | `4. ZGOMOTUL FIECARUI CADRU` |
| 2 | `sigma rezidual`, renamed `zgomot fata de master` (see "What deviated") | `R 1.60 .. 1.88; G1 1.47 .. 1.56 (mediana 1.52); ...  [ADU, cadru - master]`; `NEMASURAT: un singur cadru nu are rezidual fata de propria mediana` | `R 1.60 .. 1.88; G1 1.47 .. 1.56 (de obicei 1.52); ...  [ADU: cat difera fiecare cadru de master, de la cel mai linistit cadru la cel mai zgomotos]`; `NEMASURAT: un singur cadru nu are cu ce fi comparat` |
| 3 | zgomot de citire | `R 1.56  G1 1.26  G2 1.26  B 1.45 ADU (din 29 perechi de bias-uri consecutive); zgomot temporal peste citire pe G1 ~0.67 ADU (sqrt(sigma_t^2 - citire^2): curent de intuneric si cuantizare)`; `NEMASURAT: fara bias-uri` | `... ADU (din 29 de perechi de bias-uri luate unul dupa altul); zgomotul adaugat in timpul expunerii, peste cel de citire, pe pixelii verzi G1: ~0.67 ADU (vine din caldura senzorului si din pasul valorilor)`; `NEMASURAT: sesiunea nu are bias-uri` |
| 4 | benzi aleatoare | `orizontale 0.114 ADU rms pe rand (de 4.1 ori zgomotul alb), verticale de 1.0 ori` | `orizontale: randurile difera intre ele cu 0.114 ADU, de 4.1 ori cat ar da zgomotul singur; verticale: de 1.0 ori (1 inseamna fara benzi)` |
| 5 | check 4's verdict | `OK: niciun cadru cu zgomot peste 1.5 x mediana (1.52 ADU)`; `ATENTIE: 2 cadre cu zgomot peste 1.5 x mediana (1.52 ADU): 4, 7` | `OK: niciun cadru nu are zgomotul de peste 1.5 ori mai mare decat de obicei (1.52 ADU)`; `ATENTIE: 2 cadre au zgomotul de peste 1.5 ori mai mare decat de obicei (1.52 ADU): 4, 7` |
| 6 | `master peste nivel`, renamed `pixeli peste prag` | `10: 2283; 20: 443; 50: 111; 100: 24; 500: 0  [ADU-eq peste negru, mediana seriei, toate planurile]` | `peste 10: 2283; peste 20: 443; peste 50: 111; peste 100: 24; peste 500: 0  [pixeli ai masterului, ADU-eq peste negru, toate culorile]` |
| 7 | `pe plan la 20`, renamed `pe culori, peste 20` | `R 79  G1 143  G2 142  B 79` | the value stays |
| 8 | `amplitudine`, renamed `cat de fierbinti` | `mediana 33, p10 21, p90 81, p99 275 ADU-eq (din 443)` | `de obicei 33 ADU-eq; 80 % dintre ei intre 21 si 81; cei mai fierbinti 1 % peste 275 (din 443 de pixeli)` |
| 9 | stabilitate | `412 din 443 peste jumatate din prag in toate cele 20 de cadre, 19 lipsesc din 2 sau mai multe; sigma temporala 5.58 ADU la cei fierbinti, 1.45 la restul; 92 candidati RTS pe G1 (sigma temporala peste 6 x zgomotul unui cadru, 1.51)` | `412 din 443 se vad (peste jumatate din prag) in toate cele 20 de cadre, 19 lipsesc din 2 sau mai multe; de la un cadru la altul variaza cu 5.58 ADU, ceilalti pixeli cu 1.45; 92 de pixeli verzi G1 sar intre doua niveluri (RTS: variaza de peste 6 ori cat zgomotul unui cadru, 1.51)` |
| 10 | check 5's verdict | `OK (informativ): 443 de pixeli peste 20 ADU-eq in 24.2 Mpx` | the verdict stays |
| 11 | check 6's title | `6. MODEL FIX` | `6. MODEL FIX: CE E LA FEL IN TOATE CADRELE` |
| 12 | `benzi structurate`, renamed `benzi fixe` | `randuri 0.030 ADU-eq rms (0.39 varf la varf, G1), coloane 0.022 (0.17, B)  [profilul masterului minus mediana glisanta pe 101]` | `randurile masterului difera intre ele cu 0.030 ADU-eq (0.39 intre cel mai luminos si cel mai intunecat, pe G1), coloanele cu 0.022 (0.17, pe B)  [dupa ce se scot variatiile lente, pe cate 101 randuri sau coloane]` |
| 13 | glow | `+0.05 ADU-eq la stanga-sus, partea care creste cu incalzirea (constant +0.05 in afara ei; dark - bias)` | `+0.05 ADU-eq la stanga-sus, partea care creste cand senzorul se incalzeste (in afara ei, constant +0.05; pe dark minus bias)` |
| 14 | check 6's verdict | `OK: benzi sub 2.0 ADU-eq varf la varf si margini sub 1.0`; `ATENTIE: benzi de 2.50 ADU-eq varf la varf si glow de +1.20 ADU-eq; modelul ramane in lights fara dark-uri`; `NEMASURAT: harta sau profilele nu au valori` | `OK: benzile fixe raman sub 2.0 ADU-eq intre cel mai luminos si cel mai intunecat rand, iar marginile sub 1.0 fata de centru`; `ATENTIE: benzi fixe de 2.50 ADU-eq intre cel mai luminos si cel mai intunecat rand si glow de +1.20 ADU-eq; fara dark-uri, desenul acesta ramane in lights`; `NEMASURAT: cadrele sunt prea mici pentru harta sau pentru benzi` |
| 15 | check 7's `master` | `0 sub negru - 50 ADU, 10 la alb (4095)` (the light-leak set) | `0 pixeli morti (sub negru minus 50 ADU), 10 saturati (la alb, 4095)`; the verdict stays |
| 16 | `zgomotul mediei`, renamed `zgomot dupa medie` | `n=1 1.43 (asteptat 1.51); n=2 1.02 (asteptat 1.07); ...; n=10 0.47 (asteptat 0.48)  [G1, centrul de 2048 px, ADU, sigma temporala = cadru fata de cadru]`; `NEMASURAT: sub 2 cadre` | `1 cadru: 1.43 (se astepta 1.51); 2 cadre: 1.02 (se astepta 1.07); ...; 10 cadre: 0.47 (se astepta 0.48)  [pixelii verzi G1, centrul de 2048 px, ADU; masurat de la un cadru la altul]`; `NEMASURAT: sunt mai putin de 2 cadre` |
| 17 | check 8's `master` | `20 de cadre -> 0.40 ADU zgomot al masterului median (1.25 x sigma temporala 1.43 / sqrt(20)); mediana a 10 fata de media a 10: 0.79` | `20 de cadre dau un master cu 0.40 ADU de zgomot pe pixel (din zgomotul de la un cadru la altul, 1.43); un master din 10 cadre facut cu mediana si unul din celelalte 10 facut cu media difera cu 0.79 ADU`, `0.40 ADU` still bold in the window |
| 18 | check 8's verdict | `OK: 20 de cadre ajung; zgomotul masterului median (0.40 ADU) e sub pasul de cuantizare (2 ADU) si intra in cuadratura in fiecare light calibrat`; `ATENTIE: doar 4 cadre; mediana cere cel putin 10 ca un cadru ciudat sa n-o traga (zgomotul masterului ar fi 1.87 ADU)`; `ATENTIE: zgomotul masterului median (2.10 ADU) e peste pasul de cuantizare (2 ADU); ar cobori sub pas de la 57 de cadre` | `OK: 20 de cadre ajung; zgomotul masterului (0.40 ADU) e sub pasul valorilor din fisier (2 ADU), iar el se adauga la fiecare light calibrat`; `ATENTIE: doar 4 cadre; trebuie cel putin 10, ca un cadru ciudat sa nu strice masterul (zgomotul masterului ar fi 1.87 ADU)`; `ATENTIE: zgomotul masterului (2.10 ADU) e peste pasul valorilor din fisier (2 ADU); ar cobori sub el de la 57 de cadre` |

The English says the same, with the same figures in the same order.

## Part 3, approved as proposed ("Da, toate", 2026-11-02)

The texts of the Smida session and of the Veil session, both against the 2026-10-10 library, or
of the golden sets where neither has an example; a rare variant is made up, with the same
figures before and after. The window's Lights and Index tabs show the same rows.

| # | Text | Before | After |
| --- | --- | --- | --- |
| 1 | check 9's title | `9. LIGHTS FATA DE DARK` | `9. CAT DE BINE SE POTRIVESTE DARK-UL CU LIGHTS-URILE` |
| 2 | lights not read | `ATENTIE: 2 lights nu s-au putut decoda (primul: ...); vezi tabelul` | `ATENTIE: 2 lights nu s-au putut citi (primul: ...); sunt in tabelul de lights` |
| 3 | lights sarite (Veil) | `43 din 87 (plafonul de cadre; folosite 44)` | `43 din 87 (peste numarul maxim de cadre; s-au folosit 44)`, as part 1's biases |
| 4 | lights without ISO or exposure | `ATENTIE: 3 lights nu spun ISO-ul sau expunerea; presupuse ale dark-urilor (normalizare x0.99)` | `...; s-au luat cele ale dark-urilor (1 ADU-eq = 0.99 ADU)` |
| 5 | pixeli fierbinti | `86 de pixeli persistenti in lights (izolati, peste 6 sigma locale, in cel putin 26 din 32 de cadre)` | `86 de pixeli fierbinti se vad in lights in acelasi loc, in cel putin 26 din 32 de cadre (pixeli singuri, nu parte dintr-o stea, mai luminosi decat imprejurimea cu peste 6 ori zgomotul ei)` |
| 6 | in masterul dark | `peste 10: 85 din 86 (99 %); ...  [ADU-eq]` | the values stay, then `[cati dintre acesti pixeli sunt si in masterul dark, peste fiecare prag in ADU-eq]` |
| 7 | `master in lights`, renamed `masterul in lights` | `peste 10: 85 din 2283 (4 %); ...` | the values stay, then `[cati dintre pixelii masterului de peste fiecare prag se vad si in lights]` |
| 8 | `nepotriviti`, renamed `lipsesc din master` | `1 pixel persistent in lights sub 20 ADU-eq in master (mediana lor in master 3 ADU-eq)`; `niciun pixel persistent in lights sub pragul masterului` | `1 pixel fierbinte din lights e sub 20 ADU-eq in master (acolo are 3 ADU-eq)`; Veil `12 pixeli fierbinti din lights sunt sub 20 ADU-eq in master (acolo au de obicei 3 ADU-eq)`; `niciunul: toti pixelii fierbinti din lights sunt peste 20 ADU-eq si in master` |
| 9 | `amplitudine`, renamed `lights fata de dark` | `lights / dark 1.00 pe noapte (mediana pe 32 de cadre; primul 0.72, ultimul 0.94; 443 de pixeli)`; `NEMASURAT: niciun pixel fierbinte sub saturatie in master` | `in lights, pixelii fierbinti sunt de obicei de 1.00 ori cat in dark (pe cele 32 de cadre; in primul 0.72, in ultimul 0.94; pe 443 de pixeli)`; `NEMASURAT: masterul nu are niciun pixel fierbinte care sa nu fie saturat` |
| 10 | the verdict, the partner's too (`la partener` for `in dark`) | `OK: scadere directa (raport 1.00, in 0.9 .. 1.1; dark-ul e la aceeasi temperatura)`; `ATENTIE: scade cu -opt (raport 1.30, in 0.7 .. 1.4; dark-ul e mai rece cu 2.3 grade, -opt scaleaza amplitudinea)`; `ATENTIE: doar corectie cosmetica (-cc=dark): raport 0.43, dark-ul e mai cald cu 7.2 grade si vede toti pixelii fierbinti, dar scaderea l-ar supracorecta`; `ATENTIE: nepotrivit (raport 1.60: ...; prea rece, pierde pixelii fierbinti slabi)`; `ATENTIE: nepotrivit: lights-urile nu arata pixelii fierbinti ai dark-ului (raport 0.00; reducere de zgomot in aparat, alt aparat sau alta orientare?)` | `OK: dark-ul se poate scadea asa cum e; in lights pixelii fierbinti sunt de 1.00 ori cat in dark, intre 0.9 si 1.1: dark-ul e la aceeasi temperatura`; `ATENTIE: scade dark-ul cu -opt (-opt: Siril il inmulteste cat sa se potriveasca cu lights-urile); in lights pixelii fierbinti sunt de 1.30 ori cat in dark, intre 0.7 si 1.4: dark-ul e mai rece cu 2.3 grade`; `ATENTIE: foloseste dark-ul doar pentru corectia cosmetica (-cc=dark: Siril gaseste in el pixelii fierbinti si ii repara in lights); in lights pixelii fierbinti sunt de 0.43 ori cat in dark: dark-ul e mai cald cu 7.2 grade, ii vede pe toti, dar scazut ar scoate prea mult si ar lasa puncte negre`; `ATENTIE: nu se potrivesc; in lights pixelii fierbinti sunt de 1.60 ori cat in dark: dark-ul e mai rece cu 4.1 grade; e prea rece si ii pierde pe cei slabi`; `ATENTIE: nu se potrivesc: in lights nu se vad pixelii fierbinti ai dark-ului (de 0.00 ori cat in dark; poate reducerea de zgomot din aparat, alt aparat sau cadre rotite?)` |
| 11 | INDEX, dark-uri | `idx25 (25.2 ADU-eq: mediana pixelilor de pe rangurile 5-20 pe Mpx, al 122-lea .. al 483-lea din 24.2 Mpx, normalizat la ISO 400 si 120 s)` | `idx25 (25.2 ADU-eq: valoarea obisnuita a pixelilor fierbinti de pe locurile 122 .. 483, numarand de la cel mai fierbinte, adica 5-20 pe fiecare Mpx din 24.2 Mpx; socotita pentru ISO 400 si 120 s)` |
| 12 | lights | `echivalent idx25 (dark x 1.00, adica +0.0 grade fata de dark)`; Veil `echivalent idx29 (dark x 0.43, adica -7.2 grade fata de dark)` | `ca un dark cu idx25 (de 1.00 ori cat dark-ul, adica la aceeasi temperatura)`; Veil `ca un dark cu idx29 (de 0.43 ori cat dark-ul, adica cu 7.2 grade mai reci decat el)` |
| 13 | partener | `...: idx67 (67.4 ADU-eq; 30 de cadre din 30, ISO 1600, 120.2 s); eticheta ...` | `cadre citite 30 din 30`, as in the header; the rest stays |
| 14 | dark-uri / partener | `0.42 (q25 0.38, q75 0.44, 434 de pixeli comuni) dupa normalizare, adica dark-urile sesiunii sunt cu -7.5 grade fata de partener`; `NEMASURAT: niciun pixel comun` | `pixelii fierbinti sunt in dark-urile sesiunii de 0.42 ori cat la partener, la aceleasi setari (jumatate dintre ei intre 0.38 si 0.44; 434 de pixeli fierbinti in amandoua), adica dark-urile sesiunii sunt cu 7.5 grade mai reci decat partenerul`; `NEMASURAT: niciun pixel nu e fierbinte in amandoua` |
| 15 | lights / partener | `0.42 (prin dark-urile sesiunii), adica lights-urile sunt cu -7.5 grade fata de partener` | `pixelii fierbinti sunt in lights de 0.42 ori cat la partener (socotit prin dark-urile sesiunii), adica lights-urile sunt cu 7.5 grade mai reci decat partenerul` |
| 16 | verdict partener | `ATENTIE: doar corectie cosmetica (-cc=dark): raport 0.42, partenerul e mai cald cu 7.5 grade si vede toti pixelii fierbinti, dar scaderea l-ar supracorecta`; without lights `... (dark-urile sesiunii fata de partener)` | the shape of 10, `ATENTIE: foloseste partenerul doar pentru corectia cosmetica (-cc=dark: ...); in lights pixelii fierbinti sunt de 0.42 ori cat la partener: partenerul e mai cald cu 7.5 grade, ...`; without lights the sentence names `in dark-urile sesiunii`, so the tail goes |
| 17 | they stay | `NEMASURAT: sesiunea nu are lights`, `eticheta`, `pixeli comuni`, `dark-uri (partener)`, the partner's errors | |

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
- Part 2: the owner approved `zgomot fata de master` for check 4's first row; it is 21 characters
  and the console aligns the labels on 19, so its colon would stand two columns right of the
  others. The label is `fata de master` (`against the master`), which the title above it,
  `4. ZGOMOTUL FIECARUI CADRU`, completes; the owner is asked whether to keep it.
- Part 2: variants the list did not show take the same shape: one pixel that jumps, `1 pixel verde
  G1 sare`, and a red or blue plane without `verde`; an odd series, `un master din 3 cadre facut cu
  mediana si unul din celelalte 2`, or `din cadrul ramas` when one frame is left; a single frame,
  `din zgomotul cadrului, 2.99, fiind unul singur`; the glow without its split, `-0.14 ADU la
  stanga-sus fata de centru (pe dark minus bias)`; the counts of check 7 with `de` from 20 and
  their singular, `1 pixel mort`, `1 saturat`.
- Part 2: the doc comment of check 3's row still named it `amplitudine`, part 1's old label; it
  names `pixeli fierbinti` now, like the comments of the rows part 2 renamed.
- Part 2: the owner kept `fata de master` with part 3's "Da, toate" (the question was asked with
  it, the recommendation first).
- Part 3: the window's Lights tab carries check 9's title as its heading; it takes the new title.
- Part 3: variants the list did not show take the same shape: one hot pixel, `1 pixel fierbinte se
  vede ... (un pixel singur, ... mai luminos ...)`; one light read, `pe singurul cadru`; one light
  not read, `e in tabelul de lights`; lights warmer than the master, `adica cu 2.3 grade mai calde
  decat el`; an INDEX row whose ratio gives no degrees (not positive) drops its `adica ...` clause,
  where it printed `0.0 grade`; `-opt` for the partner, `scade partenerul cu -opt`.
- Part 3: the analysis's error messages that a row quotes (`niciun light nu s-a putut decoda`,
  `niciun dark al partenerului nu s-a putut decoda`, and the darks' and biases' in the console)
  keep `decoda`: they were not in the list; R-082 leaves them out, and the owner is asked.
- `README.md`: its three report examples (the start on Smida, check 9 and INDEX on Smida, the
  partner rows against the library) are taken again from runs on the sessions, and the words it
  quotes (the check titles, `1 ADU-eq`, `not in the master`, check 9's verdicts) are the report's
  new ones; its explanations of the method keep their words, for a task of their own.
