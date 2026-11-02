# Explain the Dithering tab in plain words

## Context

On 2026-11-02 the owner found the Dithering tab's texts too complicated and asked for texts
anyone can understand, every tab in turn, Dithering first, with the original and the proposal
side by side for approval; and that every text written for the user, and every explanation to
the owner in chat, keep that register from now on, written down as a rule and a decision. The
same request came for AstroPhotoAnalyzer (its ADR 0071) and FlatCheck (its ADR 0082). The 30
Romanian proposals were approved in chat on 2026-11-02 ("Da, si in consola"): the console's
DITHERING section and the Summary rows that share the same pieces change with the window, so
the two applications keep one voice.

One content fix rides along: the explanation's paragraph 2 said a 3"/min drift "stays under
one pixel over the whole session" at 20 mm. It is under a pixel per exposure, not per session
(Smida, 20 mm, measured 0.17 px per exposure, V-013). The new sentence gives computed figures
for the D3300's 3.92 um pixel: 3"/min x 2 min = 6", that is 1.0 px at 5.99 "/px (135 mm) and
0.15 px at 40.4 "/px (20 mm).

## Branch, numbers

- Branch `claude/Explain-the-Dithering-tab-plainly`, worktree
  `LightsCheck/.claude/worktrees/Explain-the-Dithering-tab-plainly`, from `main`.
- R-077 and ADR 0032, the next free numbers on `main`. (The plan first named R-078 and ADR
  0033, as the branch `claude/Print-LibRaws-notice-in-LightsCheck` had taken R-077 and ADR 0032;
  the owner stopped that branch and it was deleted unmerged, so the numbers are free.)
- The approved plan is saved as `docs/plans/2026-11-02-explain-the-dithering-tab-plainly.md`.

## The texts (Romanian as approved; English to the same meaning)

Numbers come from `Th` as now; examples are the golden's synthetic eight-frame session.

**Window only** (`Views/DitherView.xaml`, `ViewModels/WindowTexts.cs`)
1. Map caption. RO: Fiecare punct e un cadru, pus acolo unde a stat fata de primul cadru. O linie il leaga de cadrul urmator, iar numarul lui e scris langa el. Culoarea arata cum e cadrul: verde bun, galben la limita, rosu slab. Doua cadre aflate la 10 px sau mai mult unul de altul nu-si mai pun pixelii calzi in acelasi loc, asa ca acestia nu se mai aduna in walking noise. EN: Each dot is a frame, placed where it sat relative to the first frame. A line joins it to the next frame, and its number is written beside it. The colour shows how the frame is: green good, yellow marginal, red bad. Two frames 10 px or more apart no longer put their hot pixels in the same place, so these no longer add up into walking noise.
2. Ring note. RO: Un cerc gol in loc de punct plin: cadrul e intr-un grup de cel putin 3 cadre la rand care au stat in acelasi loc (la mai putin de 10 px de primul din grup). Din astfel de grupuri vine walking noise-ul. In tabul Cadre, coloana 'acelasi loc' spune al catelea e cadrul in grupul lui. EN: A hollow circle instead of a solid dot: the frame is in a run of at least 3 frames in a row that stayed in the same spot (less than 10 px from the first of the run). Walking noise comes from such runs. On the Frames tab, the 'same spot' column says which one of its run the frame is.
3. Step chart heading. RO: CAT S-A MUTAT FATA DE CADRUL DINAINTE. EN: HOW FAR IT MOVED FROM THE FRAME BEFORE.
4. Step chart caption. RO: Bara galbena: o miscare de dithering, adica cadrul s-a mutat cu cel putin 5 px. Bara gri: nicio miscare; cadrul a alunecat doar putin, cat driftul. EN: Yellow bar: a dither move, that is, the frame moved by at least 5 px. Grey bar: no move; the frame only slid a little, by the drift.
5. Guide line `10 px (walking noise)` becomes a pair: RO `10 px: alt loc`, EN `10 px: another spot`; `miscare >= 5 px` stays.
6. Nearest-frame caption. RO: Cat de aproape e fiecare cadru de cel mai apropiat alt cadru din sesiune, oricand ar fi fost facut acela. Sub 10 px, cele doua isi pun pixelii calzi in acelasi loc. EN: How close each frame is to the nearest other frame of the session, whenever that one was taken. Under 10 px, the two put their hot pixels in the same place.

**Rows, window and console** (`Report/SeriesReport.cs`, `Analysis/SeriesAnalyzer.cs`)
7. RO `pauze de peste 5 s : 3; miscari de cel putin 5 px: 3; pauze dupa care cadrul a ramas pe loc: 0; miscari fara pauza inainte: 0; cadre aliniate: 8 din 8`; EN `pauses over 5 s : 3; moves of at least 5 px: 3; pauses after which the frame stayed put: 0; moves without a pause before: 0; frames aligned: 8 of 8`.
8. RO `cel mai lung sir fara miscare : cadrele 1-2 (2 cadre la rand, 4 min)`; EN `longest stretch without a move : frames 1-2 (2 frames in a row, 4 min)`.
9. RO `apropierea cadrelor : jumatate din cadre au alt cadru la cel mult 0.6 px; cadre singure (la 10 px sau mai mult de oricare altul): 0`; EN `how close the frames are : half the frames have another frame within 0.6 px; frames alone (10 px or more from any other): 0`.
10. RO `zona pozitiilor : 40 px pe X, 16 px pe Y`; EN `area of the positions : 40 px on X, 16 px on Y`.
11. `AxisLine` and `TrailSource`. RO `RA e axa Y a imaginii, DEC axa X (aflat din directia darelor: presupunem ca le-a facut urmarirea, deci sunt pe RA)`, `(aflat din plate solve (WCS))`, unknown `nu se stie care axa e RA`; EN `RA is the image's Y axis, DEC the X axis (found from the direction of the trails: we assume the tracking made them, so they lie on RA)`, `(found from plate solve (WCS))`, `it is not known which axis is RA`. Also on the Summary rows `cadre miscate` and `directia darelor`.
12. RO `1 px = 5.99" pe cer; ca sa muti cadrul 5 px (cea mai mica miscare numarata) trebuie 30" pe cer, iar pentru 10 px, 60"`; EN `1 px = 5.99" on the sky; to move the frame 5 px (the smallest move counted) takes 30" on the sky, and 10 px, 60"`.
13. RO `in stiva ramane 25.0 % din pattern-ul unui cadru (ideal 12.5 %, cu 8 cadre; 4 perechi de cadre au stat la sub 1.5 px una de alta, iar pattern-ul lor se aduna)`, none `- (cadrele nu s-au putut alinia)`; EN `25.0 % of one frame's pattern stays in the stack (ideally 12.5 %, with 8 frames; 4 pairs of frames sat under 1.5 px from each other, and their pattern adds up)`, `- (the frames could not be aligned)`.
14. RO `tarie 0.0 ADU pe R (0 % din zgomotul unui cadru), 0.0 pe G1 (0 %), ...; 0 pixeli calzi la un milion de pixeli, adica cei de peste 10 ori zgomotul unui cadru (din 8 cadre, pe 480 x 480 px din centru); fara dark-uri in sesiune, pattern-ul ramane in cadre`; darks `3 dark-uri in darks\, cu care Siril scoate pattern-ul`; `nu se poate masura (mai putin de 7 cadre masurate | cadrele nu s-au putut alinia | cadrele au stat prea mult pe loc si stelele nu se pot desparti de pattern; cel mai mare grup in acelasi loc (la 3 px) are 12 cadre din 32 | niciun cadru citit)`. EN `strength 0.0 ADU on R (0 % of one frame's noise), 0.0 on G1 (0 %), ...; 0 hot pixels per million pixels, that is, those above 10 times one frame's noise (from 8 frames, on 480 x 480 px from the centre); no darks in the session, the pattern stays in the frames`; `3 darks in darks\, with which Siril takes the pattern out`; `cannot be measured (fewer than 7 frames measured | the frames could not be aligned | the frames stayed in place too much and the stars cannot be told apart from the pattern; the largest group in the same spot (within 3 px) has 12 frames of 32 | no frame read)`.
15. Verdict text (chip words unchanged; also the Summary `dithering` row). RO `dare de walking noise de cel mult 0.5 px: driftul e de 0.50 px intr-o expunere; cadrele 1-2, 2 cadre la rand in acelasi loc, la 10 px; 3 miscari la 8 cadre, 4 locuri diferite (la 10 px), 0 cadre singure, 0 miscari pe ambele axe`; hot `pixeli calzi: mai mult de 3 cadre la rand in acelasi loc (la 3 px)`; no run `niciodata 2 cadre la rand in acelasi loc, la 10 px`; `driftul nu s-a putut masura`; `cadrele nu s-au putut alinia`. EN `walking-noise streaks of at most 0.5 px: the drift is 0.50 px over one exposure; frames 1-2, 2 frames in a row in the same spot, within 10 px; 3 moves over 8 frames, 4 different spots (within 10 px), 0 frames alone, 0 moves on both axes`; `hot pixels: more than 3 frames in a row in the same spot (within 3 px)`; `never 2 frames in a row in the same spot, within 10 px`; `the drift could not be measured`; `the frames could not be aligned`. `StretchWord` and `NoRun` also reach the DRIFT row: `(cadrele 1-2, 2 cadre la rand in acelasi loc, la 10 px)`. One punctuation detail against the approved list: "(la 10 px)" of the no-run words becomes ", la 10 px", as in `StretchWord`, since the DRIFT row puts them inside parentheses already.

**Tables, window only** (titles are console labels too)
16. `cat de des`: `o miscare la fiecare | de cate ori`; EN `a move every | times`.
17. `cat de mult`: `cat de mare | cate miscari`, first row `de obicei`; EN `how big | how many moves`, `typically`.
18. `directie`: `axa | doar pe ea | spre + | spre -`, row `pe ambele`; EN `axis | on it only | towards + | towards -`, `on both`.
19. `apropierea cadrelor`: `distanta | cu alt cadru mai aproape | locuri diferite | grupuri de 3+ cadre la rand`; EN `how close the frames are`: `distance | with another frame closer | different spots | runs of 3+ frames in a row`.
20. `grupuri in acelasi loc`: `distanta | cadrele | cate | cat timp`; EN `runs in the same spot`: `distance | frames | how many | how long`.

**Console rows that are tables in the window**
21. `cat de mult : de obicei 15.32 px = 91.7"; ...`; EN `typically ...`.
22. `directie : doar pe X: 2; doar pe Y: 1; pe ambele: 0; pe X spre + / spre -: 2 / 0; pe Y spre + / spre -: 0 / 1; <axis line>`; EN `on X only: 2; on Y only: 1; on both: 0; on X towards + / towards -: 2 / 0; ...`.
23. `apropierea cadrelor : jumatate din cadre ...; cu alt cadru la sub 3 px: 8, la sub 5: 8, la sub 10: 8; cadre singure (...)`; EN `...; with another frame under 3 px: 8, under 5: 8, under 10: 8; frames alone (...)`.
24. `locuri diferite : 4 la 5 px, 4 la 10 px; zona pozitiilor 40 px pe X, 16 px pe Y`; EN `different spots : 4 within 5 px, 4 within 10 px; area of the positions 40 px on X, 16 px on Y`.
25. `grupuri sub 5 px`, `grupuri sub 10 px`; EN `runs under 5 px`, `runs under 10 px`. `cat de des` stays.

**Explanation** (`DitherExplanation`, window only), the five approved paragraphs, the third bold. EN:
26. Walking noise means fine streaks, all in the same direction, in the stacked image. The sensor's fixed pattern makes them: the hot pixels and the faint marks the sensor always has in the same place, that is, what a dark takes out. The mount slides a little from one frame to the next (the drift). When the stack aligns the frames on the stars, the pattern moves with every frame, and its copies line up into a streak. This happens in the runs of frames in a row that stayed in the same spot (less than 10 px).
27. The longer the focal length, the more pixels the same slide of the mount and the same dither move make. A drift of 3"/min moves the frame about 1 px in a 2-minute exposure at 135 mm, but only 0.15 px at 20 mm. That is why a short focal length forgives, and a long one asks for a dither after every frame.
28. What stops walking noise: move the mount after every frame, on both axes, each time in another direction, at random. How often you move matters more than how far: 5 px are enough, as long as two neighbouring frames do not stay in the same spot. A big jump every 2 frames leaves pairs of frames in the same spot, and in them the drift draws short streaks.
29. Darks take the pattern out before it gets to make streaks. You do not need to take the filter off the lens: take the lens off with the filter on it, put the body cap on, cover the viewfinder's eyepiece and shoot 20 frames at the same ISO, with the same exposure time and at the same temperature, at the end of the session. The flats stay good.
30. Where to find the figures: the 'drift over one exposure' row of DRIFT AND ROTATION (the Summary tab) says how far a frame slides in one exposure and how long the longest walking-noise streak is. Under 3 px the streak does not show, whatever the map looks like. The 'pattern in the stack' row says how much of one frame's pattern stays after stacking; the ideal is 1 divided by the number of frames, when no frame sits on another. The 'fixed pattern' row says how strong the sensor's pattern is against one frame's noise and whether you have darks to take it out.

## Steps

1. Worktree and branch; save this plan under `docs/plans/`.
2. `docs/requirements.md`: R-077, the plain-words clause for the Dithering tab and the console's
   DITHERING section, with its banned words and the renamed terms; the rows that quote the old
   words follow (R-025, R-036, R-039, R-043, R-044, R-045 with paragraph 2's figures, R-046,
   R-048).
3. ADR 0032 "Every text for the user is in plain words, the Dithering tab first" (context,
   decision, the 20 mm correction with its arithmetic, alternatives: window only, a gloss, the
   20 mm sentence kept); the rule "Plain words" in `CLAUDE.md`, citing it.
4. Tests first, failing: `R077_TheDitheringTextsAvoidTheMethodsVocabulary` in
   `tests/LightsCheck.Tests/SeriesTests.cs` (both languages by a bool; `DitherRows`,
   `DitherScalarRows`, `DitherTables`, `DitherExplanation` over the eight-frame pattern, a
   four-frame session, the darks session and the runs session; whole words, case-insensitive:
   median, mean, sigma, Mpx, registered, unregistered, correlated, distinct, isolated,
   coverage, radius, consecutive, sign, span, threshold, and mediana, medie, inregistrat(e),
   neinregistrat(e), corelat, distincte, izolate, acoperire, raza, fierbinti, consecutive,
   semn, intindere, pragul), and `R077_TheDitheringTabUsesNoneOfTheMethodsWords` in
   `tests/LightsCheck.Wpf.Tests/WindowLanguageTests.cs` (the rendered `DitherView` in each
   language, its texts plus the guide labels, map labels, grid note and bar tips, same list).
   The list lives once, in a small test helper linked into the window's tests as the fixtures
   are.
5. The texts: `SeriesReport.cs` (rows, labels, tables, verdict, `AxisLine`, `Streak`,
   `StretchWord`, `NoRun`, `ScaleRow`, `SurvivingRow`, `PatternRow`, `MeasuredPattern`,
   `DitherExplanation`), `SeriesAnalyzer.TrailSource`, `DitherView.xaml`,
   `WindowTexts.WalkingNoiseGuide` (now a `T` pair); doc comments that quote output follow.
6. Tests that pin the old words follow R-077: `SeriesTests` (R036, R044, R045, R046, R048),
   `MainViewModelTests` (direction table); the English scans get the new Romanian stems
   (`aliniat`, `singure`, `locuri`, `pozitiilor`, `tarie`, `apropier`, `dungi`, `MUTAT`,
   `DINAINTE`, `milion`).
7. Goldens `series`, `en_series`, `series-classes`, `en_series-classes` recaptured
   (`LIGHTSCHECK_UPDATE_GOLDEN=1`, `--filter GoldenOutputTests`); every changed line read and
   tied to items 7-25; a script checks that the numbers of the removed and added lines are the
   same multiset per file.
8. Docs: `language.md` glossary (registered, coverage, distinct positions, span, isolated,
   hot pixels), `series-analysis.md` and `window.md` where they quote the words, `README.md`
   (the DITHERING bullet's words and the two walking-noise paragraphs the window "says the
   same" as), `CHANGELOG.md` under Changed.
9. `dotnet build --no-incremental -warnaserror --no-restore`, then `scripts/verify.ps1`; read the
   whole diff; commit on the branch; offer the subagent review, then propose the merge.

## Verification

- The two R077 tests fail before step 5 and pass after; the whole gate green.
- The goldens' changed lines are only the Summary rows `cadre miscate`, `dithering`, TRAILED
  FRAMES `directia darelor`, DRIFT `drift intr-o expunere` and the DITHERING rows, in both
  languages, every number unchanged (script output pasted at wrap-up).
- `LIGHTSCHECK_DUMP_SCREENS` renders of the Dithering tab in both languages, looked at for the
  longer captions and the wrapped table headers.
- No formula or threshold changes, so no `/validate` run (the rule "Measured on the sessions").

## What deviated

- Numbers: R-077 and ADR 0032, not R-078 and ADR 0033 (see "Branch, numbers").
- `SurvivingRow` words its pairs by their count (`SharingPairsWords`): the approved plural for two
  or more, `1 pereche de cadre a stat ... iar pattern-ul ei se aduna` for one, and `nicio pereche
  de cadre n-a stat la sub 1.5 px una de alta` for none, since "0 perechi ..., iar pattern-ul lor
  se aduna" would say something false; R-048 quotes both.
- The longest-stretch row counts its frames with `FramesWord`, so a stretch of one frame reads
  `1 cadru la rand` (`1 frame in a row`), not `1 cadre`.
- The README's DITHERING bullet, its walking-noise paragraphs and its verdict list took the same
  plain words, keeping their measured examples (Veil, Smida); `language.md` names the new test
  and covers `PlainWords.cs`.
- `/validate` ran after all: `docs/skills.md` counts `Report/` and `Analysis/` as numeric code
  for the wrap-up, and this task edited texts in both. 10 of 13 passed. V-010 records the
  Dithering tables with their old words (`acoperire`, `median`, `ambele`); a console run on Veil
  printed all its figures unchanged, and at the owner's word its three words became the new
  ones. V-008 and V-011 failed because the Veil root's solved `result.fit` moved to
  `procesate\` on 2026-10-27, not because of this task; the owner left them so
  (`docs/validation.md`, History).
- The console run on Veil showed `1 cadre singure` in the verdict: the verdict's four counts now
  take the singular for one (`Count`: `1 cadru singur`, `1 miscare`, `1 loc`), R-077 says so, and
  `R077_...` checks it on a session with one frame alone.
- R-048's Veil example said 27 pairs at 1.6 %, which the formula puts at 1.9 %; the run measured
  16 pairs and 1.6 %, and the example now says 16.
- Found, not changed: the how-far table cuts its first value (`15.32 px = 91.7"` shows as
  `15.32 px = 91.`), its cells being 100 px wide; the old `median` row was cut the same way.
