# Validation

The project's memory of "how well it works, measured on what". Every case lives in
`validation.json` (read by `ValidationTests`) and is explained here. A number changes only after
a re-measurement, with a dated line below saying old value, new value and why.

## Cases

| ID | Fixture | Measured on | What it guards |
| --- | --- | --- | --- |
| V-001 | `numbers-small.txt` | 2026-09-24 | R-001 and R-002 on a hand-checked sequence: median 5.5, robust sigma 3.7065 (tolerance 0.0001). |

## How to re-measure

```bash
dotnet test Starter.sln -c Release --filter "FullyQualifiedName~ValidationTests"
```

Or `/validate` in Claude Code, which prints expected versus measured for every case and stops on
drift instead of editing the figures.

## History

- 2026-09-24: V-001 recorded from the kit's sample code. Hand calculation, not an external reference.
