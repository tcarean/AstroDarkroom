# 0096. The README speaks impersonally, never of the author

Date: 2026-11-11
Status: accepted 2026-11-11; the lenses it names superseded by 0097

## Context

`README.md` spoke of its owner in the third person, as "the author", eight times on seven lines:
"It was built and validated on the author's equipment", "Measured on the author's library", "the
author's script, for a camera without darks", "on the author's computer". On 2026-11-11, reading
DarksCheck's manual in the Astro Darkroom wiki, the owner objected to the same wording there
("checked on the author's own nights"): nobody speaks of themselves in the third person, and the
text read as if someone else, an AI, described them. The user documents go out as the owner's own
writing; the owner asked for impersonal wording and gave the tone by an example, "the tests were
done on a Nikon D3300". FlatCheck's wiki page changed that day, and the owner approved the
README's seven lines one by one.

## Decision

The README says impersonally where a figure was measured: "The tests and the validation were done
with this equipment", "the test masters", "the test Tokina", "the test computer", or the object
alone ("two Nikkor 35 masters"), never "the author's". A figure, a lens or a master it names stays
as it was; only the person goes.

## Alternatives rejected

- **The first person** ("my Tokina", "on my computer"). The owner asked for impersonal wording,
  and the README already speaks to its reader as "you".
- **Keeping "the author".** It is the wording the owner objected to.

## Consequences

- A sentence added later that says where a figure comes from names the tests, the test masters or
  the test computer, never "the author"; the same holds for every other user document of
  FlatCheck.
- The READMEs of the other tools and the repository's front page took the same change the same
  day, each with an ADR of its own.
