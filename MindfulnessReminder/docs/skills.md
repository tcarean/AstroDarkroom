# Project additions to the kit's skills

The skills under `.claude/skills/` are the kit's, byte-identical in every project (the kit's
ADR 0018; ADR 0002). What Mindfulness Reminder needs on top of them is here, one section per
skill; each skill reads this file before its first step, and each section says at which step it
applies. Values (the solution, the folders, the publish list, what the auditor may also read)
are in `kit.json`, not here.

## /new-feature

- At step 1: a requirement about a reminder says which of the two it is (unlock or periodic),
  what the schedule does (restart, suspend, postpone) and what the tooltip reads; one about a
  setting names the registry value, its default and its bounds (the rule "Settings are
  strings, read forgivingly"); one about a window says what is on screen and which keys do what.
- At step 2: every test lives in `tests/MindfulnessReminder.Tests`, on the fakes the suite
  already has (`FakeRegistryStore`, and the fakes inside `ReminderCoordinatorTests`: a clock, a
  timer, a session monitor, a presenter, an activity monitor). A rule gets its test in Core;
  if it cannot be tested there, it is in the wrong project (the rule "Logic in Core, adapters
  in App"). Test names are sentences with underscores, `R###_` first.
- At step 5: `MindfulnessReminder.Core` first, then the adapter or the window. A change under
  `src/MindfulnessReminder.App/` is not done until it was looked at: `--preview` runs the
  reminder window and `--detect` the activity monitor, and nothing else of the App. Anything
  else (the tray menu, Settings, About, the presenter, the timers, the session monitor) runs
  only in a full tray start, and a full start of a worktree build repairs the auto-start entry
  to that build (R-006): do not start one; list the change for the owner to look at in their
  own copy after the merge.
- At step 6: there is no numeric code and no validation case (ADR 0005); skip, unless the
  feature measures something on a machine, in which case the first case and `ValidationTests`
  are written then.
- At step 8: `README.md` is English and is the user documentation; its "Behaviour" list, the
  section of the feature and the "Design decisions" table are where a user-visible change
  goes.

## /wrap-up

- At step 2: there is no numeric code and no golden file; nothing to do.
- At step 9: a change to the reminder window is looked at with `--preview`, one to the activity
  monitor with `--detect`, before the commit. Any other change under
  `src/MindfulnessReminder.App/` is listed in the wrap-up report for the owner to look at in
  their own copy after the merge: a full tray start of the worktree's build would repair the
  auto-start entry to it (R-006).

## /validate

- At step 1: there is no large file and no case; `docs/validation.json` has `"cases": []`
  (ADR 0005). Say so and stop after step 1; steps 2 to 5 have nothing to run. When the first
  case is added, this section is rewritten.

## /release

- At step 3: no cases; skip.
- At step 5: the application is published framework-dependent into `artifacts/X.Y.Z/` itself
  (`dotnet publish -c Release` of `src/MindfulnessReminder.App`, no runtime, ADR 0002); the tag
  is `mindfulnessreminder-vX.Y.Z`. The README's "Deployment note" says where the owner
  publishes for their own machine (`%LOCALAPPDATA%\MindfulnessReminder`); a release does not go
  there.
- At step 7: the README's "Build and run" section names the build and test commands and the
  two switches; `artifacts/` is what a release produces.

## /mutate

- At step 1: Core is small (23 source files; 222 tests on 2026-10-09, 7 of them `RepoInvariantTests`), so a whole-core run is
  reasonable; `scripts/mutate.ps1 -Mutate "**/IntervalSchedule.cs"` is the shape for one
  file. No run has been measured yet; write the first one's time and score here.

## /docs-health

(none; `audit.alsoRead` in `kit.json` lets the auditor read `README.md`, the user
documentation, which states behaviour and is not code)
