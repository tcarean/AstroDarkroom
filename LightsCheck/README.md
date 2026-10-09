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

## Compilare

.NET SDK 9.0.3xx (`global.json`). `dotnet build LightsCheck.sln -c Release`. Gate-ul complet
(format, build strict, teste): `scripts/verify.ps1`. Publicarea (`scripts/release.ps1`) scrie
`lightscheck.exe` ca un singur fisier, cu DLL-urile libraw inglobate, in `artifacts/`.

## Pentru cine dezvolta

Regulile de lucru sunt in `CLAUDE.md` si `.claude/rules/kit/process.md`, cerintele cu ID in
`docs/requirements.md`, deciziile in `docs/decisions/`, cifrele masurate pe sesiuni reale in
`docs/validation.md`. Analiza din care a pornit unealta (scripturile Python, raportul pe
sesiunea Veil din 2026-10-06) e in `prototype/` si `docs/validation/2026-10-06-veil/`.
