# 0033. The window takes one Siril session folder; the Ratio tab is the exception and proposes nothing

Date: 2026-09-25
Status: accepted 2026-09-25; the Ratio tab superseded by 0065

## Context

Retroactive: recorded on 2026-09-25 from docs/design/two-applications.md; the decision itself
dates from the original design of the window (2.0.0), with the Ratio filter fix later. The
window adds a workflow, not an analysis: one folder instead of four invocations,
`pp_flat_stacked.fit` found by convention, structure and end-to-end started on open. The Ratio
mode needs two masters from two shoots, hence two session folders, and the open one is at most
one of them. The tab used to list `pp_flat*.fit` from `process/` and preselect two, which in a
normal session offered the individual frames; a pair of single frames yields a confident, wrong
answer, because one frame's noise is the same order as the signal. The same trap survived one
layer down: the file dialogs filtered on `pp_flat*.fit`, mostly `pp_flat_00001.fit` and its
ninety siblings, and `SirilSession.IsFlatMasterName` accepted exactly what the filter offered.

## Decision

The window takes one session folder. The Ratio tab proposes nothing, starts empty, asks for two
files explicitly, and carries a panel that explains what the mode needs and what it cannot tell -
the only tab that must document itself before it can be used. `RatioPattern` is
`pp_flat_stacked*.fit` (the stack, not the frames; the `*` because two masters brought into one
folder must be renamed, and `pp_flat_stacked_0deg.fit` is still a stack); the dialog builds its
filter from that constant, and the refusal says why an individual frame is not a master. The
Histogram tab lists the flat master first, then the smoothed master when the file exists at the
exact path `SmoothedMasterPath` names (never by pattern), then every raw or FITS in `flats/`,
with the master neither preselected nor run at open. `RatioTabViewModel`'s constant texts are
instance properties.

## Alternatives rejected

- **Listing `pp_flat*.fit` from `process/` and preselecting two.** Offered the individual frames
  and a confident wrong answer.
- **A `RatioCandidates` list.** The two masters live in two different folders; there is nothing
  right to propose.
- **Static text properties for the panel.** `{Binding}` resolves through the DataContext and
  never sees a static one, so the documentation rendered blank.
- **Listing a `_netezit` file by pattern.** A `_netezit` beside the *older* of two masters is
  another master's smoothing; the exact path avoids listing it.

## Consequences

- Adding a control to the window never adds a number; the console keeps `--fov`, `--sat` and the
  other options because it is the scriptable one.
- The Histogram tab's master entry goes through the same `FlatMasterLookup` as the Structure tab,
  and the smoothed master is a third kind of row (`HistEqKind`, chip `NETEZIT`, `IsMaster` false).
- The Ratio dialog starts in the folder the open session's master is in.
- The smoothed master matches `RatioPattern`, which is right: a smoothed stack is a stack.
- Guards: the `SirilSession` tests on `IsFlatMasterName` / `RatioPattern` and `HistEqCandidates`;
  `WindowSmokeTests` renders the Ratio panel with its documentation.
- Source note: docs/design/two-applications.md.
