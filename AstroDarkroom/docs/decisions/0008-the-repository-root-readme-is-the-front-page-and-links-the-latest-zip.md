# 0008. The repository's root README is its Bitbucket front page and links the latest suite zip

Date: 2026-10-26
Status: accepted 2026-10-26
Supersedes: 0005 in part (the row's commit of README.md alone)

## Context

Bitbucket shows a repository's root `README.md` on its source page; the `photo` repository had
none, so a visitor saw only a list of folders. ADR 0005 put each release's link and hash in this
project's `README.md`, in Romanian, and rejected a root README for the link because the root is
outside every kit project and its gate, leaving the owner free to add one that points there. On
2026-10-26 the owner asked for a root README in English that opens with a section holding the
latest release's download link, then says how to build the whole suite from a checkout with
every component current, what Astro Darkroom is and what each tool does, that the tools need a
Siril session folder and Siril "newer than 1.4.4", and, for FlatCheck, recommends Adobe's lens
profiles, optional, naming the Adobe programs that bring them. The version is written "1.4.4 or
newer": 1.4.4 is the version the tools were measured on (FlatCheck ADRs 0025, 0035 and 0055), and
the owner chose that wording for the same fact in the launcher the same day.

Measured for the texts on 2026-10-26. `scripts/release.ps1 -Version 1.0.0 -PublishOnly`, in a
fresh worktree of `main` at 535c555 with the NuGet cache already filled, built the suite in 105
s: 356 files, 160.1 MB, a zip of 65.6 MB. The deepest path the build makes is 161 characters
below the repository's root. On the owner's computer,
`C:\ProgramData\Adobe\CameraRaw\LensProfiles\1.0` holds 3,531 `.lcp` files written by the Adobe
DNG Converter (the folder dates from its installation, the newest profile from its build), while
Lightroom Classic and Lightroom keep their own 3,633 profiles in `Resources\LensProfiles` inside
their program folders, which FlatCheck does not read.

## Decision

- The repository's root `README.md` is its front page, in English: first the latest release's
  row (version, date, zip link, SHA-256) in a one-row table under "Download", then how to build
  the suite from a checkout with `scripts/release.ps1 -PublishOnly`, what the suite is, each
  tool, what the tools need (a Siril session folder, Siril 1.4.4 or newer, and, optional for
  FlatCheck, Adobe's lens profiles with the programs that put them where FlatCheck reads them),
  the licence and the repository's other folders.
- At a release, `/release` proposes the new row for both READMEs: a new first row of this
  project's "Download" table and the only row of the root's "Download" table, in one commit of
  the two files that the owner makes on `main` (`docs/skills.md`, step 7).

## Alternatives rejected

- **A root README that only points to this project's "Download" section.** ADR 0005 left that
  open; the owner asked for the link itself at the top of the front page.
- **The root README in Romanian, like the tools' READMEs.** The owner asked for English; the
  kit's rule "Languages" allows a README in Romanian but does not require it.
- **Naming Lightroom among the programs that bring the profiles FlatCheck reads**, as
  FlatCheck's README does. Measured above: Lightroom's profiles stay inside its program folder,
  so the root README lists it with "no" and recommends the free DNG Converter.
- **A test that the two READMEs carry the same latest row.** The root README belongs to no kit
  project, so no gate reads it; the release step writes both rows from the same `.sha256`.

## Consequences

The latest link is written twice and kept in step by the release step, not by a check; a row
added to one README and not the other is caught only by reading. The root README restates facts
the projects own (the build command, the tools' summaries, the Siril and Adobe facts), so a task
that changes one of them edits the root README by hand in the same task. Nothing in the root
README is user documentation of a single tool: each tool's `README.md` stays its user guide.
