# 0004. The version, the copyright and the licence expression move to Directory.Build.props

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Since c6b676d (2026-09-08), `<Version>`, `<Copyright>` and `<PackageLicenseExpression>` lived in
`src/ImageVisualCompare.Wpf/ImageVisualCompare.Wpf.csproj`, the one application, as the single
source that `VersionInfo` reads back from the assembly; `VersionTests` read that csproj. The
kit's `scripts/release.ps1` reads and writes `<Version>` in `Directory.Build.props` and nowhere
else, and stops with "No `<Version>` element in Directory.Build.props" without it; a script is
never edited in a project (the kit's rule "Shared folders"). The plan proposed the move, and on
2026-10-02 the owner said: "mută versiunea în Directory.Build.props".

## Decision

The three elements move, with their comments in English (ADR 0006), into the first
`PropertyGroup` of `Directory.Build.props`. The Wpf csproj keeps the project reference,
`OutputType`, `TargetFramework`, `UseWPF` and the `InternalsVisibleTo` item. `VersionTests`
reads `Directory.Build.props`: `AppCsproj()` became `BuildProps()`, and three tests were renamed
(`TheVersionComesFromTheBuildProps`, `TheCopyrightComesFromTheBuildProps`,
`TheBuildPropsDeclareTheLicence`) with the same assertions. The header comment of
`src/ImageVisualCompare.Wpf/Version.cs` names the new file. The number stays 1.1.1: the owner
bumps it (the kit's rule "Version and year are the owner's", ADR 0023).

## Alternatives rejected

- **A configurable version file in the kit.** `Directory.Build.props` is where the kit,
  FlatCheck and AstroPhotoAnalyzer keep the version; a kit parameter would serve one project.
- **Keep the csproj and set the version there by hand at every release.** `release.ps1` refuses
  to run, and the release would lose its checks.
- **The version in both files.** Two places drift; the single source is the point of ADR 0023.

## Consequences

- All three assemblies carry 1.1.1 and the copyright now; only the application's are shown.
- `release.ps1` reads and sets the version, with the newest released heading of the changelog,
  `[1.1.1]` (ADR 0007), as its floor: the next release is at least 1.1.2 (the kit's ADR 0029).
- `VersionTests` fails if `<Version>`, `<Copyright>` or `<PackageLicenseExpression>` leaves
  `Directory.Build.props`, or if `VersionInfo` stops reading them back.
