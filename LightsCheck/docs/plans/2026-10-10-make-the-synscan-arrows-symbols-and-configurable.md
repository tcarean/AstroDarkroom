# Make the SynScan arrows symbols, right east by default, and configurable

Branch `claude/Make-the-SynScan-arrows-symbols-and-configurable`, its worktree under
`.claude/worktrees/`. Requirements R-040 (core and console) and R-041 (window), drafted and
accepted in chat on 2026-10-10 with the four recommendations (two settings, the sense letter
kept, UTF-8 arrows in the console, `settings.json`); ADR 0014 (proposed, supersedes ADR 0013
in part; this plan's approval makes it accepted). The failing tests are written: `R040_*` in
`SeriesAnalyzerTests` and `SeriesTests`, `R041_*` in `MainViewModelTests`; the `R038_*` asserts
on the old words were moved to the new form (an intended change of those words).

## Context

On the owner's SynScan the right arrow moves the mount east, the reverse of the forum default
ADR 0013 took; the words `V (dreapta)` were long; the mapping must be configurable. Outcome:
the cell reads `RA 15 px ← V, DEC 12 px ↓ S`, the default is `→` east and `↑` north, the
console takes `--est` and `--nord`, the window has two selectors remembered in
`%LOCALAPPDATA%\LightsCheck\settings.json`.

## Steps

### 1. Core

- `src/LightsCheck.Core/Runs/SeriesRun.cs`: `internal enum Arrow { Left, Right, Up, Down }`;
  `internal sealed record ArrowKeys(Arrow East, Arrow North)` with `Default =
  new(Arrow.Right, Arrow.Up)`, `West` (the other horizontal), `South` (the other vertical),
  `static string Symbol(Arrow)` → `←`, `→`, `↑`, `↓`. `SeriesOptions` gains `ArrowKeys? Arrows =
  null` (null = `Default`); `SeriesResult` gains `ArrowKeys Arrows`, set by `Run` from the
  options.
- `src/LightsCheck.Core/Report/SeriesReport.cs`: `MoveCell(MountMove?, ArrowKeys)` and
  `MoveArrows(MountMove?, ArrowKeys)`; `MoveComponents(m, keys)` yields the symbol instead of
  the word (east `Symbol(keys.East)`, west `Symbol(keys.West)`, north, south); the cell is
  `$"{Axis} {px} px {Arrow} {Sense}"` when signed; `FrameRows` passes `r.Arrows`.
  `SeriesOutputs` passes `r.Arrows` to `MoveArrows` (the symbols, still quoted when two).

### 2. Console

- `src/LightsCheck.Cli/Options.cs`: `ArrowKeys Arrows` (default `ArrowKeys.Default`); `--est`
  takes `stanga` (east = `Left`) or `dreapta` (`Right`), `--nord` takes `sus` (`Up`) or `jos`
  (`Down`); anything else or a missing value is an error naming the option, through the
  existing value-taking branch (`a is "--frame" or ... or "--est" or "--nord"`).
- `src/LightsCheck.Cli/Program.cs`: `Main` sets `Console.OutputEncoding = Encoding.UTF8` before
  `Run` (the in-process tests pass writers and are unaffected); `Run` passes `o.Arrows` into
  `SeriesOptions`; `WriteUsage` adds `[--est stanga|dreapta] [--nord sus|jos]` to the first
  usage line and two option lines: `--est stanga|dreapta  sageata SynScan care muta montura spre
  est (implicit dreapta)`, `--nord sus|jos  ... spre nord (implicit sus)`.

### 3. Window

- New `src/LightsCheck.Wpf/UserSettings.cs`: `public static class UserSettings` with
  `static string? Path { get; set; }` (null → `%LOCALAPPDATA%\LightsCheck\settings.json`),
  `static (string East, string North) Load()` (the two symbols; defaults on a missing or
  unreadable file or unknown values) and `static void Save(string east, string north)`
  (creates the folder, writes `{"east":"→","north":"↑"}` with `System.Text.Json`, swallows
  IO errors). No package.
- `src/LightsCheck.Wpf/ViewModels/MainViewModel.cs`: `EastChoices = ["→", "←"]`, `NorthChoices
  = ["↑", "↓"]`, `EastArrow` / `NorthArrow` strings loaded in the constructor, saved on set;
  `Options` builds `ArrowKeys` from them (`→` → `Arrow.Right` etc.). The symbol-to-enum map
  lives beside `ArrowKeys` in the core (`ArrowKeys.Parse(string symbol)` or a static map), so
  the window maps nothing itself.
- `src/LightsCheck.Wpf/MainWindow.xaml`: two `ComboBox`es after FIRE, labels `EST` and `NORD`
  (`Faint` style), tooltips "sageata SynScan care muta montura spre est / nord", bound to the
  choices and the selected symbol, disabled while a run is in flight like the other inputs.

### 4. Tests, goldens, validation

- Regenerate the goldens: `series.txt` and `series-classes.txt` change nowhere (their cells
  are unsigned), `help.txt` gains the usage words and two lines. Any other changed line is a
  finding.
- V-011 on Veil: the cells change form (`RA 17 px ← V`), the counts do not; re-measure with
  `expected: null` printing, record the new cells, note the re-wording in `validation.md`.
  `/validate` on all eleven cases.

### 5. Record

- R-040, R-041 → `implemented`; ADR 0014 → accepted; ADR 0013's Status line already carries
  the superseded part.
- `docs/design/series-analysis.md` (ArrowKeys, the symbols, the default), `session-and-console.md`
  (the two options, UTF-8 output), `window.md` (UserSettings, the selectors).
- `README.md`: the new cell form, the default keys, the two options and the two selectors,
  where the setting is stored; the old sentence about the forum default goes.
- `CHANGELOG.md`: Changed (symbols, the default swapped), Added (the setting, console options).
- Save this plan as `docs/plans/2026-10-10-make-the-synscan-arrows-symbols-and-configurable.md`;
  `/wrap-up`.

## Verification

1. `scripts/verify.ps1 -SkipTests` while editing; the full gate before the commit.
2. `dotnet test ... --filter "FullyQualifiedName~R040|FullyQualifiedName~R041|FullyQualifiedName~R038|FullyQualifiedName~R039|FullyQualifiedName~R004"` green.
3. The golden diffs read line by line.
4. The window launched on Veil: the selectors in the top bar, the cells with symbols, the
   setting kept after a restart; the console run in a real terminal shows the arrows.
5. The Veil re-measurement for V-011 pasted.

## Outcome (2026-10-10)

Followed as written. The symbol-to-key map is `ArrowKeys.FromSymbol` in the core, beside
`Symbol`, so the view-model passes symbols through. V-011's cells were re-worded by the
known rule (`V (dreapta)` to `← V` and so on) and then re-measured on Veil, the counts
unchanged. The goldens changed in `help.txt` only: the usage line and the two option lines.

The subagent review (nine findings, all applied): the console's code page is switched for
the run only and put back, guarded for a run without a console, and the UTF-8 claim is
limited to the console window with `--out` for files; the symbols and the default live in
the core alone (`UserSettings` stores strings, the view-model validates through
`ArrowKeys`); `TestSettings` isolates every Wpf test from the developer's settings file;
`settings.json` holds the arrows unescaped; R-038, R-039, the `MoveCell` comment and the
CHANGELOG headings were brought in line; `R041` covers the export and the failed write.
