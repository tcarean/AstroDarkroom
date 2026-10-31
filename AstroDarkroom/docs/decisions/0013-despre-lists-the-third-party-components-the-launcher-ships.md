# 0013. "Despre" lists the third-party components the launcher ships, and fits its screen

Date: 2026-10-30
Status: accepted 2026-10-30; the notices file kept beside the source for the launcher's components superseded by 0014

## Context

Until the diagnostic log (ADR 0012) the launcher linked nothing written by others. NLog and its
file target, `NLog.Targets.AtomicFile`, come under BSD-3-Clause, whose binary condition asks the
copyright notice, the conditions and the disclaimer to go with the distribution, in its
documentation or other materials. Every tool of the suite that ships NLog (FlatCheck, LightsCheck,
and ImageVisualCompare and CopyWcs on their branches of the same day) lists it in its "Despre"
window under `THIRD-PARTY COMPONENTS` / `COMPONENTE TERTE`, with its role, licence, copyright line
and address, and keeps the whole text in a `THIRD-PARTY-NOTICES.md` beside the source, which a
test binds to the packages; none puts that file into the suite folder. The approved plan named a
third-party row in the README only; the card was taken in the session and put to the owner.

The card makes the window taller. Measured on 2026-10-30 by laying the window out offscreen (a
scratch program over the Release build, the suite folder's `release.json`, the chrome of a 96 DPI
screen), it goes from about 785 DIP high to about 931 DIP in Romanian and 946 in English; a first
version of the card, with a two-line lead and the licence, the copyright and the address on lines
of their own, made it about 974 and 989. It fits a 1080p screen at 100 % (1040 DIP of work area,
measured here) and, by the arithmetic of the scaling, no longer fits one at 125 % (about 825
DIP), where it did; at 150 % (about 690) it did not fit before either, and nothing in the window
scrolled. On 2026-10-30 the owner chose, of the three options put to them, to keep the card and
make the window fit its screen.

## Decision

- **The card.** "Despre" gets a card after the licence's, `THIRD-PARTY COMPONENTS` /
  `COMPONENTE TERTE`: one lead line saying where the whole text is, then each component's name and
  role on one line, its licence and address on the next, its copyright line under them, from
  `ThirdParty.Components` in the core (R-015); kept that short because the window grows by it on
  every screen. The name, the licence's identifier, the copyright line and the address are the
  same in both languages, like the product name; the role and the lead are pairs.
  `THIRD-PARTY-NOTICES.md`, at the project's root, holds each component's section with its
  licence's whole text, word for word as FlatCheck's has it for the same packages, and why the
  licence is compatible with the GPLv3. `ThirdPartyTests` checks that the list's entries claim
  each package of the projects under `src/` exactly once, that each has a section in the file
  carrying its copyright line, and that NLog's section holds the BSD conditions and disclaimer.
- **The window fits its screen** (R-016). Its height is capped at the work area of the screen
  the main window is on, which `ScreenArea` asks Windows for, since WPF knows only the primary
  screen's; its cards are in a scroll viewer, with LightsCheck's dark scroll bar, that the mouse
  wheel scrolls anywhere over it and the arrow, page, Home and End keys scroll from the window,
  and its Close button is in a row of its own under them, so it stays in view. WPF already places
  a window centred on its owner inside the owner screen's work area, its top limit applied last
  (`Window.CalculateWindowLocation` in .NET 8, read on 2026-10-30), which is why the bottom, not
  the title bar, used to leave a small screen; once the window is laid out, `WindowFit.Top` in
  the core holds that place as a guard, so R-016 does not rest on WPF's placement alone. On a
  screen where the window fits, nothing changes.

## Alternatives rejected

- **The README's list alone, as the plan said.** The README does not travel with the suite folder
  either, and the launcher would be the one window of the suite that ships NLog without naming it.
- **`THIRD-PARTY-NOTICES.md` beside the executables.** The suite folder is flat: another tool's
  file of the same name with other bytes would stop the release at `merge-publish.ps1`, and the
  tools keep theirs out of it.
- **Nothing in the launcher, since FlatCheck's "Despre" names NLog already.** The launcher's
  window links the DLLs itself, and the suite may one day bundle no tool that names them.
- **The taller window as it is.** Its bottom, with the Close button and the components'
  versions, would leave a 125 % screen; WPF keeps the top of a window centred on its owner on
  the screen, not its bottom.
- **The card dropped, as the plan had it.** The 150 % screen had no room for the window before.
- **The primary screen's work area (`SystemParameters.WorkArea`).** It is the wrong screen when the
  main window is on another one, of another size, height or place.

## Consequences

"Despre" is a card taller, about 931 DIP in Romanian and 946 in English where it fits, and as tall
as the work area where it does not: measured offscreen on 2026-10-30, a work area of 700 DIP gives
a window of 700 DIP whose cards scroll by 246 DIP, the Close button under them. `ScreenArea` gave,
for hidden windows placed over the owner's three screens, each screen's own work area (`0,0`,
`-1920,0` and `1920,0`, each 1920x1040). A new package the launcher links is a component in
`ThirdParty.cs` and a section in `THIRD-PARTY-NOTICES.md`, or `ThirdPartyTests` fails;
`Directory.Packages.props` says so beside the packages. The window's fit runs in no test (ADR
0001): `WindowFit.Top` is tested in the core, the rest is checked by opening the window on a
small screen. The scroll bar style is `App.xaml`'s implicit one, LightsCheck's whole, so a
horizontal bar elsewhere gets its own form.
