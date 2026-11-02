# Explain the Cadre tab in plain words

Plan for the task branch `claude/Explain-the-darks-Frames-tab-plainly`, worktree
`.claude/worktrees/Explain-the-darks-Frames-tab-plainly`, from `main` at 785888b4. Written on
2026-11-02. The owner picked the Cadre tab after the Rezumat (ADR 0030) and approved its texts
in one list, shown with the real texts of the Smida session before any edit ("Aprobat"). The
version is not changed.

## Context

The Cadre tab shows the frames table, the same the console prints as `TABEL PE CADRE`, a hover
hint on every column header, and three charts beside it: the hot pixels against the master, how
much each frame differs from the master, and the random banding. The core words the table, its
note, the reasons of the frames to discard and the warming chart's sentence; the window words
the headers, the hints, the chart titles and sentences and the guide lines.

## The list, approved as proposed ("Aprobat", 2026-11-02)

| # | Text | Before | After |
| --- | --- | --- | --- |
| 1 | column names | `nr  fisier  ora  medie  sigma  rezid.  benzi  amplit.  platou  lumina  latura  >20` | `nr  fisier  ora  medie  zgomot  dif.  benzi  fierb.  platou  lumina  latura  >20` |
| 2 | the table's note | `[G1; medie = ADU peste negru; benzi = randuri / zgomot alb; amplit. = pixelii fierbinti fata de master; platou = grade fata de platoul seriei; de aruncat = la peste 2.0 grade de platou (verificarea 3); lumina = ADU-eq peste dreapta mediei pe incalzire; latura = dreapta - centru dupa bias (glow inclus), ADU-eq]` | `[pixelii verzi G1; medie = cat e cadrul peste negru, ADU; zgomot = zgomotul cadrului, ADU; dif. = cat difera cadrul de master, ADU; benzi = de cate ori difera randurile intre ele mai mult decat ar da zgomotul singur (1 = fara benzi); fierb. = pixelii fierbinti fata de master (1 = la fel); platou = cu cate grade e cadrul fata de temperatura spre care urca seria; de aruncat = la peste 2.0 grade de ea (verificarea 3); lumina = lumina in plus fata de cat explica incalzirea, ADU-eq; latura = latura din dreapta fata de centru, dupa scaderea bias-ului (cu glow), ADU-eq]` |
| 3 | a frame to discard | `de aruncat: amplit. 0.826 sub 0.87 (peste 2.0 grade sub platoul 1.10)` | `de aruncat: pixelii fierbinti sunt la 0.826 din master, sub 0.87, adica la peste 2.0 grade sub platou (1.10)` |
| 4 | a skipped frame | `sarit (peste 60 de cadre, se decodeaza unul din 2)` | `sarit (peste 60 de cadre se citeste doar unul din 2)`, in the lights table too |
| 5 | hint `ora` | `ora de start a expunerii, din metadatele fisierului` | `ora la care a inceput expunerea, cum a scris-o aparatul in fisier` |
| 6 | hint `medie` | `media cadrului peste negru, ADU` | `cat e cadrul, in medie, peste negru, in ADU` |
| 7 | hint `zgomot` | `sigma cadrului pe planul de referinta, ADU` | `zgomotul cadrului, in ADU (pe pixelii scrisi in nota din dreapta)` |
| 8 | hint `dif.` | `sigma reziduului cadru - master, ADU` | `cat difera cadrul de master, in ADU (zgomotul lui fata de master)` |
| 9 | hint `benzi` | `deviatia mediilor pe randuri, de cate ori zgomotul alb` | `de cate ori difera randurile intre ele mai mult decat ar da zgomotul singur; 1 = fara benzi` |
| 10 | hint `fierb.` | `amplitudinea pixelilor fierbinti ai masterului in cadru, fata de master` | `cat de fierbinti sunt in acest cadru pixelii fierbinti ai masterului, fata de master (1 = la fel); arata cat de cald era senzorul` |
| 11 | hint `platou` | `gradele cadrului fata de platoul seriei, citite din pixelii fierbinti; un cadru la peste 2.0 grade de platou e de aruncat si are motivul pe randul de sub celule (verificarea 3)` | `cu cate grade e cadrul fata de temperatura stabila spre care urca seria (platoul), dupa pixelii fierbinti; un cadru la peste 2.0 grade de platou e de aruncat si are motivul pe randul de sub el (verificarea 3)` |
| 12 | hint `lumina` | `lumina in plus fata de dreapta mediei cadrului pe incalzire, ADU-eq: ce se schimba de la un cadru la altul (verificarea 2)` | `cata lumina in plus are cadrul fata de cat explica incalzirea, in ADU-eq: lumina care vine si pleaca de la un cadru la altul (verificarea 2)` |
| 13 | hint `latura` | `abaterea fata de centru, in acel cadru, a laturii cu cea mai multa lumina constanta (margine plus cele doua colturi), dupa bias cand exista bias-uri, glow inclus, ADU-eq; partea ei constanta e lumina din toate cadrele (verificarea 2)` | `cat difera de centru, in acest cadru, latura cu cea mai multa lumina constanta (marginea si cele doua colturi ale ei), dupa ce se scade bias-ul, daca exista, cu glow cu tot, in ADU-eq; ce nu se schimba de la un cadru la altul e lumina din toate cadrele (verificarea 2)` |
| 14 | hint `>20` | `pixeli peste 20 ADU-eq pe planul de referinta` | `cati pixeli din cadru trec de 20 ADU-eq (pe pixelii scrisi in nota din dreapta)` |
| 15 | the warming chart | `Amplitudinea pixelilor fierbinti ai masterului in fiecare cadru, fata de master: panta e incalzirea camerei in timpul seriei (verificarea 3). Platoul seriei e la 1.10 din master; cadrele sub 0.87 sau peste 1.38 (la peste 2.0 grade de platou) sunt de aruncat si apar in culoarea de atentie.`; `Sub 6 cadre cu pixeli fierbinti nu se potriveste un platou.`; the line `masterul (mediana seriei)` | `Cat de fierbinti sunt pixelii fierbinti ai masterului in fiecare cadru, fata de master: cand curba urca, camera se incalzeste in timpul seriei (verificarea 3). Seria urca spre o temperatura stabila (platoul), la 1.10 din master; cadrele sub 0.87 sau peste 1.38 (la peste 2.0 grade de platou) sunt de aruncat si apar in galben.`; `Cu mai putin de 6 cadre cu pixeli fierbinti nu se poate gasi platoul.`; `masterul`; its title stays |
| 16 | the second chart | `SIGMA REZIDUALA (ADU)`; `Zgomotul fiecarui cadru fata de master; peste linia punctata cadrul e un outlier (verificarea 4).`; the line `1.5 x mediana` | `CAT DIFERA FIECARE CADRU DE MASTER (ADU)`; `Zgomotul fiecarui cadru fata de master; un cadru peste linia punctata e mult mai zgomotos decat celelalte (verificarea 4).`; `de 1.5 ori zgomotul obisnuit` |
| 17 | the third chart | `BENZI ALEATOARE (x ZGOMOT ALB)`; `Deviatia mediilor pe randuri fata de cat ar da zgomotul alb; 1 = fara benzi.`; the line `zgomot alb` | `BENZI ALEATOARE (1 = FARA BENZI)`; `De cate ori difera randurile cadrului intre ele mai mult decat ar da zgomotul singur; 1 = fara benzi.`; `fara benzi` |
| 18 | the messages when no frame of a kind can be read | `niciun dark nu s-a putut decoda`, `niciun bias ...`, `niciun light ...`, `niciun dark al partenerului ...` | `... nu s-a putut citi` |

They stay: the title `TABEL PE CADRE`, the hints of `nr` and `fisier`, the errors `dimensiune
diferita: ...` and `setari diferite de primul cadru: ...`, the warming chart's title. The English
says the same, with the same figures in the same order.

## Steps

1. Requirements: R-083, new, names the method's words the tab must not use; R-025, R-027,
   R-053, R-071 and R-110 quote the new words.
2. Tests: the tests that quote the texts take the new words; `R083_...` reads the core's texts
   over the variants the fixtures reach and renders the tab in both languages, with one shared
   list of the method's words.
3. Code: `Report/DarkReport.cs`, the skipped and the no-frame messages in `Analysis/` and
   `Runs/`, `FramesView.xaml`, `MainViewModel.cs` and `WindowTexts.cs`, the figures untouched.
4. Goldens recaptured; every changed line is the table's header, every figure kept.
5. Gate, ADR 0031, CHANGELOG, README, `/validate`, commit, the merge proposal.

## What deviated

- The note and the warming chart say the series climbs to its plateau, `urca`, or comes down to
  it, `coboara`, when the plateau is under the first frame (the Rezumat's rule for `platou`);
  the hint of `platou`, which knows no series, keeps `urca`, the usual case of a camera that warms
  itself.
- The side in the note takes its preposition, `latura de sus`, `latura din dreapta`, as the list
  showed for the right side; without biases `(fara bias-uri, cu glow)`; unmeasured `latura =
  latura cu cea mai multa lumina constanta fata de centru (NEMASURAT)`.
- A frame above the plateau reads `... peste 1.38, adica la peste 2.0 grade peste platou (1.10)`.
- The list of the method's words lives in `tests/DarksCheck.Tests/PlainWords.cs`, linked into
  the window's test project like the session fixture, so the two R083 tests share it.
