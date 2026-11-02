# Explain the Frames tab in plain words

## Context

The owner asked on 2026-11-02 for every LightsCheck tab in plain words, one tab at a time (ADR
0032, rule "Plain words"); Dithering (0e7f8265) and Summary (472bfb7b) are merged. The Cadre
(Frames) tab is next: its column tooltips, the reasons in its `motive` column, and the star
cut-outs' caption, note and labels. The reasons are also the Rejected tab's list and the
console's RESPINSE section and frame table, so they change there too. The owner approved the 18
Romanian proposals in chat on 2026-11-02 ("Da, si in consola"). Column titles, class words and
the cells with figures (`RA 17 px ← V`, `7/10 de la 61`) stay.

## Branch, numbers

- Branch `claude/Explain-the-Frames-tab-plainly`, worktree
  `LightsCheck/.claude/worktrees/Explain-the-Frames-tab-plainly`, from `main`.
- R-079 and ADR 0034 (main holds R-078 and ADR 0033; checked again before the merge).
- This plan saved as `docs/plans/2026-11-02-explain-the-frames-tab-plainly.md`.

## The texts (Romanian as approved; English to the same meaning)

**Column tooltips** (`Views/FramesView.xaml`)
1. alung.: `cat de alungite sunt stelele, de obicei (1 = rotunde)` / `how elongated the stars are, typically (1 = round)`.
2. FWHM: `latimea stelelor (FWHM), de obicei, in px` / `the stars' width (FWHM), typically, in px`.
3. fond: `fondul cerului pe verde, in ADU peste nivelul de negru` / `the sky background on green, in ADU above the black level`.
4. pauza: `cate secunde au trecut de la sfarsitul cadrului dinainte` / `how many seconds passed since the end of the frame before`.
5. pas: `cat s-a mutat fata de cadrul dinainte, in px` / `how far it moved from the frame before, in px`.
6. dX / dY: `unde a stat fata de cadrul 1, in px, pe axa X sau Y a imaginii; in paranteza, axa monturii` / `where it sat relative to frame 1, in px, along the image's X or Y axis; in brackets, the mount's axis`.
7. dithering: `cum a fost mutata montura la dithering: pe ce axa, cu cati pixeli, incotro pe cer si cu ce sageata SynScan (EST si NORD din bara de sus spun care sageata e care)` / `how the mount was moved for the dither: along which axis, by how many pixels, which way on the sky and with which SynScan arrow (EAST and NORTH in the top bar say which arrow is which)`.
8. acelasi loc: `al catelea cadru la rand in acelasi loc (la mai putin de 10 px), din cate, si de la ce cadru incepe grupul; din astfel de grupuri vine walking noise-ul` / `which frame in a row in the same spot (less than 10 px) it is, of how many, and from which frame the run starts; walking noise comes from such runs`.

**Reasons** (`SeriesReport.Reason`; the Frames and Rejected tabs and the console)
9. Bad trail: `alungire 1.68 (de la 1.60 cadrul e slab), dara pe RA (83 grade)` / `elongation 1.68 (from 1.60 the frame is bad), trail on RA (83 degrees)`.
10. Marginal trail: `alungire 1.38 (de la 1.35 cadrul e la limita), dara pe RA (83 grade)` / `elongation 1.38 (from 1.35 the frame is marginal), trail on RA (83 degrees)`.
11. Soft: `stele late: latimea (FWHM) 4.50 px, peste 4.06, adica de 1.3 ori latimea obisnuita din sesiune` / `wide stars: width (FWHM) 4.50 px, over 4.06, that is 1.3 times the session's usual width`.
12. Clouds: `prea putine stele: 400, sub 600 (60 % din cate are de obicei un cadru): nori sau ceata` / `too few stars: 400, under 600 (60 % of a frame's usual count): clouds or haze`.
13. Background jump: `fondul cerului sare cu +25 % fata de cadrele vecine (se semnaleaza de la 20 %)` / `the sky background jumps by +25 % against the neighbouring frames (flagged from 20 %)`.
14. Trails: `1 dara de satelit sau avion (Siril o scoate la stivuire)`, plural `3 dare de satelit sau avion (Siril le scoate la stivuire)` (the Romanian had `3 dara`) / `1 satellite or plane trail (Siril takes it out when stacking)`, `3 satellite or plane trails (Siril takes them out when stacking)`.
15. Unregistered: `nu s-a putut alinia (prea putine stele comune cu cadrul de referinta)` / `could not be aligned (too few stars in common with the reference frame)`.
`nu s-a putut citi: ...`, `dara la -90 grade` and `dara oblica` stay.

**Star cut-outs** (`FramesView.xaml`, `WindowTexts.CropsBusy`, `SeriesReport.CropCaption`, `Analysis/StarCrops.cs`)
16. Caption: `Alege un cadru din lista: aici apar cele mai luminoase stele ale lui, din centru si din cele patru colturi, marite, din canalul verde. Fiecare decupaj e luminat separat, ca sa se vada bine forma stelei.` / `Choose a frame in the list: its brightest stars appear here, from the centre and the four corners, enlarged, from the green channel. Each cut-out is brightened on its own, so the star's shape shows well.`
17. Busy note: `Se citeste cadrul pentru decupaje...` / `Reading the frame for the cut-outs...`.
18. Under each cut-out: `centru: latime (FWHM) 3.1 px, alungire 1.15, unghi 86 grade` / `centre: width (FWHM) 3.1 px, elongation 1.15, angle 86 degrees`; `centru, a doua stea` / `centre, second star`.

## Steps

1. Worktree and branch; this plan under `docs/plans/`.
2. `docs/requirements.md`: R-079 (the clause, the words added to `PlainWords`: prag, consecutiv,
   decodeaza, decoding), and the rows quoting the old reasons follow (R-022, R-033).
3. ADR 0034 "The Frames tab and the reasons are in plain words", citing ADR 0032.
4. Tests first, failing: `R079_TheReasonsAvoidTheMethodsVocabulary` (`SeriesTests`, both
   languages, every flag kind's `Reason`, the crop caption and the table's reasons over the
   pattern, classes, cloud and jump sessions as fixtures allow) and
   `R079_TheFramesTabUsesNoneOfTheMethodsWords` (`WindowLanguageTests`, the rendered
   `FramesView` with its tooltips and cut-outs, both languages).
5. The texts: `FramesView.xaml`, `WindowTexts.CropsBusy`, `SeriesReport.Reason` and
   `CropCaption`, `StarCrops` (`centre, second star`); doc comments quoting output follow.
6. Tests pinning old words follow R-079 (`SeriesAnalyzerTests` R033, `SeriesTests` R033); English
   scans gain the new Romanian stems; goldens `series`, `series-classes` and their `en_` twins
   recaptured on the RESPINSE lines and the table's reasons, numbers checked by script.
7. Docs: `series-analysis.md` (`Reason`'s words), README where it quotes reasons, CHANGELOG.
8. Build `--no-incremental -warnaserror`, gate, `/validate` (Report/ and Analysis/ touched),
   read the diff, commit, review offer, merge proposal.

## Verification

- R079 tests fail first, pass after; the whole gate green.
- Golden diffs: only the reasons' lines; numbers unchanged but for constants the texts name.
- Window render of the Frames tab with cut-outs in both languages looked at.
- `/validate`: no figure moves (V-008/V-011 as before while the solve is in `procesate\`).

## What deviated

- A third test pinned an old word: `MainViewModelTests.R030_TheRejectedTabListsTheBadFramesWithTheirReasons`
  checked that each rejected frame's reason holds `prag`; it now checks `(de la 1.60 cadrul e
  slab)`, the same threshold in the words of R-079.
- The README needed no change: it quotes only `trail on RA (-90 degrees)` and `trail at -90
  degrees`, which stay, and it describes the thresholds without quoting a reason.
- `docs/design/language.md` took the reasons' terms into its glossary and now says which tabs are
  rewritten; `series-analysis.md` took `Reason`'s new words as planned.
- The goldens changed on the reasons alone, four lines in `series` and six in `series-classes`
  and the same in their `en_` twins; no constant was added and every number is the same (script
  over the four goldens).
- The same-spot tooltip's `10 px` stays a literal in the XAML, as it was; it is
  `Th.CoverageLoose`.
