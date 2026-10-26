# Astro Darkroom

Suita celor cinci unelte de astrofoto din acest repository, intr-un singur download, cu un
lansator care le arata si le porneste:

| Unealta | Ce face | Fereastra |
| --- | --- | --- |
| DarksCheck | verifica dark-urile unei sesiuni sau ale unei biblioteci | `DarksCheck.Wpf.exe` |
| FlatCheck | verifica flat-urile unui obiectiv | `FlatCheck.Wpf.exe` |
| LightsCheck | verifica light-urile unei sesiuni: focus, trailing, dithering, drift | `LightsCheck.Wpf.exe` |
| ImageVisualCompare | compara doua variante ale aceleiasi procesari, pixel cu pixel | `ImageVisualCompare.Wpf.exe` |
| AstroPhotoAnalyzer | verifica o imagine finala inainte de print | `AstroPhotoAnalyzer.Wpf.exe` |

Suita contine doar ferestrele. Consolele (`darkscheck.exe`, `flatcheck.exe`, `lightscheck.exe`,
`AstroPhotoAnalyzer.exe`) raman in release-urile proprii ale fiecarei unelte: runtime-ul lor de
baza si runtime-ul desktop al ferestrelor au trei DLL-uri cu acelasi nume si continut diferit,
deci nu incap in acelasi folder (ADR 0002). Fiecare unealta are documentatia ei de utilizator
in `README.md`-ul folderului ei. Toate sunt GPLv3 sau ulterioara ([LICENSE](LICENSE)).

## Ce primesti

Un zip, `AstroDarkroom-X.Y.Z-win-x64.zip`, cu un fisier `.sha256` alaturi. Dezarhivat, e un
singur folder cu toate executabilele unul langa altul, runtime-ul .NET 8 inclus (nu trebuie
instalat nimic), si `AstroDarkroom.exe`, lansatorul: o fereastra cu un rand pentru fiecare
unealta (titlu, descriere, versiunea ei) si un buton **Deschide**. Un rand
al carui executabil lipseste din folder e dezactivat si spune ce fisier lipseste. **Despre**
(F1) arata versiunea suitei, data ei, licenta si versiunea fiecarei componente, cu mentiunea
"modificari nelansate" cand componenta a fost impachetata cu schimbari pe care propria ei
versiune nu le-a prins inca.

Lansatorul vorbeste engleza sau romana: cele doua butoane **English** / **Romana** din bara de
jos schimba toate textele pe loc, fara repornire. La prima pornire e in engleza, ca uneltele;
alegerea se tine minte in `%LOCALAPPDATA%\AstroDarkroom\settings.json` (`{"language":"ro"}`)
si se aplica la urmatoarea pornire. O unealta deschisa din lansator porneste in limba
lansatorului (primeste `--lang=ro` sau `--lang=en`); pornita direct din folder, porneste in
engleza, cum spune README-ul ei.

`release.json`, in acelasi folder, spune acelasi lucru in forma citibila de programe: versiunea
suitei, data, commit-ul, si pentru fiecare componenta versiunea, commit-ul si daca are
modificari nelansate.

Executabilele nu sunt semnate: pe alt calculator, SmartScreen intreaba la prima pornire.

## Descarcare

Zip-ul fiecarei versiuni sta in Google Drive-ul proprietarului, partajat cu "Oricine are
linkul"; aici e linkul si SHA-256-ul lui, cea mai noua versiune prima (ADR 0005). Dupa
descarcare, verifica hash-ul inainte sa dezarhivezi:

```
certutil -hashfile AstroDarkroom-X.Y.Z-win-x64.zip SHA256
```

Rezultatul trebuie sa fie identic cu coloana SHA-256 a versiunii tale; altfel zip-ul s-a
stricat la descarcare sau nu e cel publicat.

| Versiune | Data | Zip | SHA-256 |
| --- | --- | --- | --- |
| (inca niciuna) | | | |

## Cum se face un release

Din Claude Code, in folderul `AstroDarkroom` al checkout-ului principal, pe `main`, cu toate
folderele curate: `/release`. Scriptul (`scripts/release.ps1`) verifica, scrie versiunea in
`Directory.Build.props` si sectiunea datata in `CHANGELOG.md`, publica lansatorul si ferestrele
celor cinci componente, asa cum sunt pe `main`, in `artifacts/X.Y.Z/` (un fisier care ar fi scris de doua
publicari cu continut diferit opreste release-ul), scrie `release.json` si zip-ul cu `.sha256`
in `artifacts/`. Commit-ul si tag-ul `astrodarkroom-vX.Y.Z` le face omul, cum le propune
skill-ul. Zip-ul se urca de mana in Google Drive, partajat cu "Oricine are linkul"; apoi linkul
si hash-ul din `.sha256` intra ca rand nou, primul, in tabelul de la "Descarcare" si ca singurul
rand al tabelului "Download" din `README.md`-ul de la radacina repository-ului, pagina lui de pe
Bitbucket (ADR 0008), intr-un commit al celor doua fisiere. Componentele nu trebuie lansate
inainte: `release.json` spune ce versiune si ce commit a intrat din fiecare.

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
