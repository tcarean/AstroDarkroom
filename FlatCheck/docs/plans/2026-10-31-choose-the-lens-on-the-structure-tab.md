# Choose the lens on the Structure tab when the raw gives only a range

Plan for the task branch `claude/Choose-the-lens-on-the-Structure-tab`, worktree
`FlatCheck/.claude/worktrees/Choose-the-lens-on-the-Structure-tab`, cut from `main` (9a70dfe3).
Approved by the owner on 2026-10-31.

## Context

The owner's Veil session (`2026-10-06-3h`, Samyang 135 f/2 at f/2.8 on a D3300) lists two lenses
on the Structure tab's card `VIGNETTING against the lens profile`: Adobe's `Nikkor 135mm f/2 DC`
first and lensfun's `Samyang 135mm f/2.0 ED UMC` second. The Samyang is a manual lens: the D3300
writes the "non-CPU lens" ID 74, which no profile carries, and the range set in its menu, 135 mm
f/2.0. The candidates then come by range alone (ADR 0030, R-116 / ADR 0076 for the mount): every
lens of the camera's mount whose name says exactly 135 mm f/2. The rows are in catalog order,
Adobe before lensfun (`LensIdentification.Ordered`), the verdict is the nearest profile's (ADR
0031), and the report says to read one's own lens's row (`StructureReport.RangeOnlyNote`). The
window cannot say which lens is on the camera; the console can, with `--lens`, which the owner
does not use (the window is the daily tool).

Measured (the validation table of `docs/design/lens-profiles.md`, section 11, 2026-09-18, and
the owner's window on 2026-10-31): at f/2.8 the master reads 0.15 stops, the Nikkor DC profile
0.12 and the Samyang 0.14, so the nearest is the Samyang (+0.01, PERFECT); at f/2 the two sky
masters read 0.61 / 0.60 against Nikkor 0.50 and Samyang 0.46, so the nearest was the Nikkor
(+0.11 BUN) while the owner's own lens reads +0.15 (ACCEPTABIL). Two profiles of two different
135 f/2 designs; the choice changes the verdict at f/2.

The design (five decisions) was put to the owner on 2026-10-31 and accepted as recommended:
(1) a lens picker on the Structure tab, only when the candidates came by range alone and there
is more than one; (2) the choice narrows the comparison to that lens, on the path the console
already has for one lens (no new verdict rule, no re-validation); (3) the choice is remembered
in the session's `flatcheck.json`, beside the two switches of R-119; (4) no prefill of the
Synthetic tab from it; (5) the rows keep their catalog order. Rejected with the owner: an
app-wide default per range ("135 mm f/2.0 means Samyang"), a second memory. ADR 0080.

## Design

- **Core, the choice** (`Runs/StructureRun.cs`, `LensProfiles/LensIdentification.cs`):
  `StructureQuery.LensKey` (string, null = all candidates): the `LensChoice.Key`
  (`Source|Maker|Name|Mount[|Calibration]`, stable, built from the catalog) of the lens chosen
  among the raw's candidates. `ShotIdentity.ChosenLens` (`LensChoice`, null when none). In
  `StructureRun.Compare`, with no `LensText` and a `LensKey`: the candidate whose key matches
  becomes `shot.ChosenLens` and the only lens compared; `shot.Candidates` stays the full list
  (the window's picker needs it). A key among no candidate (the raw changed, the profile gone)
  compares every candidate as today, `ChosenLens` null, a Debug line saying so. `LensText` wins
  over `LensKey` (the console never sets the key). `Describe` logs `lensKey=`.
- **Core, the words** (`Report/StructureReport.cs`): `ChosenLensNote(ShotIdentity)`, printed
  by `ComparisonSummary` in place of `RangeOnlyNote` when a lens was chosen: compared with the
  lens chosen (`SourceLabel Title`); the raw gives only the range (`shot.RawLensLabel`), and the
  other lenses of that range were left out (their names). `LensChoicePrompt(ShotIdentity)`
  for the card, above the picker: the raw says only the range, a manual lens or one unknown to
  the databases; choose yours and the verdict is its profile's; without a choice the nearest
  profile decides. Both `T(en, ro)`, no diacritics, XML docs. The console's output changes only
  when a `LensKey` is given, which the console never does: no golden moves.
- **Window, the memory** (`Session/SessionSettings.cs`): a third key `lens` (string, default
  null). `Read`: absent, not a string or empty -> null. `Write`: the dictionary becomes
  `Dictionary<string, object>`; `lens` is written only when not null, so an untouched file
  keeps `{"noFilters":..,"skyFlat":..}`; the Info line adds `lens=`. `MainViewModel.OpenAsync`'s
  settings line adds `lens=` too.
- **Window, the picker** (`ViewModels/StructureTabViewModel.cs`, `ViewModels/Rows.cs`,
  `Views/StructureView.xaml`): `LensOption` (`Key`, `Label`; key null = no choice, label
  `T("the nearest profile decides", "decide cel mai apropiat profil")`; a lens's label
  `SourceLabel Name [Where]`). `IReadOnlyList<LensOption> LensOptions` (the none option plus
  the candidates, filled by `Populate` from `explained.Shot` when the run compared and
  `Shot.CandidatesByRangeOnly && Shot.Candidates.Count > 1`, else empty), `bool OffersLensChoice
  => LensOptions.Count > 0`, `string LensChoicePrompt` (from the core), `LensOption SelectedLens`:
  `Populate` sets the field from `Shot.ChosenLens?.Key` and raises; the setter (a pick on
  screen) writes `_settings.Lens = value?.Key`, `_settings.Write()`, `Pending = RecompareAsync()`,
  the `NoFilters` pattern exactly. `QueryAsync` sets `LensKey = _settings.Lens`. `Reset` clears
  the options and the prompt. The XAML: under the filter box, a panel visible on
  `OffersLensChoice`: the prompt (`Muted`, wrap), then a row `{l:T En='My lens:', Ro='Obiectivul
  meu:'}` + a `ComboBox` (`ItemsSource` `LensOptions`, `SelectedItem` `SelectedLens` two-way,
  `DisplayMemberPath` `Label`, `IsEnabled` `!IsBusy`). With a choice the card shows one row and
  the summary says the chosen note; the picker keeps every candidate, so the choice can be undone.
  Nothing is computed in the window.

## Steps

1. **Worktree**: `git worktree add .claude/worktrees/Choose-the-lens-on-the-Structure-tab -b
   claude/Choose-the-lens-on-the-Structure-tab main` from `E:\Photografie\Code\FlatCheck`; every
   build, test and run in the worktree only (never in the main checkout).
2. **Requirement**: `docs/requirements.md`: R-121 `draft` (the picker, when it appears, what a
   choice does on screen, in the verdict and in `Test result.txt`, the recompare without a
   reload, the memory in `flatcheck.json`, the fallback when the key matches no candidate, an
   example with the Veil figures above); R-119 amended with the third key `lens`.
3. **ADR 0080**: "The Structure tab offers the lens when the raw gives only a range; the choice
   narrows the comparison and is remembered per session": context (the Veil listing, the
   measured rows), the five decisions, the alternatives rejected (the nearest row first; every
   row kept with the verdict moved to the chosen, a new rule over ADR 0031; an app-wide default
   per range; a picker on every list, when the ID already decides; the Synthetic prefill, set
   aside). Check main's latest ADR number before the merge.
4. **Test data** (`FlatCheck.Tests/LensProfileTestData.cs`): `ProfilesDir(..., rangePair: true)`
   writes `Nikon/NIKON CORPORATION (Nikkor 135mm f2 DC) - RAW.lcp` through `LcpOneProfile`
   (no lens ID, sff 1.0, focal 135) and `lensfun/slr-samyang.xml`, a `Samyang 135mm f/2.0 ED
   UMC` on `Nikon F AI`, cropfactor 1, with a `pa` vignetting entry at 135 mm f/2 (the existing
   Samyang entry has distortion only and is not read). `Sources(ws, rangePair: true)`. A raw
   fixture `Range135Raw()`: `FocalMm 135, FNumber 2.8, Camera "NIKON D3300", LensId 74,
   LensMinFocalMm 135, LensMaxFocalMm 135, LensMaxAperture 2`, no name.
5. **Tests first, failing** (`R121_...`):
   - Core, `LensChoiceTests.cs`: a `LensKey` of the Samyang gives one row, `Shot.ChosenLens`
     the Samyang, `Shot.Candidates.Count == 2`, the report holds the chosen note and not the
     range-only sentence; a key among no candidate gives both rows and `ChosenLens` null, with
     the Debug line; `LensText` set with a key gives the text's lenses.
   - `StructureTabTests`: the picker is offered on `Range135Raw()` (two lenses and the none
     option, none selected, the prompt shown) and not on the Tokina raw (by ID); picking the
     Samyang -> one row, the verdict that lens's, `Test result.txt` equal to the console's
     report for the same query with the key, the map the same bytes (`Assert.Same`), `Pending`
     awaited; picking none again -> both rows back; a remembered key applied at the run and a
     stale one ignored; `Reset` empties the options.
   - `SessionSettingsTests`: `lens` round-trips; absent, empty or of another type -> null; a
     write with no lens writes no `lens` key.
   - `MainViewModelTests`: a folder whose file holds `lens` opens with the choice applied
     (one row, `SelectedLens` set) and nothing written.
   - `WindowSmokeTests`: the renders `tab-structure-lens-open` and `tab-structure-lens-chosen`,
     dumped with `FLATCHECK_DUMP_SCREENS` and sent to the owner.
6. **Code**, core first (`StructureRun.cs`, `LensIdentification.cs`, `StructureReport.cs`),
   then the window (`SessionSettings.cs`, `MainViewModel.cs`, `Rows.cs`,
   `StructureTabViewModel.cs`, `StructureView.xaml`). XML docs on every member added or
   edited. `scripts/verify.ps1 -SkipTests` while editing.
7. **Docs**: R-121 -> `implemented`; `docs/design/two-applications.md` (the Structure tab
   paragraph: the picker, `LensKey`, what `Populate` sets; the `SessionSettings` paragraph: the
   third key); `docs/design/modes-structure.md` (a bullet for the choice and the two texts);
   `docs/design/lens-profiles.md` section 11 (a bullet: the window's choice narrows the
   comparison, the Veil rows); `docs/design/code-map.md` if `check-design.ps1 -Since main`
   lists it; `README.md` (the Structure section: the picker; the window's remembered switches
   paragraph: `lens`); `CHANGELOG.md` Added; this plan file. No `/validate`: no formula moves
   (`Runs/` filters a list, `Report/` gains two texts) - say so at wrap-up.
8. **Gate** `scripts/verify.ps1`, read the whole diff, commit on the branch, `/wrap-up`, offer
   the subagent review, then propose the merge.

## Verification

- `dotnet test FlatCheck.sln -c Release --filter "R121|R119|R118"` in the worktree, then the
  whole gate green; no golden diff (the console is untouched unless a key is given).
- The smoke renders `tab-structure-lens-open.png` / `tab-structure-lens-chosen.png`: the prompt,
  the picker, both rows, then the Samyang alone with the picker on it; `tab-structure-profile.png`
  unchanged (the Tokina raw offers no picker).
- The console in the worktree on the Veil master with `--raw` (the first raw of `flats/`), to
  paste the two rows at wrap-up and confirm the figures above; the window by hand, if the owner
  wants: open the Veil session, pick the Samyang, see one row and the verdict its own, close and
  reopen, the pick stays; `flatcheck.json` holds `"lens":"Lensfun|Samyang|Samyang 135mm f/2.0 ED
  UMC|Nikon F AI|..."`.

## Deviations (2026-10-31, during the task)

- The Adobe fixture file is named `Nikkor 135mm f2 DC`, without the slash: `LcpReader` takes the
  lens's name from the file's name, as Adobe writes it, and a slash is no file name.
- The lensfun fixture of the range pair carries a `Nikon F AF` mount definition with its compat
  list: without one `MountsOf` gives the body's mount alone and R-116 filters the `Nikon F AI`
  Samyang out, which is exactly the real database's shape.
- `ComparisonSummary` drops its single-profile sentence when a lens was chosen: "without a second
  database" would blame the databases for a list the person narrowed.
