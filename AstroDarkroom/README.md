# Astro Darkroom

Suita celor cinci unelte de astrofoto din acest repository, intr-un singur download, cu un
lansator care le arata si le porneste:

| Unealta | Ce face | Fereastra | Consola |
| --- | --- | --- | --- |
| DarksCheck | verifica dark-urile unei sesiuni sau ale unei biblioteci | `DarksCheck.Wpf.exe` | `darkscheck.exe` |
| FlatCheck | verifica flat-urile unui obiectiv | `FlatCheck.Wpf.exe` | `flatcheck.exe` |
| LightsCheck | verifica light-urile unei sesiuni: focus, trailing, dithering, drift | `LightsCheck.Wpf.exe` | `lightscheck.exe` |
| ImageVisualCompare | compara doua variante ale aceleiasi procesari, pixel cu pixel | `ImageVisualCompare.Wpf.exe` | - |
| AstroPhotoAnalyzer | verifica o imagine finala inainte de print | `AstroPhotoAnalyzer.Wpf.exe` | `AstroPhotoAnalyzer.exe` |

Fiecare unealta are documentatia ei de utilizator in `README.md`-ul folderului ei. Toate sunt
GPLv3 sau ulterioara ([LICENSE](LICENSE)).

## Ce primesti

Un zip, `AstroDarkroom-X.Y.Z-win-x64.zip`, cu un fisier `.sha256` alaturi. Dezarhivat, e un
singur folder cu toate executabilele unul langa altul, runtime-ul .NET 8 inclus (nu trebuie
instalat nimic), si `AstroDarkroom.exe`, lansatorul: o fereastra cu un rand pentru fiecare
unealta (titlu, descriere, versiunea ei, numele consolei) si un buton **Deschide**. Un rand
al carui executabil lipseste din folder e dezactivat si spune ce fisier lipseste. **Despre**
(F1) arata versiunea suitei, data ei, licenta si versiunea fiecarei componente, cu mentiunea
"modificari nelansate" cand componenta a fost impachetata cu schimbari pe care propria ei
versiune nu le-a prins inca.

`release.json`, in acelasi folder, spune acelasi lucru in forma citibila de programe: versiunea
suitei, data, commit-ul, si pentru fiecare componenta versiunea, commit-ul si daca are
modificari nelansate.

Executabilele nu sunt semnate: pe alt calculator, SmartScreen intreaba la prima pornire.

## Cum se face un release

Din Claude Code, in folderul `AstroDarkroom` al checkout-ului principal, pe `main`, cu toate
folderele curate: `/release`. Scriptul (`scripts/release.ps1`) verifica, scrie versiunea in
`Directory.Build.props` si sectiunea datata in `CHANGELOG.md`, publica lansatorul si cele cinci
componente, asa cum sunt pe `main`, in `artifacts/X.Y.Z/` (un fisier care ar fi scris de doua
publicari cu continut diferit opreste release-ul), scrie `release.json` si zip-ul cu `.sha256`
in `artifacts/`. Commit-ul si tag-ul `astrodarkroom-vX.Y.Z` le face omul, cum le propune
skill-ul; zip-ul si `.sha256`-ul se urca de mana in Bitbucket Downloads. Componentele nu
trebuie lansate inainte: `release.json` spune ce versiune si ce commit a intrat din fiecare.

Versiunea suitei e `<Version>` din `Directory.Build.props` de aici; prima versiune reala,
1.0.0, se da la cuvantul proprietarului.

## Compilare

.NET SDK 9.0.3xx (`global.json`), tinta .NET 8. `dotnet build AstroDarkroom.sln -c Release`
(fereastra cere Windows). Gate-ul complet (format, build strict, teste): `scripts/verify.ps1`.
Lansatorul singur, din folderul de build, vede toate uneltele ca lipsa: e facut sa ruleze din
folderul suitei, `artifacts/X.Y.Z/`, dupa un `scripts/release.ps1 -Version X.Y.Z -PublishOnly`.

## Pentru cine dezvolta

Regulile de lucru sunt in `CLAUDE.md` si `.claude/rules/kit/process.md`, cerintele cu ID in
`docs/requirements.md`, deciziile in `docs/decisions/`, cum e facut lansatorul in
`docs/design/launcher.md`. Planul intregii suite e in kit:
`ClaudeCodeStarterKit/docs/plans/2026-10-24-build-the-astro-darkroom-suite.md`.
