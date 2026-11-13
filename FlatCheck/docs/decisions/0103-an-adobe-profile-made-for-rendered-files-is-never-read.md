# 0103. An Adobe profile made for rendered files is never read

Date: 2026-11-13
Status: accepted 2026-11-13

## Context

Adobe ships many lenses as two files in one folder, `X (L) - RAW.lcp` and `X (L).lcp`. The
catalogue lists a lens by its file's name (ADR 0028), and `AdobeProfileFile.FromName` gave both
files the same identity, so `LensCatalog.ProfilesOf` pooled their entries and the nearest rule
chose among all of them. The review of 2026-11-13 (CR-004) found that the second file is made
for rendered pictures (JPEGs, with the camera's tone curve and its own corrections in them) and
that its entry can be the one used: for `Canon EOS-1D Mark III (Canon EF 17-40mm f4 L USM)` at
40 mm f/11 the nearest rule took the non-raw entry, a1 = -0.336607 where the raw one has
-0.491574, about a third less falloff, on a raw flat. Every frame FlatCheck reads is raw data.

Measured on 2026-11-13 on the 3531 files under `C:\ProgramData\Adobe\CameraRaw\LensProfiles\1.0`:

- Every entry carries `stCamera:CameraRawProfile`, and no file mixes `True` and `False`.
- Files with a lens in parentheses: 2409 end in ` - RAW` (all raw), 434 have no suffix (**391
  non-raw, 43 raw**: the Leica S lenses and the Lensbaby Velvet among them, none with a ` - RAW`
  sibling), 2 end in ` - Raw` (raw), 46 carry another suffix (` - RAW vig`, ` v2 - RAW`, ` v3 -
  RAW`, all raw). Files without parentheses (fixed-lens cameras): 296 ` - RAW`, 340 without a
  suffix, 4 ` - HEIC`. No file ends in ` - JPEG`, the suffix the design note named.
- Of the 3002 lens identities the catalogue made of them, 529 pooled a raw file with a non-raw
  one, and 189 hold non-raw files only (iPads, phones, compacts).

So the name cannot tell a raw profile from a non-raw one, and the flag inside can.

## Decision

The owner's choice of 2026-11-13, between this and "two lenses, by the file's name":

1. `LcpReader` skips an entry whose `CameraRawProfile` is `False`, in any case. An entry
   without the property is read: the property is there to exclude, and a profile someone wrote
   by hand without it is not refused for that.
2. The name rule stays, with ` - RAW` read without case, so ` - Raw` is no longer a lens of its
   own (`X, Raw`). A file with no suffix keeps the identity of its ` - RAW` sibling; after 1,
   only the raw entries of the two are pooled.
3. A lens left with no entry stays in the lists, which read names only, and is refused when
   chosen with R-071's message, widened to say both reasons: `has no vignetting profile for raw
   files (its file describes distortion or colour only, or was made for JPEGs)`.

## Alternatives rejected

- **Two lenses, by the name**: the suffix-less file as a lens of its own, labelled non-raw. The
  43 raw files without a suffix would be labelled wrong, and a non-raw profile could still be
  picked by hand for a raw flat, which is the defect.
- **Leaving the non-raw files out of the list.** The list is built from names without opening a
  file (36 s to parse them all, ADR 0028), and the name does not say which are non-raw.
- **Two refusal texts**, one per reason. `LcpReader` would have to return why a file gave no
  entry, through the catalogue to the report, for one sentence; one text that names both is
  true in both cases.

## Consequences

- 529 lenses are now read from their raw file alone. For the owner's three lenses nothing
  changes: their files are ` - RAW` only.
- 189 lenses are listed and refused when chosen. They were usable before only with a profile
  that does not describe raw data.
- A lens with only non-raw files is no candidate by the raw's lens ID (`ByLensId` reads the
  entries), and may still be one by name (`ByLensName` matches a stub's file name without
  opening it); it then gives no row, like a file without a vignetting model.
- The Lightroom folders hold the same files plus newer bodies (ADR 0066); they were not counted
  again.
- R-134 holds the behaviour; `R134_...` in `LensProfileTests` pins the four cases.
