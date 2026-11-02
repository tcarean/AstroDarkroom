# 0054. The Split tab builds temp sessions from a copy of the flats and compares their masters with the whole session's, against one reference profile

Date: 2026-09-28
Status: accepted 2026-09-28; the reference profile as the whole session's nearest profile superseded by 0090

## Context

Flats shot in rotated groups (15 + 15 at 180°, 4 x 10 at 90°) cancel gradients in the mean, and
the per-orientation masters are where the cancelled part can be seen (`flat-sources.md` measured
them by hand three times). Building the sub-sessions by hand means copying folders, running the
Siril script on each and running the console on each master. Measured on 2026-09-28 on the
40-frame rotated-in-place session: one 10-flat part copies in 5 s (673 MB) and runs through
`OSC_Preprocessing_WithoutDark.ssf` in 38 s, leaving a 4.1 GB `process/`; part 1 reads
7.58 / 6.95 / 7.79 / 5.48 / 7.16 % on the five tests against 1.18 / 2.03 / 2.86 / 0.58 / 2.65 %
for the whole, and its nearest lens profile is a different one (Adobe, +0.07 stops) than the
whole's (lensfun 7D2, -0.08 stops). `flats/` holds a `readme.txt` beside the raws in four of
the fourteen real sessions; `lights/` holds 2 frames in every one, but an imaging session may
hold a hundred. The PNG writer is hand-rolled (ADR 0008) and draws no text.

## Decision

The window gets a `Split` tab (R-091 to R-093). It divides the flat frames (raw or FITS files
of `flats/`, ordinal name order, consecutive blocks) into 2 or 4 temp sessions under
`%TEMP%\FlatCheck\split-<date-time>\parte-<i>\`, each with every file of each folder the
script enters other than `flats/`, `lights/` and the session's output folders `process/` and
`masters/` (which are never copied: a script with `cd process` would otherwise bring the
original's 17 GB and its master into every part), so `biases/` and `darks/`, at most the first 2
frames of `lights/` by name, and its share of the flats; runs the chosen script (the
problem card's list, `OSC_Preprocessing_WithoutDark.ssf` preselected) in each, one after another,
through the same `SirilScriptRun` and `SirilRunViewModel` as the problem card; then compares the
part masters with the whole session's master in the core (`SplitRun`, one `StructureRun.Analyze`
per master). The original session is only read. The owner's decisions, recorded so nobody
re-opens them:

- **At most 2 lights, whichever come first by name.** Enough for the script to run to the end
  (`register` and `stack` need a sequence); a hundred lights copied four times and registered
  four times would be the slow part of the run for no flat.
- **The reference profile is the whole session's nearest profile, the same for every column.**
  "Nearest" is chosen per master and differed between part 1 and the whole on the first real
  run; a table with a different reference per column compares nothing. Every profile the
  Structure tab lists gets a row (the owner asked for all of them on 2026-09-28), with each
  column's difference from it and, in the profile column, its own depth; the reference row is
  marked and is the one the verdict row uses.
- **A disk estimate before the run, no time estimate** (owner's decision, 2026-09-28): the
  copies are exact from the file sizes; Siril's `process/` comes from `SplitCost`, a
  bytes-per-pixel model measured that day (converted 2 B/px, calibrated or stacked 4 B/px, a
  calibrated or registered light and `result.fit` 12 B/px), 4.6 GB modelled against 4.35 GB
  measured on the quarter session; the free space of the temp drive is read and shown, in
  Explorer's units (1 GB = 1024^3) so the figure matches the one the user sees there. The
  measured sentence that stood on the screen before is documentation now (README).
- **Every value in the table carries its verdict**, and the last row is each column's
  verdict: the worse of its five tests and its depth against the reference profile. The
  review of 2026-09-28 found the first cut used the run verdict, whose depth part is against
  the column's own nearest profile: a part could read `BUN` under a `REFA` cell against the
  reference. One profile for both rows, in `SplitColumn.Verdict`.
- **The mosaic is labelled with a built-in 5x7 bitmap font** (`BitmapFont`: digits, `A`-`Z`,
  a few signs), no package (rule 5). The title row carries the version, the date and time of the
  run and the ends of the colour scale; the report file carries the banner and the same date.
- **One automatic colour scale shared by every map** in the file (the 1st-99th percentile over
  all of them, R-006's rule applied to the union), so the parts and the whole are comparable by
  eye; the screen offers the fixed caps as everywhere else.
- **The temp folder is deleted after the analysis and on `Opreste`, kept after a failure**
  (non-zero exit, or a script that leaves no master), with its path and the log on screen: there
  is nothing to diagnose without it.
- **The part masters are not kept.** The whole session's master is the one used; the report in
  the session root holds every number the parts produced.
- **`Rezultat split.txt` and `flatcheck-split.png`** in the session root, overwritten without
  asking, like `Rezultat testare.txt`.
- **No console mode.** The core takes file paths, so `--split A B C` stays cheap; nothing asked
  for it.
- **The version goes to 2.3.0** in this task, at the owner's request (rule 17).

## Alternatives rejected

- **Copying `lights/` whole.** Asked for at first; dropped for the cost above once measured.
- **A trimmed script that stops after the flat master.** ADR 0035 runs the user's own script
  unchanged; two lights make the whole script succeed.
- **A fixed copy list (`biases/`, `lights/`, `flats/`).** The first cut; the review found
  `OSC_Preprocessing.ssf` `cd darks`, which the pre-check would pass on the original and
  Siril would fail on part 1 after copying gigabytes. What is copied is what the script
  enters.
- **A mosaic without labels, order stated in the report.** A PNG opened alone would not say
  which map is which; the font is 80 lines of glyphs.
- **Each map on its own automatic scale in the file.** Full red would mean 8 % on one tile and
  3 % on the next, and nothing in the picture would say so (ADR 0036's finding again).
- **The nearest profile per column.** Compares nothing; see the measurement.
- **Keeping the temp folder always.** 16 GB per run on the system drive for a table already
  saved beside the session.
- **Composing the mosaic in the window with WPF text.** The file would be the window's alone;
  the core writes every file both applications could produce (ADR 0011).

## Consequences

- `StructureRun` gains `Analyze` (load, analyze, compare, no report), which `Execute` and
  `SplitRun` share; `PreprocessViewModel` becomes a thin subclass of `ScriptChoiceViewModel`,
  which the tab reuses.
- A 4-way split needs ~17 GB free on the drive `%TEMP%` is on and ~3 minutes; the README says so.
- The part figures exist only during the run: `validation.md` records the real runs as dated
  measurements, and no `validation.json` case can re-measure them without Siril.
- Guards: `SplitRunTests` (columns, verdicts, reference, report text, mosaic geometry and shared
  scale), `BitmapFontTests`, `SplitTabTests` (count and division, blockers, the copy list, the
  fake Siril per part, the original session untouched, the temp kept on failure, `Opreste`),
  the smoke render of the tab.
