# 0027. LICENSE is copied next to the application by its build, and with it into the publish folder

Date: 2026-10-02
Status: accepted 2026-10-02

## Context

The About dialog says "The full licence text is in the LICENSE file next to this program, and
at https://www.gnu.org/licenses/gpl-3.0.html" (R-072; ADR 0024 records why About carries the
GPLv3 §5(d) text). Nothing put the file there. `LICENSE` sat at the project root only, no item
of `src/ImageVisualCompare.Wpf/ImageVisualCompare.Wpf.csproj` copied it, and
`scripts/release.ps1` publishes what `dotnet publish` produces: the build folder and a release
folder held the program and no licence text, while the tests of the About text passed. The
adoption of the kit found it on 2026-10-02 (`docs/plans/2026-10-02-adopt-the-starter-kit.md`,
"What deviated"), and the owner asked the same day for it to be fixed.

## Decision

The Wpf project links the root `LICENSE` as a `None` item, `Link="LICENSE"`,
`CopyToOutputDirectory="PreserveNewest"`. The build copies it to the root of the application's
output, beside `ImageVisualCompare.Wpf.exe`, and `dotnet publish` carries it into the publish
folder. The About text is unchanged; R-074 states the behaviour.

## Alternatives rejected

- **Point About at the GNU URL only.** The dialog's promise would hold, but a program copied to
  another machine would carry its notice and not the licence it names; the file beside the
  program also works offline.
- **A second `LICENSE` inside the Wpf project folder.** Two copies of a text that must stay the
  verbatim FSF file (ADR 0024); the link keeps one.
- **Copy it in `scripts/release.ps1`.** The script is the kit's and is not edited in a project
  (the kit's rule "Shared folders"), and a build made without a release would still lack the
  file.

## Consequences

- The application's build folder and the publish folder hold `LICENSE`, byte-identical with the
  root's (35 149 bytes on 2026-10-02); so does the test project's output, through the project
  reference.
- Guards: `LicenseHeaderTests` (`LICENSE` beside the application assembly the tests load,
  identical with the root's). The publish folder was checked by hand on 2026-10-02; a release
  checks it again.

Source: src/ImageVisualCompare.Wpf/Version.cs (`VersionInfo.About`);
src/ImageVisualCompare.Wpf/ImageVisualCompare.Wpf.csproj; docs/requirements.md (R-072, R-074);
docs/plans/2026-10-02-adopt-the-starter-kit.md ("What deviated").
