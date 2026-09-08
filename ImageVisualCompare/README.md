# ImageVisualCompare

Comparator vizual pentru două imagini de aceeași dimensiune, cu o linie verticală care se
trage stânga-dreapta: în stânga liniei vezi prima imagine, în dreapta a doua, în același
loc și la același zoom. Făcut pentru a compara două variante ale aceleiași procesări astro
— înainte și după extracția stelelor, două setări de background extraction, două stretch-uri.

Aplicație WPF pe .NET 9, Windows, x64, **fără nicio dependință NuGet**. Decodarea se face
prin WIC, codecul din Windows pe care îl folosește WPF oricum.

> **Documentația de utilizator stă aici, în acest fișier.** Nu o duplica în comentarii.

---

## Ce fișiere îi dai

PNG sau TIFF, orice adâncime de biți și orice combinație de canale pe care WIC le poate
citi: 8 sau 16 biți pe canal, RGB, RGBA, grayscale, paletizat. Nu există nicio ramură în
cod care să întrebe ce format are fișierul — totul se normalizează la Bgra32 la încărcare,
iar formatul real e afișat în bara de stare ca să știi mereu ce ai deschis.

**Singura condiție: cele două imagini trebuie să aibă exact aceleași dimensiuni în pixeli.**
Altfel comparația n-ar însemna nimic, iar aplicația refuză perechea cu un mesaj care arată
ambele rezoluții. Adâncimea de biți și numărul de canale *pot* să difere între ele — poți
compara un PNG pe 16 biți cu un TIFF pe 8 biți fără probleme.

Un export „32 bit" din Siril ajunge pe disc ca PNG pe **16 biți pe canal** (48 bpp), fiindcă
formatul PNG nu are un mod pe 32 de biți. Windows raportează corect „bit depth 48".

## Cum o pornești

Fără argumente, cere cele două fișiere prin două dialoguri succesive. Al doilea dialog se
deschide direct în folderul primului.

Cu două căi ca argumente, sare peste dialoguri:

```bash
ImageVisualCompare.Wpf.exe "A.png" "B.png"
```

Prima cale e imaginea afișată în stânga liniei, a doua cea din dreapta. Dacă una dintre
căi nu există, pornește normal și întreabă prin dialoguri.

## Controale

| acțiune | cum |
|---|---|
| mută linia | trage de linie (oriunde pe ea) sau de mâner |
| mută linia fin | `←` / `→` cu 1 px, `Shift+←` / `Shift+→` cu 10 px |
| pan | trage cu mouse-ul oriunde în afara liniei |
| scroll vertical | rotița |
| scroll orizontal | `Shift` + rotița |
| zoom | `Ctrl` + rotița, ancorat în cursor |
| zoom pe trepte | `+` / `−`, sau butoanele din toolbar |
| încadrează tot | `Ctrl+0`, sau butonul `Fit` |
| mărime reală | `Ctrl+1`, sau butonul `100%` |
| sari în altă zonă | click sau drag în minimap-ul din stânga-jos |
| deschide altă pereche | `Ctrl+O`, sau butonul `Open…` |

Scurtăturile de tastatură merg când suprafața de comparație are focus; îl primește singură
după încărcare, și îl recapătă la orice click pe imagine.

**Zoom-ul e limitat în jos la încadrarea completă** — sub asta ai doar o miniatură pierdută
pe un fundal gol. Excepție: o imagine mai mică decât fereastra poate coborî până la 100%,
altfel n-ai putea-o vedea la rezoluția ei. În sus limita e 1600%.

**„100%" înseamnă un pixel de imagine peste un pixel fizic de ecran**, nu peste un pixel
logic. Pe un ecran cu scalare Windows la 125% sau 150% diferența contează, iar la
pixel peeping vrei varianta onestă. Peste 400% se trece automat pe nearest-neighbour, ca să
vezi pixelii reali, nu interpolare.

## Ce îți arată bara de stare

```
5794 × 3637 · A 16-bit RGB · B 16-bit RGB    2617, 1839    A  35  35  36    B  35  35  35    managed 2 MB, process 560 MB
```

Dimensiunea perechii și formatul real al fiecărui fișier, coordonata pixelului de sub cursor,
și **valorile RGB din ambele imagini în acel pixel**. Ultima parte e adesea singurul mod de a
vedea o diferență: pe un fundal întunecat, o diferență de una-două unități din 255 e complet
invizibilă cu ochiul, dar se citește imediat în cifre.

## Memorie

O pereche de 21 MP pe 16 biți ocupă ~170 MB după decodare (fiecare imagine devine Bgra32,
adică 84 MB, indiferent cât avea pe disc). Cu randarea WPF pornită, procesul se așază pe un
platou de ~570 MB memorie privată și ~300 MB rezidenți. Platoul e mărginit, nu o scurgere —
plimbarea prin nivele de zoom nu îl mai crește. La deschiderea unei perechi noi, cea veche e
eliberată explicit înainte ca noua să se așeze, deci nu stau două perechi în memorie simultan.

Decodarea a două fișiere de ~108 MB durează ~2,7 secunde, cu bară de progres.

## Compilare

Cere .NET 9 SDK pe Windows.

```bash
dotnet build ImageVisualCompare.sln -c Release
```

```bash
dotnet test ImageVisualCompare.sln
```

## Ce nu face (încă)

- Fără mod diferență (`|A−B|` amplificat). Ar cere păstrarea masterelor pe 16 biți, adică
  încă ~250 MB.
- Fără toggle A/B pe tastă.
- Fără drag&drop peste fereastră.
- Nu reține poziția ferestrei sau ultimul folder între sesiuni.
