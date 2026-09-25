# Process notes: conventions, versioning, licensing, build and run

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001): this is the explanation behind the code,
> read on demand; CLAUDE.md keeps only the rules. **Covers:** the rules CLAUDE.md states in one line each, with their reasons; `Directory.Build.props`, `Version.cs`, `LicenseHeaderTests`, `VersionTests`, the publish commands. The Committing section predates rule 13 of CLAUDE.md, which now governs; the rest stands. The section "Extending the console", added 2026-09-25, also covers `Options.cs`, `FlatCheckCli.cs`, `Program.cs`, `GoldenOutputTests`, `CrossCheckFixtures` and `SCENARIOS` in `crosscheck.py`.
> Cross-references such as "see X below" point at the sibling notes in this folder; the index in
> `README.md` here maps them. A change to the code this describes means this note is updated in
> the same task, or deleted with the decision it explains.

## Conventions

- **User-facing text (CLI messages, `--help`, window labels, code comments) is in
  Romanian** - keep new text consistent with this. Documentation *about* the code
  (this file, `docs/`, an implementation plan while it lives) is in English.
- **Nothing is computed outside `FlatCheck.Core`.** If a change needs a new number, a new
  threshold or a new sentence of diagnosis, it belongs in `Analysis/`, `Thresholds.cs` or
  `Report/` - never in a view-model. Both applications then get it, and get the same one.
  A view-model formats; it does not decide.
- **One external dependency, and it took a measured argument to earn it.** The FITS
  reader and PNG encoder stay hand-rolled. Don't add a NuGet package to replace them,
  and don't add a second one without the same standard of evidence; the point of this
  tool is that it is a single copyable binary. See "The one dependency".
- **Every metric is a ratio**, so ISO/gain cancels out. Don't add gain-dependent
  corrections - if a new metric isn't scale-invariant, that's a design smell.
- **Thresholds are empirical rules of thumb, not physical constants.** They are a
  starting point to be calibrated against a known-good setup.
- **Never bump the version and never commit** - both are the user's call. See
  "Versioning" and "Committing" below.
- **Every source file starts with the GPL notice.** New files included. See
  "Licensing" below.
- **User-facing documentation lives in `README.md`, and only there.** Do not restate the
  modes, the thresholds, the workflow traps or the `--light` validity limit in a
  docstring or a file header. It was duplicated between `README.md` and `flatcheck.py`'s
  docstring once, the two diverged, and the README was the wrong one - it still described
  a single-file `FlatCheck.cs`, a `csharp/` directory, three modes, and a port that had
  never been compiled. Code comments explain *why the code is the way it is*; the README
  explains *how to use the tool*.


## Versioning

Every run prints `flatcheck <version>` as its **first line** - every mode, `--help`, and
failed runs too, so a pasted report always says which version produced it. There is no
`--version` flag; it would be redundant. A machine-readable output, if one is ever added, carries
the same two strings as its first two fields instead of two lines (ADR 0042).

Both applications print it from `RunReport.WriteBanner`, so the WPF app's saved
`Rezultat testare.txt` opens on the same two lines as a console run, and a bump moves both
at once.

The version lives in **two** places:

| where | what |
|---|---|
| `Directory.Build.props` | `<Version>` - the single source of truth for C# |
| `flatcheck.py` | `__version__` |

`Directory.Build.props`, not a csproj, and that changed in 2.0.0 for a concrete reason:
there are now three assemblies. `VersionInfo` reads the one it lives in - Core - but the
console exe and the WPF exe carry their own, which is what Windows shows in file
properties. Two places to edit means, sooner or later, two different values. So the
property sits above all of them and every project inherits it.
**Don't move `<Version>` or `<Copyright>` back into a csproj**; `VersionTests` checks that
no project overrides either.

`FlatCheck.Core/Version.cs` does **not** hardcode it. `VersionInfo.Value` reads the
assembly's `AssemblyInformationalVersion`, which MSBuild derives from `<Version>`, and
trims any `+sourcerevision` suffix. Don't reintroduce a literal there.

**The user bumps the version manually**, per feature or fix, choosing the increment
themselves. **Never bump it on your own initiative** - not as part of a change, not
"while you're in there". If a change seems to warrant a bump, say so and let them decide.

**Bumping the version breaks nothing.** That is deliberate, and worth preserving: the
banner is scrubbed to `flatcheck <VERSIUNE>` before any comparison, in both places that
compare whole output.

- `GoldenOutputTests.RunCli` scrubs it, the same way it already scrubs absolute paths -
  both change without the *behaviour* changing. So `Golden/*.txt` carry the placeholder
  and survive a bump untouched.
- `crosscheck.py` scrubs it in `norm()`, and checks version agreement once up front in
  `check_versions()`. Without that, a desync would turn all 43 scenarios into diffs -
  42 diffs to learn one thing.

What *does* fail, and should:

| situation | what fails |
|---|---|
| both places bumped | nothing |
| only one place bumped | exactly one test, `R097_PythonReportsTheSameVersionAsTheCSharp`; `crosscheck.py` prints one `[VERSIUNE]` line and exits 1 |
| `<Version>` removed from `Directory.Build.props`, or re-added to a csproj | the plumbing tests in `VersionTests` |
| the banner stops being printed | the golden files, because they carry a placeholder line the output no longer produces |

The scrubbing does not weaken coverage: `VersionTests` asserts the banner's exact content
and its position on line 1, for every mode, for `--help`, and for failed runs.


## Licensing

The tool is **GPLv3-or-later**. The full text is in `LICENSE` at the repo root (the
verbatim FSF `gpl-3.0.txt` - don't reword it, and don't reflow it).

**Third-party code has its own file.** `THIRD-PARTY-NOTICES.md` lists every package linked
in, its licence, and - the part that matters - *why* that licence is compatible with
GPLv3. Two of the current entries take a real decision rather than inheriting one: LibRaw
is dual-licensed and only one of its two options is GPL-compatible, and ImageSharp's
licence is conditional and has to be shown to apply. A package added without an entry there
is a package whose compatibility nobody checked. See "The dependencies" above for the rule.

Five things follow, and all five are pinned by tests:

- **Every source file starts with the notice**, above the file's own header comment:
  the SPDX line, the copyright, and all three paragraphs of the standard GNU notice
  (permission, no-warranty, where-to-get-a-copy). `//` for C#, `#` for Python (below the
  shebang), and for XAML a single `<!-- ... -->` block wrapping the unprefixed text -
  `--` cannot repeat inside an XML comment. XAML counts as source: it is written by hand
  and it describes the interface. New files get the notice too - it is the first thing to
  write, not an afterthought. `LicenseHeaderTests` walks all four projects plus the two
  `.py` files and fails on any file missing it, which is the only thing keeping the
  convention alive as files get added; its `Projects` list is explicit rather than
  discovered, so adding a project is a decision. The SPDX line is there so licence
  scanners can read the file without parsing English.
- **Every run prints the copyright on line 2**, right under the version banner, in every
  mode, at `--help`, and on failed runs. Same reason as the banner: a pasted report says
  on its own whose tool produced it. A machine-readable output, if one is ever added, carries
  both as its first two fields (ADR 0042).
- **`--help` carries the warranty disclaimer** and points at `LICENSE` - GPLv3 §5(d),
  which wants a program that displays legal notices to display that one too. It lives
  there rather than under the banner on purpose: five lines on top of every report,
  every run, would drown the numbers. The text is `RunReport.WarrantyLines` in C#, which
  `WriteUsage` prints line by line and the window's "Despre" shows as one paragraph -
  `Golden/help.txt` pins the console side, `AboutTests` the window side; the argparse
  `epilog` (`__warranty__`) in Python.
- **The window's "Despre" is its §5(d) notice**, and it also carries the third-party
  list with each component's copyright line. That second part is LGPL-2.1 §6: when a
  program displays copyright notices at runtime - and this one does, on line 2 of every
  report and in the window's top bar - it must include the library's notice among them,
  with a reference to the LGPL. The list comes from `ThirdParty.Components`; LibRaw's
  copyright line there is the one from LibRaw 0.21.1's `COPYRIGHT` file, the version the
  runtime packages ship. `LibRawIsTakenUnderTheLgplAndCarriesItsCopyright` pins both the
  licence choice and the notice.
- **One source of truth per language for the copyright string**: `<Copyright>` in
  `Directory.Build.props` (read back through `AssemblyCopyrightAttribute` by
  `VersionInfo.Copyright` - the literal in `ReadCopyright` is only a fallback), and
  `__copyright__` in `flatcheck.py`. `VersionTests` binds all three together.

Unlike the version banner, the copyright line is **not** scrubbed anywhere: it is
constant, so the golden files carry it verbatim and `crosscheck.py` compares it as
ordinary text. That is deliberate - if the line ever stops being printed, the golden
files fail. `--help` is not one of `crosscheck.py`'s 43 scenarios (argparse and
`WriteUsage` produce entirely different text), so the two warranty notices are worded
the same by choice, not by test.

The year and the author are the user's call, exactly like the version. Don't roll the
year forward on your own initiative.


## Committing

**Do not commit.** Make the changes, run the tests, and hand over a commit message for
the user to use. They commit themselves.

This also means: don't `git add`, don't stage, don't amend. Leave the working tree as it
is so they can review the diff before deciding.

A good commit message here follows the existing log: a short lowercase subject line, then
a body that explains *why* rather than restating the diff - what the change is for, what
was deliberately left alone and why, and anything a future reader would otherwise have to
rediscover. No `Co-Authored-By` trailer.


## Build & test

```bash
dotnet build FlatCheck.sln -c Release
```

**The solution no longer builds on Linux**, because `FlatCheck.Wpf` targets
`net8.0-windows`. That is a deliberate cost, not an oversight: the console stays portable
if you build its project directly, and there is no CI workflow in the repo, so nothing
else notices.

```bash
dotnet build FlatCheck/FlatCheck.csproj -c Release
```

```bash
dotnet test FlatCheck.sln
```

Two test projects run: `FlatCheck.Tests` (core + console, portable) and
`FlatCheck.Wpf.Tests` (window, Windows-only).

The golden-output tests snapshot every mode's console text. When you change output
*on purpose*, re-approve them and read the resulting diff:

```bash
FLATCHECK_UPDATE_GOLDEN=1 dotnet test FlatCheck.sln
```

To look at the window without running it - handy when the Windows session is locked and a
screen capture would catch the lock screen:

```bash
FLATCHECK_DUMP_SCREENS=/tmp/ecrane dotnet test FlatCheck.Wpf.Tests --filter WindowSmokeTests
```


## Run

Self-contained binary:

```bash
dotnet publish FlatCheck/FlatCheck.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The last two flags are what keep the "single copyable binary" promise now that libraw is
in: without them its four native DLLs sit next to the exe as separate files. With them
the whole thing is one ~67 MB `flatcheck.exe` again. `linux-x64` works the same; other
RIDs (arm64, macOS) have no libraw binary in these packages, so FITS-only there.

Note the explicit project path: `dotnet publish` on the solution would now also try the
WPF project, which cannot be single-filed the same way.

**The `lensfun/` folder rides beside the exe**, in the build output and in the publish
folder (both csproj files copy it): the single copyable binary is now "the exe, plus the
`lensfun` folder if you want the lensfun half of the lens list". Without the folder every
mode still runs; `--lenses` says which folder it looked at, and the window's panel too.
The Adobe half needs nothing shipped: it is read from where Lightroom / Camera Raw / DNG
Converter installed it.

The window app publishes without `PublishSingleFile`:

```bash
dotnet publish FlatCheck.Wpf/FlatCheck.Wpf.csproj -c Release -r win-x64 --self-contained
```

```bash
FlatCheck.Wpf.exe "D:\Astro\2026-08-12-Smida"   # optional: the session folder
```

```bash
flatcheck process/pp_flat_stacked.fit                  # 1. structure (or masters/pp_flat_stacked.fit, Siril 1.4 stock scripts)
flatcheck s_0deg/process/pp_flat_stacked.fit --ratio s_180deg/process/pp_flat_stacked.fit   # 2. attribution
flatcheck process/pp_light_00001.fit --light           # 3. end-to-end
flatcheck flat_brut.fit --expo                         # 4. exposure
flatcheck DSC_0060.NEF --expo                          # 4. exposure, straight off the raw
flatcheck sky_flat.fit --expo --sky                    # 4b. sky flat
flatcheck flat_brut.fit --expo --sat 4095 --black 150  # override, mostly for FITS
flatcheck flat.fit -o harta.png                        # custom PNG path
flatcheck process/pp_flat_stacked.fit --raw flats/DSC_0001.NEF   # 1 + the sixth test: vignetting vs the lens profiles, lens and f-stop from the raw
flatcheck --lenses --lens "Tokina 11-20"               # 7. which lenses have a vignetting profile
flatcheck process/pp_flat_stacked.fit --synthetic process/pp_flat_stacked_sintetic.fit --lens "Tokina AT-X 11-20mm F2.8 PRO DX" --fstop 2.8
flatcheck process/pp_flat_stacked.fit --against process/pp_flat_stacked_sintetic.fit   # how much the master differs from the model
```

Remember: the flag must come *after* the file path.


## Extending the console

> Added 2026-09-25 (docs-health audit, gap 2). **Covers:** `FlatCheck/Options.cs`,
> `FlatCheck/FlatCheckCli.cs`, `FlatCheck/Program.cs`, `FlatCheck/FlatCheck.csproj`,
> `FlatCheck.Tests/GoldenOutputTests.cs` with `Golden/`, `FlatCheck.Tests/CrossCheckFixtures.cs`
> with `SCENARIOS` in `crosscheck.py`, the mode lists in `FlatCheck.Tests/VersionTests.cs`, and
> the "Opțiuni" section of `README.md`.

The console is three files, and that is the point: `Program.cs` is a one-line shim (`Main`
calls `FlatCheckCli.Run(args, Console.Out, Console.Error)`), `Options.cs` parses, and
`FlatCheckCli.cs` dispatches and turns a verdict into an exit code. Everything else is
`FlatCheck.Core/Runs/`. A new flag or a new mode touches those files plus a fixed list of
tests and documents; this section is that list, in the order the work goes.

### The Options contract

- `Options` is an `internal sealed class` of plain `internal` fields: no properties, no
  setters, no builder. One field per flag (`Image`, `Ratio`, `Light`, `Expo`, `Sky`, `Histeq`,
  `Smooth`, `ScalePx`, `ScaleAuto`, `Synthetic`, `Lens`, `Source`, `Focal`, `FStop`, `Crop`,
  `Profiles`, `Lenses`, `Against`, `Raw`, `OutPng`, `Sat`, `Black`, `Pattern`, `Fov`), plus
  `Help` and `Error`. A flag with a value is a `string` or a nullable number (`double?`), so
  "not given" is `null` and never a sentinel; a switch is a `bool`. Defaults are field
  initialisers, and when the default is a number the core owns it (`ScalePx =
  SmoothAnalyzer.DefaultScalePx`, `Pattern = SeriesFinder.DefaultPattern`): the console holds
  no number of its own (rule 6, ADR 0011).
- `Options.Parse(string[] args)` never throws. It returns an `Options` with `Help = true` (no
  arguments, or `-h` / `--help` anywhere; parsing stops there), or with `Error` set (the first
  failure wins: `Fail(message)` stores it and returns the same object), or fully parsed with
  `Error == null`. There is no exception path and no result type; `OptionsTests` asserts on
  the fields and on `Error` directly.
- The error is a Romanian string that names the argument and what it expected, without the
  `EROARE: ` prefix; `FlatCheckCli.Run` adds the prefix when it prints it to stderr and exits
  1 (R-005). Follow the existing shape: `--fov asteapta grade intre 0 si 180, a primit {v}`.
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
  with `--lenses`, which is a list and needs no input. The sentence at the end of "Run" above
  and in CLAUDE.md's Commands, "the flag comes after the path", predates phase 8; either order
  works.
- Cross-argument checks come after the loop, in the same method: `--smooth` and `--synthetic`
  refuse to write over the input (`SamePath`, case-insensitive on Windows), and `--sat` must
  exceed `--black`. Mode flags are **not** mutually validated here
  (`HisteqAndExpoTogether_BothStaySet`): `flat.fit --histeq --expo` parses cleanly with both
  set, and dispatch decides.

So a new flag is: a field with its default, an entry in `BoolFlags` or `ValueFlags`, a `case`
with its validation and its `Fail` messages, a cross-argument check when it interacts with
another flag, and an `OptionsTests` fact for the parsed value and for each `Fail`. A new mode
adds its switch or value flag the same way and continues below.

### Dispatch in FlatCheckCli.Run

`Run(string[] args, TextWriter stdout, TextWriter stderr)` is the whole program and is what
every test calls, with two `StringWriter`s. In order:

1. `RunReport.WriteBanner(stdout)`: version on line 1, copyright on line 2, before anything
   else, so `--help` and errors carry them too (R-001).
2. `Options.Parse`. `Help` prints `WriteUsage` and returns 0. `Error` prints
   `EROARE: <Error>` to stderr and returns 1.
3. A folder as input is refused with exit 1 unless `--light` (series mode).
4. The mode, chosen by the first match in a fixed `if / else if` chain: `--lenses`,
   `--synthetic`, `--against`, `--smooth`, `--histeq`, `--expo`, `--ratio`, `--light`,
   structure (the default, no flag). This chain is the mutual exclusion R-003 promises and its
   order is the requirement. The comment at the top of `FlatCheckCli.cs` still lists the
   pre-mode-7 order; the code is the truth. A new mode is one more `else if`, placed where its
   precedence belongs, and R-003 is updated in the same task.
5. Everything inside the chain runs under one `try`; any exception becomes
   `EROARE: <message>` on stderr and exit 1. That is how a missing file, an invalid FITS or a
   failed fit are reported; the run methods do not catch.
6. A trailing empty line on stdout, then the exit code.

Each mode has one static `RunX(Options, TextWriter[, TextWriter err])` that calls its
`XRun.Execute(...)` in `FlatCheck.Core/Runs/`, writes the map(s) through `SaveMap` (which also
prints the `harta salvata` line via `RunReport.WriteMapSaved`) and returns either the verdict
string or an exit code:

- A mode with a verdict returns the verdict and the chain wraps it in `ExitFor(string)`
  (`OK` / `PERFECT` / `BUN` → 0, `Stats.NotApplicable` → 1, `SUSPECT` / `ACCEPTABIL` /
  `NEDETERMINAT` → 2, anything else 3); `--expo` has its own `ExitFor(ExposureVerdict)`. The
  mapping is R-002, explained in `thresholds.md`, pinned by `R002_ExitCodesFollowVerdictSeverity`.
- A mode without a verdict (`--histeq`, `--smooth`, `--synthetic`, `--lenses`) returns its
  `int` directly and must not invent an `OK`: 0 means "wrote the file", 1 a refusal (the first
  line of the outcome's `Refusal` goes to stderr with the `EROARE: ` prefix), 2 "read this
  before deciding" (`--scale auto` with nothing to propose, `--synthetic` with an ambiguous
  lens).
- Secondary map paths are derived from `-o` by suffix (`WorldMapPath` → `-lume`,
  `AfterMapPath` → `-dupa`; R-006), never by a second flag.

Nothing in these methods computes: a run method that needs a number or a sentence gets it from
a `Runs/` outcome (rule 6).

### Usage text

`WriteUsage(TextWriter)` is a private static method at the bottom of `FlatCheckCli.cs`: one
`WriteLine` per invocation form, then the optional flags grouped by the mode they serve, then a
short paragraph per mode, then `RunReport.WarrantyLines` (GPLv3 §5(d); see "Licensing"). It is
Romanian without diacritics, like every console line; `README.md` uses diacritics.
`Golden/help.txt` pins the whole text through `Help_IsPrintedForNoArgs` and
`Help_IsPrintedForHelpFlags` (`-h`, `--help`), and
`R005_HelpCarriesTheWarrantyDisclaimerAndPointsAtTheLicense` in `VersionTests` pins the legal
lines. Any edit to `WriteUsage`, a new flag included, therefore changes `help.txt` and goes
through the golden procedure below. The default printed for `--scale` is interpolated from
`SmoothAnalyzer.DefaultScalePx`, so the number lives once; do the same for a new default.

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
- Names are lowercase `mode_scenario` in snake case: `structure_clean`, `ratio_world`,
  `light_series_undetermined`, `smooth_auto_no_texture`, `synthetic_ambiguous`, `help`. A name
  describes the behaviour, not the fixture.

The procedure:

1. Write the test in the mode's section of `GoldenOutputTests.cs` (the `// ---- mode`
   rulers). Build the fixture in a `using var ws = new TempWorkspace()` from `FitsTestData`
   scenes (every FITS fixture is generated in the test; nothing is read from disk), call
   `RunCli`, assert the exit code and one or two `Assert.Contains` on the lines that carry the
   behaviour, and end with `AssertGolden("<name>", output)`. Name the test `R###_...` when it
   implements a requirement row. Add a golden file only for a genuinely distinct behaviour;
   when one line must appear, an `Assert.Contains` on an existing scenario is enough.
2. Run the update once, on the golden tests only:

   ```powershell
   $env:FLATCHECK_UPDATE_GOLDEN = '1'; dotnet test FlatCheck.sln --filter GoldenOutputTests; Remove-Item Env:\FLATCHECK_UPDATE_GOLDEN
   ```

   The `FLATCHECK_UPDATE_GOLDEN=1 dotnet test FlatCheck.sln` form in CLAUDE.md is the same
   thing from a POSIX shell. The variable rewrites **every** golden file, not only the new one.
3. `git status`: the only change under `Golden/` must be the new untracked file. A modified
   existing file is an output change you did not mean, or one you did mean and must now
   account for; either way it is read line by line, never re-approved by reflex.
4. Read the new file top to bottom, as the user reads the console: the banner placeholder, the
   copyright, the header lines, the numbers, the verdict, the `harta salvata` line with
   `<TMP>/`.
5. Run the suite without the variable; it must be green.
6. The owner reads the diff and approves it (rule 3). The assistant proposes; it never
   re-approves a changed golden file on its own, and never edits one by hand.

### Adding a cross-check scenario

When it is required: rule 16. A formula changed in C# (`FlatCheck.Core/Metrics.cs`,
`Stats.cs`, `Analysis/`, `Report/`) is changed in `flatcheck.py` too, and
`scripts/crosscheck.ps1` runs before the task ends. A **new** scenario is required when the
change opens a path the existing scenarios do not exercise: a new band or branch of a metric, a
new input shape (size, BITPIX, a header card), a new option that changes a number, or a new
mode that has a Python counterpart. `--histeq`, `--smooth` and mode 7 have no counterpart on
purpose (rule 16, ADR 0038) and get no scenario. `python-parity.md` lists the current coverage
and the intentional divergences.

A scenario has two halves that must agree on the file name:

1. **The fixture**, in `CrossCheckFixtures.DumpsFixturesWhenAsked`. The test does nothing
   unless `FLATCHECK_DUMP_FIXTURES` names a folder; then it writes every fixture there. Add a
   `Write(dir, "name.fit", scene)` for a 512×512 frame, or
   `File.WriteAllBytes(Path.Combine(dir, "name.fit"), FitsTestData.BuildFits(w, h, scene,
   bitpix: ..., extraCards: ...))` for another size, BITPIX or header, or
   `SeriesDir(dir, "name", scenes...)` for a `--light` folder. Reuse the golden test's
   `FitsTestData` scene when there is one, so the golden file and the cross-check pin the same
   bytes, and say in a comment which branch the fixture is there to reach.
2. **The invocation**, in `SCENARIOS` in `crosscheck.py`: a tuple
   `("<mode> <what>", ["name.fit", "--flag", "value", ...])`, grouped with its mode. The
   script appends `-o py.png` / `-o cs.png` itself, runs `flatcheck.py` and the Release
   `flatcheck.exe` (`FlatCheck/bin/Release/net8.0/flatcheck.exe`) in the fixture folder,
   normalises the `harta salvata` line and the banner in `norm()`, and compares the text and
   then every numeric token as a float (tolerance 1e-9). The name is what the report prints
   and what `KNOWN_NUMERIC_DIVERGENCE` is keyed by; a scenario whose numbers differ **on
   purpose** goes there with the reason, and the reason is also in `python-parity.md` (today
   only `structure uniform`, ADR 0038). A new option must exist in `flatcheck.py`'s argparse
   too, or the Python side fails on it.

Then run `scripts/crosscheck.ps1` (it dumps with `dotnet test -c Release --filter
DumpsFixtures`, which also builds the Release exe, then runs `crosscheck.py`) and paste its
summary. The scenario count is quoted in the header of `scripts/crosscheck.ps1`, in
`python-parity.md` and in "Versioning" above (43 on 2026-09-25); update all three.

### The VersionTests mode lists

`R001_EveryModePrintsTheBannerOnTheFirstLine` and `R001_EveryModePrintsTheCopyrightOnTheSecondLine`
each hold an explicit list of invocations, one `Assert.Equal` per mode; the copyright one adds a
`--smooth` refusal, `--help` and a missing file. A new mode is one more line in each, with the
arguments it needs to reach its report. On 2026-09-25 the lists cover structure, `--ratio`,
`--light`, `--expo`, `--histeq` and `--smooth` (fixed scale and `auto`). `--synthetic`,
`--lenses` and `--against` are not in them, although R-001 says every mode; for those three
only the mode 7 golden files carry the banner placeholder. Adding the three lines is a test
change and belongs to the next task that touches mode 7 or these tests.

### The README "Opțiuni" section

`README.md` is the user documentation and the only place the options are described for users
(rule 14; "Conventions" above). Its `## Opțiuni` section is a code block that mirrors
`WriteUsage`: the invocation forms, then one line per flag with the modes it applies to and its
default, then two paragraphs (flags on either side of the path; the banner on every run). Every
option appears there, in Romanian with diacritics. A new flag gets its line there, a mention in
the mode's own `### N.` section when it changes what that mode does, and a line under
`## Coduri de ieșire` when it adds or changes an exit code. Before any of that, the behaviour
is a row in `docs/requirements.md` (rule 1: "Console contract" for a flag, the mode's block for
a mode), and the change gets its `CHANGELOG.md` line (rule 9).

### InvariantGlobalization

`FlatCheck/FlatCheck.csproj` sets `<InvariantGlobalization>true</InvariantGlobalization>`, and
so does `FlatCheck.Core.csproj`. It is a runtime switch read from the executable's
`runtimeconfig.json`: the console runs without ICU culture data, its `CurrentCulture` behaves
as the invariant culture whatever the Windows locale, so `{x:F2}` prints `0.29` and never
`0,29`, and the golden files and `crosscheck.py` compare against that. Two consequences for new
code:

- Parsing an argument still names the culture explicitly (`TryNum` passes
  `CultureInfo.InvariantCulture`), and so should any new formatting or parsing in
  `FlatCheck.Core`: the same core runs inside the WPF exe, which does **not** set the switch
  (WPF cannot load XAML without culture data, ADR 0037) and pins numbers in code through
  `App.ForceInvariantNumbers` instead. The setting in `FlatCheck.Core.csproj` has no effect on
  a host that does not set its own. `CultureTests` in `FlatCheck.Wpf.Tests` runs the window
  under `ro-RO` to prove the saved report matches the console.
- `FlatCheck.Tests` sets the same switch (ADR 0043). The test host is its own executable and
  does not inherit the console's `runtimeconfig.json`; without the switch the golden tests
  formatted under the machine's culture, because the report code mostly formats with the
  ambient culture (`StructureReport` names the invariant one in a single place), and 63
  tests failed under `ro-RO`. `InvariantGlobalizationTests` (R-007) checks the console's
  `runtimeconfig.json` and the test host. A consequence: a test there cannot construct a named
  culture; one that needs `ro-RO` goes in `FlatCheck.Wpf.Tests`.
