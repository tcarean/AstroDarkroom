# 0023. "Despre" asks for Adobe's lens profiles before FlatCheck's first run

Date: 2026-11-11
Status: accepted 2026-11-11

## Context

R-009's fourth paragraph of "Despre" opened with "Optional, for FlatCheck: Adobe's lens
profiles" ("Optional, pentru FlatCheck: ..."), the wording ADR 0007 took from the owner's
request of 2026-10-26. On 2026-11-11 the front page, this project's README (ADR 0022),
FlatCheck's README (FlatCheck's ADR 0098) and the suite's wiki stopped calling the profiles
optional and ask for them before FlatCheck's first run, since without them FlatCheck's check of
the vignetting rests on lensfun's profiles alone. ADR 0022 left the launcher's own text to a
decision of its own; the owner asked for it the same day ("fixeaza si in about").

## Decision

The fourth paragraph says, in both languages, that Adobe's lens profiles are to be installed
before FlatCheck's first run, which programs install them (the free Adobe DNG Converter first),
that without them FlatCheck compares the vignetting of the flat only with the lensfun database it
ships with, which is less reliable, and that the steps are in FlatCheck's manual in the suite's
wiki. R-009 and its test hold the opening words of both wordings, the wiki among the facts, and
no "optional" anywhere in the four paragraphs.

## Alternatives rejected

- **The manual's address in the paragraph.** "Despre" shows the paragraphs as plain text, so the
  87-character address could not be clicked; the README and the front page link it.
- **Calling the profiles required.** FlatCheck starts and runs every tab without them (FlatCheck's
  ADR 0073 has its window say in red what is missing).

## Consequences

The paragraph is longer in each language; "Despre" sits in a scroll viewer capped
at the work area's height, so nothing is cut. It was not looked at in the running window.
