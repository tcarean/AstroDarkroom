# Astro Darkroom

## Download

The latest release of Astro Darkroom: one zip for Windows 10 or 11 (x64) with the six tools, the
launcher and the .NET 8 runtime inside. No installer, and no .NET to install.

| Version | Date | Zip | SHA-256 |
| --- | --- | --- | --- |
| (none yet) | | | |

Unzip it into any folder and start `AstroDarkroom.exe`. The tools work on the session folders
Siril lays out and expect Siril 1.4.4 or newer: see
[What the tools need](#markdown-header-what-the-tools-need).

Before you unzip, check the hash. This command prints the zip's SHA-256, which must be the one in
the table; otherwise the download broke or the file is not the published one:

```
certutil -hashfile AstroDarkroom-X.Y.Z-win-x64.zip SHA256
```

The executables are not signed, so on a new computer Windows SmartScreen asks before the first
start. Older versions, each with its hash, are listed in
[AstroDarkroom/README.md](AstroDarkroom/README.md), section "Download".

[TOC]

## Build the suite from a checkout

The zip's folder can be built from this repository as it is now: the launcher and the window of
every tool, each from its latest commit, with one copy of the .NET 8 runtime. The build changes
no tracked file and commits nothing.

### What the build needs

- Windows 10 or 11, x64.
- [Git for Windows](https://git-scm.com/download/win).
- The [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0), version 9.0.318 or a later
  9.0.3xx: the `global.json` of every project in the suite asks for 9.0.318 and accepts only a
  later patch of that feature band. `dotnet --list-sdks` shows what is installed. The tools
  target .NET 8; this SDK builds them, and the build puts the .NET 8 runtime into the folder.
- Windows PowerShell 5.1, part of every Windows 10 and 11.
- Internet access the first time, for the NuGet packages and the .NET 8 runtime packs.

Visual Studio is not needed.

### Steps

**1. Get the sources**, into a folder with a short path (see "A short path" below):

```
git clone https://bitbucket.org/tcarean/photo.git C:\src\photo
```

Or bring a checkout you already have up to date. Each tool is built from its folder as it is in
the checkout, so this is what makes every component current:

```
git -C C:\src\photo switch main
git -C C:\src\photo pull
```

**2. Build and publish the suite**, from the `AstroDarkroom` folder:

```
cd C:\src\photo\AstroDarkroom
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/release.ps1 -Version 1.0.0 -PublishOnly
```

**3. Start** `C:\src\photo\AstroDarkroom\artifacts\1.0.0\AstroDarkroom.exe`. The folder
`artifacts\1.0.0\` is the whole suite; copy it anywhere. Beside it, in `artifacts\`, are its zip,
`AstroDarkroom-1.0.0-win-x64.zip`, and the zip's `.sha256`.

On the author's computer, with the NuGet packages already downloaded and the projects already
built, step 2 took 77 seconds and made a folder of 365 files and 160 MB, and a zip of 66 MB
(measured 2026-10-27, with the six tools).

### What the command does

- `-PublishOnly` runs the publish step of the suite's release script alone: no checks, no tests,
  no change to `Directory.Build.props` or `CHANGELOG.md`, nothing committed or tagged.
- It empties `artifacts\<version>\`, then publishes into it the launcher and the window of each of
  the six tools, self-contained for win-x64. The folder is flat, with one copy of every file; if
  two tools would put different files at the same path, the command stops and names the file.
- It writes `release.json` beside the executables: the suite's version, the date, the commit, and
  for each tool its version, its last commit and whether its folder had uncommitted changes
  (`"clean": false`). The launcher shows the versions from it.
- `-Version` is required, as `X.Y.Z`: it names the folder and the zip and goes into
  `release.json`. The version the launcher shows is its own, `<Version>` in
  `AstroDarkroom/Directory.Build.props`; passing that number keeps the two the same.
- FlatCheck and AstroPhotoAnalyzer print NuGet warnings NU1902 and NU1903 about
  SixLabors.ImageSharp 3.1.11. They are known and do not stop the build; the reasons are in
  FlatCheck's ADR 0058 and AstroPhotoAnalyzer's ADR 0036.

**A short path.** Windows refuses paths longer than 260 characters unless long paths are enabled,
and the deepest path the build makes is 161 characters below the repository's root. Clone into a
folder whose path is shorter than about 95 characters, such as `C:\src\photo`.

### The consoles and the tests

The suite holds the windows only. DarksCheck, FlatCheck, LightsCheck, CopyWcs and
AstroPhotoAnalyzer also have a console for scripted use (`darkscheck.exe`, `flatcheck.exe`,
`lightscheck.exe`, `copywcs.exe`, `AstroPhotoAnalyzer.exe`), which cannot share the suite's
folder: the consoles' base runtime and the windows' desktop runtime carry three DLLs with the
same names and different contents. The same command,
`scripts/release.ps1 -Version X.Y.Z -PublishOnly`, run in a tool's own folder, publishes that
tool alone, its console included, into the tool's `artifacts\`. Every folder is a project of its
own, with its own gate (format check, strict build, tests):
`powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`.

## What Astro Darkroom is

Astro Darkroom is a suite of six Windows tools for a deep-sky astrophotography session made with
a DSLR and processed in [Siril](https://siril.org), one step at a time: the darks, the flats, the
lights, two versions of a processing side by side, the plate solve put back into the processed
image, and the finished image before it goes to print. Five of them check: each measures and
reports, every verdict with the number and the threshold behind it. None of the five modifies,
moves or deletes a frame; the few files some of them write on request are new files beside the
originals. The sixth, CopyWcs, writes: it copies header cards from one FITS into another, in
place or into a new file, and leaves the pixels as they were.

The tools were built around the author's own equipment and validated on the author's sessions: a
Nikon D3300 with a Samyang 135 mm f/2, a Nikkor 35 mm DX and a Tokina 11-20 mm, on a Sky-Watcher
Star Adventurer GTi. Raws of other cameras are read too, through LibRaw, but every figure of their
validation was measured on that kit.

The launcher, `AstroDarkroom.exe`, is a small window with one row per tool, in the order of a
session, each with its description, its version and a button that opens it; a tool whose
executable is missing from the folder is shown disabled, with the missing file's name. **About**
(F1) shows the suite's version and date, the licence and the version of every tool. The launcher
computes nothing itself. It keeps a log, `log\AstroDarkroom.log` beside it, in the same `log`
folder as the tools' own logs: to report a problem, attach that folder (the launcher's
`README.md`, "Logs").

The launcher and the tools speak English or Romanian. The launcher's two language buttons switch
every text at once, and the choice is remembered; a tool opened from the launcher starts in the
launcher's language. Started on its own, a tool speaks English, or Romanian with `--lang ro`.

Each tool has its own folder in this repository, with its sources, its tests and its
documentation: the user guide is the folder's `README.md`, and the requirements, the decisions
and the validation figures are in its `docs/`, all in English.

## The tools

### DarksCheck: the darks

Checks the dark frames of a night, or a library of darks. Open a Siril session folder (`darks/`
required, `biases/` and `lights/` optional) or a folder of darks. It runs nine checks: metadata
and cadence, light leaks, thermal drift, noise, hot pixels, fixed pattern, dead pixels, whether
there are enough darks, and the lights against the darks. It computes an index of how warm the
sensor ran, on darks, on a library and on lights, so that a master from a library can be matched
to a night; a night with lights and no darks is checked against a library you choose as its
partner (**Choose partner...**). It reads camera raws (`.nef`, `.cr2`, `.cr3`, `.arw`, `.dng`,
`.raf`) or FITS files.

### FlatCheck: the flats

A quantitative diagnostic of the flats of a photographic lens on a DSLR. Open a session folder:
it finds the master flat Siril's preprocessing script wrote, `pp_flat_stacked.fit` in `process/`
or `masters/`, and runs two tests at once. **Structure**: is the master radially symmetric, and
does its vignetting match the lens's published profile. **End to end**: does the flat correct the
night's calibrated lights, `pp_light_*.fit` in `process/`. The other tabs:

- **Exposure**: whether a raw flat is exposed right.
- **Histogram**: a frame as Siril's histogram-equalised view shows it; a picture, not a test.
- **Smoothing**: writes a second master with the diffuser's texture removed.
- **Synthetic**: writes a radial flat from the lens's Adobe or lensfun profile and compares the
  master with it.
- **Split**: splits the flats into 2 or 4 groups, runs Siril's script on each and compares the
  groups' masters with the whole one.

With no master in the folder yet, **Run in Siril** runs the preprocessing script on it through
your own `siril-cli`, without opening Siril.

### LightsCheck: the lights

Checks the light frames of a night on a star tracker. Open a Siril session folder (the raws in
`lights/`, the biases in `biases/`, optional). It measures every frame (focus, elongation and
trailing, sky level and noise, star count, satellite trails), registers the frames to measure the
dithering (how often, how far, in which directions, where positions repeat), the drift and the
polar alignment, and lists the frames to leave out of the stack, each with its reason, and what
to do differently next time. It reads camera raws or FITS files.

### ImageVisualCompare: two versions side by side

A before/after slider for two versions of the same processing, such as before and after star
removal, two background extractions or two stretches: image A left of a vertical line you drag,
image B right of it, at the same zoom and pan, with the RGB of the pixel under the cursor read
from both. It is made to show a difference of one or two levels in a dark field, where the eye
cannot see it. It opens PNG and TIFF files (8 or 16 bits per channel, and 32-bit float TIFF) and
FITS; two images of different sizes are compared on a common canvas after it asks. Everything on
screen is dark, whatever the Windows theme.

### CopyWcs: the plate solve, put back

Copies the plate solve of one FITS file into another (its folder is `WCS_Copy`). You solve a
stacked image in Siril, then stretch, denoise or export it and lose the astrometric header on
the way; CopyWcs puts the WCS cards of the solved file back into the processed one, so that
Siril's annotation and AstroPhotoAnalyzer's catalogue of dark nebulae find the sky again. Choose
the solved file and the destination, preview the cards it would copy, then copy them into the
destination in place or into a new file; the image data are left exactly as they were. It
refuses two images of different sizes unless you tell it to go on, and it cannot know whether
the image was mirrored or flipped on the way: check the annotation in Siril before trusting the
result.

### AstroPhotoAnalyzer: the finished image, before print

A print-readiness diagnostic for a finished, stretched image: a TIFF, PNG or FITS as it comes out
of Siril. It says, in the order the decisions are made, how to send it to print: the file format,
whether the background survives the paper's black (from the medium's Dmax, or from the ICC
profile of the paper on its printer), the colour cast and gradient, the largest print size the
real detail supports, and how much denoise the print version needs. Each recommendation comes
with its number and its threshold. With a plate-solved FITS (a WCS in its header) it finds the
catalogued dark nebulae in the frame, by name, and judges the dust against the sky around them.
Besides its report, it writes one file, and only when asked: from its **Curves** tab, a new FITS
with a suggested contrast curve; the input is never touched.

## What the tools need

### A Siril session folder

DarksCheck, FlatCheck and LightsCheck open the folder of one night, laid out the way Siril and
its preprocessing scripts lay it out; FlatCheck also reads what those scripts write:

```
2026-10-06-Veil\
    lights\     the light frames: camera raws or FITS
    darks\      the darks
    flats\      the flats
    biases\     the biases (offsets)
    process\    what Siril's scripts write: pp_flat_stacked.fit, pp_light_00001.fit, ...
    masters\    the masters, for the scripts installed with Siril 1.4 (OSC_Preprocessing.ssf)
```

| Tool | What it opens |
| --- | --- |
| DarksCheck | the session folder: `darks/` required, `biases/` and `lights/` optional; or a folder of darks, a library |
| FlatCheck | the session folder: the master `pp_flat_stacked.fit` in `process/` or `masters/`, the calibrated lights `pp_light_*.fit` in `process/`, a raw from `flats/` |
| LightsCheck | the session folder: `lights/` required (or the frames directly in the folder), `biases/` optional; `darks/` is only counted |
| ImageVisualCompare | two PNG, TIFF or FITS files, such as two exports from Siril |
| CopyWcs | two FITS files of the same stack: the plate-solved one and the processed one, which it rewrites in place or copies to a new file |
| AstroPhotoAnalyzer | one finished TIFF, PNG or FITS, as exported from Siril |

FlatCheck's Smoothing, Synthetic and Split write their new files into the session folder, and
only when you press their button.

### Siril 1.4.4 or newer

Install [Siril](https://siril.org/download/) 1.4.4 or newer. The masters and the stacks the tools
check come out of its scripts, and the tools were built and measured against Siril 1.4.4.
FlatCheck's **Run in Siril** starts `siril-cli.exe` from your installation (found through its
entry in the registry, then in `C:\Program Files\Siril\bin`) with a script from Siril's own script
menu; it proposes `OSC_Preprocessing_WithoutDark.ssf`, from the scripts repository Siril
downloads. It gives `siril-cli` a private copy of your Siril settings, so your own
`config.1.4.ini` is only read, never rewritten.

### Optional, for FlatCheck: Adobe's lens profiles

FlatCheck compares the vignetting of a master flat with the lens's published profile, and its
Synthetic tab builds a flat from that profile. It ships with one source of profiles, the lensfun
database (the `lensfun\` folder beside its executable). It also reads Adobe's lens profiles,
`.lcp` files, when an Adobe program has installed them in one of these folders:

- `C:\ProgramData\Adobe\CameraRaw\LensProfiles\1.0`, Adobe's own profiles as Camera Raw and the
  DNG Converter install them;
- `%APPDATA%\Adobe\CameraRaw\LensProfiles`, the profiles you add yourself;
- `C:\Program Files\Adobe\<app>\Resources\LensProfiles\1.0`, the copy Lightroom Classic and
  Lightroom keep inside their own program folder.

A file present in two of them is read once.

Installing them is recommended but optional. Without them everything works, and the list of
lenses holds the lensfun profiles only; with them, the vignetting check and the synthetic flat
also have Adobe's profile of your lens. The Adobe programs that bring the profiles:

| Program | Cost | Puts them where FlatCheck reads them |
| --- | --- | --- |
| [Adobe DNG Converter](https://helpx.adobe.com/camera-raw/using/adobe-dng-converter.html) | free | yes |
| Adobe Camera Raw, the raw plug-in installed with Photoshop | subscription | yes |
| Adobe Lightroom Classic, Adobe Lightroom | subscription | yes: each keeps its copy inside its own program folder, such as `C:\Program Files\Adobe\Adobe Lightroom Classic\Resources\LensProfiles\1.0`, which FlatCheck reads |

The simplest way is the free Adobe DNG Converter: install it once and the profiles stay where
FlatCheck finds them; you never need to run the converter itself. FlatCheck only reads the
profiles; it neither copies nor redistributes them.

## Licence

Every tool and the launcher are free software, under the GNU General Public License version 3 or
(at your option) any later version: see the `LICENSE` file in each folder. What a tool ships from
others is listed, with its licence, in the folder's `THIRD-PARTY-NOTICES.md`, where it has one.

## Also in this repository

Besides the six tools' folders and `AstroDarkroom` (the launcher and the suite's release):

| Folder | What it is |
| --- | --- |
| `ClaudeCodeStarterKit` | the template and the development process the projects above follow: C#/.NET, maintained with Claude Code |
| `SirilCleanup` | a PowerShell script that tidies a session folder after Siril: it moves what is no longer needed into `safe-to-delete` and deletes nothing |
| `DitherCalculator` | a Python script: how long to hold a SynScan arrow for a wanted dither, on a Star Adventurer GTi |
| `FocalLenghChecker` | a console tool that counts the Nikon raws of a folder per focal length |
| `MindfulnessReminder` | a Windows tray reminder, not related to astrophotography |
| `CodeMetrics`, `count_lines.py` | count the code files and lines of a folder |
