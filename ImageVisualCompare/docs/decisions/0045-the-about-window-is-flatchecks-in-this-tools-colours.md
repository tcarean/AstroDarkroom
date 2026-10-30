# 0045. The About window is FlatCheck's, in this tool's colours

Date: 2026-10-30
Status: accepted 2026-10-30
Supersedes: 0023 in part (the banner heading About, in its title and on its first line); 0024 in part (About as `VersionInfo.About`, a text, in `MessageDialog`)

## Context

The About button of ImageVisualCompare has shown plain text since 1.0.0, in the application's own
`MessageDialog` since 1.1.1 (ADR 0018, ADR 0024): the banner, the copyright, the notice of GPLv3
§5(d) and, since the diagnostic log, three lines on NLog (ADR 0043), fourteen lines in all. The
other five windows of the Astro Darkroom suite, FlatCheck, LightsCheck, DarksCheck, CopyWcs and
AstroPhotoAnalyzer, show FlatCheck's About window: the icon's largest frame beside the name, a
tagline, the version and the copyright, a `LICENCE` card with the notice and a link, a card of the
components written by others, one row each, and `Close`, all in FlatCheck's palette. On
2026-10-30 the owner asked why this tool's About looks different from FlatCheck's and to make it
look the same; asked which colours, the owner chose this tool's own over FlatCheck's.

## Decision

- **A window of its own, FlatCheck's layout.** `AboutWindow`, 600 DIPs wide and as tall as its
  content, modal, centred on the main window, not in the taskbar, not resizable: the icon's
  256-pixel frame at 72 DIPs beside the name at 22 SemiBold, the tagline, the banner in Consolas
  and the copyright; the `LICENCE` card, the notice as one paragraph and the link to the GPL's text;
  the `THIRD-PARTY COMPONENTS` card, a paragraph and FlatCheck's row for each component of
  `ThirdParty.Components`; `Close`, the default and the cancel button, so Enter and Esc close it
  too (R-072, R-075). The title has no version, as FlatCheck's; the banner under the name has it,
  where ADR 0023 had it head About, in its title and on its first line.
- **This tool's colours.** FlatCheck's styles under FlatCheck's names (`Card`, `Heading`, `Muted`,
  `Faint`, the hyperlink, `ComponentRowTemplate`) join `Theme.xaml` in this tool's palette: the
  window in `ChromeBrush`, the cards a shade lighter (`PanelBrush`, as much lighter than the
  window as FlatCheck's are), the faint text the muted text darkened as FlatCheck's is, the links
  in the turquoise accent, `Close` the dialog button of `MessageDialog`.
- **The words stay `UiText`'s** (ADR 0038): `AboutViewModel` copies them, the version's and the
  components' facts into public properties the XAML binds, and words nothing itself. The window
  writes one log line, at `Debug`, for a link the system will not open (ADR 0043).
- `MessageDialog` keeps saying and asking (ADR 0018); About is no longer one of the things it says.

## Alternatives rejected

- **FlatCheck's colours.** The owner chose this tool's: the window opens over this tool's main
  window, whose chrome is bluish and whose accent is the turquoise of the icon's divider.
- **The text in `MessageDialog`, made longer.** It is what the owner asked to change: a dialog of
  plain lines cannot show the icon, the cards or the links the other windows show.
- **A shared About window, in a library the six tools reference.** The tools copy their small
  pieces rather than share a package with a version (CopyWcs's ADR 0007); the markup is ported.

## Consequences

- `VersionInfo.About` and `UiText.AboutNotice` go, with the tests that read them; R-063, R-072,
  R-074, R-075 and R-101 say the window. `AboutWindowTests` checks the view model's words against
  `VersionInfo`, `UiText` and `ThirdParty`, and builds the window on the WPF test thread and lays
  it out, never shown, which loads the card's row template too, so a missing resource, style key
  or binding of the markup fails there (ADR 0032); it reads the row as drawn, Close's Enter and
  Esc and both links. `UiTextTests` reads the Romanian. How it looks is checked by launching the
  application.
- The icon becomes a WPF resource of the application as well as its Win32 icon, so the window can
  decode its largest frame.
- A change to FlatCheck's About layout is ported here by hand, in this tool's colours.
