# 0036. The window runs its chains one after another, writes files only on a button, and never replaces the session's master

Date: 2026-09-25
Status: accepted 2026-09-25; the Netezire tab superseded by 0080

## Context

Retroactive: recorded on 2026-09-25 from docs/design/two-applications.md; the decision itself
dates from the window's original design (2.0.0) and the Netezire / Sintetic tabs that followed.
A 24 MP frame runs into the high hundreds of MB in analysis (known issue 7) and the light series
holds a loaded frame besides; two chains at once could pass a gigabyte on an ordinary machine.
The Netezire tab writes a ~96 MB file beside the master, the Sintetic tab a second one, and a
file is not something to produce because a folder was looked at. The automatic map scale
stretched colour over each image's own interval: full red was +3 % on one master and +10 % on
another of the same lens, and nothing on screen said so.

## Decision

Structure and the light series run one after the other, not in parallel. The Netezire and
Sintetic tabs run on a button and never on open, await `MainViewModel.WhenIdleAsync` first, write
the file after the numbers are on screen, and ask about an existing file through a
`Func<string, bool>` the `MainViewModel` constructor takes; a "no" keeps the old file and the
result stays. `SmoothTabViewModel.Saved` fires once after a real write and `MainViewModel` wires
it to `HistEq.FileWritten` in its constructor, the one wire between two tabs. The outputs
(`pp_flat_stacked_netezit.fit`, `pp_flat_stacked_sintetic.fit`, beside the master) never become
the session's master: `FlatMasterCandidates` are exact paths. Every file - `flatcheck.png`, the
console's maps - stays on the automatic scale whatever chip is selected; the scale switch
(auto / ±2 / ±10 / ±20 %, caps from `Th.Resid*`), the legend and the collapsed shape guide are
viewing aids and wording, not numbers.

## Alternatives rejected

(Restated 2026-11-02: the shape guide the decision calls collapsed is open by default since R-127,
ADR 0089; it stays a viewing aid, not a number.)

- **Running structure and the series in parallel.** The time saved would not be worth a gigabyte
  of peak memory.
- **Writing the smoothed / synthetic flat on open.** A ~96 MB file for looking at a folder.
- **Letting the smoothed file be found as the master.** The Structure tab must keep measuring
  the shot master and showing the texture the smoothing was made for.
- **Saving the map on the selected scale.** Both applications must write the same bytes for the
  same master; `OnTheAutoScaleTheDisplayedMapIsTheSameFileThatWasSaved` was changed on purpose.
- **Pre-encoding four PNGs per run in the core.** ~7 MB and three encodings nobody may look at;
  the tabs keep the raster and re-encode on the UI thread (90-255 ms per cap on a 1504x1004
  raster, measured, cached per option); `Task.Run` would need a generation counter.
- **The guide as a classifier.** A blotch can be a light leak or a cloud; the pixels do not tell
  them apart, and the tool must not start deciding shapes.

## Consequences

- The guide's ring row was rewritten after the `RadialModel` artefact was found: it used to say
  rings meant a decentred round obstruction and sent the author looking for an object that had
  never been there. `TheRingRowDoesNotClaimARingMeansADecentredObstruction` fails if the old
  wording returns.
- End-to-end gets the legend only: its map is the fitted radial term, graded against
  `Th.LightRad*`. The guide texts in `SessionMessages` mirror the README by hand; no test binds them.
- A Histogram read overlapping a Netezire write is accepted, not prevented: the write gets
  `IOException`, lands in `SaveError`, the numbers stay. The Netezire map starts at ±2 %
  (`Th.ResidPerfect`) because what is removed is small. `RunOutcome` was deliberately not touched.
- Guards: `OnTheAutoScaleTheDisplayedMapIsTheSameFileThatWasSaved`, `TheFixedCapsAreTheResidualScaleBoundaries`,
  `TheRingRowDoesNotClaimARingMeansADecentredObstruction`, the `HistEqTabTests` on `FileWritten`,
  the smoke renders of the Netezire and Sintetic tabs from `Populate` / `PopulateAdvice`.
- Source note: docs/design/two-applications.md.
