# 0012. The window gets its tables, paragraphs and axis labels from the report; the console keeps its one-line rows

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

Reading the window on the Veil session, the owner asked for the DITHERING figures as tables
instead of the long rows, the RA/DEC sentence of the Respinse explanation on its own line in
bold, the mount axis in the `dX`/`dY` headers, and a verdict on the FOCUS card, which alone
among the cards had none (ROUA and DITHERING close with a `verdict` row). ADR 0008 says the
window arranges what the report gives and computes nothing; until now the report gave only
labelled rows of prose, so a table or a bold sentence had no source to bind to. The map's
`X = DEC` / `Y = RA` labels were the one piece of wording the view-model chose itself.

## Decision

The report (`Report/`) gains structured outputs beside its rows: `ReportTable(Title, Columns,
Rows)` of strings for the five dithering tables, `ReportParagraph(Text, Emphasis)` for the
Respinse explanation, `AxisLabels` and `PositionHeaders` for the axis words, and
`DitherScalarRows` for the dithering rows that are not tables. FOCUS closes with a `verdict`
row carrying the REZUMAT focus verdict, the same shape as ROUA's. The console prints the
rows it printed before, plus the FOCUS verdict row; it does not print the tables, since its
one-line rows already carry every figure and the goldens pin them. The window binds the new
types as they are and composes nothing.

## Alternatives rejected

- **Tables in the console too.** Every figure is already on the console's rows; tables there
  would lengthen the report for a reader who has the window, and change every golden.
- **The view-model splitting the rows into tables.** It would parse prose the report wrote,
  the computation ADR 0008 keeps out of the window.
- **The verdict chip on the FOCUS trend row** (the first draft). A `verdict` row like ROUA's
  keeps one shape per card and leaves the trend row's text as it was.
- **Bold by marking the text** (a `*...*` convention). An `Emphasis` flag on the paragraph is
  explicit and needs no parsing in the window.

## Consequences

`R036` pins the tables, the paragraphs, the labels and the FOCUS verdict row on the fixture;
`R037` the window's bindings; the two series goldens change by one FOCUS line each. A new
dithering figure goes in a row and, when it is tabular, in a table, both in `Report/`.
