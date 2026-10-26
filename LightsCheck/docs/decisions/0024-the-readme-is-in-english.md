# 0024. The README is in English

Date: 2026-10-26
Status: accepted 2026-10-26
Supersedes: 0005 in part (README.md in Romanian)

## Context

ADR 0005 put `README.md` in the Romanian zone, with everything the user sees, since the user was
the owner. ADR 0022 then made every text of the console and the window English by default and
Romanian with `--lang ro`, and left the README Romanian (the plan of 2026-10-25), so the README
quoted output that a run without `--lang` no longer prints. On 2026-10-26 the repository got an
English front page whose tool sections send a reader to each tool's `README.md` as its user guide
(AstroDarkroom's ADR 0008), and AstroDarkroom's own README went English the same day (its ADR
0009). The same day the owner asked for this README to be rewritten in English and brought up to
date.

Measured for the text on 2026-10-26, with the console built from `main` at eb29c6e, on the Veil
session (`Veil/2026-10-06-3h`): the inventory and `--frame 4` print what the README quotes, and
the inventory names frame 75's 133.2 s exposure where the Romanian example said every frame had
120.2 s; the whole series took 123 s on the default 4 threads, against the 90 s the README gave
from the run of 2026-10-09, and 215 s while other processes held the CPU.

## Decision

`README.md` is in English, like everything else in the project except the interface's Romanian
wordings. It quotes the English output, the default, and names `--lang ro` for the Romanian one;
a Romanian word appears in it only as a name the program accepts or wrote (`--est stanga|dreapta`,
`--nord sus|jos`, the file names of version 1.0.0). Its examples are runs on the real sessions of
`docs/validation.md`.

## Alternatives rejected

- **Keeping it in Romanian.** The front page is English and sends readers here, and the README
  would go on quoting output that a run without `--lang` does not print.
- **One README per language.** Two texts of some 300 lines to keep in step, for an interface
  that already speaks both; the owner asked for one, in English.

## Consequences

- The rule "Languages" in `CLAUDE.md` names the README as English; `docs/requirements.md` calls
  it the English user documentation; `docs/skills.md` (step 8 of `/new-feature`) asks for a
  README in English that quotes the English output.
- `THIRD-PARTY-NOTICES.md` stays Romanian, as ADR 0005 put it: the request named the README
  alone. The README links it and says it is in Romanian.
- The repository's front page said every tool's README is in Romanian; it now says LightsCheck's
  is in English, since a task that changes a fact the front page restates edits it in the same
  task (AstroDarkroom's ADR 0008).
