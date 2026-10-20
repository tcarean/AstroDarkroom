# darkscheck

Verificare de calitate pentru dark frames de astrofoto, pentru un DSLR (Nikon D3300, flux
Siril). Citeste folderul unei sesiuni Siril sau un folder de dark-uri (o biblioteca), repeta
cele noua verificari facute de mana pe sesiunea Smida din 2026-08-12 (metadate si cadenta,
scurgeri de lumina, deriva termica, zgomot, pixeli fierbinti, model fix, pixeli morti, cate
dark-uri ajung, lights-urile fata de dark) si calculeaza indexul: un numar pentru cat de cald a
rulat senzorul, pe dark-uri, pe o biblioteca si pe lights, ca sa potrivesti un master din
biblioteca cu o sesiune. Pozele sunt doar citite: nimic nu e modificat, mutat sau scris in
folderele lor.

Consola `darkscheck.exe` si fereastra `DarksCheck.Wpf.exe` stau peste acelasi nucleu si dau
aceleasi cifre. Aceeasi licenta (GPLv3 sau ulterioara, [LICENSE](LICENSE)), acelasi mod de versionare
si aceeasi fereastra "Despre" ca FlatCheck si LightsCheck, de unde sunt copiate cititoarele de
raw si FITS (vezi [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)).

**Stadiu: planul de constructie e implementat (pasii 1-5 si sarcina 6), versiunea 1.0.0 e
prima.** Consola si fereastra fac inventarul si verificarea 1 (metadate, cadenta, potrivirea
dark-urilor cu lights-urile), verificarile 2-8 pe dark-uri si bias-uri, verificarea 9
(lights-urile fata de dark), indexul, eticheta, potrivirea cu alt folder de dark-uri
(`--against`) si o sesiune cu lights dar fara dark-uri comparata cu o biblioteca
(`docs/plans/2026-10-11-build-darkscheck.md`). Analiza de mana de la care porneste unealta, cu
cifrele ei, e in `docs/validation/2026-08-12-smida/report.md`.

> **Documentatia de utilizator sta aici, in acest fisier.** Nu o duplica in comentarii.

---

## Ce folder ii dai

O sesiune cu `lights\` dar fara `darks\` (lights-urile tale, dark-urile luate in alta zi sau
o biblioteca) merge si ea, cu un partener: `--against <biblioteca>` in consola sau butonul
`Alege partener...` din fereastra. Dark-urile partenerului tin locul dark-urilor sesiunii:
verificarile 2-8 se fac pe ele (afli si cat de buna e biblioteca), verificarea 9 compara
lights-urile cu masterul lor, iar indexul si eticheta sunt ale partenerului; raportul spune
`DARK-URI (ale partenerului ...)` si `darks: (lipsa; se folosesc dark-urile partenerului ...)`, iar
`darkscheck-darks.csv` si hartile din `--out` sunt ale dark-urilor partenerului.
Fara partener, un asemenea folder e refuzat cu un mesaj care spune ce sa dai; `--inventory` il
listeaza oricum, cu `darks: (lipsa)`. Pe sesiunea Veil din 2026-10-06 (87 lights, fara
dark-uri) fata de biblioteca din 2026-10-10: lights / dark 0.43, biblioteca e cu 7.2 grade mai
calda decat noaptea, doar corectie cosmetica.

Folderul sesiunii Siril, cel care are `darks/` si, optional, `biases/` si `lights/`:

```
2026-08-12-Smida\
    darks\      DSC_0034.NEF ... DSC_0053.NEF     <- obligatoriu
    biases\     DSC_0042.NEF ...                  <- optional
    lights\     DSC_0002.NEF ...                  <- optional
    flats\      ...                               <- neatins
    process\    ...                               <- neatins
```

sau un folder care are dark-urile direct in el, o biblioteca:

```
2026-10-10-ISO1600-120s-15.8celsius\darks\   DSC_0009.NEF ... DSC_0038.NEF
```

Un `darks/` gol e eroare, nu se cade pe radacina sesiunii (acolo stau `result.fit` si
celelalte rezultate ale stivuirii); la fel un folder care are `lights/`, `biases/` sau
`flats/` dar nu are `darks/`. Cadrele sunt raw-urile de aparat (`.nef`, `.cr2`, `.cr3`,
`.arw`, `.dng`, `.raf`) daca exista, altfel fisierele FITS (`.fit`, `.fits`, `.fts`); cele
doua nu se amesteca. Ordinea e cea a numelor de fisier.

## Folosire

```
darkscheck <folder sesiune sau folder de dark-uri> [--against DIR] [--out DIR] [--threads N] [--max-frames N]
darkscheck <folder> --inventory
darkscheck --help
```

Fiecare rulare incepe cu `darkscheck <versiune>` si notita de copyright. Codul de iesire e `0`
cand toate cadrele s-au citit si `1` la eroare (folder lipsa, niciun cadru, un cadru care nu se
poate citi sau decoda; cadrul e listat cu `eroare:` si restul sunt analizate in continuare).

### Toate verificarile (fara optiuni)

Decodeaza bias-urile (daca sunt) si apoi dark-urile, `--threads N` deodata (implicit jumatate
din nuclee; progresul apare pe stderr), le tine in memorie ca mozaicuri pe 16 biti (48 MB un
cadru de 24 MP; peste 60 de cadre, sau peste `--max-frames N`, se decodeaza unul din k si
restul sunt listate ca sarite, fara sa fie erori), apoi lights-urile, unul cate unul, fara sa
le tina (acelasi plafon), si tipareste, dupa liniile inventarului, cate o sectiune pe
verificare, fiecare cu cifrele ei si un rand `verdict` care incepe cu `OK`, `ATENTIE` sau
`NEMASURAT`, apoi sectiunea `INDEX`. Pe sesiunea Smida (20 dark-uri, 30 bias-uri, fara sa masoare lights-urile)
dureaza 35 s si urca la 4.5-4.7 GB de memorie; pe biblioteca de 30 de dark-uri, 24-26 s si
2.9-3.3 GB (masurat pe 2026-10-11 si 2026-10-16, 4 fire); cele 32 de lights ale sesiunii Smida
adauga 28 s (63 s in total), iar biblioteca drept partener (`--against`) inca 27 s (90 s;
masurat pe 2026-10-16, 4 fire).

Cifrele sunt in ADU peste negru. Cand dark-urile nu sunt la ISO 400 si 120 s, pragurile si
cifrele etichetate `ADU-eq` sunt impartite la factorul ISO / 400 x expunere / 120 s, ca sa
insemne acelasi lucru la orice setare: la ISO 1600 si 120 s, 1 ADU-eq = 4 ADU. Linia
`normalizare` spune factorul. Un prag nu coboara totusi sub 5 ADU bruti, ca o serie scurta
sau la ISO mic sa nu numere zgomotul drept pixeli fierbinti; pragul de pixeli morti (negru -
50 ADU) si cifrele bias-urilor sunt in ADU bruti, pentru ca nu depind de expunere.

```
=== DARK-URI: 20 cadre decodate din 20, 6016 x 4016 px, scara 150 .. 4095 ADU (libraw (exact)), CFA RGGB ===
    normalizare        : ISO 400, 119.2 s -> x0.99 fata de ISO 400 si 120 s (1 ADU-eq = 0.99 ADU)
    cuantizare         : pas 2 ADU langa negru; 44 valori distincte in primul cadru (G1)

  2. SCURGERI DE LUMINA SI GRADIENT
    master dark        : centru -0.55, colturi -0.44 / -0.49 / -0.10 / -0.31 (SS / DS / SJ / DJ), margini sus -0.48 jos -0.18 stanga -0.41 dreapta -0.50  [G1, ...]
    master bias        : centru -0.68, ...
    dark - bias        : centru 0.13, colturi 0.22 / 0.17 / 0.15 / 0.13 (SS / DS / SJ / DJ), ...; mediana 0.14, 0.05 .. 0.27 (p1 .. p99)
    lumina pe cadre    : niciun cadru peste 5 x abaterea tipica (0.10 ADU-eq) si peste 0.1 ADU-eq fata de dreapta mediei pe incalzire
    glow / constant    : la stanga-sus: glow +0.05 (creste cu incalzirea), constant +0.05, din care in bias +0.02, ramane +0.04 ADU-eq
    verdict            : OK: nicio margine sau colt la mai mult de 1.0 ADU-eq de centru (cel mai departe +0.09 la stanga-sus, pe dark - bias); niciun cadru cu lumina peste incalzirea seriei

  3. DERIVA TERMICA IN SERIE
    amplitudine        : pixelii fierbinti la 0.83 din master in primul cadru, 1.07 in ultimul (443 pixeli peste 20 ADU-eq)
    verdict            : ATENTIE: senzorul s-a incalzit cu 2.2 grade in 38 min (regula dublarii la 6 grade); masterul, mediana seriei, corespunde mijlocului ei
```

- **2. Scurgeri de lumina si gradient**: masterul dark (media cadrelor), masterul bias si
  diferenta lor (curentul de intuneric), fiecare in blocuri de 32 px, citite in centru, in cele
  patru colturi (SS stanga-sus, DS dreapta-sus, SJ stanga-jos, DJ dreapta-jos) si pe cele
  patru margini, pe planul verde G1. Verdictul se da pe `dark - bias` cand exista bias-uri
  (un offset care apare si in bias e citire, nu lumina), altfel pe masterul dark: `OK` cand
  nicio regiune nu se departeaza de centru cu mai mult de 1 ADU-eq, `ATENTIE` cu regiunea cea
  mai departata peste: mai luminoasa, cu cauza ei (glow al senzorului, scurgere de lumina,
  offset de citire, sau nedecis fara bias-uri); mai intunecata, offset de citire sau bias
  nepotrivit. Doua randuri spun de unde vine lumina, pe principiul ca glow-ul e curent de
  intuneric si creste cu incalzirea seriei (amplitudinea pixelilor fierbinti, verificarea 3),
  pe cand lumina de afara nu. `lumina pe cadre` potriveste media fiecarui cadru pe incalzire
  (o dreapta robusta, Theil-Sen) si numeste cadrele care au lumina in plus fata de ea, peste
  5 x abaterea tipica si peste 0.1 ADU-eq; asemenea cadre dau `ATENTIE` si sfatul de a le
  scoate din `darks\` sau de a reface seria (o lampa aprinsa, luna, zorii). `glow / constant`
  desparte abaterea regiunii din verdict in partea care creste cu incalzirea (glow) si partea
  constanta; cu bias-uri, partea constanta se imparte in ce e si in bias (offset de citire) si
  ce ramane (scurgere); fara bias-uri, o scurgere constanta si un offset de citire arata la
  fel, si raportul o spune. La ISO 1600 randul arata si valorile brute, in ADU, intre
  paranteze. `lumina constanta` face acelasi lucru pe cele patru laturi (marginea cu cele
  doua colturi ale ei), unde o scurgere prin ocular sau prin montura lasa o banda: cu
  bias-uri, ce ramane dupa bias pe o latura, peste 0.15 ADU-eq si peste de trei ori eroarea
  lui, e lumina din toate cadrele, pe care testul pe cadre n-o poate vedea (el vede doar ce
  se schimba de la un cadru la altul), si verdictul o numeste: `lumina constanta in toate
  cadrele: +0.20 ADU-eq pe latura de sus ... scurgere de lumina (ocular, montura)`; o lumina
  uniforma pe tot cadrul, in toate cadrele, nu are reper si raportul o spune. Ambele cer cel putin 6 cadre cu pixeli fierbinti, iar despartirea cere si o serie
  care s-a incalzit (raportul de amplitudine sa se intinda pe cel putin 0.1; seriile D3300 de
  o ora se intind pe 0.25-0.4), altfel spune ca nu o poate face. Pe D3300 glow-ul
  e in stanga-sus, 0.05 ADU-eq la Smida si 0.22 in biblioteca de la 16 grade; setul de proba
  cu ocularul descoperit si o lampa in camera (2026-10-20) a dat 5 cadre la +0.17 .. +0.47
  ADU-eq, pe care verdictul vechi, pe master, le ratase.
- **3. Deriva termica in serie**: amplitudinea pixelilor fierbinti ai masterului in primul si
  in ultimul cadru, fata de master; raportul lor da gradele dupa regula "curentul se dubleaza
  la 6 grade". Peste 1 grad, `ATENTIE`: masterul (mediana seriei) corespunde mijlocului
  seriei, nu inceputului sau sfarsitului. Camera se incalzeste singura cand trage cadru dupa
  cadru: 2.2 grade in 38 min pe Smida, 3.4 in 58 min pe biblioteca.
- **4. Zgomot pe cadru**: sigma reziduului (cadru minus master) pe fiecare plan, zgomotul de
  citire din perechile de bias-uri consecutive si zgomotul temporal peste el (curentul de
  intuneric si cuantizarea, din sigma cadru-fata-de-cadru a verificarii 8), benzile aleatoare
  (deviatia mediilor pe randuri si pe coloane fata de cat ar da zgomotul alb). `ATENTIE`
  listeaza cadrele cu zgomot peste 1.5 x mediana.
- **5. Pixeli fierbinti**: cati pixeli are masterul peste 10 / 20 / 50 / 100 / 500 ADU-eq,
  pe planuri la 20, amplitudinile lor (mediana, p10, p90, p99), cati stau peste jumatate din
  prag in toate cadrele si cati palpaie, sigma temporala a celor fierbinti fata de restul,
  candidatii RTS (pixeli a caror valoare sare de la un cadru la altul). Informativ.
- **6. Model fix**: benzile structurate ale masterului (profilul pe randuri si pe coloane
  minus mediana lui glisanta, rms si varf la varf, cel mai rau plan) si glow-ul (partea
  abaterii de la verificarea 2 care creste cu incalzirea; toata abaterea cand nu se poate
  desparti). `ATENTIE` peste 2 ADU-eq varf la varf sau 1 ADU-eq la margini.
- **7. Pixeli morti si saturati**: sub negru - 50 ADU si la alb pe master; orice pixel da
  `ATENTIE`.
- **8. Cate dark-uri ajung**: zgomotul mediei a n cadre fata de sigma / sqrt(n) (cadrele
  sunt independente cand coincid; punctul n = 1 e sigma temporala, cadru fata de cadru, fara
  modelul fix) si zgomotul prezis al masterului median, 1.25 x sigma temporala / sqrt(N),
  fata de pasul de cuantizare al fisierului. Sub 10 cadre, `ATENTIE`: mediana are nevoie de
  ele ca un cadru ciudat sa n-o traga. Peste pas, `ATENTIE` cu numarul de cadre de la care ar
  cobori sub el. Altfel `OK`: zgomotul masterului intra in cuadratura in fiecare light
  calibrat, iar sub pas e cat de mic poate fi pentru fisierul asta.
- **9. Lights-urile fata de dark**: in fiecare light, pixelii fierbinti se cauta local: un
  pixel cu peste 6 sigma peste mediana celor 8 vecini de aceeasi culoare, sub saturatie, caruia
  niciun vecin imediat nu-i trece de 3 sigma (o stea isi lumineaza vecinii, un pixel fierbinte
  nu). Un pixel gasit in cel putin 80 % din lights e persistent. Sectiunea spune cati
  persistenti are masterul peste 10 / 20 / 50 / 100 ADU-eq si cati din pixelii fierbinti ai
  masterului apar in lights (in amandoua sensurile), cati persistenti nu-s in master
  (`nepotriviti`: o stea care n-a miscat intre cadre, sau un pixel pe care dark-ul nu-l are),
  si amplitudinea: cat de tari sunt pixelii fierbinti ai masterului in fiecare light fata de
  master, normalizata la ISO si expunere, mediana pe noapte cu primul si ultimul cadru (primele
  lights ale unei nopti sunt reci: 0.72 pe Smida, 1.0 de la minutul 15). Verdictul, pe raportul
  lights / dark: `OK: scadere directa` intre 0.9 si 1.1; `ATENTIE: scade cu -opt` intre 0.7 si
  1.4 (optiunea Siril care scaleaza dark-ul); `ATENTIE: doar corectie cosmetica (-cc=dark)`
  intre 0.4 si 0.7 (dark-ul e mult mai cald: vede toti pixelii fierbinti, dar scaderea ar
  supracorecta); altfel `ATENTIE: nepotrivit`. Gradele vin din regula dublarii la 6 grade.
- **Index**: `idx<NN>`, mediana amplitudinii pixelilor de pe rangurile 5-20 pe megapixel ai
  masterului (al 122-lea .. al 483-lea cel mai tare din 24 MP), in ADU-eq, un numar pentru cat
  de cald a rulat senzorul: 25 pe Smida, 67 pe biblioteca din 2026-10-10 (ISO 1600, 15.8
  grade in camera). `eticheta` e numele folderului plus `-idx<NN>` (folderul-parinte cand
  dark-urile stau in `darks\`): o afiseaza, tu redenumesti folderul daca vrei; nimic nu e scris
  sau redenumit. Un folder deja redenumit dupa eticheta (numele se termina in `-idx` si cifre,
  sau in `-idx-` al unui index nemasurat) pastreaza eticheta la un singur sufix: cel vechi e
  scos inainte sa fie pus cel nou, daca mai ramane un nume in fata lui, asa ca
  `...-15.8celsius-idx67` ramane `...-15.8celsius-idx67`, nu `...-idx67-idx67`, iar un index
  schimbat se vede ca numar schimbat. Cu lights, `lights` da indexul echivalent al noptii (indexul dark-urilor x
  raportul lights / dark) si gradele fata de dark.
- **Tabel pe cadre**: ora, media peste negru, sigma, sigma reziduului, benzile (de cate ori
  zgomotul alb), amplitudinea pixelilor fierbinti fata de master, lumina in plus fata de
  dreapta mediei pe incalzire (ADU-eq, `-` sub 6 cadre), latura cu cea mai multa lumina
  constanta fata de centru in acel cadru, dupa bias cand exista bias-uri, cu glow-ul inclus
  (ADU-eq; nota spune care latura; doar partea constanta a ei e lumina), pixelii peste 20 ADU-eq, pe planul G1; in fereastra, fiecare coloana a
  celor doua tabele are o explicatie la trecerea mouse-ului peste antet; un cadru sarit de plafon apare cu `sarit (...)`, unul necitit cu `eroare:`.
- **Tabel pe lights**: ora, cerul peste negru, sigma locala (scara detectorului), candidatii,
  pixelii izolati, amplitudinea fata de master cu quartilele, pe planul G1. Un light care nu
  spune ISO-ul sau expunerea e luat la setarile dark-urilor, cu o linie `ATENTIE`.

```
  9. LIGHTS FATA DE DARK
    pixeli fierbinti   : 86 persistenti in lights (izolati, peste 6 sigma locale, in cel putin 26 din 32 cadre)
    in masterul dark   : peste 10: 85 din 86 (99 %); peste 20: 85 din 86 (99 %); peste 50: 70 din 86 (81 %); peste 100: 16 din 86 (19 %)  [ADU-eq]
    din master in lights: peste 10: 85 din 2283 (4 %); peste 20: 85 din 443 (19 %); peste 50: 70 din 111 (63 %); peste 100: 16 din 24 (67 %)
    nepotriviti        : 1 pixel persistent in lights sub 20 ADU-eq in master (mediana lor in master 3 ADU-eq)
    amplitudine        : lights / dark 1.00 pe noapte (mediana pe 32 cadre; primul 0.72, ultimul 0.94; 443 pixeli)
    verdict            : OK: scadere directa (raport 1.00, in 0.9 .. 1.1; dark-ul e la aceeasi temperatura)

  INDEX
    dark-uri           : idx25 (25.2 ADU-eq: mediana pixelilor de pe rangurile 5-20 pe Mpx, al 122-lea .. al 483-lea din 24.2 Mpx, normalizat la ISO 400 si 120 s)
    eticheta           : 2026-08-12-Smida-idx25
    lights             : echivalent idx25 (dark x 1.00, adica +0.0 grade fata de dark)
```

`--against DIR` compara dark-urile sesiunii cu alt folder de dark-uri, o biblioteca sau alta
sesiune (dark-urile lui se decodeaza la fel, masterul lor median se compara cu al sesiunii):
indexul si eticheta partenerului, raportul dark-uri / partener pe pixelii fierbinti in amandoua
masterele (peste 20 ADU-eq in fiecare), cu quartilele, numarul de pixeli si gradele, cati din
pixelii peste 50 ADU-eq ai sesiunii ii are si partenerul peste 50 si cati din ai partenerului
peste 50 ii are sesiunea peste 20, si `lights / partener`, raportul lights-urilor fata de
partener prin dark-urile sesiunii, cu `verdict partener` dupa aceleasi praguri (fara lights,
verdictul e al dark-urilor fata de partener). Pe Smida fata de biblioteca: 0.42 pe 434 pixeli,
adica biblioteca e cu 7.5 grade mai calda, doar corectie cosmetica. Un folder partener care nu
se poate deschide e refuzat cu `EROARE` inainte de orice decodare; unul cu alta dimensiune sau
fara niciun dark decodabil da o linie `ATENTIE` si iesire `1`, restul raportului ramane; un
dark al partenerului care nu se poate folosi e numarat intr-o linie `ATENTIE` (iesire `1`).
`--out` nu poate fi nici in folderul partenerului. Un raport in jurul lui zero (lights-urile nu
arata deloc pixelii fierbinti ai masterului: reducere de zgomot in aparat, alt aparat, alta
orientare a FITS-ului) e `nepotrivit`, cu propozitia lui. Pe o sesiune fara dark-uri,
partenerul nu e comparat cu nimic: dark-urile lui sunt dark-urile rularii, iar randurile
`partener`, `dark-uri / partener`, `pixeli comuni` si `lights / partener` lipsesc.

Un FITS nu spune nivelul de negru (un FITS convertit de Siril pastreaza piedestalul aparatului,
150 la D3300), asa ca pentru FITS negrul e mediana primului bias cand exista bias-uri, altfel a
primului dark; linia `=== DARK-URI` spune de unde vine. Nici albul nu-l spune: saturatia unui
FITS se citeste la albul fisierului (65535 pe 16 biti), deci un aparat pe 12 biti care satureaza
la 4095 intr-un FITS pe 16 biti nu apare ca saturat. Un dark cu alt ISO sau alta expunere decat
primul e lasat deoparte cu `setari diferite`; un singur dark nu e o serie: verificarile 3, 4, 5
si 8 raman `NEMASURAT`.

`--out DIR` scrie in DIR raportul (`darkscheck-raport.txt`), cate un CSV cu toate cifrele pe
cadru si pe plan (`darkscheck-darks.csv`, `darkscheck-biases.csv`, `darkscheck-lights.csv`
cand sesiunea are lights) si hartile planului G1 ca
PNG (`darkscheck-harta-dark.png`, `-bias.png`, `-dark-minus-bias.png`; un bloc = 4 x 4 px,
intins intre percentilele 1 si 99). Nimic nu se scrie in folderul sesiunii: un `DIR` care e
folderul sesiunii sau unul din el e refuzat cu `EROARE`.

### Inventarul: `--inventory`

Citeste metadatele fara sa decodeze pixelii (0.3 s pentru cele 82 de raw-uri ale sesiunii
Smida, masurat pe 2026-10-11):

```
=== SESIUNE: E:\Photografie\Astro\Milkyway-Core\2026-08-12-Smida ===
    darks              : darks\ (20 cadre .NEF)
    biases             : biases\ (30 cadre .NEF)
    lights             : lights\ (32 cadre .NEF)
    aparat             : Nikon D3300
    expunere darks     : 119.2 s la ISO 400 (toate cadrele)
    expunere biases    : 0.05 s la ISO 400 (toate cadrele)
    expunere lights    : 119.2 s la ISO 400 (toate cadrele)
    interval darks     : 2026-08-13 00:48:53 -> 2026-08-13 01:28:52 (39 min); 20 cadre la 120.0 s; fara pauze peste 5 s
    interval lights    : 2026-08-12 23:22:25 -> 2026-08-13 00:45:41 (1 h 23 min); 32 cadre la 151.0 s; 31 pauze peste 5 s (cea mai lunga 125 s inainte de cadrul 26)
    potrivire          : OK: dark-urile au expunerea (119.2 s) si ISO-ul (400) lights-urilor
    bias               : OK: 0.05 s

  DARKS
     nr  fisier                ora       expunere   ISO   pauza
      1  DSC_0034.NEF          00:48:53     119.2    400       -
      2  DSC_0035.NEF          00:50:53     119.2    400     1 s
```

- `expunere <grup>`: expunerea si ISO-ul cele mai frecvente din grup; daca nu toate cadrele le
  au, scrie `(17 din 20 cadre; expuneri sau ISO diferite)`.
- `interval`: de la inceputul primului cadru la sfarsitul ultimului, cate cadre au ora si
  expunerea, intervalul median dintre doua porniri (cadenta), si pauzele peste 5 s dintre
  sfarsitul unei expuneri si inceputul urmatoarei: la dark-uri trase cadru dupa cadru nu
  exista; o pauza inseamna Live View, un meniu sau aparatul lasat, deci senzorul s-a racit.
- `potrivire`: `OK` cand dark-urile au expunerea (la 2 %) si ISO-ul lights-urilor, `ATENTIE`
  cu ambele setari cand nu, `NEMASURAT` fara lights.
- `bias`: `OK` cu expunerea cand bias-urile sunt sub 1 s, `ATENTIE` peste, `(lipsa)` fara.
- `pauza` din tabele e timpul dintre sfarsitul expunerii anterioare si inceputul acesteia. Ora
  vine din EXIF (ora aparatului) pe un raw si din `DATE-OBS` (UTC, cum il scrie Siril) pe un
  FITS.

## Fereastra

`DarksCheck.Wpf.exe [folder]`. Fara argument, cere folderul la pornire: folderul sesiunii Siril
(cel cu `darks\`) sau folderul unei biblioteci de dark-uri. Analiza porneste singura (aceeasi ca
`darkscheck <folder>`, cu bara de progres si buton de oprire) si rezultatul apare in sapte
taburi:

- **Rezumat**: linia sesiunii, apoi fiecare verificare ca un card de randuri, cu verdictul
  colorat (verde `OK`, galben `ATENTIE`, gri `NEMASURAT`) si restul textului langa el.
- **Cadre**: tabelul pe cadre, iar alaturi trei grafice cadru cu cadru: amplitudinea pixelilor
  fierbinti fata de master (incalzirea seriei), sigma reziduala (cu linia de 1.5 x mediana) si
  benzile aleatoare.
- **Harti**: cele trei harti ale verificarii 2 (master dark, master bias, dark - bias), aceleasi
  ca PNG-urile din `--out`, fiecare cu randul ei din raport dedesubt.
- **Pixeli fierbinti**: randurile verificarii 5 si curba zgomotului mediei a n cadre fata de
  sigma / sqrt(n) (verificarea 8); tine mouse-ul pe un punct si vezi cifrele.
- **Lights**: randurile verificarii 9, raportul lights / dark pe fiecare light cu liniile
  pragurilor (0.9 si 1.1 verde, 0.7 si 1.4 galben, 0.4 rosu) si tabelul pe lights.
- **Index**: eticheta, mare, cu butonul `Copiaza eticheta` (o pui in clipboard si redenumesti
  tu folderul; programul nu redenumeste nimic), randurile INDEX si partenerul: `Alege
  partener...` (o biblioteca sau alta sesiune), `Fara partener`, `Analizeaza din nou`. Partenerul
  ales dupa o analiza se aplica la urmatoarea. O sesiune fara dark-uri deschisa fara partener
  arata cardul de problema cu butonul `Alege partener...`: alegi biblioteca si analiza porneste
  din nou cu dark-urile ei.
- **Raport**: textul consolei, identic.

In bara de sus: `FIRE` (cate cadre se decodeaza in paralel; gol = jumatate din nuclee), `CADRE
MAX` (cate cadre se tin in memorie si cate lights se masoara; implicit 60), `CASTIG / TREAPTA
ISO` (de cate ori creste semnalul la o treapta de ISO, pentru normalizarea la ISO 400; 2 =
liniar, D3300) si `GRADE / DUBLARE` (la cate grade se dubleaza curentul de intuneric, pentru
gradele din raport; 6). Toate patru si partenerul sunt citite la deschiderea folderului si
tinute minte intre porniri in `%LOCALAPPDATA%\DarksCheck\settings.json`, singurul lucru pe care
programul il scrie in afara folderului de export; o valoare care nu e un numar pozitiv cu `.`
inseamna valoarea implicita. `Scrie fisierele...` scrie, intr-un folder la alegere (propus:
folderul sesiunii; refuzat in folderul sesiunii sau al partenerului), aceleasi fisiere ca
`--out`. `Despre` (F1) arata versiunea, licenta si componentele terte. Consola ruleaza cu
castigul 2 si 6 grade pe dublare, fara optiuni pentru ele.

## Compilare

.NET SDK 9.0.3xx (`global.json`). `dotnet build DarksCheck.sln -c Release` (fereastra cere
Windows; consola singura se compileaza oriunde). Gate-ul complet (format, build strict, teste):
`scripts/verify.ps1`. Publicarea (`scripts/release.ps1`) scrie `darkscheck.exe` in
`artifacts/<versiune>/console/` si `DarksCheck.Wpf.exe` in `artifacts/<versiune>/wpf/`, fiecare
un singur fisier cu DLL-urile libraw inglobate.

## Pentru cine dezvolta

Regulile de lucru sunt in `CLAUDE.md` si `.claude/rules/kit/process.md`, cerintele cu ID in
`docs/requirements.md`, deciziile in `docs/decisions/`, cifrele masurate pe sesiuni reale in
`docs/validation.md`. Analiza din care a pornit unealta (scripturile Python, raportul pe
sesiunea Smida din 2026-08-12 si pe biblioteca de dark-uri din 2026-10-10) e in `prototype/`
si `docs/validation/2026-08-12-smida/`.
