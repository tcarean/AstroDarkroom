# 0043. A median the user reads is said "on average", the usual exposure stays "usual"

Date: 2026-11-08
Status: accepted 2026-11-08
Supersedes: 0033 in part (`de obicei` for the median); 0034 in part (the session's usual width and count)

## Context

The plain-words rewrite (ADRs 0032 to 0036) named the medians of the tabs and of the console's
sections `typically` or `usual` (`de obicei`, `obisnuit`), since `median` and `mean` name the
method and the test helper `PlainWords` also refused the Romanian `medie`. On 2026-11-08 the
owner asked to say `average` (`in medie`) wherever these words mean a value the frames have on
average: anyone understands an average. Nine texts carry such a figure, all of them medians: the
stars' width and elongation of a frame (its stars' median), the session's width and star count
(the median over the good, or all, frames), the zones' widths and the median dither move. Shown
side by side the same day, the nine rewrites were approved as proposed. The `--frame` report of
one frame, never rewritten in plain words, says `median`; the request named only `typically` and
`usual`, so it stays.

## Decision

A median the user reads is said `on average` / `in medie` (`the session's average width` /
`latimea medie din sesiune`, `a frame's average count` / `din cate are in medie un cadru`); the
figure stays the median, only its word changes. `PlainWords` no longer refuses `medie`; `median`,
`mean`, `mediana` and `mediane` stay refused. The usual exposure of R-085 keeps `usual` /
`obisnuita`, because it is the exposure most frames carry, not an average, and `Blank (the usual
way)` of the pixel-size field keeps its word, which means "normally" there.

## Alternatives rejected

- **Keep `typically` / `de obicei`.** The owner found `average` plainer, and nothing a reader does
  with the figure depends on it being the median rather than the mean.
- **Compute means to match the word.** It would move every figure and lose the median's
  robustness to a cloudy or trailed frame, for a word.
- **Say `average` for the usual exposure too.** With 80 frames of 120 s and one of 0.2 s the
  average is 118.5 s while the text shows 120.0 s, so the reader would take the figure for wrong.

## Consequences

The golden console texts change on the SUMMARY focus row, the FOCUS rows and verdict, the
how-far row and the wide-stars and clouds reasons, and nowhere else; R-036, R-077, R-078, R-079
and R-081 quote the new words, `PlainWords` checks every tab against the shorter list, and the
lists of Romanian words an English text never holds gain `medie`. V-010's first `cat de mult`
cell reads `in medie`, at the owner's word (`docs/validation.md`). A new text naming a median
uses `on average` / `in medie`.
