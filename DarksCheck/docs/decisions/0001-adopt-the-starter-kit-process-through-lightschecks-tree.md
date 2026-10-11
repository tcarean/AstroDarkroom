# 0001. Adopt the ClaudeCodeStarterKit process, starting from LightsCheck's tree; the kit's own tests stay in the kit

Date: 2026-10-11
Status: accepted 2026-10-11

## Context

DarksCheck starts on 2026-10-11 from a hand analysis of the darks of the Smida session of
2026-08-12 (Python scripts, read-only), after which the owner asked for a C# WPF application
on the kit, "like FlatCheck" for the window, the licence, the versioning, the dark theme and
the "Despre" window, with a plan first (`docs/plans/2026-10-11-build-darkscheck.md`, approved
in chat the same day). LightsCheck, built on 2026-10-09 with exactly those constraints, already
holds the kit's layout, FlatCheck's readers with the shot metadata added, the console skeleton
(banner, options, session folder, cadence, inventory report) and the repository tests; its ADR
0001 chose the kit's `src/` and `tests/` layout and .NET 9 over FlatCheck's.

## Decision

DarksCheck is the kit's process in LightsCheck's shape: the tree of LightsCheck's `main`
(6cc0078, 2026-10-11) extracted, pruned to the readers, the session and inventory code, the
console and the repository tests, renamed (`LightsCheck` to `DarksCheck`, `lightscheck` to
`darkscheck`, `LIGHTSCHECK` to `DARKSCHECK`), and then adapted to darks. The kit's shared
folders (`scripts/`, `.claude/skills/`, `.claude/agents/`, `.claude/rules/kit/`) and the
process keys of `.claude/settings.json` are the kit's current bytes (`scripts/sync.ps1`
reported no difference on 2026-10-11), `kit.json` points at `../ClaudeCodeStarterKit`, and
`Directory.Build.props` keeps the kit's strictness (nullable, warnings as errors,
`AnalysisLevel` latest-recommended, .NET 9). LightsCheck's analysis, window, icon, prototype,
validation data, plans and ADRs are not carried; its decisions that hold here are taken again
as DarksCheck's own ADRs, since a project cites only its own and the kit's. The kit's tests of
its own scripts, hooks and guide are not carried either, for the reasons LightsCheck's ADR
0001 gives: those files are the kit's and are tested there.

## Alternatives rejected

- **Copying the kit and re-copying the pieces from FlatCheck.** LightsCheck already did that
  work (its ADR 0003) and added the shot metadata the inventory needs; doing it again would
  reproduce two days of adaptation for the same result.
- **A shared library with LightsCheck.** The owner chose independent copies over a shared
  library on 2026-10-09 (LightsCheck's ADR 0003); nothing changed since.
- **Keeping LightsCheck's requirements and ADRs as history.** They describe lights; a reader
  of DarksCheck's `docs/` would have to know which rows still applied.

## Consequences

`scripts/verify.ps1` is the gate; `RepoInvariantTests` ties `docs/requirements.md` to the
`R###_` test names. The kit's own ADRs are cited as "the kit's ADR NNNN" and not repeated
(the kit's ADR 0025). The session that made this commit ran in the empty project folder, so
the kit's hooks were not loaded: the gate was run by hand before the commit, and the next
sessions open in `E:\Photografie\Code\DarksCheck` after the merge, where they load.
