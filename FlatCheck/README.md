# flatcheck

Diagnostic cantitativ pentru flat frames de astrofoto. Răspunde la patru întrebări:
e master flat-ul simetric radial, cât din abaterea lui e fixată de *aparat* și cât de
*lume*, corectează flat-ul un light real, și e un flat brut expus corect. Al cincilea mod
nu răspunde la nimic — arată: cadrul văzut prin egalizare de histogramă, adică vederea
„Histogram" din Siril, în care orice neuniformitate iese la contrast maxim.

Consolă mică în C#, cu **o singură dependință**: reader FITS propriu și encoder PNG
propriu (prin `ZLibStream` din BCL), iar pentru fișierele raw de aparat — libraw, prin
`Sdcb.LibRaw`. Un NEF nu e doar un container: pixelii sunt comprimați Huffman cu predictor
și trecuți printr-o curbă de linearizare din MakerNote, și nu există niciun decodor .NET
pur managed pentru asta. .NET 8. Binarul se numește `flatcheck.exe` și, publicat ca
`PublishSingleFile`, se copiază oriunde ca până acum.

Există și o **aplicație cu ferestre**, `FlatCheck.Wpf.exe`, care rulează aceleași cinci
moduri pe un folder de sesiune Siril, în cinci taburi. Nu e o a doua implementare: stă
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
| structură (implicit) | master flat **calibrat** — `pp_flat_stacked.fit`, în `process/` sau în `masters/`, după scriptul Siril folosit |
| `--ratio` | două master flats calibrate, din două orientări la 180° — de regulă `pp_flat_stacked.fit` din două foldere de sesiune |
| `--light` | light **calibrat cu flat**, **fără background extraction**, **neregistrat** |
| `--expo` | un **singur cadru brut, necalibrat** — de preferat chiar `.NEF`-ul |

Siril 1.4 scrie masterul ca **float**, cu valorile împărțite la 65535, deci linia
`median / max ADU` iese de forma `0.0143 / 0.0190 (float normalizat 0..1)`. Nu e un
fișier gol; toate metricile sunt rapoarte, deci scara nu contează.

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
la f/2 e sub prag (măsurat: umbra de praf vizibilă pe un master de Nikkor 35 la f/2.8 are
0.31 %, la un prag de 2 %).

Prin urmare orice abatere de la simetria radială **nu e vignetare — e eroare**: sursă de
lumină neuniformă, light leak, cameră înclinată, lumină ambientală, hârtie difuzoare
prea aproape de obiectiv.

„Flat bun" nu înseamnă „arată plat". Înseamnă „abaterea de la simetria radială e sub
zgomot". Aia e o cifră, și se poate măsura.

Modelul radial și asimetria se calculează în jurul **centrului senzorului**, nu al
vârfului de iluminare fitat. Fitarea centrului ar absorbi exact gradientul liniar pe care
îl cauți, iar testul s-ar anula singur. Profilul radial se ia însă din rasterul simetrizat
la 180° — vezi [„O capcană care a fost reparată"](#o-capcană-care-a-fost-reparată).

---

## Cele șase moduri

### 1. Structură (implicit)

```bash
flatcheck process/pp_flat_stacked.fit          # sau masters/pp_flat_stacked.fit, după scriptul Siril
```

Rulează pe masterul final. Raportează informativ vignetarea și offsetul centrului de
iluminare față de centrul senzorului, apoi cinci teste de abatere de la simetria radială:
împrăștierea celor patru colțuri, asimetria azimutală pe 12 sectoare, **reziduul după
modelul radial** — cel care contează cel mai mult, fiindcă măsoară direct cât din cadru nu
se explică printr-un model pur radial — înclinarea, adică amplitudinea vârf-la-vârf a
gradientului liniar peste cadru, și **pata maximă**, cea mai abătută dală de pe tot
cadrul, care prinde un defect compact pe care reziduul îl taie ca extremă. Salvează o
hartă reziduală PNG.

#### Praful: sesiune sau bibliotecă

Pe lângă cele cinci teste, raportul dă o cifră informativă, **pete locale**: cea mai adâncă
pată compactă rămasă după ce se scade structura de scară mare, cu poziția ei în rasterul
de analiză. Un gradient nu intră în ea; o umbră de praf, un inel de condens sau o scurgere
mică intră întreagă.

**Semnul e diagnosticul**, și raportul îl scrie în cuvinte lângă cifră:

| semn | ce înseamnă | cauze tipice |
|---|---|---|
| negativ, `(mai intunecat)` | pata e mai întunecată decât ce o înconjoară | praf pe senzor sau pe filtru, condens, un obstacol |
| pozitiv, `(mai luminos)` | pata e mai luminoasă | scurgere de lumină, o reflexie |

Peste pragul de 2 % raportul tipărește o **atenționare**, nu un verdict, fiindcă răspunsul
depinde de ce faci cu flat-ul: o pată fixă pe aparat e *corectă* pentru sesiunea ale cărei
lights au aceeași pată, și *greșită* pentru un flat de bibliotecă, unde praful se va fi
mutat. Sub prag cifra e doar informativă. Caută poziția în hartă înainte să decizi.

Un lucru de știut înainte să te iei după ea: cifra găsește ce e *compact*, dar prinde și
muchia unei structuri mari, dacă aceasta e mai abruptă decât fereastra din care se scade
fondul local. Măsurat pe biblioteca proprie, extrema unui master de Nikkor 35 la f/2.8 e o
dungă verticală pe marginea din stânga (−0.44 %), nu o pată rotundă. Amândouă rămân la o
cincime din prag, deci nu s-a aprins nimic — dar dacă vezi vreodată atenționarea, uită-te
întâi dacă forma din hartă e rotundă. Numai aia e praf.

#### Ce nu vede modul structură

O pată **rotundă și centrată exact pe axă** — un panou prea aproape, backlight-ul unui
monitor pe care ai centrat obiectivul — e absorbită întreagă de modelul radial, exact ca
vignetarea: măsurat, o pată de 10 % în centru lasă toate cifrele sub 0.1 % și harta goală.
Singura ei urmă e cifra de vignetare, care crește. Se vede doar dacă e puțin descentrată,
și atunci ca o semilună, nu ca un disc. `--ratio` e la fel de orb la ea; contra-testul e
distanța — un set la 30 cm, unul la 60.

Ce iese de aici e o **limită superioară** a erorii de flat, nu eroarea: tot ce măsoară e
suma părții fixate de aparat (inofensivă) și a celei fixate în lume (dăunătoare), iar un
singur master nu conține informația care le desparte — vezi „Scara de reziduu" și modul 2.
Rămâne totuși testul cu care începi, fiindcă e singurul care nu cere să mai fotografiezi
nimic.

### 2. Atribuire prin rotație 180° — `--ratio`

```bash
flatcheck sesiune_0deg/process/pp_flat_stacked.fit --ratio sesiune_180deg/process/pp_flat_stacked.fit
```

Nu îți spune dacă setup-ul e curat — **nu poate**, și e important să știi de ce. Îți spune
altceva, la care niciun alt mod nu poate răspunde: **din structura ne-radială a flat-ului
tău, cât e fixat de aparat și cât e fixat în lume.**

Tragi ~15 flats, rotești camera 180° în jurul axei optice, mai tragi ~15, stivuiești
**separat** cele două seturi — adică în două foldere de sesiune, câte unul pentru fiecare
orientare. Din cele două mastere se calculează:

| | ce e | cum se citește |
|---|---|---|
| `(A+B)/2` | partea fixă **pe aparat** | e chiar masterul pe care îl obții stivuind ambele orientări. **De aici vine verdictul**, pe aceeași scară de reziduu ca modul 1. |
| `A/B` | partea fixă **în lume** | informativ: atât ți-a scos rotația din master. |

Rotația **nu trebuie să fie exactă.** Vignetarea e rotațional simetrică, deci se anulează
la orice unghi; cele 180° servesc doar la inversarea gradientului extern. O eroare de unghi
δ lasă amplitudinea măsurată la `2·G·cos(δ/2)` — la 10° pierzi 0.4%, la 30° pierzi 3.4%.
Testul **subraportează, niciodată nu supraraportează**.

**Bara de eroare nu e calibrată, e derivată.** `log(A/B)` trebuie să fie exact antisimetric
la rotația de 180°: o contaminare fixă în lume intră integral în partea impară și zero în
cea pară, iar zgomotul se împarte cu varianță egală. Deci partea pară e un estimator al
zgomotului din partea impară, construit din aceleași date, fără nicio constantă de reglat.
Când cifra și eroarea ei sunt comparabile, nu s-a detectat nimic peste zgomot.

**Trei lucruri pe care modul NU le poate face**, și pe care raportul le spune pe față:

- **Nu poate verifica din pixeli că ai rotit.** Două stive din aceeași orientare și două
  stive rotite dintr-un setup cu adevărat curat produc date statistic identice — în ambele
  cazuri `A/B` e numai zgomot. De aceea raportul tipărește `STACKCNT` și `DATE-OBS` pentru
  ambele fișiere: două stive care încep una imediat după cealaltă n-au avut când să fie
  rotite.
- **„Fix pe aparat" nu înseamnă „inofensiv".** Vignetarea obiectivului se împarte corect
  din lights. O curea căzută pe lentilă, condensul sau o parasolară strâmbă se rotesc și
  ele cu aparatul, arată identic aici, și la lights nu mai sunt acolo. Ca să le deosebești,
  compară două ședințe diferite cu **același obiectiv**: semnătura fixă pe aparat trebuie
  să iasă la fel.
- **Punct orb:** orice contaminare care se rotește împreună cu aparatul — inclusiv una
  radial simetrică în jurul axei optice, cum e căderea de backlight a unui monitor pe care
  ai centrat obiectivul. Contra-testul e un **test de distanță**: un set la 30 cm, unul la
  60 cm. Vignetarea obiectivului nu depinde de distanță, contaminarea de la panou depinde.

Scrie **două** hărți: la calea dată cu `-o`, reziduul părții fixe pe aparat (cea care se
potrivește cu verdictul), și lângă ea, cu sufixul `-lume`, raportul `A/B` — pe care se
vede *forma* gradientului extern.

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

**Canalele își spun culoarea, dar numai când fișierul o spune.** Pe un raw ordinea vine
din libraw (`filters` + `cdesc`), pe un FITS din cardul `BAYERPAT` pe care îl scrie Siril
la Conversion — și atunci raportul scrie `R (CFA[0,0])`, `G1 (CFA[0,1])` și așa mai
departe. Fără sursă, pozițiile rămân anonime, ca înainte. Nu se ghicește: ordinea diferă
de la aparat la aparat (RGGB e cea mai des întâlnită, dar există și GBRG, GRBG, BGGR), iar
o etichetă pusă pe canalul greșit ar părea măsurată. Pe un D3300 iese `RGGB`. Pentru
verdict oricum nu contează care e care — limitativ e cel mai luminos, orice culoare ar
avea.

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

### 5. Egalizare de histogramă — `--histeq`

```bash
flatcheck flats/DSC_0060.NEF --histeq -o poza.png
```

Nu e un test și nu dă verdict. E **vederea „Histogram" din Siril 1.4.4**, refăcută: imaginea
trecută prin propria ei funcție de repartiție, astfel încât fiecare nivel de gri să
primească același număr de pixeli. Pe un flat — o distribuție îngustă față de scala
fișierului — curba e abruptă exact pe intervalul vignetării, și tot ce nu e uniform iese
la contrast maxim, inclusiv ce nu se vede deloc pe imaginea liniară. Măsurat pe un
sintetic cu 5 % vignetare: o undă de 0.3 % e o treime de nivel de gri în afișarea liniară
și vreo cincisprezece niveluri egalizată.

Ce face, exact ca Siril (citit din sursa lui, `image_display.c` și `histogram.c`):

- o histogramă cu 65536 de coșuri **pe canal**, din care pixelii egali cu zero lipsesc;
- tabelul de transfer e repartiția cumulată înmulțită cu 255, rotunjită ca `round_to_BYTE`;
- pe un cadru CFA cele patru poziții Bayer se egalizează **fiecare pe repartiția ei**, ca
  straturile unei imagini debayerizate în Siril; consola scrie compozitul RGB (R din
  poziția roșie, G media celor doi verzi, B din cea albastră — după culoare, nu după
  poziție, deci ordinea vine din fișier și nu se ghicește; fără ordine, media celor patru
  în gri).

Ce diferă: coșurile stau pe `[min, max]` ale canalului, nu pe 0..65535 absolut, fiindcă
rasterele pot fi la orice scală (0..4095, 0..65535, 0..1); pe date întregi maparea e
identică, pe float e mai fină. Egalizarea se face la rezoluție întreagă, apoi rezultatul
pe 8 biți se micșorează la ~1500 px pe latura lungă, cu aceeași regulă ca rasterul de
analiză.

**Ce NU se poate citi din poză.** Amplitudinea. Fiecare nivel de gri primește același număr
de pixeli, indiferent cât de apropiate erau valorile lor, deci o undă de 0.5 % și un
gradient de 10 % cu aceeași formă ies identic — și un flat perfect își arată propriul
zgomot la contrast maxim. Poza spune **ce formă** are ce măsoară modul de structură;
cifrele sunt acolo. Iese cu 0 când a scris poza; raportul e informativ.

Acceptă orice fișier pe care îl citește unealta: raw de aparat sau FITS, cadru individual
sau master. Pe un master (float, 0..1, cu mozaic CFA intact cum îl scrie Siril) merge la
fel; ce se vede acolo e ce a rămas după stivuire.

---

### 6. Netezire — `--smooth`

```bash
flatcheck process/pp_flat_stacked.fit --smooth process/pp_flat_stacked_netezit.fit
flatcheck process/pp_flat_stacked.fit --smooth netezit.fit --scale 150
```

Nu e un test și nu dă verdict. Scrie **un al doilea master**: același flat, cu tot ce e
neted păstrat și tot ce nu e scos — textura difuzorului, umbrele de praf, petele compacte,
zgomotul. Pentru un flat tras printr-un material pus pe monitor, a cărui textură ajunge în
lights ca o pătură de celule de câțiva la sută.

**De ce nu un flat sintetic radial.** Un model pur radial ar scoate textura, dar ar scoate
și tot ce face obiectivul în afară de vignetare — iar pe un Tokina 20 mm cele patru
colțuri diferă între ele cu 6-16 %, ceea ce e exact cazul în care un model radial
corectează mai prost decât flat-ul pe care îl înlocuiește. Netezirea păstrează colțurile
inegale, gradientul, petele largi: tot ce e neted la scara aleasă.

**Ce face**, pe fiecare poziție Bayer separat (pe mono, pe tot cadrul):

1. mediana pe dale de ~150 px pe senzor, cu dalele parțiale de la margine păstrate;
2. în fiecare dală, un fit pătratic local pe dalele vecine, ponderat gaussian cu sigma =
   scara (implicit 300 px pe senzor);
3. robust: dalele care se abat de la fitul local cu peste 2.5 MAD *și* peste 2 % din
   nivel — praf, pete, celulele cele mai adânci — nu mai cântăresc în fitul nimănui;
4. pe fiecare pixel, media biliniară a predicțiilor celor patru dale din jur.

Fără model radial, deliberat: două variante cu model radial au picat pe masterele reale.
Un polinom global nu urmărește structura de scară medie pe care o are orice master
(petele monitorului la 1000-1500 px, un inel descentrat), iar un model pe inele **pune
inele în fișier** — dincolo de cercul înscris mediana pe inel sare de la un inel la altul,
și un câmp local de 150 px nu le poate compensa. Netezirea de mai sus nu știe de niciun
centru, deci nu poate face inele.

**Ce raportează** e *ce a scos*, fiindcă asta e singura cifră după care se poate decide
dacă fișierul e de încredere: p1/p99 pe pixel, structura la scară mare (p95 − p5 pe dale)
și dala cea mai abătută a raportului `original / netezit`, pata cea mai adâncă, și
bateria de structură pe intrare și pe ieșire, față în față. Când dala cea mai abătută
trece de 2 % raportul atenționează că s-a scos și ceva de scară mare — praf adânc, o
pată, sau o structură reală mai îngustă decât scara — și lasă decizia omului.

Măsurat pe cinci mastere reale (Nikkor 35 DX pe cer la două ISO-uri, Tokina 20 cu
difuzorul lângă lentilă și pe monitor, Samyang 135 pe LCD), la 300 px: toate verdictele
și adâncimile de vignetare rămân neschimbate; cele cinci teste se mișcă sub o jumătate de
punct; componenta scoasă e de 0.12-0.39 % pe cele patru mastere curate (zgomot de pixel
plus praful) și 3.7 % pe cel cu textură, unde pata compactă cade de la +6.19 % la +0.62 %
și textura din fișier de la ±3 % la ±0.3 %. Pe un sintetic cu adevăr cunoscut eroarea
metodei e sub 0.2 % pe tot cadrul, colțuri incluse.

**Ce NU știe.** Ce e al obiectivului și ce e al sursei: petele unui monitor, gradientul
cerului, colțurile inegale ale unui panou rămân, fiindcă sunt netede — doar `--ratio` le
desparte. Și ce scoate poate fi ceva dorit: o umbră de praf e corectă pentru sesiunea ale
cărei lights o au și ele, și greșită într-un flat de bibliotecă. O structură reală de
~300 px (inelul descentrat al Nikkor-ului, 0.3-0.5 %) e luată parțial la 300 px și rămâne
la 150 px; scara e singurul buton. Ce rămâne din celule e fundamental: un câmp aleator cu
lungimea de corelație de 300 px trece printr-un nucleu de 300 px la ~30 % în RMS.

**Fișierul** are tipul intrării (float rămâne float la aceleași valori, 16 biți rămân 16
biți), headerul intrării copiat card cu card — `BAYERPAT`, `STACKCNT`, `DATE-OBS` — plus
două linii `HISTORY`. Nimic nu se renormalizează. Scripturile Siril numesc
`pp_flat_stacked` explicit, deci nu iau fișierul netezit singure: redenumește-l în locul
masterului (păstrează originalul) sau dă-l la `calibrate` cu `-flat=`. Modul refuză un raw
(un cadru, necalibrat — pentru raw există `--expo`) și un cub de culoare (cititorul l-ar
media într-un mono).

**Verificarea** pe care o poți face singur: netezește două mastere ale aceleiași lentile
și trece-le prin `--ratio`; ce rămâne trebuie să fie un câmp neted de ordin mic. Că flat-ul
netezit chiar corectează mai bine o spune doar `--light` pe lights calibrate — și pe un
câmp mai larg de 40° el refuză.

#### Cum se alege scara, și `--scale auto`

Scara trebuie să stea între mărimea a ce vrei să scoți și mărimea celei mai mici structuri
reale pe care vrei să o păstrezi. Amândouă se citesc din master, și `--scale auto` le
citește în locul tău:

1. **Textura fină a intrării** — partea structurată a componentei sub ~128 px pe senzor,
   cu zgomotul de pixel scăzut. Sub 0.20 % nu există nimic de netezit și unealta nu
   rulează nimic: pe masterele reale cele fără textură dau 0.04-0.06 %, cel cu difuzorul pe
   monitor 0.69 %.
2. Altfel, **câte o netezire pe fiecare treaptă** (150 / 300 / 600 px, ~15 s în total pe
   24 MP) și două cifre pe treaptă: textura rămasă în fișier (sub 0.05 % a plecat) și cât
   au *scăzut* asimetria azimutală și înclinarea — singurele două teste pe care textura nu
   le atinge, deci o scădere acolo de peste 0.5 puncte e structură reală care pleacă.
3. **Cea mai mică treaptă** la care textura a plecat și garda tace. Pe masterul cu
   difuzorul pe monitor: 150 lasă 0.09 % textură, 300 lasă 0.02 % cu garda la 0.00 / -0.01,
   deci 300. La 600 garda prinde inelul real al Nikkor-ului (asimetria -0.79) și petele
   monitorului pe Tokina (-0.80).

Trei răspunsuri: `SCARA PROPUSA`, după care netezirea rulează la ea și scrie fișierul
(cod 0); `NIMIC DE NETEZIT` și `NEDETERMINAT` (textura și structura reală au aceeași
mărime), care nu scriu nimic și ies cu **2** — nu e o eroare, e „citește înainte să
decizi". Regula **propune, nu decide**: nu știe ce e al obiectivului și ce e al sursei —
punctul cald al unui LCD arată la fel ca un inel al lentilei — și e oarbă la o structură
reală care stă doar în colțuri, în afara cercului înscris. Pragurile stau pe cinci mastere;
unul cu textură slabă, de 0.15-0.25 %, cade lângă primul.

De mână, criteriile sunt aceleași: uită-te la harta „ce s-a scos" la ±2 % — doar celule și
praf e bine, o formă netedă și mare înseamnă scara prea mare; și la tabelul înainte /
după — „pata maxima" și reziduul scad când pleacă textura, dar asimetria și înclinarea
n-au voie să se miște.

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
era explicat de cer. Pentru astfel de date răspunsul îl dă `--ratio`: el nu implică cerul
deloc și separă direct cât din abaterea masterului e fixată de aparat.

---

## Scara de reziduu

Cele cinci teste de abatere din modul structură — împrăștierea colțurilor, asimetria
azimutală, reziduul după modelul radial, înclinarea și pata maximă — măsoară *același
lucru* din unghiuri diferite: cât din cadru nu se explică printr-o funcție pur radială.
Toate cinci sunt amplitudini în procente, deci împart o scară, nu un prag:

| bandă | verdict | ce înseamnă |
|---|---|---|
| < 2% | `PERFECT` | calitate perfectă |
| 2–10% | `BUN` | utilizabil; background extraction scoate ce rămâne |
| 10–20% | `ACCEPTABIL` | utilizabil, dar confirmă cu `--light` înainte de un stretch dur |
| > 20% | `REFA` | refă flat-urile |

Cel mai important e **reziduul după modelul radial**: măsoară direct cât din cadru nu se
explică printr-un model pur radial, pe tot cadrul, colțuri incluse. Colțurile, asimetria
azimutală și înclinarea sunt vederi parțiale ale aceleiași abateri — colțurile o prind doar
unde stau cele patru cutii, testul azimutal doar în interiorul cercului înscris, iar
înclinarea doar partea care e un plan. **Pata maximă** e complementul reziduului: acesta
taie 5 % din dale la fiecare capăt, ca să nu măsoare zgomot, deci un defect *compact* — o
scurgere mică, o pată de condens — dispare din el oricât de adânc ar fi. Măsurat: o pată de
+10 % cât un deget într-un colț dădea reziduu 0.04 % și verdict `PERFECT`, cu pata perfect
vizibilă în hartă. Pata maximă citește 9.3 % pe ea.

**E o limită superioară a erorii de flat, nu eroarea.** Ce măsoară e suma a două lucruri
cu urmări opuse: o parte fixată de **aparat** (optica — se împarte corect din lights, deci
inofensivă) și una fixată în **lume** (iluminarea — nu se reproduce, deci dăunătoare). Pe
un singur master cele două nu se pot separa; informația pur și simplu nu e acolo. `--ratio`
e cel care le desparte.

De-aia un obiectiv cu vignetare genuin ne-radială iese aici ca „eroare" fără să fie una.
Măsurat pe biblioteca proprie: un master de Nikkor 35 DX are 4.00% de împrăștiere a
colțurilor, din care doar ~1.3% era cerul — restul a supraviețuit rotației de 180°, deci e
fixat de aparat.

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
setup despre care ești sigur. Toate stau în `FlatCheck.Core/Thresholds.cs`.

| metrică | mod | OK sub | PROBLEMĂ peste |
|---|---|---|---|
| împrăștiere colțuri | structură | *scara de reziduu* | |
| asimetrie azimutală | structură | *scara de reziduu* | |
| reziduu după model radial | structură | *scara de reziduu* | |
| înclinare (gradient) | structură | *scara de reziduu* | |
| pata maximă (o dală) | structură | *scara de reziduu* | |
| pete locale (praf) | structură | 2 % (doar atenționare, nu votează) | |
| cele cinci teste pe `(A+B)/2` | `--ratio` | *scara de reziduu* | |
| gradient extern / orientare | `--ratio` | 1% | 3% (doar etichetă informativă) |
| \|componentă radială\| | `--light` | 2% | 5% |
| câmp diagonal | `--light` | 20° | 40° (refuz) |
| eroarea mediei pe serie | `--light` folder | 2.00 puncte | |
| corelație cu gradientul de cer | `--light` folder | 0.70 | |
| clipping în centru | `--expo` | 0.05% | |
| nivel centru | `--expo` | 50–65% | |
| nivel centru | `--expo --sky` | 15–50% | |

Pragurile de serie sunt cele mai puțin calibrate din fișier: vin dintr-o singură sesiune
reală. Recalibrează-le când ai una despre care ești sigur.

`--ratio` **nu are praguri proprii pentru verdict**: verdictul lui e cel de structură,
pe masterul combinat, deci pe aceeași scară de reziduu ca modul 1. Pragurile de 1%/3%
au rămas doar ca etichetă pe componenta fixă în lume, care e informativă și nu votează.

---

## Coduri de ieșire

| cod | înseamnă |
|---|---|
| 0 | `OK` / `PERFECT` / `BUN` — utilizabil ca atare |
| 1 | eroare, sau intrare nepotrivită pentru modul cerut (câmp prea larg, fișier care nu e flat brut la `--expo`) |
| 2 | `SUSPECT` / `ACCEPTABIL` / `NEDETERMINAT` |
| 3 | `PROBLEMA` / `REFA` |

Severitatea e monotonă, deci „orice diferit de 0" înseamnă „nu e curat, uită-te".
`--histeq` n-are verdict: 0 înseamnă doar că a scris poza, 1 că n-a putut. La fel
`--smooth`: 0 înseamnă că a scris flat-ul netezit și hărțile, 1 că a refuzat intrarea
(raw, cub de culoare) sau n-a putut scrie; cu `--scale auto`, 2 înseamnă că n-a scris
nimic fiindcă nu e nimic de netezit sau scara e nedeterminată.

---

## Opțiuni

```
flatcheck <flat.fit>                     # structură
flatcheck <A.fit> --ratio <B.fit>        # atribuire prin rotație 180
flatcheck <light.fit> --light            # end-to-end, un cadru
flatcheck <folder/> --light              # end-to-end pe toată seria
flatcheck <flat_brut.fit> --expo         # expunere
flatcheck <flat_brut.NEF> --expo         # idem, direct pe raw de aparat
flatcheck <sky_flat.fit> --expo --sky    # expunere pentru flats pe cer
flatcheck <flat.NEF> --histeq            # vederea „Histogram" din Siril, pe canale
flatcheck <flat.fit> --smooth <netezit.fit>   # flat netezit: fără textură, praf, pete

  -o, --out <fișier.png>   harta de ieșire (implicit flatcheck.png; --ratio mai scrie
                           una lângă ea, cu sufixul -lume; la --histeq e poza egalizată;
                           --smooth mai scrie una cu sufixul -dupa)
  --sat <white>            nivel de saturație, dacă auto-detecția greșește (--expo)
  --black <nivel>          nivel black (--expo)
  --pattern <tipar>        ce fișiere ia din folder (implicit pp_light_*.fit)
  --fov <grade>            diagonala câmpului, dacă headerul nu o dă (--light)
  --scale <px>|auto        scara de netezire, în pixeli de senzor (--smooth; implicit 300);
                           'auto' o propune din master și netezește la ea, sau iese cu 2
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
reziduul de structură e `img/model ≈ 1`, ambele hărți din `--ratio` sunt centrate pe 1
(reziduul masterului combinat, și `A/B` normalizat la mediana 1), iar harta din `--light`
e `1 + radial/a`.

**`--ratio` scrie două hărți**, fiindcă desparte masterul în două componente. La calea dată
cu `-o` merge reziduul părții fixe pe aparat — cea care se potrivește cu verdictul — iar
lângă ea, cu sufixul `-lume`, raportul `A/B`, pe care se vede *forma* gradientului extern.
În fereastră aceleași două stau sub un comutator.

Spre deosebire de versiunea Python nu există figură cu 4 panouri, doar hărțile de mai sus.
Cifrele din consolă sunt aceleași; harta există ca să vezi *forma* unei probleme —
gradient orizontal, vertical, diagonal.

### Scara culorilor

Scara automată întinde culoarea pe intervalul **propriu al fiecărei imagini**, deci roșu
plin nu înseamnă nimic anume: măsurat pe două mastere ale aceluiași obiectiv, roșu plin era
`+3 %` pe unul și `+10 %` pe altul. Două hărți pe scara automată se pot compara **doar prin
formă**, nu prin intensitate.

De aceea **în fereastră** harta se poate pune pe o scară fixă, dintr-un rând de butoane:
`±2 %`, `±10 %`, `±20 %` — chiar granițele scării de reziduu — plus `auto`, utilă ca să
vezi *forma* pe un master aproape perfect, unde `±2` e deja prea grosier. Implicit e
**`±10 %`**, banda în care se așteaptă să trăiască un flat normal, ca două sesiuni să se
compare dintr-o privire fără să apeși nimic.

Plafonul și banda nu sunt însă același lucru, și merită știut când citești culoarea:
plafonul e abaterea unui *pixel* față de model, banda e o amplitudine *vârf-la-vârf*. O
pată unilaterală de +10 % atinge plafonul de 10 % chiar la granița benzii `BUN`; un
gradient de 10 % merge de la −5 % la +5 %, deci ajunge doar la jumătatea lui, și saturează
abia la 20 %, adică la `REFA`. Măsurat: la un gradient de 10 % harta are capetele la
−4.9 / +4.8 %. Nu există un plafon corect pentru amândouă, așa că se explică aici, nu se
schimbă cifrele.

Sub fiecare hartă stă o **legendă** cu capetele reale ale scării afișate, de exemplu
`albastru = -10.0 %   alb = 0 (exact pe model)   rosu = +10.0 %`. Pe `auto` arată cele două
capete calculate, care sunt de obicei asimetrice — exact ce nu se putea vedea înainte.

**Fișierele scrise pe disc sunt mereu pe scara automată**, oricare buton e apăsat: atât
`-o` din consolă, cât și `flatcheck.png` salvat de fereastră. Cele două aplicații scriu
astfel același fișier pentru același master, iar butoanele rămân un ajutor de privit.

### Ce înseamnă formele

Harta e imaginea împărțită la modelul radial, deci tot ce era simetric radial a fost scos:
**ce a rămas nu poate fi vignetare**. Citește întâi cifrele, care spun *cât*, și abia apoi
forma, care spune *de ce*. Aceeași listă e și în fereastră, sub hartă, la „Cum citesc harta".

| formă | ce a însemnat |
|---|---|
| pete fără direcție | Abateri mici, fără o axă preferată. Semnătura difuzorului plus zgomotul cerului. Background extraction le scoate fără efort. |
| dipol (o parte albastră, cealaltă roșie) | Un gradient, deci o direcție; se potrivește cu `inclinare`. Sursă neuniformă, lumină ambientală, sau un obstacol întins asimetric peste obiectiv. `--ratio` spune dacă e fix în lume sau fix pe aparat. |
| cercuri sau arce concentrice | Un inel centrat pe senzor **nu poate** rămâne în hartă — modelul radial îl absoarbe prin construcție. Deci dacă vezi unul, e centrat în altă parte: ceva rotund din fața obiectivului e în afara axei, sau lumina nu e difuză. **Nu s-a văzut încă pe un master real:** inelele care apăreau pe mastere cu gradient mare erau un artefact al modelului radial, corectat între timp — vezi mai jos. |
| pată locală într-un colț sau la o margine | Light leak: vizor, montură, latura difuzorului. Nu se rotește cu cerul, deci `--ratio` o pune pe aparat. |
| pată rotundă în centru | Iluminare contaminată simetric față de axă: un panou prea aproape, backlight-ul unui monitor. Când e chiar centrată, **nu poate apărea pe hartă** și nu o vede niciun test — modelul radial o absoarbe întreagă, exact ca vignetarea; măsurat, o pată de 10% în centru lasă harta goală și toate cifrele sub 0.1%. Singura ei urmă e cifra de vignetare, care crește. Se vede doar când e puțin descentrată, și atunci ca o semilună, nu ca un disc. Testul prin rotație e la fel de orb la ea; contra-testul e distanța — un set la 30 cm, unul la 60. |
| granulație fină peste tot | Zgomot, nu structură: prea puține cadre sau expunere prea joasă. Nu e nimic de reparat la sursă, doar de stivuit mai mult. |

### Cine decide: cifrele sau harta

Cele două jumătăți ale ecranului nu au același rol, deși arată ca două vederi ale
aceluiași lucru.

- **Cât de mult** — decid **cifrele**. „Reziduu după modelul radial" e chiar măsura a cât
  din cadru nu se explică printr-o funcție pur radială, pe scara de reziduu. Sub 2% nu
  contează ce formă are; între 2 și 10% background extraction scoate ce rămâne, oricare ar
  fi forma. **Verdictul nu depinde niciodată de ce vezi în hartă.**
- **Ce fel** — aici intră ochiul, dar nu de la zero. Forma spune ce să schimbi la ședința
  următoare, nu dacă flat-ul e bun. Și cifrele fac o parte din treabă înainte: dacă
  `inclinare` e aproape de reziduu, e un gradient, și `--ratio` spune dacă e fix pe aparat
  sau în lume; dacă `pata maximă` e mult peste reziduu, e ceva compact — o scurgere mică,
  condens — și `pete locale` spune unde; iar dacă a apărut atenționarea de praf, întrebarea
  nu mai e dacă flat-ul e bun, ci pentru ce îl folosești: sesiunea ale cărei lights au
  aceeași pată, sau biblioteca. Ce rămâne pentru ochi e tabelul de mai sus.

Aceeași explicație e în fereastră, sub verdictul din tabul Structură.

### O capcană care a fost reparată

Profilul radial se ia din **rasterul simetrizat la 180°**, adică din media dintre master
și rotitul lui, nu direct din master. Reziduul rămâne `img/model`, deci gradientul se vede
în continuare.

Motivul: dincolo de cercul înscris, un inel de rază constantă nu mai e un inel întreg,
sunt arce parțiale. Pe un cadru cu gradient, distribuția pixelilor din arcele alea e
bimodală — un capăt întunecat, celălalt luminos — iar mediana cade în golul dintre cele
două moduri, deci sare de la un inel la altul. Modelul moștenea salturile și harta le
desena drept **cercuri concentrice plus o margine dură pe cercul înscris, pe un cadru care
nu conținea niciuna.** Reprodus pe un cadru sintetic care e doar vignetare netedă ori un
gradient de 11%.

Cifrele de verdict nu erau afectate — reziduul se măsura pe atunci doar în cercul înscris,
unde mediana era deja exactă — dar harta e singurul loc din care se citește o *formă*, iar
ea mințea. Dacă ai hărți salvate dinainte cu inele pe ele, ăsta era motivul.

Odată modelul valid până în colț, restricția reziduului la cercul înscris a rămas fără
motiv, și a fost scoasă: pe un cadru 3:2 cercul acoperă 53 % din dale, colțurile — unde
gradientul e cel mai mare și unde stau scurgerile de lumină — nu se vedeau deloc, iar
același gradient citea 6.5 % dacă mergea pe axa lungă și 9.7 % pe cea scurtă. Pe tot cadrul
citește 10.4 / 10.6. Dacă ai rapoarte vechi, reziduul de acolo e mai mic decât ce iese
acum pe același master; cifra veche subestima.

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
`flats/`, `darks/`, `biases/`, `lights/` și `process/` sau `masters/`. Se poate alege din fereastră sau da
ca argument:

```bash
FlatCheck.Wpf.exe "D:\Astro\2026-08-12-Smida"
```

Imediat ce folderul e ales pornesc singure **structura** și **testul end-to-end**, cu ecran
de așteptare. Rulează una după alta, nu în paralel: un cadru de 24 MP ajunge la sute de MB
în analiză, iar două lanțuri simultane ar putea trece de un gigaoctet.

Masterul de flat, `pp_flat_stacked.fit`, se caută în **două locuri**, fiindcă scripturile
Siril îl lasă în două: `process/` (scripturile din depozitul de scripturi, ca
`OSC_Preprocessing_WithoutDark.ssf`) și `masters/` (scripturile din instalarea Siril 1.4,
ca `OSC_Preprocessing.ssf`). Ambele merg. Dacă există în amândouă, se ia **cel mai nou** și
tabul Structura arată un avertisment cu ambele căi — două mastere înseamnă două rulări de
preprocesare, posibil din două seturi de flat-uri, și trebuie să știi pe care îl vezi.
`process/` în sine nu e obligatoriu: un folder cu masterul în `masters/` și fără cadre
rulează structura, iar tabul End-to-end spune singur că n-are serie.

Dacă nu găsește masterul în niciunul din cele două locuri, aplicația nu pornește pe
folderul acela și spune ce e de făcut — să rulezi scriptul de preprocesare din Siril.
Fișierul acela e intrarea modului de structură, prin convenție.

#### Rularea scriptului din aplicație

Pe același ecran e un buton **Rulează în Siril**, care rulează scriptul fără să deschizi
Siril: pornește `siril-cli.exe` din instalarea ta, pe folderul sesiunii, cu scriptul ales
dintr-o listă. Lista e cea din meniul de scripturi al lui Siril — fișierele `.ssf` bifate în
*Preferințe › Scripturi* plus cele din folderele de scripturi de acolo, citite din
`config.1.4.ini` — iar propus e **`OSC_Preprocessing_WithoutDark.ssf`**, din depozitul de
scripturi pe care Siril îl descarcă — scriptul autorului, pentru un aparat fără darks.
`OSC_Preprocessing.ssf` din instalare merge la fel de bine: acela lasă masterul în
`masters/`, unde aplicația îl caută și ea. Aplicația nu aduce niciun script cu ea și nu
modifică niciunul; ce rulează e exact fișierul pe care l-ar rula Siril.

Se deschide o fereastră cu ieșirea lui Siril în timp real, ca panoul lui de log: comanda
curentă (*comanda 7 din 20*, cu textul ei întreg, fiindcă Siril tipărește doar verbul), bara
de progres a operației în curs, și liniile de log. Liniile de progres — trei sferturi din ce
scrie Siril — devin bara, nu text. La sfârșit fereastra spune dacă scriptul a reușit sau la
ce comandă s-a oprit, iar când o închizi folderul se redeschide și analiza pornește pe ce a
produs. **Oprește** omoară procesul; ce a apucat să scrie rămâne în `process/`, iar o rulare
nouă suprascrie tot — verificat: scriptul rulat a doua oară peste un `process/` complet sau
parțial reface fiecare fișier și iese cu 0.

Trei lucruri pot ține butonul blocat, și ecranul spune care: `siril-cli.exe` negăsit (se
caută prin intrarea de instalare din registru, apoi în `Program Files\Siril\bin`), niciun
script `.ssf` în configurarea Siril, sau un subfolder de intrare lipsă ori gol — scriptul
ales e citit dinainte și folderele în care intră (`biases/`, `flats/`, `lights/` pentru
WithoutDark) trebuie să existe și să aibă fișiere, altfel Siril s-ar opri la a treia linie.
Dacă Siril e deschis apare un avertisment, nu un blocaj: două procese care scriu în același
`process/` strică fișierele, dar aplicația nu poate ști pe ce folder lucrează celălalt.

Butonul **Despre** din bara de sus (sau **F1**) deschide o fereastră cu versiunea,
copyright-ul, nota de licență și de garanție — același text pe care consola îl tipărește la
`--help` — și lista componentelor terțe, cu licența fiecăreia și link la proiect. E acolo
și din obligație, nu doar din politețe: GPLv3 cere ca un program cu interfață să-și arate
singur notița, iar LGPL-ul sub care e luat LibRaw cere ca notița bibliotecii să apară
lângă a noastră. Un `.exe` copiat singur n-are fișierele `LICENSE` și
`THIRD-PARTY-NOTICES.md` lângă el, deci fereastra e singurul loc în care notițele
călătoresc garantat cu programul.

### Cele șase taburi

| tab | ce face | când rulează |
|---|---|---|
| **Structura** | modul 1 pe `pp_flat_stacked.fit` din `masters/` sau `process/` | la deschiderea folderului |
| **Ratio** | modul 2 între două mastere alese explicit, din două sesiuni | după ce alegi ambele fișiere |
| **End to end** | modul 3 pe toată seria `pp_light_*.fit` din `process/` | la deschiderea folderului |
| **Expunere** | modul 4 pe un raw din `flats/` | la alegere, și la comutarea „Sky" |
| **Histogram** | modul 5 pe masterul sesiunii, pe flat-ul netezit de lângă el (când există) sau pe un cadru din `flats/`, raw sau FITS | la alegere; comutarea vederii nu recalculează |
| **Netezire** | modul 6 pe masterul sesiunii; scrie `pp_flat_stacked_netezit.fit` lângă el | la buton, niciodată singur — scrie un fișier |

**Structura** arată aceleași cifre ca în consolă — cele cinci teste de abatere cu verdictul
fiecăruia, scara de reziduu cu banda curentă marcată, mărimile informative fără verdict,
atenționarea de praf sub verdict când e cazul, fiecare rând cu descrierea lui sub el — plus
harta reziduală. Salvează în folderul
ales:

- **`Rezultat testare.txt`** — ieșirea consolei, identică: banner de versiune, copyright,
  raportul, linia hărții;
- `flatcheck.png` — harta pe scara automată, aceiași octeți pe care i-ar scrie consola.

Harta de pe ecran are butoanele de scară și legenda descrise la [Scara
culorilor](#scara-culorilor), plus ghidul de forme sub ea. Fișierul rămâne pe `auto`
indiferent ce buton e apăsat, deci se potrivește cu chipul `auto`, nu neapărat cu ce vezi.

**Ratio** nu caută nimic în folder și nu presupune nimic: îți cere **două fișiere alese
explicit**, prin două butoane. Motivul e că modul are nevoie de două *mastere*, câte unul
pentru fiecare orientare, iar cele două orientări sunt două ședințe — deci două foldere de
sesiune. Un singur folder nu le poate propune; ce putea propune erau cadrele individuale,
iar o pereche de cadre individuale dă un rezultat sigur și fals, fiindcă zgomotul unui
cadru e de același ordin cu semnalul căutat.

Deasupra selectoarelor stă un panou care explică exact ce îi trebuie modului, cum se produc
cele două mastere în Siril, și ce **nu** îți poate spune testul. Îl poți plia, dar pornește
deschis: e singurul mod al aplicației care cere ceva ce nu se poate deduce din folderul
deschis.

Ferestrele de deschidere propun `pp_flat_stacked*.fit`, adică **stiva**, nu cadrele
calibrate individuale (`pp_flat_00001.fit` și următoarele) care stau în același `process/`
cu același prefix. Dacă fișierul ales nu e un master de flat, aplicația explică ce sunt
fișierele astea și de ce testul are nevoie de unul, în loc să eșueze la parsare.
`pp_flat_stacked.fit` **este** o alegere validă — în fluxul normal chiar e alegerea: câte
unul din fiecare dintre cele două sesiuni; iar dacă ai adus ambele mastere în același
folder și ai redenumit unul, `pp_flat_stacked_0deg.fit` rămâne recunoscut. Același fișier
în ambele sloturi se refuză la alegere, cu explicație.

După calcul, tabul arată întâi **de unde vin cele două stive** — câte cadre și de la ce oră,
din headerele FITS — fiindcă acolo se vede singurul lucru pe care tool-ul nu-l poate
verifica singur: că cele două chiar pot fi din două orientări diferite. Apoi componenta
fixă în lume, informativă, cu bara ei de eroare, cu cele două rânduri explicate sub ele
și cu eticheta OK/SUSPECT/PROBLEMĂ spusă pe față drept informativă, cu pragurile ei — ea
nu votează. Apoi verdictul și cele cinci teste, pe
masterul combinat, fiecare cu descrierea lui, urmate de mărimile informative — vignetarea,
centrul de iluminare, petele locale — și de atenționarea de praf când e cazul, exact ca
în tabul Structură și ca în raportul din consolă. Harta are un comutator: reziduul părții fixe pe aparat (implicit, se
potrivește cu verdictul) sau raportul `A/B`. Scara și legenda se aplică celei afișate,
oricare dintre cele două e.

**End to end** rulează pe toată seria și își umple tabelul pe măsură ce cadrele sunt gata —
32 de cadre iau vreo două minute, iar un ecran gol tot timpul ăla ar părea blocat. Poate
ieși legitim `INAPLICABIL`: pe un câmp mai larg de ~40 de grade diagonală cerul și
vignetarea au aceeași formă, deci nu se produce nicio cifră. **Nu e o eroare** — vezi
secțiunea despre când merge `--light`. Pe date ca astea răspunde tabul Ratio.

**Expunere** listează din `flats/` **doar raw-uri de aparat** (`.nef`, `.cr2`, `.cr3`,
`.arw`, `.dng`, `.raf`). Modul are nevoie de mozaicul CFA intact și de scala ADU originală,
iar pe un raw white și black se citesc exact din fișier în loc să fie ghicite din maxim.
Comutatorul „Sky" schimbă pragurile și recalculează pe loc.

Tabul desenează și **câte o histogramă pe canal**, pe aceeași axă procentuală pe care stau
toate cifrele lui, cu banda țintă, dunga de clipping și mediana centrului trase peste. Sub
ele, un paragraf dedus din rezultat spune cum e expus fișierul ăsta anume: cine decide, cât
mai e până la saturație, și de ce curba lată **nu** înseamnă subexpunere. Desenul explică;
verdictul rămâne mediana ferestrei centrale a canalului celui mai luminos și atât.

> Tabelul de mai jos e în oglindă cu ghidul pliat „Cum citesc histogramele" din fereastră
> (`SessionMessages.HistogramGuideRows`). Nu există test care să lege cele două — dacă
> reformulezi într-un loc, treci și prin celălalt.

| ce vezi | ce înseamnă |
|---|---|
| două curbe pe fiecare canal | cea palidă și lată e tot canalul; cea plină și îngustă e fereastra centrală de 20%, chiar cea din care se ia verdictul. Fiecare e scalată la propriul vârf, deci **înălțimile** nu se compară — pozițiile se compară. |
| linia verticală | mediana ferestrei centrale, singura cifră care decide. Pe canalul cel mai luminos e chiar numărul din pastila de verdict. |
| banda verde | ținta, din exact aceleași praguri ca verdictul. Se mută când bifezi „Sky". |
| benzile galbene și roșii | aceeași scară mai departe. Pe cer nu există galben în stânga: acolo ținta începe chiar de la pragul de prea-jos. |
| dunga roșie din dreapta | 98–100% din white, unde analizorul numără pixelii saturați. Orice masă a curbei pline acolo înseamnă PREA SUS, oricât de cuminte ar fi mediana. |
| de ce curba palidă e mult mai lată | aia **e** vignetarea, nu subexpunere: colțurile unui flat sunt mai întunecate prin construcție. Dacă expui după ea, clipezi centrul. |
| de ce canalele au vârfuri diferite | filtrele Bayer lasă să treacă cantități diferite din lumina sursei. Pe un panou cald poate conduce R, pe cer la ora albastră de obicei B. Unealta nu face balans de alb — tocmai asta e ideea. |
| ce **nu** e | histograma de pe aparat. Aia e a JPEG-ului procesat (debayer + WB + gama) și stă mult mai la dreapta. |
| comutatorul de deasupra desenelor | alege la ce te uiți: datele brute, pe care le măsoară unealta, sau previzualizarea pe care aparatul a înglobat-o în raw, chiar imaginea din care și-a calculat histograma de pe ecran. Numai vederea brută are bandă țintă. Pe cealaltă nu e nicio țintă și nici n-ar putea fi: ar însemna să expui după ecran. |
| de ce ecranul e mult mai la dreapta | gama plus curba de ton, pe care nicio setare din aparat nu le scoate. O curbă compresivă întinde cel mai tare nivelurile de jos și de mijloc, adică exact unde stă un flat. Pe aparatele măsurate, ecranul s-a saturat înaintea senzorului cu o treime de stop până la un stop, deci e o alarmă într-un singur sens, nu un instrument de măsură. |

**Ce a arătat aparatul.** Comutatorul de deasupra desenelor trece de la datele brute la
previzualizarea JPEG înglobată în raw, decodată din fișier. Sub desene, un tabel pune cele
două cifre cap la cap pe fiecare canal, centru cu centru. Măsurat pe două flat-uri de cer
din bibliotecă, canalul albastru:

| fișier | senzor (raw) | ecranul aparatului | vârf pe ecran |
|---|---|---|---|
| Nikkor 35 DX, ISO 800 | 29.0 % | 81.2 % | 84.7 % |
| Samyang f/2.0, ISO 1600 | 32.1 % | 98.0 % | 100.0 %, cu pixeli la 255 |

Al doilea arăta ars pe aparat și avea 1.6 stopuri de rezervă în fișier. Diferența nu e o
constantă de ținut minte: pe aparatul măsurat e maximă pe la raw 25 %, chiar unde stă un
flat, și se strânge spre saturație. Pe vederea din previzualizare **nu există bandă
țintă**, iar verdictul nu se uită la ea deloc. Vederea lipsește, cu explicație, pe un FITS
sau pe un raw fără previzualizare decodabilă.

**Histogram** listează în stânga **masterul pe prima poziție**, marcat `MASTER`; sub el,
**când există, `pp_flat_stacked_netezit.fit`**, marcat `NETEZIT` — fișierul scris de tabul
Netezire, exact pe calea pe care o scrie acela, nu orice stivă din folder; și apoi cadrele
din `flats/` — raw de aparat **și** FITS, fiindcă pentru egalizare un flat trecut prin
Conversion e la fel de bun ca NEF-ul. Un click egalizează fișierul; alt click egalizează
altul, iar dacă primul încă se calcula, e abandonat — lista rămâne apăsabilă, ecranul de
așteptare acoperă doar poza. Nimic nu rulează singur la deschidere: masterul de 24 MP îl
citește deja tabul Structura atunci.

Rândul `NETEZIT` **apare și singur**, în timpul sesiunii, de îndată ce butonul din Netezire
a scris fișierul; ce era ales în listă rămâne ales și nimic nu se recalculează — cu o
excepție: dacă pe ecran era chiar flat-ul netezit și acesta a fost rescris, poza se reface,
altfel ar arăta octeți care nu mai există pe disc. Sub poza netezitului stă o notă a lui:
o formă care se vede pe master și lipsește de aici a fost scoasă de netezire; una care se
vede pe amândouă e netedă la scara aleasă și a rămas. Și, egalizarea fiind pe ranguri, ce a
rămas pe netezit, oricât de mic în amplitudine, iese la contrast maxim — un netezit care
pare mai *zgomotos* decât masterul e poza așteptată, nu un defect.

Masterul e acolo pentru o singură întrebare: **s-au mediat imperfecțiunile văzute pe
cadrele individuale?** O formă care se vede pe un cadru și lipsește de pe master a fost
trecătoare — un nor, o umbră, o pâlpâire — și s-a mediat. Una care se vede pe amândouă e
fixă pe montaj, și pe ea o măsoară cifrele din Structura. Egalizarea fiind pe ranguri,
zgomotul mai mic al masterului face o formă comună să pară acolo mai *clară*, nu mai slabă.

Deasupra pozei stau vederile, pe un cadru CFA: `Mozaic` (implicit — tot mozaicul egalizat
ca un singur strat, literal ce arată Siril pe un raw deschis nedebayerizat, deci vederea
din care s-au citit tipare până acum), `RGB` (cele patru poziții egalizate fiecare pe
repartiția ei, în culorile lor, cu mai mult contrast) și câte un gri pe poziție (`R`, `G1`,
`G2`, `B`, sau `CFA[y,x]` când fișierul nu spune ordinea). Pe un cadru mono e o singură
vedere și nu apar chipuri. Comutarea nu recitește nimic. Tabul nu salvează niciun fișier;
consola scrie compozitul, micșorat.

Poza din fereastră e **la rezoluția senzorului**, nu micșorată ca PNG-ul consolei: se
arată direct din raster, fără encodare. Stă într-un viewport de 520 px, potrivită
întreagă la început, cu zoom și deplasare: rotița mărește în jurul cursorului (fără niciun
modificator, ca în Siril; pagina se derulează cu rotița în afara pozei), tragerea cu
mouse-ul deplasează când poza depășește viewport-ul (cursorul devine mână), dublu click
comută între potrivit și 100 %, iar butoanele `−`, `+`, `potrivit`, `100 %` fac același
lucru. La 100 % un pixel de pe ecran e un pixel de
senzor; pe `Mozaic` se vede tabla de șah R/G/B a mozaicului, exact ca în Siril la 100 %.
Peste 100 % pixelii se desenează ca pătrate, sub 100 % neteziți. „Potrivit" nu mărește
niciodată peste 100 %: o poză mai mică decât viewport-ul se arată la mărimea ei. Zoom-ul se
păstrează când schimbi vederea și se resetează când schimbi fișierul.

> Tabelul de mai jos e în oglindă cu ghidul pliat „Cum citesc poza egalizată" din fereastră
> (`SessionMessages.HistEqGuideRows`). Nu există test care să lege cele două — dacă
> reformulezi într-un loc, treci și prin celălalt.

| ce vezi | ce înseamnă |
|---|---|
| de ce un flat curat arată zgomotos | egalizarea întinde *orice* distribuție pe 0..255, și a unui cadru fără nicio structură. Atunci ce rămâne de întins e zgomotul. Granulație fină peste tot înseamnă „n-are altceva de arătat". |
| de ce nu se poate citi cât de mare e o formă | fiecare nivel de gri primește același număr de pixeli, indiferent cât de apropiate erau valorile. O undă de 0.5 % și un gradient de 10 % cu aceeași formă ies identic. Amplitudinea e în tabul Structura. |
| vederea Mozaic (implicită) | tot mozaicul ca un singur strat — Siril pe un raw nedebayerizat, de asta e prima. Trei culori în aceeași histogramă, deci la 100 % se vede tabla de șah; micșorată, se mediază. |
| vederea RGB | cele patru poziții, fiecare egalizată separat și pusă în culoarea ei, cu mai mult contrast decât mozaicul. O nuanță uniformă nu înseamnă nimic; o nuanță care *variază* pe cadru înseamnă că un canal are altă formă decât celelalte — o sursă cu gradient de culoare, o scurgere colorată. |
| vederile pe canal | aceeași poză, un singur canal, în gri. Ca să vezi dacă o formă e în toate canalele sau doar într-unul. |
| zoom și deplasare | rotiță, tragere cu mouse-ul, dublu click; pagina se derulează cu rotița în afara pozei. La 100 % un pixel de pe ecran e un pixel de senzor. Se păstrează la schimbarea vederii, se resetează la schimbarea fișierului. |
| masterul față de un cadru | vezi paragraful de mai sus: ce lipsește de pe master s-a mediat, ce e pe amândouă e fix pe montaj. |
| unde e cifra | în tabul Structura. Poza spune ce *formă* are ce măsoară ea. |

**Netezire** e modul 6 pe masterul sesiunii, și singurul tab care **nu rulează singur**:
scrie un fișier de ~96 MB lângă master, și un fișier nu se produce fiindcă s-a deschis un
folder. Ghidul stă deschis deasupra butonului, fiindcă e precondiția lui; sub el, trei
chipuri de scară (150 / 300 / 600 px, implicit 300), butonul „Propune scara" — care
rulează regula de mai sus (`--scale auto`), arată tabelul treptelor și bifează chipul
propus, fără să scrie nimic — și butonul „Netezește și salvează".
Butonul așteaptă lanțul de la deschidere — structura citește același master de 24 MP — și
întreabă înainte să suprascrie un fișier netezit existent; un „nu" lasă cifrele pe ecran
și fișierul vechi pe disc, și spune asta.

După rulare: cifrele despre *ce s-a scos*, atenționarea galbenă când s-a scos și ceva de
scară mare, bateria de structură **înainte** și **după** (aceleași rânduri ca în tabul
Structura, cu verdictul fiecăreia), și în dreapta două hărți în spatele unui comutator —
`original / netezit`, adică exact ce a plecat, și reziduul flat-ului netezit după propriul
model radial, adică ce face obiectivul odată textura plecată. Scara hărții pornește la
±2 %, nu la ±10 % ca la celelalte taburi: ce se scoate e mic, și la 10 % harta ar fi albă.

Fișierul netezit **nu devine masterul sesiunii**: numele lui nu e printre cele două căi pe
care le caută aplicația, deci tabul Structura măsoară în continuare masterul fotografiat
și arată textura pentru care a fost nevoie de netezire. Dialogul din Ratio îl acceptă
însă, fiindcă o stivă netezită e tot o stivă. Și **apare în tabul Histogram**, ca rând
propriu sub master, marcat `NETEZIT`, imediat ce a fost scris — ca să se vadă, unul lângă
altul, ce a scos netezirea și ce a lăsat.

> Ghidul pliat „Ce face netezirea" (`SessionMessages.SmoothGuideRows`) e în oglindă cu
> secțiunea „6. Netezire" de mai sus; nu există test care să lege cele două.

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

- **`--histeq` există doar în C#.** N-are paritate în `flatcheck.py` și niciun scenariu în
  `crosscheck.py`, prin decizie: nu produce nicio cifră de comparat.
- **`--smooth` există doar în C#**, din același motiv: produce un fișier, nu o cifră.
  Metoda a fost aleasă și validată cu un prototip numpy pe cinci mastere reale, iar
  portul C# reproduce prototipul la a doua zecimală; prototipul n-a fost păstrat.
- **Fără figură cu 4 panouri** — doar harta reziduală PNG.
- Fără citire EXIF, deci fără sugestii de timp de expunere. (Raw-ul se citește, dar din el
  se iau doar pixelii și scala ADU, nu și timpul de expunere.)
- Linia de sursă a scalei scrie `libraw (exact)` acolo unde Python scrie `rawpy (exact)`.
  E aceeași bibliotecă; numele diferă fiindcă `rawpy` e ambalajul de Python al ei. Din
  același motiv, pe un raw linia cu ordinea canalelor scrie `din libraw` față de
  `din rawpy`. Pe FITS amândouă citesc `BAYERPAT`, deci acolo linia e identică.
- Fără histograme pe canal și fără comparația cu previzualizarea aparatului — sunt doar în
  fereastră. În consolă un paragraf despre forma unei curbe pe care consola n-o desenează
  n-ar avea ce căuta. Versiunea Python nu citește deloc previzualizarea înglobată în raw.
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
`flatcheck --help` și în fereastra **Despre** a aplicației cu ferestre; copyright-ul se
tipărește la fiecare rulare, sub versiune.

### Componente terțe

Unealta leagă trei componente scrise de alții, toate compatibile cu GPLv3:

| componentă | rol | licență |
|---|---|---|
| Sdcb.LibRaw | ambalajul C# peste libraw | MIT |
| LibRaw (DLL-urile native) | citirea fișierelor raw de aparat | LGPL-2.1 (opțiunea aleasă din dubla LGPL-2.1 / CDDL-1.0) |
| SixLabors.ImageSharp 3.1.x | decodarea previzualizării JPEG din raw | Six Labors Split License 1.0, care acordă termenii Apache 2.0 proiectelor open source. Fixat pe 3.1.x fiindcă de la 4.0 pachetul cere o cheie de licență la compilare, pe Release ca eroare. |

Cititorul FITS, encoderul PNG, toate calculele și toate pragurile sunt scrise de mână.

Motivele pentru care fiecare licență e compatibilă — inclusiv de ce LibRaw se ia sub LGPL
și nu sub CDDL — sunt în [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). Aceeași listă,
cu notița de copyright a fiecărei componente, o arată și fereastra **Despre**.
