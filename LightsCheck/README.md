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

**Stadiu: in constructie.** Pasul 1 din 5 (planul e in `docs/plans/2026-10-09-build-lightscheck.md`):
consola deschide sesiunea si face inventarul cadrelor. Masuratorile pe cadru, analiza seriei si
a dithering-ului, lista de respins si fereastra vin in pasii urmatori.

> **Documentatia de utilizator sta aici, in acest fisier.** Nu o duplica in comentarii.

---

## Ce folder ii dai

Folderul sesiunii Siril, cel care are `lights/` si, optional, `biases/`:

```
2026-10-06-3h\
    lights\     DSC_0001.NEF ... DSC_0087.NEF     <- obligatoriu (sau cadrele direct in folder)
    biases\     DSC_0128.NEF ...                  <- optional
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
lightscheck <folder sesiune>
lightscheck --help
```

Fiecare rulare incepe cu `lightscheck <versiune>` si notita de copyright. Codul de iesire e
`0` cand toate cadrele s-au citit si `1` la eroare (folder lipsa, niciun cadru, un cadru care
nu se poate citi; cadrul e listat cu `eroare:` si restul sunt listate in continuare).

Ce se afiseaza azi:

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
  sau zgomot).
- `halo`: cat din lumina unei stele luminoase sta intr-un inel de 5-10 px fata de nucleul de
  3 px; creste cu roua sau ceata.
- `dare`: urme de sateliti sau avioane, cu lungimea si coltul din stanga-sus.

## Compilare

.NET SDK 9.0.3xx (`global.json`). `dotnet build LightsCheck.sln -c Release`. Gate-ul complet
(format, build strict, teste): `scripts/verify.ps1`. Publicarea (`scripts/release.ps1`) scrie
`lightscheck.exe` ca un singur fisier, cu DLL-urile libraw inglobate, in `artifacts/`.

## Pentru cine dezvolta

Regulile de lucru sunt in `CLAUDE.md` si `.claude/rules/kit/process.md`, cerintele cu ID in
`docs/requirements.md`, deciziile in `docs/decisions/`, cifrele masurate pe sesiuni reale in
`docs/validation.md`. Analiza din care a pornit unealta (scripturile Python, raportul pe
sesiunea Veil din 2026-10-06) e in `prototype/` si `docs/validation/2026-10-06-veil/`.
