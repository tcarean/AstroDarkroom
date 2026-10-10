# lightscheck

Verificare de calitate pentru light frames de astrofoto, pentru un DSLR pe un star tracker
(Nikon D3300 cu Samyang 135 pe Star Adventurer GTi, flux Siril). Citeste folderul unei sesiuni
Siril, masoara fiecare cadru si spune ce cadre sa scoti din stiva si de ce, cat de des si cat
de mult ai facut dithering, si ce sa faci altfel data viitoare. Pozele sunt doar citite: nimic
nu e modificat, mutat sau scris in `lights/`.

Consola `lightscheck.exe` si fereastra `LightsCheck.Wpf.exe` stau peste acelasi nucleu si dau
aceleasi cifre. Aceeasi licenta (GPLv3 sau ulterioara, [LICENSE](LICENSE)), acelasi mod de
versionare si aceeasi fereastra "Despre" ca FlatCheck, de unde sunt copiate si cititoarele de
raw si FITS (vezi [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)).

**Stadiu: prima versiune.** Consola face inventarul, masoara fiecare cadru si analizeaza seria
(dithering, drift, cadre de respins); fereastra arata aceleasi rezultate. Cifrele sunt
verificate pe sesiunea Veil din 2026-10-06 fata de analiza facuta de mana (`docs/validation.md`).

> **Documentatia de utilizator sta aici, in acest fisier.** Nu o duplica in comentarii.

---

## Ce folder ii dai

Folderul sesiunii Siril, cel care are `lights/` si, optional, `biases/` si `darks/`:

```
2026-10-06-3h\
    lights\     DSC_0001.NEF ... DSC_0087.NEF     <- obligatoriu (sau cadrele direct in folder)
    biases\     DSC_0128.NEF ...                  <- optional
    darks\      DSC_0160.NEF ...                  <- optional, doar numarate, niciodata citite
    flats\      ...                               <- neatins
    process\    ...                               <- neatins
```

Daca folderul dat nu are `lights/` dar are cadre direct in el, acelea sunt lights-urile. Un
`lights/` gol e eroare, nu se cade pe radacina sesiunii (acolo stau `result.fit` si celelalte
rezultate ale stivuirii).
Cadrele sunt raw-urile de aparat (`.nef`, `.cr2`, `.cr3`, `.arw`, `.dng`, `.raf`) daca exista,
altfel fisierele FITS (`.fit`, `.fits`, `.fts`); cele doua nu se amesteca. Ordinea e cea a
numelor de fisier.

## Folosire

```
lightscheck <folder sesiune> [--out DIR] [--threads N] [--pixel UM]
lightscheck <folder sesiune> --inventory
lightscheck <folder sesiune> --frame N
lightscheck --help
```

Fiecare rulare incepe cu `lightscheck <versiune>` si notita de copyright. Codul de iesire e
`0` cand toate cadrele s-au citit si `1` la eroare (folder lipsa, niciun cadru, un cadru care
nu se poate citi; cadrul e listat cu `eroare:` si restul sunt analizate in continuare).

### Toata sesiunea (fara optiuni)

Masoara fiecare cadru (in paralel, `--threads N`, implicit jumatate din nuclee; 87 de cadre
de 24 MP in cam 90 s pe 4 fire), le inregistreaza pe primul si tipareste raportul:

- **REZUMAT**: focus, expunere (fata de zgomotul de citire din `biases/`), roua, cadre
  miscate, dithering, cate cadre scoti din stiva si cate raman.
- **FOCUS**: FWHM median pe cadrele bune, la inceput si la sfarsit, pe trei zone radiale, si
  verdictul (acelasi ca pe randul `focus` din REZUMAT).
- **EXPUNERE SI ZGOMOT**: fondul de cer, zgomotul de citire pe fiecare canal, si pe trei
  cadre raportul "shot/citire" pe canal: peste 10, cerul ineaca zgomotul de citire; sub,
  canalul e limitat de citire si subexpunerile mai lungi ar ajuta.
- **ROUA**: halo-ul si numarul de stele la inceput si la sfarsit.
- **CADRE MISCATE**: cadrele slabe (alungire >= 1.6) si la limita (1.35..1.6), directia
  darelor si care axa a imaginii e RA (din `result.fit` plate-solvat din folderul sesiunii,
  daca exista, altfel din directia darelor). Cand axa RA se stie si stelele cadrului sunt de
  acord intre ele (coerenta peste 0.8), motivul fiecarui cadru cu dara spune si pe ce axa a
  monturii cade ea: `dara pe RA (-90 grade)` inseamna urmarirea (eroare periodica, viteza,
  backlash pe RA); `pe DEC`, alinierea polara, vantul sau o atingere a monturii; `oblica`,
  amandoua sau o lovitura. Fara axa ramane doar unghiul: `dara la -90 grade`.
- **DRIFT SI ROTATIE**: driftul dintre cadrele consecutive fara dithering, driftul intr-o
  expunere (pe ce axa a monturii) si dara de walking noise pe care o lasa in cel mai lung grup
  de cadre consecutive ramase in acelasi loc la 10 px, de la 2 cadre in sus, rotatia de camp.
- **DITHERING**: cate pauze si cate miscari (>= 5 px), la cate cadre o data, cat de mari, pe
  ce axe si in ce sens, cat de aproape sunt cadrele unul de altul (pozitii distincte,
  grupuri de cadre consecutive in acelasi loc), `scara` (cat inseamna pe cer, in secunde de
  arc, o miscare de 5 si de 10 px la focala sesiunii), `pattern in stiva` (cat din pattern-ul
  unui cadru ramane in medie dupa stivuire, fata de 1 / cadre, idealul in care niciun cadru nu
  sta peste altul), `pattern fix` (cat de mare e pattern-ul fix al senzorului, citit din
  mediana cadrelor neinregistrate: sigma pe fiecare canal, in ADU si ca procent din zgomotul
  unui cadru, pixelii fierbinti pe megapixel, si daca sesiunea are dark-uri care sa-l scoata)
  si verdictul pentru pixeli calzi si walking noise, cu cifrele lui.
  Verdictul de walking noise citeste si driftul: `OK` cand dithering-ul e des, pe ambele axe
  si cu pozitii distincte; `FARA walking noise` cand dara pe care o trage driftul in cel mai
  lung grup de cadre la 10 px e sub 3 px, oricum ar arata harta (la 20 mm, un drift de 3"/min
  e a cincisprezecea parte dintr-un pixel pe cadru si pattern-ul nu merge nicaieri);
  `INSUFICIENT` altfel, cu dara in pixeli si pe ce axa.

  Walking noise-ul e pattern-ul fix al senzorului (pixelii calzi, restul de dark) tras in
  dare: driftul il muta de la un cadru la altul si stiva aduna copiile pe traseul lui, in
  grupurile de cadre consecutive ramase in acelasi loc. Aceeasi deriva a monturii si aceeasi
  miscare de dithering sunt de atatea ori mai multi pixeli cu cat focala e mai lunga: focala
  scurta iarta, focala lunga cere dithering la fiecare cadru, pe ambele axe, in directie
  aleatoare; frecventa conteaza mai mult decat marimea, 5 px ajung cand doua cadre vecine nu
  mai impart acelasi loc. Dark-urile scot pattern-ul inainte sa apuce sa mearga si nu cer
  demontarea obiectivului sau a filtrului: scoti obiectivul cu tot cu filtru, pui capacul de
  body, inchizi ocularul vizorului si tragi 20 de cadre la acelasi ISO, acelasi timp si aceeasi
  temperatura, la finalul sesiunii; flat-urile raman valabile. Fereastra spune acelasi lucru
  sub cardul DITHERING.
- **RESPINSE**: fiecare cadru slab cu motivul, pragul si axa darei.
- **TABEL PE CADRE**: o linie pe cadru. Coloana `dithering` spune ce a facut montura la
  fiecare miscare de dithering: pe ce axa, cati pixeli, in ce sens pe cer si ce sageata din
  SynScan e aceea, de exemplu `RA 15 px ← V, DEC 12 px ↓ S`: sageata apasata, apoi sensul pe
  cer (E/V, N/S). Sensul vine din `result.fit` plate-solvat (Siril scrie FITS-ul cu randurile
  de jos in sus fata de raw, si programul tine cont de asta; verificat pe sesiunea Veil cu plate
  solve pe doua cadre brute); fara el ramane doar `RA 15 px, DEC 12 px`. Care sageata muta
  montura spre est si care spre nord depinde de setarile aplicatiei SynScan: implicit `→` est
  (deci `←` vest) si `↑` nord (deci `↓` sud); le schimbi cu `--est stanga|dreapta` si
  `--nord sus|jos`, iar in fereastra din selectoarele EST si NORD, tinute minte intre porniri
  in `%LOCALAPPDATA%\LightsCheck\settings.json`. Coloana `acelasi loc` spune al catelea cadru
  consecutiv e in acelasi loc (la 10 px), din cate si de la care: `7/10 de la 61` e al
  saptelea din zece cadre ramase pe loc de la cadrul 61. Sirurile astea fac walking noise,
  dar cadrul ramane bun si in stiva: leacul e dithering mai des data viitoare, nu mai putine
  cadre.

`--out DIR` scrie in DIR raportul (`lightscheck-raport.txt`), un CSV cu toate cifrele pe
cadru (`lightscheck-cadre.csv`, cu axa darei in `trail_on`, sirul din `acelasi loc` in
`spot_run_index`, `spot_run_frames`, `spot_run_first`, si miscarea de dithering in
`move_ra_px`, `move_dec_px`, pixeli cu semn, plus spre est si spre nord, si `move_arrows`) si
lista numelor de respins (`lightscheck-respinse.txt`),
cate unul pe linie, de scos din secventa in Siril. Nimic nu se scrie in `lights/`.
`--pixel UM` e marimea pixelului in microni cand fisierele n-o spun (implicit 3.92, D3300);
cu focala din raw da scara in "/px. `--est stanga|dreapta` si `--nord sus|jos` spun care
sageata SynScan muta montura spre est si care spre nord, pentru coloana `dithering`. Sagetile
sunt caractere Unicode: in fereastra consolei se vad (programul o trece pe UTF-8 cat ruleaza);
daca trimiti iesirea intr-un fisier cu `>` din Windows PowerShell 5.1, ele ies stricate, asa ca
pentru fisiere foloseste `--out`, ale carui fisiere sunt UTF-8.

### Doar inventarul: `--inventory`

Citeste metadatele fara sa decodeze pixelii (o secunda pentru 87 de cadre):

```
=== SESIUNE: E:\Photografie\Astro\Veil\2026-10-06-3h ===
    lights             : lights\ (87 cadre .NEF)
    biases             : biases\ (30 cadre .NEF)
    aparat             : Nikon D3300
    obiectiv           : 135 mm f/2.8 (poza), obiectiv 135 mm f/2.0, ID Nikon 74
    expunere           : 120.2 s la ISO 1600 (toate cadrele)
    interval           : 2026-10-06 23:35:03 -> 2026-10-07 02:57:49 (3 h 22 min); integrare 2 h 54 min din 87 cadre (86 % din interval)

  CADRE
     nr  fisier                ora       expunere   ISO    f/   focala   pauza
      1  DSC_0001.NEF          23:35:03     120.2   1600   2.8   135 mm       -
      2  DSC_0002.NEF          23:37:51     120.2   1600   2.8   135 mm    48 s
```

`pauza` e timpul dintre sfarsitul expunerii anterioare si inceputul acesteia: la dithering
manual e pauza in care ai miscat montura. Ora vine din EXIF (ora aparatului) pe un raw si din
`DATE-OBS` (UTC, cum il scrie Siril) pe un FITS.

### Un singur cadru: `--frame N`

```
lightscheck <folder sesiune> --frame 4
```

Decodeaza al N-lea cadru din `lights/` (1 = primul) si il masoara:

```
=== CADRU 4: DSC_0004.NEF ===
    dimensiune         : 6016 x 4016 px; scara 150 .. 4095 ADU (libraw (exact))
    nivele CFA         : R 25 (saturati 63, min -3)  G1 120 (saturati 414, min 1)  G2 120 ...  [ADU peste black]
    fond verde         : median 120, centru 165, colturi 76 .. 86 (dale 71 .. 171); zgomot sigma 13.1
    stele              : 15132 detectate, 2104 luminoase, 357 saturate; 398 masurate
    FWHM               : 3.06 px median (p90 3.64); centru 3.15 (95), mijloc 3.08 (202), margine 2.90 (101)
    alungire           : 1.15 mediana (p90 1.39); 14 % peste 1.3; unghi 86 grade, coerenta 0.36; ...
    halo               : 0.019 (inel 5-10 px / nucleu 3 px, la cele mai luminoase 40 stele)
    dare               : 0
    inregistrare       : 600 stele retinute
```

- `nivele CFA`: mediana fiecarei pozitii Bayer peste black, cati pixeli ating varful scarii
  (stelele luminoase; nebuloasa nu ar trebui sa apara aici), minimul (sub 0 = zgomot, nu
  taiere in negru).
- `fond verde`: fondul de cer pe verde, din mediane pe dale de 128 px; `centru` vs `colturi`
  arata vignetarea si gradientul; `zgomot sigma` e abaterea robusta a pixelului fata de fond.
- `stele`: cate componente de marimea unei stele s-au detectat la 5 sigma, cate au varful
  peste 50 sigma, cate ating saturatia; `masurate` = cele carora li s-a masurat forma (cele
  mai luminoase 400, nesaturate).
- `FWHM`: latimea stelelor in pixeli (focusul), pe tot cadrul si pe trei zone radiale, cu
  numarul de stele din fiecare in paranteze; margini mai mari decat centrul = coma sau camp
  curb.
- `alungire`: raportul axelor (1 = rotund); `% peste 1.3` cate stele sunt vizibil alungite;
  `unghi` e directia axei mari fata de orizontala, `coerenta` cat de bine se aliniaza stelele
  (aproape de 1 = toate in aceeasi directie, deci urmarirea; mic = fara directie, deci optica
  sau zgomot). Peste 0.8, raportul seriei leaga unghiul de axa monturii (vezi CADRE MISCATE).
- `halo`: cat din lumina unei stele luminoase sta intr-un inel de 5-10 px fata de nucleul de
  3 px; creste cu roua sau ceata.
- `dare`: urme de sateliti sau avioane, cu lungimea si coltul din stanga-sus.

## Fereastra

`LightsCheck.Wpf.exe [folder sesiune]`. Fara argument, cere folderul la pornire. Analiza
porneste singura (aceeasi ca `lightscheck <folder>`, cu bara de progres si buton de oprire) si
rezultatul apare in sase taburi:

- **Rezumat**: sectiunile raportului, rand cu rand, cu verdictul colorat.
- **Cadre**: un rand pe cadru (ora, clasa, alungire, FWHM, stele, fond, pauza, pas, pozitia
  `dX (DEC)` / `dY (RA)` cand se stie axa, `dithering`, `acelasi loc`, motive). Alege un cadru si in dreapta
  apar decupajele stelelor lui (centru si colturi, pe canalul verde): asa vezi cu ochiul dara
  pe care o raporteaza alungirea.
- **Dithering**: harta pozitiilor fata de primul cadru (grila la 10 px, culoarea e clasa
  cadrului, inel in loc de punct = cadru dintr-un sir de 3 sau mai multe in acelasi loc),
  pasul fata de cadrul anterior (galben = miscare >= 5 px; tine mouse-ul pe o bara si vezi
  cadrul, pasul si miscarea monturii din coloana `dithering`), distanta la cel mai apropiat alt
  cadru, si in dreapta cifrele DITHERING: pauzele si miscarile, cel mai lung interval,
  acoperirea, intinderea si verdictul ca randuri, iar cat de des, cat de mult, directia,
  acoperirea pe raze si grupurile ca tabele.
- **Calitate**: FWHM, alungire (cu liniile 1.35 si 1.6), stele si fond de cer, cadru cu cadru.
- **Respinse**: cadrele slabe si cele necitite, cu motivul si pragul, sub o explicatie a
  cuvintelor (alungirea, dara si axa ei; ce inseamna dara pe RA fata de dara pe DEC, pe rand
  separat, ingrosat); cele la limita raman la alegerea ta, lista lor e in CADRE MISCATE.
- **Raport**: textul consolei, identic.

In bara de sus: `PIXEL (um)` (marimea pixelului cand fisierele n-o spun; 3.92 la D3300),
`FIRE` (cate cadre se masoara in paralel; gol = jumatate din nuclee) si `EST` / `NORD`, sagetile
SynScan care muta montura spre est si spre nord (implicit `→` si `↑`), citite la deschiderea
folderului si tinute minte intre porniri in `%LOCALAPPDATA%\LightsCheck\settings.json`, singurul
lucru pe care programul il scrie acolo. `Scrie fisierele...`
scrie, intr-un folder la alegere (propus: folderul sesiunii), aceleasi trei fisiere ca
`--out`. `Despre` (F1) arata versiunea, licenta si componentele terte. Nimic nu se scrie in
`lights/` sau `biases/`.

## Compilare

.NET SDK 9.0.3xx (`global.json`). `dotnet build LightsCheck.sln -c Release` (fereastra cere
Windows; consola singura se compileaza oriunde). Gate-ul complet (format, build strict,
teste): `scripts/verify.ps1`. Publicarea (`scripts/release.ps1`) scrie `lightscheck.exe` si
`LightsCheck.Wpf.exe`, fiecare un singur fisier cu DLL-urile libraw inglobate, in `artifacts/`.

## Pentru cine dezvolta

Regulile de lucru sunt in `CLAUDE.md` si `.claude/rules/kit/process.md`, cerintele cu ID in
`docs/requirements.md`, deciziile in `docs/decisions/`, cifrele masurate pe sesiuni reale in
`docs/validation.md`. Analiza din care a pornit unealta (scripturile Python, raportul pe
sesiunea Veil din 2026-10-06) e in `prototype/` si `docs/validation/2026-10-06-veil/`.
