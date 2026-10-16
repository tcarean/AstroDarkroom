# darkscheck

Verificare de calitate pentru dark frames de astrofoto, pentru un DSLR (Nikon D3300, flux
Siril). Citeste folderul unei sesiuni Siril sau un folder de dark-uri (o biblioteca), repeta
cele noua verificari facute de mana pe sesiunea Smida din 2026-08-12 (metadate si cadenta,
scurgeri de lumina, deriva termica, zgomot, pixeli fierbinti, model fix, pixeli morti, cate
dark-uri ajung, lights-urile fata de dark) si calculeaza indexul: un numar pentru cat de cald a
rulat senzorul, pe dark-uri, pe o biblioteca si pe lights, ca sa potrivesti un master din
biblioteca cu o sesiune. Pozele sunt doar citite: nimic nu e modificat, mutat sau scris in
folderele lor.

Consola `darkscheck.exe` si fereastra (in lucru) stau peste acelasi nucleu si dau aceleasi
cifre. Aceeasi licenta (GPLv3 sau ulterioara, [LICENSE](LICENSE)), acelasi mod de versionare
si aceeasi fereastra "Despre" ca FlatCheck si LightsCheck, de unde sunt copiate cititoarele de
raw si FITS (vezi [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)).

**Stadiu: pasul 2 din 5 al planului.** Consola face inventarul si verificarea 1 (metadate,
cadenta, potrivirea dark-urilor cu lights-urile) si verificarile 2-8 pe dark-uri si bias-uri.
Verificarea 9 (lights-urile fata de dark), indexul si fereastra vin in pasii urmatori
(`docs/plans/2026-10-11-build-darkscheck.md`). Analiza de mana de la care porneste unealta, cu
cifrele ei, e in `docs/validation/2026-08-12-smida/report.md`.

> **Documentatia de utilizator sta aici, in acest fisier.** Nu o duplica in comentarii.

---

## Ce folder ii dai

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
darkscheck <folder sesiune sau folder de dark-uri> [--out DIR] [--threads N] [--max-frames N]
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
restul sunt listate ca sarite, fara sa fie erori) si tipareste, dupa liniile
inventarului, cate o sectiune pe verificare, fiecare cu cifrele ei si un rand `verdict` care
incepe cu `OK`, `ATENTIE` sau `NEMASURAT`. Pe sesiunea Smida (20 dark-uri, 30 bias-uri)
dureaza 35 s si urca la 4.5-4.7 GB de memorie; pe biblioteca de 30 de dark-uri, 24-26 s si
2.9-3.3 GB (masurat pe 2026-10-11 si 2026-10-16, 4 fire).

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
    verdict            : OK: nicio margine sau colt la mai mult de 1.0 ADU-eq de centru (cel mai departe +0.09 la stanga-sus, pe dark - bias)

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
  mai departata peste: mai luminoasa, scurgere de lumina sau glow; mai intunecata, offset de
  citire sau bias nepotrivit.
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
  minus mediana lui glisanta, rms si varf la varf, cel mai rau plan) si glow-ul (offsetul de
  la verificarea 2). `ATENTIE` peste 2 ADU-eq varf la varf sau 1 ADU-eq la margini.
- **7. Pixeli morti si saturati**: sub negru - 50 ADU si la alb pe master; orice pixel da
  `ATENTIE`.
- **8. Cate dark-uri ajung**: zgomotul mediei a n cadre fata de sigma / sqrt(n) (cadrele
  sunt independente cand coincid; punctul n = 1 e sigma temporala, cadru fata de cadru, fara
  modelul fix) si zgomotul prezis al masterului median, 1.25 x sigma temporala / sqrt(N),
  fata de pasul de cuantizare al fisierului. Sub 10 cadre, `ATENTIE`: mediana are nevoie de
  ele ca un cadru ciudat sa n-o traga. Peste pas, `ATENTIE` cu numarul de cadre de la care ar
  cobori sub el. Altfel `OK`: zgomotul masterului intra in cuadratura in fiecare light
  calibrat, iar sub pas e cat de mic poate fi pentru fisierul asta.
- **Tabel pe cadre**: ora, media peste negru, sigma, sigma reziduului, benzile (de cate ori
  zgomotul alb), amplitudinea pixelilor fierbinti fata de master, pixelii peste 20 ADU-eq, pe
  planul G1; un cadru sarit de plafon apare cu `sarit (...)`, unul necitit cu `eroare:`.

Un FITS nu spune nivelul de negru (un FITS convertit de Siril pastreaza piedestalul aparatului,
150 la D3300), asa ca pentru FITS negrul e mediana primului bias cand exista bias-uri, altfel a
primului dark; linia `=== DARK-URI` spune de unde vine.

`--out DIR` scrie in DIR raportul (`darkscheck-raport.txt`), cate un CSV cu toate cifrele pe
cadru si pe plan (`darkscheck-darks.csv`, `darkscheck-biases.csv`) si hartile planului G1 ca
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

## Compilare

.NET SDK 9.0.3xx (`global.json`). `dotnet build DarksCheck.sln -c Release`. Gate-ul complet
(format, build strict, teste): `scripts/verify.ps1`. Publicarea (`scripts/release.ps1`) scrie
`darkscheck.exe`, un singur fisier cu DLL-urile libraw inglobate, in `artifacts/`.

## Pentru cine dezvolta

Regulile de lucru sunt in `CLAUDE.md` si `.claude/rules/kit/process.md`, cerintele cu ID in
`docs/requirements.md`, deciziile in `docs/decisions/`, cifrele masurate pe sesiuni reale in
`docs/validation.md`. Analiza din care a pornit unealta (scripturile Python, raportul pe
sesiunea Smida din 2026-08-12 si pe biblioteca de dark-uri din 2026-10-10) e in `prototype/`
si `docs/validation/2026-08-12-smida/`.
