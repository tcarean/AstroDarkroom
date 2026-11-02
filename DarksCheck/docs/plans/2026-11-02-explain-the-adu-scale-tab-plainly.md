# Explain the ADU scale tab in plain words

Plan for the task branch `claude/Explain-the-ADU-scale-tab-plainly`, worktree
`.claude/worktrees/Explain-the-ADU-scale-tab-plainly`, from `main` at 601a795b. Written on
2026-11-02. The owner asked for the texts in chat and approved the list below as proposed
("Da, toate", 2026-11-02); this file records that list and the steps. The version is not changed.

## Context

The owner found the explanations of the window's tabs too complicated and asked for every tab in
words anyone can follow, starting with `Explicatii scara ADU`, the original and the proposal of
each text side by side before any edit. The owner also asked that every text written for users,
and every explanation given to the owner in chat, be in the same plain register from now on,
noted in the rules and in a decision. The other tabs follow, one task each.

The tab shows `ScaleReport`'s eleven rows and four paragraphs (R-080, R-078) under a lead written
in `ScaleView.xaml`. The console prints the same rows and paragraphs at the end of the report, so
one change in the core changes both. `ScaleReport.AgainstMasters` also ends check 2's
`lumina constanta` and `lumina pe cadre` rows and its verdict on the Rezumat tab.

## The approved texts

The Romanian as approved; the English says the same in the same order. The figures are the
synthetic session's of the goldens and stay what they are.

| # | Text | Before | After |
| --- | --- | --- | --- |
| 1 | tab and title | `Explicatii scara ADU` | kept (the owner's name, ADR 0015) |
| 2 | lead (window) | `Cat valoreaza un ADU pe setul deschis: scara fisierului, normalizarea, zgomotele, pragurile si ce s-a gasit, in ADU brut, ADU-eq si electroni.` | `ADU e unitatea in care fisierul scrie cat de luminos e fiecare pixel. Aici vezi ce inseamna 1 ADU pe setul deschis: cat incape in fisier, cat zgomot e, de unde incep pragurile si ce s-a gasit.` |
| 3 | `fisier` | `16 biti, 0 .. 65535 ADU; negrul la 200, albul la 65535: semnal util 0 .. 65335 ADU` | `valori de la 0 la 65535 ADU (16 biti); negrul camerei e la 200 si albul la 65535, deci pentru lumina raman 0 .. 65335 ADU`; a float file `valori cu zecimale (virgula mobila), de la 0 la 1.000; negrul la 0.003` |
| 4 | `1 ADU-eq` | `1 ADU brut: setul e la ISO 400, 120.0 s, scara de referinta (ISO 400 si 120 s)` | `1 ADU din fisier: setul e facut chiar la setarile de referinta, ISO 400, 120.0 s`; other settings `4.00 ADU din fisier la ISO 1600, 120.0 s (ADU-eq e ADU-ul recalculat ca pentru ISO 400 si 120 s)`; unknown `fisierele nu spun ISO-ul sau expunerea, deci 1 ADU-eq e luat egal cu 1 ADU din fisier` |
| 5 | `cuantizare`, renamed `pasul valorilor` | `pas 1 ADU langa negru` | `langa negru, valorile din fisier cresc din 1 in 1 ADU` |
| 6 | `electroni` | `1 ADU = 2.07 e- la ISO 400 (Nikon D3300, photonstophotos.net din curbele DxOMark, citit 2026-10-20; metoda supraestimeaza castigul pe senzorii mici); 1 ADU-eq = 1.00 ADU = 2.1 e-; full well 34446 e-, QE 56 %` | `la ISO 400, 1 ADU = 2.07 electroni (senzorul transforma lumina in electroni, iar camera ii numara in ADU); 1 ADU-eq = 1.00 ADU = 2.1 electroni; intr-un pixel incap 34446 electroni (full well), iar pixelul prinde 56 % din lumina care cade pe el (QE). Cifre pentru Nikon D3300 de pe photonstophotos.net, scoase din masuratorile DxOMark, citite pe 2026-10-20; la senzorii mici metoda da prea multi electroni pe ADU.`; unknown camera `necunoscut pentru Canon EOS 6D: aplicatia nu are in tabel cati electroni inseamna 1 ADU la acest aparat`; an ISO outside the table `Nikon D3300: cati electroni inseamna 1 ADU se stie doar intre ISO 100 si 25600 (<source>), nu si la ISO-ul acestor cadre` |
| 7 | `zgomot de citire` | `2.02 ADU pe pixel (G1, din bias-uri) = 4.2 e-` | `2.02 ADU pe pixel = 4.2 electroni: zgomotul pe care camera il adauga cand citeste senzorul, masurat pe bias-uri (pixelii verzi G1)`; without biases `NEMASURAT: sesiunea nu are bias-uri (sursa da 2.0 electroni pentru Nikon D3300)` |
| 8 | `zgomot pe cadru`, renamed `zgomot pe un dark` | `sigma 3.01 ADU pe pixel pe un dark; curentul de intuneric median -0.03 ADU la ISO 400, 120.0 s` | `un dark are 3.01 ADU de zgomot pe pixel; curentul de intuneric (ce aduna un pixel obisnuit din caldura senzorului) e -0.03 ADU la ISO 400, 120.0 s`; without biases `...; curentul de intuneric (ce aduna un pixel din caldura senzorului) se masoara doar cu bias-uri` |
| 9 | `zgomot in master` | `dark: 1.87 ADU pe pixel (mediana a 4 cadre); stack lights: 3.4 ADU pe pixel cu media, 4.0 cu mediana (5 lights; cu zgomotul masterului dark inclus)` | `masterul dark, facut din 4 cadre, mai are 1.87 ADU de zgomot pe pixel; stack-ul celor 5 lights are 3.4 ADU pe pixel daca il faci cu media si 4.0 daca il faci cu mediana (cu zgomotul masterului dark inclus)`; without lights `stack-ul de lights: sesiunea nu are lights`; one frame `NEMASURAT: cu un singur cadru nu exista master, deci nici zgomotul lui` |
| 10 | `praguri pe set` | `pixel fierbinte 20 ADU-eq = 20 ADU; scurgere pe harta 1.0 ADU-eq = 1.0 ADU; ...` | `pixel fierbinte: peste 20 ADU-eq (20 ADU in fisier); scurgere de lumina pe harta: peste 1.0 ADU-eq (1.0 ADU); lumina constanta pe o latura: peste 0.15 ADU-eq (0.15 ADU); lumina pe un singur cadru: peste 0.1 ADU-eq (0.10 ADU)` |
| 11 | `gasit`, renamed `ce s-a gasit` | `lumina constanta +0.20 ADU-eq pe latura de sus = +0.80 ADU = +0.42 e- pe pixel; +0.80 ADU e 0.56 din zgomotul pe pixel al masterului dark (1.43 ADU) si 0.24 din al stack-ului de lights (3.3 ADU)` | `lumina constanta pe latura de sus: +0.20 ADU-eq = +0.80 ADU in fisier = +0.42 electroni pe pixel; +0.80 ADU e 0.56 din zgomotul pe pixel al masterului dark (1.43 ADU) si 0.24 din zgomotul stack-ului de lights (3.3 ADU)`; the ratio's words change in check 2 too; `nimic peste praguri` kept |
| 12 | `cer` | `mediana 500.0 ADU peste negru pe un light (G1, 5 lights), sigma 6.3 ADU pe pixel` | `pe un light, cerul e la 500.0 ADU peste negru, cu 6.3 ADU de zgomot pe pixel (pixelii verzi G1, din 5 lights)` |
| 13 | `saturatie` | `65535 ADU (34446 e- full well dupa sursa)` | `la 65535 ADU pixelul e plin si nu mai poate creste; dupa sursa, intr-un pixel incap 34446 electroni (full well)` |
| 14 | second heading | `DE CE CONTEAZA O FRACTIUNE DE ADU` | kept |
| 15 | paragraph 1 | `ADU e unitatea fisierului: ... pentru ca zgomotul amesteca treptele.` | `ADU e unitatea in care fisierul scrie cat de luminos e fiecare pixel: aici numere intregi de la 0 la 65535 (16 biti), cu negrul camerei la 200. ADU-eq e acelasi ADU recalculat ca si cum cadrul ar fi fost facut la ISO 400 si 120 s, ca sa poti compara serii facute cu setari diferite: pe acest set 1 ADU-eq = 1.00 ADU din fisier. Fisierul urca din 1 in 1 ADU, dar media multor pixeli poate iesi si o fractiune de ADU, pentru ca zgomotul face ca unii pixeli sa cada pe o treapta si altii pe urmatoarea.`; an unknown camera ends it with `Aparatul Canon EOS 6D nu e in tabelul aplicatiei, asa ca nicio cifra de aici nu e data in electroni si lipsesc cifrele din sursa: cati electroni incap intr-un pixel (full well) si zgomotul de citire.` (`Fisierele nu spun ce aparat le-a facut, asa ca ...` without a camera) |
| 16 | paragraph 2 | `O abatere din masterul dark (o scurgere de lumina, un offset) nu e zgomot: ...` | `O greseala din masterul dark nu e zgomot. De exemplu: lumina intrata in aparat, sau un nivel de negru putin mutat. Masterul se scade din fiecare light, asa ca greseala ajunge intreaga in stack, oricate cadre ai aduna. Zgomotul, in schimb, scade cand aduni cadre: de 4 ori mai multe cadre il injumatatesc. Masterul dark, facut din 4 cadre, mai are 1.87 ADU de zgomot pe pixel; cu acesta compari o greseala, nu cu zgomotul unui singur cadru. Pe un singur pixel, cei 3.0 ADU de zgomot ai unui dark ascund orice greseala sub 1 ADU. Dar ochiul vede zone, nu pixeli: intr-o zona de 50 x 50 pixeli zgomotul se compenseaza, iar greseala se vede de 50 de ori mai bine decat pe un pixel. De aceea o treapta de 1 ADU cu margine dreapta se vede cand intinzi tare (stretch) un cer intunecat.` |
| 17 | paragraph 3 | `Fata de cer: un light al acestei sesiuni are 500.0 ADU peste negru, ... in cuadratura, care nu se mediaza; iar un offset de 1 ADU din master ramane 1 ADU.` | `Fata de cer: pe un light al acestei sesiuni cerul e la 500.0 ADU peste negru, deci 1 ADU e 0.20 % din cer, cam cat greseste de obicei un flat. In stack-ul celor 5 lights zgomotul pe pixel scade la 3.40 ADU daca il faci cu media (4.02 cu mediana). In cifra asta intra doua zgomote. Al lights-urilor (6.3 ADU pe un light) scade cu cat aduni mai multe. Al masterului dark (1.87 ADU) nu scade, pentru ca acelasi master se scade din toate lights-urile. Iar o greseala de 1 ADU din master ramane 1 ADU in stack.`; without lights `Fata de cer: sesiunea nu are lights, deci nu se poate spune cat inseamna 1 ADU fata de cerul tau, nici cat zgomot ramane in stack. Deschide sesiunea impreuna cu lights-urile ei si randurile ...` |
| 18 | paragraph 4 | `Pragurile: 20 ADU-eq pentru un pixel fierbinte; ... remedierea (ocularul acoperit, seria refacuta) costa la fel in ambele cazuri.` | `Pragurile: un pixel e fierbinte peste 20 ADU-eq. Pe harta din tab-ul Harti, o scurgere de lumina e evidenta peste 1.0 ADU-eq (pragul din analiza facuta de mana). Lumina constanta pe o latura conteaza peste 0.15 ADU-eq, si doar daca e de peste 3 ori mai mare decat cat poate gresi masuratoarea. Lumina pe un singur cadru conteaza peste 0.1 ADU-eq, si doar daca e de peste 5 ori mai mare decat cat difera de obicei un cadru de celelalte. Ultimele doua praguri spun 'aici e lumina reala, masurata', nu 'se vede in imagine': la 0.15 ADU in fisier abia s-ar vedea, la 1.0 ADU se vede. Remediul e acelasi in ambele cazuri: acoperi vizorul si refaci seria.` |

## Steps

1. Requirements: R-080 quotes the new labels and words; R-081, new, says the section is in plain
   words and names the method words it must not use.
2. Tests: the R-080 tests and the window's R-078 test take the new words; `R081_...` checks
   every row and paragraph, in both languages, on the scenes that reach each variant (lights and
   biases, a bare series, a found light with lights, an unknown camera, one frame).
3. Code: `ScaleReport` (rows, paragraphs, `AgainstMasters`), `CameraFacts.SourceWords`, the lead
   in `ScaleView.xaml`, both languages, the figures untouched.
4. Goldens: the six that carry the section are recaptured; every changed line must lie in the
   section, its figures unchanged.
5. The rule "Plain words" in `CLAUDE.md` and ADR 0028; dated notes in ADRs 0015, 0016 and 0023
   for the words they quote; the design note `dark-analysis.md` takes the new row names;
   CHANGELOG.
6. Gate, commit on the branch, then the reviews and the merge proposal.

## What deviated

- The approved row 6 said `metoda da putin prea multi electroni pe ADU`. The source says only
  that the method overestimates the gain on small sensors, not by how little, so the text says
  `prea multi` without `putin` (ADR 0028).
- Variants the list did not show take the same shape: the found row's map offset,
  `<region>: +1.20 ADU-eq fata de centru (+4.80 ADU in fisier)`, and a reference position whose
  colour the file does not give, `(pixelii CFA[0,1])` instead of `(pixelii verzi G1)`.
- The English writes the step `1 by 1 ADU`, so it carries the figure twice, as the Romanian
  `din 1 in 1` does: `EnglishGoldenOutputTests` holds both languages to the same figures in the
  same order, and it failed on `in steps of 1 ADU`.
- Row 9's `NEMASURAT: cu un singur cadru nu exista master, deci nici zgomotul lui` was wrong,
  as the old words were: a single frame gets a master noise predicted from its own noise
  (R-024), and the row says `NEMASURAT` only when the frames' noise could not be measured. Put
  to the owner, who approved a wording that says what happens ("Da, schimba-l"): `NEMASURAT:
  zgomotul cadrelor nu s-a putut masura, deci nici al masterului` (`UNMEASURED: the frames'
  noise could not be measured, so neither could the master's`). No fixture reaches that row,
  so R-080's test and `R081_...` take a result's master noise away by hand; the comments of
  `ScaleReport` that said "NaN for a single frame" now say when it is.
- The design note's sentence that `AgainstMasters` serves the `zgomot in master` row was wrong
  (only `ce s-a gasit` and check 2 call it) and was fixed with the row names.
- The README's description of the tab takes the new row names.
