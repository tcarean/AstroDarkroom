# 0008. FITS and PNG stay hand-rolled; a package is added only after a measured argument

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Retroactive: recorded on 2026-09-25 from docs/design/dependencies.md and docs/design/code-map.md;
the decision itself dates from the original design, with the rule in its present form written
when the first package (libraw) was added. The tool's point is a single copyable binary:
`PublishSingleFile` with the native libraries self-extracted gives one `flatcheck.exe`. The core
hand-parses FITS (`FitsReader`, `Load(byte[])` as the real entry point so it is testable without
disk; `FitsWriter` as its pair, copying the raw 80-character cards) and hand-writes PNG
(`PngWriter`, deflate through `ZLibStream` from the BCL, hand-rolled CRC-32). Two jobs were
beyond honest hand-rolling: decoding a camera raw and decoding the JPEG preview inside it.

## Decision

The FITS reader/writer and the PNG encoder stay hand-rolled and nothing replaces them. A package
is reached for where hand-rolling would be dishonest work, and only after its candidate, licence,
size and alternatives were brought to the author and measured; it is never added silently. Its
licence must be GPLv3-compatible and the reason recorded in `THIRD-PARTY-NOTICES.md`.

## Alternatives rejected

- **A NuGet FITS or PNG package replacing the hand-rolled code.** Both are small, honest work
  on the BCL, and each package costs single-file simplicity; the rule says don't.
- **No packages at all.** There is no pure-managed NEF decoder, and a JPEG decoder is an order
  of magnitude beyond an encoder that only writes; refusing both would have meant no `--expo`
  on a raw and no camera-preview comparison (ADRs 0009 and 0010).
- **Adding a package "to save an afternoon" on something the BCL already does.** Forbidden by
  the rule as written.
- **Treating per-RID native binaries as a veto.** They cost the same care `Sdcb.LibRaw` already
  needs, which is a point against a candidate, not a veto.

## Consequences

- A new package means three places: the `PackageReference`, an entry in
  `THIRD-PARTY-NOTICES.md` (why the licence is compatible) and an entry in
  `FlatCheck.Core/ThirdParty.cs` (name, role, licence, copyright notice, URL - what the
  window's "Despre" displays). The README table under "Third-party components" is a fourth copy
  kept by hand.
- `ThirdPartyTests.R095_EveryPackageTheCoreLinksIsListed` fails if a Core package is not claimed by
  exactly one component or a component claims a package that is gone;
  `R095_EveryComponentHasAnEntryInTheNoticesFile` fails if the notices file has no heading for it;
  `EveryComponentNamesItsLicenceAndUrl` allows exactly one data component (the lensfun database,
  shipped as files, `IsData`). The Adobe profiles are deliberately not a component: read from
  where Adobe installed them, never copied.
- The hand-rolled PNG bytes are verified three ways in `PngWriterTests`: decoded by an
  independent `PngDecoder` in the test assembly (not `System.Drawing`), every chunk CRC checked,
  CRC-32 pinned against `"123456789"` -> `0xCBF43926` and the empty-IEND constant `0xAE426082`.
- Anyone proposing a package brings the measurement; the notes for libraw and ImageSharp are
  the standard of evidence.

Source notes: docs/design/dependencies.md ("The rule"), docs/design/code-map.md (`FitsReader`,
`FitsWriter`, `PngWriter`), docs/design/process.md (the convention line).
