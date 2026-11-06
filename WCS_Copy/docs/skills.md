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
- A new key in `WcsMetadataKeys` changes V-001, and a new key of `WcsKeys` V-002 too, since the
  keys a copy drops are read from the same list: re-measure them with the console's
  `--dry-run` on the fixtures (V-002's pair needs `--force` too, being a mirror, R-017) and
  record the change in `docs/validation.md` before `validation.json`.
- A new text of the window is a `{l:T En='...', Ro='...'}` pair in XAML or a `T(...)` pair in
  the view-model, never a sentence about the copy (ADR 0007); its Romanian word joins the
  list in `tests/CopyWcs.Wpf.Tests/WindowSmokeTests.cs`, so `R036_` keeps the English pass
  clean. A behaviour of the window gets its row in the block R-030 to R-039 and its test in
  `MainViewModelTests` (against the console) or `WindowSmokeTests` (the rendering).
- A new log line is English, composed from `LogText`, never through `T(...)`
  (`NoLogLineGoesThroughTheTranslation`); a new seam's lines join the table in
  `docs/design/logging.md`, a line of the copy run joins R-042 and its test in `LoggingTests`.
  A new package is a component in `ThirdParty.cs` and a section in `THIRD-PARTY-NOTICES.md`
  (`ThirdPartyTests`); a package FlatCheck also ships takes FlatCheck's version (ADR 0009).

## /wrap-up

(none)

## /validate

- The two cases read fixtures in the repo, `tests/testdata/fixtures/veil-2026-10-06-siril-1.4.4-solve.fit`
  and, for V-002, `veil-2026-10-06-syqon-cli-1.1.8-denoised.fit` as the destination; nothing is
  outside git and no environment variable is needed. The measurement is the console's dry run:
  for V-001 on the first fixture against a header-only destination of 5601 x 3692, for V-002 on
  the first fixture against the second, once without `--force` (refused, exit 1, R-017) and once
  with it (the 72 cards and the drop line) (`docs/validation.md`, "How to re-measure").

## /release

- Two publish targets: the release folder's `console/` holds `copywcs.exe` and `wpf/` holds
  `CopyWcs.Wpf.exe` with `LICENSE` beside it, both self-contained, single-file, win-x64.

## /mutate

- Numeric code worth mutating: `src/CopyWcs.Core/FitsHeader.cs` (the block arithmetic, the
  card padding) and `src/CopyWcs.Core/WcsMetadataKeys.cs` (the SIP pattern). No
  `stryker-config.json` is in the repo yet; `scripts/mutate.ps1` needs one in
  `tests/CopyWcs.Tests/` and the `dotnet-stryker` tool restored, both to add the first time
  (the kit's `.config/dotnet-tools.json` and `tests/Starter.Tests/stryker-config.json` are
  the models).

## /docs-health

(none)
