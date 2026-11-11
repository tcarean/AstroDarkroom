# 0019. The front page speaks impersonally, never of the author

Date: 2026-11-11
Status: accepted 2026-11-11; the equipment it names superseded by 0020

## Context

The repository's front page, its root `README.md` (ADR 0008), spoke of its owner in the third
person on two lines: "On the author's computer" and "The tools were built around the author's own
equipment and validated on the author's sessions". On 2026-11-11, reading DarksCheck's manual in
the Astro Darkroom wiki, the owner objected to the same wording there ("checked on the author's
own nights"): nobody speaks of themselves in the third person, and the text read as if someone
else, an AI, described them. The user documents go out as the owner's own writing; the owner
asked for impersonal wording and gave the tone by an example, "the tests were done on a Nikon
D3300". The wiki's manuals changed that day, and the owner approved the front page's two lines.
The front page lies outside every kit project and its gate, so its decisions are recorded here.

## Decision

The front page says impersonally where a figure was measured: "On the test computer", "The tests
and the validation were done with this equipment", never "the author's". The equipment and the
figures it names stay as they were; only the person goes.

## Alternatives rejected

- **The first person** ("my equipment", "on my computer"). The owner asked for impersonal
  wording, and the front page already speaks to its reader as "you".
- **Keeping "the author".** It is the wording the owner objected to.

## Consequences

- A sentence added later to the front page or to this project's `README.md` that says where a
  figure comes from names the tests or the test computer, never "the author".
- The READMEs of the five tools took the same change the same day, each with an ADR of its own.
