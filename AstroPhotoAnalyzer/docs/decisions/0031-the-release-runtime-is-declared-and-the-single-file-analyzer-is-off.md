# 0031. The release's runtime is declared and the single-file analyzer is off, so a publish leaves the lock files alone

Date: 2026-10-05
Status: accepted 2026-10-05

## Context

The release entry of `kit.json` publishes the console with `-r win-x64 --self-contained
-p:PublishSingleFile=true` and the window with `-r win-x64 --self-contained`, and every project
restores with a lock file (ADR 0003). `scripts/release.ps1` publishes after it edits
`CHANGELOG.md` and `Directory.Build.props`, and the `/release` skill's step 6 stops when anything
else changed. Measured on 2026-10-05, the two publishes changed the lock files of Core, the
console and the window, for two reasons:

- A restore without a runtime writes no runtime section, and `dotnet publish -r win-x64`
  restores with one: it adds `"net8.0/win-x64"` to Core's and the console's lock files and
  `"net8.0-windows7.0/win-x64"` to the window's.
- `PublishSingleFile` turns on the single-file analyzer, whose package
  `Microsoft.NET.ILLink.Tasks` the console's publish then adds to the console's lock file and,
  through the global property, to Core's, until the window's publish rewrites Core's again. Its
  version is the one the installed SDK carries (`8.0.31` in SDK 9.0.318's
  `Microsoft.NETCoreSdk.BundledVersions.props`), so the entry would also change with the SDK.

Declaring `<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>` alone, as ImageVisualCompare had
(its ADR 0028), left the analyzer's package to change the console's lock file, and on every
project it also wrote 844 lines into each test project's lock file: 76 runtime-specific packages
(`runtime.any.*`, `runtime.win.*` and the like) of the System.* 4.3.0 libraries the test packages
bring in, for projects that are never published. Publishing with
`-p:RestorePackagesWithLockFile=false` is refused while lock files exist (NU1005). Put to the
owner with the measurements, the owner chose the runtime declared without the test projects in
the three projects that publish for `win-x64`, ImageVisualCompare included ("A, cu condiția și la
IVC"), and the analyzer turned off in the two that publish a single file.

## Decision

`Directory.Build.props` declares `<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>` for every
project whose name does not end in `.Tests` (Core, the console, the window), and
`<EnableSingleFileAnalyzer>false</EnableSingleFileAnalyzer>` for all. Every restore writes the
`win-x64` section into the three lock files, committed with them; the test projects' lock files
stay as they were; a publish with the release's arguments finds the section already there and
asks for no analyzer package. ImageVisualCompare (its ADR 0028) and FlatCheck (its ADR 0056)
declare the same runtime the same day; FlatCheck turns the analyzer off too, ImageVisualCompare
publishes no single file.

## Alternatives rejected

- **The runtime alone, as approved first.** The console's publish still adds the analyzer's
  package, and the release still stops at step 6.
- **Keep the analyzer and commit its package.** The package would enter every restore, each SDK
  update would rewrite the lock files at the next build, and the analyzer would run in the gate,
  where a warning is an error.
- **Commit the changed lock files after each release.** Every release would carry a change that
  has nothing to do with it, and CI would refuse the publish in locked mode until then.
- **Leave the lock files to the first release.** It stops at step 6 and the question comes back
  then, mid-release.

## Consequences

- Measured on 2026-10-05: one forced restore added an empty `win-x64` section to the lock files
  of Core, the console and the window and left the two test projects' alone; a forced publish of
  both release targets and a forced build then left every lock file unchanged.
- The single-file analyzer no longer runs. It runs only in a single-file publish, so only the
  release ran it, and with it on the console's publish raised no warning on 2026-10-05;
  `release.ps1` publishes without `-warnaserror`, so a warning would only have been printed. The
  console exe published with the change runs (`--help`), and differs from one published without
  it by 953 bytes: the analyzer's package is no longer listed in the deps.json bundled in it.
- A publish target for another runtime needs that runtime added to `RuntimeIdentifiers`, or the
  release stops on the lock files again.
- The condition goes by the name: a project that is published must not end in `.Tests`.
- Guards: none by test. A missing runtime shows when a release stops at the `/release` skill's
  step 6.

Source: kit.json (release.publish); Directory.Build.props;
../ImageVisualCompare/docs/plans/2026-10-05-declare-the-win-x64-runtime.md.
