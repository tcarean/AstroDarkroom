# Componente terțe

AstroPhotoAnalyzer e distribuit sub **GNU General Public License v3 sau ulterioară** (vezi
[LICENSE](LICENSE)). Fișierul ăsta listează codul terț pe care îl leagă, licența fiecărei
componente și motivul pentru care e compatibilă cu GPLv3.

Nu e o formalitate: licența ImageSharp e condiționată și trebuie arătat că se aplică. Un
pachet adăugat fără o intrare aici e un pachet a cărui compatibilitate n-a verificat-o
nimeni.

Ce e scris de mână și **nu** e cod terț: cititorul FITS, toate măsurătorile, toate
pragurile și tot textul recomandărilor.

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

## Componente de test

Folosite numai la compilarea și rularea testelor, deci nu sunt distribuite cu unealta.

| pachet | licență |
|---|---|
| xUnit | Apache-2.0 |
| Microsoft.NET.Test.Sdk | MIT |
| coverlet.collector | MIT |

---

## Cum se adaugă o intrare

Vezi „The dependencies" din [CLAUDE.md](CLAUDE.md). Pe scurt: pachetul se discută înainte,
nu după; compatibilitatea cu GPLv3 se demonstrează, nu se presupune; iar motivul se scrie
aici, ca următorul cititor să nu fie nevoit să-l redescopere.
