# The launcher

**Covers:** `src/AstroDarkroom.Core/SuiteApp.cs`, `src/AstroDarkroom.Core/AppCatalogue.cs`,
`src/AstroDarkroom.Core/SuiteFolder.cs`, `src/AstroDarkroom.Core/Launcher.cs`,
`src/AstroDarkroom.Core/ReleaseManifest.cs`, `src/AstroDarkroom.Core/LicenseInfo.cs`,
`src/AstroDarkroom.Core/Prerequisites.cs`, `src/AstroDarkroom.Core/Version.cs`,
`src/AstroDarkroom.Core/ThirdParty.cs`, `src/AstroDarkroom.Core/WindowFit.cs`,
`src/AstroDarkroom.Core/VcRuntime.cs`, `src/AstroDarkroom.Core/Language/Ui.cs`,
`src/AstroDarkroom.Core/Language/LanguageSetting.cs`, `src/AstroDarkroom.Wpf/App.xaml.cs`,
`src/AstroDarkroom.Wpf/Icons.cs`, `src/AstroDarkroom.Wpf/AppRow.cs`,
`src/AstroDarkroom.Wpf/MainWindow.xaml.cs`, `src/AstroDarkroom.Wpf/DarkTitleBar.cs`,
`src/AstroDarkroom.Wpf/ScreenArea.cs`, `src/AstroDarkroom.Wpf/NativeProbe.cs`,
`src/AstroDarkroom.Wpf/AboutWindow.xaml.cs`, `src/AstroDarkroom.Wpf/AstroDarkroom.Wpf.csproj`,
`Icon/make_icon.py` and the icon files in `Icon/`.

The launcher is two layers: a core that answers three questions about a folder (which apps
exist, which of them are there, how to start one), reads two files (`release.json`, the
language setting) and words every answer in one of two languages, and a window that asks those
questions at start-up and at every change of language and shows the answers. The requirements
are R-001 to R-010 and R-015 to R-018 in `docs/requirements.md`; the choice of a compiled-in catalogue
over a scan of the folder is ADR 0002, the split with no test project for the window is ADR
0001, the two languages and the live switch are ADR 0006. What the launcher writes to its
diagnostic log, and how, is in `logging.md` (R-011 to R-014, ADR 0012).

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
  so an interruption leaves the last good file; a failed write is swallowed, and logged. The
  core knows no path; the window gives it `%LOCALAPPDATA%\AstroDarkroom\settings.json`.
- `SuiteApp` is one app: its folder name in the repository (also its name in `release.json`),
  its title (the component's name), its description in one language, its window exe and the
  file name of its icon in `Icon/` (R-010). The suite ships windows only, so there is no
  console field (ADR 0002). `AppCatalogue.Apps` is
  the fixed list of six, in workflow order (darks, flats, lights, the processed pair, the
  plate solve put back into the processed image, the print; ADR 0010), in the language of
  `Ui.Current`: `Build` makes the list once per language when the type loads, each
  description a `T(en, ro)` pair, and `Apps` picks the list. The exe names are the
  components' `AssemblyName` plus `.exe`; `SuiteTests` reads the components' project files to
  keep them so, reads `kit.json` to keep the list equal to the set of components the release
  bundles, refuses an entry whose project is a console, and compares each icon copy's bytes
  with the component's `ApplicationIcon` file (ADR 0011).
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
  null: no file, no JSON object, no `components` array, besides a file it cannot read; each
  read leaves one line in the diagnostic log, the versions or why there are none.
  `ReleaseInfo.Find(name)` is the lookup the rows use, by `SuiteApp.Folder`.
- `VersionInfo` reads `<Version>` and `<Copyright>` back from the assembly attributes MSBuild
  writes from `Directory.Build.props`; `Banner` is `"Astro Darkroom " + Value`. `LicenseInfo`
  holds the warranty paragraph, a pair picked at each read, and the licence URL for the about
  window.
- `Prerequisites` holds what the suite needs on the machine, as texts only (R-009, ADR 0007):
  `Summary`, one line for the main window, and `Details`, four paragraphs for the about
  window (the Siril session folder, Siril 1.4.4 or newer, the Visual C++ runtime with
  `VcRuntime.DownloadUrl`, the optional Adobe lens profiles for FlatCheck), each a pair picked
  at each read. Of these, only the runtime is checked, by `VcRuntime` and the window.
- `VcRuntime` is the Visual C++ 2015-2022 runtime (x64) that LibRaw's DLLs import and the suite
  does not ship (R-018, ADR 0016): `Files`, its four DLLs as the import tables name them;
  `DownloadUrl`, Microsoft's permalink for its latest x64 package; `Missing(canLoad)`, the files
  a loader refuses, asking each once, in order, and keeping that order; `Warning`, the main
  window's text before the link, a pair picked at each read. The core loads nothing: the window
  passes the loader, the tests a fake one. `LogText.VcRuntime` words the log's line.
- `ThirdParty.Components` is the list of the components written by others that the launcher
  ships, for the about window (R-015, ADR 0013): today one, NLog, which two packages bring. A
  `ThirdPartyComponent` has a name, a role (a pair picked at each read of the list), a licence
  identifier, a copyright line, an address and its packages; all but the role are the same in
  both languages. `ThirdPartyTests` binds the list to the packages of the projects under `src/`
  and to the sections of `THIRD-PARTY-NOTICES.md`, which holds each licence's whole text.
  `ThirdParty.NoticesFile` is the name that file has in the suite folder,
  `THIRD-PARTY-NOTICES.txt`, and `ThirdParty.Lead` the card's lead, a pair picked at each read
  that names it (R-015, R-017, ADR 0014); the window project publishes the file under that name,
  which a test compares with its `Link`.
- `WindowFit.Top(top, height, areaTop, areaHeight)` keeps a window inside a work area (R-016,
  ADR 0013): its own top when it fits, the area's top or the top that puts its bottom on the
  area's when it runs past one edge, the area's top when it is as tall as the area or taller.
  Plain numbers in device-independent pixels; the window brings the area. It is a guard: WPF
  places "Despre" there already (below).

## The window

- `App`'s constructor starts the diagnostic log and `App.OnExit` closes it (`logging.md`).
  `App.OnStartup` reads the setting file into `Ui.Default` (English when it gives none), makes
  one `SuiteFolder` from `AppContext.BaseDirectory`, the folder the executable runs from, asks
  `VcRuntime.Missing(NativeProbe.CanLoad)` once and logs its outcome (Info, or Warn with the
  missing files), and opens `MainWindow` on the folder, the setting's path and that list.
  Nothing is read from the command line, which the log's first line records as it was.
- `NativeProbe.CanLoad(file)` is `NativeLibrary.TryLoad` of a bare file name, released at once
  when it loads. For a bare name Windows uses its standard search from the program's folder:
  the suite folder, then System32, Windows, the current folder and PATH, the places where
  `raw_r.dll`'s dependencies are found when a tool loads it, since .NET 8 loads a package's
  native library by its full path with `LOAD_WITH_ALTERED_SEARCH_PATH` (ADR 0016). The four
  loads take about 16 ms.
- `Icons` is the `.ico` resources of `Icon/`: `LargestFrame(fileName)` is the widest frame
  of that file (its pack URI from the private `Source`), decoded once and kept in a
  dictionary (the rows are rebuilt at every change of language), since the first frame of an
  icon is not reliably the largest; `Icons.Launcher` names the launcher's own, for the two
  logos.
- `MainWindow` reads `release.json` once. The XAML carries no user text: `Relabel()` assigns
  every label (the subtitle, the prerequisites line under it from `Prerequisites.Summary`, the
  runtime's warning under that, the folder line, the selector's tooltip, the about button and
  its tooltip) through `T` and
  builds one `AppRow` per catalogue app: the app, its `AppLocation`, its `ComponentInfo` or
  null. The row's properties are the strings the XAML binds (`OpenLabel`, `VersionHint` as
  `version X` / `versiunea X` or `version unknown` / `versiune necunoscuta`, `ButtonHint` as
  the exe or the reason), picked at the row's build, `Icon`, the app's icon through
  `Icons.LargestFrame`, which the card's first column shows at 40 px with a `DataTrigger`
  that dims it to the disabled button's opacity when the app is missing (R-010, ADR 0011),
  and `IsAvailable`, which
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
  The start asked for and its outcome go to the diagnostic log; the started process's id is
  read through `IdOf`, which never throws, so the log line cannot turn a start that worked into
  the message box of one that failed.
- The runtime's warning (R-018) is `RuntimeWarning`, a `Muted` `TextBlock` in `WarnBr` under the
  prerequisites line, collapsed unless the list `App` passed holds a file: a `Run` that
  `Relabel()` fills from `VcRuntime.Warning`, then a `Hyperlink` in `AccentBr` whose `Run` shows
  `VcRuntime.DownloadUrl` and whose `NavigateUri` the constructor sets to it (the inlines touch
  in the XAML, so no space slips in before the link). `OpenRuntimeLink` starts the URI's
  `OriginalString` through the shell, as `OpenApp` starts an app, and logs it; a
  `Win32Exception` or `InvalidOperationException` ends in a message box with the address and an
  Error line.
- `AboutWindow` takes the `ReleaseInfo` the main window read and words itself once, in its
  constructor, in the language of that moment (it is modal and made anew at every open): the
  title, the tagline, the headings, the banner, the copyright, the warranty, the third-party
  card after the licence's (a one-line `Muted` lead, `ThirdParty.Lead`, then an `ItemsControl` over
  `ThirdParty.Components`, each the name and the role on one line, the licence and the address
  on a `Faint` line under it and the copyright line under that; R-015, ADR 0013), the prerequisites
  card (an `ItemsControl` of `Muted` paragraphs over `Prerequisites.Details`), the release line
  and the component list from `release.json` without the launcher's own entry (`Name` equal to
  the release's `name`), each as a `ComponentLine` under the catalogue's title for that name
  (`release.json` names a component by its folder, so `WCS_Copy` is shown as CopyWcs; a name
  the catalogue lacks is shown as it is) with `?` for an unknown version; the `unreleased`
  flag of the file is not shown (ADR 0007). Without a file, or with one that names no
  component, the list is the catalogue's titles with `?` and the line says the versions are
  unknown. Enter and Esc close it (`IsDefault` and `IsCancel` on one button).
- `AboutWindow` fits its screen (R-016, ADR 0013). `MainWindow` hands it the work area of the
  screen the main window is on, from `ScreenArea.WorkAreaOf`: Windows' `MonitorFromWindow`
  (the screen nearest the window) and `GetMonitorInfoW` (its work area, in the pixels Windows
  gives the process: the system DPI's, since WPF makes the launcher system aware by default),
  divided by the window's DPI scale, since `SystemParameters.WorkArea` is the primary screen's;
  the primary screen's stands in when the window has no handle or the call fails. The about
  window sets its `MaxHeight` to the area's height, so `SizeToContent` stops there. Its content
  is a grid of two rows: a `ScrollViewer` named `Cards`, with the header and the cards, in the
  star row, and the Close button in the auto row, so the button stays in view and only the cards
  scroll; with the window sized to its content the star row is as tall as the cards, so on a
  screen where everything fits nothing changes. `Cards` has a transparent background, so the
  mouse wheel scrolls it over the margins and the gaps between the cards too, which a null
  background would leave to the window; nothing in it takes the focus, which stays on the Close
  button, so the window's `PreviewKeyDown` passes Up, Down, Page Up, Page Down, Home and End to
  it (`ScrollCards`) and leaves Enter and Esc to the button. WPF places a window centred on its
  owner inside the owner screen's work area, applying the top limit last
  (`Window.CalculateWindowLocation`, .NET 8), so a window taller than the area used to keep its
  title bar and lose its bottom; on `Loaded` the window's `Top` goes through `WindowFit.Top` as a
  guard of that place, and a Debug line writes its size and place, the area and whether the cards
  scroll. The scroll bar is `App.xaml`'s implicit style, LightsCheck's dark one whole: a thin
  rounded thumb, page buttons with no opacity, and its horizontal form.
- The palette and the button, tooltip and card styles are in `App.xaml`, the same colours as
  LightsCheck's dark theme reduced to what two windows need. Both windows darken their title
  bar on `SourceInitialized`, when the handle exists, through `DarkTitleBar`, a copy of
  LightsCheck's: one `LibraryImport` of `DwmSetWindowAttribute` with attribute 20, then 19 for
  the Windows 10 builds before 20H1; a Windows without either keeps the light bar (ADR 0004),
  and a call that cannot be made at all (no `dwmapi.dll`, or no such entry point) leaves a
  Debug line in the log, as LightsCheck's copy does. Message boxes are the system's and keep
  its bar.

## What the window's project publishes

`AstroDarkroom.Wpf.csproj` puts two texts beside `AstroDarkroom.exe`, in the build output and in
whatever is published from it, each a `None` item of the repository's root with a `Link` for its
name: `LICENSE`, the GPL text, which every component publishes with the same bytes, so the
suite's merge skips the copies; and `THIRD-PARTY-NOTICES.txt`, the repository's
`THIRD-PARTY-NOTICES.md` under the name `ThirdParty.NoticesFile` gives (R-017), which no
component publishes.

The target `PublishDotnetLicenceTexts` adds the .NET runtime's two texts to a self-contained
publish, which carries the runtime (ADR 0014). It runs after `ComputeResolvedFilesToPublishList`
and depends on the SDK's `ResolveFrameworkReferences`, which gives each `ResolvedRuntimePack` item
its `PackageDirectory`, the runtime pack's folder in the NuGet cache at the version being
published. From the item whose `FrameworkName` is `Microsoft.NETCore.App` it takes `LICENSE.TXT`
and `THIRD-PARTY-NOTICES.TXT` into `ResolvedFileToPublish`, with the `RelativePath`s
`DOTNET-LICENSE.txt` and `DOTNET-THIRD-PARTY-NOTICES.txt`, so they are copied unchanged; when
either file is missing, or no such pack was resolved, its `Error` stops the publish. A
framework-dependent publish carries no runtime and gets neither file. The WindowsDesktop pack is
left out: its `LICENSE` has the same words, and it has no notices file (the notices file links
WPF's repository's one instead).

`ThirdPartyTests` reads the project file for the notices item's `Link`, the two published names and
the error, and asks MSBuild what the publish would copy without publishing (`ReadPublishList`,
run once for its two tests): `dotnet build` of the window project with the target
`ComputeResolvedFilesToPublishList`, `RuntimeIdentifier=win-x64` and `SelfContained=true`, the
switches of the launcher's `release.publish` entry, which the test compares with `kit.json`, and
`-getItem:ResolvedFileToPublish`, whose JSON lists what the publish takes from the project and
from the runtime packs, each with its `RelativePath` and `CopyToPublishDirectory`, in about a
second once the project is built (the referenced projects' outputs are not in it: that target
alone does not resolve them); a whole publish would cost the gate about a minute. An item left
without `CopyToPublishDirectory` is listed as `Always`, so it is still copied. The second test
takes the WindowsDesktop pack's version from the list's paths and checks that the notices file
links WPF's notices at that tag. `SuiteTests` binds the notices file's `Packages:`
and `Programs:` lines to the lock file beside each window project of `release.publish`, which
holds the packages of the projects that window references too; a list item may go on over
indented lines, which `Notices.Field` joins.

## The icons

`Icon/make_icon.py` draws `astrodarkroom.ico` at eight sizes on a 110-unit grid with 8x
supersampling: a red-brown rounded square (the safelight) and a white star with a halo, a core
and, from 32 px up, four spikes; at 24 px and below the spikes would fall under a pixel, so the
core is drawn larger instead. The `.ico` is committed; the script runs only when the drawing
changes. The csproj uses the file as `ApplicationIcon`, and every `.ico` of the folder as a
`Resource` under `Icon/`: the windows' `Icon=` names the launcher's by its pack path in XAML,
and `Icons.Source` builds the same URI for the frames `LargestFrame` decodes.

The other six files are the components' own icons, copied under their file names and never
drawn here; the catalogue names each (`SuiteApp.Icon`) and `SuiteTests` keeps every copy
byte-equal to the component's `ApplicationIcon` (R-010, ADR 0011). When a component redraws
its icon, the gate here goes red until the new file is copied over the old copy.
