# 0034. The flat master may be in masters/ or process/; when both exist the newest wins, and only the master is required

Date: 2026-09-25
Status: accepted 2026-09-25

## Context

Retroactive: recorded on 2026-09-25 from docs/design/two-applications.md ("Two places for the
master") (and the memory note flatcheck-siril-preprocess-button); the decision itself dates from
2026-09-15. Siril has two conventions for where `pp_flat_stacked.fit` lands, and both exist in
the owner's archive: the scripts repo (`OSC_Preprocessing_WithoutDark.ssf` and its siblings)
stacks with no `-out=`, so the master stays in `process/` beside the calibrated frames; the
scripts shipped with the 1.4 install (`OSC_Preprocessing.ssf`, `Mono_Preprocessing.ssf`, the
`Extract_*` ones) use `-out=../masters/pp_flat_stacked`. `2026-06-18-doar-cer-acasa` is the first
kind, `2026-08-12-Smida` the second, and the window refused the second for a whole version
because `FlatMasterPath` was a fixed path into `process/`.

## Decision

`SirilSession.FlatMasterCandidates` lists both paths, `FindFlatMaster()` picks the one that
exists, and `FlatMasterPath` is that or null. Three owner's decisions, recorded so nobody
re-opens them: when both exist the newest wins, by last-write time, and `FlatMasterLookup.Other`
names the older one so the Structure tab shows `SessionMessages.TwoFlatMasters` under the source
line; only the master is required - `Check()` no longer demands `process/`, and `NoProcessDir`
means neither folder exists; nothing else moved - the light series stays in `process/` under
both conventions, the console takes explicit paths, the core knows no folders.

## Alternatives rejected

- **A fixed path into `process/`.** Refused every session preprocessed with the stock 1.4 scripts.
- **Refusing a folder with two masters, or taking the first by convention.** Two masters in one
  folder are two runs, possibly from two sets of flats; the newest is what the last
  preprocessing produced, and the warning tells the user which one the numbers are about.
- **Requiring `process/`.** A folder with `masters/` and no `process/` (frames deleted to save
  space, master kept) is a valid input for Structure; the End-to-end tab explains by itself that
  it has no series.

## Consequences

- The problem message for a missing master prints both paths it looked at.
- The Histogram tab's master entry is the same file, through the same lookup.
- The Ratio dialog starts in the folder the open session's master is in, since that is where the
  stacks are.
- `FlatMasterCandidates` are exact paths, which is also what keeps the smoothed and synthetic
  files from ever becoming the session's master (see ADR 0036).
- The stock `OSC_Preprocessing.ssf` works as well as the WithoutDark script for the window's
  purposes; WithoutDark is the default only because it is the owner's script (ADR 0035).
- Guards: the `SirilSession` tests on `FlatMasterCandidates` / `FindFlatMaster` and on `Check()`
  without `process/`.
- Source note: docs/design/two-applications.md; memory note flatcheck-siril-preprocess-button.
