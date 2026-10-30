# 0013. "Despre" lists the third-party components the launcher ships

Date: 2026-10-30
Status: proposed

## Context

Until the diagnostic log (ADR 0012) the launcher linked nothing written by others. NLog and its
file target, `NLog.Targets.AtomicFile`, come under BSD-3-Clause, whose binary condition asks the
copyright notice, the conditions and the disclaimer to go with the distribution, in its
documentation or other materials. Every tool of the suite that ships NLog (FlatCheck, LightsCheck,
and ImageVisualCompare and CopyWcs on their branches of the same day) lists it in its "Despre"
window under `THIRD-PARTY COMPONENTS` / `COMPONENTE TERTE`, with its role, licence, copyright line
and address, and keeps the whole text in a `THIRD-PARTY-NOTICES.md` beside the source, which a
test binds to the packages; none puts that file into the suite folder. The approved plan named a
third-party row in the README only. The choice was taken in the session, without the owner, and
waits for the owner's yes at the merge.

## Decision

"Despre" gets a card after the licence's, `THIRD-PARTY COMPONENTS` / `COMPONENTE TERTE`: one lead
line saying where the whole text is, then each component's name and role on one line, its
licence and address on the next, its copyright line under them, from `ThirdParty.Components` in
the core (R-015); kept that short because the window grows by it on every screen. The name, the
licence's identifier, the copyright line and the address are the same in both languages, like the
product name; the role and the lead are pairs. `THIRD-PARTY-NOTICES.md`, at the project's root,
holds each component's section with its licence's whole text, word for word as FlatCheck's has it
for the same packages, and why the licence is compatible with the GPLv3. `ThirdPartyTests` checks
that the list's entries claim each package of the projects under `src/` exactly once and that
each has a section in the file carrying its copyright line.

## Alternatives rejected

- **The README's list alone, as the plan said.** The README does not travel with the suite folder
  either, and the launcher would be the one window of the suite that ships NLog without naming it.
- **`THIRD-PARTY-NOTICES.md` beside the executables.** The suite folder is flat: another tool's
  file of the same name with other bytes would stop the release at `merge-publish.ps1`, and the
  tools keep theirs out of it.
- **Nothing in the launcher, since FlatCheck's "Despre" names NLog already.** The launcher's
  window links the DLLs itself, and the suite may one day bundle no tool that names them.

## Consequences

"Despre" is a card taller. Measured on 2026-10-30 by laying the window out offscreen (a scratch
program over the Release build, the suite folder's `release.json`, the chrome of a 96 DPI
screen), it goes from about 785 DIP high to about 931 DIP in Romanian and 946 in English; a
first version of the card, with a two-line lead and the licence, the copyright and the address
on lines of their own, made it about 974 and 989. It fits a 1080p screen at 100 % (1040 DIP of
work area, measured here) and, by the arithmetic of the scaling, no longer fits one at 125 %
(about 825 DIP), where it did; at 150 % (about 690) it did not fit before either. The window has
no scroll bar: Enter and Esc still close it. A new package the
launcher links is a component in `ThirdParty.cs` and a section in `THIRD-PARTY-NOTICES.md`, or
`ThirdPartyTests` fails; `Directory.Packages.props` says so beside the packages.
