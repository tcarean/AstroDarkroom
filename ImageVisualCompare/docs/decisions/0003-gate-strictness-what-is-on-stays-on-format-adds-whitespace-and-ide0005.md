# 0003. Gate strictness: what is on stays on; dotnet format adds whitespace and IDE0005; the kit's code style stays off

Date: 2026-10-02
Status: accepted 2026-10-02; comments for the "why" only superseded by the kit's ADR 0035

## Context

The kit's `Directory.Build.props` turns on `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`,
`AnalysisLevel` latest-recommended, `Nullable` and the documentation file, and its
`.editorconfig` demands a code style. ImageVisualCompare on 2026-10-02, measured:
`TreatWarningsAsErrors`, `EnforceCodeStyleInBuild` and `Nullable` already on in its own
`Directory.Build.props`; a Release build with 0 warnings; no `.editorconfig`; the four test
packages versioned in the test csproj; no `global.json`, no lock files. With the kit's
`.editorconfig` the formatter reported 38 IDE0011 (braces on one-line `if`s, from
`csharp_prefer_braces`), three unused usings (IDE0005) and one whitespace finding; a build at
`AnalysisLevel` latest-recommended raised 48 warnings, 37 of them CA1707 for the underscores in
test names, which the kit's own `R###_` names would multiply. With `EnforceCodeStyleInBuild` on,
setting IDE0005 to warning made the build stop on `EnableGenerateDocumentationFile`: Roslyn
checks unused usings at build only when the documentation file is generated. Generating it then
raised six CS1573 in `src/ImageVisualCompare.Wpf/Imaging/ImagePairLoader.cs`, where a comment
documents the one parameter that needs a "why" and not the others.

## Decision

- What was on stays on: `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild` and `Nullable` in
  `Directory.Build.props`; `scripts/verify.ps1` also builds with `-warnaserror`.
- `.editorconfig` holds whitespace rules only (LF, four spaces, two for JSON, YAML, props,
  targets, csproj and sln, a final newline, no trailing space) plus IDE0005 as a warning; XAML
  is not in the two-space group, since this project's XAML is four-space indented.
- `Directory.Build.props` sets `GenerateDocumentationFile` and mutes CS1591 (a member without a
  comment) and CS1573 (a parameter without a tag while another has one), so unused usings fail
  the build as well as the format check, and comments stay for the "why".
- The kit's code-style rules (braces, `var`) and `AnalysisLevel` are not enabled; each is a task
  of its own, with its own CHANGELOG line.
- Reproducibility comes from the kit in full: `global.json` pins the SDK 9.0.318 with
  `latestPatch`; `Directory.Packages.props` holds the four test package versions once; every
  project has a `packages.lock.json` (the core's and the application's list no package); builds
  are `Deterministic`, and CI restores in locked mode.

The one `dotnet format` pass of 2026-10-02 changed 4 files: one space in
`src/ImageVisualCompare.Wpf/AssemblyInfo.cs` (`[assembly: ThemeInfo(`) and three unused usings,
in `src/ImageVisualCompare.Wpf/Controls/MinimapView.xaml.cs`,
`src/ImageVisualCompare.Wpf/Imaging/ImagePairLoader.cs` and
`src/ImageVisualCompare.Wpf/MainWindow.xaml.cs`. No behaviour changed: the 142 tests are
unmoved.

## Alternatives rejected

- **The kit's strictness as it is.** 38 brace findings and 48 analyzer warnings on the first
  build: the migration would have become a style rewrite with no test behind it, which the
  kit's guide warns against.
- **IDE0005 for the formatter only.** Not possible while `EnforceCodeStyleInBuild` is on, and
  turning that off would weaken what was on before.
- **A param tag for every parameter.** Six comments padded with tags that say nothing, to
  satisfy a warning about comments.
- **CRLF.** Every source file is LF in the repository and on disk (the kit's ADR 0014, the root
  `.gitattributes`); the project's `.gitattributes` and `.editorconfig` say so again.

## Consequences

- A warning anywhere in the three projects fails the gate, and so does an unused using, at
  build and at format.
- The build writes an XML documentation file beside each assembly; a malformed XML comment
  (CS1570) or a `cref` that does not resolve (CS1574) now fails the build.
- A new package changes `Directory.Packages.props` and the lock files together; CI restores in
  locked mode and fails on a lock file that does not match.
