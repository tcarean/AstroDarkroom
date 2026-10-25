# 0019. Every comment is English; the copies' comments describe DarksCheck

Date: 2026-10-25
Status: accepted 2026-10-25
Supersedes: 0003 in part (a copy changes only after its source); 0005 in part (the copied files' Romanian comments)

## Context

ADR 0005 kept the comments of the files copied unchanged under `Imaging/` in Romanian, as
FlatCheck wrote them, and ADR 0003 lets a copy change only after its source, so a refresh stays
a plain copy. On 2026-10-25 the owner asked for the XML documentation backfill (the kit's ADR
0035), for every other comment in the code to be English too, and for comments to be English
only from then on. FlatCheck's own backfill (its ADR 0060) had just made every one of its
comments English, but LightsCheck, which DarksCheck refreshes its copies from, has not done its
backfill yet. Measured on `main` at 3197e36: every Romanian comment line of the code (218) sat
in eight `Imaging/` files, with 18 of the 23 comments of `Theme/Dark.xaml` and fourteen lines in
`FitsTestData.cs` and `FitsReaderTests.cs`; five of the ten `Imaging/` files had code identical
to FlatCheck's (namespace, usings and `#nullable` aside), the other five did not (LightsCheck's
adaptations in `RawReader.cs` and `ImageLoader.cs`, an older `FitsReader.cs`, members FlatCheck
added since to `PngWriter.cs` and `Stats.cs`).

## Decision

Every comment in DarksCheck is English, in every file, the copies included. The copies' comments
are DarksCheck's own, written here without waiting for LightsCheck: they say what each file and
member does in DarksCheck, who calls it, and that a member DarksCheck does not call is kept only
because the file is a copy; they name no FlatCheck mode, option or tab. FlatCheck's English text
was the starting point where it describes the same code. The code of the copies does not change:
a refresh from LightsCheck takes its code and keeps these comments. Each copy says so under its
`#nullable` line.

## Alternatives rejected

- **Wait for LightsCheck's backfill, then refresh.** It keeps a copy a plain copy, but leaves the
  Romanian comments in DarksCheck for as long as LightsCheck's backfill waits, against what the
  owner asked for now.
- **Do LightsCheck's backfill first.** The same result a task later; the owner chose to start
  with DarksCheck.
- **FlatCheck's English comments word for word.** The first form of this decision, taken in
  planning. The review found that they describe FlatCheck's modes, options, tabs and types
  (the attribution mode, `--light`, `--expo`, ExposureAnalyzer, FitsWriter), none of which
  exists here, so a reader looks for code that is not there; the owner chose comments that
  describe DarksCheck.
- **Take FlatCheck's files whole.** Five of them differ in code (FlatCheck's ADR 0057 in
  `FitsReader.cs`, new members in `PngWriter.cs` and `Stats.cs`, LightsCheck's adaptations); taking
  them would change behaviour, which this task does not.

## Consequences

The rule "Languages" in `CLAUDE.md` loses its exception for the copied files, and `.editorconfig`
no longer names Romanian comments among the copies' style. A refresh from LightsCheck takes the
code only and keeps this tree's comments, adjusting the ones the new code makes false; the
design note `docs/design/imaging.md` says so. A diff of a copy against its source shows every
comment as changed. The comments of `FitsReader.cs` describe this copy's behaviour (no SIMPLE
check, an empty header for a file that is not a FITS), not FlatCheck's newer one.
