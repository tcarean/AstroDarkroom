# AstroPhotoAnalyzer

Măsoară o imagine astro **finită** (TIFF, PNG sau FITS deja stretch-uit) și spune cum să
fie trimisă la print: în ce format, dacă fundalul e destul de sus pentru hârtie, ce
tentă are cerul, la ce mărime ține și cât denoise îi trebuie. Nu exportă nimic — produce
doar un raport cu recomandări, fiecare cu cifra și pragul din spatele ei. Exportul se face
curat, din Siril.

Două aplicații peste un nucleu comun: `AstroPhotoAnalyzer.exe` (consolă) și
`AstroPhotoAnalyzer.Wpf.exe` (fereastră, temă întunecată). Niciuna nu reimplementează o
cifră.

**Stare: în lucru.** Consola e completă (etapele 0–4 din `docs/implementation-plan.md`):
citește fișierul, face toate măsurătorile și scrie raportul. Fereastra vine în etapele 5–6.

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

## Licență

GPLv3 sau ulterioară — vezi [LICENSE](LICENSE). Componentele terțe, cu licența fiecăreia
și motivul compatibilității, sunt în [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

### Componente terțe

| componentă | rol | licență |
|---|---|---|
| SixLabors.ImageSharp | citirea TIFF/PNG/JPEG, encodarea JPEG pentru tabelul de compresie | Six Labors Split License 1.0 (termenii Apache 2.0) |
