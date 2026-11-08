# 0093. The histograms' level is called the average level

Date: 2026-11-08
Status: accepted 2026-11-08
Supersedes: 0084 in part (the words the typical level)

## Context

The plain-words texts of the Exposure tab (ADR 0084) called the level the histograms' vertical
line marks `the typical level` / `nivelul tipic`, in five texts, all of them the window's: two
sentences of the reading under the histograms (the centre that saturates "whatever the typical
level", and the level over the whole frame below the centre), the two captions under the
histograms (the raw data and the camera's preview), and the guide's row for the vertical line.
On 2026-11-08 the owner asked to replace, in every text the user reads, the words usual, typical
and usually where they mean "on average" with `medie` / `average`, because anyone understands an
average. Shown every use of those words, original and proposal side by side, the owner answered
"Da, cele cinci".

## Decision

1. **The level is `the average level` / `nivelul mediu`** in those five texts and in every new
   one: "whatever the average level", "The average level over the whole frame", "the centre's
   average level" in both captions, and the guide's "The average level at the middle of the
   frame".
2. **The figures do not change.** The level is the middle value of the pixels (the median), not
   their mean; the everyday word is the owner's choice, as in DarksCheck's ADR 0035.
3. **The words that do not mean "on average" stay**: `Typical cause` / `Cauza tipica` (the most
   frequent cause of a processed file), `Usually` / `De obicei` the flats were shot at another
   aperture, the sky at the blue hour `usually` / `de obicei` blue (both "most often"), `a usual
   vignetting` / `o vignetare obisnuita` (an ordinary one, symmetric about the centre), and the
   single profile's note `they usually differ by ~0.2 stops` / `de obicei difera cu ~0.2
   stopuri`.
4. **The tests hold it**: `R123_TheHistogramsLevelIsCalledTheAverageLevel` checks the reading and
   both captions in both languages, and `R123_TheVerticalLineRowIsTheRequirementsExample` the
   guide's row.

## Alternatives rejected

- **`in medie` for the profiles' ~0.2 stops.** The 0.2 stops (`Th.ProfileDatabaseSpreadStops`)
  were measured on one lens, the Tokina 11-20 (`flat-sources.md`), not averaged over several, so
  "on average" would claim a figure nobody computed.
- **`vignetare medie` for the usual vignetting.** It means an ordinary, round vignetting; an
  average vignetting is another thing.
- **`the middle level` / `nivelul din mijloc`**, exact for a median: longer, and the owner asked
  for `medie`.

## Consequences

- R-123 and the README's histogram guide quote the new words.
- No golden changes: the five texts are the window's alone, not printed by the console.
