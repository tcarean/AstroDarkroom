# The diagnostic log

> Added 2026-10-30 with ADR 0043. **Covers:** `src/ImageVisualCompare.Wpf/Program.cs`,
> `src/ImageVisualCompare.Wpf/Logging/LogSetup.cs`,
> `src/ImageVisualCompare.Wpf/Logging/LogText.cs`,
> `src/ImageVisualCompare.Wpf/Logging/RunClock.cs`,
> `src/ImageVisualCompare.Wpf/Logging/Loggers.cs`,
> `tests/ImageVisualCompare.Tests/LogCapture.cs`,
> `tests/ImageVisualCompare.Tests/LoggingTests.cs`,
> `tests/ImageVisualCompare.Tests/WindowLoggingTests.cs`, and the log lines of the files the
> table below lists. A change to the code this describes means this note is updated in the same
> task, or deleted with the decision it explains. The decisions, the reasons and the figures
> measured are ADR 0043's, the behaviour R-120 to R-123's; this note is the mechanics. The port
> follows FlatCheck's `LogSetup`, whose measurements of NLog it relies on (FlatCheck's ADRs 0070
> and 0072).

## How the parts fit

`Program.Main`, the project's `StartupObject`, calls `LogSetup.Apply` first, in a `try` that
swallows anything it throws; then it builds the `App`, hooks its `DispatcherUnhandledException`,
and runs it as WPF's generated `Main` would (`InitializeComponent`, `Run`); in a `finally` it calls
`LogSetup.Close`, guarded the same way, with the exit code `Run` returned, or with none when `Run`
never returned, which `ExitLine` writes as `ended by an unhandled exception`. `App` never touches
the log's configuration. `Apply` runs once per process:

1. It sets an empty configuration first, before any logger exists, so that NLog never goes looking
   for an `NLog.config` beside the executable, even when what follows throws.
2. `Plan` decides the place without configuring anything: the level from `IMAGEVISUALCOMPARE_LOG`
   (`LevelFrom`), the executable's name (`ExecutableName`: the process path's file name, the entry
   name when the path is the `dotnet` host), and, unless the level is `Off`, the folder
   (`ResolveFolder`). `ResolveFolder` creates `log` beside the executable, a probe file in it,
   `<name>.probe-<pid>-<guid>.tmp`, opened with `DeleteOnClose`, and opens for append the file NLog
   will write next when there is one (`NewestLog`: the highest-numbered `<name>_NN.log`, else
   `<name>.log`), with the share mode that lets another process of the application hold it open;
   when any of these fails it does the same under `%LOCALAPPDATA%\ImageVisualCompare\log`. Without
   the append probe a read-only newest file would pass, and NLog would drop every line in silence.
3. `Build` makes the configuration, one `AtomicFileTarget` and one rule from the level up, and
   `Apply` sets it. `Off` gives a configuration without targets, and `Apply` returns there.
4. The hooks: `AppDomain.UnhandledException` (at `Fatal`) and `TaskScheduler.UnobservedTaskException`
   (at `Error`); `Program.Main` adds the window thread's `DispatcherUnhandledException`. `Fatal`
   writes an exception once: the same one seen by the dispatcher's hook, then by the process's, is
   named the second time, not written again.
5. The first line, `StartupLine`, at `Info`.

`LogSetup`'s own logger is a property fetched on use, since a logger created in its type initialiser
would exist before `Apply` sets the configuration; `NoLogLineGoesThroughTheInterfaceTexts` checks
how every logger is declared (the rule is `CLAUDE.md`'s), so its scan of the calls sees them all.

## The files

NLog 6.2.2 rolls forward without moving a file: `ImageVisualCompare.Wpf.log`, then past
`FileSizeLimit` `ImageVisualCompare.Wpf_01.log`, `_02`, and so on (`ArchiveSuffixFormat`'s default
`_{0:00}`). Each writer checks the size before its line, so a file ends one line past the limit, or
one line per writer when two write at once. `MaxArchiveFiles` counts the file being written, so it
is 5, and the next process appends to the newest file. `R120_TheLogRollsAt1MiBAndKeepsAtMost5Files`
writes 8 MiB through one factory, then a little through another, and reads the five names and the
newest file's end. In `R123_TwoProcessesWritingAtFullSpeedKeepEveryLine` two factories on one file
stand for two processes, each on a thread of its own released by a `Barrier` so that they overlap;
it asks for every line of both, whole, once, across the roll, and for their lines to interleave both
ways.

## The line

`LineLayout`: `${longdate}` (local time to 0.1 ms; the time zone is in the first line), the level
padded to five, `[pid:thread]`, the logger's name, the message, and on the next lines
`Exception.ToString()` when there is one. Messages are composed from `LogText` (invariant numbers,
six significant digits in `Num` and every digit in `Exact`, `none` for a missing value, sizes in
MiB, an elapsed time as `Ms`, `1141 ms`, or `Seconds`, `12.9 s`) and logged as plain strings, so
NLog never parses them as templates and a `{` in a path is harmless. A line whose message costs
something to build is guarded by `IsDebugEnabled` or `IsTraceEnabled`. What a probe found is
`LogText.Image`, the log's own words for `ImageInfo`, never `FormatDescription`, which is the
interface's. A message the user was shown goes through `LogText.OneLine`, which writes its line
breaks as ` | `.

## A load's lines

`ImagePairLoader.LoadAsync` holds a `RunClock` in a `using`: `RunClock.Start` writes
`load start A=<path> B=<path> mode=<asked>`, `Lap` ends a stage (`probe A`, `probe B`, `question`
while the padding is asked, `decode A`, `decode B`), and `Done` writes `load done <outcome> in N ms`.
`ReplaceAsync` (R-009) holds one named `replace`: `replace start A=<path> B=<path> changes=A|B
mode=<asked>`, the stages of the one side probed and decoded, `decode` of the other side only when
the kept file is decoded again, which a line `the kept A|B was rendered in <m>, so it is decoded
again in <m>` announces, and `replace done` with the same outcomes as a load. The load's lines
continue: `Done` writes `load done <outcome> in N ms
(<stages>)`. The outcomes: `mode=<m> padded=yes|no canvas=WxH buffers=<MiB>`, `declined`,
`refused: the sizes differ` (no one to ask), `failed on <path>` after the `Error` line with the
exception, `cancelled during <path>` without a stack. A load that throws past the catches, a defect,
leaves `load ended without an outcome after N ms (<stages>)` from `Dispose`, and the crash hook
writes the exception. Between the stages: `probed A:` and `probed B:` with the path, the size and
`LogText.Image`, the sizes of a mismatched pair and the answer, a mode that fell back to `Linear`,
and `decoded A:` and `decoded B:` with the range the values were read in and the mode drawn.

## What is logged where

| Seam | Logger | Level | What |
| --- | --- | --- | --- |
| `LogSetup` | `Process` | Info, Error, Fatal | the first line; the last line, `ExitLine`; the unhandled exceptions, an unobserved task |
| `App.ApplyCommandLine` | `App` | Info, Warn | `language=en\|ro`; a `--lang` refused; the pair the command line names, or the arguments that are not one, each missing file marked |
| `MainWindow` | `Window` | Info, Debug | `render tier <0-2> of 2, process render mode <m>` when the window is built (Debug); `open asked by the start\|the Open button\|Ctrl+O`, one asked while another runs (Debug); `change of A\|B asked by the Change A\|B button`, one asked while a pair is being opened or with no pair shown (Debug); what the dialogs chose, or that they were cancelled and what follows; a failed open, change or switch, the message being shown; a declined padding; `mode switch <from> -> <to>`, a refused one with why (Debug); `shown A=<file> B=<file> mode=<m> view=fit\|kept`; `memory:` managed and process, and what the process held before the outgoing pair was released (Debug); About. A repeat of a held `H` or `F` does nothing (`OnModeKeyAsync`, ADR 0044), and a repeat of `Ctrl+O` while a pair is being opened is not passed on, so neither writes a line |
| `CompareView` | `Surface` | Debug | `pair set` and `pair replaced, the view kept` with the canvas, the view in physical pixels, the display scale and the zoom; `zoom in\|out\|to fit\|to 100% -> <zoom>`, for a held key its first step only (`_keyRepeat`), then, when it is released, `zoom after the held key -> <zoom>` (`OnKeyUp`); a change of display scale |
| `ImagePairLoader` | `Load` | Info, Debug, Error | the load's lines above |
| `WicImageLoader` | `Wic` | Debug | `probed` and `decoding <path>: decoder=<type> format=<WPF's name, or unnamed> bpp=<n> WxH`, for a TIFF its `SampleFormat`, `BitsPerSample` and `SamplesPerPixel`, then `profiles=<n>`, the colour profiles it carries; a float TIFF's range decision (`LogText.FloatRange`). A tag or a profile the codec cannot give is written as such, never thrown |
| `FitsImageLoader` | `Fits` | Debug, Trace | `probed` and `decoding <path>: WxH planes bitpix bzero bscale data at <offset>`, BZERO and BSCALE with `Exact`; a float file's range decision; at Trace, from `Probe`'s `finally`, every card of the headers |
| `DarkTitleBar` | `Chrome` | Debug | the build, the attribute set and the HRESULTs of the dark caption and, on Windows 11, of the caption and border colours |
| `AboutWindow` | `Window` | Debug | `the link <url> could not be opened`, with the exception, when the system will not open a link of About |

`LogCards`, the Trace lines of `FitsImageLoader`, reads up to 1 MiB of the file on a stream of its
own, without the reader, so a header the reader refuses is written too. It walks each header card
by card to its `END`; the next header is the first 2880-byte block after it that starts with
`XTENSION`, past the data of the unit before, as the reader steps over random groups or a table to
the image. A byte that is not printable ASCII is written as `?` (`Printable`), so that no control
byte reaches the file; a card more than half of whose bytes are such is data, not a header, and
ends the walk; blank cards are left out.

`RoutingImageLoader`, `MinimapView`, `Bitmaps`, `MemoryHousekeeping`, `MessageDialog`,
`FileDialogService` and everything in Core write nothing: their facts reach the log through the
lines above (which loader a file went to is the logger column, `Wic` or `Fits`).

## Reading a log

A process begins at its `<name> <version> starting` line and ends at `exit <code> after <s>`; a
crash ends instead at a `FATAL` line with the stack under it, followed by `ended by an unhandled
exception after <s>` when the runtime still ran `Main`'s `finally`. Two windows writing at once
interleave, and the `[pid:thread]` field separates them. An open is found by `open asked by`, its
load by `load start` and `load done`, whose stage times say where the time went; a failure by
`ERROR` and the stack under it, with the message the user read on the same line.

## In the tests

`LogCapture` configures NLog once per test run, through a module initializer, with one target at
`Trace` that renders every event and with `LogManager.ThrowExceptions` on, so that an exception in a
line's composition, in its layout or in the target fails the test that reaches it. A test that reads
the lines opens a capture with `LogCapture.Start`, an `AsyncLocal` scope that sees only its own call
tree, so the tests running beside it add nothing; the scope flows into `Task.Run` and into a
dispatcher operation of the WPF test thread, which runs in the execution context of the call that
queued it. The WPF test thread itself is created in the execution context of the test that first
uses it, and the start WPF runs on it, the `App`'s own (`testing.md`), runs there too: a window
test therefore runs one empty body on the thread before it opens its capture, and leaves the lines
that report a state, the render tier and the memory, out of the lists it compares. The file tests
never touch that configuration: they build the application's with `LogSetup.Build` into a private
`LogFactory`, which stands for one process. `LanguageLoggingTests` runs with the other tests of the
language (ADR 0038), since it switches `UiText.Current`, and checks that the same loads write the
same messages in English on an English culture and in Romanian on a Romanian one. `Program.Main`,
`Apply` and `Close` run in no test:
`TheEntryPointStartsTheLogBeforeTheApplication_AndEveryCrashIsHooked` reads their order, their `try`
and `finally` and their hooks in the source, and the rest is checked by launching the application. A
test cannot make a `KeyEventArgs` a repeat: the held `H` and `F` are tested through
`OnModeKeyAsync`, which takes the repeat as an argument; the repeats of `Ctrl+O` and of the zoom
keys, and the zoom line of a held key's release, are checked by launching the application.
