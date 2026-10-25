# Add a Romanian interface chosen by `--lang`

Branch `claude/Add-the-Romanian-interface`, worktree `.claude/worktrees/Add-the-Romanian-interface`.

## Context

The owner asked for two interface languages, English and Romanian, chosen by `--lang=ro` or
`--lang ro` (default `en`), translated in context, with the existing tests changed as little as
possible and no number changed. Settled in chat on 2026-10-25: requirements R-100..R-102 and
the glossary accepted; C# tables, not `.resx`; an unknown value or a missing one shows a
message (in `MessageDialog`, ADR 0018) and goes on in English. Already written on the branch:
the three requirement rows (status `draft`), ADR 0038 (accepted 2026-10-25, varies the kit's
rule "Languages" for the Romanian texts and supersedes ADR 0006 in part), and the failing tests
`StartupArgumentsTests.cs`, `UiTextTests.cs`, `InRomanian.cs` (the build stops on
`CS0246: 'UiText' could not be found`).

Today every user-visible text is English, in twelve files; no test asserts Romanian. English
stays the default, so every existing test keeps passing unchanged.

## Design

- **Core `UiLanguage.cs`**: `enum UiLanguage { English, Romanian }`.
- **Core `StartupArguments.cs`**: `record StartupArguments(UiLanguage Language, string?
  UnknownLanguage, IReadOnlyList<string> Paths)` with `static Parse(IReadOnlyList<string>)`:
  `--lang=<v>` or `--lang <v>` (next argument, whatever it is), name and value
  case-insensitive, last one wins, `UnknownLanguage` = the rejected value (`""` for none, null
  when the last `--lang` was valid or there was none), the rest are `Paths` in order.
- **Core `UiText.cs`**: one sealed class, two instances `English` and `Romanian`, `For(UiLanguage)`,
  `static Current { get; set; } = English`. Every text is a member written as
  `Pick("English", "Română")` so both wordings sit side by side and neither can be missing;
  texts with values are methods with named parameters. Groups: toolbar and hint; window and
  dialog titles; overlay; dialog buttons; open-dialog titles and filter; padding question;
  `CouldNotRead`, `ImageTooLarge`, the nine FITS messages; `Format(bits, isFloat, channels)`
  (Romanian: `RGB pe 16 biți`, `gri float pe 32 de biți`, with the "de" rule for numbers whose
  last two digits are 0 or 20..99, `1 bit`); `PaddedCanvas`; `Memory`; the About lines. The
  `--lang` message (`UnknownLanguageTitle`, `UnknownLanguage(value)`) is static and English only,
  since it announces English.
- No `CultureInfo` is touched (ADR 0038, ADR 0017): numbers read the same in both languages.

## Steps (Core first, then the entry points)

1. Core: add `UiLanguage.cs`, `StartupArguments.cs`, `UiText.cs`; route through `UiText.Current`:
   `ImagePairValidator.DescribeMismatch` (`PadQuestion`), `ImageInfo.FormatDescription`
   (`Format` + the unchanged ` (0..65535)` suffix), `FitsHeader` (every message, `TooLarge`),
   `FitsRowDecoder` (data ends early). Verify: `scripts/verify.ps1 -SkipTests`, then the R-100,
   R-102, Format, padding and FITS tests plus the whole existing suite (English unchanged).
2. Wpf imaging: `Bitmaps.Create/TooLarge` take `bool fits` instead of the `"FITS: "` prefix
   (`FitsImageLoader`, `WicImageLoader` call sites); `ImagePairLoader` uses `CouldNotRead`;
   `MemorySnapshot.Describe` uses `Memory`.
3. Wpf shell: `App.OnStartup` parses with `StartupArguments`, sets `UiText.Current` before any
   window, keeps the pair from `Paths` (R-002 unchanged otherwise) and the unknown value;
   `MainWindow` names `NavigationLabel` and `HintText` in XAML, drops the XAML literals and
   assigns every text in its constructor, uses `UiText` for titles, overlay, padded status;
   on `Loaded` it shows the `--lang` message first (via `App.TakeUnknownLanguage()`), then
   opens the pair. `MessageDialog` buttons, `FileDialogService` titles and filter (filter built
   per call, no longer a `const`), `VersionInfo.About` becomes a computed property.
4. Full gate (`scripts/verify.ps1`); a `--no-incremental -warnaserror` build first (memory note
   on hidden warnings).
5. Run the application on the real pair (copy the files `docs/skills.md` lists into the
   worktree's `tests/testdata/large/`): `--lang=ro`, `--lang ro`, no `--lang`, `--lang=de`
   (message, then English), and a padded pair in Romanian; screenshot each, check the
   Romanian toolbar and status bar fit and nothing is clipped beyond the hint's ellipsis.
6. Record: requirements R-100..R-102 to `implemented`; a note under the rows that quote English
   text is already in the Language section; `CLAUDE.md` gains the rule "Two interface languages"
   (varies the kit's rule "Languages", ADR 0038) and `UiText.cs`/`StartupArguments.cs` in the
   Layout line; new design note `docs/design/language.md` (Covers `UiText.cs`,
   `StartupArguments.cs`, `UiLanguage.cs`, `App.xaml.cs`), its row in `docs/design/README.md`,
   `code-map.md` lines; `shell.md`/`testing.md` touched where they name the texts or the
   collection; `README.md` (Romanian): `--lang` under "Cum o pornești", the Romanian button
   names beside the English ones in "Controale" and the status bar section; CHANGELOG
   `Added` line. Plan saved to `docs/plans/2026-10-25-add-the-romanian-interface.md`.
7. `/wrap-up`: diff read back, commit on the branch, merge proposed.

## Not in scope

- The Astro Darkroom launcher passing `--lang` to this tool (another project; offered as a
  follow-up).
- The version: a new feature may warrant 1.2.0, the owner's call (kit rule "Version and year").

## Verification

- New tests: `StartupArgumentsTests` (R-100, R-102), `UiTextTests` (R-101, in the
  non-parallel collection "UI language"): padding question, formats, FITS messages, a file that
  cannot be read, memory, About, dialog texts, the main window's toolbar and hint, and every
  string member differing between the languages (but `OK`).
- Existing tests unchanged and green: the English texts.
- The application launched in both languages and with `--lang=de`, screenshots looked at.

## Outcome (2026-10-25)

Done as planned, with these differences:

- Three requirements, not four: the message for an unknown or missing value is part of R-102.
- `imaging.md` and `versioning-and-licence.md` were updated too, since `check-design -Since main`
  named them (the `Bitmaps` prefix became the `fits` flag; `VersionInfo.About` is computed).
- The validation cases were run although only texts changed in the numeric folders: V-001 to
  V-004 pass against the recorded figures.
- The application was launched on the real pair with `--lang=ro`, `--lang ro` (the padding
  question, then the padded status bar), and `--lang=de` (the message, then English).
- A subagent's code review found no blocking defect. Fixed after it: the start-up moved into
  `App.ApplyCommandLine` with two tests, the rule "Two interface languages" allows quoting the
  Romanian texts, a clearer Romanian hint and FITS messages, the ADR's consequence and the Run
  command. The parser's edge cases it listed were left as specified.
