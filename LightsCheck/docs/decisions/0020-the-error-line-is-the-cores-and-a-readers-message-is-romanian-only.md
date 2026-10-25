# 0020. The error line is the core's, and a reader's message is Romanian only

Date: 2026-10-25
Status: accepted 2026-10-25
Supersedes: 0019 in part (libraw's words kept in brackets in the reader's message)

## Context

The review of the defects task (`docs/plans/2026-10-25-fix-the-defects-the-backfill-found.md`)
left three questions. The `EROARE: ` prefix was written in three places, twice in `Program.cs`
and once in `WindowTexts.Error`, while `VerdictPalette` matched the word by `StartsWith`. ADR
0019's message for a raw libraw refuses kept libraw's English words in brackets, `<file>:
libraw nu poate citi fisierul (Failed opening file)`, against the rule "Languages" (Romanian in
everything the user sees), with `Missing` as the precedent. And
`RawReaderTests.AFileThatIsNotActuallyARawIsRefused` (`Assert.ThrowsAny<Exception>`) had become
a weaker copy of the R-007 test that pins the type and the file name on the same junk `.NEF`.
The owner answered on 2026-10-25: fix all three now; the FITS half of ADR 0019 waits for
FlatCheck.

## Decision

`RunReport.Error(message)` is the one error line, `EROARE: ` plus the message, and both the
console and the window call it; no application writes the prefix itself. The reader's message
for a file libraw refuses is Romanian and names the file, `<file>: libraw nu poate citi
fisierul`; libraw's own exception and words stay as the inner exception, for a developer, not
on the user's line. The older `ThrowsAny` test is deleted, the R-007 test being its stronger
form.

## Alternatives rejected

- **Keeping libraw's words in brackets, as `Missing` does.** The rule "Languages" allows no
  English on a line the user reads; what libraw knew ("Unsupported file format", "Corrupted
  data") adds nothing the user can act on beyond "libraw cannot read it", and a developer finds
  it in the inner exception.
- **A prefix per application, with the window's in `WindowTexts`.** Two homes for one word
  the two outputs must share; the core already owns every other word of a report.

## Consequences

`RawReaderTests` pins the exact message on `Load` and `LoadInfo` and that the inner exception
names the file. `AcceptanceTests` and the window's tests keep asserting the `EROARE: ` line
through `RunReport.Error`'s text. `Missing` still quotes the loader's message in brackets: it
names a DLL and a publish option, and changing it is a question of its own.
