# 0049. The README speaks impersonally, never of the author

Date: 2026-11-11
Status: accepted 2026-11-11

## Context

`README.md` spoke of its owner in the third person, as "the author", on eight lines: "made for
the author's Nikon D3300", "On the author's computer", "the author's darks of 2026-11-07". On
2026-11-11, reading DarksCheck's manual in the Astro Darkroom wiki, the owner objected to the
same wording there ("checked on the author's own nights"): nobody speaks of themselves in the
third person, and the text read as if someone else, an AI, described them. The user documents go
out as the owner's own writing; the owner asked for impersonal wording and gave the tone by an
example, "the tests were done on a Nikon D3300". The wiki's manuals changed that day, and the
owner approved the README's eight lines one by one.

## Decision

The README says impersonally where a figure was measured: "the test sessions", "the test
computer", "the test series", "the three test libraries", or a session by its date alone ("the
darks of 2026-11-07"), never "the author's". A figure, a date or a session it names stays as it
was; only the person goes.

## Alternatives rejected

- **The first person** ("my Nikon D3300", "on my computer"). The owner asked for impersonal
  wording, and the README already speaks to its reader as "you".
- **Keeping "the author".** It is the wording the owner objected to.

## Consequences

- A sentence added later that says where a figure comes from names the tests, the test computer
  or the session, never "the author"; the same holds for every other user document of
  DarksCheck.
- The READMEs of the other tools and the repository's front page took the same change the same
  day, each with an ADR of its own.
