# AstroPhotoAnalyzer

Măsoară o imagine astro **finită** (TIFF, PNG sau FITS deja stretch-uit) și spune cum să
fie trimisă la print: în ce format, dacă fundalul e destul de sus pentru hârtie, ce
tentă are cerul, la ce mărime ține și cât denoise îi trebuie. Nu exportă nimic — produce
doar un raport cu recomandări, fiecare cu cifra și pragul din spatele ei. Exportul se face
curat, din Siril.

Două aplicații peste un nucleu comun: `AstroPhotoAnalyzer.exe` (consolă) și
`AstroPhotoAnalyzer.Wpf.exe` (fereastră, temă întunecată). Niciuna nu reimplementează o
cifră.

**Stare: complet.** Consola și fereastra fac același lucru peste același nucleu; unealta
a fost rulată pe imaginea de referință și cifrele sunt în `docs/validation.md`. Ce face
unealta — fiecare măsurătoare, regulă și prag — e în `docs/requirements.md`, cu ID-uri; cum
funcționează și de ce, în `docs/design/` și `docs/decisions/`.

## Fereastra

```bash
AstroPhotoAnalyzer.Wpf.exe "E:\Astro\poza.tif"     # optional: imaginea
```

Stânga: parametrii (mediu, Dmax, distanță, lățimi, raport de aspect, fundal-țintă,
passepartout, ramă) și „Analizează"; distanța, raportul de aspect, fundalul-țintă,
passepartout-ul și rama explică la trecerea mouse-ului ce înseamnă, de unde vine
valoarea implicită și când merită schimbată. Dreapta, șase taburi: **Raport** (deciziile, cu
pastile de verdict și „fiindca" sub fiecare; raportul text pliabil), **Tonuri**
(histograma L\* cu negrul hârtiei marcat, cifrele tonale cu o explicație sub fiecare,
simularea de fundal, tabela nivel - L\* a reperelor și un panou despre cum se citește
tabul), **Export** (tabelul JPEG cu explicația coloanelor sub el, rotunjirea, stresul
shadow-lift, palierul), **Rezoluție** (FWHM pe regiuni, tabelul de mărimi, scara dpi -
distanță), **Culoare** (R/G/B ale fundalului și cele opt margini, cu „! colorat" / „! neutru" și
explicația tabelului), **Preview** (decupaje la 100 % și 200 %,
cadrul întreg, simularea de print, decupajul la raportul țintă, fiecare cu ce ai de
verificat în ea, și regula ecran-hârtie cu pasul înapoi calculat pe imagine). În toate
taburile, fiecare cifră are sub ea ce măsoară și pragul de care se judecă. „Salvează
raportul..." scrie `Raport.txt` — byte-identic cu ieșirea consolei. Temă întunecată;
F1 = Despre.

## Consola

```bash
AstroPhotoAnalyzer poza.tif
```

```bash
AstroPhotoAnalyzer poza.tif --medium baryta --distance 1.5 --sizes 60,70,80 --aspect 50:35 -o raport.txt
```

Opțiuni: `--medium baryta|canvas|acrilic`, `--dmax`, `--distance` (m), `--sizes` (cm),
`--aspect` (l:i), `--target-bg`, `--passepartout`, `--frame`, `-o`. `--help` le explică.
Coduri de ieșire: 0 ok, 1 eroare, 2 atenție, 3 problemă.

Raportul începe cu deciziile — format, fundal, culoare, mărime, denoise, avertismente —
fiecare cu un rând `fiindca:` cu cifra și pragul, apoi anexele cu tabelele. O imagine
liniară (nestretch-uită) e refuzată cu mesaj, nu măsurată greșit.

## Compilare

```bash
dotnet build AstroPhotoAnalyzer.sln -c Release
```

```bash
dotnet test AstroPhotoAnalyzer.sln
```

Soluția e Windows-only din cauza ferestrei; consola singură se compilează oriunde cu
`dotnet build AstroPhotoAnalyzer/AstroPhotoAnalyzer.csproj`. Binar de sine stătător:

```bash
dotnet publish AstroPhotoAnalyzer/AstroPhotoAnalyzer.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

```bash
dotnet publish AstroPhotoAnalyzer.Wpf/AstroPhotoAnalyzer.Wpf.csproj -c Release -r win-x64 --self-contained
```

### Dezvoltare: gate-ul, documentația și Claude Code

Din 2026-10-02 proiectul urmează procesul din `ClaudeCodeStarterKit` (folderul de lângă acesta
în repo-ul `photo`; ghidul cu motivația fiecărei piese e `GHID.md` acolo), la fel ca FlatCheck.
Pe scurt, ce e impus mecanic:

- **`scripts/verify.ps1` e gate-ul**: scripturile identice cu ale kit-ului, manifestul datelor
  de test, antetele ADR-urilor, notele de design (fiecare în index, cu fișierele ei numite în
  propoziția `Covers:`), restore, `dotnet format` (doar verifică), build cu avertismentele ca
  erori, toate testele în afară de cele de validare. Cam o jumătate de minut. Îl rulează
  hook-ul git `pre-commit` de la rădăcina repo-ului la orice commit care atinge proiectul,
  hook-ul `pre-merge-commit` la orice merge fără conflicte, hook-ul Claude Code înainte de
  orice `git commit` al modelului, și `scripts/release.ps1`. Fără el verde nu intră niciun
  commit.

  ```bash
  powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
  ```

- **Mesajul de commit** e o singură linie, în engleză, de cel mult 100 de caractere.
- **`scripts/`, `.claude/skills/`, `.claude/agents/` și `.claude/rules/kit/` sunt ale
  kit-ului**, byte cu byte, și nu se editează aici; ce e specific proiectului ca valori e în
  `kit.json`, pașii pe care îi adaugă la skill-uri sunt în `docs/skills.md`.
- **Comportamentul e în `docs/requirements.md`**, cu ID-uri `R-NNN` purtate în numele testelor;
  `RepoInvariantTests` cere ca fiecare cerință `implemented` să aibă un test cu ID-ul ei și
  invers. Specificația de dinainte (`docs/specification.md`) a fost împărțită pe 2026-10-02 în
  cerințe, decizii și note de design; nimic nu s-a pierdut, s-a mutat.
- **Deciziile sunt în `docs/decisions/`**, un ADR per decizie, cu alternativele respinse; cele
  luate înainte de 2026-10-02 au fost scrise retroactiv din specificație și din planul de
  implementare.
- **Cifrele pe imaginea de referință** sunt în `docs/validation.json`, citit de
  `ValidationTests` (`--filter "Category=Validation"`, nu în gate). Imaginea, `de_printat.png`,
  nu intră în repo: o copie făcută de mână stă în `AstroPhotoAnalyzer.Tests/testdata/large/`,
  pe care git îl ignoră, iar `AstroPhotoAnalyzer.Tests/testdata/manifest.json` îi ține hash-ul.
  Pe alt calculator sau într-un worktree nou, copia se face din nou. `docs/validation.md` spune
  de unde vine fiecare cifră, lângă numerele făcute de mână în conversația care a pornit unealta.
- **Explicațiile din spatele codului** sunt în `docs/design/`, cu indexul în
  `docs/design/README.md`; `CLAUDE.md` ține doar regulile proiectului, cele de proces sunt ale
  kit-ului, în `.claude/rules/kit/process.md`.
- **Versiunea** o crește omul, în `Directory.Build.props`. Un release se face pe `main`, cu
  `scripts/release.ps1 -Version X.Y.Z`, care rulează gate-ul, închide secțiunea din CHANGELOG
  și publică în `artifacts/X.Y.Z/` (consola ca fișier unic, fereastra separat). Commit-ul și
  tag-ul `astrophotoanalyzer-vX.Y.Z` rămân ale omului.
- În Claude Code: `/new-feature`, `/wrap-up`, `/validate`, `/docs-health`, `/mutate`, `/release`.

## Licență

GPLv3 sau ulterioară — vezi [LICENSE](LICENSE). Componentele terțe, cu licența fiecăreia
și motivul compatibilității, sunt în [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

### Componente terțe

| componentă | rol | licență |
|---|---|---|
| SixLabors.ImageSharp | citirea TIFF/PNG/JPEG, encodarea JPEG pentru tabelul de compresie | Six Labors Split License 1.0 (termenii Apache 2.0) |
