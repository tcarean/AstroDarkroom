# 2026-10-26 Tell the prerequisites, hide the unreleased flag, draw the flags

## Context

The owner asked for four changes to the launcher on 2026-10-26:

1. The "unreleased changes" note (`modificari nelansate`) disappears from the main window's
   rows and from the "Despre" window's component list. `release.json` keeps the `unreleased`
   key and `ReleaseManifest` keeps reading it (R-004, its tests unchanged); it is simply no
   longer shown.
2. The launcher says somewhere that the tools work on a Siril session folder and expect Siril
   installed, 1.4.4 or newer. Owner's answers: **in "Despre" and in `README.md`, plus one
   short line under the main window's subtitle**; the version is written **"1.4.4 or newer"**
   (the components were measured against 1.4.4: FlatCheck ADRs 0025, 0035, 0055).
3. For FlatCheck, the same text recommends installing Adobe's lens profiles, optional, and
   names the Adobe programs that bring them. FlatCheck reads `.lcp` files from
   `C:\ProgramData\Adobe\CameraRaw\LensProfiles\1.0` and `%APPDATA%\Adobe\CameraRaw\LensProfiles`
   (FlatCheck R-070, ADR 0028); its README names Lightroom, Camera Raw (Photoshop) and the
   free Adobe DNG Converter as the installers, and the DNG Converter's install into that
   folder is confirmed by Rawpedia (<https://rawpedia.pixls.us/how_to_get_lcp_and_dcp_profiles/>).
   Without them FlatCheck falls back to the lensfun database it ships with.
4. The language buttons `English` / `Romana` get a flag each. Windows draws no emoji flags
   (regional indicators render as letters), so the flags are vector drawings in XAML. Owner's
   answer: **the US flag** for English, the Romanian tricolour for Romanian.

Facts about the components, for the wording: DarksCheck and LightsCheck open the session
folder (`darks/`, `lights/`, optional `biases/`); FlatCheck opens the master
`pp_flat_stacked.fit` in `process/` or `masters/` and runs Siril's preprocessing script
through the user's `siril-cli` (FlatCheck ADR 0035/0055); ImageVisualCompare and
AstroPhotoAnalyzer open files exported from Siril.

## Branch and worktree

From `E:\Photografie\Code\AstroDarkroom`:
`git worktree add .claude/worktrees/Show-prerequisites-and-flags -b claude/Show-prerequisites-and-flags main`,
then work in `<worktree>\AstroDarkroom`. The plan is saved there as
`docs/plans/2026-10-26-show-prerequisites-and-flags.md`.

## Requirements

- **R-004** gets one sentence: the window shows each component's version only; `unreleased`
  and `clean` are read for `release.json`'s sake and not shown.
- **R-009 (new, `draft` until the test passes)**: `Prerequisites.Summary` is one line and
  `Prerequisites.Details` three paragraphs, each in both languages through `Ui.T`: the Siril
  session folder (`lights/`, `darks/`, `flats/`, `biases/`, `process/`), Siril 1.4.4 or newer
  installed, and, optional for FlatCheck, Adobe's lens profiles with the programs that install
  them (Lightroom, Photoshop with Camera Raw, the free Adobe DNG Converter). The main window
  shows the summary under its subtitle; "Despre" shows the three paragraphs under a
  "REQUIREMENTS" / "CERINTE" heading. Example: in English the second paragraph starts with
  `Siril 1.4.4 or newer installed`.

## Steps

1. **Worktree and branch** as above; copy this plan to `docs/plans/`. `docs/requirements.md`:
   R-009 as `draft`, the sentence on R-004.
2. **Test first**: `tests/AstroDarkroom.Tests/LanguageTests.cs` gains
   `R009_ThePrerequisites_NameSirilAndTheAdobeProfilesInBothLanguages`: under the default
   (Romanian) and under `Ui.Use(English)`, the summary and the three paragraphs are non-empty,
   each differs between the languages, and the texts contain `lights/`, `darks/`, `flats/`,
   `process/`, `Siril 1.4.4`, `FlatCheck`, `Adobe`, `Lightroom`, `Camera Raw`, `DNG Converter`.
   Build fails (no `Prerequisites` yet): that is the failing run.
3. **Core**: `src/AstroDarkroom.Core/Prerequisites.cs`, a static class shaped like
   `LicenseInfo`: `Summary` (string) and `Details` (`IReadOnlyList<string>`), each text a
   `T(en, ro)` pair where it stands, Romanian without diacritics, English ASCII. Wordings:
   - Summary: `The tools read a Siril session folder and expect Siril 1.4.4 or newer; details under About.` /
     `Uneltele citesc folderul unei sesiuni Siril si cer Siril 1.4.4 sau mai nou; detalii la Despre.`
   - 1: the session folder of a night with `lights/`, `darks/`, `flats/`, `biases/` as Siril
     lays them out and the scripts' results in `process/` (or `masters/`); DarksCheck,
     FlatCheck and LightsCheck open that folder, ImageVisualCompare and AstroPhotoAnalyzer the
     files exported from Siril.
   - 2: Siril 1.4.4 or newer installed (siril.org): the masters and stacks the tools check come
     from its scripts, and FlatCheck runs the preprocessing script through `siril-cli` from the
     user's installation.
   - 3: optional, for FlatCheck: Adobe's lens profiles, read from the machine when an Adobe
     program installed them: Lightroom, Photoshop with Camera Raw, or the free Adobe DNG
     Converter; without them FlatCheck uses the lensfun database it ships with.
   Gate `-SkipTests` green, then the tests green; R-009 to `implemented`.
4. **Window, the unreleased note**: `AppRow.UnreleasedHint` removed and the second `TextBlock`
   of the version line in `MainWindow.xaml` with it; `ComponentLine` loses `Note`, the
   `AboutWindow.xaml` template loses its third column, the constructor its `Unreleased` read.
5. **Window, the prerequisites**: `MainWindow.xaml` gets a `Prerequisites` `TextBlock` (style
   `Faint`, wrapping) under `Subtitle`, assigned in `Relabel()` from `Prerequisites.Summary`.
   `AboutWindow.xaml` gets a card between the licence and the components: heading
   `PrerequisitesHeading` (`REQUIREMENTS` / `CERINTE`) and an `ItemsControl` of `Muted`
   paragraphs bound to `Prerequisites.Details`, set in the constructor. The about window's
   width stays 560; the paragraphs wrap.
6. **Window, the flags**: two `DrawingImage` resources in `App.xaml`, `FlagEnglish` (US: 13
   stripes red/white, blue canton; the stars left out at that size, the icon convention) and
   `FlagRomanian` (three vertical stripes, cobalt blue / chrome yellow / vermilion). Each
   `RadioButton`'s `Content` becomes a horizontal `StackPanel` with an `Image` (about 21x14,
   `Stretch="Fill"`, pixel-snapped) and the `TextBlock` with the language's name; the
   `LanguageChoice` style is unchanged. The names stay the single literals CLAUDE.md allows.
7. **Docs**: ADR 0007 "The launcher tells the suite's prerequisites, hides the unreleased
   flag and draws a flag on each language button" (the three owner's answers, why the texts
   live in Core: so a test guards both languages; why vector flags: Windows renders no emoji
   flags; alternatives: emoji, a PNG per flag, the Union Jack / St George). `docs/design/launcher.md`:
   Covers adds `Prerequisites.cs`; the row's and the component line's descriptions lose the
   unreleased note; the about window's card; the flags. `README.md` (Romanian): the
   "modificari nelansate" sentence of "Ce primesti" goes, a new section "Cerinte" says the
   three paragraphs, and the language paragraph mentions the flags. CHANGELOG `[Unreleased]`:
   Added (the prerequisites), Changed (no unreleased note; flags on the language buttons).
8. **Verify**: `dotnet build --no-incremental -warnaserror --no-restore`, then
   `scripts/verify.ps1` green. Start the launcher from
   `src/AstroDarkroom.Wpf/bin/Release/net8.0-windows/` (every app "missing", the texts and the
   flags show), screenshot both windows in both languages with a PowerShell script and read the
   screenshots back; back up and restore `%LOCALAPPDATA%\AstroDarkroom\settings.json` around
   the run. Then `/wrap-up`: read the diff, commit on the branch, propose the merge.

## Verification

- `dotnet test AstroDarkroom.sln --filter "FullyQualifiedName~R009"` red at step 2 (build
  failure), green at step 3; `LauncherTests` and `SuiteTests` unchanged and green.
- `scripts/verify.ps1` green before the commit (format, strict build, tests, design index).
- By eye, from the screenshots: no "unreleased" text anywhere; the summary line under the
  subtitle; the "CERINTE" card in Despre with three paragraphs; a US flag on `English`, a
  tricolour on `Romana`, in both languages.

## What deviated

- The windows were screenshot with `PrintWindow` (flag `PW_RENDERFULLCONTENT`) and "Despre"
  opened by posting F1 to the window: a background script cannot bring the launcher to the
  foreground, so a screen copy caught whatever was on top and `SendKeys` went elsewhere. The
  four screenshots (both windows, both languages) showed what the plan asked; the settings
  file was restored afterwards.
- Nothing else: the code, the texts and the docs are as planned.
