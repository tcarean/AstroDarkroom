---
name: new-feature
description: Start a new feature the project way - requirement first, acceptance test second, plan third, code last. Use when the user asks for new behaviour or says /new-feature.
---

# New feature

Follow the order. Do not write implementation code before step 4 is approved. Before step 1,
read `docs/skills.md` if it exists: its `## /new-feature` section adds project-specific steps
(where the tests of each kind go, what a mode or a scenario also needs) and says where they go.

1. **Requirement.** Read `docs/requirements.md`. Draft the new entries with the next free IDs,
   status `draft`: observable behaviour, one concrete example with numbers, error behaviour,
   the exit code when there is one. Show the draft to the user and wait for it to be accepted
   before going on.
2. **Acceptance test.** Write the black-box test(s) named after the requirement IDs
   (`R###_...`), in the test project(s) `kit.json` lists under `testFolders`. If the feature has
   measurable output, add a case to `docs/validation.json` with `expected` left as `null` and a
   note in `docs/validation.md` saying it is unmeasured. The tests must fail now. Run them and
   paste the failing output.
3. **Decision.** If the feature needs a design choice with more than one reasonable answer,
   write `docs/decisions/NNNN-slug.md` (copy `0000-template.md`) with status `proposed`, the
   alternatives and your recommendation, and ask. Only the owner's explicit yes in this session
   makes it `accepted` (with the date); then, and not before, go on to the plan and the code
   without re-asking. A choice the owner answered in an earlier session is not asked again: the
   plan names its ADR, and the owner's approval of the plan is that yes. Measure before you
   design: a choice made on a guess is the kind an ADR later records as rejected.
4. **Plan.** Enter plan mode. List the files to touch, the order, and how each step is verified.
   After approval save the plan to `docs/plans/YYYY-MM-DD-slug.md`.
5. **Code.** Implement in the core library first (the compile boundary CLAUDE.md names), then
   the entry points, which only call it. Keep `scripts/verify.ps1 -SkipTests` green while
   editing; run the full gate before claiming done.
6. **Figures.** If the change touched numeric code (CLAUDE.md says which files), run `/validate`
   and paste its drift table. A golden file, where the project has them, is re-approved only
   after the owner has read the diff and said yes.
7. **Measure.** Fill in the `validation.json` figures from real output, paste the output in the
   session, and record the date in `validation.md`.
8. **Record.** Set the requirement status to `implemented`, add the CHANGELOG line, and update
   `README.md` (the user documentation) if the behaviour is user-visible; then `/wrap-up`.
