# 0036. A release stages each publish and merges it with a hash check; it may bundle sibling projects, writes release.json and may zip

Date: 2026-10-24
Status: accepted 2026-10-24

## Context

On 2026-10-24 the owner decided to ship the five astrophotography tools of the `photo`
repository (AstroPhotoAnalyzer, DarksCheck, FlatCheck, ImageVisualCompare, LightsCheck) as one
suite, Astro Darkroom: one flat self-contained win-x64 folder with one copy of the runtime and
of every library, a launcher that is itself a kit project, its `<Version>` the suite's, its
`/release` the suite's release, the components taken from `main` as they are, and a zip with
its SHA-256 for Bitbucket Downloads (`docs/plans/2026-10-24-build-the-astro-darkroom-suite.md`).
`release.ps1` published every `release.publish` entry with `dotnet publish -o
artifacts/<version>/<output>`, straight into the target, so a second entry writing a file the
first one wrote replaced it unseen: two runtimes, two versions of a library or two licence files
in one folder, and only the last one left. The entries could name only the project's own files
in practice: nothing recorded which other project a file came from, at which version or commit.
Measured on 2026-10-24 in the kit's worktree: the kit's CLI published framework-dependent and
then self-contained into one folder differs in `starter.deps.json`, `starter.dll`,
`starter.pdb` and `starter.runtimeconfig.json`; and under Windows PowerShell 5.1 both
`Compress-Archive` and `ZipFile.CreateFromDirectory` write backslashes into the zip's entry
names (`lensfun\a.xml`).

## Decision

`release.ps1` step 5 empties `artifacts/<version>/`, publishes each entry into a staging folder
`artifacts/<version>/.stage/<n>/` and merges it into `artifacts/<version>/<output>/` with the
new `scripts/merge-publish.ps1`: a file already there with the same SHA-256 is skipped, one with
other bytes is a collision, and on any collision nothing of that entry is copied and the release
stops, naming each path; the owner decides (usually an `output` subfolder for that entry) and
re-runs with the new `-PublishOnly`, which runs step 5 alone with no checks and no change to the
version files. An entry may name a project in another kit project (`../X/...`, the nearest
`kit.json` above it), which makes that project a component: step 1 requires its `kit.json`, a
`<Version>`, a `CHANGELOG.md` and a clean folder, and `-WhatIf` lists the components. Step 5
always writes `artifacts/<version>/release.json` (name, version, date, commit, and per component,
this project first, its `<Version>`, last commit and whether its `[Unreleased]` has content).
The new optional key `release.zip` names a zip of `artifacts/<version>/`, written to `artifacts/`
entry by entry with forward-slash names, with `<zip>.sha256` beside it.

## Alternatives rejected

- **Publish straight into the folder, as before.** The collision stays unseen; the folder holds
  whichever file came last, and a runtime mismatch shows only on another machine.
- **Build the components from their tags in temporary worktrees.** Far more machinery (a
  worktree, a restore and a build per component) for a single developer who keeps `main` green;
  no component has a tag yet, and the owner chose components from `main` as they are, with
  `release.json` recording each one's commit and whether it has unreleased changes.
- **A suite script outside the kit.** The kit's rule "Shared folders" forbids a script of one
  project's own in `scripts/`; a bundle is a release that publishes more than one project, so
  the release script gains it, for any project.
- **`ZipFile.CreateFromDirectory` or `Compress-Archive`.** Measured above: backslashes in the
  entry names under Windows PowerShell 5.1, which other unzip tools take as part of the file
  name.
- **Keep the previous run's files in `artifacts/<version>/`.** They would be compared with this
  run's and reported as collisions, and would end up in the zip and in no `release.json`.

## Consequences

A release of a plain project is unchanged except for `release.json` beside its files and the
staging step. A bundle fails loudly at the first differing file instead of shipping the last
one. A collision or a failed publish still comes after steps 3 and 4, as a failed publish did:
the message says so and names `-PublishOnly`. `-PublishOnly` also measures what a release would
publish from a task branch. Guarded by `MergePublishTests` (skipped equal files, the collision
report, nothing copied on a collision) and `ReleaseTests` (an entry outside every kit project,
a component that is not clean, the `-WhatIf` listing, and `-PublishOnly` staging a library into
a subfolder, writing `release.json` and a zip with forward-slash entries and its hash).
