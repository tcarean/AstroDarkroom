# 0029. A package under another number than `<Version>` is a trial, never handed out

Date: 2026-11-14
Status: accepted 2026-11-14

## Context

The suite's version comes from two places. The window shows the assembly's version, the
`<Version>` of `Directory.Build.props`. The folder's name, the zip's name and the `version` of
`release.json` take the `-Version` given to `scripts/release.ps1`. A full release makes the two
equal; `-PublishOnly` checks nothing and leaves `Directory.Build.props` untouched, and nothing
in the launcher or in the tests compares them. The code review of 2026-11-13 found a case
(CR-001): `artifacts\AstroDarkroom-0.9.0-win-x64.zip`, made that day with `-PublishOnly`, whose
`release.json` says `0.9.0` while its window and "Despre" say `Astro Darkroom 1.0.0`; a user
who received it would report a problem "in 1.0.0", and another zip, named 1.0.0, says the same.

No release of the suite has been cut yet: on 2026-11-14 the repository has no
`astrodarkroom-v` tag and `CHANGELOG.md` no released heading, so the three zips in `artifacts/`
(0.1.0, 0.9.0, 1.0.0) are trials made with `-PublishOnly`.

The review gave three ways out and left the choice to the owner. Asked on 2026-11-14, with the
launcher saying both numbers recommended, the owner chose the rule alone.

## Decision

No code. A package published under another number than `<Version>` is a trial and is never
handed out: what leaves the PC is the zip of a full release, whose number is `<Version>`. The
rule is written where the publish is run from, `docs/skills.md`: the trial publish of `/wrap-up`
uses `<Version>` as its number, and `/release` compares the `version` of `release.json` with
that of its `AstroDarkroom` component before the upload. CR-001 is closed as rejected, with
this reason.

## Alternatives rejected

- **The launcher says both numbers when they differ** (`Astro Darkroom 1.0.0 (package 0.9.0)`).
  A text and a core function for a package that should not exist outside the PC; the owner
  preferred not to have the package over having it explained.
- **The publish is given the number**, so the window of a `-PublishOnly` package says the
  number of its folder. That is in the kit's `scripts/release.ps1`, the same bytes in every
  project (the kit's rule "Shared folders"); it can be taken there, for every project, and it
  would make this rule unnecessary.

## Consequences

Nothing guards the rule but the reading of `release.json` at `/release`: a zip made with
`-PublishOnly` under another number can still be made, and handed out by hand. The launcher
goes on showing `<Version>` alone and writing the `version` of `release.json` to its log, where
a report shows both. The 0.9.0 package of 2026-11-13 is such a trial.
