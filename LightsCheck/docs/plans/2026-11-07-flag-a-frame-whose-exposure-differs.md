# Show the exposure per frame and flag a differing one

Branch `claude/Flag-a-frame-whose-exposure-differs`, worktree
`.claude/worktrees/Flag-a-frame-whose-exposure-differs`, cut from the tip of
`claude/Judge-walking-noise-by-streak-and-pattern` (ADR 0039), since both touch
`SeriesAnalyzer.cs`, `SeriesReport.cs` and the series goldens; it merges after that branch.
Approved in chat on 2026-11-07 ("e perfect. mergem pe recomandarea ta") after the requirement
draft, the threshold and the texts were shown and the question about Siril's handling of a
longer frame was answered; the plan is written here without a plan-mode round, on that approval.

## Context

The Veil session of 2026-11-06 had four frames of odd exposure from a failing intervalometer,
which the series table did not show and the frame rules caught only sideways. ADR 0040 records
the decision; R-085 states the behaviour; R-022 and R-081 point at it.

## Steps

1. `docs/requirements.md`: R-085 draft, R-022 and R-081 amended (done before this plan).
2. Tests, failing first: `SessionFixture` (a per-frame `Exposure`, three more frames in
   `ClassesPattern`), `SeriesTests` R-022 and R-081 counts, `SeriesAnalyzerTests` R-085 (the
   usual exposure, the short and the other flag, the jump's detail, the tie and the null case),
   the golden tests' frame counts; `docs/validation.json` V-014's classes.
3. Core: `Thresholds.cs` (two constants), `Analysis/SeriesAnalyzer.cs` (the flags, the usual
   exposure, the short frame out of the medians and the neighbours), `Report/SeriesReport.cs`
   (the column, the reasons, the explanation's fifth paragraph), `Logging/LogText.cs`.
4. Window: `FrameRowVm.Exposure`, the column in `FramesView.xaml` (both grids shifted by one).
5. Goldens regenerated; every changed line read and tied to the column, the three fixture
   frames or the new flags at wrap-up.
6. `/validate` on the three sessions.
7. ADR 0040, `docs/design/series-analysis.md`, `README.md`, `CHANGELOG.md`, R-085 to
   `implemented`, `docs/validation.md`.
8. `scripts/verify.ps1`, commit, a review subagent, the merge proposal.

## What deviated

- The long frame of the classes fixture first had only one readable neighbour (the unreadable
  frame 9 on one side, the short frame 11 on the other, which no longer counts), so its jump went
  unflagged; a plain frame 13 was added after it.
- The short frame's sky is also left out of its neighbours' medians, which the first draft of
  the code did not do: with it in, the frame after a 0.2 s one read a false jump.
- The review found that the TRAILED FRAMES rows listed the bad and marginal frames by class, so
  the short frame appeared under `bad (elongation of at least 1.60)`, and that a short frame
  with a coherent trail could vote for the RA axis; both now go by the `Trailed` flag. The
  second review caught the marginal row changed along with it, dropping the soft and clouds
  frames from the list the R-081 paragraph points at; it lists the marginal class again.
- The first commit message ran to 103 characters and the commit-msg hook refused it (100 at
  most); the gate had passed.
