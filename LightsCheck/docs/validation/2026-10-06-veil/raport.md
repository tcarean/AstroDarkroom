# Veil, 2026-10-06 - analiza celor 87 de lights

> Copia raportului livrat pe 2026-10-09 (scris in `E:\Photografie\Code\LightsCheck\reports\`),
> pastrat ca istoric: cifrele lui sunt referinta cazurilor din `docs/validation.json`.
> **Corectie 2026-10-09**: cadrul 75 are in EXIF o expunere de 133.2 s, nu 120.2 s (singurul;
> `frames.csv` o are, raportul n-a observat-o). Saltul lui de fond de +11 % de la sectiunea 2
> e exact raportul expunerilor (133.2 / 120.2 = 1.108), nu o lumina de moment sau un nor.

Sursa: `E:\Photografie\Astro\Veil\2026-10-06-3h\lights\DSC_0001..0087.NEF` (doar citite, nemodificate).
Nikon D3300 (12 bit, black 150, white 4095), Samyang 135 mm f/2.8, Optolong L-eNhance, Star Adventurer GTi,
120.2 s, ISO 1600. Scara: 5.94"/px. Sesiunea: 23:35:03 - 02:57:49 (3 h 23 min), integrare 2 h 54 min (86 %).

Fisiere in acest folder: `frames.csv` (toate masuratorile pe cadru), `dither_map.png`, `dither_steps.png`,
`quality_timeline.png`, `crops_*.png` (decupaje de stele), `scripts/` (scripturile Python cu care s-a masurat).

## Verdict pe scurt

| Aspect | Verdict |
|---|---|
| Focus | BUN si constant toata noaptea: FWHM 3.0-3.2 px (18-19") pe cadrele bune, la fel la inceput si la sfarsit; colturile nu sunt mai slabe decat centrul |
| Expunere | BUNA: fondul de cer in G 126-177 ADU peste bias (3-4.5 % din scara), nimic taiat in negru; in R (Ha) doar 25-37 ADU - canalul rosu e la limita regulii "cer peste zgomotul de citire" |
| Roua | NU a aparut: FWHM constant, halo-ul stelelor scade (0.027 -> 0.012), stelele detectate scad doar cat creste fondul |
| Cadre miscate | DA, problema principala: 22 cadre clar miscate (alungire >= 1.6), 17 la limita (1.35-1.6), 48 bune; toate miscarile sunt pe axa RA |
| Dithering | INSUFICIENT: practic 1D (pe RA), 1/3 din pasi sub 10 px, fiecare pozitie e folosita de 2 cadre, grupuri de 4-10 cadre in aceeasi zona de 5-10 px |
| Nori / transparenta | fara nori; fondul creste cu 40 % (campul coboara), gradientul de la 10 la 24 ADU intre colturi - background extraction il scoate |
| Dare de satelit/avion | 2 cadre (2, 36), le scoate sigma clipping |

## 1. Focus

- FWHM median pe cadrele bune: 3.18 px in centru, 3.17 px la mijloc, 2.97 px la margine (r > 2200 px). Colturile nu
  sunt mai slabe; alungirea la margine 1.19 vs 1.17 in centru - fara coma sau astigmatism vizibile la 5.9"/px.
- Primele 10 cadre bune vs ultimele 10: 3.15 vs 3.18 px. Focusul a tinut; nu e nevoie de refocus pe parcurs.
- 3 px FWHM la 135 mm e in jur de ce dau obiectivul + seeing + interpolarea Bayer; nu e loc evident de castig.

## 2. Expunere si zgomot

Masurat din 2 bias-uri (zgomot de citire) si 3 lights (4, 40, 80), pe fiecare canal CFA, in ADU 12-bit:

| Canal | zgomot citire | fond cer (peste bias) | varianta shot / varianta citire | penalizare zgomot |
|---|---|---|---|---|
| R | 3.3 | 33 -> 45 | 9 -> 11 | 4.5-5.4 % |
| G | 2.9 | 162 -> 231 | 42 -> 58 | ~1 % |
| B | 3.3 | 183 -> 258 | 42 -> 56 | ~1 % |

- In G si B fondul "ineaca" zgomotul de citire de 40-60 de ori: 120 s la ISO 1600 e corect expus pentru cerul asta.
- In R (unde e Ha, deci Veil-ul) raportul e doar 9-11: la limita regulii uzuale (>= 10). D3300 nemodificat lasa
  ~20-25 % din Ha; subexpuneri mai lungi (180-240 s) ar ajuta rosul marginal (penalizare 5 % -> 2-3 %), dar
  montura nu tine curat nici 120 s (vezi 4). ISO mai mare nu ajuta: zgomotul de citire in electroni nu mai scade.
- Saturare: doar stelele luminoase (median 430 pixeli saturati pe sub-canal verde, adica cateva stele); nebuloasa
  nu e taiata. Negru: minim 147 la black 150, nimic taiat.
- Fondul creste monoton pe noapte: G 126 -> 177, B 143 -> 200, R 27 -> 37 (+40 %); gradientul intre colturi creste
  de la 10 la 24 ADU. E campul care coboara spre orizont (+ poluare luminoasa), nu roua. Cadrul 75 are un salt
  tranzitoriu de +11 % (180 vs 162 la vecini): o lumina de moment sau un nor subtire; e oricum la limita (1.50).

## 3. Roua

Nu. Trei semne, toate negative: FWHM constant; raportul halo/nucleu la cele mai luminoase 40 de stele scade
(0.027 in primele cadre bune -> 0.012 in ultimele); numarul de stele detectate scade de la 15 600 la 12 400, dar
exact in pasul cresterii fondului (+40 %), care ridica pragul de detectie.

## 4. Cadre miscate - problema principala

Alungirea mediana a stelelor (axa mare / axa mica, 1.0 = rotund) pe fiecare cadru, din cele mai luminoase 400 de
stele nesaturate:

- **48 bune** (< 1.35): 3, 4, 5, 6, 7, 9, 12, 21, 24, 28, 29, 30, 32, 33, 35, 37, 39, 40, 41, 42, 44, 46, 47, 48, 49, 53, 55, 57, 58, 59, 61, 62, 64, 65, 66, 67, 72, 73, 74, 77, 78, 79, 80, 81, 83, 84, 85, 87
- **17 la limita** (1.35-1.6): 2, 8, 10, 11, 14, 16, 19, 26, 31, 34, 36, 51, 69, 71, 75, 82, 86
- **22 slabe** (>= 1.6): 1, 13, 15, 17, 18, 20, 22, 23, 25, 27, 38, 43, 45, 50, 52, 54, 56, 60, 63, 68, 70, 76

Ce arata:

- Directia alungirii e aceeasi pe toate cadrele slabe: unghi de pozitie +/-85-90 grade, adica axa verticala (Y) a
  imaginii, cu coerenta 0.92-0.99 intre stele. Din plate-solve-ul tau (`result-bge-cc.fit`, PC1_2 = -1) axa Y a
  imaginii este RA. Deci toate miscarile sunt pe RA = urmarirea monturii, nu vant si nu flexiune.
- Lungimea darei pe cadrele slabe: 5-8 px = 30-48" in 120 s (decupajele din `crops_*.png`). Pe 17 si 23 stelele
  sunt de-a dreptul dublate: un salt brusc in timpul expunerii, nu o alunecare continua.
- Nu e de la dithering-ul manual: cadrele slabe sunt la fel de des primul cadru dupa pauza (10) ca si al doilea,
  fara pauza (11). Si cadrele bune au o usoara alungire coerenta pe RA (1.15-1.2), explicata de driftul din 5.
- Cea mai probabila cauza: eroarea periodica a melcului RA al GTi (perioada ~10 min, esantionata la 2.35 min -
  de aici alternanta bun/slab), eventual agravata de echilibrare. Un test simplu data viitoare: 20 x 30 s continuu,
  fara dithering; din ele se poate trasa curba de eroare periodica.
- Siril a respins doar 7 cadre (17, 22, 23, 43, 50, 54, 56); in stiva de 80 au ramas inca 15 cu alungire >= 1.6.
  Criteriul "roundness" al Siril nu le prinde (pentru 13 si 15 raporteaza 0.96, adica rotund), si nici wFWHM nu
  separa curat (cadre slabe cu wFWHM 3.6-8.9, cadre bune pana la 5.5). Selectia merge facuta pe lista de mai sus:
  fara cele 22 slabe raman 65 cadre (2 h 10 min); strict, doar cele 48 bune (1 h 36 min) dau stelele cele mai mici.

## 5. Drift si rotatie de camp

- Intre cele doua cadre ale unei perechi (fara dithering intre ele, 0.7-0.9 s pauza) pozitia se muta constant cu
  -1.9 px pe RA si -0.4 px pe DEC per 141 s, indiferent de directia dithering-ului dinainte si la fel toata noaptea:
  ~0.8 px/min (5"/min) pe RA.
- Rotatia de camp fata de cadrul 1: 8.5' in 3 h 20 min (2.5'/h). Intr-o expunere de 120 s, in colt, inseamna sub
  0.1 px: nimic.
- Amandoua sunt consistente cu o aliniere polara gresita cu ~10-20'. Nu strica la 120 s (da alungirea de baza
  1.15-1.2 a cadrelor bune), dar o aliniere mai buna le-ar face mai rotunde. Siril corecteaza rotatia.

## 6. Dithering - masurat

Pozitia fiecarui cadru fata de cadrul 1 s-a obtinut prin potrivirea a ~500 stele pe cadru (reziduu 0.3-0.8 px);
coincide cu matricea de inregistrare a Siril din `process/pp_light_.seq`. Harta e in `dither_map.png`, pasii in
`dither_steps.png`.

Ritm:
- 47 pauze (dithering manual) la 87 cadre: 40 perechi + 7 cadre singure. Pauza mediana 27 s (10-140 s). Cele doua
  cadre ale unei perechi stau in acelasi loc (pas median 2.2 px, doar driftul). Deci cel mult 44 de pozitii
  distincte pentru 87 de cadre.

Marime (pasul de la un cadru la urmatorul, dupa o pauza):
- median 12.5 px (74"); 9 pasi de 3-5 px (cadrele 9, 11, 41, 53, 59, 63, 65, 82, 86), 7 pasi de 5-10 px,
  31 pasi >= 10 px, din care 8 >= 20 px.

Directie:
- aproape numai pe RA (Y): |dY| median 9.6 px, |dX| median 0.9 px. Pe DEC (X) sunt doar 9 salturi de 12-29 px,
  toate in acelasi sens (+X), cam la 8 cadre. Pe RA pasii alterneaza semnul, asa ca pozitiile se intorc peste
  cele vechi. Harta e o "scara": coloane verticale de pozitii, la 20-30 px una de alta.

Repetari (cat de departe e un cadru de cel mai apropiat alt cadru, ignorand geamanul din pereche):
- median 4.1 px; 26 de cadre au alt cadru la sub 3 px, 55 la sub 5 px; doar 16 cadre sunt la >= 10 px de orice alt
  cadru.
- Pozitii distincte: 38 la rezolutie de 5 px, 25 la 10 px (din 87).
- Portiuni cu dithering prea rar sau prea mic (cadre consecutive care raman in 5 px de primul): 8-12 (5 cadre),
  13-16 (4), 61-67 (7 cadre = 16 min), 80-83 (4); la 10 px: 39-44 (6), 61-70 (10 cadre = 24 min), 73-77 (5),
  47-50, 84-87.

Concluzie: pentru pixeli calzi e suficient (orice >= 2-3 px rupe coincidenta, si sigma clipping pe 80 cadre
scoate 2 valori aberante). Pentru walking noise / banding nu: modelul e 1D, pe jumatate din pasi sub 10 px, si
fiecare pozitie e folosita de doua ori.

Data viitoare:
- dithering dupa **fiecare** cadru, nu la doua;
- 15-30 px (1.5-3') pe **ambele** axe, semn aleator pe amandoua (nu DEC mereu in acelasi sens);
- tinta: niciun alt cadru la sub 10 px. Cu 87 cadre intr-un patrat de 150 x 200 px asta e usor de atins daca
  pasii sunt pe 2D;
- daca montura e condusa din NINA/APT (camera pe USB, montura pe SynScan WiFi), dithering-ul devine automat si
  aleator, si pauza de linistire e respectata de fiecare data.

## 7. Altele de verificat / de invatat

1. **Montura, nu optica, limiteaza sesiunea.** 25 % cadre de aruncat si 20 % la limita, toate pe RA. Optiuni, in
   ordinea castigului: ghidaj (GTi are port ST-4 si ghidaj prin SynScan; un 30 mm + camera de ghidaj ajung la
   135 mm); subexpuneri de 60 s (dara se injumatateste, penalizarea de zgomot pe R creste de la 5 % la ~10 %,
   acceptabil); echilibrare usor "spre est" ca melcul sa stea incarcat; cuplajul RA strans; testul de eroare
   periodica de la 4.
2. **Re-stivuire** cu selectia de la 4 in loc de filtrul Siril: 65 cadre (sau 48 strict) vor da stele mai mici decat
   cele 80 de acum, cu pierdere de 25-45 % din timp.
3. **Aliniere polara** ceva mai buna (5): elimina alungirea de baza si driftul de 0.8 px/min.
4. **Expunere**: 120 s / ISO 1600 e corect; cu ghidaj, 180-240 s ar ajuta canalul rosu, care e cel care conteaza
   la Veil pe o camera nemodificata.
5. **Nemasurabil din NEF**: temperatura senzorului (D3300 nu o scrie in EXIF) si praful (se vede pe flat-uri, nu pe
   lights). Daca vrei, urmatoarea analiza poate lua si flat-urile si bias-urile din sesiune pentru praf si
   stabilitatea bias-ului.
6. Darele de pe 2 si 36 nu cer nimic: sigma clipping le scoate.

## 8. Tabel pe cadre

Clasa: bun < 1.35, mediu 1.35-1.6, SLAB >= 1.6 (alungire mediana). "pauza" = secunde intre sfarsitul cadrului
anterior si inceputul acestuia (0-1 s = fara dithering). "pas dither" = cat s-a mutat campul fata de cadrul anterior.
"cel mai apropiat cadru ne-geaman" = distanta la cel mai apropiat cadru care nu e perechea fara pauza.

Siril wFWHM of my good frames (max 8): [4.9, 5.0, 5.2, 5.2, 5.2, 5.3, 5.4, 5.5]
Siril wFWHM of marginal: [4.7, 4.7, 4.7, 4.8, 4.9, 5.0, 5.1, 5.2, 5.3, 5.4, 5.4, 5.4, 5.6, 5.8, 6.1, 6.4, 6.4]
corr(el, wfwhm)= -0.063
wFWHM<5.0 keeps 55; among them bad 9, marginal 5; excluded good 7
wFWHM<5.5 keeps 71; among them bad 11, marginal 12; excluded good 0
wFWHM<6.0 keeps 74; among them bad 12, marginal 14; excluded good 0

| cadru | ora | pauza (s) | clasa | alungire | FWHM px | Siril wFWHM | in stack Siril | pas dither (px) | cel mai apropiat cadru ne-geaman (px, cadru) | fond G | note |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 23:35:03 | - | SLAB | 1.68 | 3.85 | 3.6 | da | - | 5.5 (2) | 126 |  |
| 2 | 23:37:51 | 48 | mediu | 1.38 | 3.18 | 4.7 | da | 5.5 | 5.5 (1) | 129 | dara satelit/avion |
| 3 | 23:40:14 | 23 | bun | 1.28 | 3.12 | 4.8 | da | 12.7 | 12.6 (1) | 129 |  |
| 4 | 23:43:43 | 88 | bun | 1.15 | 3.06 | 4.5 | da | 21.0 | 5.6 (7) | 120 |  |
| 5 | 23:46:32 | 49 | bun | 1.16 | 3.00 | 4.3 | da | 8.7 | 3.5 (11) | 129 |  |
| 6 | 23:48:48 | 15 | bun | 1.28 | 3.19 | 5.2 | da | 12.0 | 6.5 (7) | 129 |  |
| 7 | 23:51:27 | 39 | bun | 1.17 | 3.14 | 4.4 | da | 6.5 | 5.6 (4) | 123 |  |
| 8 | 23:53:41 | 14 | mediu | 1.58 | 3.57 | 6.4 | da | 13.0 | 2.4 (12) | 129 |  |
| 9 | 23:56:08 | 26 | bun | 1.17 | 3.08 | 4.6 | da | 3.6 | 1.8 (12) | 123 |  |
| 10 | 23:58:09 | 1 | mediu | 1.51 | 3.56 | 4.8 | da | 2.3 | 3.2 (12) | 126 |  |
| 11 | 00:00:48 | 39 | mediu | 1.50 | 3.40 | 5.4 | da | 5.0 | 3.1 (9) | 126 |  |
| 12 | 00:02:49 | 1 | bun | 1.19 | 3.10 | 4.3 | da | 1.8 | 1.8 (9) | 126 |  |
| 13 | 00:05:19 | 29 | SLAB | 1.73 | 3.78 | 7.0 | da | 12.3 | 3.1 (16) | 126 |  |
| 14 | 00:07:20 | 1 | mediu | 1.42 | 3.46 | 4.7 | da | 2.0 | 1.2 (16) | 126 |  |
| 15 | 00:09:43 | 23 | SLAB | 2.09 | 4.34 | 7.0 | da | 6.2 | 4.4 (13) | 129 |  |
| 16 | 00:11:45 | 1 | mediu | 1.43 | 3.45 | 4.7 | da | 7.3 | 1.2 (14) | 129 |  |
| 17 | 00:14:16 | 31 | SLAB | 2.31 | 4.95 | 0.0 | NU | 28.0 | 27.3 (20) | 129 | stele dublate (salt) |
| 18 | 00:16:17 | 1 | SLAB | 2.08 | 4.37 | 5.3 | da | 1.0 | 26.9 (13) | 129 |  |
| 19 | 00:19:07 | 50 | mediu | 1.49 | 3.38 | 5.4 | da | 27.8 | 4.7 (23) | 132 |  |
| 20 | 00:21:08 | 1 | SLAB | 1.90 | 4.13 | 6.1 | da | 0.4 | 5.1 (23) | 132 |  |
| 21 | 00:24:05 | 57 | bun | 1.19 | 3.10 | 4.5 | da | 10.9 | 1.0 (24) | 132 |  |
| 22 | 00:26:06 | 1 | SLAB | 1.77 | 4.15 | 0.0 | NU | 5.9 | 6.8 (24) | 135 |  |
| 23 | 00:28:28 | 22 | SLAB | 2.24 | 4.89 | 0.0 | NU | 11.7 | 4.7 (19) | 135 | stele dublate (salt) |
| 24 | 00:30:29 | 1 | bun | 1.27 | 3.22 | 4.8 | da | 5.0 | 1.0 (21) | 135 |  |
| 25 | 00:32:58 | 29 | SLAB | 1.68 | 3.71 | 5.4 | da | 29.9 | 4.6 (30) | 138 |  |
| 26 | 00:34:59 | 1 | mediu | 1.37 | 3.25 | 4.9 | da | 1.1 | 3.6 (30) | 138 |  |
| 27 | 00:37:19 | 19 | SLAB | 1.66 | 3.66 | 6.2 | da | 17.1 | 13.0 (29) | 138 |  |
| 28 | 00:39:20 | 1 | bun | 1.23 | 3.18 | 4.5 | da | 2.7 | 15.7 (29) | 138 |  |
| 29 | 00:41:43 | 23 | bun | 1.14 | 3.06 | 4.1 | da | 15.7 | 4.2 (26) | 141 |  |
| 30 | 00:43:44 | 1 | bun | 1.31 | 3.17 | 4.6 | da | 1.0 | 3.6 (26) | 141 |  |
| 31 | 00:46:06 | 22 | mediu | 1.43 | 3.40 | 5.1 | da | 20.9 | 11.5 (35) | 138 |  |
| 32 | 00:48:07 | 1 | bun | 1.17 | 3.06 | 4.4 | da | 1.6 | 10.0 (35) | 138 |  |
| 33 | 00:50:42 | 34 | bun | 1.17 | 3.11 | 4.8 | da | 27.5 | 15.3 (36) | 138 |  |
| 34 | 00:52:43 | 1 | mediu | 1.50 | 3.54 | 5.0 | da | 1.0 | 14.4 (36) | 138 |  |
| 35 | 00:55:10 | 26 | bun | 1.19 | 3.14 | 4.6 | da | 16.6 | 10.0 (32) | 138 |  |
| 36 | 00:57:11 | 1 | mediu | 1.43 | 3.38 | 5.4 | da | 2.2 | 12.2 (32) | 138 | dara satelit/avion |
| 37 | 00:59:34 | 23 | bun | 1.23 | 3.16 | 4.6 | da | 23.8 | 13.7 (44) | 141 |  |
| 38 | 01:01:35 | 1 | SLAB | 1.80 | 3.92 | 7.0 | da | 2.3 | 11.6 (44) | 141 |  |
| 39 | 01:03:52 | 17 | bun | 1.19 | 3.11 | 4.6 | da | 16.6 | 2.9 (51) | 141 |  |
| 40 | 01:05:53 | 1 | bun | 1.15 | 3.09 | 4.6 | da | 2.7 | 1.5 (51) | 141 |  |
| 41 | 01:08:26 | 32 | bun | 1.27 | 3.23 | 4.8 | da | 4.1 | 4.1 (40) | 141 |  |
| 42 | 01:10:27 | 1 | bun | 1.16 | 3.07 | 4.4 | da | 2.8 | 4.8 (40) | 141 |  |
| 43 | 01:12:48 | 21 | SLAB | 1.79 | 4.00 | 0.0 | NU | 10.7 | 7.7 (39) | 144 |  |
| 44 | 01:14:49 | 1 | bun | 1.22 | 3.12 | 5.3 | da | 0.5 | 8.1 (39) | 141 |  |
| 45 | 01:17:09 | 20 | SLAB | 1.77 | 3.89 | 6.0 | da | 13.4 | 10.3 (39) | 144 |  |
| 46 | 01:19:10 | 1 | bun | 1.20 | 3.10 | 4.4 | da | 3.0 | 8.8 (39) | 144 |  |
| 47 | 01:21:56 | 45 | bun | 1.24 | 3.28 | 4.4 | da | 15.0 | 5.5 (54) | 144 |  |
| 48 | 01:23:57 | 1 | bun | 1.16 | 3.08 | 4.7 | da | 2.3 | 4.8 (53) | 144 |  |
| 49 | 01:26:58 | 62 | bun | 1.19 | 3.12 | 5.0 | da | 6.4 | 2.3 (58) | 144 |  |
| 50 | 01:29:00 | 1 | SLAB | 1.77 | 3.91 | 0.0 | NU | 3.5 | 2.8 (54) | 147 |  |
| 51 | 01:31:12 | 12 | mediu | 1.50 | 3.47 | 5.3 | da | 13.4 | 1.5 (40) | 147 |  |
| 52 | 01:33:13 | 1 | SLAB | 1.98 | 4.26 | 7.9 | da | 1.1 | 1.8 (40) | 147 |  |
| 53 | 01:35:34 | 21 | bun | 1.17 | 3.16 | 4.6 | da | 4.2 | 2.3 (57) | 147 |  |
| 54 | 01:37:35 | 1 | SLAB | 1.70 | 3.98 | 0.0 | NU | 9.2 | 2.8 (50) | 147 |  |
| 55 | 01:39:53 | 18 | bun | 1.17 | 3.10 | 4.7 | da | 9.8 | 4.6 (63) | 147 |  |
| 56 | 01:41:54 | 1 | SLAB | 2.10 | 4.46 | 0.0 | NU | 0.8 | 3.7 (63) | 147 |  |
| 57 | 01:44:18 | 24 | bun | 1.25 | 3.16 | 4.7 | da | 18.1 | 2.3 (53) | 150 |  |
| 58 | 01:46:19 | 1 | bun | 1.15 | 3.08 | 4.2 | da | 2.3 | 2.3 (49) | 147 |  |
| 59 | 01:48:29 | 10 | bun | 1.21 | 3.11 | 4.5 | da | 3.0 | 3.0 (58) | 150 |  |
| 60 | 01:50:30 | 1 | SLAB | 1.78 | 4.08 | 4.5 | da | 4.1 | 3.1 (49) | 150 |  |
| 61 | 01:52:46 | 16 | bun | 1.33 | 3.30 | 5.2 | da | 17.4 | 1.5 (65) | 150 |  |
| 62 | 01:54:47 | 1 | bun | 1.28 | 3.20 | 4.9 | da | 1.6 | 2.3 (66) | 150 |  |
| 63 | 01:57:06 | 18 | SLAB | 2.05 | 4.43 | 8.9 | da | 3.1 | 3.0 (61) | 153 |  |
| 64 | 01:59:07 | 1 | bun | 1.33 | 3.32 | 4.8 | da | 4.5 | 0.3 (67) | 153 |  |
| 65 | 02:01:34 | 27 | bun | 1.23 | 3.15 | 4.8 | da | 4.1 | 1.5 (61) | 156 |  |
| 66 | 02:03:35 | 1 | bun | 1.31 | 3.26 | 5.2 | da | 1.2 | 1.6 (61) | 153 |  |
| 67 | 02:06:36 | 61 | bun | 1.20 | 3.10 | 4.5 | da | 5.4 | 0.3 (64) | 156 |  |
| 68 | 02:08:37 | 1 | SLAB | 1.70 | 3.85 | 5.8 | da | 1.3 | 1.3 (64) | 156 |  |
| 69 | 02:11:24 | 47 | mediu | 1.46 | 3.45 | 6.1 | da | 13.3 | 4.7 (55) | 156 |  |
| 70 | 02:13:25 | 1 | SLAB | 1.94 | 4.19 | 8.9 | da | 1.5 | 4.5 (56) | 159 |  |
| 71 | 02:16:10 | 45 | mediu | 1.49 | 3.46 | 5.2 | da | 18.9 | 14.6 (55) | 159 |  |
| 72 | 02:18:11 | 1 | bun | 1.16 | 3.03 | 4.2 | da | 2.3 | 13.7 (75) | 159 |  |
| 73 | 02:20:26 | 14 | bun | 1.29 | 3.26 | 5.4 | da | 20.5 | 5.5 (76) | 162 |  |
| 74 | 02:22:27 | 1 | bun | 1.19 | 3.11 | 4.9 | da | 3.4 | 2.2 (76) | 162 |  |
| 75 | 02:24:54 | 28 | mediu | 1.50 | 3.47 | 5.8 | da | 10.4 | 7.1 (73) | 180 | fond +11% (tranzitoriu) |
| 76 | 02:29:28 | 140 | SLAB | 1.71 | 3.80 | 8.2 | da | 12.2 | 2.2 (74) | 162 |  |
| 77 | 02:31:29 | 1 | bun | 1.16 | 3.09 | 4.4 | da | 1.6 | 3.7 (74) | 165 |  |
| 78 | 02:33:43 | 14 | bun | 1.16 | 3.09 | 4.4 | da | 16.6 | 8.3 (83) | 165 |  |
| 79 | 02:35:44 | 1 | bun | 1.18 | 3.11 | 4.5 | da | 1.9 | 8.1 (83) | 165 |  |
| 80 | 02:39:58 | 133 | bun | 1.34 | 3.30 | 5.5 | da | 12.5 | 3.8 (82) | 168 |  |
| 81 | 02:41:59 | 1 | bun | 1.20 | 3.09 | 4.6 | da | 2.2 | 3.2 (83) | 171 |  |
| 82 | 02:44:26 | 27 | mediu | 1.59 | 3.62 | 6.4 | da | 4.5 | 3.8 (80) | 174 |  |
| 83 | 02:46:27 | 1 | bun | 1.13 | 3.06 | 4.3 | da | 3.6 | 3.2 (81) | 174 |  |
| 84 | 02:49:04 | 36 | bun | 1.21 | 3.15 | 4.8 | da | 14.3 | 3.8 (86) | 174 |  |
| 85 | 02:51:05 | 1 | bun | 1.23 | 3.14 | 4.6 | da | 0.5 | 4.1 (86) | 174 |  |
| 86 | 02:53:48 | 43 | mediu | 1.48 | 3.48 | 5.6 | da | 4.1 | 3.8 (84) | 177 |  |
| 87 | 02:55:49 | 1 | bun | 1.25 | 3.15 | 4.7 | da | 2.2 | 5.3 (84) | 177 |  |
