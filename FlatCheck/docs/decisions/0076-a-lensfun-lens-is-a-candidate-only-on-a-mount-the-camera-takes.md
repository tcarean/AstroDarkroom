# 0076. A lensfun lens is a candidate only on a mount the camera takes, by lensfun's own mount table; the camera's first word stays for the Adobe files

Date: 2026-10-30
Status: accepted 2026-10-30

## Context

Extends 0030 (the candidates by range) and 0068 (the lensfun join beside an ID hit), which
stay as they are. ADR 0030 says the candidates of a lens ID no profile carries are "the lenses of the mount whose
name says exactly that range", and ADR 0068 lets the lensfun lenses of an Adobe hit's makers join
by range. Both took "the mount" to be the camera's first word found in the lens's mount or maker
(`NIKON` from `NIKON D3300`), a word and not a table, so that nothing is guessed that is not
written in the files. The word is not a mount: `Nikon Z Viltrox` contains it.

Measured 2026-10-28 and again 2026-10-30 on the Nikkor 35 DX master with its raw (ID 159, 35 mm
f/1.8, a D3300), with `--profiles` pointing at a folder holding only a copy of `lensfun/`, the case
of a computer without Adobe's profiles (the task that found it is the sibling
`docs/plans/2026-10-28-say-when-adobe-profiles-are-missing.md`, item 3): six candidates, and the
verdict ACCEPTABLE came from the `Viltrox AF 35mm f/1.8 Z [Nikon Z / Sony E]` at +0.20 stops =
14.7 %, with the `MEKE SL 35mm F1.8 STM PRO [Leica L / Sony E / Nikon Z]` (+0.26) and the `Nikkor
Z 35mm f/1.8 S [Nikon Z]` (+0.33, REDO) beside it; the lens itself gave +0.21 = 15.4 % from its
full-frame calibration and +0.47 from its APS-C one, the `Tamron SP 35mm f/1.8 [Canon EF / Nikon F
AF / Sony Alpha]` +0.36. With Adobe's profiles present the same raw lists four rows, the Adobe
D90 file nearest (+0.11, GOOD, V-016) and the `Nikkor Z 35mm f/1.8 S` among them through the ID
path's join: a wrong row, no wrong verdict. The Tokina control (ID 139, 11-20 mm f/2.8) and the
Samyang 135 f/2 (ID 74, by range) list no lens of another mount with or without Adobe.

lensfun's database says which mounts a body takes: every `<camera>` names its `<mount>`
(`Nikon F AF` for the D3300) and a `<mount>` element at the root of a file defines each mount
with its `<compat>` list - `Nikon F AF` takes `Nikon F`, `Nikon F AI`, `Nikon F AI-S`, `M42`,
`M39/1`, `DKL`, `T2` and `Generic`, never `Nikon Z`; `Nikon Z` takes every F mount. Over the 712
lensfun lenses with vignetting, the rule "the camera's first word, and one of the lens's mounts in
the body's mount or its compat list" leaves the 35 mm f/1.8 list at three from six (exactly the
three Z lenses out), the 11-20 f/2.8 at two, the 135 f/2 at one, a 50 mm f/1.4 at six from seven
(a tilt lens on Sony E / Fujifilm X / Canon RF / Nikon Z / Micro 4/3 / Leica L out), an 85 mm f/1.8
at four from six (the Nikkor Z 85 S and a MEKE SL out), a 24 mm f/1.4 at two; a K-70 (Pentax KAF4)
keeps the DA 18-135 on Pentax K, a 7D Mark II (Canon EF-S) keeps the EF lenses. Every capitalised
camera mount in the shipped copy has a definition. Adobe's files carry no mount: the file name
says `NIKON CORPORATION`, the calibration body inside (`stCamera:Model`, a `Z 7` on the 30 files
of Viltrox and Laowa Z lenses here) is not a mount either, and a Nikon F lens profiled through
the FTZ adapter would say `Z 7` too.

## Decision

The owner's, 2026-10-30, four questions answered "yes to all":

1. **The rule.** A lensfun lens is a candidate for a camera only when one of its `<mount>`s is the
   mount lensfun's camera table gives the body, or one that mount's definition lists as
   `<compat>` (`LensfunData.MountsOf`, `LensIdentification.FitsMount`), in addition to the
   camera's first word in its mount or maker, which stays. The rule only removes: a lens the
   word let through and the table refuses.
2. **A body the table does not know**, or knows without a mount, keeps the first-word rule
   alone, as before, with a Debug line in the diagnostic log saying so when a lensfun copy was
   read (without one every body is unknown, and ADR 0075's Warn line already says why). Every body with
   interchangeable lenses in the shipped copy has a mount, so the case is a database gap, not a
   lens class, and the list the user had is not taken away.
3. **The Adobe files** keep the first-word rule: they carry no mount, and the limit is
   recorded in `docs/design/lens-profiles.md` section 12 with the measurement above.
4. **Both paths.** The same `FitsMount` filters the range-only candidates (`CandidatesByRange`)
   and the lensfun lenses that join an Adobe ID hit by maker and range (`CandidatesFromRaw`):
   one defect, one rule, one decision.

`LensfunReader` reads the `<mount>` definitions (`LensfunMount`), and `LensfunData.MountsOf`
gives the set for a camera name matched as `CropOf` matches it. R-116.

## Alternatives rejected

- **The lensfun mount alone, without the word.** The compat lists name the adapter mounts, so a
  Takumar 50/1.4 on `M42 / Sony E` would be a candidate for a D3300 shot at 50 mm f/1.4; the word
  keeps it out, and nothing is lost by keeping both.
- **The word alone, documented as a known issue.** The verdict on the Nikkor master without Adobe
  came from another maker's lens on another mount; that is the comparison answering a question
  nobody asked.
- **A mount for the Adobe files from their calibration body**, through lensfun's camera table
  (`Z 7` -> `Nikon Z`). It reads every candidate Adobe file during identification (the catalog
  lists them lazily on purpose, ADR 0028) and would drop an F lens Adobe profiled on a Z body
  through the FTZ. On the owner's three lenses the Adobe range path never meets a Z file.
- **Refusing the range path for an unknown body** (asking for `--lens`). Takes away a list that
  was right in every case measured, for a database gap.
- **The range path alone, the join left for another task.** Two tasks and two ADRs for one
  rule.

## Consequences

- `LensfunMount`, `LensfunData.Mounts` / `MountsOf` / `CameraOf`; `LensIdentification.FitsMount`,
  a `mounts` parameter on `CandidatesByRange` and `CandidatesFromRaw` (null, for a camera the
  table does not know, keeps the old behaviour; every caller, the window's prefill included,
  goes through `Identify` and gets the set); one Debug line for a body without a mount.
- On the Nikkor 35 DX master without Adobe's profiles the list is three rows and the verdict is
  the lens's own (+0.21 stops = 15.4 %, ACCEPTABLE, from the full-frame calibration); with them,
  three rows instead of four and the same verdict as before (V-016). The Tokina and Samyang
  outputs do not change. No golden moves (no golden scenario carries a mount definition).
- Guards: `R116_ARangeOnlyCandidateFitsTheCamerasLensfunMount` (the F AF body drops the Z Tokina
  and keeps the F AI one through the compat list; an unknown body and a Z body keep all four),
  `R116_TheLensfunLensesThatJoinAnIdHitFitTheCamerasMountToo`,
  `Lensfun_MountsOfIsTheCamerasMountAndItsCompatList`; the fixture `LensfunMountsXml`
  (`Sources(ws, mounts: true)`). The lensfun-only Nikkor case is V-049 in `docs/validation.json`.
- Open: a lens whose `<mount>` is misspelt in the database (`Pentak K` three times, `M39`) fits
  no body and is left out where the word alone used to admit it; a database fix, not ours. The
  Adobe limit of point 3 stays until an Adobe file is seen on the range path for a lens of
  another mount of the same maker.
- Numbered 0076 because 0073 to 0075 landed on `main` while this branch was open (ADR 0074's
  note explains the same skip for its own number).
