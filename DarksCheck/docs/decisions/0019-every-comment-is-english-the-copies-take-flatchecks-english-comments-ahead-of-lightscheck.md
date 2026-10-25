# 0019. Every comment is English; the copies take FlatCheck's English comments ahead of LightsCheck

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
in eight `Imaging/` files, with twelve more comments in `Theme/Dark.xaml` and fourteen lines in
`FitsTestData.cs` and `FitsReaderTests.cs`; five of the ten `Imaging/` files had code identical
to FlatCheck's (namespace, usings and `#nullable` aside), the other five did not (LightsCheck's
adaptations in `RawReader.cs` and `ImageLoader.cs`, an older `FitsReader.cs`, members FlatCheck
added since to `PngWriter.cs` and `Stats.cs`).

## Decision

Every comment in DarksCheck is English, in every file, the copies included. The copies' comments
are translated here without waiting for LightsCheck: FlatCheck's English text word for word
where the code is the same as FlatCheck's, a translation in FlatCheck's terms where it is not,
and the comments that were already English (LightsCheck's adaptations) kept. The code of the
copies does not change. Until LightsCheck's backfill recopies FlatCheck, DarksCheck's copies
differ from LightsCheck's in their comments only, and the next refresh from LightsCheck replaces
them. An ADR number in a comment the copies took from FlatCheck is named as FlatCheck's.

## Alternatives rejected

- **Wait for LightsCheck's backfill, then refresh.** It keeps a copy a plain copy, but leaves the
  Romanian comments in DarksCheck for as long as LightsCheck's backfill waits, against what the
  owner asked for now.
- **Do LightsCheck's backfill first.** The same result a task later; the owner chose to start
  with DarksCheck.
- **Take FlatCheck's files whole.** Five of them differ in code (FlatCheck's ADR 0057 in
  `FitsReader.cs`, new members in `PngWriter.cs` and `Stats.cs`, LightsCheck's adaptations); taking
  them would change behaviour, which this task does not.

## Consequences

The rule "Languages" in `CLAUDE.md` loses its exception for the copied files, and `.editorconfig`
no longer names Romanian comments among the copies' style. A refresh from LightsCheck before its
own backfill would bring the Romanian comments back: that refresh takes the comments from this
tree instead, until LightsCheck's are English. The comments of `FitsReader.cs` describe this
copy's behaviour (no SIMPLE check, an empty header for a file that is not a FITS), not
FlatCheck's newer one.
