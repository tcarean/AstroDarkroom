# Astro Darkroom

*Check every step of an astrophoto, from the frames of the night to the print.*

## What is Astro Darkroom

Astro Darkroom is a free set of Windows tools that check an astrophoto at every step, from the
frames of the night to the print. It measures instead of guessing. Every verdict comes with the
figure it measured and the limit it compared it with, so you see not only what is wrong, but by
how much.

- **Calibration frames.** Are your darks and flats good enough to calibrate your lights? Did
  light leak in? Did the camera warm up? Does the flat remove the vignetting and nothing else?
- **Light frames.** Which frames to leave out of the stack, and why: trailed stars, soft focus,
  clouds. Did dew form on the lens? Did you dither enough? How well did the mount track?
- **Processing.** Two versions of an image side by side, pixel by pixel, to see what a step
  really changed and which version is better. And when a program drops the plate solve on the
  way, you can put it back.
- **Print.** What the paper will do to your image: whether the faint sky keeps its shades above
  the paper's black, how large you can print, and a soft proof with the paper's own profile.

The tools never change, move or delete the frames of your night.

**Free and open source** (GPL v3 or later) · **Windows 10 and 11** · **No installer**: unzip and
start · **English or Romanian**

**[Download the latest version](https://github.com/tcarean/AstroDarkroom/releases/latest)**: one
zip with the six tools, nothing to install. [Download](#download) below says how to check it and
start it.

## The tools

![The Astro Darkroom launcher: one row for each of the six tools, with what it does, its version and an Open button](https://raw.githubusercontent.com/wiki/tcarean/AstroDarkroom/img/AstroDarkRoom/main.PNG)

The launcher, `AstroDarkroom.exe`, lists the six tools in the order of a night's work and opens
the one you pick ([its manual](https://github.com/tcarean/AstroDarkroom/wiki)).

### [DarksCheck](https://github.com/tcarean/AstroDarkroom/wiki/DarksCheck): can you trust your darks?

Do your darks match your lights? Did light leak in while you took them? Did the camera warm up
during the series? DarksCheck reads the darks of a night, or a library of darks you keep for
other nights, and answers in nine checks, each ending in a coloured verdict: light leaks,
warming, noise, hot and dead pixels, and whether you took enough. It also sums up how warm the
sensor was in one number, so you can pick matching darks from your library for another night.

**[Read the DarksCheck manual](https://github.com/tcarean/AstroDarkroom/wiki/DarksCheck)**, with
pictures.

### [FlatCheck](https://github.com/tcarean/AstroDarkroom/wiki/FlatCheck): does your flat fix the vignetting, and nothing else?

A flat should remove the dark corners and the dust shadows, and add nothing of its own.
FlatCheck takes the master flat Siril made and checks that its shading is symmetric around the
centre, as a lens's is, that its corners are as dark as the lens's published profile says, and
that it really corrected your lights. It also tells you whether your flats were exposed right.
No master flat yet? It runs Siril's script for you.

**[Read the FlatCheck manual](https://github.com/tcarean/AstroDarkroom/wiki/FlatCheck)**, with
pictures.

### [LightsCheck](https://github.com/tcarean/AstroDarkroom/wiki/LightsCheck): which frames to keep, and what to change next time

LightsCheck goes through every light frame of a night and lists the ones to leave out of the
stack, each with its reason: trailed stars, soft focus, clouds. It tells you whether dew formed
on the lens, whether your exposures were long enough, whether you dithered enough to avoid
walking noise, and how well the mount tracked and was polar-aligned. It ends with what to change
next time.

**[Read the LightsCheck manual](https://github.com/tcarean/AstroDarkroom/wiki/LightsCheck)**, with
pictures.

### [ImageVisualCompare](https://github.com/tcarean/AstroDarkroom/wiki/ImageVisualCompare): which version is better?

Drag a line across the image: version A on one side, version B on the other, always at the same
zoom and in the same place. It is made for the questions of processing: did star removal leave
marks, which background extraction is cleaner, which stretch keeps more of the faint dust. For
the pixel under the mouse it shows the exact values in both images, so a difference too faint
for the eye becomes a number you can read.

**[Read the ImageVisualCompare manual](https://github.com/tcarean/AstroDarkroom/wiki/ImageVisualCompare)**,
with pictures.

### [CopyWcs](https://github.com/tcarean/AstroDarkroom/wiki/CopyWcs): give a processed image its plate solve back

Some programs drop the plate solve, the part of the file that says where in the sky the image
points, when they save a processed image. CopyWcs copies it back from the solved stack without
touching a pixel, so that Siril can name the stars and nebulae on it again.

**[Read the CopyWcs manual](https://github.com/tcarean/AstroDarkroom/wiki/CopyWcs)**, with
pictures.

### [AstroPhotoAnalyzer](https://github.com/tcarean/AstroDarkroom/wiki/AstroPhotoAnalyzer): what will the paper do to your image?

A paper's black is never as deep as a screen's, and the faint sky of an astrophoto sits right
where the paper runs out of shades. AstroPhotoAnalyzer checks whether your dark sky and dust
clouds will keep their shades on paper, and how much to lift the background if not. It tells you
how large you can print before the stars stop looking like points, and which file to send to the
lab. With the paper's ICC profile it shows a soft proof: your image as that paper will print it.

**[Read the AstroPhotoAnalyzer manual](https://github.com/tcarean/AstroDarkroom/wiki/AstroPhotoAnalyzer)**,
with pictures.

The limits behind the verdicts come from tests with one camera; raw files from other cameras are
read too.

## Download

The latest release of Astro Darkroom: one zip for Windows 10 or 11 (x64) with the six tools, the
launcher and the .NET 8 runtime inside. No installer, and no .NET to install. Download
`AstroDarkroom-X.Y.Z-win-x64.zip` from the release page:

**<https://github.com/tcarean/AstroDarkroom/releases/latest>**

Unzip it into any folder and start `AstroDarkroom.exe`. FlatCheck needs Siril 1.4.4 or newer,
and Adobe's free lens profiles, installed before its first run; the three tools that read camera
raw files also need Microsoft's Visual C++ runtime, which most computers have and the launcher
checks for: see [What the tools need](#what-the-tools-need).

Before you unzip, check the hash. This command prints the zip's SHA-256, which must be the one on
the release page, beside the zip and in the release's notes; otherwise the download broke or the
file is not the published one:

```
certutil -hashfile AstroDarkroom-X.Y.Z-win-x64.zip SHA256
```

The executables are not signed, so on a new computer Windows SmartScreen asks before the first
start. Every earlier version is on the repository's Releases page, with its own zip and hash.

The launcher and the tools write their logs into a `log` folder beside them; to report a
problem, attach that folder. The [launcher's manual](https://github.com/tcarean/AstroDarkroom/wiki)
says more.

## What the tools need

### A Siril session folder

DarksCheck, FlatCheck and LightsCheck open the folder of one night, laid out the way Siril and
its preprocessing scripts lay it out; FlatCheck also reads what those scripts write:

```
MySessionFolder\
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

FlatCheck writes into the session folder: `Test result.txt` and `flatcheck.png` from its
Structure tab, `flatcheck.json` when you change one of its switches or pick your lens, and the
files of Synthetic and Split when you press their button.

### Siril 1.4.4 or newer

FlatCheck checks the master flat that Siril's preprocessing script makes, so it needs
[Siril](https://siril.org/download/) 1.4.4 or newer, the version the tools were built and
measured against. The other tools run without Siril: DarksCheck and LightsCheck read the camera's
own files from folders named the way Siril names them, which you can also make by hand;
ImageVisualCompare and AstroPhotoAnalyzer open image files from any program; CopyWcs needs a FITS
that Siril has plate-solved. FlatCheck's **Run in Siril** starts `siril-cli.exe` from your
installation (found through its entry in the registry, then in `C:\Program Files\Siril\bin`) with
a script from Siril's own script menu; it proposes `OSC_Preprocessing_WithoutDark.ssf`, from the scripts repository Siril
downloads. It gives `siril-cli` a private copy of your Siril settings, so your own
`config.1.4.ini` is only read, never rewritten.

### Microsoft's Visual C++ runtime

DarksCheck, FlatCheck and LightsCheck read camera raw files through LibRaw, whose DLLs need
Microsoft's Visual C++ v14 runtime for x64. Most computers already have it, installed by other
programs; Windows lists it under installed apps as "Microsoft Visual C++ v14 Redistributable
(x64)", or, when it is older, "Microsoft Visual C++ 2015-2022 Redistributable (x64)",
"2015-2019", "2017" or "2015"; the last two are too old. The zip does not include it. Without
it, or with one too old, those three tools cannot read raw files (their FITS modes still work),
and `AstroDarkroom.exe` says so on its window, with Microsoft's download of the latest version:
<https://aka.ms/vc14/vc_redist.x64.exe>. Once it is installed, the warning goes when you come
back to the launcher.

### For FlatCheck: Adobe's lens profiles

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

Install them before you use FlatCheck for the first time. FlatCheck starts without them, but its
check of the vignetting against the lens then rests on lensfun's profiles alone, measured by
volunteers: on one lens in the tests, both were about a quarter of a stop darker than Adobe's,
enough to turn a PERFECT flat into an ACCEPTABLE one. And a camera that names the lens only by a
number, as a Nikon does, cannot tell FlatCheck which lens it was: FlatCheck then goes by the
lens's range of focal lengths and its aperture alone. With Adobe's profiles, the vignetting check
and the synthetic flat have Adobe's own measurement of your lens, which gives the verdict when
the raw file names your lens. The Adobe programs that bring the profiles:

| Program | Cost | Puts them where FlatCheck reads them |
| --- | --- | --- |
| [Adobe DNG Converter](https://helpx.adobe.com/camera-raw/using/adobe-dng-converter.html) | free | yes |
| Adobe Camera Raw, the raw plug-in installed with Photoshop | subscription | yes |
| Adobe Lightroom Classic, Adobe Lightroom | subscription | yes: each keeps its copy inside its own program folder, such as `C:\Program Files\Adobe\Adobe Lightroom Classic\Resources\LensProfiles\1.0`, which FlatCheck reads |

The simplest way is the free Adobe DNG Converter: install it once and the profiles stay where
FlatCheck finds them; you never need to run the converter itself. It needs no Adobe account, and
the download is about 1.8 GB. The steps, and how to check that FlatCheck found the profiles, are
in [FlatCheck's manual](https://github.com/tcarean/AstroDarkroom/wiki/FlatCheck#installing-adobes-lens-profiles).
FlatCheck only reads the profiles; it neither copies nor redistributes them.

## For developers

Each tool has its own folder in this repository, with its `README.md`, its sources and its
tests, and in `docs/` its requirements, its decisions and its validation figures, all in English.

### Build the suite from a checkout

The zip's folder can be built from this repository as it is now: the launcher and the window of
every tool, each from its latest commit, with one copy of the .NET 8 runtime. The build changes
no tracked file and commits nothing.

#### What the build needs

- Windows 10 or 11, x64.
- [Git for Windows](https://git-scm.com/download/win).
- The [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0), version 9.0.318 or a later
  9.0.3xx: the `global.json` of every project in the suite asks for 9.0.318 and accepts only a
  later patch of that feature band. `dotnet --list-sdks` shows what is installed. The tools
  target .NET 8; this SDK builds them, and the build puts the .NET 8 runtime into the folder.
- Windows PowerShell 5.1, part of every Windows 10 and 11.
- Internet access the first time, for the NuGet packages and the .NET 8 runtime packs.

Visual Studio is not needed.

#### Steps

**1. Get the sources**, into a folder with a short path (see "A short path" below):

```
git clone https://github.com/tcarean/AstroDarkroom.git C:\src\photo
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

The folder takes about 160 MB, and its zip about 65 MB.

#### What the command does

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
  SixLabors.ImageSharp 3.1.11. They are known and do not stop the build; the two tools'
  `docs/decisions/` say why.

**A short path.** Windows refuses paths longer than 260 characters unless long paths are enabled,
and the deepest path the build makes is 161 characters below the repository's root. Clone into a
folder whose path is shorter than about 95 characters, such as `C:\src\photo`.

#### The consoles and the tests

The suite holds the windows only. DarksCheck, FlatCheck, LightsCheck, CopyWcs and
AstroPhotoAnalyzer also have a console for scripted use (`darkscheck.exe`, `flatcheck.exe`,
`lightscheck.exe`, `copywcs.exe`, `AstroPhotoAnalyzer.exe`), which cannot share the suite's
folder. The same command, `scripts/release.ps1 -Version X.Y.Z -PublishOnly`, run in a tool's own
folder, publishes that tool alone, its console included, into the tool's `artifacts\`. In each
tool's folder, `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1` checks
the format, makes a strict build and runs the tests.

## Licence

Every tool and the launcher are free software, under the GNU General Public License version 3 or
(at your option) any later version: see the `LICENSE` file in each folder. What a tool ships from
others is listed, with its licence, in the folder's `THIRD-PARTY-NOTICES.md`, where it has one;
the zip holds all of it, each licence's whole text included, in `THIRD-PARTY-NOTICES.txt` beside
`AstroDarkroom.exe`.
