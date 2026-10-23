# 0026. Test images are generated at run time; the WPF layer is checked by launching the application, not by unit tests

Date: 2026-10-02
Status: accepted 2026-10-02; the WPF layer untested by design superseded by 0032

## Context

Retroactive: recorded on 2026-10-02 from `CLAUDE.md` ("Testing", all three paragraphs) and the
`TempImages` summary; the decision itself dates from a6e5f10 (the base version, 2026-09-08) for
`TempImages`, and from 591f433 (documentation for image compare, 2026-09-08) for the "Testing"
text, which then said "Not covered at all: the whole WPF layer." 801bda5 (refactoring and new
tests, 2026-09-08) moved the last arithmetic out of the controls into Core records (ADR 0009)
and rewrote that line as "genuinely untestable without UI automation". The imaging layer reads
whatever format a file comes in (ADR 0022). Both real defects found in the project lived in the
WPF layer, and neither would have been caught by any reasonable unit test: one was WPF layout
behaviour (ADR 0012), the other build configuration killing text rendering
(`InvariantGlobalization`, ADR 0017). Both were found by launching the application and looking
at a screenshot.

## Decision

- The imaging tests write their files at run time through `TempImages`: PNG or TIFF, of random
  pixels in the pixel format each test names, in a new folder under the system's temp directory
  that is deleted afterwards, plus a corrupt file (a PNG signature and three bytes). Counted on
  2026-10-02, `WicImageLoaderTests` writes six pixel formats as PNG (`Rgb48`, `Bgr24`, `Gray8`,
  `Gray16`, `Bgra32`, `Indexed8`) and one TIFF (`Rgb48`), as `CLAUDE.md` says;
  `ImagePairLoaderTests` uses `TempImages` for its real loads and the corrupt file. No binary
  fixture is committed.
- Core and the imaging layer are tested. The WPF layer is not, by design: what is left in it is
  event wiring, mouse capture, property assignment and the interop that darkens a caption, and
  every number it puts on screen comes from Core, where it has a test (ADR 0009). `MainWindow`,
  `MessageDialog`, `DarkTitleBar`, `FileDialogService` and `MemoryHousekeeping` have no test.
- A change to the WPF layer is checked by launching the application and looking at it: a green
  suite says nothing about that code.

## Alternatives rejected

- **Binary fixtures in the repository.** Generating the files "beats checking binary fixtures
  into the repository, and it keeps the formats under test visible in code" (the `TempImages`
  summary).
- **A stand-in for the codecs.** The tests are there to "exercise the real WIC codecs rather
  than a stand-in" (the same summary); the recording fake of `ImagePairLoaderTests` only counts
  probes, decodes and concurrent loads.
- **UI automation for the WPF layer.** What is left for it is wiring, capture and property
  assignment; the numbers those controls show already have their tests in Core (ADR 0009), and
  the two defects the layer did have were seen on a screenshot of the running application.

## Consequences

- The test project is `net9.0-windows` with `UseWPF`, because the imaging tests drive the real
  codecs, so the suite runs on Windows only.
- The tests assert what a file is and how it loads, never pixel values, which are random. A real
  export at full size is the job of the validation cases, outside the gate (ADR 0005).
- `docs/skills.md` makes the launch a step of `/new-feature` and `/wrap-up`: when the WPF layer
  changed, the application is launched on the real pair and a screenshot looked at before the
  commit.
- Guards: none by test, as a rule about the tests themselves; `WicImageLoaderTests` and
  `ImagePairLoaderTests` are the classes that use `TempImages`.

Source: CLAUDE.md ("Testing", and as 591f433 and 801bda5 wrote it; "Structure", the line on the
test project); tests/ImageVisualCompare.Tests/TempImages.cs;
tests/ImageVisualCompare.Tests/WicImageLoaderTests.cs;
tests/ImageVisualCompare.Tests/ImagePairLoaderTests.cs.
