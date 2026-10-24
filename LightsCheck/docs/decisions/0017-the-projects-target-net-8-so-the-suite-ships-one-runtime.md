# 0017. The projects target .NET 8 so the suite ships one runtime

Date: 2026-10-24
Status: accepted 2026-10-24

## Context

The Astro Darkroom suite puts the five astrophotography tools of the `photo` repository
(AstroPhotoAnalyzer, DarksCheck, FlatCheck, ImageVisualCompare, LightsCheck) and a launcher in
one self-contained folder with one copy of the runtime and of every library. LightsCheck targeted
.NET 9, AstroPhotoAnalyzer and FlatCheck target .NET 8, and two self-contained publishes of two
runtimes into one folder overwrite `coreclr.dll`, the `System.*` assemblies and WPF with the
other version. On 2026-10-24 the owner chose .NET 8 for every project of the suite for now
(`ClaudeCodeStarterKit/docs/plans/2026-10-24-build-the-astro-darkroom-suite.md`). Measured the
same day: `System.Threading.Lock` was the only .NET 9-only API here, and every package resolves
to the same version on net8.0 (the restore renamed the lock files' framework keys and changed no
version).

## Decision

The console, the core and their tests target `net8.0` (`Directory.Build.props`), the window and
its tests `net8.0-windows`. The SDK pin in `global.json` (9.0.318, `latestPatch`) is unchanged:
SDK 9 builds net8.0 with C# 13 through `LangVersion latest`.

## Alternatives rejected

- **.NET 10 LTS for every project.** Declined by the owner for now; a later task, one per
  project.
- **.NET 9 for every project.** The two .NET 8 projects would move instead, and .NET 9 leaves
  support in November 2026.
- **A single-file exe per app with its own runtime.** Five copies of the runtime in the suite;
  the owner chose one folder.

## Consequences

`System.Threading.Lock` is gone: the console's progress lock is a plain `object` lock, with
the same `lock` statement. C# 13 stays. The runtime the release publishes for is declared as
before (the kit's ADR 0034). Guarded by
`RepoInvariantTests.TargetFramework_IsNet8_InEveryProjectThatShips`, which reads
`Directory.Build.props` and the window's project file.
