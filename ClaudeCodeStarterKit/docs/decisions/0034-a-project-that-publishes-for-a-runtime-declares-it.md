# 0034. A project whose release publishes for a runtime declares it, and a single-file publish turns the analyzer off

Date: 2026-10-05
Status: accepted 2026-10-05

## Context

`release.ps1` publishes each entry of `release.publish` in `kit.json` with its own `args`, after
it edits `CHANGELOG.md` and `Directory.Build.props`, and `/release` stops at step 6 when anything
else changed (ADR 0029). Every project restores with a lock file (`RestorePackagesWithLockFile`
in the kit's `Directory.Build.props`). The kit publishes `Starter.Cli` without a runtime, but a
project that adds `-r` to an entry's `args` meets two changes, measured on 2026-10-05 in
ImageVisualCompare, AstroPhotoAnalyzer and FlatCheck, which publish with `-r win-x64`:

- `dotnet publish -r <rid>` restores with that runtime and writes its section into the lock
  files of the published project and of every project it references; a restore without the
  runtime writes none.
- `-p:PublishSingleFile=true` turns on the single-file analyzer, whose package
  `Microsoft.NET.ILLink.Tasks`, at the version the installed SDK carries, then enters the lock
  files.

Either one stops the first release at step 6. Declared on every project, the runtime also wrote
844 lines into each test project's lock file in AstroPhotoAnalyzer, for projects that are never
published. For the three, the owner chose the runtime declared on every project but the test
projects, and the analyzer turned off where the console publishes a single file (their ADRs:
ImageVisualCompare 0028, AstroPhotoAnalyzer 0031, FlatCheck 0056); then the owner asked for the
kit's documentation to say so ("da, adaugă fraza în documentația kit-ului").

## Decision

A project whose `release.publish` publishes for a runtime declares it in `Directory.Build.props`:
`<RuntimeIdentifiers>` with that runtime on every project whose name does not end in `.Tests`
(`Condition="!$(MSBuildProjectName.EndsWith('.Tests'))"`), and, when an entry publishes a single
file, `<EnableSingleFileAnalyzer>false</EnableSingleFileAnalyzer>`. Every restore then writes the
runtime's section into the lock files, committed with them, and the release's publish changes
none. `GHID.md` says so in its Release section. The kit itself publishes for no runtime and
declares neither.

## Alternatives rejected

- **`release.ps1` puts the lock files back after the publish, or `/release` accepts their
  change.** The published binaries would come from a graph the committed lock files do not
  record, CI's locked mode would still refuse the publish, and the script and the skill are
  synced to every project for a case only some of them have.
- **A check in `verify.ps1` that a runtime in `release.publish` is declared.** Possible later;
  for now the release itself shows the omission at step 6, before anything is committed.
- **The kit's `Directory.Build.props` declares a runtime.** The kit publishes none; the
  declaration would only add a section to its lock files.

## Consequences

- `GHID.md`, section Release: one sentence.
- A project made from the kit that publishes for a runtime adds the lines with its first such
  publish target and cites this ADR instead of writing its own (ADR 0025).
- Where the analyzer is turned off it no longer runs; it runs only in a single-file publish, so
  only the release ran it, and in AstroPhotoAnalyzer and FlatCheck it raised no warning on
  2026-10-05.
- Guards: none. A missing declaration shows when a release stops at step 6.
