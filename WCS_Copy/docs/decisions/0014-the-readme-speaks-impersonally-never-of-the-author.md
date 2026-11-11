# 0014. The README speaks impersonally, never of the author

Date: 2026-11-11
Status: accepted 2026-11-11

## Context

`README.md` spoke of its owner in the third person once, in its first paragraph: "It is a small
tool for the author's own stacks". On 2026-11-11, reading DarksCheck's manual in the Astro
Darkroom wiki, the owner objected to the same wording there ("checked on the author's own
nights"): nobody speaks of themselves in the third person, and the text read as if someone else,
an AI, described them. The user documents go out as the owner's own writing; the owner asked for
impersonal wording and gave the tone by an example, "the tests were done on a Nikon D3300", and
approved the README's new line, "It is a small tool: it reads no pixel and needs no other
program."

## Decision

The README speaks impersonally and does not say whose stacks the tool is for; where a figure was
measured, it names the tests, never "the author". What the tool does stays said as it was.

## Alternatives rejected

- **The first person** ("my own stacks"). The owner asked for impersonal wording, and the README
  already speaks to its reader as "you".
- **Keeping "the author".** It is the wording the owner objected to.

## Consequences

- A sentence added later names the tests or the test files where it says where a figure comes
  from, never "the author"; the same holds for every other user document of CopyWcs.
- The READMEs of the other tools and the repository's front page took the same change the same
  day, each with an ADR of its own.
