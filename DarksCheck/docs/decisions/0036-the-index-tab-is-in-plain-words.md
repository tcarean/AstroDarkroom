# 0036. The Index tab is in plain words

Date: 2026-11-06
Status: accepted 2026-11-06; the partner's sentence (kept after the program closes) superseded by 0041

## Context

ADR 0028 put every text the user reads in plain words, one tab at a time; the ADU scale tab, the
Rezumat (ADR 0030), Cadre (ADR 0031), Harti (ADR 0032), Pixeli fierbinti (ADR 0033) and Lights
(ADR 0034) are done, and a typical value is `medie` (ADR 0035). The owner picked the Index tab
next. Its INDEX rows are the Rezumat's. Its own texts are mostly plain; three are not: the text
under the label named the method (`mediana amplitudinii`, `rangurile`, `normalizata`, `idx<NN>`),
the partner's explanation spoke of `grade echivalente` and of the lights `prin dark-uri` without
saying what is compared, and the hint of `Analizeaza din nou` read `Ruleaza din nou folderul`. The
owner approved the list of the three, original and proposal side by side, with "Da, toate".

## Decision

1. **The rule of ADR 0028 holds**: what a figure means for the frames, in short sentences and
   everyday words.
2. **The text under the label says the index in the INDEX row's words**: `valoarea medie a
   pixelilor fierbinti din master de pe locurile 5-20 pe fiecare megapixel, numarand de la cel mai
   fierbinte, socotita pentru ISO 400 si 120 s`, then which way it goes (`Cu cat senzorul a fost
   mai cald, cu atat indexul e mai mare`), and the label as `idx si indexul rotunjit (idx- cand
   indexul nu se poate masura)`.
3. **The partner's explanation says what is compared and what comes out**: the session's darks
   against the partner's master on the pixels hot in both, by how many degrees they are warmer or
   colder, the lights through the session's darks, or directly against the partner's darks when
   the session has none (R-054), and that the choice is kept after the program closes. The list's
   first wording left out the `idx-` of an index that cannot be measured and said the partner's
   darks are used without saying what for; the review found both and the owner approved the two
   fixes.
4. **`Analizeaza din nou` says it analyses**: `Analizeaza din nou folderul deschis, cu partenerul
   si setarile de acum`.
5. **R-087 guards it**: `R087_...` renders the tab with and without a partner in both languages, with the word
   list of R-083 to R-086, which now also holds `rang` / `rank` and `echivalent` / `equivalent`.

## Consequences

- R-087 is new; R-071's description of the Index tab holds.
- The console's `--help` still says `in grade echivalente`; it belongs to the console's own texts,
  the Raport task's.
