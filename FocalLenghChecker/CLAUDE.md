# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A .NET 8 console tool that walks a folder for Nikon `.NEF` raw files, reads the EXIF
focal length out of each one, and reports how many frames fall into each configured
focal length range. It is one of three independent tools in the `FlatPhotoAnalysis`
git repository (alongside `FlatCheck` and `WCS_Copy`); work stays inside this folder
unless asked otherwise.

**The project name is misspelled** — `FocalLenghChecker`, not `FocalLengthChecker`.
The typo is consistent across the folder, solution, assembly, namespace and exe.
Keep it. The `FocalLength*` *types* inside the namespace are spelled correctly.

## Commands

Run from the solution folder (`FocalLenghChecker/`, the one holding the `.sln`).

```bash
dotnet build
```

```bash
dotnet test
```

Run one test class or one test:

```bash
dotnet test --filter "FullyQualifiedName~RangeAnalyzerTests"
```

```bash
dotnet test --filter "FullyQualifiedName~RangeAnalyzerTests.No_focal_length_is_ever_counted_twice"
```

Run the tool against a folder:

```bash
dotnet run --project FocalLenghChecker -- "E:\some\folder" --ff
```

Note the `--` separator: without it `dotnet run` swallows `--ff` itself.

## Exit codes

`0` success · `1` bad arguments · `2` folder not found · `3` no `.NEF` files found ·
`4` the ranges in `FocalLengthRanges.cs` failed validation. These are asserted
behaviour, not incidental — changing one is a breaking change.

## Architecture

One pipeline, split so that everything except the two ends is pure and directly
testable:

```
args ──> CommandLineParser ──> CliOptions
                                   │
folder ──> INefFileFinder ─────────┤
                                   ├──> FolderAnalyzer ──> FocalLengthTally
file ────> IFocalLengthReader ─────┘                            │
                                                                v
                                                         RangeAnalyzer  (pure)
                                                                │
                                                                v
                                                          AnalysisResult
                                                                │
                                                                v
                                                        ReportFormatter (pure)
```

`Program.cs` does nothing but wire these together, map failures onto exit codes,
and paint the stderr progress line. Put no logic there — it is the only file with
no test coverage, by design.

`FolderAnalyzer` is the seam: it takes `INefFileFinder` and `IFocalLengthReader`,
so the whole flow can be exercised with fakes and never touches a disk.
`RangeAnalyzer` and `ReportFormatter` take plain data and return plain data.

## The invariants that matter

**Everything is bucketed on the full-frame-equivalent value.** The configured
ranges are FF millimetres, so a recorded focal length is converted before it is
matched. `--ff` means "the values the camera recorded are already full-frame";
its absence means they are DX and get multiplied by the crop factor.
`FocalLengthConverter` is the only place that conversion happens.

**A range written `16-23` matches up to but excluding 24.** The four configured
ranges leave integer gaps (23→24, 27→28, 70→71) that a fractional focal length
would otherwise fall straight through — a 15.5 mm DX lens is FF 23.25. The
exclusive upper bound lives in `FocalLengthRange.UpperBoundExclusive` as `Max + 1`,
which is also why `FocalLengthRanges.Validate` rejects a range starting at the
previous range's `Max`.

**EXIF `FocalLengthIn35mmFilm` (0xA405) is deliberately ignored**, even in files
that carry it. The `--ff` switch is the single source of truth, so that a folder
is interpreted the same way from end to end. Do not "improve" the reader by
falling back to it.

**Percentages are shares of the files that had a readable focal length**, not of
the files scanned. Unreadable files are counted, reported separately, and kept out
of the base. Every range count plus the "Outside ranges" count equals
`FilesWithFocalLength` — there is a test asserting exactly that.

## Changing the ranges

`FocalLengthRanges.cs` is the whole tunable surface: the range array and the crop
factor, nothing else. Ranges must be ascending and non-overlapping;
`FocalLengthRanges.Validate` runs at startup so a bad edit fails loudly with
exit code 4 rather than silently double-counting.

Editing the array will break `FocalLengthRangesTests.The_shipped_ranges_are_the_ones_that_were_asked_for`
and the golden report test. That is intended — both are there so a change to the
shipped configuration has to be a deliberate one.

## Testing conventions

- **No binary fixtures.** `TiffBuilder` synthesises a minimal little-endian TIFF
  with an EXIF sub-IFD in memory, which is enough to drive the real
  MetadataExtractor parsing path (a `.NEF` is a TIFF underneath). Use it rather
  than checking in a 25 MB raw file.
- **`ReportGoldenTests` compares the entire report.** It was generated from real
  output rather than hand-typed; if the layout legitimately changes, regenerate it
  instead of nudging spaces by hand.
- `ReportFormatter` emits `\n`, never `Environment.NewLine`, and formats every
  number with `InvariantCulture`. There is a test that runs the formatter under
  `de-DE` to keep it that way.
- Test names are full sentences with underscores
  (`A_focal_length_falling_between_two_written_ranges_is_still_counted`).

## Dependency

`MetadataExtractor` (2.9.3) is the only runtime package. It handles Nikon raw
natively; there is no hand-rolled TIFF/IFD walker in the production code.
