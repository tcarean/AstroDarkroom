# 2026-10-05 Declare the win-x64 runtime, so a release's publish leaves the lock files alone

Status: approved by the owner on 2026-10-05 ("da, fă merge-ul, curățenia și apoi
RuntimeIdentifiers"), after the finding of `docs/plans/2026-10-02-copy-the-licence-next-to-the-program-and-fix-stale-texts.md`
("What deviated") and the recommendation to add it in ImageVisualCompare, AstroPhotoAnalyzer and
FlatCheck before their next release. Changed by the owner the same day, after step 1 measured
two facts in AstroPhotoAnalyzer and FlatCheck (see "What deviated"): option A, "A, cu condiția și
la IVC", the declaration without the test projects in the three, and the single-file analyzer
turned off in the two that publish a single file.

## Goal

The release entry of each project's `kit.json` publishes with `-r win-x64`, and each project
restores with a lock file. A plain restore writes no runtime section into `packages.lock.json`;
`dotnet publish -r win-x64` adds one, so the first release through `scripts/release.ps1` would
leave the lock files changed, and the `/release` skill stops at its step 6 on anything but
`CHANGELOG.md` and `Directory.Build.props`. Declaring `<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>`
in `Directory.Build.props` makes every restore write that section, measured on ImageVisualCompare
on 2026-10-02.

## Steps

One commit per project, on `claude/Declare-the-win-x64-runtime`, in this order:
ImageVisualCompare, AstroPhotoAnalyzer, FlatCheck. In each:

1. Measure first: publish each release target of `kit.json` with its arguments into a scratch
   folder and record which lock files change; restore them.
2. Add the property to `Directory.Build.props`, with a comment in the file's own language
   (English here and in FlatCheck; Romanian without diacritics in AstroPhotoAnalyzer, its ADR
   0005), then restore and commit the lock files it rewrites. Since the owner's answer: with
   `Condition="!$(MSBuildProjectName.EndsWith('.Tests'))"`, and in AstroPhotoAnalyzer and
   FlatCheck also `<EnableSingleFileAnalyzer>false</EnableSingleFileAnalyzer>`.
3. Publish the release targets again: the lock files must stay unchanged. Build, gate.
4. An ADR, as each project's own value: ADR 0028 here, AstroPhotoAnalyzer ADR 0031 (0028 to
   0030 are taken by its open branches), FlatCheck ADR 0056; an `Internal` line in each
   `CHANGELOG.md`.
5. ImageVisualCompare's rule "Run the application" names `Directory.Build.props`: launch it once.

Nothing else: no guard test, no kit change. Both stay offered to the owner.

## Verification

Each project's gate green; after the change, a publish with the release's arguments and a plain
build leave every lock file unchanged.

## What deviated

(filled at wrap-up)
