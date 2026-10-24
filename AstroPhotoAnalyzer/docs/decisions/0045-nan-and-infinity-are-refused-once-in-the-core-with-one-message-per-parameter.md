# 0045. NaN and infinity are refused once, in the core's `Validate`, with one message per parameter

Date: 2026-10-24
Status: accepted 2026-10-24

## Context

The documentation backfill (`docs/plans/2026-10-22-document-every-type-and-method-and-translate-the-comments.md`,
areas 4 and 5) probed what the number parameters accept. `Options.TryNum` and
`ParameterFields.ParseNumber` read with `NumberStyles.Float` in the invariant culture, which
accepts the words "NaN", "Infinity" and "-Infinity"; every range check the two applications
make (`v <= 0`, `v > hi`) is false on NaN, so NaN passed Dmax, the viewing distance, the widths,
the passepartout and the frame in both, and infinity passed the widths and the aspect in both
and the console's distance, passepartout and frame. The aspect is built from two sides, so
"1:Infinity" arrived as a ratio of 0, "Infinity:1" as infinity and "Infinity:Infinity" as NaN,
past the checks on the sides. The console then ran the analysis and printed `n/a` for the
figures the value entered (exit 2, no error); the window built the parameters and ran. Only
the target background refused all three, with the core's message (R-023, ADR 0040).

The owner accepted on 2026-10-24 the recommendation of area 5: one check in the core.

## Decision

`Parameters.Validate()`, which both applications already call before any reading, refuses NaN
and infinity in each number parameter with one Romanian message per parameter, in the
parameters' order, after the target background's check and before the rule between the
passepartout and the frame: `Dmax`, `distanta de privire`, `latimea de print` (each width),
`raportul de aspect`, `passepartout-ul`, `rama`. The value is quoted as the target
background's message quotes it (`NaN, care nu e un numar`, `infinit`, `minus infinit`), from one
helper, so the two messages spell them alike. The console prints the message behind `EROARE: `
and exits 1; the window shows it in the error card; the applications keep their own syntax and
range checks and messages (R-024).

The aspect is the one parameter the user types as two sides, not as the number the core sees:
"1:Infinity" would reach the core as 0, "Infinity:Infinity" as NaN, and a message quoting that
ratio would name a value nowhere on the command line (the code review of this branch,
2026-10-24). So the two aspect parsers (`Options.TryAspect`, `ParameterFields.AspectOf`) refuse
a side that is not a finite positive number with their own syntax message, which quotes the
text typed, and the core's check that the ratio is finite and positive stays as the backstop
for a `Parameters` built in code.

## Alternatives rejected

- **Refuse in the parsers (`TryNum`, `ParseNumber`).** Two places, two messages, and the
  window's `SizesOf` and `AspectOf` parse on their own: five sites in all, and a sixth for every
  parameter added later. The core's check is one line per parameter and covers a `Parameters`
  built in code too. The aspect's sides are the exception above, because there the parser is
  the only place that still has the text the user typed.
- **Let the core's aspect message quote the ratio.** The first version of this branch did;
  `--aspect 1:Infinity` then read `a primit 0`, a value the user never typed.
- **Make the parsers reject the words (`NumberStyles.Float` without the symbols).** Hides the
  reason: the user who typed "Infinity" would read "nu e un numar" and not know that the word
  reads but is refused.
- **Refuse in `AnalysisRun.Execute`.** Later than the applications' own checks, after the image
  is read; `Validate` already stands before the reading.

## Consequences

- R-024 says what the syntax lets through and the core refuses, with the messages; R-025 says
  where in `Validate` the rule between parameters now comes. Guards:
  `R024_NaNAndInfinityAreRefusedByValidateWithOneMessagePerParameter` (the core),
  `R024_TheConsoleRefusesNaNAndInfinityBeforeReading` (the console, exit 1 on an empty file),
  `R024_NaNAndInfinityInAFieldGiveTheCoresMessage` (the window).
- No report or `--help` text changes; the goldens stand. `Parameters.cs` moved, so `/validate`
  was re-run (the plan's "What deviated").
- The comments of `Options.TryNum`, `TryAspect`, `ParameterFields.ParseNumber`,
  `OptionalNumber`, `SizesOf` and `AspectOf` say where the refusal is; `console.md` and
  `extending.md` say the same for a maintainer.
