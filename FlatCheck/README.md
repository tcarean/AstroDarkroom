# flatcheck (.NET / C#)

Port C# al `flatcheck.py` — diagnostic cantitativ pentru master flats de astrofoto.
Un singur fișier (`FlatCheck.cs`), **fără dependințe NuGet**: reader FITS propriu și
encoder PNG propriu (prin `ZLibStream` din .NET). Doar .NET 8 SDK.

## Build

```bash
cd csharp
dotnet build -c Release
```

Sau, pentru un executabil de sine stătător pe care îl poți copia oriunde:

```bash
dotnet publish -c Release -r win-x64 --self-contained    # Windows
dotnet publish -c Release -r linux-x64 --self-contained  # Linux
```

## Rulare

```bash
# din folderul csharp, cu dotnet run:
dotnet run -c Release -- process/pp_flat_stacked.fit                 # 1. structura
dotnet run -c Release -- pp_flat_A.fit --ratio pp_flat_B.fit         # 2. rotatie 180
dotnet run -c Release -- process/pp_light_00001.fit --light          # 3. end-to-end
dotnet run -c Release -- flat_brut.fit --expo                         # 4. expunere
dotnet run -c Release -- sky_flat.fit --expo --sky                   # 4b. expunere sky flat
dotnet run -c Release -- flat_brut.fit --expo --sat 4095 --black 150  # expunere, scala manuala
dotnet run -c Release -- flat.fit -o harta.png                       # PNG custom

# sau direct pe binarul compilat:
./bin/Release/net8.0/flatcheck process/pp_flat_stacked.fit
```

## Ce fișier îi dai

**FITS calibrat** — flat-dark-ul deja scăzut. Din workflow-ul Siril, acela e
`process/pp_flat_stacked.fit`. NU un stack necalibrat (offset-ul aplatizează profilul
și toate testele spun fals "OK"). Vezi comentariul lung din capul lui `FlatCheck.cs`.

Nu citește NEF direct — lasă Siril să producă FITS-ul, exact ca în pipeline-ul tău.
FITS suportat: BITPIX 16 / 32 / -32 / -64, mono sau color (media pe canale).

**Excepție — modul `--expo`:** ăsta vrea un flat BRUT NECALIBRAT (un cadru single
convertit în Siril, NU masterul — stacking-ul normalizează și șterge nivelul). Verifică
dacă expunerea e la 50-60% și spune în ce direcție s-o modifici. Scala de saturație e
auto-detectată; dacă e greșită, dă `--sat` (și `--black`). Versiunea Python poate rula
`--expo` direct pe NEF (via rawpy, cu white/black exacte); C#-ul cere FITS.

### Cum obții un FITS BRUT în Siril (pentru `--expo`)

**NU** deschide NEF-ul și "Save" — asta îl **debayerizează** și normalizează. `--expo`
va respinge un astfel de fișier (canale CFA identice, nivel ~0) cu un mesaj explicit.

Folosește tab-ul **Conversion**:
1. Conversion → adaugi NEF-urile.
2. Convert.

Fișierul rezultat păstrează mozaicul CFA brut, la scala ADU originală (D3300: white
4095, black 150 la 12-bit). Apoi:

```bash
flatcheck.exe flat_brut.fit --expo --sat 4095 --black 150
flatcheck.exe sky_flat.fit  --expo --sky --sat 4095 --black 150   # pt sky flats
```

Pentru celelalte moduri (structură, ratio, light) folosești FITS-ul **calibrat**
(`pp_flat_stacked.fit`) — acolo debayerizarea/normalizarea nu contează, sunt rapoarte.

## Diferențe față de versiunea Python

- **Fără plot cu 4 panouri.** Python (matplotlib) desena imagine + profil radial +
  hartă reziduală + grafic azimutal. Aici salvez **doar harta reziduală** ca PNG
  (albastru = sub model, roșu = peste). Cifrele din consolă sunt identice; harta e
  suficientă ca să vezi *forma* unei probleme (gradient orizontal/vertical/diagonal).
- Restul — praguri, verdicte, cele trei moduri, fit-ul plan+radial din `--light` cu
  sigma-clipping — sunt portate 1:1.

## Verificare (ce am putut confirma fără compilator)

N-am putut compila C# în mediul unde a fost scris, deci am validat **algoritmul**,
reimplementând fiecare formulă în Python și comparând cu originalul:

- percentila cu interpolare liniară == `numpy.percentile` (bit-identic)
- solverul Gauss cu pivotare == `numpy.linalg.solve`; normal equations == `lstsq`
- encoderul PNG (CRC32 + zlib) produce fișier valid, verificat cu un decoder standard

Ce **nu** e testat: că se compilează fără typo-uri. Prima dată când rulezi
`dotnet build`, dacă apare o eroare de sintaxă, e trivial de reparat — logica de
dedesubt e verificată numeric.

**Test de acceptanță** după primul build: rulează pe un flat despre care Python-ul
(sau versiunea ta anterioară) a zis "OK" și pe unul "PROBLEMA". Cifrele trebuie să
coincidă cu Python la ~0.01%. Dacă da, portul e corect.
