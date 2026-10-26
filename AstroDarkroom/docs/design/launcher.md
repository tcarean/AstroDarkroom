# The launcher

**Covers:** `src/AstroDarkroom.Core/SuiteApp.cs`, `src/AstroDarkroom.Core/AppCatalogue.cs`,
`src/AstroDarkroom.Core/SuiteFolder.cs`, `src/AstroDarkroom.Core/Launcher.cs`,
`src/AstroDarkroom.Core/ReleaseManifest.cs`, `src/AstroDarkroom.Core/LicenseInfo.cs`,
`src/AstroDarkroom.Core/Prerequisites.cs`, `src/AstroDarkroom.Core/Version.cs`,
`src/AstroDarkroom.Core/Language/Ui.cs`,
`src/AstroDarkroom.Core/Language/LanguageSetting.cs`, `src/AstroDarkroom.Wpf/App.xaml.cs`,
`src/AstroDarkroom.Wpf/AppRow.cs`, `src/AstroDarkroom.Wpf/MainWindow.xaml.cs`,
`src/AstroDarkroom.Wpf/AboutWindow.xaml.cs` and `Icon/make_icon.py`.

The launcher is two layers: a core that answers three questions about a folder (which apps
exist, which of them are there, how to start one), reads two files (`release.json`, the
language setting) and words every answer in one of two languages, and a window that asks those
questions at start-up and at every change of language and shows the answers. The requirements
are R-001 to R-008 in `docs/requirements.md`; the choice of a compiled-in catalogue over a scan
of the folder is ADR 0002, the split with no test project for the window is ADR 0001, the two
languages and the live switch are ADR 0006.

## The core

- `Ui` is the language: `UiLanguage` (English, Romanian), `Ui.Default` for the process (the
  start-up sets it from the setting, the selector at every switch, the test assembly to
  Romanian once), `Ui.Use(language)` for a scope on the current flow (an `AsyncLocal`, for the
  tests), `Ui.Current` for the innermost of the two, and `Ui.T(en, ro)`, the one call every
  text goes through. `Ui.Code` and `Ui.TryParse` map a language to `en`/`ro` and back, for the
  setting file and the `--lang=` argument.
- `LanguageSetting.TryRead(path)` and `Write(path, language)` are the one remembered setting:
  `{"language":"ro"}`, read through `ReleaseManifest.Text`, the same tolerant key read as
  `release.json` (no model binding, a type check on the value), null for anything it cannot
  read. The write goes to `settings.json.tmp` and then replaces the file in one `File.Move`,
  so an interruption leaves the last good file; a failed write is swallowed. The core knows no
  path; the window gives it `%LOCALAPPDATA%\AstroDarkroom\settings.json`.
- `SuiteApp` is one app: its folder name in the repository (also its name in `release.json`),
  its title (the component's name), its description in one language and its window exe. The
  suite ships windows only, so there is no console field (ADR 0002). `AppCatalogue.Apps` is
  the fixed list of five, in workflow order (darks, flats, lights, the processed pair, the
  print), in the language of `Ui.Current`: `Build` makes the list once per language when the
  type loads, each description a `T(en, ro)` pair, and `Apps` picks the list. The exe names are the
  components' `AssemblyName` plus `.exe`; `SuiteTests` reads the components' project files to
  keep them so, reads `kit.json` to keep the list equal to the set of components the release
  bundles, and refuses an entry whose project is a console.
- `SuiteFolder` is one base directory. `Locate(app)` combines it with the window exe and asks
  `File.Exists`; the result, `AppLocation`, carries the path either way, so a row can say what
  file it would start, and the reason `<exe> is missing` / `Lipseste <exe>` when it is not
  there. `ManifestPath` is `release.json` in the same folder.
- `Launcher.Plan(app, folder)` turns an available location into a `LaunchPlan`: the exe's full
  path, the base directory as working directory, so a component finds its own files (the
  shared desktop runtime, `lensfun/`, `LICENSE`) where it expects them, and the arguments as one
  string, `--lang=` plus the code of `Ui.Current`, the one-token form every component's window
  accepts (a string rather than a list keeps the record's value equality). An unavailable app
  throws `InvalidOperationException` with the reason; the core never calls `Process.Start`.
- `ReleaseManifest.TryRead(path)` parses the file `scripts/release.ps1` writes
  (`name`, `version`, `date`, `commit`, `components[]` with `name`, `version`, `commit`,
  `unreleased`, `clean`) with `System.Text.Json` and no model binding: every key is read with
  `TryGetProperty` and a type check, a string key that is wrong reads as `""`, a boolean one
  as `false`, a component that is not an object is skipped. Only three things make it return
  null: no file, no JSON object, no `components` array. `ReleaseInfo.Find(name)` is the lookup
  the rows use, by `SuiteApp.Folder`.
- `VersionInfo` reads `<Version>` and `<Copyright>` back from the assembly attributes MSBuild
  writes from `Directory.Build.props`; `Banner` is `"Astro Darkroom " + Value`. `LicenseInfo`
  holds the warranty paragraph, a pair picked at each read, and the licence URL for the about
  window.
- `Prerequisites` holds what the suite needs on the machine, as texts only (R-009, ADR 0007):
  `Summary`, one line for the main window, and `Details`, three paragraphs for the about
  window (the Siril session folder, Siril 1.4.4 or newer, the optional Adobe lens profiles for
  FlatCheck), each a pair picked at each read. The core checks nothing of it.

## The window

- `App.OnStartup` reads the setting file into `Ui.Default` (English when it gives none), makes
  one `SuiteFolder` from `AppContext.BaseDirectory`, the folder the executable runs from, and
  opens `MainWindow` on it and on the setting's path. Nothing is read from the command line.
  `App.LargestIconFrame` picks the widest frame of the `.ico` for the two logos, since the
  first frame of an icon is not reliably the largest.
- `MainWindow` reads `release.json` once. The XAML carries no user text: `Relabel()` assigns
  every label (the subtitle, the prerequisites line under it from `Prerequisites.Summary`, the
  folder line, the selector's tooltip, the about button and its tooltip) through `T` and
  builds one `AppRow` per catalogue app: the app, its `AppLocation`, its `ComponentInfo` or
  null. The row's properties are the strings the XAML binds (`OpenLabel`, `VersionHint` as
  `version X` / `versiunea X` or `version unknown` / `versiune necunoscuta`, `ButtonHint` as
  the exe or the reason), picked at the row's build, and `IsAvailable`, which
  enables the button. The button's tooltip is shown on a disabled button too
  (`ToolTipService.ShowOnDisabled`), since the reason is the one thing a disabled row must
  tell. The window does not watch the folder: a file that appears or goes away is seen at the
  next relabel, or at the click, where a missing file becomes a message box instead of a crash.
- The selector is two `RadioButton`s in the `LanguageChoice` style of `App.xaml` (the buttons'
  look, the checked one on the accent border), `English` and `Romana`, each with its language
  in its `Tag`. Each button's content is a horizontal panel: its flag, an `Image` in the `Flag`
  style of `App.xaml` over the `FlagEnglish` or `FlagRomanian` `DrawingImage` (vector drawings
  at the flags' proportions, 13 px high, aliased so the stripes stay crisp, the US stars a grid
  of dots), then its name in a `TextBlock` with `Style="{x:Null}"`, so the text takes the
  button's `Foreground` instead of the implicit style's and dims and lights with the check
  (ADR 0007). The constructor checks the current one, which fires `LanguageChecked` for a
  language equal to `Ui.Default` and so changes nothing; a click sets `Ui.Default`, writes the
  setting and calls `Relabel()`, so every text and every row changes at once.
- `OpenApp` asks `Launcher.Plan` and calls `Process.Start` with `UseShellExecute`, the plan's
  working directory and its arguments string, disposing the returned handle at
  once (the started app is not affected). The bottom line shows the base directory without its
  trailing separator. `Win32Exception` (Windows refused the start) and
  `InvalidOperationException` (the file went away) end in a message box with the row's title.
- `AboutWindow` takes the `ReleaseInfo` the main window read and words itself once, in its
  constructor, in the language of that moment (it is modal and made anew at every open): the
  title, the tagline, the headings, the banner, the copyright, the warranty, the prerequisites
  card (an `ItemsControl` of `Muted` paragraphs over `Prerequisites.Details`), the release line
  and the component list from `release.json` without the launcher's own entry (`Name` equal to
  the release's `name`), each as a `ComponentLine` with `?` for an unknown version; the
  `unreleased` flag of the file is not shown (ADR 0007). Without a file, or with one that
  names no component, the list is the catalogue with `?` and the line says the versions are
  unknown. Enter and Esc close it (`IsDefault` and `IsCancel` on one button).
- The palette and the button, tooltip and card styles are in `App.xaml`, the same colours as
  LightsCheck's dark theme reduced to what two windows need. Both windows darken their title
  bar on `SourceInitialized`, when the handle exists, through `DarkTitleBar`, a copy of
  LightsCheck's: one `LibraryImport` of `DwmSetWindowAttribute` with attribute 20, then 19 for
  the Windows 10 builds before 20H1; a Windows without either keeps the light bar (ADR 0004).
  Message boxes are the system's and keep its bar.

## The icon

`Icon/make_icon.py` draws `astrodarkroom.ico` at eight sizes on a 110-unit grid with 8x
supersampling: a red-brown rounded square (the safelight) and a white star with a halo, a core
and, from 32 px up, four spikes; at 24 px and below the spikes would fall under a pixel, so the
core is drawn larger instead. The `.ico` is committed; the script runs only when the drawing
changes. The csproj uses the file as `ApplicationIcon` and as a `Resource`, which the windows'
`Icon=` and `App.IconSource` name.
