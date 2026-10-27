# 0018. An edge region that departs in colour is a problem to fix; one that brightens neutrally is the object

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from `docs/specification.md` §5 (E2) and §6 (the Colour
rows), `docs/acceptance-de-printat.md` ("Edge regions that brighten neutrally are the object, not
a gradient") and `docs/implementation-plan.md` ("Edge-region verdicts changed after the real
image"); the decision dates from the acceptance run, 2026-09-21. The first run on the reference
image flagged all eight edge regions as residual gradient, because the Milky Way band enters the
top-left corner: +11 / +10 / +16 levels against the background, a neutral brightening. The corner
the conversation had found, bottom-right, reads +9.4 / +1.7 / -9.9: warm, horizon glow.

## Decision

A region more than 3 levels from the background in some channel is flagged. It is **coloured**
when its channels move differently, its deltas spreading more than 3 levels and more than 0.5 x
their mean absolute delta: light pollution or horizon glow, an Attention that says to fix it on
linear data or keep it as composition, named warm, cool, greenish or other. It is **neutral** when
its channels move together: most likely the object, one informative Ok note that sends the reader
to the Preview. On mono there is one channel, and no region is coloured. The window marks a
flagged region "! colorat" or "! neutru"; the report keeps "!".

## Alternatives rejected

- **Flag every departure.** Eight false gradients on the reference image.
- **Ignore neutral departures.** A neutral gradient left by a flat or by vignetting would vanish
  from the report; the note keeps it in view, for the eye to judge.

## Consequences

- On the reference image exactly the two warm regions the conversation had found remain
  Attention: the bottom-right corner and the bottom edge.
  (Restated 2026-10-27: those figures were against the darkest quarter. Against the sky
  patches of ADR 0055 the two warm regions remain, the horizon corner at +4.6 / -3.6 / -17.0,
  and the top-left corner, where the band enters, reads +6.5 / +5.0 / +8.8 on every pixel:
  bluer than the sky, coloured by this rule, where it read +11 / +10 / +16, neutral, against the
  dust; on its sky pixels alone, ADR 0056, it reads +4.2 / +2.9 / +5.6, neutral again. The rule
  stands; the measurements are in `docs/plans/2026-10-27-sky-colour-from-the-sky-patches.md`
  and `docs/plans/2026-10-27-edge-regions-on-sky-patches.md`.)
- A neutral brightening at the edge cannot be told from the object by the tool
  (`known-limitations.md`).
- Guards: `BackgroundAnalyzerTests`, `RecommendationsTests`, the golden files, validation case
  V-001.

Source: docs/specification.md §5, §6; docs/acceptance-de-printat.md; docs/implementation-plan.md.
