# Componente terțe

FlatCheck e distribuit sub **GNU General Public License v3 sau ulterioară** (vezi
[LICENSE](LICENSE)). Fișierul ăsta listează codul terț pe care îl leagă, licența fiecărei
componente și motivul pentru care e compatibilă cu GPLv3.

Nu e o formalitate: două dintre intrările de mai jos presupun o **alegere**, nu o simplă
constatare. LibRaw e oferit sub două licențe dintre care doar una e compatibilă cu GPL, iar
licența ImageSharp e condiționată și trebuie arătat că se aplică. Un pachet adăugat fără o
intrare aici e un pachet a cărui compatibilitate n-a verificat-o nimeni.

Ce e scris de mână și **nu** e cod terț: cititorul FITS, encoderul PNG, toate calculele și
toate pragurile.

---

## Sdcb.LibRaw

**Rol.** Citește mozaicul Bayer nedebayerizat, nivelurile exacte de alb și negru, ordinea
culorilor din CFA și octeții previzualizării dintr-un fișier raw de aparat. Fără el, modul
de expunere n-ar putea rula direct pe un `.NEF`.

**Licență.** MIT (ambalajul C#).

**Compatibilitate.** MIT e permisivă și compatibilă cu GPLv3.

- Proiect: <https://github.com/sdcb/Sdcb.LibRaw>

## LibRaw (bibliotecile native din `Sdcb.LibRaw.runtime.*`)

**Rol.** Decodarea propriu-zisă a formatelor raw. Vine ca DLL-uri native
(`raw_r`, `jpeg8`, `lcms2`, `zlib1`), livrate alături de executabil.

**Licență.** Dublă, la alegerea celui care o folosește:
**LGPL-2.1-only SAU CDDL-1.0**.

**Compatibilitate - aici se ia o decizie.** FlatCheck folosește LibRaw sub **LGPL-2.1**.

CDDL-1.0 **nu** e compatibilă cu GPL, deci alegerea celeilalte opțiuni ar rupe licența
uneltei. LGPL-2.1 e compatibilă: articolul 3 al ei permite explicit aplicarea termenilor
GPL obișnuite, „version 2 of the ordinary GNU General Public License or any later version",
asupra unei copii, ceea ce aduce codul într-o lucrare GPLv3.

Dacă cineva schimbă vreodată opțiunea asta, licența FlatCheck se rupe tăcut. De aceea e
scrisă aici, nu dedusă la nevoie.

- Proiect: <https://www.libraw.org/>

## SixLabors.ImageSharp

**Rol.** Decodează previzualizarea JPEG pe care aparatul o înglobează în fișierul raw,
adică exact imaginea din care aparatul și-a calculat histograma de pe ecran. Folosită numai
pentru afișare, în tabul de expunere: nu intră în niciun verdict și nu produce nicio cifră
pe care să se sprijine o decizie despre flat.

**Licență.** Six Labors Split License, versiunea 1.0 (iunie 2022).

**Compatibilitate.** Nu e o licență restrictivă unică, ci una **duală condiționată**.
Textul acordă termenii Apache 2.0 când:

> "You are consuming the Work in for use in software licensed under an Open Source or
> Source Available license."

FlatCheck e sub GPLv3, deci e software sub o licență open source și califică pe condiția
asta. Licența comercială e cerută doar pentru celelalte cazuri, în esență utilizarea
proprietară de către o entitate cu venit anual de cel puțin 1M USD.

Iar Apache 2.0 intră în GPLv3 într-o singură direcție, ceea ce Apache Software Foundation
afirmă explicit:

> "Apache 2 software can therefore be included in GPLv3 projects, because the GPLv3 license
> accepts our software into GPLv3 works."

Nu e nevoie de nicio modificare a licenței FlatCheck.

**Două lucruri de păstrat.**

1. **Versiunea se fixează în `PackageReference`.** Textul licenței e „Version 1.0, June
   2022" și nu spune ce versiune de licență guvernează care lansare. Grantul primit vine cu
   versiunea luată, deci fixarea e igienă juridică, nu doar reproductibilitate.
   Versiunea fixată e **3.1.11**, nu 4.x: de la 4.0.0 pachetul aduce un task MSBuild care
   validează o cheie de licență la compilare, avertisment pe Debug și **eroare pe
   Release** fără `sixlabors.lic` sau `$(SixLaborsLicenseKey)`. Six Labors acordă cheia
   gratuit proiectelor open source, dar pe bază de cerere, și oricine clonează repo-ul ar
   avea nevoie de a lui ca să facă publish. 3.1.x are exact același text de licență și
   niciun task. Trecerea la 4.x înseamnă obținerea cheii și, în cod, setarea explicită a
   lui `ColorProfileHandling.Preserve` la decodare.
2. **Calificarea nu se rupe în aval.** GPLv3 obligă orice derivat să rămână GPLv3, adică tot
   o licență open source, deci oricine preia FlatCheck califică la rândul lui pe aceeași
   condiție.

**Alternativa evaluată.** `StbImageSharp` (Unlicense sau MIT, fără dependințe tranzitive)
produce, măsurat pe previzualizări reale față de un decodor independent, mediane identice
pe toate canalele, și n-ar fi purtat nicio notă de licență. ImageSharp a fost preferată
pentru robustețe pe cazurile incomode pe care le poate avea o previzualizare de aparat -
JPEG progresiv, CMYK/YCCK, subeșantionare neobișnuită - și pentru decodarea la scară
redusă. Dacă licența ei se mută vreodată, StbImageSharp e înlocuitorul direct, iar cusătura
e lată de un singur fișier (`PreviewReader.cs`).

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

---

## Cum se adaugă o intrare

Vezi „The dependencies" din [CLAUDE.md](CLAUDE.md). Pe scurt: pachetul se discută înainte,
nu după; compatibilitatea cu GPLv3 se demonstrează, nu se presupune; iar motivul se scrie
aici, ca următorul cititor să nu fie nevoit să-l redescopere.
