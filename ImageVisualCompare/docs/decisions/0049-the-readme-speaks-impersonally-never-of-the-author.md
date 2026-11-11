# 0049. The README speaks impersonally, never of the author

Date: 2026-11-11
Status: accepted 2026-11-11

## Context

`README.md` spoke of its owner in the third person once: "the figures measured on the author's
files of one 23 MP frame, three exports and two FITS stacks". On 2026-11-11, reading DarksCheck's
manual in the Astro Darkroom wiki, the owner objected to the same wording there ("checked on the
author's own nights"): nobody speaks of themselves in the third person, and the text read as if
someone else, an AI, described them. The user documents go out as the owner's own writing; the
owner asked for impersonal wording and gave the tone by an example, "the tests were done on a
Nikon D3300", and approved the README's line.

## Decision

The README says impersonally what the figures were measured on: "the test files", never "the
author's". The files it names stay as they were; only the person goes.

## Alternatives rejected

- **The first person** ("my files"). The owner asked for impersonal wording, and the README
  already speaks to its reader as "you".
- **Keeping "the author".** It is the wording the owner objected to.

## Consequences

- A sentence added later that says where a figure comes from names the tests or the test files,
  never "the author"; the same holds for every other user document of ImageVisualCompare.
- The READMEs of the other tools and the repository's front page took the same change the same
  day, each with an ADR of its own.
