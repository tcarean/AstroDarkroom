# AstroPhotoAnalyzer

Măsoară o imagine astro **finită** (TIFF, PNG sau FITS deja stretch-uit) și spune cum să
fie trimisă la print: în ce format, dacă fundalul e destul de sus pentru hârtie, ce
tentă are cerul, la ce mărime ține și cât denoise îi trebuie. Nu exportă nimic — produce
doar un raport cu recomandări, fiecare cu cifra și pragul din spatele ei. Exportul se face
curat, din Siril.

Două aplicații peste un nucleu comun: `AstroPhotoAnalyzer.exe` (consolă) și
`AstroPhotoAnalyzer.Wpf.exe` (fereastră, temă întunecată). Niciuna nu reimplementează o
cifră.

**Stare: în lucru.** Scheletul (etapa 0 din `docs/implementation-plan.md`) e gata;
măsurătorile vin în etapele următoare. Consola rulează, tipărește `--help` și refuză cinstit
să analizeze până atunci.

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
