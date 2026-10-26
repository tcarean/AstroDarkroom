# 0030. The lens is identified by the Nikon ID narrowed by the MakerNote range, by range alone for manual lenses, in one shared LensIdentification

Date: 2026-09-25
Status: accepted 2026-09-25; the Nikon-only lens ID and the ID as the one key superseded by 0068 (the ID read by mount on every make, the camera's name of the lens as the first key)

## Context

Retroactive: recorded on 2026-09-25 from docs/design/modes-synthetic.md, docs/design/modes-structure.md
and docs/design/code-map.md (and the memory note flatcheck-structure-profile-comparison); the
decision itself dates from 2026-09-17/18. libraw gives no lens name on a D3300 with third-party
glass, but gives the Nikon lens ID (139 for the Tokina 11-20), which Adobe's profiles carry in
`AlternateLensIDs`, and in `LensInfo.MakerNotes` the lens's range (11-20 mm f/2.8; the top-level
`MinFocal` / `MaxFocal` are zero). The ID alone is not enough: on the author's Adobe folder 139 is
also the Nikkor 18-200 f/3.5-5.6 (two files) and the Sigma 18-35 f/1.8, and the first cut took the
alphabetically first hit, a Nikkor, for a raw shot with the Tokina. lensfun entries carry no Nikon
IDs at all. A manual lens (the Samyang 135 f/2) writes the "non-CPU lens" ID 74, which no profile
has, plus the range set in the camera's menu (135 mm f/2.0). The identification had lived half in
`SyntheticRun.Complete` and half in the Sintetic tab's prefill, and the Structure tab's comparison
needed exactly the same rule.

## Decision

`LensSpec.Parse` reads the focal range and maximum aperture out of a lens name ("11-20mm F2.8",
"18-200mm f3.5-5.6G", "135mm f/2.0"; nothing out of "Zeiss Batis 2/25", which then contradicts
nothing), and `LensCatalog.Narrow` drops the ID hits that contradict the raw (focal ends within
1 mm or 5 %, aperture within 0.25 stops). The lensfun lenses of the same maker(s) as the surviving
Adobe hits, narrowed by the same range, join the candidates. When the ID matches nothing but the
raw gives a full range, the lenses of the mount whose name says exactly that range are the
candidates (`CandidatesByRangeOnly`), and the report says the list may hold another lens of the
same range. `LensIdentification` in Core is the one identification of the shot - focal, f-stop,
crop, camera, each with its source, and the candidates - used by the console, the Sintetic tab
and the Structure tab; `FirstRaw` is the shared "raw beside the master" rule. The owner's
decision: identification extracted to Core and reused, not tied to the Sintetic tab.

## Alternatives rejected

- **The first ID hit.** Chose the Nikkor 18-200 for a Tokina raw.
- **Adobe hits only.** The narrowed list showed the Adobe half only and the user asked where the
  lensfun Tokina had gone.
- **Picking one of several survivors.** The two Adobe generations of the Tokina 11-20 (AT-X PRO DX,
  atx-i CF) and its two lensfun entries survive together; the window narrows the list to them
  and says so (`OfferCandidates`) and the user picks. For this lens the choice is moot: the two
  Adobe files carry byte-identical vignetting parameters (checked), and the raw's primary ID 139
  is the atx-i file's `LensID`.
- **A name that does not parse is dropped.** Kept instead, so narrowing can only remove wrong
  lenses, never lose the right one for a strange name.

## Consequences

- `SyntheticRun.CandidatesFromRaw` is the one place both applications start from; when every hit
  contradicts the raw the list is empty and the console asks for `--lens`.
- The Samyang 135 f/2 is compared by range: the Nikkor 135 f/2 DC's Adobe file and the Samyang
  lensfun entry (2026-09-18: +0.11 / +0.10 BUN at f/2, +0.01 PERFECT at f/2.8); the block says
  the list is by range and to read one's own lens's row.
- `LoadedFrame` carries `NikonLensId`, `LensMinFocalMm`, `LensMaxFocalMm`, `LensMaxAperture`;
  `ImageLoader.LoadInfo` fills them without decoding pixels.
- Guards: `LensProfileTests` on the parse and narrowing, `VignettingComparisonTests`,
  `StructureTabTests` with a fabricated raw, `SyntheticTabTests`; the golden `synthetic_ambiguous`.
- Source notes: docs/design/modes-synthetic.md, modes-structure.md, code-map.md,
  lens-profiles.md (sections 5, 10, 11); memory note flatcheck-structure-profile-comparison.
