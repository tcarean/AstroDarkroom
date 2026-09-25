# Process notes: conventions, versioning, licensing, build and run

> Moved unchanged from CLAUDE.md on 2026-09-25 (ADR 0001): this is the explanation behind the code,
> read on demand; CLAUDE.md keeps only the rules. **Covers:** the rules CLAUDE.md states in one line each, with their reasons; `Directory.Build.props`, `Version.cs`, `LicenseHeaderTests`, `VersionTests`, the publish commands. The Committing section predates rule 13 of CLAUDE.md, which now governs; the rest stands.
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
