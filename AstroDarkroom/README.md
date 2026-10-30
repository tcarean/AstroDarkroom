# Astro Darkroom

The six astrophotography tools of this repository in one download, with a launcher that lists
them and starts them:

| Tool | What it does | Window |
| --- | --- | --- |
| DarksCheck | checks the darks of a session or of a library | `DarksCheck.Wpf.exe` |
| FlatCheck | checks the flats of a lens | `FlatCheck.Wpf.exe` |
| LightsCheck | checks the lights of a session: focus, trailing, dithering, drift | `LightsCheck.Wpf.exe` |
| ImageVisualCompare | compares two versions of the same processing, pixel by pixel | `ImageVisualCompare.Wpf.exe` |
| CopyWcs | copies the plate solve of one FITS into another | `CopyWcs.Wpf.exe` |
| AstroPhotoAnalyzer | checks a final image before printing | `AstroPhotoAnalyzer.Wpf.exe` |

The suite holds the windows only. The consoles (`darkscheck.exe`, `flatcheck.exe`,
`lightscheck.exe`, `copywcs.exe`, `AstroPhotoAnalyzer.exe`) stay in each tool's own release:
their base runtime and the windows' desktop runtime carry three DLLs with the same names and
different contents, so they cannot share one folder (ADR 0002). Each tool's user documentation
is the `README.md` of its folder, and the repository's front page, the `README.md` at its root,
describes every tool and how to build the whole suite from a checkout. All of them are GPLv3 or
later ([LICENSE](LICENSE)).

## What you get

A zip, `AstroDarkroom-X.Y.Z-win-x64.zip`, with a `.sha256` file beside it. Unzipped, it is one
folder with every executable side by side and the .NET 8 runtime included (nothing to install),
and `AstroDarkroom.exe`, the launcher: a window with one row per tool (its icon, title,
description and version) and an **Open** button. A row whose executable is missing from the
folder is disabled and names the missing file. **About** (F1) shows the suite's version, its date, the licence, the
component written by others that the launcher uses (see "Third-party components"), the
requirements below and the version of every component.

The launcher speaks English or Romanian: the two buttons with a flag in the bottom bar,
**English** / **Romana**, switch every text at once, without a restart. At its first start it is
in English, like the tools; the choice is remembered in
`%LOCALAPPDATA%\AstroDarkroom\settings.json` (`{"language":"ro"}`) and applies at the next start.
A tool opened from the launcher starts in the launcher's language (it receives `--lang=ro` or
`--lang=en`); started directly from the folder, it starts in English, as its README says.

`release.json`, in the same folder, says the same in a form programs read: the suite's version,
its date, its commit, and for each component its version, its commit and whether it had
unreleased changes.

The executables are not signed: on another computer, SmartScreen asks at the first start.

## Requirements

The launcher checks none of the following; it states them (one line under the title, the
details under **About**) because the tools expect them:

- **A Siril session folder**: the folder of one night with `lights/`, `darks/`, `flats/` and
  `biases/` as Siril lays them out, and the results of its scripts in `process/` (or `masters/`,
  depending on the script). DarksCheck, FlatCheck and LightsCheck open that folder;
  ImageVisualCompare, CopyWcs and AstroPhotoAnalyzer open the files exported from Siril. Each
  tool's README says exactly what the tool looks for in it.
- **Siril 1.4.4 or newer, installed** (<https://siril.org>): the masters and the stacks the
  tools check come out of its scripts, and FlatCheck runs the preprocessing script through
  `siril-cli.exe` from your installation (with a private copy of your settings, which it never
  touches). The tools' figures were measured on 1.4.4.
- **Optional, for FlatCheck: Adobe's lens profiles** (`.lcp`), read from
  `C:\ProgramData\Adobe\CameraRaw\LensProfiles\1.0` and `%APPDATA%\Adobe\CameraRaw\LensProfiles`,
  where the free Adobe DNG Converter and Camera Raw with Photoshop put them, and from
  `C:\Program Files\Adobe\<app>\Resources\LensProfiles\1.0`, the copy Lightroom Classic and
  Lightroom keep inside their program folders; a file present in two folders is read once.
  Without the profiles, FlatCheck uses the lensfun database it ships in
  its `lensfun/` folder; with them, the synthetic flat and the vignetting comparison also have
  Adobe's profile of your lens.

## Download

Each version's zip sits in the owner's Google Drive, shared with "Anyone with the link"; here
are its link and its SHA-256, the newest version first (ADR 0005). After the download, check the
hash before you unzip:

```
certutil -hashfile AstroDarkroom-X.Y.Z-win-x64.zip SHA256
```

The result must be identical to the SHA-256 column of your version; otherwise the zip broke
during the download or is not the published one.

| Version | Date | Zip | SHA-256 |
| --- | --- | --- | --- |
| (none yet) | | | |

## Logs

The launcher keeps a log of what it did, for the day something goes wrong. To report a problem,
attach the whole `log` folder beside `AstroDarkroom.exe`: it holds the launcher's log and those
of the tools that keep one, each under its own name (`FlatCheck.Wpf.log`,
`LightsCheck.Wpf.log`, ...), so one folder shows what happened from the click in the launcher to
the tool it opened.

**Where it is.** In the folder `log` beside the launcher: `log\AstroDarkroom.log`. When that
folder cannot be written (a suite folder under Program Files, a read-only drive), the log goes
to `%LOCALAPPDATA%\AstroDarkroom\log` instead, and its first line says so; a tool that cannot
write there goes to a folder of its own name under `%LOCALAPPDATA%`. Past 1 MiB (1,048,576
bytes, so a file ends one line past that) a file is left and the next one is started
(`AstroDarkroom_01.log`, `AstroDarkroom_02.log`, ...); the five most recent files are kept and
the older ones deleted, so the launcher's log takes about 5 MiB at most. Two launchers open at
the same time write into the same file, each line marked with its process.

**What is in it.** One line per event, numbers with a point: at the start the version, the
command line, the Windows and .NET versions, the processors and the memory, the culture and the
time zone, and the folders; the language and where it came from; what `release.json` says, the
version and commit of every component, or why it could not be read; how many of the six tools
are in the folder and which are missing; the window's size and place on the screen; each change
of language; each tool you open, with its file, its arguments and the process it became, or the
error Windows gave; when **About** was opened and closed; every error with its technical
details, those the launcher works around without a word included (a `release.json` or a
settings file it cannot read, a setting it cannot save); at the end the exit code and how long
the launcher was open. The log's own words are English whatever the window's language; an
error's message appears as you saw it. It holds the paths of the suite folder, of the working
folder and of the settings file, which is under your Windows account's folder and so names the
account, and nothing of your images.

**How much.** The environment variable `ASTRODARKROOM_LOG` sets the level: `off`, `fatal`,
`error`, `warn`, `info`, `debug` (the default) or `trace`; `debug` already writes everything
above, `off` writes no log and creates no folder. Set it once for your account with
`setx ASTRODARKROOM_LOG warn` (or in Windows' environment variables) and start the launcher
again; a program already open keeps the value it started with.

## How a release is made

From Claude Code, in the `AstroDarkroom` folder of the main checkout, on `main`, with every
folder clean: `/release`. The script (`scripts/release.ps1`) checks, writes the version into
`Directory.Build.props` and the dated section into `CHANGELOG.md`, publishes the launcher and
the windows of the six components, as they are on `main`, into `artifacts/X.Y.Z/` (a file that
two publishes would write with different contents stops the release), and writes
`release.json` and the zip with its `.sha256` into `artifacts/`. The owner makes the commit and
the tag `astrodarkroom-vX.Y.Z`, as the skill proposes them. The zip is uploaded to Google Drive
by hand, shared with "Anyone with the link"; then the link and the hash from the `.sha256` go in
as a new first row of the table under "Download" and as the only row of the "Download" table of
the `README.md` at the repository's root, its Bitbucket page (ADR 0008), in one commit of the
two files. The components need not be released first: `release.json` says which version and
which commit of each went in.

The suite's version is `<Version>` in this folder's `Directory.Build.props`; the first real
version, 1.0.0, is set at the owner's word.

## Building

.NET SDK 9.0.3xx (`global.json`), target .NET 8. `dotnet build AstroDarkroom.sln -c Release`
(the window needs Windows). The full gate (format, strict build, tests): `scripts/verify.ps1`.
The launcher alone, from the build folder, sees every tool as missing: it is made to run from
the suite's folder, `artifacts/X.Y.Z/`, after a `scripts/release.ps1 -Version X.Y.Z -PublishOnly`
(the steps are in the `README.md` at the repository's root).

## Third-party components

The launcher links one component written by others; **About** names it with its licence, its
copyright line and its address, and `THIRD-PARTY-NOTICES.md` holds the licence's whole text and
why it is compatible with the GPLv3 (ADR 0013).

| Component | Used for | Licence |
| --- | --- | --- |
| NLog and NLog.Targets.AtomicFile 6.2.x | writing the log (see "Logs"), two launchers into one file | BSD-3-Clause |

FlatCheck's and LightsCheck's windows ship the same two files at the same version, so the suite
folder holds one copy of each.

## For developers

The working rules are in `CLAUDE.md` and `.claude/rules/kit/process.md`, the requirements with
IDs in `docs/requirements.md`, the decisions in `docs/decisions/`, how the launcher is made in
`docs/design/launcher.md` and how its log is made in `docs/design/logging.md`. The plan of the
whole suite is in the kit:
`ClaudeCodeStarterKit/docs/plans/2026-10-24-build-the-astro-darkroom-suite.md`.
