# Project additions to the kit's skills

The skills under `.claude/skills/` are the kit's, byte-identical in every project (the kit's
ADR 0018). What CopyWcs needs on top of them goes here, one section per skill, and each skill
reads this file before its first step. A section says at which step of the skill its additions
apply; an empty section, or a missing one, adds nothing. Keep it to what a fresh model could
not infer from `CLAUDE.md` and `docs/`.

Values, not prose, go in `kit.json` (the solution, the folders, the publish list, the files the
`docs-auditor` may also read); this file is for steps. English, like the skills.

## /new-feature

- A new user-visible text is a `T("english", "romana")` pair where it is composed, in the core
  when both applications may show it (ADR 0004). A new Romanian word that the English output
  must not contain goes into `RomanianWords` in `tests/CopyWcs.Tests/LanguageTests.cs`, and a
  new console scenario into `Scenarios()` there, so `R023_` covers it in both languages.
- A behaviour of the copy itself (what is read, selected or written) gets its row in the
  block R-010 to R-019 of `docs/requirements.md` and its test in `WcsCopyTests` (the outcome)
  and `CopyWcsCliTests` (the printed text); a console option, in the block R-001 to R-009 and
  `OptionsTests`.
- A new key in `WcsMetadataKeys` changes V-001: re-measure it with the console's `--dry-run`
  on the fixture and record the change in `docs/validation.md` before `validation.json`.

## /wrap-up

(none)

## /validate

- The one case reads `tests/testdata/fixtures/veil-2026-10-06-siril-1.4.4-solve.fit`, in the
  repo; nothing is outside git and no environment variable is needed. The measurement is the
  console's dry run on the fixture against a header-only destination of 5601 x 3692
  (`docs/validation.md`, "How to re-measure").

## /release

- The console is the one publish target until the window exists; the release folder's
  `console/` holds `copywcs.exe` self-contained, single-file, win-x64.

## /mutate

- Numeric code worth mutating: `src/CopyWcs.Core/FitsHeader.cs` (the block arithmetic, the
  card padding) and `src/CopyWcs.Core/WcsMetadataKeys.cs` (the SIP pattern). No
  `stryker-config.json` is in the repo yet; `scripts/mutate.ps1` needs one in
  `tests/CopyWcs.Tests/` and the `dotnet-stryker` tool restored, both to add the first time
  (the kit's `.config/dotnet-tools.json` and `tests/Starter.Tests/stryker-config.json` are
  the models).

## /docs-health

(none)
