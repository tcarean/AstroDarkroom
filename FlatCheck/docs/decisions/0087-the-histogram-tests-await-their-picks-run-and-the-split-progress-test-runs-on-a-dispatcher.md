# 0087. The histogram tests await the run their pick started, and the split's progress test runs on a dispatcher

Date: 2026-11-02
Status: accepted 2026-11-02

## Context

On 2026-11-01 two window tests failed now and then while other sessions loaded the machine, and
each failure turned the gate red:

- `HistEqTabTests.SwitchingTheViewChangesThePictureWithoutRerunning`, in 2 of 9 full test runs;
- `SplitTabTests.R092_TheProgressCoversEveryPartAndReachesTheEnd`, once, in a commit hook's gate
  run.

On 2026-11-02 a scratch harness ran each test in a loop and counted its failures.

**The histogram test failed 16 times in 150, with no load,** always at
`Assert.True(vm.Pending.IsCompleted)`.

- Setting `Selected` starts a run and keeps it in `Pending`.
- The test then started a second run with `RecomputeAsync()`, which supersedes the first, and
  awaited only the second.
- A superseded run stops at its token. If its computation had not started yet, `Task.Run`
  never starts it. If it had, the run checks the token only after `HistEqRun.Execute` returns,
  so it was often still running when the test asserted.
- Five other tests of the class started the same second run, seven times in all, and so did
  one test of `MainViewModelTests`. None looked at `Pending`, so none failed, but in
  `HistEqTabTests` each could leave a superseded run computing after it ended.
- The application is not at fault: in the window only the pick starts a run.

**The split test did not fail again:** 0 failures in 80 runs without load, and 0 in 60 with the
thread pool kept full. With every core kept busy it ran too slowly to say anything (3 runs in
275 s). Its message was never captured: the gate's quiet output shows only `[FAIL]`.

The likely cause is how its progress reports travel in a test. Without a dispatcher, the copy
step's `Progress<double>` posts its reports to the thread pool, in no fixed order, so a late one
could overwrite the final `Progress = 1`. In the window the dispatcher keeps them in order, ahead
of the run's end.

The owner chose on 2026-11-02 to change all six histogram tests, eight waits in all, and the
split test. The review of the same day added the one wait in `MainViewModelTests`, so that no
test starts a second run after a pick.

## Decision

- **The histogram tests.** Every test that picks a histogram file awaits `Pending`, the run the
  pick started: the six of `HistEqTabTests` and one of `MainViewModelTests`. None calls
  `RecomputeAsync()` after a pick.
- **The split test.** `R092_TheProgressCoversEveryPartAndReachesTheEnd` moves into a class of
  its own, `SplitTabProgressTests`, in the `window` collection. It runs `StartAsync` on that
  collection's dispatcher (`StaFixture.InvokeAsync`), as the window runs it, with the fixtures
  of `SplitTabTests`, now internal.

## Alternatives rejected

- **The failing histogram test alone.** The other five would keep starting a second run that
  can go on computing after them, loading the tests that follow.
- **The whole of `SplitTabTests` in the `window` collection.** Its fifteen other tests would
  then run beside the collection's Application, and the Siril runner would send their lines to
  the collection's thread through a blocking `Dispatcher.Invoke`. A stuck thread would then
  hang those tests instead of failing them (the review of 2026-11-02).
- **Monotonic progress in the view-model.** The window already keeps the reports in order.
  Changing the application for an ordering only the test lacks would stop the test from
  checking the window's own behaviour.
- **Waiting in the test until the progress reads 1.** A late report can land after the read,
  or keep the value below 1 for good, and polling cannot tell which happened.

## Consequences

- **Measured with the change:**
  - the histogram test passed 300 of 300 runs: 150 without load, 150 with the pool kept full;
  - the split test passed 60 of 60 with the pool kept full, and 2 of 2 with every core busy;
  - the gate passed with both changes.
- **The split test's cause is only the likely one,** since its failure did not come back in 140
  runs. Another possible cause is not addressed: a file held briefly by the antivirus during
  heavy disk use. If the test fails again, its message is needed, and the gate's quiet output
  hides it.
- **Only the progress test runs in the `window` collection.** The other split tests run
  without the collection's Application, as before, with the Siril runner's lines handled inline.
  The test assembly runs its collections one at a time (`DisableTestParallelization`).
