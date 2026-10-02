# 0013. "Fundal" names the sky; its level and its colour are two measurements, named apart

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from the old `CLAUDE.md` ("\"Fundal\" names one thing, the sky,
and two measurements of it"), `docs/specification.md` §10 ("Layout") and
`docs/implementation-plan.md` ("The naming pass"); the decision dates from 2026-09-21 (commit
7bcdaf5, "Renamed Fundal to Culoare everywhere"). Writing the tooltip of the target-background
field exposed that "fundal" named two different numbers: the Tonuri tab's `BackgroundMedian`, the
median of the green channel over the whole frame on a 0..1 scale, which the print simulation
lifts; and the tab then called Fundal, `BackgroundResult.Levels`, the per-channel medians of the
darkest quarter on a 0..255 scale, for the cast and the gradients. The only place that said so
was one row's description.

## Decision

The labels name the property, not the thing: the tab is called Culoare; the rows are "Nivelul
fundalului" (the level) and "Culoarea fundalului" (the colour); `Anexa B` names the level it
starts from and `Anexa E` is "Culoarea fundalului"; the background recommendation's `Because`
says "nivelul fundalului". Every label, row, appendix and `Because` that shows one of the two
says which, and no tab is called just "Fundal".

## Alternatives rejected

- **A third term**, such as "cer curat", for one of the two. Another word for the same sky, with
  the same ambiguity.
- **One measurement for both.** The level would no longer be the frame's median, which moves the
  acceptance figure 0.103 and the simulation built on it; or the colour would no longer come from
  the darkest quarter, which is what keeps the object out of the cast.

## Consequences

- R-040, R-060, R-092 and R-110 use the two names; the golden files carry them.
- A new figure about the sky says which of the two it is about, or is a third measurement with
  its own name.

Source: CLAUDE.md (2026-09-21); docs/specification.md §10; docs/implementation-plan.md.
