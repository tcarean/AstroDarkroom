# Validation

The project's memory of "how well it works, measured on what". Every case lives in
`validation.json` (read by `ValidationTests`, in the gate: the fixture is in the repo, ADR 0005)
and is explained here. A figure changes only after a re-measurement, with a dated line under
History saying old value, new value and why.

## Cases

None yet. V-001, the keys the tool selects from the header of a real Siril plate solve, comes
with the fixture (ADR 0005).

## How to re-measure

```bash
dotnet test CopyWcs.sln -c Release --filter "FullyQualifiedName~ValidationTests"
```

Or `/validate` in Claude Code, which prints expected versus measured for every case and stops on
drift instead of editing the figures.

## History

(none yet)
