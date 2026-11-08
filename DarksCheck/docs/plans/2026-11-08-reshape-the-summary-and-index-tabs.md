# Reshape the Summary and Index tabs, read every light, forget the partner

Branch `claude/Reshape-the-Summary-and-Index-tabs`, worktree
`DarksCheck/.claude/worktrees/Reshape-the-Summary-and-Index-tabs` (the whole `photo` repo; the
project is its `DarksCheck/` subfolder). Approved by the owner on 2026-11-08.

## Context

The owner (2026-11-08) asked for seven changes to the window, after using it on real sessions:

1. Summary tab: the card `9. How well the dark fits the lights` comes second, right after
   `Session`.
2. That card shows the index of the darks and the index of the lights, each on its own row;
   `in the dark master` and `master in lights` are drawn as tables; every light is analysed
   (no subsample), so the `skipped lights` row goes.
3. Summary tab, card 5 `Hot pixels`: only `stability` and `verdict` stay (the rest is on the
   Hot pixels tab); the stability statements each stand on their own line, on the Summary and
   on the Hot pixels tab.
4. Summary tab, card 8: `noise when averaged` as a table.
5. Index tab: the two statements of `shared pixels` on separate lines.
6. The tab `Lights` is renamed `Match with lights`.
7. The partner of the Index tab is no longer remembered from one run of the program to the
   next. The owner chose (asked 2026-11-08): it is also dropped when another folder is opened,
   exactly like the lights folder of R-122.

Assumptions stated, not asked: the console report keeps its order and numbering, so the
reorder and the trimming of card 5 are the Summary's arrangement only; the new content (the
two index rows of check 9, the tables, the split lines) is composed in the core, so the console
prints the two index rows too and keeps its one-line form for the rest; the card keeps its
title `9. ...`; the INDEX card stays last on the Summary.

## Steps

1. Requirements: R-027, R-031, R-050, R-051, R-053, R-063, R-071, R-073, R-075, R-087 edited,
   R-056 added (the two index rows of check 9).
2. Tests first: `R053_...EveryLightIsReadWhateverTheCap`, `R056_Check9NamesBothIndexesAnd
   CarriesTheOverlapAsTables`, `R063_TheSharedPixelsRowCarriesItsTwoStatementsAsLines`,
   `R031_TheStabilityRowCarriesItsFourStatementsAsLines`, `R024_TheNoiseCurveRowCarriesIts
   PointsAsATable`, `R030_CheckFivesCountRowsLeaveTheSummaryToTheHotPixelsTab`, the window's
   `R071_TheTabsHold...` (the order, the trimmed card, lines and tables) and
   `R073_ThePartnerReachesTheRunAndIsNeitherRememberedNorKeptForAnotherFolder`, the language
   tests (the tab's name, the tip, the partner's sentence).
3. Core: `ReportRow.Lines`, `ReportRow.Table` (`ReportTable`), `ReportRow.Summary`;
   `StabilityRow`, `CurveRow`, `OverlapRow`, `LightsLike` in `DarkReport`; the two index rows;
   `LightsAnalyzer.Analyze` without the cap, `LightFigures` and `LightTableRow` without
   `Skipped`, the lights CSV without its `skipped` column; the `--max-frames` texts.
4. Window: `RowVm(..., Lines, Table)`, `TableVm`; `Sections` arranged (check 9 first, the
   `Summary` rows); the `ReportRow` template draws the lines and the `ReportTable` template the
   table; `StoredSettings` without `Partner`; the partner dropped with the lights folder in
   `OpenAsync`; the tab `Match with lights` / `Potrivire cu lights`; the Index tab's sentence.
5. Goldens regenerated and read: the stability line's `, ` → `; ` in ten, the two index rows in
   six, the help's `--max-frames` in two.
6. `/validate`: V-007 and V-012 move (every Veil light read); the drift pasted, the figures
   recorded at the owner's word.
7. Docs: ADR 0041 (dated notes on 0008, 0009, 0036, 0039), `window.md`, `dark-analysis.md`,
   `lights-and-index.md`, README, CHANGELOG.
8. Gate green, review offered, merge proposed.

## Texts changed (original → proposal, approved with the plan)

- Tab: `Lights` → `Match with lights` / `Potrivire cu lights`.
- Index tab, the partner's last sentence: "Programul tine minte partenerul ales si dupa ce il
  inchizi, si il foloseste la urmatoarea analiza." → "Partenerul tine doar pentru folderul
  deschis: nu se pastreaza dupa ce inchizi programul si se pierde cand deschizi alt folder."
  (English: "The partner holds for the open folder only: it is not kept after you close the
  program and is dropped when you open another folder.")
- `--max-frames` help and the `MAX FRAMES` hint: "Cate cadre se tin in memorie (48 MB fiecare
  la 24 MP) si cate lights se masoara; daca sunt mai multe, se citesc doar unele, la distante
  egale (de exemplu unul din doua). Implicit 60." → "Cate cadre dintr-un grup se tin in memorie
  (48 MB fiecare la 24 MP); daca sunt mai multe, se citesc doar unele, la distante egale (de
  exemplu unul din doua). Lights-urile se masoara toate. Implicit 60."
- New rows of check 9: `index dark-uri : idx121` (`index partener` when the darks are the
  partner's), `index lights : idx157 (de 1.30 ori cat dark-ul, adica cu 2.3 grade mai calde
  decat el)`; the tables' headers `prag | pixeli | procent` and `cadre | masurat | se astepta`.
