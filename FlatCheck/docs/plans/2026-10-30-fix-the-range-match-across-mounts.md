# Fix the range match across mounts

Date: 2026-10-30. Branch `claude/Fix-the-range-match-across-mounts`, worktree
`.claude/worktrees/Fix-the-range-match-across-mounts`, from `main` at 829e9bb. The owner's brief:
on a computer without Adobe's lens profiles the Nikkor 35 DX master is compared against lenses of
other mounts and one of them gives the verdict (found by the sibling task
`2026-10-28-say-when-adobe-profiles-are-missing.md`, item 3, decision D6); reproduce, read ADR
0030, ADR 0068, `lens-profiles.md` sections 10 and 11 and `known-issues.md` before calling the
maker-word rule a defect, then the measured facts and options to the owner, then the requirement,
the failing test and the fix, validation with V-016 to V-030 and V-041 to V-048 unmoved, the ADR,
`/wrap-up` and a merge proposal.

## Measured (Release console in the worktree, `--lang en`)

- Nikkor 35 DX (`ISO400/f2.8-sky`, raw `DSC_0031.NEF`, ID 159) with `--profiles` on a copy of
  `lensfun/` alone: exit 2, six rows, verdict ACCEPTABLE +0.20 stops = 14.7 % from `Viltrox AF
  35mm f/1.8 Z [Nikon Z / Sony E]`; also `MEKE SL 35mm F1.8 STM PRO [Leica L / Sony E / Nikon Z]`
  +0.26 and `Nikkor Z 35mm f/1.8 S [Nikon Z]` +0.33 REDO; the lens's own rows +0.47 (APS-C
  calibration) and +0.21 = 15.4 % (full frame); `Tamron SP 35mm f/1.8 [Canon EF / Nikon F AF /
  Sony Alpha]` +0.36. With Adobe: exit 0, four rows, Adobe D90 +0.11 GOOD nearest (V-016), the
  `Nikkor Z 35mm f/1.8 S` joined through the ID path.
- Tokina control (ID 139): lensfun alone two rows, -0.58 REDO exit 3; with Adobe four rows, Adobe
  AT-X -0.39 REDO (V-025). Samyang 135 f/2 (ID 74, by range): no other-mount row either way.
- Cause: `LensIdentification.CandidatesByRange` and the lensfun join in `CandidatesFromRaw` take
  the camera's first word (`NIKON`) in the lens's mount or maker as "the mount"; `Nikon Z` passes.
  lensfun gives each camera a `<mount>` and defines each mount with a `<compat>` list (`Nikon F
  AF`: the F mounts and the adapter mounts, never Z). Adobe's files carry no mount.
- Bench over the 712 lensfun lenses with vignetting, rule "word and a lens mount in the body's
  mount or its compat list", D3300: 35/1.8 6 -> 3 (the three Z lenses), 11-20/2.8 2 -> 2, 135/2
  1 -> 1, 50/1.4 7 -> 6, 85/1.8 6 -> 4, 24/1.4 2 -> 2; K-70 and 7D Mark II keep their lenses.

## Decisions (the owner, "yes to all", 2026-10-30; ADR 0076)

1. The rule: the camera's first word stays, and a lensfun lens must also sit on the body's lensfun
   mount or one of its compat mounts. Not the lensfun mount alone (adapter mounts would join), not
   a known-issue note.
2. A body lensfun does not know keeps the first-word rule alone, logged.
3. Adobe files keep the first-word rule; the limit is recorded with the 30 Z 7 files measured.
4. The same rule on the ID path's lensfun join.

## Steps

1. R-116 in `docs/requirements.md` (R-114 and R-115 landed on `main` meanwhile; ADR 0073 to
   0075 too, so the ADR is 0076).
2. Fixture `LensfunMountsXml` (`Sources(ws, mounts: true)`), the failing tests
   `R116_ARangeOnlyCandidateFitsTheCamerasLensfunMount`,
   `R116_TheLensfunLensesThatJoinAnIdHitFitTheCamerasMountToo`, the reader test
   `Lensfun_MountsOfIsTheCamerasMountAndItsCompatList`.
3. `LensfunReader`: `LensfunMount`, `LensfunData.Mounts`, `MountsOf`, `CameraOf`.
   `LensIdentification`: `FitsMount`, the `mounts` parameter on both candidate paths, the Debug
   line for a body without a mount.
4. Re-run the three reproductions: Nikkor lensfun-only three rows, verdict from its own row
   (+0.21 = 15.4 %); Nikkor with Adobe four rows -> three, same verdict; Tokina unchanged.
5. Validation: the 48 cases with the fix, no drift; V-049 added (`sources: lensfun`, a profile
   case without an Adobe folder, `ValidationTests.LensfunOnly`).
6. `lens-profiles.md` (sections 5, 10, 12), README, CHANGELOG, ADR 0076, this plan, `/wrap-up`,
   the merge proposal (the branch is behind `main`: merge `main` in first, with the owner's yes).
