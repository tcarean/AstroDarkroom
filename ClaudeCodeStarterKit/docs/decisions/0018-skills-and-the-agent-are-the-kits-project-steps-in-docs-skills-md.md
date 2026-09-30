# 0018. The skills and the agent are the kit's too: values from kit.json, project steps in docs/skills.md

Date: 2026-09-27
Status: accepted 2026-09-27; the auditor's extra inputs superseded by 0025
Supersedes: 0009 in part (the list of inputs)

## Context

ADR 0017 made `scripts/` byte-identical in every project. The six skills under
`.claude/skills/` and `.claude/agents/docs-auditor.md` were copied to FlatCheck on 2026-09-25 and
edited there like the scripts had been. Measured on 2026-09-26 with `diff` (212 lines in the
kit), the differences were of three kinds: values that `kit.json` already holds (solution, code
folders, test filter, publish targets, test projects, the large-data variable); eleven generic
improvements made in FlatCheck only (a `/validate` step after numeric code, `Internal` in the
CHANGELOG step, a new `R###` test moves its requirement, ADRs for choices settled in chat, README
as user documentation, `git status -- .`, the owner picks the version, a fallback when the agent
type is not yet registered, a "Files opened" section and an English report from the auditor, the
exit code in a requirement, a filter on a large suite); and project prose (golden and parity
scenarios, "measure before you design", which paths are numeric code, two extra files the
auditor may read). A skill is prose the model reads, not code that runs, so "parameter" means
something else here.

## Decision

- The six skills and the agent are mirrored by `scripts/sync.ps1` like `scripts/`, checked at
  step 0 of `verify.ps1` and guarded by the Stop hook, under rule 15.
- Values come from `kit.json` by reference: a skill says "the solution `kit.json` names", the
  agent reads `kit.json` first and never opens `codeFolders` or `codeFiles`; the extra files it
  may read are the optional list `audit.alsoRead`.
- Project-specific steps go in `docs/skills.md`, one section per skill (`## /new-feature`,
  ...), which every skill reads before its first step; the kit ships it with empty sections.
- Validation tests carry the trait `Category=Validation` in every project, the kit included, so
  `/validate` runs one command everywhere; a project whose cases read large files keeps them
  out of the gate with `kit.json`'s `testFilter`. The kit's gate still runs its own.
- The eleven generic improvements are in the kit's versions now.
- `.claude/settings.json` stays per project: its allowlist grows with the commands a project
  uses (GHID), and today it differs from the kit's only in a comment.

## Alternatives rejected

- **Leave the skills per project.** The same drift as the scripts: eleven improvements the kit
  never received, and four value edits that `kit.json` already answered.
- **Project prose inside `kit.json`.** Steps are sentences, and a JSON string is where nobody
  reads or diffs them; a markdown file with one section per skill is.
- **A `validate.filter` key instead of the trait.** A second filter to keep in step with the
  first; a trait is the tool xUnit has for "these tests are a category".
- **Drop the FlatCheck specifics and keep the skills generic.** The specifics are what a fresh
  model cannot infer (where a golden scenario goes, what a mode also needs); they stay, in
  `docs/skills.md`.

## Consequences

A project edits `kit.json` and `docs/skills.md`, nothing under `.claude/skills/` or
`.claude/agents/`; step 0 of `verify.ps1` names the files otherwise. After a kit change to a
skill, every project's next commit needs `scripts/sync.ps1` first, as for a script. Step 9 of
`/wrap-up` states the rule instead of "probably belongs in the kit". `SyncTests` covers a skill
edited and an agent added in a project. A skill's text can only point at `kit.json`; the model
has to read it, which the skill asks in its first line.
