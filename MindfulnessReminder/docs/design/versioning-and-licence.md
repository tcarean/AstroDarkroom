# Versioning and licence

> Moved unchanged on 2026-10-09 (ADR 0001) from `CLAUDE.md`, "Versioning" and "Licensing". Where a
> paragraph became a decision record, its number is given after it. **Covers:**
> `Directory.Build.props`, `src/MindfulnessReminder.Core/Version.cs`,
> `src/MindfulnessReminder.Core/LicenseInfo.cs`,
> `src/MindfulnessReminder.App/MindfulnessReminder.App.csproj`,
> `tests/MindfulnessReminder.Tests/VersionTests.cs`,
> `tests/MindfulnessReminder.Tests/LicenseHeaderTests.cs`, `LICENSE`. A change to the code this
> describes means this note is updated in the same task, or deleted with the decision it explains.

## Versioning

`<Version>` in `Directory.Build.props` is the single source of truth. `Version.cs` does
**not** hardcode it — `VersionInfo.Value` reads `AssemblyInformationalVersion` back out of
the assembly and trims any `+sourcerevision` suffix. `No_project_overrides_the_shared_version`
fails if a `<Version>` appears in any `.csproj`.

See ADR 0011.

**The user bumps the version manually**, per feature or fix, choosing the increment
themselves. **Never bump it on your own initiative** — not as part of a change, not "while
you're in there". If a change seems to warrant a bump, say so and let them decide.

Since 2026-10-09 this is the kit's rule "Version and year are the owner's" (the kit's ADR 0029).

Bumping breaks nothing: there are no golden files here, and `VersionTests` asserts the shape
of the version and the plumbing behind it, never a particular number.

## Licensing

GPLv3-or-later. Four things follow, all pinned by `LicenseHeaderTests`:

- **Every `.cs` and `.xaml` file starts with the notice** — SPDX line, copyright, all three
  standard paragraphs. `//` for C#, one `<!-- -->` block for XAML.
- **About displays the copyright and the warranty disclaimer** (GPLv3 §5(d)). It cannot go on
  the reminder window without defeating the point of the reminder.
- **One source of truth for the copyright string**: `<Copyright>` in `Directory.Build.props`,
  read back through `AssemblyCopyrightAttribute` by `LicenseInfo.Copyright`.
- **`LICENSE` ships with the build**, so About's "next to the executable" stays true after a
  `dotnet publish`.

See ADR 0009 for the notice and ADR 0010 for the other three.

The year and the author are the user's call, exactly like the version. Don't roll the year
forward on your own initiative.

Since 2026-10-09 this is the kit's rule "Version and year are the owner's".
