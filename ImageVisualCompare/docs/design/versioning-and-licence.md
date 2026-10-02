# Versioning and licence

> Moved unchanged on 2026-10-02 (ADR 0001) from `CLAUDE.md`, "Versioning" and "Licensing". Where a
> paragraph became a decision record, its number is given beside it.
> **Covers:** `src/ImageVisualCompare.Wpf/Version.cs`,
> `Directory.Build.props`,
> `tests/ImageVisualCompare.Tests/VersionTests.cs`,
> `tests/ImageVisualCompare.Tests/LicenseHeaderTests.cs`,
> `src/ImageVisualCompare.Wpf/MainWindow.xaml`,
> `LICENSE`.
> A change to the code this describes means this note is updated in the same task, or
> deleted with the decision it explains.

## Versioning

The version is shown in the window title, in the status bar next to the copyright, and in
the About dialog. There is no `--version` flag; a GUI that already displays it permanently
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
- **The About dialog carries the warranty disclaimer** and points at `LICENSE` — GPLv3
  §5(d), which wants a program that displays legal notices to display that one too. It
  lives behind a button rather than in the status bar on purpose: five lines of legal text
  across the bottom of a comparison window would crowd out the pixel readout. The text is
  `VersionInfo.About`, a property rather than a string built inside the click handler, so
  `VersionTests` can assert its content without opening a window.
- **One source of truth for the copyright string**: `<Copyright>` in the app csproj
  (`Directory.Build.props` since 2026-10-02, ADR 0004), read
  back through `AssemblyCopyrightAttribute` by `VersionInfo.Copyright`. The literal in
  `ReadCopyright` is only a fallback.

The year and the author are the user's call, exactly like the version. Don't roll the year
forward on your own initiative.
