# 0053. A set that does not say its settings is compared as made at the other set's

Date: 2026-11-13
Status: accepted 2026-11-13

## Context

ADR 0004 compares two sets after dividing each by its own factor, read from its ISO and its
exposure. A frame that says neither has the factor 1 (`Normalisation.NormOf`), which stands for
"unknown", not for ISO 400 and 120 s. R-052 handled one direction only: a light that does not
say its settings is read at the darks' factor, with an `ATENTIE` line. The code review of
2026-11-13 (CR-002) showed the others: darks as FITS without `ISOSPEED` against NEF lights at
ISO 1600 read a ratio of 0.25 at the same temperature (`do not match ... too warm`, the lights'
index a quarter of the darks'), and darks at ISO 1600 against a partner without `ISOSPEED` read
0.25 too, 4.00 the other way round, beside a partner row that prints `setari necunoscute`. The
tests of this task reproduced 0.25 on synthetic sets before the fix. The owner chose on
2026-11-13, in chat, between comparing at the same settings and refusing to measure.

## Decision

When one of two compared sets does not say its ISO or its exposure and the other does, the two
are compared as made at the same settings, and the report says so in an `ATENTIE` line:

- lights against darks: a light is read at the darks' factor when the light or the darks do
  not say their settings (`LightsAnalyzer.FactorOf`), in the ratio of check 2 and in the noise
  of a light that R-067 and check 9 read;
- darks against a partner: the partner's master is read at the session's darks' factor for the
  ratio and for the overlap levels (`DarkSeriesRun.MatchPartner`, `PartnerAnalysis.Silent`).

Each set's own index stays at its own factor, and the `INDEX` row of a set that does not say
its settings ends `in ADU din fisier, pentru ca fisierele nu spun ISO-ul sau expunerea` instead
of `socotita pentru ISO 400 si 120 s`. Two sets that both say their settings, or neither, are
compared as before.

## Alternatives rejected

- **Unmeasured, with the reason.** Safer when the silent set was in fact made at other
  settings, but it drops check 2 and the partner match for every FITS set without `ISOSPEED`,
  and it is not what R-052 already does for a silent light; the owner chose one rule for all
  three cases.
- **The silent darks take the lights' settings as their own factor.** The index would come out
  in ADU-eq, but every figure of the darks would then depend on whether lights are present, and
  a library, which has none, would read differently from the same darks in a session.
- **The partner's factor for both when the session is the silent one.** The ratio is the same
  either way; the session's factor keeps the overlap levels in the unit of every other figure
  the report gives for the open folder.

## Consequences

A comparison with a silent set is right only if the two were in fact made at the same settings;
the `ATENTIE` line is what tells the reader it was assumed, and check 1 says `NEMASURAT` on the
settings in the same report. `Normalisation.Says` is the test the comparisons use for "says its
settings" (an ISO and an exposure, both above 0); `DarkSeriesAnalysis.SaysSettings` carries it
for the darks. The rows that print a set's settings (the scale section's `1 ADU-eq`, the
partner's row) ask only whether the two are there; for a file that was read the answers are the
same, since the readers already turn an ISO or an exposure that is not positive into "not said".
Guarded by `R052_DarksThatDoNotSayTheirSettingsAreComparedWithTheLightsAsMadeAtTheSame`,
`R063_APartnerOrASessionThatDoesNotSayItsSettingsIsComparedAsMadeAtTheSame` and
`R060_TheIndexRowSaysWhenTheFilesDoNotGiveTheSettings`. Sets that say their settings are
compared exactly as before.
