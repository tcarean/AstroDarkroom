# Project additions to the kit's skills

The skills under `.claude/skills/` are the kit's, byte-identical in every project (ADR 0018).
What a project needs on top of them goes here, one section per skill, and each skill reads
this file before its first step. A section says at which step of the skill its additions
apply; an empty section, or a missing one, adds nothing. Keep it to what a fresh model could
not infer from `CLAUDE.md` and `docs/`: where the tests of each kind go, what a new mode or
scenario also needs, which paths count as numeric code for `/validate`.

Values, not prose, go in `kit.json` (the solution, the folders, the publish list, the files the
`docs-auditor` may also read); this file is for steps. English, like the skills.

## /new-feature

(none)

## /wrap-up

(none)

## /validate

(none)

## /release

(none)

## /mutate

(none)

## /docs-health

(none)
