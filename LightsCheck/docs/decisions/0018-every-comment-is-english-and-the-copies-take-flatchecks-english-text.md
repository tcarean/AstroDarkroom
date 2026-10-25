# 0018. Every comment is English, and the copies take FlatCheck's English text

Date: 2026-10-25
Status: accepted 2026-10-25
Supersedes: 0003 in part (the copied files keep FlatCheck's Romanian comments); 0005 in part (the comments of the copied files Romanian, a comment added to a copied file Romanian)

## Context

ADR 0003 (2026-10-09) copied ten files from FlatCheck 2.3.0 into `Imaging/` and kept their
Romanian comments, so that a copy would diff cleanly against its source. ADR 0005 made the
comments of those copies the one Romanian zone of the code. On 2026-10-24 the owner asked for
the XML documentation backfill (the kit's ADR 0035) and for every comment in the code to be
English, with English the only comment language from then on. FlatCheck's own backfill (its
ADRs 0059 and 0060, merged as 504b5d1 on 2026-10-25) translated the source files. Measured on
2026-10-25, the code of five copies (`BitmapFont`, `CfaPattern`, `IFrameReader`, `Img`,
`Mosaic`) is token for token FlatCheck's. The code of three (`FitsReader`, `PngWriter`,
`Stats`) differs: FlatCheck's later FITS-magic check (its ADR 0057), the parts ADR 0003 cut,
and the edits it lists (`FitsReader` reads EXPTIME, ISOSPEED, DATE-OBS and XPIXSZ and throws
InvalidDataException where FlatCheck throws Exception; `PngWriter` takes a MemoryStream). The
theme `Dark.xaml` and two test files carried FlatCheck's Romanian too.

## Decision

Every comment in LightsCheck is English: code, XAML, tests, the copies included. Romanian
stays in what the user sees, and a comment quotes that output as it is. A copy takes
FlatCheck's English text: whole where the code matches, member by member where it does not,
translated here only for what FlatCheck does not have. Its code stays as copied, and its marker
says so: "its code of 2.3.0", with "with the edits ADR 0003 lists" where there are any, "its
English comments of 504b5d1".

## Alternatives rejected

- **Keeping the copies Romanian until a refresh.** The owner asked for English now, and the
  copies' code is not due a refresh. Taking FlatCheck's text keeps the copy and its source
  saying the same thing.
- **Refreshing the copies whole from FlatCheck now.** `FitsReader` would bring in the FITS-magic
  check, which changes what a non-FITS file gives. That is a behaviour change, and the owner
  asked for none in this task.
- **Translating the copies here independently.** That would make a second English text for the
  same code, and the copies would drift from FlatCheck in their comments.

## Consequences

A copy diffs against FlatCheck's file in its code, its header lines and the parts ADR 0003
cut; its comments match wherever its code does. A comment fix in a copy still goes to
FlatCheck first (CLAUDE.md, the rule "Copied files stay copies"). Copying FlatCheck's FITS-magic
check into `FitsReader` is a task of its own: a behaviour change with its test. Nothing
enforces the language; the backfill's check found no Romanian comment line left except quoted
output.
