# Design notes

One note per area whose code does not explain itself. Each says what the code does, how its
parts fit, and, where it was found out the hard way, why the obvious simpler version is wrong.
`CLAUDE.md` points here under "Read before touching"; nothing in this folder is loaded into a
session unless the task needs it.

Until 2026-10-02 these notes were sections of `CLAUDE.md` ("Structure", "Invariants", "No
imaging dependency", "Memory, measured", "Testing", "Versioning", "Licensing"); they were moved
here unchanged (ADR 0001), with a pointer to the decision record after each paragraph that
became one. The behaviour the notes describe is also stated, with IDs, in `../requirements.md`,
and for the user, in Romanian, in the project's `README.md`. Where a row, a note and an ADR were
written from the same paragraph, the three still say it in their own words; cutting the notes
down to what neither a row nor an ADR says is a task of its own.

**Every note is maintained** (the kit's rule "Design notes"): it says above its first section,
in a **Covers:** sentence, which files it describes, and a change to those files means the note
is updated in the same task, or deleted with the decision it explains. `scripts/check-design.ps1`
checks this index and the Covers paths at every commit; `scripts/check-design.ps1 -Since main`,
run in a task's worktree, lists the notes a branch touched. A Covers sentence names its files as
paths in backticks, from the project root (`src/...`, `tests/...`), and ends at its first full
stop.

| Area | Note | Read before editing |
| --- | --- | --- |
| Every file, one line each, and why the controls do no arithmetic of their own | `code-map.md` | a file you have not opened before |
| The comparison surface: one shared transform, physical and device-independent pixels, `Canvas` layers sized in pixels, the clip on the layer, the shared opaque backdrop, the floating scrollbars | `rendering.md` | anything in `Controls/`, `Viewport.cs`, `RenderPlan.cs`, `DividerModel.cs`; ADR 0010 to ADR 0013 and ADR 0019 first |
| The decoder seam and why WIC, the memory a pair costs and how it is given back, padding a mismatched pair | `imaging.md` | anything in `Imaging/`, `ImagePairValidator.cs`, a new package; ADR 0008 and ADR 0014 to ADR 0016 first |
| The culture trap and the dark chrome: no `InvariantGlobalization`, no `MessageBox`, the DWM caption | `shell.md` | `Directory.Build.props`, a new `Window`, `Theme.xaml`; ADR 0017 and ADR 0018 first |
| The version and the licence: one source for each, what `VersionTests` and `LicenseHeaderTests` pin | `versioning-and-licence.md` | `Version.cs`, `Directory.Build.props`, `VersionTests`, `LicenseHeaderTests`; ADR 0004, ADR 0023 and ADR 0024 first |
| What is tested, how the fixtures are made, what is not tested and why the application has to be launched | `testing.md` | a new fixture, any change to the WPF layer; ADR 0026 first |

The decisions themselves, with the alternatives rejected, are in `../decisions/`. The behaviour,
with IDs, is in `../requirements.md`. The figures measured on the real pair are in
`../validation.md`.
