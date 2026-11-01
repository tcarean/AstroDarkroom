# 0027. About lists the libraries inside LibRaw's DLLs apart, points to the suite's licence texts and fits its screen

Date: 2026-11-01
Status: accepted 2026-11-01
Supersedes: 0009 in part (the About window improved in LightsCheck first)

## Context

The Astro Darkroom suite's zip carries every third-party licence text in
`THIRD-PARTY-NOTICES.txt` at its root (AstroDarkroom's ADR 0014, its R-017). The sections of that
file say which program ships each component. For DarksCheck they are NLog, Sdcb.LibRaw, LibRaw,
libjpeg-turbo, Little CMS, zlib, and .NET 8 and WPF. About listed three components, Sdcb.LibRaw,
LibRaw and NLog, said there were two, and sent the reader to `THIRD-PARTY-NOTICES.md`.

What the own release ships was measured on 2026-11-01 (`kit.json`'s two publishes): each program
is a single file with LibRaw's four DLLs inside, beside its `.pdb` and `.xml`. There is no notices
file and no `LICENSE`. The suite publishes the window flat, with `THIRD-PARTY-NOTICES.txt` beside
it.

Six rows grow About from about 750 DIP to about 1030, its frame included. That was measured at
its 584 DIP client width, before the two Romanian roles below were shortened. A 1080-line screen
at 125 % leaves a work area of about 825 DIP, so About would fall off it, where it fitted
before. AstroPhotoAnalyzer met the same in its ADR 0070.

On 2026-11-01 the owner chose two things, each as recommended:

- six components, with no row for .NET;
- About fitted to its screen in this task, with AstroPhotoAnalyzer's design.

## Decision

- **Six components, in this order:** Sdcb.LibRaw, LibRaw (`raw_r.dll`), libjpeg-turbo
  (`jpeg8.dll`), Little CMS (`lcms2.dll`), zlib (`zlib1.dll`) and NLog (R-120).
  - They are what the suite's notices name for DarksCheck, but .NET 8 and WPF. That is the
    platform, whose texts travel as `DOTNET-LICENSE.txt` and `DOTNET-THIRD-PARTY-NOTICES.txt` in
    the suite.
  - Each role names its DLL.
  - The three libraries claim LibRaw's Windows package together with LibRaw (R-010).
- **The IJG statement.** libjpeg-turbo's licence line quotes "this software is based in part on
  the work of the Independent JPEG Group". The IJG licence asks it of the documentation of a
  program distributed as executables, and the single file has no other documentation. It is
  quoted in English in both languages, as the licence words it.
- **The lead counts from the list,** in words (`WindowTexts.CountWord`, as FlatCheck's), so the
  count cannot drift from the rows again.
- **The pointer names both places,** since a single file name is true in only one of them:
  `THIRD-PARTY-NOTICES.txt` beside the program in the Astro Darkroom folder, and
  `AstroDarkroom/THIRD-PARTY-NOTICES.md` in the source repository, with the repository's
  address. DarksCheck's own `THIRD-PARTY-NOTICES.md` gains a section for each library and points
  to the suite's file for the whole texts.
- **The fit (R-121)** is AstroPhotoAnalyzer's design (its ADR 0070, after the launcher's ADR
  0013). It is taken into the window copied from LightsCheck as a hunk of its own, for
  LightsCheck to take back. That is a fix made here before LightsCheck has it, so this supersedes,
  for the About window, ADR 0009's "LightsCheck's to improve first". The hunk:
  - the window's height is capped at the work area of the screen the main window is on;
  - the cards scroll inside a `ScrollViewer`, by the wheel, and from the window's
    `PreviewKeyDown` while they can scroll;
  - the Close button is in a row of its own, so it stays in view;
  - at `ContentRendered`, the top is kept inside the area and one Debug line is written.
- **`ScreenArea` is DarksCheck's own.** It holds the launcher's query, with `LibraryImport` as
  `DarkTitleBar` uses it, and AstroPhotoAnalyzer's `TopInside`.
- **Two Romanian roles shortened.** The roles of libjpeg-turbo and zlib lost the word
  "comprimate" ("compressed"), so that each Romanian row fits on one line. The Romanian window
  then fits a 1040 DIP area, as the English one does. Before that it needed 1045 DIP, and its
  cards scrolled by 5 at 100 % on a 1080-line screen.
- **Measured on 2026-11-01.** This machine at 100 %, the window shown off the screen and not
  activated, at the work areas of the launcher's arithmetic:

  | work area | English | Romanian |
  | --- | --- | --- |
  | 1040 DIP (1080 lines at 100 %) | 1015 DIP high, nothing scrolls | 1015, nothing scrolls |
  | 825 DIP (125 %) | 825, the cards scroll by 190 | 825, by 190 |
  | 728 DIP (1366x768 at 100 %) | 728, by 287 | 728, by 287 |
  | 690 DIP (150 %) | 690, by 325 | 690, by 325 |

  The Close button ends 20 DIP above the client area's bottom in every case.

## Alternatives rejected

- **A seventh row for .NET 8 and WPF.** It is the platform, and R-010's binding would then have to
  admit a component that no package of the core brings.
- **One row for LibRaw and its three libraries.** Each has its own licence and copyright, which
  the row could not hold.
- **A single link to the repository's file.** It needs a network, and it shows `main`, not the
  version in hand.
- **A notices file beside the own release's executables.** The suite's flat folder holds one
  notices file for every program, and no file of any one tool.
- **The fit in a later task, LightsCheck first.** About would fall off a 125 % screen from this
  merge on.
- **Shorter rows only, the licence and the copyright on one line.** That saves six lines of
  11 px text, about 90 DIP, against the 190 DIP by which the cards overflow at 825.

## Consequences

- **`ThirdPartyTests` binds the list** to three things:
  - to the suite's notices: the sections that name DarksCheck, but .NET 8 and WPF. It reads
    `../AstroDarkroom/THIRD-PARTY-NOTICES.md`, so a component the suite adds for DarksCheck
    turns DarksCheck's gate red until About lists it;
  - to the DLLs of LibRaw's Windows package in the build output;
  - to the IJG statement, in both languages.
- **`AboutTests`** checks the lead's count and both places, in both languages.
- **`AboutWindowFitTests`** are AstroPhotoAnalyzer's tests of the fit, ported.
- **ADR 0025's** "ThirdPartyTests counts three" gets a dated note: it counts six now.
