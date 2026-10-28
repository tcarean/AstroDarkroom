# 0008. The window has the siblings' top band: the banner and the copyright, a short explanation of the tool in its middle, and the window's only About button

Date: 2026-10-28
Status: accepted 2026-10-28

## Context

On 2026-10-28 the owner asked for the band FlatCheck has at the top of its window (the name
with the version, the copyright, the About button) and for a short explanation of what CopyWcs
does, in both interface languages. FlatCheck's band, which DarksCheck, LightsCheck and
AstroPhotoAnalyzer share (DarksCheck and LightsCheck with a further column of settings before
the buttons), is a `Border` on `PanelBg` with a 1 px `BorderBr` line under it:
`VersionInfo.Banner` over the copyright on the left, what the window works on in the middle
(the session folder; AstroPhotoAnalyzer's image and ICC profile), the buttons on the right with
About last, run by `ApplicationCommands.Help` (F1) and carrying a tooltip. CopyWcs has no
folder, so the middle of its band is free; its About button sat in the row of Preview and Copy.
Two placements of the explanation were shown to the owner as mockups at the window's 800 px
width: in the middle of the band, where it takes two lines, or as a paragraph under the band,
above the source.

## Decision

The window gets the siblings' band, edge to edge above its form: `copywcs <version>` over the
copyright line (the console's lines 1 and 2, R-001, from the core), the explanation in the
middle in the muted style, wrapped, and on the right the About button, which leaves the row of
Preview and Copy, so the window has one. The owner chose the middle of the band on 2026-10-28.
The explanation is a label of the window, a `{l:T En='...', Ro='...'}` pair in XAML: like the
About window's tagline (a `T(...)` pair of `AboutViewModel`), it describes the tool, not a run,
so it is not one of the sentences about the copy that ADR 0007 keeps in the core. The tooltip
drops FlatCheck's "components": this About window has no third-party card.

## Alternatives rejected

- **The explanation under the band, above the source.** The middle of the band would stay empty
  and the form would give one more row to the text; the owner chose the band.
- **About in both places, the band and the row of buttons.** Two buttons for one window; every
  sibling has one, in its band.
- **The name without the version (`CopyWcs`), or the icon in the band.** FlatCheck's band shows
  the console's banner (`flatcheck <version>`) and no icon, and the owner pointed at
  FlatCheck's; AstroDarkroom's launcher band has an icon.
- **The explanation in the core, beside the console's usage line.** Only the window shows it;
  the console's usage keeps its own one-line wording, and a label of the window is a pair in its
  XAML (ADR 0007).

## Consequences

- R-038 describes the band; R-030 loses About from the row of buttons, and R-035 opens About
  from the band or F1. `WindowSmokeTests` find the band's texts in both languages, the banner
  and the copyright equal to the console's first two lines, and one About button, in the band,
  run by Help; the band's Romanian words join the R-036 list.
- The band is a copy of FlatCheck's markup and is marked so in `MainWindow.xaml`'s header, like
  the other copied pieces (ADR 0007).
- The window opens taller by the band's height, so the outcome card keeps the room it had
  (`docs/design/window.md`).
