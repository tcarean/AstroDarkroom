# Explain the Quality tab in plain words

## Context

The owner asked on 2026-11-02 for every LightsCheck tab in plain words, one tab at a time (ADR
0032, rule "Plain words"); Dithering (0e7f8265), Summary (472bfb7b) and Frames (1ed22fbe) are
merged. The Calitate (Quality) tab is next: four charts, each with a heading and a note. Its texts
live only in the window (`src/LightsCheck.Wpf/Views/QualityView.xaml`); the console has no such
section, so no golden changes and no core change. The owner approved the 8 rewrites and the one
new line above the charts in chat on 2026-11-02 ("Merg pe recomandarile tale"). The guide labels
on the elongation chart (the class words, `SeriesReport.ClassWord`) and the axis numbers stay.

## Branch, numbers

- Branch `claude/Explain-the-Quality-tab-plainly`, worktree
  `LightsCheck/.claude/worktrees/Explain-the-Quality-tab-plainly`, from `main` (1ed22fbe).
- R-080 and ADR 0035 (main holds R-079 and ADR 0034; checked again before the merge).
- This plan saved as `docs/plans/2026-11-02-explain-the-quality-tab-plainly.md`.

## The texts (Romanian as approved; English to the same meaning)

1. Heading 1: `LATIMEA STELELOR (FWHM, px)` / `THE STARS' WIDTH (FWHM, px)` (was `FWHM MEDIAN (px)`).
2. Note 1: `Cat de late sunt stelele in fiecare cadru, de obicei. Ies mai late cand se pierde focusul sau cand aerul e agitat (seeing); un cadru miscat iese si el mai sus, fiindca stelele lui sunt intinse.` / `How wide the stars are in each frame, typically. They come out wider when the focus slips or the air is unsteady (seeing); a trailed frame stands higher too, since its stars are stretched.`
3. Heading 2: `CAT DE ALUNGITE SUNT STELELE` / `HOW ELONGATED THE STARS ARE` (was `ALUNGIREA MEDIANA`).
4. Note 2: `1 = stele rotunde. Peste linia punctata galbena cadrul e la limita; peste cea rosie e slab si iese din stiva.` / `1 = round stars. Above the yellow dotted line the frame is marginal; above the red one it is bad and leaves the stack.` No threshold figures: they would be literals in the XAML, while the lines come from `Th`.
5. Heading 3: `STELE GASITE` / `STARS FOUND` (was `STELE DETECTATE`; the words of the DEW row).
6. Note 3: `Cate stele s-au gasit in fiecare cadru. Scad cand trec nori sau ceata si cand se pune roua pe obiectiv; scad putin si cand cerul se lumineaza.` / `How many stars were found in each frame. They drop when clouds or haze pass and when dew forms on the lens; they drop a little when the sky brightens too.`
7. Heading 4: `FONDUL CERULUI, PE VERDE (ADU peste nivelul de negru)` / `THE SKY BACKGROUND, ON GREEN (ADU above the black level)` (was `... (ADU peste black)`).
8. Note 4: `Cat de luminos e cerul in fiecare cadru. Urca atunci cand campul coboara spre orizont, cand rasare luna sau din cauza poluarii luminoase; un salt pe un singur cadru vine de la o lumina trecatoare sau de la o expunere diferita.` / `How bright the sky is in each frame. It rises as the field sinks toward the horizon, when the moon rises, or with light pollution; a jump on a single frame comes from a passing light or a different exposure.`
9. New, above the four charts: `Fiecare punct e un cadru, in ordinea fotografierii (numerele de jos). Pe primele doua grafice culoarea spune clasa cadrului: verde bun, galben la limita, rosu slab.` / `Each point is a frame, in the order they were shot (the numbers along the bottom). On the first two charts the colour gives the frame's class: green good, yellow marginal, red bad.` (The FWHM and elongation points take `MainViewModel.ClassColour`; the other two charts one colour each.)

## Steps

1. Worktree and branch; this plan under `docs/plans/`.
2. `docs/requirements.md`: R-080 after R-079 (the clause, the new line, an example).
3. ADR 0035 "The Quality tab is in plain words", citing ADR 0032: the words, the new line and
   why the elongation note names no figure.
4. Test first, failing: `R080_TheQualityTabUsesNoneOfTheMethodsWords` in `WindowLanguageTests`,
   built as `R077_`/`R078_` there: renders `QualityView` with the pattern session in both
   languages, holds every text and the elongation guides' labels against `PlainWords`, and
   checks the new line is there (starts `Fiecare punct e un cadru` / `Each point is a frame`).
   It fails now on `MEDIAN`.
5. The texts in `QualityView.xaml`: the eight replaced, the new `TextBlock` (style `Muted`,
   wrapping) above the first card.
6. The English scan of `WindowLanguageTests` gains the new Romanian stems (intinse, galbena,
   rosie, rasare, poluar, trecatoare, punctata, numerele, culoarea; none is inside an English
   word the window writes).
7. Docs: `docs/design/language.md` (which tabs are rewritten, the Tests paragraph names the
   Quality tab), README's Quality bullet in the new words, CHANGELOG (Changed), the plan's
   "What deviated".
8. Build `--no-incremental -warnaserror`, render the Quality tab in both languages and look at
   it, gate, read the diff, commit, merge proposal. No `/validate`: neither `Report/` nor
   `Analysis/` changes, and no golden.

## Verification

- R080 fails first on `MEDIAN`, passes after; the whole gate green.
- `git status` on `tests/LightsCheck.Tests/Golden/` stays clean.
- The Quality tab rendered in both languages (`LIGHTSCHECK_DUMP_SCREENS`) and looked at: the new
  line wraps, the headings fit.
- `R063_EveryScreenIsInEnglishWithLangEn` passes with the new stems.

## What deviated

- The worktree came from `main` at 785888b4, a DarksCheck merge after 1ed22fbe; LightsCheck's
  files are the same.
- The four notes took `TextWrapping="Wrap"`: the new ones are longer, and the old text blocks did
  not wrap, so a narrow window would have cut them.
- `R080_TheQualityTabUsesNoneOfTheMethodsWords` failed first on `MEDIANA` (in `ALUNGIREA
  MEDIANA`), the walk meeting it before `MEDIAN`.
- The glossary of `docs/design/language.md` gained `cat de alungite sunt stelele` and `stele
  gasite`.
