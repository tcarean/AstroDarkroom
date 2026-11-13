# 0111. A stopped Siril run ends two seconds after Siril is gone

Date: 2026-11-13
Status: accepted 2026-11-13

## Context

A stop of a `siril-cli` run (the run window's button, the Split tab's, the window closing, a
folder change mid-split) cancels a token whose callback kills the process tree
(`SirilScriptRun.Kill`), and the run then waits for the process to exit before it reports the
cancellation. The review of 2026-11-13 (CR-015) read that `Process.Kill(entireProcessTree:
true)` throws an `AggregateException` when not every process of the tree could be ended, that
`Kill` caught two other types only, and that the exception leaves `Cancel()` in the view-models,
which catch `ObjectDisposedException` alone. Whether a child ever refuses to die was left to
measure.

Measured on 2026-11-13 with a throw-away .NET 8 program that ran the same calls on a `.cmd`
whose child, a PowerShell, denies everyone the right to terminate it (as a process of another
user, or an elevated one, does):

```
[0.9s] child: READY pid=22848 convert=True set=True
[1.0s] Cancel() threw AggregateException: One or more errors occurred. (Not all processes in process tree could be terminated. (Access is denied.))
[1.0s] root (cmd) HasExited=True
[9.0s] WaitForExitAsync finished within 8 s of the kill: False
[20.9s] child: CHILD-EXITS-BY-ITSELF
[20.9s] WaitForExitAsync finished
```

Two facts. The exception does leave `Cancel()`, and the window's handler for an exception on
its thread only logs it (`App`, `LogSetup.Fatal`), so the window closes. And catching it is not
enough: the root process is dead at once, but `WaitForExitAsync` on a process with redirected
output also waits for the output to reach its end, which the surviving child holds open; the
run would have stayed "running" for the 20 s the child lived.

No real Siril run was seen to leave such a child: Siril starts helper processes of its own and
can start external tools, and the review gave the case the reach "rare". The measurement shows
what happens when one does, not how often.

## Decision

The owner's choice of 2026-11-13, between catching the exception alone and also bounding the
wait:

1. `Kill` never throws: the `AggregateException` is caught and logged at Warn, with the
   processes' own errors inside it.
2. After a kill the run waits for the exit and for the output as before, but for the output
   no longer than `SirilScriptRun.OutputGrace`, 2 seconds. When `siril-cli` itself has exited
   by then, the run is reported as stopped, the same cancellation as any stop, with a Warn
   line saying a process it started still holds its output.
3. When `siril-cli` itself is still alive after those 2 seconds (the kill did not end it), the
   wait goes on without a limit, as before: a run is never called stopped while Siril still
   works in the session folder.
4. The screen says what it says for any stop. The surviving process is named only in the log:
   the window does not know what it is, and the user who started an elevated tool does.

Two seconds is a choice, not a measured figure. An ordinary stop does not wait for it: the
output closes when the last process of the tree ends, and the wait ends there. The grace is
paid only in the case above, and has to be long enough for the readers to take the last lines
a killed Siril left in the pipe.

## Alternatives rejected

- **Catch the exception and nothing else.** The window no longer closes, but the stop button
  appears to do nothing for as long as the child lives, and the Split tab stays locked.
- **Do not wait for the output at all after a kill.** The last lines Siril printed before the
  kill may still be in the pipe; two seconds cost nothing when the tree is gone.
- **Tell the user on screen that a process survived.** A new text in two languages for a case
  that needs an elevated or foreign child; the log line is there for the report of a bug.
- **Kill without the tree** and let children be. Siril's helpers would outlive every ordinary
  stop and go on writing in `process/`.

## Consequences

- After such a stop a process Siril started may still run and write in the session folder (or
  in the split's temporary folder, whose deletion then fails as it does for any file in use).
  It did before too; now the window survives to be used.
- The view-models' `Cancel()` still catch `ObjectDisposedException` alone: nothing else leaves
  the callback now.
- R-140 holds the behaviour; `R140_AStopEndsTheRunEvenWhenAChildOfSirilCannotBeKilled` in
  `SirilRunTests.cs` starts the measurement's protected child from the fake `siril-cli`, about
  8 seconds of the gate, most of them the wait for the child's own end before the test's folder
  can be removed.
