# 0044. The README speaks impersonally, never of the author

Date: 2026-11-11
Status: accepted 2026-11-11; the nights and the equipment it names superseded by 0045

## Context

`README.md` spoke of its owner in the third person, as "the author", on three lines: "(the
author's Nikon D3300 with a Samyang 135 mm on a Star Adventurer GTi)", "two of the author's
nights", "On the author's computer". On 2026-11-11, reading DarksCheck's manual in the Astro
Darkroom wiki, the owner objected to the same wording there ("checked on the author's own
nights"): nobody speaks of themselves in the third person, and the text read as if someone else,
an AI, described them. The user documents go out as the owner's own writing; the owner asked for
impersonal wording and gave the tone by an example, "the tests were done on a Nikon D3300".
LightsCheck's wiki page changed that day, and the owner approved the README's three lines one by
one.

## Decision

The README says impersonally where a figure was measured: "tested with a Nikon D3300 and a
Samyang 135 mm", "two test nights", "the test computer", never "the author's". A figure, a night
or a piece of equipment it names stays as it was; only the person goes.

## Alternatives rejected

- **The first person** ("my Nikon D3300", "on my computer"). The owner asked for impersonal
  wording, and the README already speaks to its reader as "you".
- **Keeping "the author".** It is the wording the owner objected to.

## Consequences

- A sentence added later that says where a figure comes from names the tests, the test nights or
  the test computer, never "the author"; the same holds for every other user document of
  LightsCheck.
- The READMEs of the other tools and the repository's front page took the same change the same
  day, each with an ADR of its own.
