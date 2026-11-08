# Match the open darks with another folder's lights, and label a folder with its own index

Branch `claude/Match-darks-with-another-sessions-lights`, worktree
`DarksCheck/.claude/worktrees/Match-darks-with-another-sessions-lights` (the whole `photo` repo;
the project is its `DarksCheck/` subfolder). Saved as
`docs/plans/2026-11-07-match-darks-with-another-sessions-lights.md`.

## Context

The owner opens a library of darks and wants to know whether those darks fit the lights of
another session (one without darks, or with bad ones). The measurement exists (check 9: the
dark's hot pixels read in the lights, ratio, verdict, degrees, the lights' index "like a dark of
idxNN", R-052/R-064), but a run only takes the lights of the folder it opens, so today the
question goes one way only: open the lights-only session, give the darks as the partner (R-054).
Two things on the Index tab in that lights-only case confuse: the big label is the partner's
(`<library>-idx67`), not the open folder's, and the partner's index and label appear again on the
`dark-uri (partener)` row.

Accepted by the owner on 2026-11-07: the draft requirements R-055, R-066, R-122 (in
`docs/requirements.md`, status `draft`), the two choices (the lights folder is not remembered and
is dropped when another folder is opened; a lights-only session is labelled with its lights'
index). ADR 0039 is written (accepted). The acceptance tests are written and fail to compile on
the missing members (`DarkOptions.LightsFrom`, `MainViewModel.LightsFrom`, `WindowTexts.*`):
`R055_*` (2), `R066_*` (2), `R122_*` (2), goldens `lights-from.txt`, `en_lights-from.txt`.

## Changes

### Core (`src/DarksCheck.Core`), first

1. `Session/SessionFolder.cs`: `Open(path, allowLightsOnly = false, string? lightsFrom = null)`.
   With `lightsFrom`: the full path must exist (else `DirectoryNotFoundException`, message
   `T("the chosen lights: cannot find the folder X", "lights-urile alese: nu gasesc folderul X")`);
   its `lights\` subfolder when that holds frames, otherwise the folder itself; no frame at all
   is `InvalidOperationException` with the prefix and the `no frame in` wording of the darks
   (`niciun light in X (raw: ...; FITS: ...)`). Those paths replace the folder's own lights
   (`LightsDir`, `Lights`); a new property `string? ChosenLightsRoot` (the folder given, full
   path; null otherwise) and `bool LightsChosen => ChosenLightsRoot is not null`. The log's
   `LogText.Session` line adds `chosenLights=`.
2. `Runs/InventoryRun.cs`: `Run(string folder, string? lightsFrom = null)` passes it through.
3. `Runs/DarkSeriesRun.cs`: `DarkOptions` gets a last parameter `string? LightsFrom = null`
   (positional callers unchanged); `Run` calls `InventoryRun.Run(folder, options.LightsFrom)`
   and logs it. In the lights-only branch `labelSession` goes: the label is computed after the
   lights, `IndexAnalyzer.Label(inv.Session, darksFromPartner is null ? index.Tag :
   IndexFigures.TagFor(lightsIndex))`; the partner's own label is kept in a new result field
   `DarkSeriesResult.DarksFromPartnerLabel` (`IndexAnalyzer.Label(partnerSession, index)`).
4. `Analysis/IndexAnalyzer.cs`: `Label(SessionFolder, string tag)` does the folder-name work;
   `Label(SessionFolder, IndexFigures)` calls it with `index.Tag`; `IndexFigures.TagFor(double)`
   static, `Tag` uses it (`idx-` for NaN). For a lights-only session `DarksDir` is `<root>\darks`
   (absent), so the parent, the session's own folder, names the label, as today.
5. `Report/InventoryReport.cs`: the `lights` row for chosen lights prints the full folder and
   `; alese de tine` / `; chosen by you`: `E:\...\lights (4 cadre .fit; alese de tine)`.
6. `Report/DarkReport.cs`: `IndexRows`: when `r.DarksFromPartnerLabel` is set, the
   `dark-uri (partener)` row ends with `; eticheta <label>` / `; label <label>`.
7. `Report/DarkOutputs.cs`: `CheckOutDir(root, dir, OutFolder which)` with an enum
   `{ Session, Partner, ChosenLights }` replacing `bool partner`; the third message
   `--out nu poate fi folderul lights-urilor alese sau unul din el (...)` /
   `--out cannot be the chosen lights' folder or one inside it (...)`; `WriteAll` also checks
   `ChosenLightsRoot`.

### Console (`src/DarksCheck.Cli`)

8. `Options.cs`: `--lights DIR` beside `--against`: no value → `--lights cere o valoare` (the
   generic branch), blank → `--lights cere un folder de lights` / `--lights expects a folder of
   lights`; `Lights` property. `--inventory` goes with it.
9. `Program.cs`: the inventory branch passes `o.Lights`; before decoding, when `o.Lights` is
   set, `SessionFolder.Open(o.Folder!, allowLightsOnly: true, o.Lights)` so a bad lights folder
   is `EROARE` and exit 1 at once (R-055), and its `ChosenLightsRoot` goes through `CheckOutDir`
   when `--out` is given; `DarkOptions` gets `LightsFrom: o.Lights`; the mode log line adds
   `lights=`. Usage: `[--lights DIR]` in both usage lines, and the option text in both
   languages: `--lights DIR  lights-urile din alt folder (o sesiune, din lights\ al ei, sau un
   folder de lights): se compara cu dark-urile deschise in locul celor din lights\` /
   `--lights DIR  the lights of another folder (a session, from its lights\, or a folder of
   lights): compared with the open darks instead of the ones in lights\`.

### Window (`src/DarksCheck.Wpf`)

10. `ViewModels/WindowTexts.cs`: `ChooseLightsTitle`, `NoLightsFrom`
    (`(niciunul: lights-urile folderului deschis)`), `LightsFromChanged`
    (`Lights-urile s-au schimbat: apasa "Analizeaza din nou" ca sa le compari cu dark-urile.`),
    `LightsFromLead` (the card's sentence: another folder's lights are compared with the open
    darks, as if they were in the folder's lights\; the choice is for this folder only and is not
    kept), `IndexLeadLights` (for a folder without darks: the label is the lights': the dark they
    are like, read through the partner's darks; copy it to name the session's folder; nothing
    is renamed). Plain words: no `echivalent`, `amplitud`, `median` (the test regexes).
11. `ViewModels/MainViewModel.cs`: `LightsFrom` (`string?`, blank → null; raises
    `HasLightsFrom`, `LightsFromText`; `Status = LightsFromChanged` when `HasResult`; logged; not
    saved), `HasLightsFrom`, `LightsFromText`. `OpenAsync(folder)`: when `folder` differs from
    `_folder` (full paths, ordinal ignore case) the choice is cleared first (so a rerun and the
    problem card's flow keep it). `Options` passes `LightsFrom: _lightsFrom`; the run's log line
    adds it. `IndexLead` becomes `_result?.DarksFromPartner is null ? WindowTexts.IndexLead :
    WindowTexts.IndexLeadLights`, raised in `RaiseAll`.
12. `Views/LightsView.xaml`: a new first row with one card `LIGHTS-URI DIN ALT FOLDER` /
    `LIGHTS FROM ANOTHER FOLDER`: the lead (`Muted`, wrapped), the folder text (`LightsFromText`,
    trimmed with tooltip), the buttons `Alege lights-urile...`, `Lights-urile folderului`
    (enabled by `HasLightsFrom`), `Analizeaza din nou` (enabled by `HasFolder`, the Index tab's
    tooltip), the row `IsEnabled="{Binding IsIdle}"` as on the Index tab. The existing rows move
    down one. `Views/LightsView.xaml.cs`: three handlers to `MainWindow.ChooseLightsFromView`,
    `ClearLightsFromView`, `RerunFromView` (as `IndexView.xaml.cs`).
13. `MainWindow.xaml.cs`: `ChooseLights()` (an `OpenFolderDialog` titled `ChooseLightsTitle`,
    starting in the current choice when it exists; logs chosen/cancelled; sets `_vm.LightsFrom`),
    `ChooseLightsFromView`, `ClearLightsFromView`.

### Tests and goldens (`tests/`)

14. `LightsAndIndexTests.R054_*`: the label assertion becomes the lights' label (the requirement
    changes with ADR 0039; reported at wrap-up). `LanguageWindowTests.R0xx_TheLightsTabIsInPlainWords`:
    the new card's words in both languages (`(none: the open folder's lights)` / the Romanian).
15. Goldens regenerated with `DARKSCHECK_UPDATE_GOLDEN=1`: `lights-only.txt`,
    `en_lights-only.txt` (the `eticheta` row and the `dark-uri (partener)` row), `help.txt`,
    `en_help.txt` (the usage line and the `--lights` option), new `lights-from.txt`,
    `en_lights-from.txt`. Every changed line is read and tied to the change at wrap-up.
16. `docs/validation.json` / `.md`: a new case V-0NN: the 2026-10-10 library opened as the folder
    with `--lights` on the Veil session reads V-007's figures (0.43, `CosmeticOnly`, 3933
    pixels) and the library's own label; `expected` null until `/validate` measures it
    (DARKSCHECK_TESTDATA_LARGE on E:), run from the worktree.

### Docs

17. `docs/requirements.md`: R-054 loses the partner-label clause (points at R-066); R-055,
    R-066, R-122 → `implemented`; R-075 says the lights folder is not stored.
18. `docs/design/session-and-console.md` (SessionFolder's chosen lights, `--lights` in the
    console's dispatch), `docs/design/lights-and-index.md` (`Label(session, tag)`, the lights-only
    label, `DarksFromPartnerLabel`), `docs/design/window.md` (the Lights tab's card, `LightsFrom`
    not stored and cleared on another folder, `IndexLead` per result).
19. `README.md`: `[--lights DIR]` in the usage block and the option list; a short section
    "Do these darks fit another session's lights?" (open the darks, choose the lights on the
    Lights tab or `--lights`), and a line under "A session without darks" on the label.
20. `CHANGELOG.md`: Added (`--lights DIR` and the Lights tab's card, R-055, R-122, ADR 0039);
    Changed (the label of a session without darks is its own, R-066).

## Order and verification

Core (1-7) → console (8-9) → `scripts/verify.ps1 -SkipTests` green → window (10-13) → tests
(14) → `dotnet test` on `R05|R06|R12|Golden|Language|Smoke` → goldens (15), diff read →
validation case (16) + `/validate` on the real sessions → docs (17-20) → full
`scripts/verify.ps1` → `/wrap-up` (commit on the task branch, merge proposed).
