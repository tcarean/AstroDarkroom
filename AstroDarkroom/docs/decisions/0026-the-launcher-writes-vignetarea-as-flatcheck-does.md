# 0026. The launcher writes `vignetarea`, as FlatCheck does

Date: 2026-11-14
Status: accepted 2026-11-14

## Context

The code review of 2026-11-13 found one Romanian word spelled two ways in the launcher (CR-004):
the row of FlatCheck in the main window said `vinietarea fata de profilul obiectivului`
(`AppCatalogue`, quoted in R-001 and pinned by its test), and the fourth paragraph of "Despre"
`compara vignetarea flat-ului tau` (`Prerequisites`). FlatCheck, which that row opens, writes
`vignetar-` in its own window: on 2026-11-14, 53 lines of `FlatCheck.Core` and `FlatCheck.Wpf`
hold it and none holds `vinietar-`. Which spelling stays was the owner's call; asked on
2026-11-14 with `vignetarea` recommended, the owner chose it.

The row's description must take at most two lines of its 416 px column (ADR 0017). Measured on
2026-11-14 with WPF's own text layout (`FormattedText`, Display mode, Segoe UI 12, in a
throw-away program beside the real window shown off the screen): with `vignetarea` the
description takes two lines at 100 %, 125 % and 150 % scaling, and the narrowest width that
keeps it on two lines at 100 % is 386 px, as with `vinietarea`.

## Decision

The launcher writes `vignetarea` wherever it names the effect in Romanian: the description of
FlatCheck in `AppCatalogue`, R-001 and its test follow the paragraph of "Despre" and FlatCheck's
own texts. The English wordings (`vignetting`) do not change.

## Alternatives rejected

- **`vinietarea` in both places.** The paragraph of "Despre" would change instead, and the
  launcher would still spell the word otherwise than the tool the row opens, whose window the
  user reads next.
- **Leaving both.** One window, two spellings of one word; the review listed it as a wrong text.

## Consequences

R-001 quotes the new wording and `R001_Catalogue_ListsTheSixAppsInWorkflowOrderWithTheirTexts`
pins it; nothing pins the word in `Prerequisites`, where a later edit has to keep it by hand.
A new Romanian text of the launcher that names the effect writes `vignetar-`.
