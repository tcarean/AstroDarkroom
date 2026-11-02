# 0036. The Rejected tab is in plain words

Date: 2026-11-02
Status: accepted 2026-11-02

## Context

ADR 0032 made plain words the rule for every text the user reads; the Dithering, Summary, Frames
and Quality tabs were rewritten first (ADRs 0032 to 0035). The Respinse (Rejected) tab was the
last. Its four explanation paragraphs spoke in the method's words (`raportul dintre axa lunga si
axa scurta`, `mediana pe cadru`, `(coerenta)`) and listed the causes of a trail as bare nouns. The
line under its heading, which the console prints as `RESPINSE (...)`, put every count in the
plural: `1 cadre slabe, 1 necitite`. The owner approved the six proposals in chat on 2026-11-02,
"in the console too".

## Decision

The Rejected tab takes the approved words (R-081). The heading line counts through
`SeriesReport.Count`, singular for one (`1 cadru slab si 1 cadru necitit`), and says that the
marginal frames stay in the stack; it is composed once, for the window and the console. The
paragraphs say what the elongation is, as how many times longer than wide the stars are, where
the marginal frames are listed, that the reason names the mount axis when the frame's stars were
all stretched about the same way, and what a trail on each axis comes from, `backlash` kept beside
its plain name (`jocul din angrenaje`). The thresholds stay read from `Th`. The title `RESPINSE`
and the frames' reasons (R-079) stay. `coherence` joins the list of `PlainWords`.

## Alternatives rejected

- **The window only.** The heading line is composed once (ADR 0008) for the window and the
  console's REJECTED line; ADR 0032 rejected the same split.

## Consequences

- `R081_TheRejectedHeadingAndExplanationAreInPlainWords` (the heading's words on two sessions and
  the paragraphs, both languages) and `R081_TheRejectedTabUsesNoneOfTheMethodsWords` (the rendered
  tab, both languages) check it.
- The goldens `series` and `series-classes` and their English twins change on their RESPINSE
  line; every number in them is unchanged.
- Every tab of the window now speaks in plain words.
