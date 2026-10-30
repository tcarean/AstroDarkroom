# Componente terte

LightsCheck e distribuit sub **GNU General Public License v3 sau ulterioara** (vezi
[LICENSE](LICENSE)). Fisierul asta listeaza codul tert pe care il leaga, licenta fiecarei
componente si motivul pentru care e compatibila cu GPLv3. Un pachet adaugat fara o intrare
aici e un pachet a carui compatibilitate n-a verificat-o nimeni; `ThirdPartyTests` leaga
lista de aici, lista din `ThirdParty.cs` (ce arata fereastra "Despre") si pachetele din
`LightsCheck.Core.csproj`.

Ce e scris de mana si **nu** e cod tert: cititorul FITS, encoderul PNG, toate calculele si
toate pragurile. Cititorul FITS, cititorul raw si encoderul PNG sunt copiate din FlatCheck,
acelasi autor, aceeasi licenta (ADR 0003).

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

**Compatibilitate - aici se ia o decizie.** LightsCheck foloseste LibRaw sub **LGPL-2.1**,
aceeasi alegere ca FlatCheck. CDDL-1.0 **nu** e compatibila cu GPL, deci alegerea celeilalte
optiuni ar rupe licenta uneltei. LGPL-2.1 e compatibila: articolul 3 al ei permite explicit
aplicarea termenilor GPL obisnuite, "version 2 of the ordinary GNU General Public License or
any later version", asupra unei copii, ceea ce aduce codul intr-o lucrare GPLv3. LGPL-2.1
articolul 6 cere ca, daca programul isi afiseaza notitele de copyright la rulare (o face, pe
a doua linie a fiecarei rulari), notita LibRaw sa apara printre ele: de asta exista lista din
`ThirdParty.cs` si fereastra "Despre".

- Proiect: <https://www.libraw.org/>

## NLog

**Rol.** Scrie jurnalul de diagnostic, fisierele text din folderul `log` de langa fiecare
executabil, pe care le poarta un raport de eroare (ADR 0029). Nu decide si nu calculeaza nimic:
nucleul si cele doua aplicatii ii dau linii gata scrise, iar fara configuratia lui fiecare apel
ramane fara efect. Vin doua pachete ale aceluiasi proiect: `NLog` si `NLog.Targets.AtomicFile`,
tinta lui de fisier cu adaugarea atomica a sistemului de operare, ca doua ferestre care scriu
acelasi jurnal in acelasi timp sa nu piarda nicio linie (R-073).

**Licenta.** BSD-3-Clause, pentru ambele pachete, versiunea luata fiind **6.2.2**; amandoua sunt
construite din acelasi commit al aceluiasi depozit (`d9ebf82`), iar niciunul nu aduce un fisier
nativ.

**Compatibilitate.** BSD-3-Clause e permisiva si compatibila cu GPLv3; Free Software Foundation
listeaza licenta BSD modificata (cea cu 3 clauze) printre cele compatibile cu GPL. Din cele trei
conditii ale ei, cea pentru sursa nu se aplica (nicio sursa NLog nu e copiata aici); cea pentru
forma binara cere ca notita de copyright, conditiile si declinarea raspunderii sa insoteasca
distributia, iar fereastra "Despre" arata notita, numele licentei si legatura ei, ca pentru
ambalajul MIT, in timp ce sectiunea asta reproduce textul intreg, cuvant cu cuvant din
`LICENSE.txt` de la commit-ul din care e construita versiunea 6.2.2 (`d9ebf82`), asa cum l-a
citit FlatCheck pentru aceleasi pachete (ADR 0070 al lui); a treia interzice folosirea numelor
autorilor pentru a recomanda LightsCheck, ceea ce nimic de aici nu face.

> Copyright (c) 2004-2024 Jaroslaw Kowalski <jaak@jkowalski.net>, Kim Christensen, Julian Verdurmen
>
> All rights reserved.
>
> Redistribution and use in source and binary forms, with or without
> modification, are permitted provided that the following conditions
> are met:
>
> * Redistributions of source code must retain the above copyright notice,
>   this list of conditions and the following disclaimer.
>
> * Redistributions in binary form must reproduce the above copyright notice,
>   this list of conditions and the following disclaimer in the documentation
>   and/or other materials provided with the distribution.
>
> * Neither the name of Jaroslaw Kowalski nor the names of its
>   contributors may be used to endorse or promote products derived from this
>   software without specific prior written permission.
>
> THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
> AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
> IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
> ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
> LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
> CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
> SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
> INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
> CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
> ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF
> THE POSSIBILITY OF SUCH DAMAGE.

Metadatele ambelor pachete poarta o a doua notita, `Copyright (c) 2004-2026 NLog Project -
https://nlog-project.org/`.

**Alternativele cantarite.** `Microsoft.Extensions.Logging` cere un al doilea pachet pentru un
fisier; Serilog face aceeasi treaba, iar proprietarul a cerut NLog pe nume. `NLog.Targets.AtomicFile`
a fost masurat de FlatCheck fata de tinta de fisier obisnuita a NLog inainte sa fie luat: doua
procese care scriu cate 2000 de linii deodata au pierdut 33 pana la 48 din cele 4000 de linii cu
tinta obisnuita inchisa intre linii, 67 pana la 72 cu ea tinuta deschisa, si niciuna cu tinta
atomica (ADR 0070 al FlatCheck).

- Proiect: <https://nlog-project.org/>
- Licenta: <https://github.com/NLog/NLog/blob/master/LICENSE.txt>

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
