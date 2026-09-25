---
name: new-feature
description: Start a new feature the project way - requirement first, acceptance test second, plan third, code last. Use when the user asks for new behaviour or says /new-feature.
---

# New feature

Follow the order. Do not write implementation code before step 4 is approved.

1. **Requirement.** Read `docs/requirements.md`. Draft the new entries with the next free IDs,
   status `draft`: observable behaviour, one concrete example with numbers, error behaviour,
   the exit code. Show the draft to the user and wait for it to be accepted before going on.
2. **Acceptance test.** Write the black-box test(s) named after the requirement IDs
   (`R###_...`), in `FlatCheck.Tests` for the console and the core, in `FlatCheck.Wpf.Tests`
   for the window. A console mode also gets a golden scenario in `GoldenOutputTests` and, if
   it computes a figure `flatcheck.py` can compute, a `crosscheck.py` scenario. If the feature
   has a figure measured on a real master, add a case to `docs/validation.json` with
   `expected` left as `null` and a note in `docs/validation.md` saying it is unmeasured.
   The tests must fail now. Run them and paste the failing output.
3. **Decision.** If the feature needs a design choice with more than one reasonable answer,
   write `docs/decisions/NNNN-slug.md` (copy `0000-template.md`) with status `proposed` and the
   alternatives, and ask. Only the owner's explicit yes in this session makes it `accepted`
   (with the date); then, and not before, go on to the plan and the code. Measure before you
   design: on this project a design chosen without a measurement has been wrong three times
   (see `docs/design/README.md`).
4. **Plan.** Enter plan mode. List the files to touch, the order, and how each step is verified.
   After approval save the plan to `docs/plans/YYYY-MM-DD-slug.md`.
5. **Code.** Implement in `FlatCheck.Core` first (`Analysis/`, `Report/`, `Runs/`), then the
   console and the window, which only call `Runs/`. Nothing is computed outside the core. Keep
   `scripts/verify.ps1 -SkipTests` green while editing; run the full gate before claiming done.
6. **Parity.** If the change touched a formula (`Metrics.cs`, `Stats.cs`, `Analysis/`,
   `Report/`) or `flatcheck.py`, mirror it in `flatcheck.py` and run `scripts/crosscheck.ps1`;
   paste the summary line. Golden files are re-approved (`FLATCHECK_UPDATE_GOLDEN=1`) only
   after the owner has read the diff and said yes.
7. **Measure.** Fill in the `validation.json` figures from real output, paste the output in the
   session, and record the date in `validation.md`.
8. **Record.** Set the requirement status to `implemented`, add the CHANGELOG line, and update
   `README.md` (the user documentation, in Romanian) if the behaviour is user-visible; then
   `/wrap-up`.
