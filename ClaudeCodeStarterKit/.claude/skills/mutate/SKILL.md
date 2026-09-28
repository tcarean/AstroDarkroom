---
name: mutate
description: Run mutation testing (Stryker.NET) on the core library, or one file of it, and turn the surviving mutants into proposed tests. Use after adding or changing numeric code, before a release, or when the user says /mutate. Slow (minutes per file); never part of the commit gate.
---

# Mutate

Mutation testing measures the tests, not the code: Stryker changes the core in small plausible
ways (a `+` to a `-`, a `<` to a `<=`, a constant, a condition), one mutant at a time, and runs
the tests. A mutant that survives is a line no test really checks. Before step 1, read
`docs/skills.md` if it exists: its `## /mutate` section adds project-specific steps and says
where they go.

1. Ask which file or area, unless the user named one. A whole-core run is fine on a small test
   suite; on a large one always pass a file filter, because Stryker runs the whole suite once
   per mutant: `scripts/mutate.ps1 -Mutate "**/Stats.cs"`. Stryker runs from the test project
   `kit.json` names under `mutate.testProject`.
2. Run `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/mutate.ps1 [-Mutate ...]`.
   Paste the summary line (total, killed, survived, no coverage, score) and the survivors list.
3. For each survivor, say in one line what behaviour it changes and whether that behaviour is a
   requirement (name the R-ID) or an equivalent mutant (no observable change; say why).
4. For each non-equivalent survivor, propose the test that would kill it: name (with the R-ID),
   input, expected value. Do not write the tests yet; the user picks which ones are worth it.
5. If the user accepts, write the tests, run `scripts/verify.ps1`, then re-run `mutate.ps1` on the
   same filter and paste the new score. New tests alone get no CHANGELOG line; a bug they
   expose, once fixed, gets one under `Fixed`.
6. Never "fix" a survivor by weakening the mutation config or ignoring the file. The score is a
   measurement, not a target; 80 to 90 % on numeric code is realistic, 100 % is not.
