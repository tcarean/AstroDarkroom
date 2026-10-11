# Componente terte

DarksCheck e distribuit sub **GNU General Public License v3 sau ulterioara** (vezi
[LICENSE](LICENSE)). Fisierul asta listeaza codul tert pe care il leaga, licenta fiecarei
componente si motivul pentru care e compatibila cu GPLv3. Un pachet adaugat fara o intrare
aici e un pachet a carui compatibilitate n-a verificat-o nimeni; `ThirdPartyTests` leaga
lista de aici, lista din `ThirdParty.cs` (ce arata fereastra "Despre") si pachetele din
`DarksCheck.Core.csproj`.

Ce e scris de mana si **nu** e cod tert: cititorul FITS, encoderul PNG, toate calculele si
toate pragurile. Cititorul FITS, cititorul raw si encoderul PNG sunt copiate din LightsCheck,
care le-a copiat din FlatCheck; acelasi autor, aceeasi licenta (ADR 0003).

---

## Sdcb.LibRaw

**Rol.** Citeste mozaicul Bayer nedebayerizat, nivelurile exacte de alb si negru, ordinea
culorilor din CFA si metadatele fotografierii (expunere, ISO, ora, focala, diafragma,
obiectiv) dintr-un fisier raw de aparat.

**Licenta.** MIT (ambalajul C#).

**Compatibilitate.** MIT e permisiva si compatibila cu GPLv3.

- Proiect: <https://github.com/sdcb/Sdcb.LibRaw>

## LibRaw (bibliotecile native din `Sdcb.LibRaw.runtime.*`)

**Rol.** Decodarea propriu-zisa a formatelor raw. Vine ca DLL-uri native (`raw_r`, `jpeg8`,
`lcms2`, `zlib1`), livrate alaturi de executabil.

**Licenta.** Dubla, la alegerea celui care o foloseste: **LGPL-2.1-only SAU CDDL-1.0**.

**Compatibilitate - aici se ia o decizie.** DarksCheck foloseste LibRaw sub **LGPL-2.1**,
aceeasi alegere ca FlatCheck. CDDL-1.0 **nu** e compatibila cu GPL, deci alegerea celeilalte
optiuni ar rupe licenta uneltei. LGPL-2.1 e compatibila: articolul 3 al ei permite explicit
aplicarea termenilor GPL obisnuite, "version 2 of the ordinary GNU General Public License or
any later version", asupra unei copii, ceea ce aduce codul intr-o lucrare GPLv3. LGPL-2.1
articolul 6 cere ca, daca programul isi afiseaza notitele de copyright la rulare (o face, pe
a doua linie a fiecarei rulari), notita LibRaw sa apara printre ele: de asta exista lista din
`ThirdParty.cs` si fereastra "Despre".

- Proiect: <https://www.libraw.org/>

---

## Componente de test

Folosite numai la compilarea si rularea testelor, deci nu sunt distribuite cu unealta.

| pachet | licenta |
|---|---|
| xUnit | Apache-2.0 |
| Microsoft.NET.Test.Sdk | MIT |

---

## Cum se adauga o intrare

Vezi regula "Packages" a kit-ului (`.claude/rules/kit/process.md`) si regula "Package
notices" din [CLAUDE.md](CLAUDE.md). Pe scurt: pachetul se discuta inainte, nu dupa;
compatibilitatea cu GPLv3 se demonstreaza, nu se presupune; iar motivul se scrie aici.
