# 0083. The Structure explanations are in plain words, the same in the console

Date: 2026-11-01
Status: accepted 2026-11-01

## Context

Right after the End to end tab's explanations were put in plain words (ADR 0082), the owner asked
for the same on the Structure tab. Its texts were the method's: deviation from radial symmetry, a
purely radial model, the fit, a gradient and its curvature, scale-invariant, sampled azimuths,
sectors, a Lambertian diffuser, an achromatic profile, the 2x2 mean of the mosaic, a degenerate
fit. Forty-two texts, of two kinds: twenty-one live only in the window (the notes under the five
tests and the informative rows, the filter note, the lens-choice line, the Adobe paragraph, the
"How to read" guide, the map guide) and twenty-one are the console's too (`StructureReport`: the
advices, the scale, why the residual matters, the local-spot note and warning, the
illumination-offset explanation, the depth comparison's block). The owner approved the Romanian
texts one by one and said yes to changing the console's too.

## Decision

Every explanation of the tab is in plain words: a usual vignetting, symmetric about the centre;
one side of the frame brighter than the other; a small compact spot; the sky. R-122 carries that
as a requirement, and `R122_` tests check the words of the method are absent from the texts, in
both languages, as whole words. The thresholds stay read from `Th`.

Three things stay as they were, on purpose. The names of the five tests and of the informative
rows ("reziduu dupa model rad.", "asimetrie azimutala max", "pata maxima (o dala)") are labels in
every saved report and in the README; the notes under them now say what they mean, and renaming
them is a task of its own. The "cannot compare" reasons name the missing input and the option
that gives it, which is their whole use. The map guide's row on concentric circles drops the
history of the corrected ring artefact (the symmetrised profile, the even term), which stays in
the README and in `radial-model-even-term.md`: a reader of the window needs to know a visible
ring is centred elsewhere, not how the model was fixed.

The texts shared with the console change in the console too, one voice for one verdict (the
reason of ADR 0082); the parity goldens that print them were recaptured with the owner's yes for
their text lines alone, every figure unchanged (`python-parity.md` lists them).

## Alternatives rejected

- **Renaming the tests in the same task.** "asimetrie azimutala" is jargon, but the label is in
  every golden, every saved `Test result.txt` and the README's tables; the note under it carries
  the meaning at no such cost.
- **Keeping the ring artefact's history in the window.** It explains a bug that no longer shows,
  to a reader who never saw it.

## Consequences

- The `structure_*`, `en_structure_*`, `against_*` and the parity goldens that print an advice,
  the scale, the offset note or the depth block carry the new sentences. A C#-only call (ADR 0047).
- `thresholds.md`'s table of the residual scale and the README's tables of the tests, of the scale,
  of the map guide and of "who decides" quote the new English texts.
