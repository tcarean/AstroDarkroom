---
name: new-feature
description: Start a new feature the project way - requirement first, acceptance test second, plan third, code last. Use when the user asks for new behaviour or says /new-feature.
---

# New feature

Follow the order. Do not write implementation code before step 4 is approved.

1. **Requirement.** Read `docs/requirements.md`. Draft the new entries with the next free IDs,
   status `draft`: observable behaviour, one concrete example with numbers, error behaviour.
   Show the draft to the user and wait for it to be accepted before going on.
2. **Acceptance test.** Write the black-box test(s) in `tests/*/AcceptanceTests.cs` named after
   the requirement IDs. If the feature has measurable output, add a case to `docs/validation.json`
   with `expected` left as `null` and a note in `docs/validation.md` saying it is unmeasured.
   The tests must fail now. Run them and paste the failing output.
3. **Decision.** If the feature needs a design choice with more than one reasonable answer,
   write `docs/decisions/NNNN-slug.md` (copy `0000-template.md`) with the alternatives and ask.
4. **Plan.** Enter plan mode. List the files to touch, the order, and how each step is verified.
   After approval save the plan to `docs/plans/YYYY-MM-DD-slug.md`.
5. **Code.** Implement in `Core` first, then the entry point. Keep `scripts/verify.ps1 -SkipTests`
   green while editing; run the full gate before claiming done.
6. **Measure.** Fill in the `validation.json` figures from real output, paste the output in the
   session, and record the date in `validation.md`.
7. **Record.** Set the requirement status to `implemented`, add the CHANGELOG line, then `/wrap-up`.
