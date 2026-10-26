# 0069. The README is English and holds the user guide only

Date: 2026-10-26
Status: accepted 2026-10-26
Supersedes: 0006 in part (the README in Romanian)

## Context

ADR 0006 put `README.md` with the tool's output in the Romanian zone, because the output was the
product and its reader the owner, and `CLAUDE.md` kept the line "`README.md` stays Romanian" when
ADR 0062 made the interface speak English by default. By 2026-10-26 the README was 1563 lines of
Romanian that mixed the user guide with the development process (the gate, the kit, Claude Code),
the parity goldens with the Python cross-check commands, the list of differences from the frozen
prototype, the history of a fixed ring artefact and notes for maintainers. It was also stale in
places: one dependency where there are two packages, four projects where there are five, and a
first paragraph that still asked the question of the removed `--ratio` mode. The repository's front
page and AstroDarkroom's README are English. The owner asked for the README in English, up to date
and relevant, chose to keep it a user guide only, and to start once the lens-identification branch
(ADR 0068) was merged. The 48 validation cases were re-measured on that merge, all passing, so the
figures the README quotes stand.

## Decision

`README.md` is English, in one file with no Romanian copy, in the British spelling of the code's
English texts, and it holds what a user needs: what FlatCheck is, how to get it, the six modes, the
`--light` validity limit, the residual scale, the thresholds, the exit codes, the options, the map,
the window, a short "Development" section that points at `CLAUDE.md` and `docs/`, and the licence.
The process, the parity goldens, the divergences from the Python prototype and the history of fixed
defects stay in `CLAUDE.md`, `docs/design/` and the decision records. The README's tables that
mirror the window's guides carry the window's own English wording. It has no anchor links: sections
are named in quotes, because Bitbucket and other renderers build different anchors.

## Alternatives rejected

- **Keep the Romanian README and add an English one.** Two files of about a thousand lines, kept in
  step by hand, would drift; a Romanian reader already has the interface with `--lang ro`.
- **Translate everything as it was.** The process and the history are not the user's, and their own
  files keep them better than the README did.
- **Drop the tables that mirror the window's guides.** The window explains itself, but a reader on
  Bitbucket, or of the map the console writes, has no window; the owner kept them.

## Consequences

- `CLAUDE.md` (Layout and the rule "Languages"), `docs/requirements.md`, `docs/skills.md` and the
  design notes that name the README say it is English and name its English sections ("Options",
  "Development", "Third-party components"), and so do the comments of `SessionMessages.cs` that
  point at the tables the window's guides mirror.
- A user-visible change edits the README in English; the Romanian texts live in the interface only.
- The front page of the repository belongs to AstroDarkroom; what it says of each tool's guide is
  changed there, not here.
