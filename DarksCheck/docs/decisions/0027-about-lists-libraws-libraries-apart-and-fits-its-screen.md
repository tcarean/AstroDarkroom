# 0027. About lists the libraries inside LibRaw's DLLs apart, under LightsCheck's lead, and fits its screen as LightsCheck's does

Date: 2026-11-01
Status: accepted 2026-11-02

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

Six rows grow About from about 750 DIP to about 1000 or more, its frame included. A 1080-line
screen at 125 % leaves a work area of about 825 DIP, so About would fall off it, where it fitted
before. AstroPhotoAnalyzer met the same in its ADR 0070.

On 2026-11-01 the owner chose six components, with no row for .NET, and About fitted to its
screen in this task with AstroPhotoAnalyzer's design. This task's first version took that design
into DarksCheck's About window as a hunk of its own, and counted the list in code
(`WindowTexts.CountWord`). During the task's third review, on 2026-11-01, LightsCheck merged its
own answer to the same request: its lead in the core, with the count written out (its ADR 0030),
and the same fit (its ADR 0031). DarksCheck's About window, its view-model and their test fixture
are LightsCheck's copies (ADR 0009). On 2026-11-02 the owner chose to take LightsCheck's pieces,
and a fixed list of the six in place of a test that read the suite's notices.

## Decision

- **Six components, in this order:** Sdcb.LibRaw, LibRaw (`raw_r.dll`), libjpeg-turbo
  (`jpeg8.dll`), Little CMS (`lcms2.dll`), zlib (`zlib1.dll`) and NLog (R-120).
  - They are what the suite's notices name for DarksCheck, but .NET 8 and WPF. That is the
    platform, whose texts travel as `DOTNET-LICENSE.txt` and `DOTNET-THIRD-PARTY-NOTICES.txt` in
    the suite.
  - Each role names its DLL. The rows' words are DarksCheck's own, in `ThirdParty.cs`.
  - The three libraries claim LibRaw's Windows package together with LibRaw (R-010).
- **The IJG statement.** libjpeg-turbo's licence line quotes "this software is based in part on
  the work of the Independent JPEG Group". The IJG licence asks it of the documentation of a
  program distributed as executables, and the single file has no other documentation. It is
  quoted in English in both languages, as the licence words it.
- **The lead in the core, LightsCheck's.** `ThirdParty.Lead` replaces the window's own lead. It
  says "six" in words, which a test holds to the list's length, and it points to each licence's
  whole text: `THIRD-PARTY-NOTICES.txt` beside the program in the Astro Darkroom suite folder, and
  `AstroDarkroom/THIRD-PARTY-NOTICES.md` in the source. Both are true from either form of the
  program, since the own release is built from the repository that holds the second.
- **The About window, its view-model and `ScreenArea` are LightsCheck's copies,** their comments
  DarksCheck's. The fit is LightsCheck's (its ADR 0031, after AstroPhotoAnalyzer's ADR 0070):
  - the window's height is capped at the work area of the screen the main window is on;
  - the cards scroll inside a `ScrollViewer`, by the wheel, and from the window's
    `PreviewKeyDown` while they can scroll;
  - the Close button is in a row of its own, so it stays in view;
  - at `ContentRendered`, the top is kept inside the area and one Debug line is written;
  - `ScreenArea.ToDips` turns Windows' pixels into the window's, tested at 125 % and 150 %.

  ADR 0009's rule for the copies holds unchanged: nothing here is improved before LightsCheck.
- **A fixed list.** `ThirdPartyTests` holds the six names in their order. Its package test and
  its DLL test already catch a new component on DarksCheck's side.
- **DarksCheck's notices file** gains a section for each library and points to the suite's file
  for the whole texts.
- **Measured on 2026-11-02.** This machine at 100 %, the window shown off the screen and not
  activated, at the work areas of the launcher's arithmetic:

  | work area | English | Romanian |
  | --- | --- | --- |
  | 1040 DIP (1080 lines at 100 %) | 1000 DIP high, nothing scrolls | 1000, nothing scrolls |
  | 825 DIP (125 %) | 825, the cards scroll by 190 | 825, by 190 |
  | 728 DIP (1366x768 at 100 %) | 728, by 287 | 728, by 287 |
  | 690 DIP (150 %) | 690, by 325 | 690, by 325 |

  The Close button ends 20 DIP above the client area's bottom in every case. Two Romanian roles,
  libjpeg-turbo's and zlib's, were shortened by the word "comprimate" ("compressed") in the
  first version, when the Romanian window needed 1045 DIP and its cards scrolled by 5 at 100 %.

## Alternatives rejected

- **A seventh row for .NET 8 and WPF.** It is the platform, and R-010's binding would then have
  to admit a component that no package of the core brings.
- **One row for LibRaw and its three libraries.** Each has its own licence and copyright, which
  the row could not hold.
- **AstroPhotoAnalyzer's fit as DarksCheck's own hunk, and the count worked out in code** (this
  task's first version). The About window would differ from its source on purpose, and the next
  refresh of its view-model would bring LightsCheck's lead back. LightsCheck's ADR 0030 rejected
  a count composed in code: a number word in two languages needs a table for a sentence that
  changes only when a package does.
- **A test that reads the suite's notices.** A commit that touches only AstroDarkroom runs only
  AstroDarkroom's gate (the kit's ADR 0004), so it could leave DarksCheck's gate red unseen.
- **A single link to the repository's file.** It needs a network, and it shows `main`, not the
  version in hand.
- **A notices file beside the own release's executables.** The suite's flat folder holds one
  notices file for every program, and no file of any one tool.
- **The fit in a later task.** About would fall off a 125 % screen from this merge on.

## Consequences

- **`ThirdPartyTests`** holds the six names in their order, the lead's word to the list's length
  in both languages, each DLL of LibRaw's Windows package in exactly one role, and the IJG
  statement in both languages.
- **`AboutTests`** holds the view-model's lead to the core's.
- **`AboutWindowFitTests`** are LightsCheck's tests of the fit, `ToDips` included.
- **ADR 0025's** "ThirdPartyTests counts three" gets a dated note: it counts six now.
