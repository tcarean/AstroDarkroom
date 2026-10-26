# 0050. The README is in English

Date: 2026-10-26
Status: accepted 2026-10-26
Supersedes: 0048 in part (README.md Romanian only)

## Context

ADR 0048 (2026-10-25) made the interface speak English beside Romanian, English when `--lang` is
not given, and kept `README.md`, the user documentation, in Romanian only, documenting `--lang`:
an English copy beside the Romanian one would have been a second copy of the usage, the drift the
rule "User documentation lives in `README.md`, and only there" exists against. Since then the
repository got an English front page, which describes this tool and sends readers to its folder
(AstroDarkroom's ADR 0008), and the launcher's README went English (AstroDarkroom's ADR 0009). On
2026-10-26 the owner asked for this README to be rewritten in English and checked against the
current behaviour ("Vreau sa rescrii README.md in limba engleza si sa te asiguri ca este la zi si
este relevant").

## Decision

`README.md` is in English, and only in English: still one user documentation, in the language
the interface speaks by default. It quotes the window's English labels and the English report;
the Romanian ones are what `--lang ro` shows, and are not documented apart. The rewrite checked
every statement against the code and the requirements, and keeps to what a user needs: what the
tool needs, how to get it, the window, the console, how the background is judged, building, and
for developers only pointers to `CLAUDE.md` and `docs/`, the gate and the release, no longer the
history of the kit's adoption. `THIRD-PARTY-NOTICES.md` stays Romanian, with diacritics, and the
README says so beside its link. The rule "Languages" in `CLAUDE.md` says the same.

## Alternatives rejected

- **Keeping it in Romanian, ADR 0048's choice.** The interface starts in English and the front
  page is English; a reader who does not read Romanian would meet a Romanian page at the first
  link.
- **One README per language.** Two texts to keep in step, the drift ADR 0048 refused.
- **Translating the old text as it was.** It kept a process section that repeated `CLAUDE.md`
  and the kit with the history of 2026-10-02, and a few statements the code had left behind: the
  window saves `Report.txt` in English, the reader takes JPEG too, the Veil spans 10.9 degrees of
  latitude, not 10, a saved FITS keeps a FITS input's own row order (only a PNG, TIFF or JPEG
  source is written `BOTTOM-UP`), and the test command ran the validation cases too, minutes long
  on the reference images, which the gate's filter leaves out.

## Consequences

- A behaviour change updates the English README; its section names, "The window", "The console"
  and "Building", are what `docs/skills.md` (steps 8 of `/new-feature` and 7 of `/release`) and
  `docs/design/extending.md` point at, and `docs/requirements.md` names the README as English.
- The repository's front page says which tools' READMEs are in Romanian.
- The README quotes the reference image's report (the verdict and the first section of
  `de_printat.png` with the defaults); a change of that output changes the quote with it.
