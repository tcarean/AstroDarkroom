# Explain the Summary tab in plain words

## Context

The owner asked on 2026-11-02 for every LightsCheck tab in plain words, one tab at a time (ADR
0032, rule "Plain words"). Dithering is merged (0e7f8265). The Rezumat (Summary) tab is next:
its seven cards (SESIUNE, REZUMAT, FOCUS, EXPUNERE SI ZGOMOT, ROUA, CADRE MISCATE, DRIFT SI
ROTATIE) are the console report's session lines and sections, so the console changes with it.
The owner approved the 25 Romanian proposals in chat on 2026-11-02 ("Da, si in consola"),
including "latimea stelelor (FWHM)" as in AstroPhotoAnalyzer. Card titles and chip words stay.

## Branch, numbers

- Branch `claude/Explain-the-Summary-tab-plainly`, worktree
  `LightsCheck/.claude/worktrees/Explain-the-Summary-tab-plainly`, from `main` (0e7f8265+).
- R-078 and ADR 0033 (main holds R-077 and ADR 0032; checked again before the merge).
- This plan saved as `docs/plans/2026-11-02-explain-the-summary-tab-plainly.md`.

## The texts (Romanian as approved; English to the same meaning)

Veil figures as examples; numbers come from the run and `Th` as now.

**SESIUNE** (`InventoryReport.WriteHeader`, `SeriesReport.ScaleLine`, the pixel sources in `SeriesRun`)
1. Lens. RO `135 mm f/2.8 (la fotografiere), obiectiv de 135 mm f/2.0, ID 74 (montura Nikon F)`, with a name `"EF50mm f/1.4 USM" (ID 198, montura Canon EF)`; EN `135 mm f/2.8 (when shot), a 135 mm f/2.0 lens, ID 74 (Nikon F mount)`, `"EF50mm f/1.4 USM" (ID 198, Canon EF mount)`.
2. Interval. RO `de la 2026-10-06 23:35:03 pana la 2026-10-07 02:57:49 (3 h 22 min); expunere totala 2 h 54 min, din 87 cadre (aparatul a expus 86 % din timp)`; EN `from ... to ... (3 h 22 min); total exposure 2 h 54 min, from 87 frames (the camera was exposing 86 % of the time)`.
3. Scale. RO `1 px = 6.11" pe cer; pixelul are 4.00 um (calculat din raw, din focala echivalenta 35 mm); cadrele sunt aliniate dupa cadrul 1`; sources `scris in fisier (XPIXSZ)`, `introdus de tine`, `valoarea pentru Nikon D3300, implicit`; no focal length `nu se stie (lipseste focala)`; no reference `cadrele nu s-au aliniat`. EN `1 px = 6.11" on the sky; the pixel is 4.00 um (worked out from the raw, from the 35 mm-equivalent focal length); the frames are aligned on frame 1`; `written in the file (XPIXSZ)`, `typed by you`, `the Nikon D3300's value, by default`; `not known (no focal length)`; `the frames were not aligned`.
4. Warning line. `(vezi lista cadrelor)` / `(see the list of frames)`.

**REZUMAT** (`SummaryRows`, `Focus`, `Exposure`, `Dew`, `PolarSummary`, `PolarBounds`, `NoUpperBound`, `PolarReason`, `Keep`)
5. Focus. RO `latimea stelelor (FWHM) e de obicei 3.12 px = 19.0" pe cadrele bune; 3.11 px la inceput si 3.11 px la sfarsit (+0 %): focusul a tinut toata sesiunea`, `focusul s-a schimbat`, `prea putine cadre bune ca sa se vada daca s-a schimbat`; EN `the stars' width (FWHM) is typically 3.12 px = 19.0" on the good frames; 3.11 px at the start and 3.11 px at the end (+0 %): the focus held the whole session`, `the focus changed`, `too few good frames to see whether it changed`.
6. Exposure. RO `cerul pe verde: 128 -> 171 ADU; cel mai slab e canalul R: shot/citire 9.4 (bine e de la 10), zgomotul de citire adauga 5 %`; `zgomotul de citire nu s-a putut masura (lipsesc bias-urile)`; `raportul shot/citire nu s-a putut masura (zona masurata e prea mica)`; `cerul nu s-a putut masura`. EN `the sky on green: 128 -> 171 ADU; the weakest is channel R: shot/read 9.4 (good from 10), the read noise adds 5 %`; `the read noise could not be measured (the biases are missing)`; `the shot/read ratio could not be measured (the measured area is too small)`; `the sky could not be measured`.
7. Dew. RO `latimea stelelor +0 %, lumina din jurul stelelor (halo) -0.015`; possible `latimea stelelor a crescut cu 18 %, iar lumina din jurul lor (halo) cu +0.031`. EN `the stars' width +0 %, the light around the stars (halo) -0.015`; `the stars' width rose by 18 %, and the light around them (halo) by +0.031`.
8. Polar alignment. RO `axa monturii e deviata de la pol cu cel putin 4.6'; asta muta stelele cu cel putin 0.40 px intr-o expunere (fara plate solve nu se stie declinatia, deci nici cat poate fi cel mult)`; bounds `cu 4.5' pana la 6.7'`, `cu 5.7'`; cost `cu cel mult 0.46 px`, `cu 0.39 pana la 0.46 px`; short session `sesiunea are sub 1 h, prea putin ca sa se afle cat poate fi cel mult`; reasons `nu se poate masura (sub 3 perechi de cadre la rand, fara pauza si fara miscare intre ele; 10 sau mai multe cadre la rand fara dithering ar ajunge)`, `- (cadrele nu s-au putut alinia)`, `- (nu se stie care axa e RA)`, `- (nu se stie scara)`, `- (nu se stie expunerea)`. EN `the mount's axis is off the pole by at least 4.6'; this moves the stars by at least 0.40 px over one exposure (without a plate solve the declination is not known, so neither is how large it can be)`; `by 4.5' to 6.7'`, `by 5.7'`; `by at most 0.46 px`, `by 0.39 to 0.46 px`; `the session is under 1 h, too short to find how large it can be`; `cannot be measured (under 3 pairs of frames in a row, with no pause and no move between them; 10 or more frames in a row without dithering would do)`, `- (the frames could not be aligned)`, `- (it is not known which axis is RA)`, `- (the scale is not known)`, `- (the exposure is not known)`.
9. Leave out. RO `22 cadre (lista e la RESPINSE); raman 65 cadre, adica 2 h 10 min de expunere`; EN `22 frames (the list is under REJECTED); 65 frames stay, that is 2 h 10 min of exposure`; one frame singular through `FramesWord`.

**FOCUS** (`FocusRows`)
10. `latimea stelelor (FWHM) : 3.12 px = 19.0" (de obicei, pe cadrele bune)`; EN `the stars' width (FWHM) : 3.12 px = 19.0" (typically, on the good frames)`.
11. `primele / ultimele 10 cadre bune : 3.11 px / 3.11 px (+0 %): focusul a tinut` (`focusul s-a schimbat`); EN `first / last 10 good frames : 3.11 px / 3.11 px (+0 %): the focus held` (`the focus changed`).
12. `in centru / la mijloc / la margine : latimea stelelor 3.18 / 3.16 / 2.97 px (de obicei, pe cadrele bune)`; EN `in the centre / in the middle / at the edge : the stars' width 3.18 / 3.16 / 2.97 px (typically, on the good frames)`. The verdict row is item 5's text.

**EXPUNERE SI ZGOMOT** (`NoiseRows`)
13. `fondul cerului (cadrele bune, primele -> ultimele) : 128 -> 171 ADU pe verde, peste nivelul de negru (+33 %)`; EN `sky background (good frames, first -> last) : 128 -> 171 ADU on green, above the black level (+33 %)`.
14. `zgomot de citire : R 3.3  G1 2.9  G2 2.9  B 3.3 ADU (zgomotul pe care il adauga senzorul la fiecare cadru, masurat din doua bias-uri)`, `nu s-a putut masura (trebuie cel putin doua bias-uri in biases\)`; EN `read noise : ... ADU (the noise the sensor adds to every frame, measured from two biases)`, `could not be measured (at least two biases are needed in biases\)`.
15. Frame rows: `zgomot in plus 5.2 %` / `extra noise 5.2 %` for `penalizare` / `penalty`.
16. Legend: label `ce inseamna` / `what it means` (no longer `scara`); `shot/citire de la 10 in sus: cerul acopera zgomotul de citire, care adauga sub 5 %; sub 10: canalul sufera de zgomotul de citire, iar expuneri mai lungi ar ajuta`; EN `shot/read from 10 up: the sky covers the read noise, which adds under 5 %; under 10: the channel suffers from the read noise, and longer exposures would help`.

**ROUA** (`DewRows`)
17. `halo (lumina din jurul stelelor), primele / ultimele : 0.027 / 0.012; stele gasite 15568 -> 12418 (-20 %); latimea stelelor 3.11 / 3.11 px (+0 %)`; EN `halo (the light around the stars), first / last : ...; stars found ...; the stars' width 3.11 / 3.11 px (+0 %)`. The verdict row is item 7's text.

**CADRE MISCATE** (`TrailRows`)
18. `slabe (alungire de cel putin 1.60)` / `bad (elongation of at least 1.60)`.
19. `la limita (alungire intre 1.35 si 1.60)` / `marginal (elongation between 1.35 and 1.60)`.
20. `87 grade fata de orizontala, pe cadrele slabe, adica pe RA; ...`, `nu se stie (prea putine cadre slabe cu dare in aceeasi directie)`; EN `87 degrees from the horizontal, on the bad frames, that is on RA; ...`, `not known (too few bad frames with trails in the same direction)`.

**DRIFT SI ROTATIE** (`DriftRows`, `PairsWord`, `DriftPerExposure`, `Streak`, `PolarRow`, `RaDriftRow`)
21. Label `drift intre cadre la rand, fara dithering` / `drift between frames in a row, without dithering`; value `X -0.20 px/min, Y -0.73 px/min = -1.2 / -4.5 "/min (din 36 perechi de cadre fara pauza intre ele)`, approximate `(din 29 perechi de cadre cu pauza; aproximativ, fiindca in cifra intra si miscarile mici de dithering)`; EN `(from 36 pairs of frames without a pause between them)`, `(from 29 pairs of frames with a pause; approximate, since the small dither moves count in the figure)`.
22. `1.52 px = 9.3" in 120 s, pe RA; ...` / `1.52 px = 9.3" in 120 s, along RA; ...`; `- (nicio pereche de cadre la rand fara dithering)` / `- (no pair of frames in a row without dithering)`; `nu se stie dara de walking noise (driftul nu s-a putut masura)` / `the walking-noise streak is not known (the drift could not be measured)`.
23. `imaginea s-a rotit cu 8.5' in total, 2.5' pe ora; din cauza asta, o stea din colt se misca 0.09 px intr-o expunere`; EN `the image turned by 8.5' in all, 2.5' an hour; because of this, a star in the corner moves 0.09 px over one exposure`.
24. Polar row. RO `axa monturii e deviata de la pol cu cel putin 4.6' (aflat din driftul pe DEC, 1.2"/min; fara plate solve nu se stie declinatia, deci nici cat poate fi cel mult); din cauza asta stelele se misca cel putin 0.40 px intr-o expunere de 120 s`; with a solve `... cu 4.5' pana la 6.7' (aflat din driftul pe DEC, 1.2"/min, si din rotatia imaginii, 1.1'/h fara dithering, la DEC +31.6 grade); din cauza asta stelele se misca cel mult 0.46 px intr-o expunere de 120 s (sigur 0.39 px), iar alungirea pe care o produce e cel mult 1.00 (1 = rotund), la o latime a stelelor (FWHM) de 3.12 px`. EN `the mount's axis is off the pole by at least 4.6' (found from the DEC drift, 1.2"/min; without a plate solve the declination is not known, so neither is how large it can be); because of this the stars move at least 0.40 px over an exposure of 120 s`; `... (found from the DEC drift, 1.2"/min, and from the image's rotation, 1.1'/h without dithering, at DEC +31.6 degrees); because of this the stars move at most 0.46 px over an exposure of 120 s (0.39 px for certain), and the elongation it makes is at most 1.00 (1 = round), at a star width (FWHM) of 3.12 px`.
25. RA drift. RO `4.5"/min (fara plate solve nu se stie cat din el vine din aliniere)`; `4.4"/min, stelele spre est; din aliniere vine cel mult 0.7"/min, restul vine din urmarire (viteza, eroarea periodica, flexiune)`; `: poate veni tot din aliniere`; `sesiunea are sub 1 h: nu se stie inca cat vine din aliniere`. EN `4.5"/min (without a plate solve it is not known how much of it comes from the alignment)`; `...; at most 0.7"/min comes from the alignment, the rest comes from the tracking (rate, periodic error, flexure)`; `: it may all come from the alignment`; `the session is under 1 h: it is not known yet how much comes from the alignment`.

## Steps

1. Worktree and branch; this plan under `docs/plans/`.
2. `docs/requirements.md`: R-078 (the clause for the Summary tab and the console's session lines
   and sections, the words added to the banned list: penalty, penalizare, coherent, coerent(a),
   integration, integrare, arcmin, mediane, medians; the renamed terms); the rows quoting the
   old words follow (R-023, R-036, R-043, R-049, R-050, R-065, R-066).
3. ADR 0033 "The Summary tab is in plain words", citing ADR 0032's rule.
4. Tests first, failing: `R078_TheSummaryTextsAvoidTheMethodsVocabulary` (`SeriesTests`, both
   languages, the session header, `ScaleLine` and the six row producers over the pattern, the
   classes session (unread frame, no RA axis), the drifting and the solved session) and
   `R078_TheSummaryTabUsesNoneOfTheMethodsWords` (`WindowLanguageTests`, the rendered
   `SummaryView` in both languages); `PlainWords` gains the new words (Dithering still passes).
5. The texts: `InventoryReport.cs` (lens, interval, warning), `SeriesRun.cs` (pixel sources),
   `SeriesReport.cs` (the producers above, a separate legend label instead of `ScaleLabel`, a
   first/last formatter for item 11); doc comments quoting output follow.
6. Tests pinning old words follow R-078: `InventoryReportTests` (lens lines and the comment on
   `as shot`), `AcceptanceTests` (interval line), `PixelPitchTests`, `SeriesTests` (R036 FOCUS
   verdict, R026 pixel source, R033 `= pe RA`, R049, R050); English scans gain the new
   Romanian stems. Goldens: `inventory`, `series`, `series-classes` and their `en_` twins
   recaptured; every changed line tied to items 1-25; numbers checked unchanged by script.
7. Docs: `language.md` glossary, `series-analysis.md` (`PairsWord` words),
   `session-and-console.md` if it quotes the header, README (the `--inventory` example's lens and
   interval lines re-run on Veil, the polar quotes, the section bullets' method words, and the
   leftover `coverage per radius` of the Dithering bullet), CHANGELOG.
8. Build `--no-incremental -warnaserror`, gate, `/validate` (Report/ touched; V-008/V-011 expected
   to fail on the missing solve as on 2026-11-02, nothing else), read the diff, commit, review
   offer, merge proposal.

## Verification

- R078 tests fail first, pass after; the whole gate green.
- Golden diffs: only the session lines and the Summary sections' lines, numbers unchanged.
- Window renders of the Summary tab in both languages looked at.
- `/validate`: no figure moves.

## What deviated

- The English lens line has no article (`135 mm f/2.0 lens`, not `a 135 mm f/2.0 lens`): the
  article would depend on how the number is said (`an 11-20 mm lens`); R-065 quotes it so.
- Counts of one take the singular where the approved list showed plurals: `1 pereche de cadre` in
  the drift pairs, `ramane 1 cadru` / `1 frame stays` in the leave-out row, `din 1 cadru` in the
  interval.
- The goldens gained three constants the approved texts name: the `10` of `(bine e de la 10)`,
  the second `10` of the legend's `sub 10`, and the `1` of `1 px =`; no measured figure moved
  (script over the six goldens). The English RA drift line was already the approved wording, so
  `en_series` changes on one line fewer than `series`.
- The README's section bullets for FOCUS, EXPOSURE AND NOISE, DEW, TRAILED FRAMES and DRIFT took
  the same plain words, and its Dithering window bullet lost the old `coverage per radius`.
