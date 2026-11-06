# 0013. A count written before its noun follows the language's grammar: one in the singular, and in Romanian "de" from 20 on

Date: 2026-11-06
Status: accepted 2026-11-06

## Context

The copy's closing line read `Copied 1 keys into ...` and `Copiate 72 chei in ...`, while the
line R-016 added on 2026-11-06 (ADR 0011) names its count in the singular for one key and, in
Romanian, with "de" from 20 keys on. The review of that branch saw the two rules side by side in
one output (`Eliminate 24 de chei vechi ...` above `Copiate 72 chei in ...`), and the owner asked
the same day to fix the closing line ("copiate 75 chei"). Romanian puts "de" between a number and
its noun from 20 on, unless the number's last two digits are 01 to 19: `20 de chei`, `72 de
chei`, `100 de chei`, but `19 chei`, `101 chei`, `119 chei`. LightsCheck follows the same rule
(its ADR 0037).

## Decision

A count the user reads before its noun takes the singular for one (`Copied 1 key`, `Copiata 1
cheie`) and, in Romanian, the "de" that `Ui.RoCount` adds; a count in brackets after a heading
(`Keys that would be copied (72):`) stays as it is, since the grammar does not reach into the
brackets.

## Alternatives rejected

- **Keep `Copiate 72 chei`.** Two rules in one output, the newer line right and the older wrong.
- **Put every count in brackets** (`Copied keys (72) into ...`). It changes a sentence the owner
  reads after every copy, to avoid a rule one small helper keeps.

## Consequences

- `Ui.RoCount` serves the copy's line and R-016's copy line alike; a new line with a count before
  its noun uses it too.
- R-012 quotes the forms; `R012_Run_TheCopysCount_FollowsEachLanguagesGrammar` pins 1, 19, 20, 72,
  101 and 120 keys in both languages, as R-016's tests pin the drop line.
- The diagnostic log is not the user's text and keeps its one English form (`copied 1 keys`).
