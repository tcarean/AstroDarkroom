# 0022. The version lives in Directory.Build.props only, and every run prints it first

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from the old `CLAUDE.md` ("Versioning"), `docs/specification.md`
§12, `VersionTests` and the comments of `Directory.Build.props` and `Version.cs`; the decision
dates from phase 0, 2026-09-20, as in FlatCheck (its ADR 0039). Three assemblies carry a version,
the core, the console and the window, and Windows shows each in the file's properties;
`VersionInfo`, in the core, prints the one it reads from its own assembly. A report copied
somewhere should say which version, and whose tool, produced it.

## Decision

- `<Version>` and `<Copyright>` live in `Directory.Build.props` only; no csproj sets either.
  MSBuild turns them into `AssemblyInformationalVersion` and `AssemblyCopyrightAttribute`, from
  which `VersionInfo` reads them; there is no literal (the copyright has one, as a fallback).
- Every run prints `AstroPhotoAnalyzer <version>` as its first line and the copyright as its
  second, before the options are parsed: a normal run, `--help`, a failed run.
  `RunReport.WriteBanner` is the only place that prints them. With the banner on every run, there
  is no `--version` flag.
- The owner bumps the version by hand; the kit's rule "Version and year are the owner's" says the
  same, and the kit's ADR 0029 how a release uses it.

## Alternatives rejected

- **A version per csproj.** Two places to edit become, sooner or later, two values.
- **A literal in `VersionInfo`.** A third place.

## Consequences

- The golden tests replace the version line with `AstroPhotoAnalyzer <VERSIUNE>`, so a bump does
  not fail them; the copyright line stays verbatim.
- Guards: `VersionTests` (a three-part number, read from the props, no csproj override, the
  banner on line 1), the golden files, R-001 and R-120.

Source: CLAUDE.md (2026-09-21); docs/specification.md §12; Directory.Build.props;
AstroPhotoAnalyzer.Core/Version.cs.
