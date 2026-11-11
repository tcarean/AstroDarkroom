# 0024. The launcher says that FlatCheck alone needs Siril

Date: 2026-11-11
Status: accepted 2026-11-11

## Context

The launcher's line under its subtitle said "The tools read a Siril session folder and expect
Siril 1.4.4 or newer", and the second paragraph of "Despre" that "the masters and the stacks the
tools check come from its scripts"; the first paragraph had ImageVisualCompare, CopyWcs and
AstroPhotoAnalyzer open "the files exported from Siril". Those wordings date from ADR 0007. On
2026-11-11 the suite's wiki, on the owner's approval, and then the front page (ADR 0021) were
made exact: FlatCheck alone needs Siril, since it checks the master flat Siril's preprocessing
script makes and can run that script; DarksCheck and LightsCheck read the camera's own files from
folders named the way Siril names them, which can be made by hand; ImageVisualCompare and
AstroPhotoAnalyzer open image files from any program, and CopyWcs needs a FITS Siril
plate-solved. Asked whether the launcher should say the same, the owner said yes ("aliniaza").

The line sits in the top bar's text column, 568 px wide (ADR 0017), in the `Faint` style, Segoe
UI 11. Measured on 2026-11-11 with WPF's own text layout (`FormattedText`, `Display` formatting,
96 DPI, in Windows PowerShell), the old and the new wordings:

```
 449.0 px   91 ch  The tools read a Siril session folder and expect Siril 1.4.4 or newer; details under About.
 441.0 px   94 ch  Uneltele citesc folderul unei sesiuni Siril si cer Siril 1.4.4 sau mai nou; detalii la Despre.
 516.0 px  104 ch  Three tools read a Siril session folder; only FlatCheck needs Siril 1.4.4 or newer. Details under About.
 531.0 px  111 ch  Trei unelte citesc folderul unei sesiuni Siril; doar FlatCheck cere Siril 1.4.4 sau mai nou. Detalii la Despre.
```

## Decision

- The line under the subtitle says that three tools read a Siril session folder and that only
  FlatCheck needs Siril 1.4.4 or newer, and points at "Despre", on one line in both languages.
- In "Despre", the first paragraph says what the other three open (image files from any
  program; a FITS Siril plate-solved), and the second that Siril is for FlatCheck, why, and that
  the other tools run without it. The README's "Requirements" says the same, as ADR 0007 asks.
- R-009 and its test hold the summary's opening words, that it names FlatCheck, and the
  sentences "from any program" and "The other tools run without Siril" in both languages.

## Alternatives rejected

- **Keeping "expect Siril 1.4.4 or newer" for the whole suite.** It tells a reader who uses only
  ImageVisualCompare or AstroPhotoAnalyzer to install a program they do not need, and it
  contradicts the front page and the wiki.
- **Naming the three tools in the line** ("DarksCheck, FlatCheck and LightsCheck read ...").
  The rows under it already name every tool, and the line would grow toward the column's width.

## Consequences

The new line takes 531 px of the 568 at most, in Romanian; a longer wording would wrap to a
second line, so it is measured again when it changes. The window was not looked at running: the
widths come from the layout above, which matched the screenshot's 449 px line.
