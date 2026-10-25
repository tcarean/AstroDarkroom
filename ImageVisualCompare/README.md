# ImageVisualCompare

Comparator vizual pentru două imagini de aceeași dimensiune, cu o linie verticală care se
trage stânga-dreapta: în stânga liniei vezi prima imagine, în dreapta a doua, în același
loc și la același zoom. Făcut pentru a compara două variante ale aceleiași procesări astro
— înainte și după extracția stelelor, două setări de background extraction, două stretch-uri.

Aplicație WPF pe .NET 8, Windows, x64, **fără nicio dependință NuGet**. Decodarea se face
prin WIC, codecul din Windows pe care îl folosește WPF oricum; fișierele FITS, pe care WIC nu
le știe, sunt citite de un cititor propriu, tot fără dependințe.

Totul e întunecat, indiferent de tema sistemului — fereastra, bara de titlu și casetele de
mesaj — fiindcă un chrome luminos în jurul unei imagini astro strică percepția a ce e cu
adevărat întunecat în poză. Singura excepție e dialogul de deschidere fișiere: ăla e dialogul
comun Windows și urmează tema sistemului, nu a aplicației.

> **Documentația de utilizator stă aici, în acest fișier.** Nu o duplica în comentarii.

---

## Ce fișiere îi dai

PNG sau TIFF, orice adâncime de biți și orice combinație de canale pe care WIC le poate
citi: 8 sau 16 biți pe canal, RGB, RGBA, grayscale, paletizat, și TIFF cu valori float pe 32
de biți (vezi „TIFF float" mai jos). Și FITS, cu ce e scris mai jos la „Fișiere FITS". Totul
se normalizează la Bgra32 la încărcare, iar formatul real e afișat în bara de stare ca să știi
mereu ce ai deschis; singurul loc în care codul întreabă ce format are fișierul e cel al
valorilor float dintr-un TIFF.

Adâncimea de biți și numărul de canale *pot* să difere între cele două — poți compara un PNG
pe 16 biți cu un TIFF pe 8 biți fără probleme.

**Dacă dimensiunile în pixeli diferă, ești întrebat înainte să se încarce ceva.** Mesajul arată
ambele rezoluții și dimensiunea pânzei comune (maximul pe fiecare axă). Dacă accepți, cele două
se compară pe acea pânză, fiecare ancorată în colțul din stânga-sus, iar restul rămâne negru —
imaginea mai îngustă are negru în dreapta, cea mai scundă are negru dedesubt. Dacă refuzi, se
redeschide selectorul de fișiere.

Fișierele de pe disc nu sunt atinse, și nici pixelii din memorie: negrul e fundalul peste care
se desenează oricum ambele imagini, nu o copie mai mare a lor. Citirea RGB din bara de stare
arată `—` acolo unde o imagine nu ajunge, deci se vede imediat că ești în zona de padding și
nu într-o zonă neagră din poză.

**Un fișier care nu poate fi citit** (lipsă, blocat, stricat, într-un format pe care nu-l
citește niciun codec instalat, sau prea mare ca să fie decodat) dă un mesaj cu numele lui și
motivul, apoi se redeschid dialogurile. Aplicația nu se închide.

Un export „32 bit" din Siril ajunge pe disc ca PNG pe **16 biți pe canal** (48 bpp), fiindcă
formatul PNG nu are un mod pe 32 de biți. Windows raportează corect „bit depth 48".

### Fișiere FITS

Poți deschide direct și fișiere FITS (`.fit`, `.fits`, `.fts`), ca imagine A, ca imagine B sau
ca amândouă, inclusiv un FITS față în față cu un PNG sau un TIFF. Se citesc întregi pe 8, 16 și
32 de biți și float pe 32 și 64 de biți, cu un plan (afișat gri) sau cu trei (RGB). Fișierul e
recunoscut după conținut, nu după extensie.

- **Valorile se afișează așa cum sunt în fișier, fără niciun stretch.** Un FITS deja procesat
  arată exact ca exportul lui PNG sau TIFF pe 16 biți, nivel cu nivel, deci îl poți compara
  cinstit cu exportul. Un stack liniar, nestretch-uit, apare aproape negru: e corect, așa e
  el. Stretch-uiește-l în Siril și salvează-l dacă vrei să-l vezi.
- **Orientarea e cea din Siril.** Se respectă `ROWORDER` din antet; un fișier fără acest
  cuvânt-cheie e considerat scris de jos în sus, cum îl arată și Siril.
- **Float în 0..1 sau în 0..65535.** Siril scrie float între 0 și 1. Dacă mai mult de 1% din
  valorile din fișier trec de 2, fișierul e citit ca fiind între 0 și 65535, iar bara de stare
  o spune: `32-bit float RGB (0..65535)`. Câțiva pixeli fierbinți peste 2 nu schimbă scara.
  Valorile negative și NaN devin negru, cele peste maxim devin alb.
- **Ce nu se citește:** FITS comprimat (tile-compressed, `.fz`), cuburi cu alt număr de
  planuri decât 1 sau 3, întregi pe 64 de biți. Primești un mesaj care spune care dintre ele
  e, nu o eroare generică; la fel pentru un antet stricat sau o imagine prea mare ca să fie
  afișată. Un cadru brut cu matrice Bayer se deschide, dar apare ca mozaicul
  gri care este: nu se face debayer.

### TIFF float

Un TIFF cu valori float pe 32 de biți, cum sunt exporturile „32 bit" din GraXpert și din
Siril, e citit ca un FITS cu aceleași valori: bara de stare spune `32-bit float RGB` (sau
`gray`, `RGBA`), iar valorile se afișează așa cum sunt în fișier, fără stretch, între 0 și 1
sau între 0 și 65535, după aceeași regulă ca la FITS. Conversia proprie a Windows le-ar fi
arătat mult mai luminoase, în medie cu ~73 de niveluri din 255 pe exporturile GraXpert: stack-ul
pe 16 biți de dinainte de GraXpert și rezultatul float de după ar fi părut unul de două ori mai
luminos decât celălalt, deși în date nu e așa.

## Cum o pornești

Fără argumente, cere cele două fișiere prin două dialoguri succesive. Al doilea dialog se
deschide direct în folderul primului.

Cu două căi ca argumente, sare peste dialoguri:

```bash
ImageVisualCompare.Wpf.exe "A.png" "B.png"
```

Prima cale e imaginea afișată în stânga liniei, a doua cea din dreapta. Dacă una dintre
căi nu există, pornește normal și întreabă prin dialoguri.

Fereastra pornește maximizată, ca zoom-ul „fit" să fie calculat de la început pe tot ecranul.

### Limba interfeței

Interfața e în engleză sau în română. Limba o alegi din linia de comandă, cu `--lang`:

```bash
ImageVisualCompare.Wpf.exe --lang=ro "A.png" "B.png"
```

Merge și `--lang ro`, cu spațiu, și poate sta oriunde printre argumente; nu contează literele
mari sau mici. Fără `--lang`, interfața e în engleză. Dacă limba nu e una pe care aplicația o
are (`--lang=de`) sau lipsește (`--lang=`), primești întâi un mesaj, apoi interfața pornește în
engleză.

Se traduc toate textele: butoanele, titlurile, bara de stare, mesajele și întrebările. Numerele
arată la fel în ambele limbi (`5938 × 3888`, `152%`, `560 MB`), iar mesajele care vin de la
Windows, cum e motivul pentru care un codec n-a putut citi un fișier, sunt în limba sistemului.
În tabelele de mai jos, numele din paranteză e cel din interfața în română.

## Controale

| acțiune | cum |
|---|---|
| mută linia | trage de linie (oriunde pe ea) sau de mâner |
| mută linia fin | `←` / `→` cu 1 px, `Shift+←` / `Shift+→` cu 10 px |
| pan | trage cu mouse-ul oriunde în afara liniei |
| zoom | rotița, ancorat în cursor (și `Ctrl` + rotița, la fel) |
| scroll orizontal | `Shift` + rotița |
| zoom pe trepte | `+` / `−`, sau butoanele din toolbar |
| încadrează tot | `Ctrl+0`, sau butonul `Fit` (`Încadrează`) |
| mărime reală | `Ctrl+1`, sau butonul `100%` |
| sari în altă zonă | click sau drag în minimap-ul din stânga-jos |
| deschide altă pereche | `Ctrl+O`, sau butonul `Open…` (`Deschide…`; nu în timpul unei încărcări) |

Scurtăturile de tastatură merg când suprafața de comparație are focus; îl primește singură
după încărcare, și îl recapătă la orice click pe imagine. Pasul săgeților e în pixeli
logici, ca tot ce ține de linie: la scalarea Windows de 100% e un pixel de ecran, la 150%
linia se mută cu 1,5 pixeli de ecran, respectiv 15 cu `Shift`.

**Zoom-ul e limitat în jos la încadrarea completă** — sub asta ai doar o miniatură pierdută
pe un fundal gol. Excepție: o imagine mai mică decât fereastra poate coborî până la 100%,
altfel n-ai putea-o vedea la rezoluția ei. În sus limita e 1600%.

**„100%" înseamnă un pixel de imagine peste un pixel fizic de ecran**, nu peste un pixel
logic. Pe un ecran cu scalare Windows la 125% sau 150% diferența contează, iar la
pixel peeping vrei varianta onestă. Ține pe orice monitor, și când monitoarele au scalări
diferite: aplicația urmărește scalarea monitorului pe care e fereastra. Peste 400% se trece
automat pe nearest-neighbour, ca să vezi pixelii reali, nu interpolare.

## Ce îți arată bara de stare

```
5794 × 3637 · A 16-bit RGB · B 16-bit RGB    2617, 1839    A  35  35  36    B  35  35  35    managed 2 MB, process 560 MB
```

Dimensiunea perechii și formatul real al fiecărui fișier, coordonata pixelului de sub cursor,
și **valorile RGB din ambele imagini în acel pixel**. Ultima parte e adesea singurul mod de a
vedea o diferență: pe un fundal întunecat, o diferență de una-două unități din 255 e complet
invizibilă cu ochiul, dar se citește imediat în cifre.

Pentru un FITS, ca și pentru un TIFF float, formatul arată adâncimea și planurile din fișier,
de exemplu `A 32-bit float RGB` sau `B 16-bit gray`. Valorile RGB citite sunt tot cele 0–255
de pe ecran, nu valorile float din fișier.

Când perechea a avut nevoie de padding, prima parte devine `600 × 400 padded · A 600 × 300 ·
B 400 × 400`: dimensiunea pânzei singură ar fi o jumătate de adevăr, fiindcă niciuna dintre
imagini nu are dimensiunea aceea.

În interfața în română, aceeași bară arată `A RGB pe 16 biți` (sau `gri float pe 32 de biți`,
`RGB float pe 32 de biți (0..65535)`), `memorie .NET 2 MB, proces 560 MB`, iar la padding
`pânză 600 × 400 · A 600 × 300 · B 400 × 400`.

În dreapta barei de stare stau permanent versiunea și copyright-ul. Butonul `About` (`Despre`)
din toolbar arată nota completă de licență.

## Memorie

O pereche de 21 MP pe 16 biți ocupă ~170 MB după decodare (fiecare imagine devine Bgra32,
adică 84 MB, indiferent cât avea pe disc). Cu randarea WPF pornită, procesul se așază pe un
platou de ~570 MB memorie privată și ~300 MB rezidenți. Platoul e mărginit, nu o scurgere —
plimbarea prin nivele de zoom nu îl mai crește. La deschiderea unei perechi noi, cea veche
rămâne pe ecran cât timp se decodează noua, deci în timpul încărcării stau amândouă în
memorie; imediat ce noua e gata, cea veche e eliberată explicit, înainte ca noua să fie
afișată, și de acolo rămâne una singură.

Decodarea a două fișiere de ~108 MB durează ~2,7 secunde, cu bară de progres.

## Compilare

Cere .NET SDK 9.0.3xx pe Windows (`global.json`); proiectele țintesc .NET 8.

```bash
dotnet build ImageVisualCompare.sln -c Release
```

```bash
dotnet test ImageVisualCompare.sln
```

Gate-ul complet, cel pe care îl rulează hook-ul `pre-commit`, e `scripts/verify.ps1` (vezi mai
jos).

### Dezvoltare: gate-ul, documentația și Claude Code

Din 2026-10-02 proiectul urmează procesul din `ClaudeCodeStarterKit` (folderul de lângă acesta
în repo-ul `photo`; ghidul cu motivația fiecărei piese e `GHID.md` acolo), la fel ca FlatCheck
și AstroPhotoAnalyzer. Pe scurt, ce e impus mecanic:

- **`scripts/verify.ps1` e gate-ul**: scripturile identice cu ale kit-ului, manifestul datelor
  de test, antetele ADR-urilor, notele de design (fiecare în index, cu fișierele ei numite în
  propoziția `Covers:`), restore, `dotnet format` (doar verifică), build cu avertismentele ca
  erori, toate testele în afară de cele de validare. Cam 20 de secunde. Îl rulează hook-ul git
  `pre-commit` de la rădăcina repo-ului la orice commit care atinge proiectul, hook-ul
  `pre-merge-commit` la orice merge fără conflicte, hook-ul Claude Code înainte de orice
  `git commit` al modelului, și `scripts/release.ps1`. Fără el verde nu intră niciun commit.

  ```bash
  powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
  ```

- **Mesajul de commit** e o singură linie, în engleză, de cel mult 100 de caractere.
- **`scripts/`, `.claude/skills/`, `.claude/agents/` și `.claude/rules/kit/` sunt ale
  kit-ului**, byte cu byte, și nu se editează aici; ce e specific proiectului ca valori e în
  `kit.json`, pașii pe care îi adaugă la skill-uri sunt în `docs/skills.md`.
- **Comportamentul e în `docs/requirements.md`**, cu ID-uri `R-NNN` purtate în numele testelor;
  `RepoInvariantTests` cere ca fiecare cerință `implemented` să aibă un test cu ID-ul ei și
  invers. Vechiul `CLAUDE.md` a fost împărțit pe 2026-10-02 în cerințe, decizii și note de
  design; nimic nu s-a pierdut, s-a mutat.
- **Deciziile sunt în `docs/decisions/`**, un ADR per decizie, cu alternativele respinse; cele
  luate înainte de 2026-10-02 (cele zece invariante din vechiul `CLAUDE.md`, printre altele) au
  fost scrise retroactiv.
- **Cifrele pe o pereche reală** (exporturi de 23 MP ale aceleiași imagini, care nu sunt în
  repo) sunt în `docs/validation.json`, citit de `ValidationTests` (`--filter
  "Category=Validation"`, nu în gate); copiile stau în `tests/testdata/large/`, ignorat de git,
  iar `tests/testdata/manifest.json` le ține hash-urile. `docs/validation.md` spune de unde vine
  fiecare cifră, lângă măsurătorile de memorie din 2026-09-08.
- **Explicațiile din spatele codului** sunt în `docs/design/`, cu indexul în
  `docs/design/README.md`; `CLAUDE.md` ține doar regulile proiectului, cele de proces sunt ale
  kit-ului, în `.claude/rules/kit/process.md`.
- **Versiunea** o crește omul, în `Directory.Build.props` (mutată acolo din csproj pe
  2026-10-02). Un release se face pe `main`, cu `scripts/release.ps1 -Version X.Y.Z`, care
  rulează gate-ul, închide secțiunea din CHANGELOG și publică aplicația în `artifacts/X.Y.Z/`.
  Commit-ul și tag-ul rămân ai omului.
- În Claude Code: `/new-feature`, `/wrap-up`, `/validate`, `/docs-health`, `/mutate`, `/release`.

## Ce nu face (încă)

- Fără mod diferență (`|A−B|` amplificat). Ar cere păstrarea masterelor pe 16 biți, adică
  încă ~250 MB.
- Fără toggle A/B pe tastă.
- Fără drag&drop peste fereastră.
- Nu reține poziția ferestrei sau ultimul folder între sesiuni.

---

## Licență

Image Visual Compare — comparator vizual pentru două imagini de aceeași dimensiune.
Copyright (C) 2026 Tudor Carean

Acest program este software liber: îl poți redistribui și/sau modifica în termenii
**GNU General Public License v3** publicată de Free Software Foundation, fie versiunea 3
a licenței, fie (la alegerea ta) orice versiune ulterioară.

Programul e distribuit în speranța că va fi util, dar **FĂRĂ NICIO GARANȚIE** — fără
măcar garanția implicită de VANDABILITATE sau POTRIVIRE PENTRU UN SCOP ANUME. Vezi GNU
General Public License pentru detalii.

Textul complet al licenței e în fișierul [LICENSE](LICENSE), care stă și lângă program, în
folderul în care e compilat sau publicat; îl găsești și la
<https://www.gnu.org/licenses/gpl-3.0.html>. Aceeași notă, pe scurt, apare la butonul
`About`; versiunea și copyright-ul stau permanent în bara de stare.
