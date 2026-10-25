# Validation

The project's memory of "how well it works, measured on what". Every case lives in
`validation.json` and is explained here. A number changes only after a re-measurement, with a
dated line below saying old value, new value and why.

The launcher has no measured figures: it computes nothing, it lists the apps of the suite and
starts them. Each component keeps its own `docs/validation.md`. The suite's one measurement, the
publish of 0.1.0 (file count, folder size, zip size, collisions), is in ADR 0002, not here: it
describes a build, not a behaviour of the launcher.

## Cases

| ID | Fixture | Measured on | What it guards |
| --- | --- | --- | --- |
| (none) | | | |

## How to re-measure

```bash
dotnet test AstroDarkroom.sln -c Release --filter "Category=Validation"
```

Or `/validate` in Claude Code, which prints expected versus measured for every case and stops on
drift instead of editing the figures. With no case, both have nothing to run.

## History

- 2026-10-25: started without a case; the kit's V-001 went with the sample code (ADR 0001).
