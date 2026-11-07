# 0041. The default arrows are a Star Adventurer GTi's with SynScan Pro: right east, down north

Date: 2026-11-07
Status: accepted 2026-11-07
Supersedes: 0014 in part (the default north arrow)

## Context

ADR 0014 made the SynScan arrows a setting and took its default from the owner's reading of the
Veil session: `→` east, `↑` north. Nothing had tested either key: the SynScan manuals say only
that left and right turn RA and up and down DEC, and the notes DitherCalculator keeps of
2026-10-12 found no source for `→` east (a forum default and an EQMOD user say right is west).
On 2026-11-07 the owner tested the pair LightsCheck is used with, the SynScan Pro app driving a
Star Adventurer GTi, on a target west of the meridian: `→` moves the mount east, `←` west, `↓`
north, `↑` south. East was right, north the other way round. The sense letters of a cell (E or W, N or S) come from the plate
solve and were never in question (ADR 0013, and the four solved frames of `docs/validation.md`,
History, 2026-10-12); only the arrow written beside them follows the setting.

## Decision

The default keys are the tested pair's: `ArrowKeys.Default` is right east and down north, so
left is west and up south. They stay a setting for another mount or app, `--east` and `--north`
(`--est`, `--nord`) in the console and the EAST and NORTH selectors in the window. Where the
program shows the defaults it says whose they are: the default sentence of the window's two
tips names the pair ("Default: → east, as on a Star Adventurer GTi with the SynScan Pro app."),
and `--help` gains the line "The defaults are those of a Star Adventurer GTi with the SynScan
Pro app." The help takes its default words from `ArrowKeys.Default`, as the tips take their
symbols, so a later default changes every place that shows it. The texts are the ones the owner
approved with the plan of 2026-11-07.

## Alternatives rejected

- **Keep `↑` north and say in the README that the owner's app reverses it.** The default
  exists for the owner's setup; one wrong for it would make every session of theirs need the
  setting.
- **A list of presets, one per mount and app.** One pair is tested; a list would set guesses
  beside it, and the two selectors already cover any other pair.
- **Rewrite a stored setting to the new default.** The file cannot tell a choice from a value
  saved beside it: the window writes both arrows whenever either selector changes, so a window
  where only EAST was ever changed holds the old `↑` north without anyone choosing it. Rewriting
  `↑` would also undo a real choice; the CHANGELOG asks to check NORTH once after the update
  instead. The owner's own file already holds `↓` north.
- **Keep the help's default words as literals.** A change of the core alone would leave them
  wrong with every test green, which this change would have done.

## Consequences

The tests of R-038 (R-039's example cell among them), R-040, R-041, R-064, R-071 (the log's
`north=Down`) and R-083 pin the new default, the help's words, the log line and the tips. Under
the default keys every signed DEC cell swaps its arrow (`↓ S` to `↑ S`, `↑ N` to `↓ N`); the
senses and every figure stay. The goldens `help.txt` and `en_help.txt` change their north line
and gain the one line. V-011's five cells with a DEC part are re-worded, re-measured on a copy of
the Veil session's root with its solved FITS (`docs/validation.md`, History, 2026-11-07). A
window without a settings file starts at `↓` north; one with a file keeps both arrows the file
holds. The tips that ADR 0038 decided gain the pair's name. The test was on a target west of
the meridian (the owner's word, recorded the same day in DitherCalculator's `CLAUDE.md`). East
of it, where the camera goes to the other side of the mount, the DEC arrows may swap north and
south; that side is not tested, and the README says so and how to check it. The tips and
`--help` do not name the side.
