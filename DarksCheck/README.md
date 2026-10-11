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

**Stadiu: pasul 1 din 5 al planului.** Consola face inventarul si verificarea 1 (metadate,
cadenta, potrivirea dark-urilor cu lights-urile). Verificarile 2-9, indexul si fereastra vin in
pasii urmatori (`docs/plans/2026-10-11-build-darkscheck.md`). Analiza de mana de la care
porneste unealta, cu cifrele ei, e in `docs/validation/2026-08-12-smida/report.md`.

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
darkscheck <folder sesiune sau folder de dark-uri>
darkscheck <folder> --inventory
darkscheck --help
```

Fiecare rulare incepe cu `darkscheck <versiune>` si notita de copyright. Codul de iesire e `0`
cand toate cadrele s-au citit si `1` la eroare (folder lipsa, niciun cadru, un cadru care nu se
poate citi; cadrul e listat cu `eroare:` si restul sunt analizate in continuare).

### Inventarul: `--inventory` (deocamdata si fara optiuni)

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
