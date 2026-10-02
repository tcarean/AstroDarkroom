# 0011. Every recommendation carries its evidence, in decision order, and the exit code follows the verdict

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

Retroactive: recorded on 2026-10-02 from the old `CLAUDE.md` ("Every recommendation carries its
evidence", the exit codes), `docs/specification.md` §6 and §9, and `docs/implementation-plan.md`
("The report"); the decision itself dates from phase 4, 2026-09-20. The report is a list of
decisions the owner takes alone, in front of a print lab's order form: which file to send,
whether to re-stretch, what to fix, how large to print, how much to denoise. A bare "do X" cannot
be checked or weighed, and a table of metrics leaves the decisions to be assembled by the reader.

## Decision

- A `Recommendation` has a topic, a verdict, a text and a `Because`; the constructor refuses an
  empty `Because`. The `Because` names the metric and the threshold it was judged against.
- The report is in decision order, not metric order: Format, Background, Colour, Size, Denoise,
  Warnings, then the appendices with the tables.
- Verdicts are monotone, Ok < Attention < Problem; a section's verdict is its maximum, the overall
  verdict the maximum of all.
- The exit code is 0, 2 or 3 from the overall verdict, and 1 for an error, so any non-zero code
  means "look".

## Alternatives rejected

- **Metric order** (file, tones, export, resolution, colour). It is the order of the analyzers,
  and the reader would have to turn it into decisions.
- **A single score.** It hides which decision is wrong and by how much.
- **The same exit code for an error and a problem.** A script could not tell a run that failed
  from an image that needs work.

## Consequences

- A new rule cannot be written without its evidence; `RecommendationsTests` checks that every
  recommendation has a `Because` and that the topics are in decision order, and that the exit code
  follows the verdict; the golden files pin the wording.
- R-002 and R-090 to R-096 state the rules.

Source: CLAUDE.md (2026-09-21); docs/specification.md §6, §9; docs/implementation-plan.md.
