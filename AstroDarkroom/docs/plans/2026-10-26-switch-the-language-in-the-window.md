# 2026-10-26 Switch the launcher's language in the window

## Context

Every text the launcher shows is Romanian without diacritics (ADR 0003). The five components
gained an English interface beside the Romanian one on 2026-10-25, chosen by `--lang`, English
by default. The owner now asks the launcher for the same two languages, chosen from the window,
applied at once (no restart), and remembered for the next start; translations made in context;
the existing tests that assert Romanian keep asserting Romanian, with as few changes to them as
possible (texts may change, figures not).

Answers given in this session (the ADR records them):

- First start without a saved setting: **English**, like the five components.
- The launcher **passes its language to the tool it starts** (`--lang=ro` / `--lang=en`); all
  five windows accept `--lang=<code>` and `--lang <code>`.
- The setting lives in **`%LOCALAPPDATA%\AstroDarkroom\settings.json`**, the convention of
  LightsCheck's and DarksCheck's `UserSettings`.

Decisions I make and state (routine, following the suite's majority convention):

- **Both wordings in place, `T("english", "romana")`**, the shape of FlatCheck, DarksCheck,
  LightsCheck and AstroPhotoAnalyzer (FlatCheck's ADR 0062), not ImageVisualCompare's table:
  the texts stay beside the code that composes them. `Ui` is copied from FlatCheck's
  `Language/Ui.cs` (enum `UiLanguage`, `Ui.Default`, `Ui.Current`, `Ui.Use` scope, `Ui.T`),
  public here since the window uses it.
- **The test assembly runs in Romanian**: a module initializer sets `Ui.Default` to Romanian
  (FlatCheck's `RomanianByDefault.cs`), so R001..R004 stand unchanged.
- **The language control is a ComboBox in the bottom bar**, between the folder line and
  "Despre", with the two items named each in its own language, `English` and `Romana`, so a
  user finds theirs whatever the current language; its tooltip is the only translated part.
- **Live switch = one `Relabel()` in `MainWindow`** that assigns every label and rebuilds the
  five rows; the About window is modal and built fresh at every open, so it reads the current
  language in its constructor. The XAML carries no user text (as ImageVisualCompare's ADR 0038
  ends up); every text is assigned in code-behind, which is what makes the live switch one
  method. No `TExtension`: it picks once per element and cannot re-pick.
- Numbers and `release.json` values never change with the language; no `CultureInfo` is set.

## Branch and worktree

From `E:\Photografie\Code\AstroDarkroom`:
`git worktree add .claude/worktrees/Switch-the-language-in-the-window -b claude/Switch-the-language-in-the-window main`,
then work in `<worktree>\AstroDarkroom`. The plan is saved there as
`docs/plans/2026-10-26-switch-the-language-in-the-window.md`.

## Requirements (new rows, status `draft` until the tests pass)

- **R-006 Two languages.** Every text the launcher shows exists in English and in Romanian
  without diacritics; `Ui.Current` picks, English when nothing set it. The catalogue's
  descriptions, the reason of R-002 and the message of R-003 follow it. Example: in English
  `AppCatalogue.Apps[1].Description` is `Checks the flats of a lens` and the reason for a
  missing FlatCheck is `FlatCheck.Wpf.exe is missing`; in Romanian they are what R-001 and
  R-002 quote. The window's language ComboBox switches every visible text at once.
- **R-007 The setting.** `LanguageSetting.TryRead(path)` reads `{ "language": "ro" }` (or
  `"en"`) from the file and gives that language; a missing file, a file that is not a JSON
  object, no `language` key, a non-string or an unknown code gives null, and the launcher
  starts in English. `LanguageSetting.Write(path, language)` writes the file, creating its
  folder; a failed write is ignored (the choice applies to this run). The window reads
  `%LOCALAPPDATA%\AstroDarkroom\settings.json` at start and writes it at every switch.
- **R-008 The tool's language.** `LaunchPlan` carries the arguments; `Launcher.Plan` gives
  `--lang=ro` or `--lang=en` for the current language, and the window passes it to
  `Process.Start`. Example: in Romanian the plan of LightsCheck is
  `<folder>\LightsCheck.Wpf.exe --lang=ro`.

R-001 and R-002 get one sentence each: the quoted texts are the Romanian wordings; the English
ones are in R-006.

## The English wordings (reviewed in context, the Romanian stays byte for byte)

| Romanian | English |
| --- | --- |
| Verifica dark-urile unei sesiuni sau ale unei biblioteci | Checks the darks of a session or of a library |
| Verifica flat-urile unui obiectiv | Checks the flats of a lens |
| Verifica light-urile unei sesiuni: focus, trailing, dithering, drift | Checks the lights of a session: focus, trailing, dithering, drift |
| Compara doua variante ale aceleiasi procesari, pixel cu pixel | Compares two versions of the same processing, pixel by pixel |
| Verifica o imagine finala inainte de print | Checks a final image before printing |
| Lipseste `<exe>` | `<exe>` is missing |
| Uneltele de astrofoto, in ordinea in care se folosesc. Deschide una din randul ei. | The astrophotography tools, in the order they are used. Open one from its row. |
| Deschide / Despre / Inchide | Open / About / Close |
| Versiune, licenta, componente (F1) | Version, licence, components (F1) |
| versiunea X / versiune necunoscuta / modificari nelansate | version X / version unknown / unreleased changes |
| `<folder>`  -  release din `<date>` | `<folder>`  -  release of `<date>` |
| `<exe>` nu a putut porni: `<message>` | `<exe>` could not start: `<message>` |
| Despre Astro Darkroom | About Astro Darkroom |
| Lansatorul si pachetul comun al uneltelor de astrofoto. | The launcher and the shared package of the astrophotography tools. |
| LICENTA / COMPONENTE | LICENCE / COMPONENTS |
| Release din `<date>`; versiunile componentelor asa cum au intrat in pachet. | Release of `<date>`; the components' versions as they went into the package. |
| Versiunile componentelor asa cum au intrat in pachet. | The components' versions as they went into the package. |
| Fara un release.json cu componente langa program: versiunile componentelor nu se cunosc. | No release.json with components beside the program: the components' versions are not known. |
| the warranty paragraph (`LicenseInfo.Warranty`) | This program comes with ABSOLUTELY NO WARRANTY, not even the implied warranty of merchantability or fitness for a particular purpose. It is free software: you may redistribute and modify it under the terms of the GPLv3 (or, at your option, any later version). The full text is in the LICENSE file beside the program, or at <url>. |
| Limba interfetei (the ComboBox tooltip) | Interface language |

## Steps

1. **Worktree and branch** as above. `docs/requirements.md`: R-006..R-008 as `draft`, the
   notes on R-001/R-002. Then the tests, which must fail (no `Ui` yet, so a build failure is
   the failing output):
   - `tests/AstroDarkroom.Tests/RomanianByDefault.cs` (module initializer).
   - `tests/AstroDarkroom.Tests/LanguageTests.cs`: R006 (English catalogue descriptions under
     `Ui.Use(English)`, Romanian under the default; English reason of R-002; `Ui.T` picks;
     every text pair differs and neither side is empty for the catalogue), R007 (round trip
     through a `TempWorkspace` file; missing file, `{ not json`, `[1]`, `{}`, `{"language":1}`,
     `{"language":"fr"}` give null; a write into a folder that does not exist creates it), R008
     (the plan's arguments in both languages).
2. **Core**: `src/AstroDarkroom.Core/Language/Ui.cs` (copy of FlatCheck's, public, header, docs
   say AstroDarkroom), `Language/LanguageSetting.cs` (System.Text.Json, `TryGetProperty` like
   `ReleaseManifest`; `Ui.Code(language)` / `Ui.TryParse(code)` for `en`/`ro`). `AppCatalogue.Apps`
   becomes a property computed from the current language over one list of
   `(folder, title, en, ro, exe)`; `SuiteFolder.Locate` and `AppRow` texts through `T`;
   `LicenseInfo.Warranty` becomes a property; `LaunchPlan(FileName, WorkingDirectory, Arguments)`
   with `Launcher.Plan` filling `--lang=<code>`. Gate `-SkipTests` green, then the tests green.
3. **Window**: `App.OnStartup` reads the setting (`Environment.SpecialFolder.LocalApplicationData`
   + `AstroDarkroom\settings.json`) into `Ui.Default` before the window. `MainWindow.xaml`:
   texts removed, `x:Name`s added, the `Language` ComboBox in the bottom bar; `MainWindow`:
   `Relabel()` called from the constructor and from `SelectionChanged` (which also sets
   `Ui.Default` and writes the setting); `OpenApp` passes `plan.Arguments` through
   `ProcessStartInfo.ArgumentList`. `AppRow` gains `OpenLabel`. `AboutWindow`: texts assigned in
   the constructor, title included.
4. **Docs**: ADR 0006 "The interface speaks English or Romanian, chosen in the window and
   remembered; the tool starts in the launcher's language" (supersedes 0003 in part, records the
   three answers and the pairs-in-place choice; status accepted on the plan's approval). CLAUDE.md
   rule "Romanian without diacritics..." reworded to the two-language rule, naming ADR 0006.
   `docs/design/launcher.md`: Covers adds the two new files; a section on the language (the
   scope, `Relabel`, the setting, the argument). `README.md` (Romanian): the language selector,
   the setting file and that the tools open in the launcher's language. CHANGELOG `Added`.
   Requirements to `implemented`.
5. **Verify**: `scripts/verify.ps1` green (after a `dotnet build --no-incremental -warnaserror`
   to surface what an incremental build hides); `scripts/release.ps1 -Version 1.0.0 -PublishOnly
   -WhatIf` is not needed (no publish change). Start the launcher from a published suite folder
   if one exists under `artifacts/`, else from the build folder: switch the language, close,
   reopen, check it is remembered, open "Despre" in both languages. Then `/wrap-up`: commit on
   the branch, propose the merge.

## Verification

- `dotnet test AstroDarkroom.sln --filter "FullyQualifiedName~LanguageTests"` red at step 1,
  green at step 2; the existing `LauncherTests` and `SuiteTests` unchanged and green.
- `scripts/verify.ps1` green before the commit.
- By hand in the window: the switch is immediate, the choice survives a restart, a started
  tool (from a suite folder) opens in the launcher's language.

## What deviated

- The selector is two `RadioButton`s drawn as buttons, not a ComboBox: WPF's default ComboBox
  brings the system's light chrome into the dark window, and a dark one needs a full template
  with a popup (ADR 0006 records it).
- `AppRow.OpenLabel` is a property set when the row is built, not a computed one: the XAML can
  only bind an instance member, and the analyzer (CA1822) refuses a computed property that
  reads no instance data.
- The window was checked by driving it with UI Automation from a script, not by hand: the
  published suite folder `artifacts/0.1.0` with the new launcher assemblies copied in, every
  text read back in both languages, the setting file read, the launcher restarted, and
  FlatCheck opened from a row with `--lang=ro` on its command line. The folder was restored
  afterwards.
