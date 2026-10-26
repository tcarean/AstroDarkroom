# 0003. Romanian without diacritics for the user, English for the process and the code

Date: 2026-10-25
Status: accepted 2026-10-25; Romanian as the only language of what the user sees superseded by 0006

## Context

The kit's rule "Languages" (its ADR 0012): English in everything the model reads or writes,
Romanian only in the guide and the READMEs. The five components vary it the same way
(FlatCheck's ADR 0006, LightsCheck's ADR 0005): what the user sees is Romanian, since the reader
is the owner. The launcher's rows and its "Despre" window sit beside those windows and are read
by the same person. The components write Romanian without diacritics (AstroPhotoAnalyzer's
convention, kept in its CLAUDE.md), so that one font and one code page show every text the same.

## Decision

Two zones. Romanian, without diacritics: everything the user sees, the catalogue's titles and
descriptions in `AppCatalogue`, the window's labels, tooltips and the "Despre" texts, and
`README.md`. English: everything about the process and every source file (code and comments,
tests, `docs/`, `CHANGELOG.md`, the ADRs, commits). A file is never in two languages: a Romanian
string sits in an English file as a constant with an English comment.

## Alternatives rejected

- **The kit's rule verbatim.** The launcher's rows would be the one English window in a suite
  of Romanian ones.
- **Romanian with diacritics.** The components do without; a launcher with them would spell the
  same tool names two ways in one folder.

## Consequences

The design note quotes Romanian labels inside English prose. The tests compare the catalogue's
Romanian texts as written in `docs/requirements.md`, where they are quoted.
