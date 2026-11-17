# 0110. Every file a test or a recorded measurement reads is kept in code-testdata, and cited from there

Date: 2026-11-17
Status: accepted 2026-11-17

## Context

ADR 0088 put the twelve reference files of the validation cases in
`E:\Photografie\Astro\code-testdata\AstroPhotoAnalyzer`. Measurements that decided a threshold
or a design were made on other files too, read where the owner keeps them: the Smida Milky Way
TIFFs of ADR 0081, ADR 0083 and ADR 0109 in the session's folders, WhiteWall's and Hahnemuhle's
printer profiles under `E:\Photografie\Print and ICC profiles`, the owner's Lightroom captures
in a temporary folder of the chat. Those places are the owner's catalogue, and it is
reorganised: on 2026-11-17 two paths the documents cited no longer existed (ADR 0025's FITS
under `Processed_With_1mmPTFE_Flats\CropAndPlateSolve\RBF0.5\preview`, a plan's TIFF under
`finale\finalisti`), and trial files of the task of that day were deleted before a check that
was to read one. The owner asked the same day that any file needed be copied into the
code-testdata folder, which they do not touch, "as a general rule from now on, for new files
too".

## Decision

A file a test or a recorded measurement reads (an image, an ICC profile, a capture the owner
sent as evidence) is copied into `E:\Photografie\Astro\code-testdata\AstroPhotoAnalyzer` before
the work that reads it, its copy checked against the original by hash, and read and cited from
there. It is copied, never moved: the catalogue stays the owner's. A file a test reads enters
`testdata/manifest.json` with its hash, as ADR 0088 has it; a file that is only the input of a
recorded measurement is listed, with its hash, where it came from and the decision that rests
on it, in `AstroPhotoAnalyzer.Tests/testdata/README.md`. A trial file the code writes again from
a kept input needs no copy.

## Alternatives rejected

- **Cite the catalogue's path and re-find the file when it moves.** Two cited paths were
  already dead, and a file found again by name is not known to be the one measured.
- **Every kept file in the manifest.** The manifest is what `check-testdata.ps1` checks and
  the gate lists; files no test reads would add lines to every gate for nothing, and the
  count the documents give for the validation cases would stop meaning those cases' files.
- **The files in the repository.** They are 135 MB images; ADR 0004 and ADR 0088 keep such
  files outside git.

## Consequences

- Copied on 2026-11-17, hashes equal to the originals': the two Smida TIFFs, the WhiteWall and
  the P900 Photo Rag Baryta profiles, and the ten Lightroom captures behind ADR 0109's table
  (`lightroom-captures-2026-11-17`); `testdata/README.md` lists them. The plate-solved FITS of
  ADR 0025 has been there since ADR 0088.
- The paths ADR 0025 and ADR 0081 and the plan of 2026-11-05 gave are the copies' now (a path
  fixed in place, the kit's ADR 0024); what they say was measured is unchanged.
- The rule is the last sentence of "Measured on the reference image" in `CLAUDE.md`, which
  stays at the 200 lines `RepoInvariantTests` allows it; the how is here and in
  `testdata/README.md`. Nothing checks the rule.
- The folder grows with every such file (270 MB that day); nothing in it is deleted without
  the documents that cite it changing too.
