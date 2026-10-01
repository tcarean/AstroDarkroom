# 0006. Rules live in CLAUDE.md, explanations live once in the kit's guide

Date: 2026-09-25
Status: accepted 2026-09-25; the rules in each project's CLAUDE.md superseded by 0028

## Context

The kit had three documents about process: `CLAUDE.md` (rules, loaded every session),
`docs/ai-workflow.md` (the same rules in long form, copied into every project) and `GHID.md`
(the guide with the reasoning, kit only). The assistant never read `ai-workflow.md` in the
working loop because `CLAUDE.md` was always in context; humans read the guide. Two documents
had to be kept in step by hand, and the owner asked whether that was worth it.

## Decision

Two documents, two roles. `CLAUDE.md` in each project holds the rules, short, for the model.
`GHID.md` holds the explanations (why, alternatives, adoption) and exists once, in the kit
folder inside the `photo` repository. Each project points to it from `CLAUDE.md` and its README.
`docs/ai-workflow.md` is deleted.

## Alternatives rejected

- **Keep `ai-workflow.md` as a tool-independent rulebook.** Its only reader would have been
  another AI tool without CLAUDE.md support, which would read the README anyway.
- **Copy `GHID.md` into every project.** One copy per project goes stale at every change in the
  kit, and the sections on starting and migrating a project do not belong in a project.
- **Merge the guide into CLAUDE.md.** Would push CLAUDE.md far past its size target and load
  the reasoning into every session that does not need it.

## Consequences

A project that leaves the `photo` repository takes a copy of the guide with it at that moment.
`/docs-health` no longer lists `ai-workflow.md` among the inputs. A rule added to `CLAUDE.md`
that needs a "why" gets it in the guide, in the same task.
