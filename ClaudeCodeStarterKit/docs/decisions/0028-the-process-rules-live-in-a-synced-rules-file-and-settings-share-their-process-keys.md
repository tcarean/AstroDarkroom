# 0028. The process rules live in a synced rules file; settings.json shares its process keys

Date: 2026-10-01
Status: accepted 2026-10-01
Supersedes: 0006 in part (the rules in each project's CLAUDE.md); 0009 in part (the list of inputs); 0017 in part (the shared folders and the rule's place in CLAUDE.md); 0018 in part (settings.json per project)

## Context

The rules every project follows (the gate, commits and merges, branches and worktrees, the shared
folders, decisions, the CHANGELOG, languages, requirements and tests) were written into each
project's `CLAUDE.md` by hand, and the copies drifted within a week. The kit's rule 15 was the
first adopter's rule 19. That adopter's commit rule cited its own mirror ADR where the kit cited
0021, its CHANGELOG rule said "refactor or process change" where the kit said "refactor", and its
decisions rule carried a sentence the kit's lacked. Both `.claude/settings.json` files cited
"rule 13" in their comments. Their hooks and their `ask`, `deny` and git `allow` lists were
identical only because nobody had changed one yet. ADR 0025 had already stopped synced files from
citing rule numbers, because each project numbers its rules its own way. On 2026-09-30 the owner
chose, in chat, variant C of the programme's decision Q2: a kit-owned rules file synced into every
project, `CLAUDE.md` keeping only project content, the shared-folder list defined once, the process
keys of `settings.json` checked at step 0 with an empty `attribution`, and no `CLAUDE.md` or
settings at the repository root. On 2026-10-01 he added that the method rules (requirements first,
a failing test before a fix, no test edited to pass, packages, measured numbers, two readings) are
universal too.

Claude Code loads `.claude/rules/**/*.md` as project instructions. This was measured on 2026-10-01
from the session transcripts, which record what was loaded and from which path; `/context` in the
desktop app shows only a total for these files. A session started in a project folder whose
`.claude/rules/kit/process.md` existed loaded at start, as `Project` instructions, that folder's
`CLAUDE.md` (12123 characters) and `.claude/rules/kit/process.md` (6253 characters). A session
started in the main checkout and working in a hand-made worktree loaded the worktree's `CLAUDE.md`
and rules file as `nested_memory` when it first read a file there. The plan of this task records
the three readings.

## Decision

- `.claude/rules/kit/process.md` holds the process rules every project follows. It is the kit's,
  the same bytes in every project, and `scripts/sync.ps1` mirrors `.claude/rules/kit/` like
  `scripts/`, `.claude/skills/` and `.claude/agents/`. A project may keep rules files of its own
  in `.claude/rules/`, outside `kit/`.
- A project's `CLAUDE.md` holds its description, commands, layout, design-note pointers, style,
  its own rules and its deviations. A project rule that extends or varies a kit rule names it.
- A rule has a name and is cited by it in quotes, never by a number: the kit's rule "Gate", the
  rule "Ratios". Accepted ADRs, plans and CHANGELOG entries keep the numbers of their date; the
  plan of this task maps the old numbers to the names.
- The shared-folder list is written once, `Get-KitSharedFolders` in `scripts/kit-config.ps1`,
  and `sync.ps1` and the Stop hook take it from there.
- Some keys of `.claude/settings.json` are the kit's: `hooks`, `permissions.ask`,
  `permissions.deny`, the `permissions.allow` entries for git commands (`Bash(git ...)`,
  `Bash(git:*)` and the same for `PowerShell`), `attribution`, which is
  `{"commit": "", "pr": ""}` so Claude Code adds no attribution line the commit rule forbids,
  and the two keys that switch the hooks or the prompts off, `disableAllHooks` and
  `permissions.defaultMode`, which the kit leaves unset and so a project may not set.
  `sync.ps1 -Check`, step 0 of `verify.ps1`, compares them parsed: the lists as sets, the
  objects with their properties in any order. `sync.ps1` rewrites only those keys and keeps the
  values of the rest; a settings file that is not valid JSON is named and left for a fix by
  hand. The other `allow` entries and `$comment` are the project's.
- The `docs-auditor` reads `.claude/rules/` beside `CLAUDE.md`. Its 200-line limit counts the
  project's own rules, `CLAUDE.md` and any file of its own under `.claude/rules/` outside
  `kit/`; the kit's rules file is the same everywhere and does not count, or every adopter would
  pass the limit only by cutting its own rules to make room for the kit's.

## Alternatives rejected

- **A `CLAUDE.md` and settings at the repository root.** Claude Code would load them in every
  folder of `photo`, including the projects that have not adopted the kit, and a project that
  leaves the repository would lose them. The owner declined it (variant C only).
- **An `@import` of the kit's file from each `CLAUDE.md`.** A path out of the project breaks in a
  standalone repository, and the gate cannot see whether the imported text changed.
- **Keep the copies in each `CLAUDE.md`.** That is the drift described above.
- **Compare the whole `settings.json`.** A project's `allow` list grows with the commands it uses,
  by design.
- **Keep the rule numbers.** The numbers already differed between the kit and its first adopter,
  and every rule moved would renumber the rest.
- **Let the Stop hook guard `settings.json` too.** A project edits its own `allow` entries in that
  file; step 0 catches a changed process key at the next commit.

## Consequences

The kit's `CLAUDE.md` keeps two rules of its own, "Core has no UI" and "No code structure in docs".
A project's next commit after a kit change to the rules file or to a process key needs
`scripts/sync.ps1` first, as for a script. `SyncTests` covers the settings keys, including a copy
that comes back byte for byte. `HookTests` covers an edit under `.claude/rules/kit/` refused in a
project and one beside it accepted. The citation test covers `.claude/rules/kit/` through the
shared-folder list. A session started at a checkout root still loads no project's rules
(ADR 0027).
