# 0019. A reader translates a foreign exception where it happens; the console's filter stays narrow

Date: 2026-10-25
Status: accepted 2026-10-25; the message's bracketed libraw words superseded by 0020

## Context

`Program.Run` prints `EROARE: <message>` and exits 1 for five exception types (IO,
InvalidData, InvalidOperation, Argument, UnauthorizedAccess) and lets any other crash, as a
bug. The documentation backfill (its plan of 2026-10-25, question 7) found two paths that
reach the console with a type outside the five: libraw's own `Sdcb.LibRaw.LibRawException`
out of `RawReader.Load` on a file libraw cannot open (measured 2026-10-25 on a `.NEF` of 4096
zero bytes: `Failed opening file: ...minte.NEF`), and `IndexOutOfRangeException` out of
`FitsReader.LoadFull` on a FITS whose data is shorter than its header says. Only `--frame`
is hit: the inventory and the series catch per frame. The owner chose to translate the
failure where it happens rather than widen the console's filter.

## Decision

`RawReader` catches `LibRawException` at the two libraw calls (`OpenFile`, `Unpack`) and
throws `InvalidDataException` naming the file, libraw's message in brackets and its exception
as the inner one: the type a corrupt FITS already gives (R-008), so the console, `FrameInfo`
and the window handle a raw libraw refuses as one more unreadable frame. The catch sits in
the methods that already reference Sdcb.LibRaw types, not in `Load` or `LoadInfo`, so the
lazy-loading guards of R-007 still see a missing assembly as a load failure. The console's
filter does not change. The FITS gap is FlatCheck's to fix first (`FitsReader.cs` is a copy,
the rule "Copied files stay copies"; FlatCheck's `LoadFull` has the same pixel loop with no
check of the data length), then the copy is refreshed; until then a truncated FITS still
crashes `--frame`.

## Alternatives rejected

(Restated 2026-10-25: "until then a truncated FITS still crashes `--frame`" in the Decision
above no longer holds. FlatCheck fixed it, its R-009, b9b20a6 and 469da9b; the copy took the
hunk, ADR 0021, and a truncated FITS now ends `--frame` with `EROARE: FITS: date trunchiate:
...` and exit 1.)

- **Catching every exception in the console.** A bug would then print as `EROARE:` and exit
  1 like a bad file, and nothing would tell the two apart.
- **Catching libraw's type in the console.** The console would reference Sdcb.LibRaw for an
  exception type alone, and the inventory, the series and the window would each need the same.
- **Catching `IndexOutOfRangeException` in `ImageLoader` for the FITS half.** That hides a
  bug-class exception at the seam instead of fixing the reader, and the fix belongs to the
  file's owner, FlatCheck.

## Consequences

`RawReaderTests` pins the type and the file name on a junk `.NEF`; `AcceptanceTests` pins the
`EROARE` line and exit 1 of `--frame` on it. A raw libraw refuses in a series or inventory run
reads `eroare: <path>: libraw nu poate citi fisierul (Failed opening file)` where it read
libraw's own `eroare: Failed opening file: <path>`. The truncated-FITS case is
an open gap until FlatCheck's fix lands; the plan of this task holds the prompt for it.
