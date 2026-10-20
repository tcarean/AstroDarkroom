# 2026-10-20 Strip an existing idx suffix from the label

## Context

On 2026-10-20 the owner renamed the three libraries under `Flats and Darks/Darks` to their
labels (`...-idx67`, `...-idx47`, `...-idx33`). R-061 appended the tag to the folder's name as
it stood, so the first library read `2026-10-10-ISO1600-120s-15.8celsius-idx67-idx67` and the
validation cases V-005, V-006 and V-007 failed on the label alone (`docs/validation.md`,
History 2026-10-20). The label is built in one place, `IndexAnalyzer.Label`.

## Decision put to the owner (the kit's ADR 0030)

- A (recommended): strip one trailing `-idx<digits>` from the folder's name before appending
  the tag; a renamed folder keeps a stable label and an index drift shows as a changed number.
- B: leave R-061 as it was, keep the folders unrenamed, give V-005 to V-007 new expected labels.

The owner answered "A" on 2026-10-20 (ADR 0013).

## Steps

1. Amend R-061 in `docs/requirements.md`.
2. A failing theory `R061_AFolderAlreadyNamedWithALabelLosesItsOldSuffixBeforeTheNewOne` in
   `tests/DarksCheck.Tests/LightsAndIndexTests.cs`: the renamed library with `darks\`, a stale
   `-idx60`, a bare folder, a name whose `-idx67` is not trailing, a bare `-idx`, upper case.
3. `WithoutLabelSuffix` in `src/DarksCheck.Core/Analysis/IndexAnalyzer.cs`, called by `Label`.
4. ADR 0013; the README's `eticheta` sentence; `docs/design/lights-and-index.md`; CHANGELOG.
5. `/validate` with `DARKSCHECK_TESTDATA_LARGE=E:\Photografie\Astro`; record in
   `docs/validation.md` that V-005, V-006 and V-007 pass again, `docs/validation.json` unchanged.
6. `/wrap-up`: the gate, the commit on `claude/Strip-an-existing-idx-suffix-from-the-label`, the
   merge proposal.

## Verification

- The R061 tests (seven cases) pass; the gate is green.
- The validation run shows V-005, V-006 and V-007 passing on the renamed library, pasted in
  `docs/validation.md`.
