# Versioning and licence

> Moved unchanged on 2026-10-02 (ADR 0001) from `CLAUDE.md`, "Versioning" and "Licensing". Where a
> paragraph became a decision record, its number is given beside it.
> **Covers:** `src/ImageVisualCompare.Wpf/Version.cs`,
> `Directory.Build.props`,
> `tests/ImageVisualCompare.Tests/VersionTests.cs`,
> `tests/ImageVisualCompare.Tests/LicenseHeaderTests.cs`,
> `src/ImageVisualCompare.Wpf/MainWindow.xaml`,
> `src/ImageVisualCompare.Wpf/ImageVisualCompare.Wpf.csproj`,
> `src/ImageVisualCompare.Wpf/ThirdParty.cs`,
> `src/ImageVisualCompare.Wpf/AboutViewModel.cs`,
> `tests/ImageVisualCompare.Tests/ThirdPartyTests.cs`,
> `LICENSE`, `THIRD-PARTY-NOTICES.md`.
> A change to the code this describes means this note is updated in the same task, or
> deleted with the decision it explains.

## Versioning

The version is shown in the window title, in the status bar next to the copyright, and in
the About window. There is no `--version` flag; a GUI that already displays it permanently
does not need one.

The single source of truth is `<Version>` in `src/ImageVisualCompare.Wpf/ImageVisualCompare.Wpf.csproj`
(`Directory.Build.props` since 2026-10-02, ADR 0004).
`Version.cs` does **not** hardcode it: `VersionInfo.Value` reads the assembly's
`AssemblyInformationalVersion`, which MSBuild derives from `<Version>`, and trims any
`+sourcerevision` suffix. Don't reintroduce a literal there. See ADR 0023.

**The user bumps the version manually**, per feature or fix, choosing the increment
themselves. **Never bump it on your own initiative** — not as part of a change, not "while
you're in there". If a change seems to warrant a bump, say so and let them decide.

Bumping breaks nothing: `VersionTests` asserts the format and the csproj-to-assembly
plumbing, never a particular number. What does fail, and should: removing `<Version>` or
`<Copyright>` from the csproj (`Directory.Build.props` since 2026-10-02, ADR 0004), or breaking
the way `VersionInfo` reads them back.

## Licensing

The tool is **GPLv3-or-later**. The full text is in `LICENSE` at the project root (the
verbatim FSF `gpl-3.0.txt` — don't reword it, and don't reflow it). See ADR 0024.

Four things follow, and all four are pinned by tests:

- **Every source file starts with the notice**, above the file's own header comment: the
  SPDX line, the copyright, and all three paragraphs of the standard GNU notice
  (permission, no-warranty, where-to-get-a-copy), with a `//` prefix. New files get it too
  — it is the first thing to write, not an afterthought. `LicenseHeaderTests` walks all
  three projects and fails on any file missing it, which is the only thing keeping the
  convention alive as files get added. The SPDX line is there so licence scanners can read
  the file without parsing English. `.xaml` files do not carry it, matching FlatCheck's
  `.cs`-only convention.
- **The copyright is on screen for the whole run**, in the status bar, docked right so it
  is never what gets clipped when the window narrows.
- **The About window carries the warranty disclaimer** and points at `LICENSE` — GPLv3
  §5(d), which wants a program that displays legal notices to display that one too. It
  lives behind a button rather than in the status bar on purpose: a paragraph of legal text
  across the bottom of a comparison window would crowd out the pixel readout. Since 2026-10-30
  it is FlatCheck's About window in this tool's colours (ADR 0045, `shell.md`): its `LICENCE`
  card holds `UiText.AboutWarranty`, the notice as one paragraph, English or Romanian
  (`language.md`), and the link to the GNU page, `VersionInfo.LicenceUrl`; the licence stays the
  English `LICENSE` file. `AboutViewModel` reads every text when the window is built, so
  `AboutWindowTests` checks them without opening a window. The components written by others
  have the card under it (below).
- **One source of truth for the copyright string**: `<Copyright>` in the app csproj
  (`Directory.Build.props` since 2026-10-02, ADR 0004), read
  back through `AssemblyCopyrightAttribute` by `VersionInfo.Copyright`. The literal in
  `ReadCopyright` is only a fallback.

The year and the author are the user's call, exactly like the version. Don't roll the year
forward on your own initiative.

The About window says the full licence is "in the LICENSE file next to this program", and since
2026-10-02 the build puts it there (ADR 0027): the Wpf csproj links the root `LICENSE` as a
`None` item with `Link="LICENSE"` and `CopyToOutputDirectory="PreserveNewest"`, so the file lands
at the root of the application's build output and `dotnet publish` carries it into the publish
folder. The project reference copies it into the test project's output too, which is where
`R074_TheLicenceSitsNextToTheApplication` finds it beside the application assembly. There is one
`LICENSE` in the repository; the item is a link, not a copy.

Since 2026-10-30 the application ships code written by others, NLog's two packages for the
diagnostic log (ADR 0043), whose BSD-3-Clause licence asks a binary distribution to carry its
copyright notice, its conditions and its disclaimer. The executable carries the notice in About,
as the About windows of FlatCheck and CopyWcs do: the card `THIRD-PARTY COMPONENTS` holds
`UiText.AboutThirdParty`, the paragraph that says where the licences' whole text is, then
FlatCheck's row for each component of `ThirdParty.Components`: the name and the role (a `UiText`
member, so worded in the interface's language), the licence, the copyright line, the address as
a link (R-075). The list is a property, read when `AboutViewModel` copies it into public
`ComponentRow`s, so the roles follow the language of that moment. `THIRD-PARTY-NOTICES.md` at the
project root holds the
licence's whole text and why it is compatible with GPLv3, and stays beside the source: the Wpf
csproj links `LICENSE` beside the executable and nothing else, since the suite's release puts
every tool in one folder. `ThirdPartyTests` ties the list, in both directions, to the packages
of the Wpf project's lock file, those another package brings included, and to its csproj; each
component to its section of the notices file, which quotes its copyright line and, for NLog, the
licence's three conditions and its disclaimer; each link to an absolute https address; and it
finds no item of the csproj or of the `Directory.Build` files that names the notices file, in any
case (the card's rows are `AboutWindowTests`'s, the Romanian `UiTextTests`'s). It reads the build
files, not the build output, where a copy an earlier build left would outlive the item that made
it; a wildcard that would sweep the file in is not looked for.
