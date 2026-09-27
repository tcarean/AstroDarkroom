---
name: validate
description: Re-measure the acceptance figures in docs/validation.json against the real data and report drift. Use after a change to numeric code, before a release, or when the user says /validate.
---

# Validate

The figures in `docs/validation.json` are the project's memory of "how well it works". This skill
re-measures them; it never edits them silently. The validation tests carry the trait
`Category=Validation`; where they read large files they are kept out of the gate by `kit.json`'s
`testFilter`. Before step 1, read `docs/skills.md` if it exists: its `## /validate` section adds
project-specific steps and says where they go.

1. Run `scripts/check-testdata.ps1`. List which large files are present and which are absent
   (the folder is `<testData.folder>/large/` or the one named by the environment variable
   `kit.json` calls `testData.largeEnv`); a case whose file is absent is reported as
   "not measured", not as passed.
2. Run `dotnet test <solution from kit.json> -c Release --filter "Category=Validation"`. Paste
   the output.
3. For each case, print a table: ID, fixture, expected, measured, difference, tolerance, verdict.
4. A drift outside tolerance is a finding, not a fix. Report it with the two candidate causes
   (the code regressed, or the recorded figure was wrong) and stop. The user decides which.
5. Only when the user says a new figure is right: update `validation.json`, add a dated line to
   `docs/validation.md` with the old value, the new value and the reason, and a CHANGELOG line.
