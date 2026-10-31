# Process notes: conventions, versioning, licensing, build and run

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001), then cut on 2026-09-27 to what neither
> CLAUDE.md nor a decision record says (ADR 0051): the rules are in CLAUDE.md and the kit's
> `.claude/rules/kit/process.md`, the decisions with their reasons in `docs/decisions/`, and this
> note keeps the mechanics of the console and the few reasons that have no other home. **Covers:**
> by pointer, `Directory.Build.props`, `Version.cs`, `LicenseHeaderTests`, `VersionTests` and the
> publish commands; in full, the section "Extending the console", added 2026-09-25: `Options.cs`,
> `FlatCheckCli.cs`, `Program.cs`, `GoldenOutputTests`, `ParityGoldenTests` and `ParityFixtures`.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in the
> same task, or deleted with the decision it explains.

## Conventions

The rules are in `CLAUDE.md` and `.claude/rules/kit/process.md`; the packages, the hand-rolled
FITS and PNG and the standard of evidence a further package would have to meet are ADR 0008 and
`dependencies.md`. One reason with no other home:

- **User-facing documentation lives in `README.md`, and only there.** Do not restate the
  modes, the thresholds, the workflow traps or the `--light` validity limit in a docstring or
  a file header. It was duplicated between `README.md` and `flatcheck.py`'s docstring once,
  the two diverged, and the README was the wrong one - it still described a single-file
  `FlatCheck.cs`, a `csharp/` directory, three modes, and a port that had never been
  compiled. Code comments explain *why the code is the way it is*; the README explains *how
  to use the tool*.

## Versioning

ADR 0039 holds the whole of it, amended by ADR 0047 (the version is declared once, in
`Directory.Build.props`; the frozen `flatcheck.py` is no longer synchronised): the banner on line 1
of every run and why there is no `--version` flag, `VersionInfo` reading
`AssemblyInformationalVersion` with no literal, the scrub to `flatcheck <VERSIUNE>` in
`GoldenOutputTests.RunCli`, and what fails when the plumbing moves. ADR 0042 settles the
machine-readable case. The owner bumps, never the assistant (the kit's rule "Version and year are
the owner's"). Guards: `VersionTests`, the golden files.

## Licensing

ADR 0040 records the five things pinned by tests (the notice on every source file, the copyright
on line 2, the warranty in `--help`, the window's "Despre", one source of truth for the copyright
string), their guards, and why the copyright line is not scrubbed like the banner; ADR 0047
amends the Python half. A new file follows the rule "License headers", a new package the rule "Package notices" and ADR 0008. Three
mechanics the ADR does not spell out:

- XAML gets one `<!-- ... -->` block wrapping the unprefixed notice because `--` cannot repeat
  inside an XML comment.
- The SPDX line is there so licence scanners can read the file without parsing English.
- `VersionInfo.Copyright` reads `AssemblyCopyrightAttribute`; the literal in `ReadCopyright` is
  only a fallback, and `VersionTests` binds the two.

## Build, test and publish

The build, test, golden and screen-dump commands are in CLAUDE.md ("Commands"). The two publish
targets are in `kit.json` (`scripts/release.ps1` runs them), and `README.md` ("Getting it" and
"Development") explains them for the user: the two flags that keep `flatcheck.exe` a single
copyable file now that libraw is in, the RIDs that have a libraw binary, the `lensfun/` folder that rides
beside both executables (ADR 0028), why the window publishes without `PublishSingleFile`, and
why the whole solution no longer builds on Linux. `Directory.Build.props` declares the runtime
they publish for and turns the single-file analyzer off, so a release's publish leaves the lock
files alone (ADR 0056). One reason kept here: `FLATCHECK_DUMP_SCREENS`
exists for the locked Windows session, where a screen capture would catch the lock screen and
the render does not (ADR 0037: the PNGs are for eyes, never a golden).


## Extending the console

> Added 2026-09-25 (docs-health audit, gap 2). **Covers:** `FlatCheck/Options.cs`,
> `FlatCheck/FlatCheckCli.cs`, `FlatCheck/Program.cs`, `FlatCheck/FlatCheck.csproj`,
> `FlatCheck.Tests/GoldenOutputTests.cs` with `Golden/`, `FlatCheck.Tests/ParityGoldenTests.cs`
> with `ParityFixtures.cs`, the mode lists in `FlatCheck.Tests/VersionTests.cs`, and
> the "Options" section of `README.md`.

The console is three files, and that is the point: `Program.cs` is a shim (`Main` calls
`FlatCheckCli.Run(args, Console.Out, Console.Error)` between the diagnostic log's first and last
lines, `logging.md`), `Options.cs` parses, and
`FlatCheckCli.cs` dispatches and turns a verdict into an exit code. Everything else is
`FlatCheck.Core/Runs/`. A new flag or a new mode touches those files plus a fixed list of
tests and documents; this section is that list, in the order the work goes.

### The Options contract

- `Options` is an `internal sealed class` of plain `internal` fields: no properties, no
  setters, no builder. One field per flag (`Image`, `Light`, `Expo`, `Sky`, `Histeq`,
  `Rainbow`, `Synthetic`, `Lens`, `Source`, `Focal`, `FStop`, `Crop`,
  `Profiles`, `Lenses`, `Against`, `Raw`, `OutPng`, `Sat`, `Black`, `Pattern`, `Fov`), plus
  `Help` and `Error`. A flag with a value is a `string` or a nullable number (`double?`), so
  "not given" is `null` and never a sentinel; a switch is a `bool`. Defaults are field
  initialisers, and when the default is a value of the core's the core owns it (`Pattern =
  SeriesFinder.DefaultPattern`): the console holds
  no number of its own (the rule "Computed only in the core", ADR 0011).
- `Options.Parse(string[] args)` never throws. It returns an `Options` with `Help = true` (no
  arguments, or `-h` / `--help` anywhere; parsing stops there), or with `Error` set (the first
  failure wins: `Fail(message)` stores it and returns the same object), or fully parsed with
  `Error == null`. There is no exception path and no result type; `OptionsTests` asserts on
  the fields and on `Error` directly.
- The error is a `T(english, romanian)` pair that names the argument and what it expected,
  without the `ERROR: ` prefix (`EROARE: ` in Romanian); `FlatCheckCli.Run` adds the prefix when
  it prints it to stderr and exits 1 (R-005). Follow the existing shape:
  `T($"--fov expects degrees between 0 and 180, got {v}", $"--fov asteapta grade intre 0 si 180, a primit {v}")`.
  `--lang` never reaches `Options`: `LanguageArgument.Extract` takes it out first (`language.md`).
- Every flag is in exactly one of two static sets: `BoolFlags` (a switch, no value) or
  `ValueFlags` (consumes the next token). A token that starts with `-` and is in neither set
  fails with `optiune necunoscuta`, unless it parses as a number, so `-5` stays a positional.
  A value flag with no next token, or whose next token is itself a flag, fails before the
  value is read. The value's own validation is the `case` for that flag in the `switch`:
  `TryNum` (`double.TryParse` with `NumberStyles.Any` and `CultureInfo.InvariantCulture`, so
  `0.5` parses and `0,5` does not; `SatAndBlack_UseInvariantCulture...` in `OptionsTests`),
  then the range or the enumeration inline (`--fov` in (0, 180), `--scale` in [50, 2000] or
  `auto`, `--source` `adobe` or `lensfun`).
- Flags come before or after the path with the same result (R-003;
  `R003_FlagBeforePath_GivesTheSameResultAsFlagAfterPath` in `GoldenOutputTests`). The parser
  collects every non-flag token as positional and, after the loop, wants exactly one; none
  with `--lenses`, which is a list and needs no input.
- Cross-argument checks come after the loop, in the same method: `--synthetic`
  refuses to write over the input (`SamePath`, case-insensitive on Windows), and `--sat` must
  exceed `--black`. Mode flags are **not** mutually validated here
  (`HisteqAndExpoTogether_BothStaySet`): `flat.fit --histeq --expo` parses cleanly with both
  set, and dispatch decides.

So a new flag is: a field with its default, an entry in `BoolFlags` or `ValueFlags`, a `case`
with its validation and its `Fail` messages, a cross-argument check when it interacts with
another flag, and an `OptionsTests` fact for the parsed value and for each `Fail`. A new mode
adds its switch or value flag the same way, a value of `Mode` where its precedence belongs in
`Options.ModeOf` (`Parse` decides the mode once, R-003, and the dispatch and the log's `mode=`
line both read `o.Mode`, so a mode cannot be in one and not the other), and continues below.

### Dispatch in FlatCheckCli.Run

`Run(string[] args, TextWriter stdout, TextWriter stderr)` is the whole program and is what
every test calls, with two `StringWriter`s. In order:

1. `RunReport.WriteBanner(stdout)`: version on line 1, copyright on line 2, before anything
   else, so `--help` and errors carry them too (R-001). The order is the invariant R-001 states:
   the banner is written before the options are parsed, so no option can suppress or move it;
   a machine-readable output would be the only exception (ADR 0042), and none exists.
2. `LanguageArgument.Extract` takes `--lang` out; a value it does not know prints an English
   `ERROR:` and returns 1; the rest runs in that language (`language.md`).
3. `Options.Parse`. `Help` prints `WriteUsage` and returns 0. `Error` prints
   `ERROR: <Error>` (Romanian `EROARE:`) to stderr and returns 1.
4. A folder as input is refused with exit 1 unless `--light` (series mode).
5. The mode, `o.Mode`, chosen by `Options.ModeOf` as the first match in a fixed order:
   `--lenses`, `--synthetic`, `--against`, `--histeq`, `--expo`, `--light`,
   structure (the default, no flag). That order is the mutual exclusion R-003 promises and
   the requirement itself; `Run` logs `mode=<name>` from it and switches on it. The comment
   at the top of `FlatCheckCli.cs` still lists the pre-mode-7 order; the code is the truth. A
   new mode is one more value of `Mode`, placed where its precedence belongs in `ModeOf`, one
   more `case` in the switch, and R-003 is updated in the same task.
6. Everything inside the chain runs under one `try`; any exception becomes
   `ERROR: <message>` on stderr and exit 1. That is how a missing file, an invalid FITS or a
   failed fit are reported; the run methods do not catch.
7. A trailing empty line on stdout, then the exit code.

Each mode has one static `RunX(Options, TextWriter[, TextWriter err])` that calls its
`XRun.Execute(...)` in `FlatCheck.Core/Runs/`, writes the map(s) through `SaveMap` (which also
prints the `map saved` line via `RunReport.WriteMapSaved`) and returns either the verdict
string or an exit code:

- A mode with a verdict returns the verdict and the chain wraps it in `ExitFor(string)`
  (`OK` / `PERFECT` / `BUN` → 0, `Stats.NotApplicable` → 1, `SUSPECT` / `ACCEPTABIL` /
  `NEDETERMINAT` → 2, anything else 3); `--expo` has its own `ExitFor(ExposureVerdict)`. The
  mapping is R-002, explained in `thresholds.md`, pinned by `R002_ExitCodesFollowVerdictSeverity`.
- A mode without a verdict (`--histeq`, `--synthetic`, `--lenses`) returns its
  `int` directly and must not invent an `OK`: 0 means "wrote the file", 1 a refusal (the first
  line of the outcome's `Refusal` goes to stderr with the `ERROR: ` prefix), 2 "read this
  before deciding" (`--synthetic` with an ambiguous
  lens).
- A mode writes one map, at `-o`. Until ADR 0080, `--smooth` derived its second map's path
  from `-o` by suffix (`-after`, the same in both languages; R-006, R-103), never by a second
  flag; do the same for a mode with two maps.

Nothing in these methods computes: a run method that needs a number or a sentence gets it from
a `Runs/` outcome (the rule "Computed only in the core").

### Usage text

`WriteUsage(TextWriter)` is a private static method at the bottom of `FlatCheckCli.cs`: it
writes `WriteUsageEnglish` or `WriteUsageRomanian`, each one `WriteLine` per invocation form,
then the optional flags grouped by the mode they serve, then a short paragraph per mode, and
then `RunReport.WarrantyLines` (GPLv3 §5(d); see "Licensing"). The two are written whole, not
line by line in pairs, because their columns are aligned by hand; a new flag goes in both. The
Romanian is without diacritics, like every console line.
`Golden/help.txt` (Romanian) and `Golden/en_help.txt` pin the whole text through `Help_IsPrintedForNoArgs` and
`Help_IsPrintedForHelpFlags` (`-h`, `--help`), and
`R005_HelpCarriesTheWarrantyDisclaimerAndPointsAtTheLicense` in `VersionTests` pins the legal
lines. Any edit to `WriteUsage`, a new flag included, therefore changes `help.txt` and goes
through the golden procedure below. A default printed in the usage is interpolated from the
core's constant, so the number lives once (as `--scale` did until ADR 0080); do the same for a
new default.

### Adding a golden scenario

The golden tests are the console's output contract; the class comment in
`GoldenOutputTests.cs` says why they are not scaffolding. The mechanics:

- `RunCli(TempWorkspace ws, params string[] args)` runs `FlatCheckCli.Run` in-process with two
  `StringWriter`s, appends stderr after a `[stderr]` marker line when there was any, replaces
  the workspace folder with `<TMP>` (`<TMP>/` when a separator follows), replaces the banner
  line with `flatcheck <VERSIUNE>` (`VersionBanner`, multiline with `\r?`), and returns
  `(Output, Exit)`. The copyright line is not scrubbed (see "Licensing"). Only paths inside
  the workspace are scrubbed, so every fixture and every `-o` goes through `ws.PathTo` or
  `Flat(ws, ...)`.
- `AssertGolden(name, actual)` normalises CRLF to LF and compares with
  `FlatCheck.Tests/Golden/<name>.txt`, found relative to the test source file
  (`[CallerFilePath]`), not to the build output. With `FLATCHECK_UPDATE_GOLDEN=1` it writes the
  file instead (UTF-8 without BOM, LF) and asserts nothing; **a missing file is therefore
  created on the first run under the variable**. Without the variable a missing file fails
  with a message that says to re-run with it.
- Names are lowercase `mode_scenario` in snake case: `structure_clean`,
  `light_series_undetermined`, `synthetic_ambiguous`, `help`. A name
  describes the behaviour, not the fixture.

The procedure:

1. Write the test in the mode's section of `GoldenOutputTests.cs` (the `// ---- mode`
   rulers). Build the fixture in a `using var ws = new TempWorkspace()` from `FitsTestData`
   scenes (every FITS fixture is generated in the test; nothing is read from disk), call
   `RunCli`, assert the exit code and one or two `Assert.Contains` on the lines that carry the
   behaviour, and end with `AssertGolden("<name>", output)`. Name the test `R###_...` when it
   implements a requirement row. Add a golden file only for a genuinely distinct behaviour;
   when one line must appear, an `Assert.Contains` on an existing scenario is enough. Add the
   scenario to `EnglishOutputTests` too (`Scenarios` and `Arguments`), which writes
   `Golden/en_<name>.txt` and checks its numbers against the Romanian run (`language.md`).
2. Run the update once, on the golden tests only:

   ```powershell
   $env:FLATCHECK_UPDATE_GOLDEN = '1'; dotnet test FlatCheck.sln --filter "GoldenOutputTests|EnglishOutputTests"; Remove-Item Env:\FLATCHECK_UPDATE_GOLDEN
   ```

   The `FLATCHECK_UPDATE_GOLDEN=1 dotnet test FlatCheck.sln --filter "GoldenOutputTests|EnglishOutputTests"`
   form in CLAUDE.md is the same thing from a POSIX shell. The variable rewrites **every** golden file
   the filter selects, not only the new one; without the filter it would rewrite the 39 parity
   goldens too.
3. `git status`: the only change under `Golden/` must be the new untracked file. A modified
   existing file is an output change you did not mean, or one you did mean and must now
   account for; either way it is read line by line, never re-approved by reflex.
4. Read the new file top to bottom, as the user reads the console: the banner placeholder, the
   copyright, the header lines, the numbers, the verdict, the `harta salvata` (English `map saved`) line with
   `<TMP>/`.
5. Run the suite without the variable; it must be green.
6. The wrap-up report names the new file and every changed line of an existing one with its
   cause (the rule "Golden files").

### Adding a parity golden scenario

When it is required: the rule "Formula changes re-validate" and `/new-feature` step 2. A **new** scenario is required when a
change opens a numeric path the 39 existing scenarios do not exercise: a new band or branch of a
metric, a new input shape (size, BITPIX, a header card), a new option that changes a number.
`--histeq` and mode 7 produce a picture, a file or a list and get no scenario; their
goldens are in `GoldenOutputTests`. `python-parity.md` lists the current coverage and where the
set came from (the retired Python cross-check, ADR 0047).

A scenario has two halves that must agree on the file name:

1. **The fixture**, in `ParityFixtures.WriteAll`. Add a `Write(dir, "name.fit", scene)` for a
   512×512 frame, or `File.WriteAllBytes(Path.Combine(dir, "name.fit"),
   FitsTestData.BuildFits(w, h, scene, bitpix: ..., extraCards: ...))` for another size, BITPIX
   or header, or `SeriesDir(dir, "name", scenes...)` for a `--light` folder. Reuse the golden
   test's `FitsTestData` scene when there is one, so the two goldens pin the same bytes, and say
   in a comment which branch the fixture is there to reach.
2. **The invocation**, in `ParityGoldenTests.Scenarios`: a row `{ "<mode> <what>", new[] {
   "name.fit", "--flag", "value", ... } }`, grouped with its mode. Every argument that names a
   fixture file or folder is made absolute by the test; the rest pass through unchanged (a
   `--pattern` stays a pattern because no such file exists). The test appends
   `-o <slug>.png` itself. The name becomes the golden file, `Golden/parity_<slug>.txt`
   (lower case, runs of non-alphanumerics to `_`), with the exit code as its last line.

Then capture the one new file with `FLATCHECK_UPDATE_GOLDEN=1 dotnet test --filter
ParityGoldenTests`, read it, and check `git status` shows only the new `parity_*.txt`. A capture
that also rewrote one of the 39 originals is a finding for the owner (the rule "Golden files"):
put that file back with `git checkout -- <file>` and ask; a scenario added after 2026-09-26 is
judged like any golden. Bump the count in
`TheTableHasTheThirtyNineScenariosLeftOfTheCrossCheckAndNoTwoShareAGolden`, in R-098 and in
`python-parity.md`. Unlike the 39 originals, a new scenario's numbers were never confirmed by a
second implementation; its comment says what confirms them (a unit test, a hand calculation, a
figure on a real master).

### The VersionTests mode lists

`R001_EveryModePrintsTheBannerOnTheFirstLine` and `R001_EveryModePrintsTheCopyrightOnTheSecondLine`
each hold an explicit list of invocations, one `Assert.Equal` per mode; the copyright one adds a
`--synthetic` refusal, `--help` and a missing file. A new mode is one more line in each, with the
arguments it needs to reach its report. Since 2026-09-27 the lists cover every mode (seven
since `--ratio` and `--smooth` left, ADR 0067 and ADR 0080): structure, `--light`, `--expo`, `--histeq`,
`--synthetic`, `--lenses` and `--against` (the last three with an empty `--profiles` folder,
which is enough for the banner: it is written before the options are parsed).

### The README "Options" section

`README.md` is the user documentation and the only place the options are described for users
(the rule "Languages"; "Conventions" above). Its `## Options` section is a code block that mirrors
the English `WriteUsage`: the invocation forms, then one line per flag with the modes it applies
to and its default, then two paragraphs (flags on either side of the path, and the order the
modes are dispatched in; the banner on every run). Every option appears there, in English (ADR
0069). A new flag gets its line there, a mention in the mode's own `### N.` section when it
changes what that mode does, a row under `## Thresholds` when it brings a threshold, and a line
under `## Exit codes` when it adds or changes an exit code. The requirement row of the kit's rule
"Requirements first" goes under "Console contract" for a flag and in the mode's block for a mode.

### InvariantGlobalization

`FlatCheck/FlatCheck.csproj` sets `<InvariantGlobalization>true</InvariantGlobalization>`, and
so does `FlatCheck.Core.csproj`. It is a runtime switch read from the executable's
`runtimeconfig.json`: the console runs without ICU culture data, its `CurrentCulture` behaves
as the invariant culture whatever the Windows locale, so `{x:F2}` prints `0.29` and never
`0,29`, and the golden files compare against that. Two consequences for new
code:

- Parsing an argument still names the culture explicitly (`TryNum` passes
  `CultureInfo.InvariantCulture`), and so should any new formatting or parsing in
  `FlatCheck.Core`: the same core runs inside the WPF exe, which does **not** set the switch
  (WPF cannot load XAML without culture data, ADR 0037) and pins numbers in code through
  `App.ForceInvariantNumbers` instead. The setting in `FlatCheck.Core.csproj` has no effect on
  a host that does not set its own. `CultureTests` in `FlatCheck.Wpf.Tests` runs the window
  under `ro-RO` to prove the saved report matches the console.
- `FlatCheck.Tests` sets the same switch, for the reason and with the consequence ADR 0043
  records: the test host is its own executable, and a test that needs a named culture such as
  `ro-RO` goes in `FlatCheck.Wpf.Tests`. `InvariantGlobalizationTests` (R-007) checks the
  console's `runtimeconfig.json` and the test host. The report code mostly formats with the
  ambient culture; `StructureReport` names the invariant one in a single place.
