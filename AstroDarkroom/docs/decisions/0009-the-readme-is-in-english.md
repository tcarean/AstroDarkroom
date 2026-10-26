# 0009. The README is in English

Date: 2026-10-26
Status: accepted 2026-10-26
Supersedes: 0003 in part (README.md in Romanian)

## Context

ADR 0003 put `README.md` in the Romanian zone, with everything the user sees, since the reader
was the owner. ADR 0006 then made the windows speak English or Romanian, English at a first
start. On 2026-10-26 the repository got an English front page (ADR 0008) whose download section
sends the reader to this README for the older versions, with a note that it is in Romanian. The
same day the owner asked to drop that note and to rewrite this README in English too.

## Decision

`README.md` is in English, like everything else in the project but the windows' Romanian
wordings. It quotes the English labels of the launcher (**Open**, **About**) and keeps its
sections, their names in English: "What you get", "Requirements", "Download" (the table of every
version's link and hash, ADR 0005), "How a release is made", "Building", "For developers".

## Alternatives rejected

- **Keeping it in Romanian.** The front page is English and sends readers here; one who does not
  read Romanian would meet a Romanian page at the first link.
- **One README per language.** Two texts to keep in step for one launcher whose window already
  speaks both.

## Consequences

The rule "Two interface languages, English everywhere else" in `CLAUDE.md` names the windows
alone as bilingual; for this file the kit's rule "Languages" now holds as written. The section
names in `docs/skills.md` (step 7 of `/release`), in `CHANGELOG.md` and in ADRs 0005 and 0007
follow the English headings. The five tools' READMEs stay in Romanian, and the front page says
so. The "Requirements" section names the Adobe programs as ADR 0008 measured them (Lightroom's
copy is not read), while the launcher's own text still names Lightroom (ADR 0007's note); that
correction is the owner's to decide. (Restated 2026-10-26: FlatCheck reads Lightroom's folders
since its ADR 0066; both texts name Lightroom and both are right, nothing left to correct.)
