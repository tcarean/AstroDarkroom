# 0104. A number in a lens name is an aperture only between f/0.7 and f/11

Date: 2026-11-13
Status: accepted 2026-11-13

## Context

`LensSpec.Parse` reads a lens's range from its name so that a candidate whose name contradicts
the raw's range is left out (ADR 0030). The aperture was the first `[fF]/?\s?<number>` not
preceded by a letter. The review of 2026-11-13 (CR-005) found names where that number is not
an aperture: `TAMRON SP 35mm F_1.4 Di USD F045` read 45 (the underscore hides `F_1.4`, the
model code `F045` matches), `Zeiss Distagon T 21 f28 ZE` read 28, `Zeiss Otus 55 f14 ZE` read
14, and lensfun's `smc Pentax-F 28mm f/2.8` read 28, its focal length. For a raw whose range
says f/1.4, `Contradicts` then found ten stops of difference and dropped the right lens; the
candidates left were other lenses, and the depth verdict came from one of them.

Measured on 2026-11-13 on the 3242 distinct lens names of the two catalogues (the Adobe files
under ProgramData and the shipped lensfun copy), with the rule before:

- 2540 names gave an aperture. The true maximum apertures run from f/0.8 to f/11 (the Canon RF
  600 and 800 mm F11, six names with their extenders).
- Every number read above 11 was something else: 14 (Zeiss `f14`, five names), 22 and 32
  (lensfun's `35mm f/22.0-3.5`, `120mm f/32.0-4.0`, a range written backwards), 28 and 35
  (Zeiss `f28`, `f35`; `Pentax-F 28mm`, `Pentax-F 35-80mm`), 45 (Tamron `F045`), and 200, 601,
  707, 770, 810, 828 (the compacts `FinePix F200EXR`, `DSC-F707` and their kin).
- With the rule chosen, 3205 names read the same and 37 differently: 33 codes and backward
  ranges now give no aperture, the three `Pentax-F` names give their own (2.8, 3.5, 4.0), and
  one name loses a right reading, below.

## Decision

The owner's choice of 2026-11-13, between this and "the same, and also read `f28` as 2.8":

1. A number followed by `mm`, or by `-<m>mm`, is a focal length, not an aperture; the search
   goes on to the next `f` number. The word must end after `mm`, so `f/2.8 MMJ` is an aperture.
2. The first number left is the aperture when it lies between `Th.LensNameApertureMin` (0.7)
   and `Th.LensNameApertureMax` (11). Outside that interval it is a model code: the name gives
   no aperture, and a name that gives none contradicts nothing. No later number is tried.
3. The bounds are thresholds in `Th`, with the measurement beside them (the rule "Ratios").

## Alternatives rejected

- **Reading `f28` as 2.8, `f14` as 1.4 and `F_1.4` as 1.4.** It would read ten more names, but
  it is a guess about a spelling, and the rule of this area since ADR 0030 is that nothing is
  guessed from a name: a lens that says nothing stays a candidate, which is the safe side.
- **A list of known model codes.** It ages at the first new lens.
- **Only the `mm` filter, without bounds.** It fixes the three Pentax names and none of the
  codes.

## Consequences

- Where the candidates come from the raw's lens ID or name, a lens whose name gives no
  aperture is narrowed by its focal range alone (`LensCatalog.Narrow`), so the list can be one
  lens longer than with a right reading, and that lens is no longer dropped. On the range-only
  path (a manual lens, `CandidatesByRange`) a name must state the whole range to be a
  candidate, so such a lens is not found there, as it was not when its name read f/45.
- `Leica SUMMICRON-S 100 mm f2 mm ASPH.` (one Adobe file) loses its aperture: its `f2` is
  followed by `mm`. It gives none now, which contradicts nothing.
- `FinePix F11 & compatibles` still reads f/11: a code inside the interval cannot be told from
  an aperture. It is a compact's fixed lens.
- A lens faster than f/0.7 or slower than f/11, if one enters a catalogue, gives no aperture
  until the threshold moves.
- CR-014 (the same expressions take non-ASCII digits and then throw) is not fixed here.
- R-018 holds the behaviour; `R018_...` in `LensProfileTests` pins the names above.
