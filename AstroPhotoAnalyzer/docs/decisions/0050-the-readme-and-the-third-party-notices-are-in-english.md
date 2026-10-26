# 0050. The README and the third-party notices are in English

Date: 2026-10-26
Status: accepted 2026-10-26
Supersedes: 0005 in part (THIRD-PARTY-NOTICES.md in Romanian); 0048 in part (README.md Romanian only)

## Context

ADR 0005 (2026-10-02) put everything the user sees in Romanian, `README.md` and
`THIRD-PARTY-NOTICES.md` included. ADR 0048 (2026-10-25) made the interface speak English beside
Romanian, English when `--lang` is not given, and kept `README.md`, the user documentation, in
Romanian only, documenting `--lang`: an English copy beside the Romanian one would have been a
second copy of the usage, the drift the rule "User documentation lives in `README.md`, and only
there" exists against. Since then the repository got an English front page, which describes this
tool and sends readers to its folder (AstroDarkroom's ADR 0008), and the launcher's README went
English (AstroDarkroom's ADR 0009). On 2026-10-26 the owner asked for this README to be rewritten
in English and checked against the current behaviour ("Vreau sa rescrii README.md in limba
engleza si sa te asiguri ca este la zi si este relevant"), and, once it was, for the notices to
follow and the rule to change ("fa si THIRD-PARTY-NOTICES.md in engleza si schimba regula").

## Decision

`README.md` and `THIRD-PARTY-NOTICES.md` are in English, and only in English: still one user
documentation, in the language the interface speaks by default. The README quotes the window's
English labels and the English report; the Romanian ones are what `--lang ro` shows, and are not
documented apart. Both files were checked against the code while they were rewritten. The README
keeps to what a user needs: what the tool needs, how to get it, the window, the console, how the
background is judged, building, and for developers only pointers to `CLAUDE.md` and `docs/`, the
gate and the release, no longer the history of the kit's adoption. The notices keep their
structure and their quoted licence texts word for word. The rule "Languages" in `CLAUDE.md` says
both files are English.

## Alternatives rejected

- **Keeping them in Romanian, ADR 0005's and ADR 0048's choice.** The interface starts in English
  and the front page is English; a reader who does not read Romanian would meet a Romanian page
  at the first link, and at the README's link to the notices.
- **One README per language.** Two texts to keep in step, the drift ADR 0048 refused.
- **Translating the old texts as they were.** The README kept a process section that repeated
  `CLAUDE.md` and the kit with the history of 2026-10-02, and a few statements the code had left
  behind: the window saves `Report.txt` in English, the reader takes JPEG too, the Veil spans 10.9
  degrees of latitude, not 10, a saved FITS keeps a FITS input's own row order (only a PNG, TIFF
  or JPEG source is written `BOTTOM-UP`), and the test command ran the validation cases too,
  minutes long on the reference images, which the gate's filter leaves out. The notices said the
  dust is measured as a fraction of the frame without the catalogue (since ADR 0026 the verdict
  does not depend on it; it gives the names), that ImageSharp decodes TIFF and PNG (JPEG too) and
  encodes the JPEGs of the compression table (the 8-bit TIFF whose size it gives too), that its
  version is pinned in the `PackageReference` (in `Directory.Packages.props`, by central
  package management), and that Unicolour makes no colour conversion on the image (since ADR 0032
  it also carries the image's colours through the profile for the gamut figure, R-144); their
  table of test packages lacked `xunit.runner.visualstudio` (Apache-2.0, read in its package).

## Consequences

- A behaviour change updates the English README; its section names, "The window", "The console"
  and "Building", are what `docs/skills.md` (steps 8 of `/new-feature` and 7 of `/release`) and
  `docs/design/extending.md` point at, and `docs/requirements.md` names the README as English. A
  new package's notice is written in English.
- The repository's front page says which tools' READMEs are in Romanian.
- The README quotes the reference image's report (the verdict and the first section of
  `de_printat.png` with the defaults); a change of that output changes the quote with it.
- The About window's role texts (`ThirdParty.cs`), which named only the Dmax for Unicolour and
  only the JPEGs for ImageSharp's encoding, name the gamut figure and the 8-bit TIFF too, in both
  languages, like the README and the notices.
