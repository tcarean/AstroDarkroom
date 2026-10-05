# Componente terțe

AstroPhotoAnalyzer e distribuit sub **GNU General Public License v3 sau ulterioară** (vezi
[LICENSE](LICENSE)). Fișierul ăsta listează codul terț pe care îl leagă, licența fiecărei
componente și motivul pentru care e compatibilă cu GPLv3.

Nu e o formalitate: licența ImageSharp e condiționată și trebuie arătat că se aplică. Un
pachet adăugat fără o intrare aici e un pachet a cărui compatibilitate n-a verificat-o
nimeni.

Ce e scris de mână și **nu** e cod terț: cititorul FITS, cititorul WCS, toate măsurătorile,
toate pragurile și tot textul recomandărilor. Pe lângă cod, unealta îmbarcă un set de **date**
scrise de alții, catalogul Lynds; e tratat aici ca o componentă, cu aceeași notiță.

---

## Lynds' Catalogue of Dark Nebulae

**Rol.** Catalogul norilor de praf după care unealta recunoaște, într-un FITS plate-solved,
ce nebuloase întunecate sunt în cadru și unde: 1787 de nori cu centrul, aria și clasa de
opacitate, plus numerele Barnard cu care sunt cunoscuți (B78 pentru LDN 42, de pildă). Fără el,
praful se măsoară pe tot cadrul, ca o fracție; cu el, pe nume (ADR 0025, R-019, R-044). Stă în
`AstroPhotoAnalyzer.Core/Input/ldn.txt`, o resursă text de 52 KB citită de `DarkNebulaCatalogue`.

**Sursa.** Lynds, B. T., *Catalogue of Dark Nebulae*, Astrophysical Journal Supplement 7, 1
(1962). Fișierul îmbarcat e versiunea citibilă de mașină din 1979, pregătită de NASA/ADC din
copia autorului (1791 de intrări, cu identificările Barnard adăugate din tabelul publicației
originale), distribuită de CDS Strasbourg prin VizieR ca **VII/7A**
(<https://cdsarc.cds.unistra.fr/viz-bin/cat/VII/7A>), luată pe 2026-10-02. Se păstrează
coloanele originale (centrul în B1950, aria, opacitatea, numerele Barnard), nu coloanele ICRS
calculate de VizieR; precesia în J2000 o face unealta. Cele patru intrări fără număr LDN de la
sfârșitul tabelului sunt lăsate afară.

**Licență.** Nicio licență formală nu însoțește catalogul: e un tabel de fapte dintr-o
publicație științifică din 1962, distribuit de 47 de ani de agențiile de date astronomice.
Regulile VizieR (<https://cds.unistra.fr/vizier-org/licences_vizier.html>) cer citarea
autorului și a publicației originale și apreciază o mențiune a serviciului VizieR
(DOI 10.26093/cds/vizier); nota lor despre licența CC-BY-NC privește tabelele jurnalelor AAS din
seria `J/` (J/ApJ, J/ApJS, J/AJ), din care VII/7A nu face parte. ReadMe-ul catalogului nu
conține nicio clauză de copyright.

**Compatibilitate.** Datele nu sunt cod și nu se leagă de licența programului: GPLv3 acoperă
programul, iar un tabel de poziții, arii și clase e un set de fapte, pe care dreptul de autor
american nu-l protejează (Feist, 1991); dreptul european *sui generis* asupra bazelor de date
durează 15 ani de la constituire, iar compilația e din 1979. Rămâne obligația de citare, pe
care unealta o îndeplinește aici, în `ThirdParty.cs` (ce arată „Despre") și în README. Un
derivat al AstroPhotoAnalyzer preia aceeași obligație, nimic mai mult.

**Un lucru de știut.** Catalogul dă aria unui nor, nu conturul lui: unealta îl tratează ca un
disc de aceeași arie în jurul centrului (ADR 0025), iar rândul unui nor neregulat amestecă
dalele lui cu cerul din jur.

Lynds B. T., 1962; versiunea citibilă NASA/ADC, 1979; distribuită de CDS, Strasbourg.

- Catalog: <https://cdsarc.cds.unistra.fr/viz-bin/cat/VII/7A>
- Articol: <https://ui.adsabs.harvard.edu/abs/1962ApJS....7....1L>

---

## SixLabors.ImageSharp

**Rol.** Decodează TIFF și PNG (8 și 16 biți pe canal, RGB și mono, cu profilul ICC
încorporat) — adică pixelii pe care se fac toate măsurătorile — și encodează/decodează
JPEG pentru tabelul de compresie (q100 / q95 / q90, 4:4:4). Spre deosebire de rolul ei
în FlatCheck, aici pachetul **nu** e periferic: cifrele din tabelul JPEG sunt ale
encoderului lui, și ce vede unealta într-un TIFF e ce a decodat el.

**De ce nu scris de mână.** Un cititor TIFF onest înseamnă strips și tiles, LZW, Deflate,
PackBits, predictor orizontal, planar și chunky, 8 și 16 biți, big și little endian, plus
PNG cu Adam7 și un encoder JPEG cu DCT, Huffman și subeșantionare controlată. Sunt
săptămâni de muncă pentru a reproduce, cu bug-uri proprii, ceva ce există și e testat pe
milioane de fișiere. Cititorul FITS rămâne scris de mână, fiindcă acolo chiar e simplu:
carduri de 80 de octeți în blocuri de 2880, apoi date big-endian.

**Licență.** Six Labors Split License, versiunea 1.0 (iunie 2022).

**Compatibilitate.** Nu e o licență restrictivă unică, ci una **duală condiționată**.
Textul acordă termenii Apache 2.0 când:

> "You are consuming the Work in for use in software licensed under an Open Source or
> Source Available license."

AstroPhotoAnalyzer e sub GPLv3, deci e software sub o licență open source și califică pe
condiția asta. Licența comercială e cerută doar pentru celelalte cazuri, în esență
utilizarea proprietară de către o entitate cu venit anual de cel puțin 1M USD.

Iar Apache 2.0 intră în GPLv3 într-o singură direcție, ceea ce Apache Software Foundation
afirmă explicit:

> "Apache 2 software can therefore be included in GPLv3 projects, because the GPLv3 license
> accepts our software into GPLv3 works."

Nu e nevoie de nicio modificare a licenței AstroPhotoAnalyzer.

**Două lucruri de păstrat.**

1. **Versiunea se fixează în `PackageReference`.** Textul licenței e „Version 1.0, June
   2022" și nu spune ce versiune de licență guvernează care lansare. Grantul primit vine cu
   versiunea luată, deci fixarea e igienă juridică, nu doar reproductibilitate.
   Versiunea fixată e **3.1.11**, nu 4.x: de la 4.0.0 pachetul aduce un task MSBuild care
   validează o cheie de licență la compilare, avertisment pe Debug și **eroare pe
   Release** fără `sixlabors.lic` sau `$(SixLaborsLicenseKey)`. Six Labors acordă cheia
   gratuit proiectelor open source, dar pe bază de cerere, și oricine clonează repo-ul ar
   avea nevoie de a lui ca să facă publish. 3.1.x are exact același text de licență și
   niciun task.
2. **Calificarea nu se rupe în aval.** GPLv3 obligă orice derivat să rămână GPLv3, adică tot
   o licență open source, deci oricine preia AstroPhotoAnalyzer califică la rândul lui pe
   aceeași condiție.

**Un lucru de știut la decodare.** 3.1.x nu aplică niciodată profilul ICC la decodare —
pixelii ies exact cum sunt în fișier, iar profilul e disponibil separat în metadate. E
chiar comportamentul dorit: unealta măsoară ce a scris Siril, nu o conversie de culoare.

Copyright (c) Six Labors.

- Proiect: <https://github.com/SixLabors/ImageSharp>
- Licență: <https://github.com/SixLabors/ImageSharp/blob/main/LICENSE>

---

## Wacton.Unicolour

**Rol.** Evaluează tabelele unui profil ICC de imprimantă (v2 `lut8`/`lut16`, v4 `lutAToB`):
unealta îi dă negrul dispozitivului și intenția colorimetrică relativă și primește culoarea
lui față de albul hârtiei, din care iese Dmax-ul hârtiei pe imprimanta aceea (`--icc`, butonul
„Din profil ICC..." al ferestrei). Nu face nicio conversie de culoare pe imagine.

**De ce nu scris de mână.** Un cititor scris de mână pentru cele două profile ale atelierului a
dat aceleași cifre până la ultima zecimală, dar știa un singur format de tabelă; al doilea
(`lutAToB`, din profilele v4) înseamnă curbe parametrice, matrice și cub, fiecare opțional.
Pachetul le are pe toate și e testat pe profile reale.

**Licență.** MIT.

**Compatibilitate.** MIT e o licență permisivă, compatibilă cu GPLv3 fără condiții în afara
păstrării notiței de copyright și a textului licenței, ceea ce face fișierul acesta. Pachetul e
cod gestionat pur, fără dependențe și fără componente native, deci consola rămâne un singur
fișier copiabil.

**Un lucru de știut.** Pe un fișier care nu e profil, pachetul nu aruncă: notează o eroare și
calculează „naiv" mai departe (L\* 100). Unealta verifică antetul înainte și eroarea după.
Intenția perceptuală dă același negru (L\* 3.14) pentru orice profil; unealta cere mereu
intenția colorimetrică relativă.

Copyright (c) 2022-2026 William Acton.

- Proiect: <https://github.com/waacton/Unicolour>
- Licență: <https://github.com/waacton/Unicolour/blob/main/LICENSE>

---

## Componente de test

Folosite numai la compilarea și rularea testelor, deci nu sunt distribuite cu unealta.

| pachet | licență |
|---|---|
| xUnit | Apache-2.0 |
| Microsoft.NET.Test.Sdk | MIT |
| coverlet.collector | MIT |

---

## Cum se adaugă o intrare

Vezi regula „Packages" a kit-ului, în `.claude/rules/kit/process.md`, regula „Package notices"
din [CLAUDE.md](CLAUDE.md) și ADR 0009, în `docs/decisions/`. Pe scurt: pachetul se discută înainte,
nu după; compatibilitatea cu GPLv3 se demonstrează, nu se presupune; iar motivul se scrie
aici, ca următorul cititor să nu fie nevoit să-l redescopere.
