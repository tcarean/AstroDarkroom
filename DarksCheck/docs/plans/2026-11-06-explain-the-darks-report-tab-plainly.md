# Explain the Raport tab in plain words

Plan for the task branch `claude/Explain-the-darks-Report-tab-plainly`, worktree
`.claude/worktrees/Explain-the-darks-Report-tab-plainly`, from `main` at 0eff39b3. Written on
2026-11-06. The owner picked the Raport tab, the last one, after Index (ADR 0036). The version is
not changed.

## Context

The Raport tab shows the console's text, the same to the byte; its sections, tables and the ADU
scale's rows are the other tabs' texts, already in plain words (ADRs 0028 to 0036). What is left
are the texts around the report, in two parts, each approved on its own:

1. The window's start screen, the progress lines (the window's wait screen and the console's
   stderr line, both from the phases `DarkSeriesRun` names), and the hints of the top bar's four
   settings, with the second one's name.
2. The console's `--help` and its one misleading error (`folderul e gol` for a blank path).

## Part 1, approved as proposed ("Da, toate", 2026-11-06)

| # | Text | Before | After |
| --- | --- | --- | --- |
| 1 | the start screen | `Folderul de lucru din Siril, cel care contine darks\ si, optional, biases\ si lights\, sau un folder cu dark-urile direct in el (o biblioteca). Analiza porneste singura dupa ce l-ai ales: bias-urile, dark-urile si lights-urile sunt decodate si masurate, apoi vine indexul si eticheta.` | `Folderul de lucru din Siril, cel care contine darks\ si, daca le ai, biases\ si lights\, sau un folder cu dark-urile direct in el (o biblioteca). Analiza porneste singura dupa ce l-ai ales: programul citeste si masoara bias-urile, dark-urile si lights-urile, apoi socoteste indexul si eticheta.` |
| 2 | the progress phases | `decodare bias-uri`, `decodare dark-uri`, `decodare dark-uri (partener)`, `decodare partener`, `masurare dark-uri`, `masurare lights` | `Se citesc bias-urile`, `Se citesc dark-urile`, `Se citesc dark-urile partenerului`, `Se citeste partenerul`, `Se masoara dark-urile`, `Se masoara lights-urile` |
| 3 | `FIRE`'s hint | `Cate cadre se decodeaza in paralel; gol = jumatate din nuclee` | `Cate cadre se citesc in acelasi timp; gol = jumatate din nucleele procesorului` |
| 4 | `CADRE MAX`'s hint | `Cate cadre se tin in memorie (48 MB fiecare la 24 MP) si cate lights se masoara; peste, se ia unul din k. Implicit 60.` | `Cate cadre se tin in memorie (48 MB fiecare la 24 MP) si cate lights se masoara; daca sunt mai multe, se citesc doar unele, la distante egale (de exemplu unul din doua). Implicit 60.` |
| 5 | the second setting | `CASTIG / TREAPTA ISO`; `De cate ori creste semnalul la o treapta de ISO, pentru normalizarea la ISO 400; 2 = liniar in ISO (D3300)` | `SEMNAL / TREAPTA ISO`; `De cate ori creste semnalul la fiecare treapta de ISO; cu el, toate cadrele se socotesc la ISO 400, ca sa poata fi comparate. 2 = semnalul se dubleaza la fiecare treapta (D3300)` |
| 6 | `GRADE / DUBLARE`'s hint | `La cate grade se dubleaza curentul de intuneric, pentru gradele din raport; 6 la D3300` | `La cate grade in plus se dubleaza curentul de intuneric (ce aduna senzorul din caldura lui); din el se socotesc gradele din raport. 6 la D3300` |

The English says the same, with the same figures: `Reading the darks: 3 / 20`, `SIGNAL / ISO STOP`.

## Steps

1. Requirements: R-088, new, grown by each part; R-025 and R-075 quote the new words.
2. Tests: `R088_...` collects the phases of real runs (with biases, lights and a partner, and a
   session that takes the partner's darks) and renders the empty window, in both languages, with
   the word list of R-083 to R-087, which takes `liniar` / `linear`; the tests that read the
   progress line take the new phases.
3. Code: `WindowTexts.cs`, `MainWindow.xaml`, `DarkSeriesRun.cs`; the progress line is on stderr,
   not in the goldens, and no figure changes, so no golden changes and no `/validate`.
4. Gate, ADR 0037, CHANGELOG, README, the design note, commit; then part 2's list.
