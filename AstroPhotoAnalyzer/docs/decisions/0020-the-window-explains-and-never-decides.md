# 0020. The window explains and never decides: its texts interpolate every number, its tables are the report's strings

Date: 2026-10-02
Status: accepted 2026-10-02; the described row's two equal columns superseded by 0080

## Context

Retroactive: recorded on 2026-10-02 from `docs/specification.md` §10 and §15 ("An explanatory text
in the window", "A new tab"), `docs/implementation-plan.md` ("Notes from phases 5-7", "The
explanation pass") and the old `CLAUDE.md` ("Nothing is computed outside the core", "The
window"); the decision dates from phase 5, 2026-09-20, and the explanation pass of 2026-09-21.
After phase 7 every metric row of the window got a description of what it measures, every table
its guide paragraphs, every preview tile what to check in it, and four fields their help texts,
proposed and reviewed tab by tab. They are sentences, and the core writes every sentence of
diagnosis (ADR 0007).

## Decision

- The window's explanatory texts, a row's `Description`, a table's `GuideParagraph`s, a tile's
  `Check` and a field's `*Help`, are the one kind of sentence a view-model writes. They explain
  what a figure is and how it is judged; they never judge the image. Every number in them is
  interpolated from `Thresholds`, `Parameters` or the result, never typed (ADR 0012).
- The tables are shown as the pre-aligned mono strings the report writes, through one template,
  instead of one XAML template per table.
- The parameters are text fields, parsed once when "Analizeaza" is pressed: a text box bound to a
  number cannot be emptied while typing.
- Two layout rules came from the pass: a described row is laid on two equal columns, or the
  longest value narrows every description; a tile's texts wrap to the image's width, or a
  sentence widens the card.

## Alternatives rejected

- **The texts in the core.** They speak of the window's tabs and controls, which the report does
  not have; the core would have to know about tabs.
- **Typed numbers in the texts.** They drift from the constants they describe.
- **A template per table.** The same formatting a second time, beside the report's.
- **Fields bound to numbers.** The field refuses an empty value while the owner types.

## Consequences

- The texts have no golden files; `ViewModelTests` checks that the tab texts exist and
  `ParameterFieldsTests` that the help texts carry their key phrases, their interpolated numbers
  and their paragraphs.
- A flagged region reads "! colorat" or "! neutru" in the window and "!" in the report (ADR 0018);
  the Preview's step back is computed in the core (ADR 0007).
- R-026 and R-110 to R-117 state what each tab shows.

Source: docs/specification.md §10, §15; docs/implementation-plan.md; CLAUDE.md (2026-09-21).
