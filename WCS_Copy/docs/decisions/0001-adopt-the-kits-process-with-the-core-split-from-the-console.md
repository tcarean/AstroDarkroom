# 0001. Adopt the kit's process, with the core split from the console and the layout `src/` and `tests/`

Date: 2026-10-26
Status: accepted 2026-10-26

## Context

CopyWcs was written in August 2026 as one console project with its tests beside it, a
Python/astropy prototype at the root and a `CLAUDE.md` of conventions; no requirements with
IDs, no decisions, no gate. On 2026-10-26 the owner asked for a WPF window over the same
functionality, dark, with an About window, built on the kit, with English and Romanian chosen
by `--lang` as FlatCheck does, and approved the plan
`docs/plans/2026-10-26-adopt-the-kit-and-build-the-window.md` with its recommended answers
(Q1 to Q10). The window needs a library without a console under it, and the kit's scripts
need `kit.json` to say where the code and the tests are. Measured before the change: 87 tests
green, 16 `.cs` files, every comment Romanian.

## Decision

- The project follows the kit's process (`ClaudeCodeStarterKit`, `GHID.md` "Migrarea unui
  proiect existent"), in one task: the gate `scripts/verify.ps1`, the kit's skills, agent and
  rules byte for byte (the kit's ADRs 0017, 0018 and 0028), `kit.json` with this project's
  values, `CHANGELOG.md`, `docs/` in the kit's shape. The kit's decisions are cited as "the
  kit's ADR NNNN" and not written again (the kit's ADR 0025).
- The layout is the kit's default: `src/CopyWcs.Core` (the FITS header engine, the key lists,
  the copy run and its texts, the version and the copyright read from the assembly; no
  Console, no UI, a compile boundary, the kit's ADR 0002), `src/CopyWcs.Cli` (`copywcs.exe`,
  kept: options, dispatch, exit codes, printing; `Main` a one-line shim to
  `CopyWcsCli.Run`), later `src/CopyWcs.Wpf` (`CopyWcs.Wpf.exe`, the siblings' naming) and
  `tests/CopyWcs.Tests`, later `tests/CopyWcs.Wpf.Tests`. The files moved with `git mv`.
- The copy run moved into the core as `WcsCopy.Run(WcsCopyRequest)`, which returns a
  `WcsCopyOutcome` of lines already worded (the error, the warnings, the cards, the notes);
  the console prints them where it printed them before and the window will show them. The
  console's output is unchanged, which the 87 tests prove.
- The Python prototype is frozen under `prototype/copy_wcs.py`, prior art with its Romanian
  docstrings, not maintained, not run by the gate; `LicenseHeaderTests` still checks its
  licence notice.
- .NET 8 stays (the Astro Darkroom suite's rule "Same .NET across the suite", should the tool
  join it); `global.json` pins SDK 9.0.318 like the siblings, which builds net8.0.

## Alternatives rejected

- **The root layout, as FlatCheck's.** Possible by listing the folders in `kit.json`, but the
  project is 16 files and `git mv` keeps the history; the default costs nothing here.
- **The window project in the console's folder.** The window must not reference `Console` or
  the console's `Options`; a separate core is the only compile-time guarantee.
- **The run left in the console, the window calling `CopyWcsCli.Run` with string writers.**
  The window would parse printed text back into warnings and cards; a typed outcome is what
  both need.

## Consequences

- The gate is live from the first commit: the repository's pre-commit hook finds
  `scripts/verify.ps1` and runs it on every commit that touches this folder.
- `kit.json` names the console as the one publish target until the window exists; task 2 adds
  the window's entry.
- `RepoInvariantTests` binds `docs/requirements.md` to the tests; `LicenseHeaderTests` walks
  `src/`, `tests/` and `prototype/`.
