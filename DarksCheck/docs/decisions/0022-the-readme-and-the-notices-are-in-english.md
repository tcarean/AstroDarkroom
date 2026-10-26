# 0022. The README and the notices are in English

Date: 2026-10-26
Status: accepted 2026-10-26
Supersedes: 0005 in part (README.md and THIRD-PARTY-NOTICES.md in Romanian); 0021 in part (the README in Romanian only)

## Context

ADR 0005 put `README.md` and `THIRD-PARTY-NOTICES.md` in the Romanian zone, with everything the
user sees, since their reader was the owner. ADR 0021 then made the interface speak English or
Romanian, English when `--lang` is missing, and kept the README in Romanian only, documenting
`--lang`, as FlatCheck and AstroPhotoAnalyzer did the same day. On 2026-10-26 the repository got
an English front page that describes every tool and sends the reader to each tool's README
(AstroDarkroom's ADR 0008), and AstroDarkroom's own README went English (its ADR 0009). The same
day the owner asked for DarksCheck's README in English, brought up to date, and, asked in plan
mode, chose the notices in English too.

## Decision

`README.md` and `THIRD-PARTY-NOTICES.md` are in English, like every other file but the
interface's Romanian wordings. The README documents the English interface: its labels, its
verdict words and its examples, cut from English runs on the real sessions; it says that
`--lang ro` turns every text Romanian and names the Romanian tabs once. The rule "Languages" in
`CLAUDE.md` keeps Romanian only in the interface's pairs and in the tests and docs that quote
them.

## Alternatives rejected

- **Keeping the README in Romanian.** The interface speaks English unless asked, and the English
  front page sends readers here; one who does not read Romanian would meet a Romanian page at
  the first link.
- **The README in English, the notices in Romanian.** The README links the notices, and the
  About window names them; the same reader would meet Romanian one link further.
- **One README per language.** Two texts to keep in step, for programs that already speak both.

## Consequences

- `CLAUDE.md` (its Layout line and the rule "Languages"), the introduction of
  `docs/requirements.md` and step 8 of `/new-feature` in `docs/skills.md` name the README as
  English.
- A user-visible change updates the README in English: its rows, words and examples follow the
  English output (`Golden/en_*.txt`, an English run).
- The README's examples are cut from real runs (Smida, the 2026-10-10 library, Veil), not from
  the synthetic goldens, and its timings are dated; a change to those rows or to the speed makes
  them stale until they are taken again from a run.
- The repository's front page names the tools whose README is in English, and AstroDarkroom's
  ADR 0009, which said the tools' READMEs stay in Romanian, carries a dated note.
