# 0081. The Structure tab offers the lens when the raw gives only a range; the choice narrows the comparison to that lens and is remembered per session

Date: 2026-10-31
Status: accepted 2026-10-31

## Context

A manual lens writes no lens ID that any profile carries: the owner's Samyang 135 f/2 on a D3300
writes the "non-CPU lens" ID 74 and the range set in the camera's menu, 135 mm f/2.0. The
candidates then come by range alone (ADR 0030; the mount from lensfun's table, ADR 0076): every
lens of the camera's mount whose name says exactly that range. On the owner's Veil session
(2026-10-06, f/2.8) the card lists two: Adobe's `Nikkor 135mm f/2 DC` first and lensfun's
`Samyang 135mm f/2.0 ED UMC` second, in the catalog's order (Adobe before lensfun, then by
name), with the verdict the nearest profile's (ADR 0031) and a sentence saying to read one's own
lens's row. The owner asked (2026-10-31) how the Nikkor got there and whether that is right. The
window cannot know which of the two is on the camera; the console can be told with `--lens`, which
the owner does not use.

Measured (`docs/design/lens-profiles.md`, section 11, 2026-09-18): at f/2.8 the master reads
0.15 stops, the Nikkor DC profile 0.12 and the Samyang 0.14, so the nearest is the Samyang, +0.01
PERFECT; at f/2 the two sky masters read 0.61 / 0.60 against the Nikkor's 0.50 and the Samyang's
0.46, so the nearest was the Nikkor (+0.11, GOOD) while the owner's own lens reads +0.15
(ACCEPTABLE). Two designs of a 135 f/2 differ by 0.04 stops at f/2; the choice changes the
verdict there.

What exists: `StructureQuery` already carries `LensText` and `Source`, the console's `--lens`,
and `StructureRun.Compare` already narrows the list; the window passes the raw's path only. The
window already keeps two switches per session folder in `flatcheck.json` (ADR 0078, R-119) and
redoes the comparison on the kept result without reloading the master (`Recompare`, R-118).

## Decision

1. **A picker, only where the raw cannot decide.** The Structure tab's card offers `My lens:`
   with every candidate and a first entry meaning no choice, only when the candidates came by
   range alone (`ShotIdentity.CandidatesByRangeOnly`) and there is more than one. A list found
   by the lens's ID or name offers nothing: the raw decided.
2. **The choice narrows the comparison to that lens.** The query carries the chosen lens's
   catalog key (`StructureQuery.LensKey`); `StructureRun.Compare` compares that candidate alone,
   keeps the full candidate list on the identity (`ShotIdentity.Candidates`, for the picker) and
   records the choice (`ShotIdentity.ChosenLens`). The verdict is then that lens's profile's,
   through the unchanged nearest rule over a list of one: no new rule, no figure moves, no
   re-validation. The report's summary says which lens was chosen and which were left out, in
   place of the range-only sentence, so `Test result.txt` says what the screen shows. `LensText`,
   the console's, wins over the key; the console never sets the key.
3. **The choice lives with the session.** `flatcheck.json` gains `lens`, the key, written when a
   lens is picked and absent when none was; applied when the folder reopens. A key that matches
   no candidate (the raw changed, the profile gone) is ignored with a `Debug` line: every
   candidate is compared and nothing is selected, so nothing stale is ever shown.
4. **The rows keep the catalog's order.** Adobe before lensfun, then by name: with the picker the
   order no longer decides anything.

## Alternatives rejected

- **The nearest row first.** Hides the cause: the user would still not know which row is their
  lens, and the verdict would still be another lens's at f/2.
- **Every row kept, the verdict moved to the chosen lens.** A new rule over ADR 0031's "the
  nearest profile's verdict" and a table whose marked row is not the nearest: the console's
  `--lens` path already gives the narrowed list, and one row says it plainly.
- **An app-wide default per range** ("135 mm f/2.0 means the Samyang"), in `%LOCALAPPDATA%`. A
  second memory beside the session's; the owner declined it. A session's file is the record of
  that session's setup, as for the two switches.
- **A picker on every list.** When the ID or the name decided, a choice would only remove rows
  the raw itself vouched for (the two Tokina generations carry byte-identical parameters).
- **Prefilling the Synthetic tab with the choice.** Set aside by the owner; the Synthetic tab
  has its own picker with search.

## Consequences

- R-121 (the picker, the narrowing, the memory), R-119 amended (the third key).
- `StructureQuery.LensKey`, `ShotIdentity.ChosenLens`, `StructureReport.ChosenLensNote` and
  `LensChoicePrompt`; `SessionSettings.Lens`; `StructureTabViewModel.LensOptions` /
  `SelectedLens`; the picker in `StructureView.xaml`.
- The console's output changes only for a query with a key, which the console never builds: no
  golden moves. No formula moves: `/validate` is not re-run.
- Guards: `R121_*` in `FlatCheck.Tests` (the core's narrowing and the report's note),
  `StructureTabTests`, `SessionSettingsTests`, `MainViewModelTests`, and a render in
  `WindowSmokeTests`.
- Source notes: `docs/design/two-applications.md`, `modes-structure.md`, `lens-profiles.md`
  section 11.
