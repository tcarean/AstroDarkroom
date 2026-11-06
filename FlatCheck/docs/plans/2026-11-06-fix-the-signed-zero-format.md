# Fix the signed zero format (R-129, ADR 0092)

## Context

On 2026-11-02 a structure run whose master was the model of a lensfun profile itself wrote
`-+0.00 stopuri` on the Samyang row and in the comparison summary. The cause is the format with
sections, `{x:+0.00;-0.00}`: for a negative that rounds to zero .NET formats with the positive
section and still puts the minus in front. The project met it twice before: `LocalSpotSize`
shows what rounds to zero as `+0.00` (ADR 0041), and `LightReport.Signed` keeps `-0.00` for the
parity with the Python prototype (the `parity_light_*` goldens depend on it; it stays).

The owner's brief, 2026-11-06, is the plan; this file records it (the kit's rule "Plan first").

## Steps

1. Find every signed section format in `FlatCheck.Core`: `StructureReport` (`ProfileRows`,
   `ComparisonSummary`, `DepthDetail`, `LocalSpotSize`), `SplitReport` (the difference cells,
   `PngTitle`), `RunReport.MapLegend`, `ExposureReport` (`ScreenReading`, `ScreenGap`).
2. A failing test per report first (`FlatCheck.Tests/SignedZeroTests.cs`, modelled on the
   negative-zero regression in `LightReportTests.cs`), with its failing output pasted in the
   report to the owner in the session, not in the history: the tests and the fix land in one
   commit.
3. One helper, `Report/Numbers.cs`: `Numbers.Signed(value, decimals, provider)` formats the
   digits first and decides the sign on them; `Numbers.RoundsToZero` gives `LocalSpotSize` the
   same zero for the word beside the digits. Every place above calls it with its own decimals;
   `LightReport.Signed` does not change.
4. No golden holds `-+0`, so no golden line changes; one that does is a finding, and the 39
   parity goldens are not touched.
5. Documentation comments on everything added or edited; `code-map.md` says where the format
   lives; a `Fixed` line in the CHANGELOG; R-129 in `docs/requirements.md`; `/validate` on the
   real masters, zero drift expected.
6. `R128_TheStructureTabMarksTheAdobeRowAndSaysSo` parses the row's difference with
   `double.Parse` and must keep passing.
7. The gate green, the fix committed on `claude/Fix-the-signed-zero-format` (then the ADR and
   each review's fixes, a commit apiece), a subagent's code review, then the merge proposed
   to the owner.
