# 0003. Gate strictness: what is on stays on, in Directory.Build.props; dotnet format adds whitespace and IDE0005; the kit's code style and the analyzers stay off

Date: 2026-10-09
Status: accepted 2026-10-09; comments for the "why" only superseded by the kit's ADR 0035

## Context

The kit's `Directory.Build.props` turns on `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`,
`AnalysisLevel` latest-recommended, `Nullable` and the documentation file, and its
`.editorconfig` demands a code style. Mindfulness Reminder on 2026-10-09, measured:
`TreatWarningsAsErrors`, `Nullable` and `ImplicitUsings` set in each of the three csproj files,
not in `Directory.Build.props`, which held only the version and the copyright; a Release build
with 0 warnings; no `.editorconfig`; the three test packages versioned in the test csproj; a
`global.json` asking for SDK `9.0.0` with `rollForward` `latestMajor`; no lock files.
`dotnet format --verify-no-changes` found nothing without an `.editorconfig`; with a
whitespace-only one plus IDE0005 it found six unused usings in five files of the App, and the
kit's full `.editorconfig` found the same six and nothing more. With `EnforceCodeStyleInBuild`
on, Roslyn checks unused usings at build only when the documentation file is generated, and
generating it raised 394 CS1591 (a public member without an XML comment) and nothing else. A
build at `AnalysisLevel` latest-recommended raised 374 warnings: 352 CA1707 for the underscores
every test name here uses, 6 CA1861, 4 CA1001, 4 CA1822, 4 CA1859, 2 CA1305, 2 CA1716. The
target framework is `net8.0` (`net8.0-windows` for the App), and SDK 9.0.318 is installed.

## Decision

- What was on stays on, and moves to one place: `Nullable`, `ImplicitUsings` and
  `TreatWarningsAsErrors` leave the three csproj files for `Directory.Build.props`, which also
  sets `EnforceCodeStyleInBuild`, `GenerateDocumentationFile` and `NoWarn` CS1591, so an unused
  using fails the build as well as the format check and comments stay for the "why".
  `scripts/verify.ps1` also builds with `-warnaserror`. No `TargetFramework` moves: the three
  projects differ.
- `.editorconfig` holds whitespace rules only (LF, UTF-8, four spaces, two for JSON, YAML,
  props, targets, csproj and sln, a final newline, no trailing space) plus IDE0005 as a
  warning; XAML is not in the two-space group, since this project's XAML is four-space indented.
- The kit's code-style rules (braces, `var`, file-scoped namespaces) and `AnalysisLevel` are not
  enabled; each is a task of its own, with its own CHANGELOG line.
- Reproducibility comes from the kit in full: `global.json` pins SDK 9.0.318 with
  `latestPatch`, which builds `net8.0`; the target framework does not change (the kit's guide:
  a project on .NET 8 stays on .NET 8). `Directory.Packages.props` holds the three test package
  versions once, at the versions the csproj had (`Microsoft.NET.Test.Sdk` 17.11.1, `xunit`
  2.9.2, `xunit.runner.visualstudio` 2.8.2); every project has a `packages.lock.json` (Core's
  and the App's list no package); builds are `Deterministic`, and CI restores in locked mode.

The one `dotnet format` pass of 2026-10-09 changed 5 files, all in `src/MindfulnessReminder.App/`:
six unused usings, two in `App.xaml.cs` and one each in `BreakWindow.xaml.cs`,
`DarkMenuRenderer.cs`, `TrayIconHost.cs` and `WindowsActivityMonitor.cs`. Rewriting
`App.xaml.cs` also saved it without the UTF-8 byte order mark it had, as `charset = utf-8`
asks; it was the only source file with one. No behaviour changed: the 215 tests are unmoved.

## Alternatives rejected

- **The kit's strictness as it is.** 374 analyzer warnings on the first build, 352 of them for
  a naming style the whole test suite uses: the migration would have become a style rewrite with
  no test behind it, which the kit's guide warns against.
- **IDE0005 for the formatter only.** Not possible while `EnforceCodeStyleInBuild` is on.
- **A comment on every public member.** 394 comments that say nothing, to satisfy a warning
  about comments.
- **Leaving the strictness in each csproj.** Three copies to keep in step, and a fourth project
  would start without them.
- **CRLF.** Every source file is LF in the repository and on disk (the kit's ADR 0014, the root
  `.gitattributes`); the project's `.editorconfig` says so again. The project keeps no
  `.gitattributes` of its own: a `*` line in it would override the root's CRLF rule for `.cmd`
  and `.bat` files (the code review of 2026-10-09).

## Consequences

- A warning anywhere in the three projects fails the gate, and so does an unused using, at
  build and at format.
- The build writes an XML documentation file beside each assembly, and a publish leaves it out
  (`PublishDocumentationFiles` false); a malformed XML comment
  (CS1570) or a `cref` that does not resolve (CS1574) now fails the build.
- A new package changes `Directory.Packages.props` and the lock files together; CI restores in
  locked mode and fails on a lock file that does not match.
