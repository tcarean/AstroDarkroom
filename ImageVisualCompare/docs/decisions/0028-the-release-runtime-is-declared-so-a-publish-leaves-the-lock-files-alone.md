# 0028. The runtime the release publishes for is declared in RuntimeIdentifiers, so a publish leaves the lock files alone

Date: 2026-10-05
Status: accepted 2026-10-05

## Context

The release entry of `kit.json` publishes the application with `-r win-x64 --self-contained`
(ADR 0002), and every project restores with a lock file (ADR 0003). A restore without a runtime
writes no runtime section into `packages.lock.json`, and `dotnet publish -r win-x64` restores with
that runtime: measured on 2026-10-02 and again on 2026-10-05, it adds `"net9.0/win-x64": {}` to
Core's lock file and `"net9.0-windows7.0/win-x64": {}` to the application's. `scripts/release.ps1`
publishes after it edits `CHANGELOG.md` and `Directory.Build.props`, and the `/release` skill's
step 6 stops when anything else changed, so the first release would have stopped on the two lock
files; in CI's locked mode the publish would fail instead. AstroPhotoAnalyzer and FlatCheck
publish for the same runtime with lock files of their own. The owner approved the fix for the
three on 2026-10-05 ("adaugă RuntimeIdentifiers").

## Decision

`Directory.Build.props` declares `<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>`, the runtime
of the release's publish. Every restore then writes the `win-x64` section into the lock file of
each project, committed with it, and a publish for `win-x64` finds it already there. The same
declaration went into AstroPhotoAnalyzer (its ADR 0030) and FlatCheck (its ADR 0056) the same day.

## Alternatives rejected

- **`RuntimeIdentifier`, singular.** It makes every build runtime-specific and moves the build
  output under `bin/Release/<framework>/win-x64/`, which nothing here needs.
- **Commit the changed lock files after each release.** Every release would carry a change that
  has nothing to do with it, and CI would still refuse the publish in locked mode until then.
- **Restore with `-r win-x64` in `scripts/release.ps1`.** The script is the kit's and is not
  edited in a project (the kit's rule "Shared folders"), and the declaration also covers a
  publish made by hand.

## Consequences

- Measured on 2026-10-05: after a forced restore, which wrote the section into the three lock
  files, a forced publish with the release's arguments and a forced build left every lock file
  unchanged.
- A publish target for another runtime needs that runtime added here, or the release stops on
  the lock files again.
- A lock file put back from git while NuGet's cache still holds a restore for the runtime is not
  rewritten by a plain restore, which takes itself to be up to date; `dotnet restore --force`
  rewrites it.
- Guards: none by test. A missing runtime shows when a release stops at the `/release` skill's
  step 6.

Source: kit.json (release.publish); Directory.Build.props;
docs/plans/2026-10-05-declare-the-win-x64-runtime.md.
