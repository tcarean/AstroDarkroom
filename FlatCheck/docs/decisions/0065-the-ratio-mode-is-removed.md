# 0065. The --ratio mode and the Ratio tab are removed; one master is what the tool judges

Date: 2026-10-26
Status: accepted 2026-10-26
Supersedes: 0019; 0033 in part (the Ratio tab)

## Context

Mode 2, the attribution by 180-degree rotation (ADR 0019), needed two flat masters from two
sittings with the camera rotated between them, so two Siril session folders stacked separately.
The owner's workflow never produced that input: one session, one master, one stack. In the
window the tab stayed empty, since it proposed nothing by design (ADR 0033) and nobody chose two
files; in the console the flag went unused. The owner asked on 2026-10-26 to remove the mode
from both applications and to clean up what it left unused. Nothing in its numbers was wrong:
ADR 0019's measurements stand as history.

## Decision

The `--ratio` mode is removed: `RatioAnalyzer`, `RatioReport`, `RatioRun`, the console's
`--ratio` option and its `-world` map, the window's Ratio tab (`RatioTabViewModel`,
`RatioView`), its messages and the master-name filter that served only its file dialogs
(`RatioPattern`, `IsFlatMasterName`). With them go the helpers that had no other caller:
`Img.Rotate180`, `Img.CropCentred`, `Th.RatioOk` / `Th.RatioBad`, the two-argument
`StructureReport.WriteBody`, `RunOutcome.SecondaryPng` (a mode with two maps, `--smooth`, has
an outcome type of its own). Requirements R-020 and R-082 are `dropped`; the four `--ratio`
parity goldens are deleted (39 remain, R-098). The frozen Python prototype keeps its
`--ratio` (ADR 0047: not maintained, not run).

The sentences of the other modes that sent the reader to `--ratio` or to the Ratio tab as the
next test are reworded to what one master can and cannot say, in both languages: the
structure report's "why the residual matters" paragraph says that one master cannot tell the
camera-fixed part from the world-fixed one; the REFA advice says the figure is an upper bound
and that an off-centre optic shows there without being a flat error; the `--light` refusal on a
wide field and the series' undetermined verdict send the reader to the structure tests (the
console) or the Structure tab (the window); the smoothing and histogram notes say nothing here
separates the lens's part from the source's. The 17 parity goldens that carried those sentences
(15 structure, 2 `--light`) were recaptured with the owner's yes of 2026-10-26, text lines only,
no figure (`python-parity.md`).

## Alternatives rejected

- **Keep the mode in the console, drop only the tab.** The console's flag was as unused as
  the tab, and a mode that exists in one application only breaks the "two applications over
  one core" rule the window was built on (ADR 0011, ADR 0033).
- **Keep the recommendations of the other reports as they were.** A report that sends the
  reader to a mode the tool no longer has is wrong on its face; the recapture of 17 confirmed
  goldens for text lines only is the smaller cost.
- **Keep `Img.Rotate180` for the symmetry test.** Its one test,
  `Symmetrise_OutputIsExactlyInvariantUnderRotation`, states the invariant directly on the pixels; a helper kept for one
  test is dead code with a test.
- **Replace the mode's advice with a second-session comparison.** The check that actually
  caught the 11 % case of ADR 0019 (the same camera-fixed signature on two sessions with the
  same lens) is not in the tool, and designing it is a task of its own, not a removal.

## Consequences

- Removing a documented console flag is a breaking change by SemVer; the version bump is the
  owner's (the kit's rule "Version and year are the owner's").
- `--ratio` is an unknown option, exit 1; `--help` lists six modes in both languages
  (`help.txt`, `en_help.txt`).
- The window has seven tabs; `WindowSmokeTests` renders them all.
- The README, the user documentation, numbers the six modes consecutively (1 to 6). The
  requirements keep their ID blocks, and the design notes and the code's header comments
  keep the original mode numbers with mode 2 marked removed: a "mode N" in the English
  docs is the historical number, a "modul N" in the README the current one.
- The light-leak row of the map guide no longer attributes the spot to the camera by rotation;
  it says the leak is camera-fixed and comes back every session until its cause is found.
- `docs/design/modes-ratio.md` is deleted; `code-map.md`, `two-applications.md`,
  `pipeline.md`, `thresholds.md`, `png-output.md`, `process.md`, `known-issues.md` and
  `python-parity.md` no longer describe the mode, except as history.
- Guards: `RepoInvariantTests` (R-020 and R-082 have no `R###_` test), the regenerated goldens,
  `TheTableHasTheThirtyNineScenariosLeftOfTheCrossCheckAndNoTwoShareAGolden`.
