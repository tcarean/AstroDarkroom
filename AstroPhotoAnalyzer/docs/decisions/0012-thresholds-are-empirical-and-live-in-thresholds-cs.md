# 0012. Thresholds are empirical rules of thumb kept in Thresholds.cs, and a literal a window text needs goes there first

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from the old `CLAUDE.md` ("Thresholds are empirical rules of
thumb, not physical constants", and the sentence on literals in "Nothing is computed outside the
core"), `docs/specification.md` §7 and §15 ("An explanatory text in the window"), and
`docs/implementation-plan.md` ("The explanation pass", "The naming pass"); the decision dates from
phase 1, extended on 2026-09-21. Most thresholds came from measuring one image, `de_printat.png`,
in the conversation that started the tool. The explanation pass found three numbers typed into
window texts that no constant held (the edge-region fractions, the 5 % of samples over 2 levels
at q100, the monitor's pixel pitch); the naming pass needed two ranges nobody had written down,
where a screen image's sky sits (0.05 to 0.10) and where a print's should (0.15 to 0.25).

## Decision

Every threshold lives in `Thresholds.cs`, with a comment saying where it came from; changing one
means changing its comment, and since 2026-10-02 its requirement row. No threshold is a literal in
an analyzer, a rule or a text. A number a window text needs is lifted into `Thresholds` first,
with its comment, even when no rule reads it: `ScreenBackgroundLo`/`Hi` and
`PrintBackgroundLo`/`Hi` exist only for a tooltip. The thresholds are rules of thumb from one
image, a starting point, and the report shows each one as `prag` next to the figure so the owner
can judge.

## Alternatives rejected

- **Literals where they are used.** A text and a rule would drift: the window would explain a
  threshold the rule no longer applies.
- **Present the thresholds as standards.** They come from one image; a reader who took them for
  physics would trust a 2 % boundary more than it deserves.

## Consequences

- `docs/design/thresholds.md` keeps the table; every requirement row quotes the names and values
  it uses.
- Three literals were lifted during the explanation pass (`EdgeRegion*Fraction`,
  `JpegOverThresholdWarnPct`, `ScreenPixelMm`) and two during the naming pass.
- Guards: none by test; `ViewModelTests` and `ParameterFieldsTests` check that the texts carry
  the interpolated numbers.

Source: CLAUDE.md (2026-09-21); docs/specification.md §7, §15; docs/implementation-plan.md.
