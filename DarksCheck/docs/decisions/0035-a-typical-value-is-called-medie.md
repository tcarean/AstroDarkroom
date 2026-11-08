# 0035. A typical value is called medie

Date: 2026-11-06
Status: accepted 2026-11-06; the usual of check 4's verdict superseded by 0040
Supersedes: 0028 in part (the word obisnuit of the dark current); 0030 in part (the word obisnuita of the index); 0031 in part (the word obisnuit of the noise guide line)

## Context

The plain-words tabs (ADR 0028) called a typical value `obisnuit` / `obisnuita` (`usual` or
`typical` in English) in seven texts: the dark current's meaning on the ADU scale tab, the `dark -
bias` row and the frame light row of check 2 (twice), the index, the darks' header for a FITS
without a black, and the Cadre tab's guide line. Approving the Lights tab's list (ADR 0034), the
owner wrote "peste tot in loc de obisnuita pune medie"; shown the seven, original and proposal side
by side, the owner answered "Da, toate".

## Decision

1. **A typical value is `medie`, `mediu` or `in medie`**, `average` in English, in every text the
   user reads, these seven and every new one: `pixelul mediu`, `variatia medie a cadrelor`,
   `(variatia medie 0.02)`, `valoarea medie a pixelilor fierbinti`, `ce aduna in medie un pixel`,
   `negrul luat din valoarea medie a cadrului 1`, `de 1.5 ori zgomotul mediu`, and on the Lights
   tab `valoarea medie a pixelilor` (ADR 0034).
2. **The figures do not change.** Most are middle values (the median of the pixels, or a spread
   read so that a few bright pixels do not pull it), not means; the owner was told that Siril lists
   such a figure as the median, and chose the everyday word.
3. **The tests hold it**: the Romanian word lists of R-081, R-082 and R-083 to R-086 take
   `obisnuit`, so a text that says it again fails them. The English `the usual` of check 4's
   verdict stays: its Romanian says `de obicei`, which the owner did not ask to change.

## Alternatives rejected

- **`valoarea din mijloc`** (the middle value), exact for a median: longer, and the owner asked
  for `medie`.

## Consequences

- R-081, R-082 and R-083 name `obisnuit`; R-020, R-028, R-071 and R-093 quote the new words.
- Six goldens take the new words on 16 lines and change nothing else; the README's examples
  follow.
