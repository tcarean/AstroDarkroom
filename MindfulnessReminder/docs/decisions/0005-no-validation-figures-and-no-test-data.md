# 0005. No validation figures: docs/validation.json lists no case, there is no ValidationTests and no test data

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

In the kit, `docs/validation.json` holds acceptance figures measured on real data and
`ValidationTests` (trait `Category=Validation`) reads them; FlatCheck, AstroPhotoAnalyzer and
ImageVisualCompare each measure on images kept outside git, listed with their hashes in a test
data manifest. Mindfulness Reminder has no numeric output and no input data: it shows a window,
keeps a deadline on the wall clock and reads two registry keys. Every one of its 215 tests of the code
runs on fakes (`FakeRegistryStore`, and the clock, timer, session monitor, presenter and activity
monitor inside `ReminderCoordinatorTests`); the seven `RepoInvariantTests` the migration added
read the repository's own documents, not test data. The numbers its code carries are constants, which
the unit tests use by name, not by value: `TrayTooltip.MaxLength` (63), `ReminderSettings.MaxMessageLength` (50),
the interval bounds 1 to 480 minutes and the retry bounds 5 to 300 seconds,
`ReminderCoordinator.DefaultDebounce` (2 s), `DefaultTickInterval` (1 s),
`DefaultActivityRefresh` (10 s), `WpfReminderPresenter.DefaultShowDelay` (600 ms). The kit's
skills `/validate`, `/release` (its step 3) and `/wrap-up` (its step 2) look for
`docs/validation.json`, and `scripts/check-testdata.ps1` reads a manifest.

## Decision

- `docs/validation.json` exists with `"cases": []`, so the kit's skills find it and do nothing;
  `docs/validation.md` says why it is empty and what would become a case.
- There is no `ValidationTests` class and no `TestData` helper; `kit.json` has no `testFilter`.
- `tests/testdata/manifest.json` lists no file and `tests/testdata/README.md` says why; there is
  no `fixtures/` and no `large/` folder.
- `RepoInvariantTests` reads `docs/validation.json` and checks that it parses and that case IDs
  are unique, so the shape is guarded when the first case arrives.

## Alternatives rejected

- **A `ValidationTests` class with no cases.** A theory with no data, which xUnit reports as a
  test that runs nothing.
- **A case per constant.** It would measure a value the code states outright, with a tolerance
  where the code is exact. A changed value is caught by the kit's rule "Requirements first",
  which changes its row before the code, not by a test.
- **A case on the `--detect 60` timeline.** It needs a person to start a game and a call; no
  test host can produce the state it measures.

## Consequences

- `/validate` stops after its first step, `/release` skips its figures step, and `/wrap-up`
  step 2 has nothing to run; `docs/skills.md` says so in each skill's section.
- The first figure measured on a machine (for example the time `WindowsActivityMonitor.Read`
  takes to walk the capture records, which is the reason for the 10 s refresh) brings
  `ValidationTests`, a case with a tolerance, and `testFilter` `Category!=Validation` with it.
