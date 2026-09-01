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

`Program.cs` does nothing but print the banner, wire these together, map failures onto
exit codes, and paint the stderr progress line. Put no logic there — it is the only file with
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

## Versioning

Every run prints `FocalLenghChecker <version>` as its **first line** - `--help` and
failed runs included - so a pasted report always says which version produced it. Under
it goes the copyright line; see "Licensing" below. There is no `--version` flag; it
would be redundant, and a test keeps it that way.

`<Version>` in `FocalLenghChecker.csproj` is the single source of truth.
`Version.cs` does **not** hardcode it: `VersionInfo.Value` reads the assembly's
`AssemblyInformationalVersion`, which MSBuild derives from `<Version>`, and trims any
`+sourcerevision` suffix. Don't reintroduce a literal there.

**The user bumps the version manually**, per feature or fix, choosing the increment
themselves. **Never bump it on your own initiative** - not as part of a change, not
"while you're in there". If a change seems to warrant a bump, say so and let them decide.

**Bumping the version breaks nothing.** The banner is printed in `Program.Main`, which
is the one file with no test coverage, so it never reaches `ReportFormatter` and
`ReportGoldenTests` is untouched by a bump. What does fail, and should: removing
`<Version>` from the csproj, or breaking the MSBuild plumbing - `VersionTests` covers
both, plus the exact shape of the banner built from it.

## Licensing

The tool is **GPLv3-or-later**, the same as the sibling `FlatCheck`. The full text is
in `LICENSE` next to the solution (the verbatim FSF `gpl-3.0.txt` - don't reword it,
and don't reflow it).

Four things follow, and all four are pinned by `LicenseTests`:

- **Every source file starts with the notice**, above the file's own header comment:
  the SPDX line, the copyright, and all three paragraphs of the standard GNU notice
  (permission, no-warranty, where-to-get-a-copy). New files get it too - it is the
  first thing to write, not an afterthought. `LicenseTests` walks both projects and
  fails on any file missing it, which is the only thing keeping the convention alive
  as files get added. The SPDX line is there so licence scanners can read the file
  without parsing English.
- **Every run prints the copyright on the second line**, right under the version
  banner, at `--help` and on failed runs too. A report pasted somewhere else then says
  on its own whose tool produced it, not just which version. It is printed in
  `Program.Main`, so it never reaches `ReportFormatter` and the golden report is
  unaffected.
- **`--help` carries the warranty disclaimer** and points at `LICENSE` - GPLv3 §5(d),
  which wants a program that displays legal notices to display that one too. It lives
  there rather than under the copyright line on purpose: five lines on top of every
  report, every run, would drown the numbers. That is why `CommandLineParser.Usage`
  is a `static readonly` composition rather than a `const`.
- **One source of truth for the copyright string**: `<Copyright>` in
  `FocalLenghChecker.csproj`, read back through `AssemblyCopyrightAttribute` by
  `LicenseInfo.Copyright`. The literal in `ReadCopyright` is only a fallback for a
  build that lost the attribute, and a test binds the two together.

The year and the author are the user's call. Don't roll the year forward on your own
initiative.
