# 0018. The main window shows the copyright where the folder's path was

Date: 2026-11-02
Status: accepted 2026-11-02

## Context

The left of the main window's bottom bar showed the folder the launcher runs from, without its
trailing separator, and after it, when `release.json` gives a date, `  -  release of <date>`
(`  -  release din <date>`): from the suite folder of 0.9.0, for example,
`E:\Photografie\Code\AstroDarkroom\artifacts\0.9.0  -  release of 2026-10-31`, cut with an
ellipsis when longer than the bar's first column. The suite's copyright,
`Copyright (C) 2026 Tudor Carean`, was shown only in "Despre", under the banner. On 2026-11-02
the owner asked for their copyright in that place instead of the path and, asked whether the
release's date should stay after it, chose the copyright alone.

## Decision

The left of the bottom bar shows `VersionInfo.Copyright`, the `<Copyright>` of
`Directory.Build.props`, the same words in both languages and whatever `release.json` says
(R-019). The path and the date leave the main window: the log keeps the suite folder (R-012),
"Despre" the date, on its release line beside the components' versions, and the top bar the
version.

## Alternatives rejected

- **The copyright followed by the release's date** (`Copyright (C) 2026 Tudor Carean  -  release
  of 2026-10-31`). The owner chose the copyright alone; the date stays one click away, in
  "Despre", beside the components' versions.
- **The banner and the copyright on one line, as ImageVisualCompare's status bar shows them**
  (`ImageVisualCompare 1.1.1 · Copyright (C) 2026 Tudor Carean`). The launcher's top bar already
  shows the banner, in larger type, above the rows.
- **A Romanian wording through `T`, such as `Drepturi de autor (C) 2026 Tudor Carean`.** The
  notice is the one line GPLv3 asks every copy to carry, written once in `Directory.Build.props`;
  "Despre" has always shown it unchanged in both languages, as it shows the third-party
  components' copyright lines (ADR 0013).

## Consequences

The line no longer changes with the language or with `release.json`, so the constructor sets
it once, beside the banner, and `Relabel` no longer touches it. Two copies of the suite in two
folders now look the same in the window; which folder a launcher runs from is in its log, on the
start's `language=` line (`suite folder=<dir>`) and at every build of the rows
(`suite folder <dir>: N of 6 apps present`), where a bug report reads it. A `release.json` that
names no component besides the launcher, which only a hand edit makes, now has its date in no
window: "Despre" shows the date only with the components' versions; the log keeps it (`date=`).

`R019_TheBottomBarsCopyright_IsTheNoticeOfDirectoryBuildProps_TheSameInBothLanguages` checks the
core's part: the notice is the `<Copyright>` of `Directory.Build.props`, the same words whatever
the language, so a notice translated in the core fails it. It passed before the window changed,
since the core already had the notice. That the window shows the notice, at the left of the bar
and untranslated, no test checks: the window has no test project (ADR 0001), so, like the
subtitle of ADR 0017, it was checked by rendering it. On 2026-11-02 the new build's
`MainWindow`, made in each language by a throwaway harness and shown off-screen (never
activated, no taskbar button) over the suite folder of 0.9.0, gave:

```
en: window 680x848, client 664x809, dpi scale 1; line "Copyright (C) 2026 Tudor Carean" at x 20 y 777, column 347 px, text 167.0 px (Segoe UI 11); selector at x 379
ro: window 680x848, client 664x809, dpi scale 1; line "Copyright (C) 2026 Tudor Carean" at x 20 y 777, column 340 px, text 167.0 px (Segoe UI 11); selector at x 372
```

The launcher itself, started from a suite folder published from this branch
(`scripts/release.ps1 -Version 1.0.0 -PublishOnly`) and driven through UI Automation alone, gave
the same: the line `Copyright (C) 2026 Tudor Carean` at 347 × 13 px in English, 340 × 13 px
after a switch to Romanian, 347 × 13 px again after the switch back, in a window of 680 × 848 px;
its log ends with `Process exit 0 after 4.1 s` and has no `WARN` or `ERROR` line.

The column is narrower in Romanian, where `Despre` is wider than `About`. The window keeps the
848 px of ADR 0017: the notice takes one line, as the path did. A notice up to 340 px, about
twice today's, fits: with a range of years, `Copyright (C) 2026-2027 Tudor Carean`, it takes
195 px (WPF's `FormattedText` in Windows PowerShell, Segoe UI 11, `Display` formatting, 96 DPI,
which gives today's notice its 167.0 px). A wider one is cut with an ellipsis, so a changed
notice is measured again.
