# 0002. The suite is one flat self-contained folder of the five windows on .NET 8, built from main as it is

Date: 2026-10-25
Status: accepted 2026-10-25

## Context

On 2026-10-24 the owner decided the Astro Darkroom suite: AstroPhotoAnalyzer, DarksCheck,
FlatCheck, ImageVisualCompare and LightsCheck with a launcher, in one flat folder, every
executable beside the others with one copy of the runtime and of every library, self-contained
for win-x64 on .NET 8, the components taken from `main` as they are, the version the launcher's
`<Version>`, the tag `astrodarkroom-vX.Y.Z`, the zip `AstroDarkroom-X.Y.Z-win-x64.zip` with a
`.sha256` beside it (the kit's `docs/plans/2026-10-24-build-the-astro-darkroom-suite.md`). The
kit's `release.ps1` bundles sibling projects with a hash check on every file (the kit's ADR
0036). The plan expected collisions on `LICENSE`, `THIRD-PARTY-NOTICES.md` and `lensfun/` only.

Measured on 2026-10-25 in the task's worktree with `scripts/release.ps1 -Version 0.1.0
-PublishOnly`, every component clean at `main` (446947c):

- **The plan's list, windows and consoles (ten entries):** the launcher's window merged 248
  files (144.7 MB), then DarksCheck's console (196 files, 73.4 MB staged) collided on three
  files and the release stopped:

  ```
  collision  Microsoft.VisualBasic.dll  (.stage\2 differs from artifacts\0.1.0)
  collision  System.Drawing.dll  (.stage\2 differs from artifacts\0.1.0)
  collision  WindowsBase.dll  (.stage\2 differs from artifacts\0.1.0)
  merge-publish: 3 collision(s), nothing copied
  ```

  The three files exist in both runtimes with different bytes: in `Microsoft.NETCore.App`,
  which a plain `net8.0` console ships, they are facades (17 232, 20 264 and 16 168 bytes);
  in `Microsoft.WindowsDesktop.App`, which a `net8.0-windows` WPF app ships, they are the
  real assemblies (18 768, 21 288 and 2 254 672 bytes). All four consoles are plain `net8.0`,
  so a console and a window cannot share one flat folder as published.
- **The consoles under a `console/` subfolder:** ten entries, no collision; the five windows
  merged byte for byte with the launcher (`14 copied, 238 same`, `67 copied, 243 same`,
  `9 copied, 243 same`, `9 copied, 239 same`, `8 copied, 239 same`); 636 files, 241.9 MB,
  of which `console/` 222 files and 84 MB (a second copy of the base runtime); zip 100.5 MB;
  86 s.
- **The windows only (the decision below):** see the pasted output under Consequences.

On 2026-10-25 the owner, asked whether the consoles go to a subfolder, answered that they do not
want the consoles in this release at all.

## Decision

The suite is one flat folder of the launcher and the five windows, self-contained for win-x64
on .NET 8, one copy of the desktop runtime and of every library, built from `main` as the
components are, with `release.json` recording each component's version and commit. The
consoles are not bundled: `kit.json` lists one entry per component, its window project, and
`SuiteTests` refuses an entry whose project is a console (`OutputType` `Exe`). The consoles stay
in each component's own release, as single-file executables, unchanged. The catalogue has no
console field. The launcher's catalogue is compiled in, not scanned from the folder.

## Alternatives rejected

- **Consoles under a `console/` subfolder.** Measured above: no collision, 84 MB and 222
  files more for a second base runtime, and the launcher starts windows only. The owner does
  not want the consoles in the suite's release.
- **Console projects referencing the WindowsDesktop framework.** One flat folder, but four
  component changes with new lock files, and each console's own single-file release would
  grow from the base runtime (73 MB measured for DarksCheck's console publish) to the desktop
  one (145 MB measured for the launcher's window publish).
- **A kit exception letting the desktop file win for three named files.** The desktop
  assemblies are supersets of the facades, so a console would run with them, but it is a hole
  in the collision check of the kit's ADR 0036, synced to six projects, for files the suite
  does not need.
- **A subfolder per app.** Two or three copies of the runtime and no shared library; the
  owner chose one folder.
- **Single-file executables.** Five runtimes in one folder; the components' own releases keep
  their single-file consoles.
- **Components from their tags.** The kit's ADR 0036 rejected it: more machinery than a single
  developer who keeps `main` green needs; `release.json` records the commit instead.
- **A scan of the folder instead of a catalogue.** No titles, no descriptions, no workflow
  order, and every stray `.exe` of the runtime (`createdump.exe`) listed as an app.

## Consequences

The suite's publish, every component clean at `main` (446947c), the launcher at its third
commit (94ac368) with this ADR and the console removal still uncommitted (the output of
`scripts/release.ps1 -Version 0.1.0 -PublishOnly`, pasted, paths shortened):

```
== publish only 0.1.0: no checks, Directory.Build.props and CHANGELOG.md untouched
merged   248 file(s) copied, 0 already there with the same bytes, into artifacts\0.1.0
merged   14 file(s) copied, 238 already there with the same bytes, into artifacts\0.1.0
merged   67 file(s) copied, 243 already there with the same bytes, into artifacts\0.1.0
merged   9 file(s) copied, 243 already there with the same bytes, into artifacts\0.1.0
merged   9 file(s) copied, 239 already there with the same bytes, into artifacts\0.1.0
merged   8 file(s) copied, 239 already there with the same bytes, into artifacts\0.1.0
component AstroDarkroom 0.1.0 (94ac368, unreleased changes, NOT CLEAN:  M AstroDarkroom/CHANGELOG.md)
component DarksCheck 0.1.0 (bfbc078, unreleased changes)
component FlatCheck 2.3.0 (446947c, unreleased changes)
component LightsCheck 0.1.0 (9f765bb, unreleased changes)
component ImageVisualCompare 1.1.1 (8019698, unreleased changes)
component AstroPhotoAnalyzer 1.0.0 (d2cf498, unreleased changes)
published artifacts\0.1.0 (356 files, 159.0 MB)
zipped    artifacts\AstroDarkroom-0.1.0-win-x64.zip (65.4 MB), SHA-256 in AstroDarkroom-0.1.0-win-x64.zip.sha256
== published. Nothing is committed or tagged by this script.
exit 0 after 55 s
```

The entries are the launcher, then DarksCheck, FlatCheck (its 67 files include `lensfun/`),
LightsCheck, ImageVisualCompare and AstroPhotoAnalyzer; every file a window brought that the
launcher already had was byte-identical. The launcher's own line says `NOT CLEAN` because the
measurement ran on the task branch with this task's uncommitted documentation; a real
`/release` runs on `main` with a clean folder. The folder holds `AstroDarkroom.exe`, the five
`*.Wpf.exe`, `LICENSE` once, `lensfun/`, `release.json` and the runtime's `createdump.exe`.
`AstroDarkroom-0.1.0-win-x64.zip.sha256`:
`3519e6d7585fe26f4ac3cb5e709ab7be37562a2fb7592cd772bf82ede76c13bf  AstroDarkroom-0.1.0-win-x64.zip`.

`release.json` of that publish:

```json
{
  "name": "AstroDarkroom",
  "version": "0.1.0",
  "date": "2026-10-25",
  "commit": "94ac3680bdae5855255680c1e134b21e40842d5e",
  "components": [
    { "name": "AstroDarkroom", "version": "0.1.0", "commit": "94ac3680bdae5855255680c1e134b21e40842d5e", "unreleased": true, "clean": false },
    { "name": "DarksCheck", "version": "0.1.0", "commit": "bfbc0782261b3c4273d9a2d4c24b5408d9fe5a1d", "unreleased": true, "clean": true },
    { "name": "FlatCheck", "version": "2.3.0", "commit": "446947c49a6cae4c9a569cca2e03a756bccc30de", "unreleased": true, "clean": true },
    { "name": "LightsCheck", "version": "0.1.0", "commit": "9f765bb1c05fdf65278b9a3130f19fee325d2080", "unreleased": true, "clean": true },
    { "name": "ImageVisualCompare", "version": "1.1.1", "commit": "8019698113164872ae5b040f589381e65219205c", "unreleased": true, "clean": true },
    { "name": "AstroPhotoAnalyzer", "version": "1.0.0", "commit": "d2cf4984190f0d0d79b87b77789f0d4dae5f63a9", "unreleased": true, "clean": true }
  ]
}
```

Checked from that folder through UI Automation: every row is enabled with its window exe as
tooltip, every "Deschide" started its app from the suite folder (DarksCheck, FlatCheck,
LightsCheck, Image Visual Compare 1.1.1 and AstroPhotoAnalyzer windows seen), "Despre" opened
with the suite's version, the licence, the date and the five component versions, and with
`FlatCheck.Wpf.exe` renamed away its row was disabled with the tooltip
`Lipseste FlatCheck.Wpf.exe`. A `release.json` whose `components` all say `unreleased: true`
is expected until the components cut their own releases: every CHANGELOG had `[Unreleased]`
content on 2026-10-25.

What must now be maintained: a new component is a window project entry in `kit.json` and a
catalogue line (the rule "The catalogue and kit.json agree"); a component that moves off .NET 8
or bumps a shared package alone is a `SuiteTests` failure, fixed in that component. Someone who
wants a console takes the component's own release. Guards: `SuiteTests` (components equal the
catalogue, window exe names from the project files, no console entry, `net8.0` lock files, one
resolved version per shared package) and the collision check of `merge-publish.ps1` at every
release.
