# 0115. A settings value that is no text means no profile

Date: 2026-11-20
Status: accepted 2026-11-20

## Context

The window reads its settings file once, while it starts, and R-156 says a file that cannot be
used means no profile, "never an exception". `WindowSettings.Read` caught the exceptions of a
file that cannot be opened and of text that is not JSON. The code review of 2026-11-13 (CR-013)
found one shape that passes both: the JSON parser accepts a string whose `\ud800` escape has no
second half, and reading that string as text throws `InvalidOperationException` ("Cannot read
incomplete UTF-16 JSON text as string with missing low surrogate"). Thrown inside the window's
start, before the window is built, it stopped every start until the file was deleted by hand.

## Decision

`Read` also takes `InvalidOperationException` for "no setting", with the same Warn line in the
diagnostic log: the window starts as if the file were absent, and the next change of profile
writes a good file over it.

## Alternatives rejected

- **Catch every exception.** A fault of the tool's own in these few lines would then pass as a
  missing setting; the four named are what a file's content or the file system can cause.
- **Check the escapes before reading the value.** A second parser for what the first one
  already tells.

## Consequences

- No file content stops the window from starting.
- Guard: `R156_ASettingsFileWithABrokenEscapeMeansNoProfile`.
