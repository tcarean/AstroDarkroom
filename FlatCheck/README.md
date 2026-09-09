# flatcheck

Diagnostic cantitativ pentru flat frames de astrofoto. Răspunde la patru întrebări:
e master flat-ul simetric radial, e *setup-ul* de flat curat, corectează flat-ul un
light real, și e un flat brut expus corect.

Consolă mică în C#, cu **o singură dependință**: reader FITS propriu și encoder PNG
propriu (prin `ZLibStream` din BCL), iar pentru fișierele raw de aparat — libraw, prin
`Sdcb.LibRaw`. Un NEF nu e doar un container: pixelii sunt comprimați Huffman cu predictor
și trecuți printr-o curbă de linearizare din MakerNote, și nu există niciun decodor .NET
pur managed pentru asta. .NET 8. Binarul se numește `flatcheck.exe` și, publicat ca
`PublishSingleFile`, se copiază oriunde ca până acum.

Există și o **aplicație cu ferestre**, `FlatCheck.Wpf.exe`, care rulează aceleași patru
moduri pe un folder de sesiune Siril, în patru taburi. Nu e o a doua implementare: stă
peste același nucleu (`FlatCheck.Core`) și produce aceleași cifre și același raport.
Vezi [„Aplicația cu ferestre"](#aplicația-cu-ferestre).

`flatcheck.py` este prototipul Python din care a fost portat. Nu se menține în paralel,
dar rămâne **oracolul numeric**: `crosscheck.py` rulează amândouă implementările pe
aceiași octeți și compară fiecare cifră.

> **Documentația de utilizator stă aici, în acest fișier.** Nu o duplica în docstring-uri
> sau comentarii — au divergat deja o dată.

---

## Ce fișier îi dai

Cea mai probabilă cauză de rezultat greșit nu e în cod, e în fișierul ales.

| mod | ce vrea |
|---|---|
| structură (implicit) | master flat **calibrat** — `process/pp_flat_stacked.fit` |
| `--ratio` | două master flats calibrate, A și B |
| `--light` | light **calibrat cu flat**, **fără background extraction**, **neregistrat** |
| `--expo` | un **singur cadru brut, necalibrat** — de preferat chiar `.NEF`-ul |

Prefixul `pp_` din Siril înseamnă „calibrat". Un stack necalibrat mai are un piedestal
de ~150-600 ADU care aplatizează profilul și subraportează vignetarea cu ~1% — iar
**toate testele vor spune „OK" oricum**, fiindcă profilul rămâne simetric radial, doar
amplitudinea e greșită. Nu e detectabil, doar evitabil.

Formate acceptate: FITS (`.fit` / `.fits` / `.fts`), BITPIX 8/16/32/-32/-64, mono sau
color, **și raw de aparat** (`.nef` / `.cr2` / `.cr3` / `.arw` / `.dng` / `.raf`). Raw-ul
se citește prin libraw — exact biblioteca din spatele lui `rawpy`, cel folosit de
versiunea Python — deci cifrele ies identice, nu doar apropiate.

Pe raw, `--expo` capătă ceva ce pe FITS nu poate avea: nivelul de saturație și cel de
negru scrise de aparat în fișier, în loc să fie ghicite din maximul cadrului.

### ISO-ul nu contează

Nu modifica nimic pentru ISO. Fiecare metrică e un raport — `log2(centru/colț)`,
`(max−min)/medie`, `img/model` — iar ISO e un scalar, deci se simplifică din toate. Dacă
o metrică nouă nu e invariantă la scală, ăsta e un semn că e greșită.

---

## Ideea de bază

Pentru un obiectiv fotografic fără tren optic, un flat *corect* este prin construcție o
funcție **pur radială** centrată pe axa optică. Vignetarea e simetrică rotațional; praful
la f/2 e sub prag.

Prin urmare orice abatere de la simetria radială **nu e vignetare — e eroare**: sursă de
lumină neuniformă, light leak, cameră înclinată, lumină ambientală, hârtie difuzoare
prea aproape de obiectiv.

„Flat bun" nu înseamnă „arată plat". Înseamnă „abaterea de la simetria radială e sub
zgomot". Aia e o cifră, și se poate măsura.

Modelul radial și asimetria se calculează în jurul **centrului senzorului**, nu al
vârfului de iluminare fitat. Fitarea centrului ar absorbi exact gradientul liniar pe care
îl cauți, iar testul s-ar anula singur.

---

## Cele patru moduri

### 1. Structură (implicit)

```bash
flatcheck process/pp_flat_stacked.fit
```

Rulează pe masterul final. Raportează informativ vignetarea și offsetul centrului de
iluminare față de centrul senzorului, apoi patru teste de abatere de la simetria radială:
împrăștierea celor patru colțuri, asimetria azimutală pe 12 sectoare, **reziduul după
modelul radial** — cel care contează cel mai mult, fiindcă măsoară direct cât din cadru nu
se explică printr-un model pur radial — și înclinarea, adică amplitudinea vârf-la-vârf a
gradientului liniar peste cadru. Salvează o hartă reziduală PNG.

### 2. Rotație 180° — `--ratio`

```bash
flatcheck pp_flat_A.fit --ratio pp_flat_B.fit
```

Validează **setup-ul**, nu masterul. Tragi ~15 flats, rotești camera 180° în jurul axei
optice, mai tragi ~15, stivuiești separat cele două seturi. Vignetarea și praful sunt
fixe *pe senzor*, deci se anulează în A/B; orice gradient extern e fix *în lume*, deci se
inversează și apare în A/B la ~2× amplitudine. De aceea eroarea reală din fiecare master e
cam jumătate din cifrele raportate aici.

**Punct orb:** contaminarea care e ea însăși simetrică radial în jurul axei optice — de
exemplu căderea de backlight a unui monitor pe care ai centrat obiectivul — supraviețuiește
rotației și e raportată „curat". Contra-testul e un **test de distanță**: un set la 30 cm,
unul la 60 cm, apoi `--ratio` între ele. Vignetarea obiectivului nu depinde de distanță,
contaminarea de la panou depinde.

### 3. End-to-end — `--light`

```bash
flatcheck process/pp_light_00001.fit --light    # un cadru
flatcheck process/ --light                      # toată seria (recomandat)
```

Singurul mod care închide bucla; 1 și 2 sunt proxy pentru el. **Dar nu merge pe orice
date** — vezi secțiunea următoare, e importantă.

Estimează fondul local (percentila 15 pe fiecare tile, robustă la stele și nebulozitate)
și fitează simultan:

```
fond = a + b·X + c·Y + d·R² + e·R⁴
```

Planul absoarbe gradientul de poluare luminoasă; termenii radiali captează vignetarea
reziduală, adică eroarea de flat. Fitul simultan e ce face testul să meargă și pe cer
poluat: cele două au forme diferite și se separă. Patru iterații de sigma-clipping
unilateral resping tile-urile cu nebulozitate (contaminanții adaugă doar semnal pozitiv).

**Semnul e diagnosticul:**

- **pozitiv** — fondul *cade* spre margini → **sub-corecție**. Altă diafragmă la flats
  decât la lights, sau flat contaminat.
- **negativ** — fondul *crește* spre margini → **supra-corecție**. Aproape sigur
  flat-darks lipsă sau greșite: împarți la un flat care încă conține offset.

> **Fără darks:** lights-urile *trebuie* să primească un bias, altfel offsetul se împarte
> la flat și se amplifică radial în colțuri. În Siril: `-bias=...` la calibrate.
> Flat-darks-urile din `biases/` trebuie să fie la **același ISO** ca lights-urile.

#### Rulează-l pe folder, nu pe un cadru

Componenta radială dintr-un cadru e suma a două lucruri: eroarea de flat, care e
**aceeași pe toate cadrele prin construcție** (același master flat s-a împărțit din
fiecare), și un termen radial al cerului, care se schimbă în timpul sesiunii.

Pe o sesiune reală de 32 de cadre în 81 de minute, cifra a baleiat neted de la +3.13%
prin −19.20% până la +35.73%, cu doar 1.36 puncte între cadre consecutive. Tot ce se mișcă
așa e cer, nu flat.

Dându-i folderul, rulează pe toate cadrele care se potrivesc cu tiparul (implicit
`pp_light_*.fit`), tipărește un tabel per cadru și mediază. **Dar dă verdict doar dacă
media chiar înseamnă ceva** — trei condiții:

| condiție | prag | de ce |
|---|---|---|
| număr de cadre | ≥ 5 | sub atât, o medie nu înseamnă nimic |
| eroarea mediei (`σ/√n`) | < 2.00 puncte | dacă depășește chiar pragul de OK, seria nu rezolvă flat-ul la precizia la care se pronunță pragurile |
| \|corelația cu gradientul de cer\| | < 0.70 | dacă „eroarea de flat" urmărește cerul, ce se măsoară e cerul |

Testul de corelație se aplică doar când gradientul de cer chiar variază peste serie
(interval > 10 puncte). Pe o sesiune curată unde stă constant, corelația e zgomot și ar
respinge date bune.

Dacă vreo condiție pică, verdictul e **`NEDETERMINAT`** și raportul spune care și de ce.
Un „nu știu" motivat e mai util decât un număr care arată încrezător.

Tiparul implicit exclude `r_pp_light_*` — cadrele registrate sunt transformate geometric,
deci eroarea de flat, care e fixă pe senzor, e mutată față de centrul în jurul căruia se
fitează modelul radial. Folosește `--pattern` dacă ai altă convenție de nume.

### 4. Expunere — `--expo`

```bash
flatcheck DSC_0060.NEF --expo                           # calea recomandată
flatcheck DSC_0060.NEF --expo --sky                     # flat pe cer
flatcheck flat_brut.fit --expo                          # FITS brut din Siril
flatcheck flat_brut.fit --expo --sat 4095 --black 150   # doar pe FITS, dacă scala e ghicită greșit
```

Rulează pe un **singur flat brut**, nu pe master — stacking-ul normalizează și șterge
nivelul. Sparge cadrul în cele 4 poziții Bayer și ia mediana fiecăreia; dacă
`max/min > 1.15` tratează fișierul ca CFA și folosește canalul cel mai luminos (el
clipează primul, deci el e limitativ).

**Cifra care decide e mediana ferestrei centrale de 20%, nu mediana globală.** Un flat are
prin definiție o distribuție largă — asta *e* vignetarea — deci mediana globală e trasă în
jos de colțuri. Dacă te iei după ea, supraexpui până clipează centrul, exact unde semnalul
de vignetare e maxim.

> Histograma camerei sau a ViewNX arată imaginea **procesată** — debayer + WB + gamma — și
> trage vârfurile spre dreapta, de aceea pare „la limită" când datele brute sunt la ~40%.
> Pentru histogramă pe date brute reale: Siril.

**Pe un fișier raw nu se ghicește nimic**: aparatul scrie în fișier atât saturația cât și
nivelul de negru, iar modul le folosește pe alea și scrie `sursa: libraw (exact)`. Pe un
D3300 la 12 biți: white 4095, black 150.

**Pe FITS**, unde scala nu mai există, nivelul de saturație e auto-detectat din maximul
cadrului, iar `--sat` / `--black` sunt acolo pentru când ghicește greșit. Ca să afli
white-ul exact: trage un cadru deliberat supraexpus — maximul de acolo *e* saturația.
(Pe raw, nu-ți mai trebuie gestul ăsta.)

`--sat` și `--black` bat fișierul, deci rămân portiță și pe raw. Un motiv real: libraw
raportează `maximum = 4095` pentru D3300, dar senzorul nu mai e liniar peste ~3827.
Diferența nu e cosmetică — pe același cadru, 50.3% față de 53.9%.

**Verificare de sănătate:** modul respinge fișierul dacă nivelul central e sub 1.0
(deja calibrat sau normalizat) sau dacă cele patru poziții CFA sunt aproape identice
(fișierul e debayerizat). Prinde exact greșeala „am deschis NEF-ul în Siril și am dat
Save".

#### Dacă chiar ai nevoie de un FITS brut

Nu mai e necesar pentru `--expo` — dă-i direct NEF-ul. Dar dacă îți trebuie FITS-ul din
alt motiv: **NU** deschide NEF-ul și „Save", asta îl debayerizează și normalizează.
Folosește tab-ul **Conversion**: adaugi NEF-urile → Convert. Fișierul rezultat păstrează
mozaicul CFA brut, la scala ADU originală.

Modul rămâne valabil pe FITS și pentru camerele astro mono, care scot FITS nativ și n-au
fișier raw deloc.

#### `--sky`, pentru flats pe cer

La flats pe cerul de la ora albastră, un nivel modest e **corect**: cerul se întunecă
continuu și e plin de stele slabe, iar SNR-ul masterului vine din *numărul* de cadre, nu
din a urca un cadru la 55%. Fără `--sky`, un sky flat la 22% ar fi raportat greșit ca
subexpus.

---

## Când merge și când NU merge `--light`

Modul separă fondul în „plan de poluare luminoasă" + „termeni radiali = eroare de flat".
Separarea ține **cât timp cupola de poluare e cvasi-liniară peste cadru**. Pe un câmp larg
ea se *curbează în interiorul cadrului*, iar partea radial simetrică a acelei curburi are
exact forma vignetării. Două lucruri cu aceeași formă nu se pot separa printr-un fit,
oricât de bine ar fi scris. **Nu e o limitare a uneltei, e o degenerare geometrică.**

De aceea unealta **refuză** să răspundă peste ~40° diagonală, în loc să dea un răspuns
prost.

Condiții, în ordinea importanței:

1. **Câmp îngust.** Sub ~20° diagonală e sigur; peste ~40° nu se poate. Pe APS-C asta
   înseamnă de la ~100 mm în sus. Un telescop e practic mereu în regulă.
2. **Ținta sus pe cer.** Gradientul crește abrupt și neliniar spre orizont.
3. **Cer decent**, și de preferat cu orașul în afara cadrului.
4. **Ținta să nu umple cadrul.** La Calea Lactee centrală, „fondul" pe care îl măsoară
   unealta nu e fond, e galaxie.

Câmpul se deduce din headerul FITS, pe straturi, în ordinea încrederii: `--fov` declarat
de tine → scara dintr-un plate solve (`CDELT1` / `CD1_1`) → `XPIXSZ` + `FOCALLEN` →
`FOCALLEN` singură. Ultimul strat e cazul unui DSLR trecut prin Siril, care nu scrie
`XPIXSZ`: acolo câmpul real nu se poate calcula, deci se presupune cel mai mare senzor
plauzibil (full-frame) și se compară focala direct — conservator prin construcție. Pe un
senzor mai mic, dă câmpul real cu `--fov <grade>`.

**Dacă headerul nu spune nimic, testul rulează** și tipărește o linie care zice că n-a
putut verifica. Necunoscut înseamnă neverificat, nu suspect.

**Contra-exemplu concret.** Tokina la 20 mm pe D3300 acoperă ~70° diagonală. Pe o sesiune
reală de Cale Lactee cu setup-ul ăsta, corelația între componenta radială raportată și
gradientul de cer a ieșit **−0.94** (R² = 0.88): 88% din ce raporta unealta drept „flat"
era explicat de cer. Pentru astfel de date răspunsul îl dă `--ratio`, care nu implică
cerul deloc.

---

## Scara de reziduu

Cele patru teste de abatere din modul structură — împrăștierea colțurilor, asimetria
azimutală, reziduul după modelul radial și înclinarea — măsoară *același lucru* din
unghiuri diferite: cât din cadru nu se explică printr-o funcție pur radială. Toate patru
sunt amplitudini în procente, deci împart o scară, nu un prag:

| bandă | verdict | ce înseamnă |
|---|---|---|
| < 2% | `PERFECT` | calitate perfectă |
| 2–10% | `BUN` | utilizabil; background extraction scoate ce rămâne |
| 10–20% | `ACCEPTABIL` | utilizabil, dar confirmă cu `--light` înainte de un stretch dur |
| > 20% | `REFA` | refă flat-urile |

Cel mai important e **reziduul după modelul radial**: măsoară direct eroarea de flat.
Celelalte trei sunt vederi parțiale ale aceleiași abateri — colțurile o prind doar unde
stau cele patru cutii, testul azimutal doar în interiorul cercului înscris, iar înclinarea
doar partea care e un plan.

Offsetul centrului de iluminare **nu** e pe scara asta și, din 1.2.0, nu mai e nici test:
apare sub `INFORMATIV (nu sunt teste)`, fără verdict. Măsoară o poziție, iar poziția aia e
raportul dintre gradient și curbură — deci nu e o fracție și nu e scale-invariantă. Cu
același gradient de 11% ținut fix, offsetul raportat urcă de la 2.4% pe un obiectiv cu 2
stopuri de vignetare la 57% pe unul cu 0.08 stopuri, iar sub asta vârful de iluminare iese
din cadru și cifra cade brusc la 0.00%. Adică exact pe obiectivele cele mai plate, cu cât
defectul era mai mare, cu atât raportul arăta mai curat.

`Înclinarea` măsoară același defect ca amplitudine, ceea ce scara cere — și prinde în
continuare o decentrare reală a axei optice, ponderată cu cât te costă ea de fapt.

---

## Praguri

Sunt **reguli empirice, nu constante fizice** — un punct de plecare, de calibrat pe un
setup despre care ești sigur. Toate stau în `FlatCheck/Thresholds.cs`.

| metrică | mod | OK sub | PROBLEMĂ peste |
|---|---|---|---|
| împrăștiere colțuri | structură | *scara de reziduu* | |
| asimetrie azimutală | structură | *scara de reziduu* | |
| reziduu după model radial | structură | *scara de reziduu* | |
| înclinare (gradient) | structură | *scara de reziduu* | |
| structură în A/B | `--ratio` | 1% | 3% |
| împrăștiere colțuri în A/B | `--ratio` | 1% | 3% |
| \|componentă radială\| | `--light` | 2% | 5% |
| câmp diagonal | `--light` | 20° | 40° (refuz) |
| eroarea mediei pe serie | `--light` folder | 2.00 puncte | |
| corelație cu gradientul de cer | `--light` folder | 0.70 | |
| clipping în centru | `--expo` | 0.05% | |
| nivel centru | `--expo` | 50–65% | |
| nivel centru | `--expo --sky` | 15–50% | |

Pragurile de serie sunt cele mai puțin calibrate din fișier: vin dintr-o singură sesiune
reală. Recalibrează-le când ai una despre care ești sigur.

`--ratio` își păstrează pragurile mai strânse fiindcă are altă treabă: raportul A/B ar
trebui să fie *fără trăsături*, iar „background extraction scoate ce rămâne" nu se aplică
unui test de validare a setup-ului.

---

## Coduri de ieșire

| cod | înseamnă |
|---|---|
| 0 | `OK` / `PERFECT` / `BUN` — utilizabil ca atare |
| 1 | eroare, sau intrare nepotrivită pentru modul cerut (câmp prea larg, fișier care nu e flat brut la `--expo`) |
| 2 | `SUSPECT` / `ACCEPTABIL` / `NEDETERMINAT` |
| 3 | `PROBLEMA` / `REFA` |

Severitatea e monotonă, deci „orice diferit de 0" înseamnă „nu e curat, uită-te".

---

## Opțiuni

```
flatcheck <flat.fit>                     # structură
flatcheck <A.fit> --ratio <B.fit>        # rotație 180
flatcheck <light.fit> --light            # end-to-end, un cadru
flatcheck <folder/> --light              # end-to-end pe toată seria
flatcheck <flat_brut.fit> --expo         # expunere
flatcheck <flat_brut.NEF> --expo         # idem, direct pe raw de aparat
flatcheck <sky_flat.fit> --expo --sky    # expunere pentru flats pe cer

  -o, --out <fișier.png>   harta de ieșire (implicit flatcheck.png)
  --sat <white>            nivel de saturație, dacă auto-detecția greșește (--expo)
  --black <nivel>          nivel black (--expo)
  --pattern <tipar>        ce fișiere ia din folder (implicit pp_light_*.fit)
  --fov <grade>            diagonala câmpului, dacă headerul nu o dă (--light)
```

Flagurile pot veni oriunde: `flatcheck --expo f.fit` și `flatcheck f.fit --expo` sunt
echivalente. `--pattern` acceptă `*` și `?`; clasele de caractere (`[abc]`) sunt tratate
literal.

Fiecare rulare tipărește versiunea pe prima linie și copyright-ul pe a doua — inclusiv
`--help` și rulările eșuate — ca un raport copiat undeva să spună singur ce l-a produs.

---

## Ce invalidează masterul

Curățare de senzor. Altă diafragmă. Filtru sau inel schimbat. Lovitură serioasă la
obiectiv.

**Nu** invalidează: timpul, rotația camerei pe cer, sesiuni diferite, temperatura, ISO-ul
lights-urilor.

---

## Ieșire PNG

O hartă divergentă albastru → alb → roșu, cu albul fixat la **1.0** și intervalul scalat
la percentilele 1/99 plus 10% padding. Fixarea la 1.0 e validă pentru toți apelanții:
reziduul de structură e `img/model ≈ 1`, raportul e normalizat la mediana 1, iar harta din
`--light` e `1 + radial/a`.

Spre deosebire de versiunea Python nu există figură cu 4 panouri, doar harta reziduală.
Cifrele din consolă sunt aceleași; harta există ca să vezi *forma* unei probleme —
gradient orizontal, vertical, diagonal.

---

## Aplicația cu ferestre

Pe lângă `flatcheck.exe` există `FlatCheck.Wpf.exe`, care face **exact aceleași măsurători**
pe un folder întreg, cu patru taburi în loc de patru rulări.

Nu e o reimplementare: amândouă aplicațiile stau peste aceeași bibliotecă, `FlatCheck.Core`,
și trec prin același lanț (`Runs/`). Cifrele sunt aceleași prin construcție, nu prin grijă —
iar raportul pe care fereastra îl salvează pe disc e produs de același cod care scrie în
consolă, comparat caracter cu caracter de un test.

### Cum se folosește

Îi dai **un folder**: directorul de lucru al sesiunii din Siril, cel cu subfolderele
`flats/`, `darks/`, `biases/`, `lights/`, `process/`. Se poate alege din fereastră sau da
ca argument:

```bash
FlatCheck.Wpf.exe "D:\Astro\2026-08-12-Smida"
```

Imediat ce folderul e ales pornesc singure **structura** și **testul end-to-end**, cu ecran
de așteptare. Rulează una după alta, nu în paralel: un cadru de 24 MP ajunge la sute de MB
în analiză, iar două lanțuri simultane ar putea trece de un gigaoctet.

Dacă nu găsește `process/pp_flat_stacked.fit`, aplicația nu pornește pe folderul acela și
spune ce e de făcut — să rulezi scriptul **`OSC_Preprocessing.ssf`** din Siril. Fișierul
acela e intrarea modului de structură, prin convenție.

### Cele patru taburi

| tab | ce face | când rulează |
|---|---|---|
| **Structura** | modul 1 pe `process/pp_flat_stacked.fit` | la deschiderea folderului |
| **Ratio** | modul 2 între două mastere din `process/` | la alegere, și la orice schimbare |
| **End to end** | modul 3 pe toată seria `pp_light_*.fit` din `process/` | la deschiderea folderului |
| **Expunere** | modul 4 pe un raw din `flats/` | la alegere, și la comutarea „Sky" |

**Structura** arată aceleași cifre ca în consolă — cele patru teste de abatere cu verdictul
fiecăruia, scara de reziduu cu banda curentă marcată, mărimile informative fără verdict —
plus harta reziduală. Salvează în folderul ales:

- **`Rezultat testare.txt`** — ieșirea consolei, identică: banner de versiune, copyright,
  raportul, linia hărții;
- `flatcheck.png` — aceeași hartă care se vede pe ecran, aceiași octeți.

**Ratio** propune masterele găsite în `process/` (`pp_flat*.fit`) și preselectează **primul
și ultimul** din listă, ceea ce pe cazul obișnuit — setul A și setul B — înseamnă că nu ai
nimic de ales. Se poate lua și un fișier din altă parte; dacă nu e un master de flat,
aplicația explică ce sunt fișierele astea și de ce testul are nevoie de unul, în loc să
eșueze la parsare.

`pp_flat_stacked.fit` **nu apare în listă**, deși se potrivește tiparului: el e stiva
*tuturor* flat-urilor, produsă de scriptul de preprocesare, iar testul compară două stive
făcute **separat** — una dinainte, una de după rotire. Comparat cu unul dintre seturi ar
da o cifră fără sens, fiindcă jumătate din el chiar *e* setul acela. (Conta și practic:
sortarea fiind ordinală, ar fi căzut ultimul, adică exact pe preselecția setului B.) Dacă
la tine chiar e unul dintre cele două seturi, îl poți alege oricând cu „Alt fișier..." —
exclus din listă nu înseamnă respins.

**End to end** rulează pe toată seria și își umple tabelul pe măsură ce cadrele sunt gata —
32 de cadre iau vreo două minute, iar un ecran gol tot timpul ăla ar părea blocat. Poate
ieși legitim `INAPLICABIL`: pe un câmp mai larg de ~40 de grade diagonală cerul și
vignetarea au aceeași formă, deci nu se produce nicio cifră. **Nu e o eroare** — vezi
secțiunea despre când merge `--light`. Pe date ca astea răspunde tabul Ratio.

**Expunere** listează din `flats/` **doar raw-uri de aparat** (`.nef`, `.cr2`, `.cr3`,
`.arw`, `.dng`, `.raf`). Modul are nevoie de mozaicul CFA intact și de scala ADU originală,
iar pe un raw white și black se citesc exact din fișier în loc să fie ghicite din maxim.
Comutatorul „Sky" schimbă pragurile și recalculează pe loc.

### Ce nu face

Nu înlocuiește consola: n-are `--fov`, `--sat`, `--black`, `--pattern`, nici coduri de
ieșire. Pentru scripting și pentru cazurile în care trebuie să spui uneltei ceva ce headerul
nu zice — un câmp pe care headerul nu-l dă, o scală ADU greșit ghicită — `flatcheck.exe`
rămâne unealta.

---

## Build, rulare, teste

Soluția are patru proiecte:

| proiect | ce e |
|---|---|
| `FlatCheck.Core` | nucleul: citire, analiză, rapoarte, PNG. Nicio interfață. |
| `FlatCheck` | aplicația de consolă, `flatcheck.exe` |
| `FlatCheck.Wpf` | aplicația cu ferestre, `FlatCheck.Wpf.exe` |
| `FlatCheck.Tests`, `FlatCheck.Wpf.Tests` | testele |

```bash
dotnet build FlatCheck.sln -c Release
```

> **Pe Linux, soluția întreagă nu se mai construiește**, fiindcă `FlatCheck.Wpf` țintește
> `net8.0-windows`. Consola rămâne portabilă dacă îi construiești proiectul direct:
> `dotnet build FlatCheck/FlatCheck.csproj -c Release`.

```bash
dotnet publish FlatCheck/FlatCheck.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Cele două flaguri de la urmă contează de când se citește raw: libraw vine ca DLL-uri
native (`raw_r`, `jpeg8`, `lcms2`, `zlib1`, ~2.1 MB), iar fără `IncludeNativeLibrariesForSelfExtract`
ele rămân fișiere separate lângă executabil. Cu ele, `flatcheck.exe` redevine ce a fost
mereu: **un singur fișier de copiat**.

Dacă totuși le pierzi — ai copiat doar `.exe`-ul dintr-un publish obișnuit — modurile pe
FITS merg mai departe neschimbate, iar un fișier raw e refuzat cu un mesaj care spune ce
lipsește. Nu există cale prin care lipsa lor să producă cifre greșite.

Pentru Linux, `-r linux-x64` (pachetul runtime e deja referit). Alte platforme — arm64,
macOS — n-au binare libraw în pachetele astea, deci acolo merge doar FITS-ul.

Aplicația cu ferestre se publică la fel, fără `PublishSingleFile` (WPF nu se împachetează
la fel de curat într-un singur fișier):

```bash
dotnet publish FlatCheck.Wpf/FlatCheck.Wpf.csproj -c Release -r win-x64 --self-contained
```

```bash
dotnet test FlatCheck.sln
```

Testele golden fixează textul de consolă al fiecărui mod. Când schimbi ieșirea **intenționat**,
reaprobă-le și **citește diff-ul** — toată valoarea lor e acolo:

```bash
FLATCHECK_UPDATE_GOLDEN=1 dotnet test FlatCheck.sln
```

### Verificarea față de Python

`flatcheck.py` e oracolul numeric. Fișierele golden sunt capturate din C# însuși, deci
fixează comportamentul fără să-l confirme; `crosscheck.py` închide gaura, rulând amândouă
implementările pe aceiași octeți:

```bash
FLATCHECK_DUMP_FIXTURES=/tmp/xc dotnet test FlatCheck.sln --filter DumpsFixtures
```

```bash
python crosscheck.py /tmp/xc
```

Dependințe pentru Python: `numpy`, `astropy`, `matplotlib`, opțional `rawpy` pentru NEF.

---

## Diferențe față de versiunea Python

Sunt intenționate, nu scăpări:

- **Fără figură cu 4 panouri** — doar harta reziduală PNG.
- Fără citire EXIF, deci fără sugestii de timp de expunere. (Raw-ul se citește, dar din el
  se iau doar pixelii și scala ADU, nu și timpul de expunere.)
- Linia de sursă a scalei scrie `libraw (exact)` acolo unde Python scrie `rawpy (exact)`.
  E aceeași bibliotecă; numele diferă fiindcă `rawpy` e ambalajul de Python al ei.
- `Percentile` pe o listă goală întoarce 0 în loc să arunce.
- Interpretarea din `--light` scrie diagnosticul cu majuscule („Fondul CADE/CREȘTE spre
  margini") față de minusculele din Python. Semnul *e* diagnosticul, deci accentul e
  deliberat.
- Pe un cadru **perfect constant**, C#-ul are o gardă `|det| < 1e-15` la fitarea centrului
  și cade pe centrul senzorului, raportând 0.00% și OK. Python n-are garda, `np.linalg.solve`
  nu aruncă pe o matrice doar *numeric* singulară, și raportează un vârf fals cu 6.35%
  offset. **C#-ul e cel corect**; nu „repara" garda ca să se potrivească.

---

## Licență

flatcheck — diagnostic cantitativ pentru flat frames de astrofoto.
Copyright (C) 2026 Tudor Carean

Acest program este software liber: îl poți redistribui și/sau modifica în termenii
**GNU General Public License v3** publicată de Free Software Foundation, fie versiunea 3
a licenței, fie (la alegerea ta) orice versiune ulterioară.

Programul e distribuit în speranța că va fi util, dar **FĂRĂ NICIO GARANȚIE** — fără
măcar garanția implicită de VANDABILITATE sau POTRIVIRE PENTRU UN SCOP ANUME. Vezi GNU
General Public License pentru detalii.

Textul complet al licenței e în fișierul [LICENSE](LICENSE); îl găsești și la
<https://www.gnu.org/licenses/gpl-3.0.html>. Aceeași notă, pe scurt, apare și la
`flatcheck --help`; copyright-ul se tipărește la fiecare rulare, sub versiune.
