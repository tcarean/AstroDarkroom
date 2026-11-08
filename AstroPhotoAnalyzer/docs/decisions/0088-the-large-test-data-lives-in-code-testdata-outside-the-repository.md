# 0088. The large test data lives in code-testdata, outside the repository, and the tests look there by default

Date: 2026-11-08
Status: accepted 2026-11-08
Supersedes: 0004 in part (the copy in testdata/large/)

## Context

ADR 0004 kept the reference image as a copy made by hand in the git-ignored
`AstroPhotoAnalyzer.Tests/testdata/large/` of the main checkout; the owner had answered "no
variable, it is one file". The folder has since grown to twelve files, 1 368 MB: `de_printat.png`,
nine FITS frames and the lab's two ICC profiles, all listed in `testdata/manifest.json`. A task's
worktree has none of them, so every task that ran `/validate` first copied up to 1.4 GB into its
worktree, and `git clean -x` in the main checkout would have deleted the owner's files. On
2026-11-08 DarksCheck moved its leak set to `E:\Photografie\Astro\code-testdata\` (its ADR 0042),
and the owner asked that this project's files move to
`E:\Photografie\Astro\code-testdata\AstroPhotoAnalyzer` too, the manifest and the test data
README staying in the repository. The kit's `scripts/check-testdata.ps1`, which the gate runs,
knows two places only: `<testData.folder>/large/` and the folder `testData.largeEnv`
(`APA_TESTDATA_LARGE`) names, and no `*_TESTDATA_LARGE` variable is set on this machine. Asked
how the tests should find the new folder, the owner chose a fixed default in the tests.

## Decision

1. **The twelve files live in `E:\Photografie\Astro\code-testdata\AstroPhotoAnalyzer`**, moved
   there whole on 2026-11-08 (12 files and 1 368 MB before and after, the twelve hashes of the
   manifest unchanged); `testdata/large/` is gone.
2. **`TestData.LargeDir()` returns that folder when `APA_TESTDATA_LARGE` is unset**
   (`TestData.DefaultLargeDir`); the variable stays an override. The validation cases and
   `StarPairTests` find the files from the main checkout and from every worktree, with no copy
   and no setting.
3. **`check-testdata.ps1` is not changed.** It checks the twelve hashes when the run sets
   `APA_TESTDATA_LARGE` to the folder, which `/validate` does (`docs/skills.md`); the gate, which
   does not set it, lists them as absent, in yellow, as it already did in every worktree.

## Alternatives rejected

- **The variable alone, as DarksCheck, LightsCheck and FlatCheck do.** No code change, but any
  test run without it prints `NOT MEASURED`, and no variable is set on this machine: every
  session would have to remember it.
- **A kit key `testData.largeFolder`, read by `check-testdata.ps1` and the tests.** It would
  check the hashes in every gate, but it is a change to the kit synced to nine projects, and the
  gate would read 1.4 GB from the HDD on every run, about 10 to 15 s on a gate of 30 s.
- **The copy in `testdata/large/` (ADR 0004).** It needs a copy per worktree and sits where `git
  clean -x` deletes it.
- **The files in git.** The `photo` repository is public on GitHub, and they are 1.4 GB.

## Consequences

- A task no longer copies images into its worktree before `/validate`; `CLAUDE.md`,
  `docs/skills.md`, `docs/validation.md`, the test data README and the manifest's comment say
  where the files are.
- The gate no longer checks these hashes anywhere, the main checkout included; `/validate` does,
  before the cases run. A file changed in the folder is caught there, not at commit.
- On another machine or a fresh clone the files are copied into the same folder, or
  `APA_TESTDATA_LARGE` names wherever they are; without them the cases report themselves not
  measured and pass, as before.
- The root path is written once, in `TestData.DefaultLargeDir`; the docs name it, the scripts do
  not.
- The project `.gitignore` keeps `AstroPhotoAnalyzer.Tests/testdata/large/`, so a stray copy made
  by the old instructions never enters the repository.
