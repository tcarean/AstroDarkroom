# Let Adobe give the depth verdict (R-128, ADR 0090)

## Context

On 2026-11-02 the owner asked why the Tokina tablet master (`coperti-7-foi`) took its depth
verdict from the lensfun `Tokina AF 11-20mm f/2.8 AT-X Pro DX` entry, not the Adobe file of
their lens (the atx-i CF). The measurement (ADR 0090's context) showed the lensfun entries are
deeper than every panel flat of the owner allows, so the nearest rule (ADR 0031) let a panel's
own falloff pass as the lens's. The owner chose "Adobe decides" ("Da, Adobe decide") and
accepted R-128 ("Da, accept"). Measured on V-016..V-030: 3 of 15 verdicts turn one step
stricter (V-023, V-029, V-030), nothing else changes its verdict.

Already on `claude/Let-Adobe-give-the-depth-verdict` (worktree
`FlatCheck/.claude/worktrees/Let-Adobe-give-the-depth-verdict`, from main 472bfb7b, nothing
committed): R-128 as `draft` in `docs/requirements.md`; ADR 0090 `accepted 2026-11-02` with
ADR 0031's status line naming the part it supersedes (`check-decisions.ps1`: ok); the fixture
option `atxi` in `LensProfileTestData` (the Tokina's Nikon file under its atx-i name, own ID
139); `FlatCheck.Tests/AdobeVerdictTests.cs`, six R128 tests: four fail on today's code (ID
path verdict PERFECT instead of ACCEPTABIL; the AT-X name decides instead of the atx-i; the
console marker on the lensfun row; the Split reference is lensfun), two guards pass (range
path, no Adobe row).

The open branch `claude/Merge-the-Structure-tab-scales` (R-126/R-127, ADRs 0088/0089, waiting
for the owner's merge) touches the same files: `StructureReport.cs`, `StructureTabViewModel.cs`,
`StructureView.xaml`, `StructureTabTests.cs`, README, `lens-profiles.md`, `modes-structure.md`.
This branch takes R-128 and ADR 0090 so nothing collides; when that branch reaches main first,
main is merged into this one before the merge proposal, at the owner's yes.

## Decisions taken (owner, 2026-11-02) and the calls left to me

- Off the range path, with at least one Adobe row: the Adobe rows whose own `stCamera:LensID`
  is the raw's first, the nearest of them decides; else the nearest Adobe row.
- By range alone (R-121's case), with a lens chosen, with no Adobe row (no Adobe folder,
  `--source lensfun`): the nearest row, as before. `--lens <text>` counts as "not by range".
- The Split tab's reference profile is the row that gives the whole's verdict.
- My calls: `VignettingComparison.Nearest` becomes `Deciding` (plus `AdobeDecides`),
  `ProfileRow.IsNearest` and the window's `LensProfileRow.IsNearest` become `GivesVerdict`;
  `validation.json`'s key `nearest` becomes `decides`. No `ParityGoldenTests` scenario: the
  parity set holds no profile comparison (as for R-016, R-116, R-121); the R-016 golden covers
  the console path.

## The texts, old and new (only where Adobe decides; the nearest case keeps its words)

| where | old (EN / RO) | new (EN / RO) |
|---|---|---|
| console row marker (`WriteProfiles`) | `<- the nearest, gives the verdict` / `<- cel mai apropiat, da verdictul` | `<- Adobe, gives the verdict` / `<- Adobe, da verdictul` |
| window row note (now composed in the core, `StructureReport.VerdictRowNote`) | `The nearest profile - it gives the verdict.` / `Cel mai apropiat profil - da verdictul.` | `The Adobe profile - it gives the verdict.` / `Profilul Adobe - da verdictul.` |
| summary, whose verdict (`ComparisonSummary`) | `given by the nearest profile (X)` / `dat de profilul cel mai apropiat (X)` | `given by the Adobe profile (X)` / `dat de profilul Adobe (X)` |
| summary, the spread sentence | `the N profiles differ from each other by S stops in the corner; a master that matches one of them is not wrong` / `cele N profile difera intre ele cu S stopuri in colt; un master care coincide cu unul din ele nu e gresit` | `the N profiles differ from each other by S stops in the corner; the Adobe profile decides, because the lensfun ones are measured by volunteers, each with their own camera and diffuser, and vary more; they are shown for comparison` / `cele N profile difera intre ele cu S stopuri in colt; decide profilul Adobe, pentru ca cele lensfun sunt masurate de voluntari, fiecare cu aparatul si difuzorul lui, si variaza mai mult; sunt aratate pentru comparatie` |
| Split, reference line (`SplitReport.ReferenceLine`) | `(the nearest to the whole, the same on every column)` / `(cel mai apropiat de intreg, acelasi pe toate coloanele)` | `(the whole's Adobe profile, the same on every column)` / `(profilul Adobe al intregului, acelasi pe toate coloanele)` |
| Split, reference row note (`SplitReport.ProfileNote`) | `the nearest to the whole - the REFERENCE, gives the depth verdict on every column.` / `cel mai apropiat de intreg - REFERINTA, da verdictul adancimii pe fiecare coloana.` | `the whole's Adobe profile - the REFERENCE, gives the depth verdict on every column.` / `profilul Adobe al intregului - REFERINTA, da verdictul adancimii pe fiecare coloana.` |

Unchanged on purpose: the depth line under VERDICT and the advice (they name whichever row
decides), the --light note (the databases still differ by 0.2 stops), the R-121 picker and its
"the nearest profile decides" entry, `RangeOnlyNote`.

README (user guide, English): the paragraph "**The comparison's verdict is the nearest
profile's**" becomes "when the lens was found by its ID or name the Adobe profile gives the
verdict, why, the own-ID rule for two Adobe names of one optic, and the nearest profile by range,
with a chosen lens or without Adobe"; the measured table's column becomes "profile that decides",
its Tokina rows take the Adobe figures and gain the three paper masters; the Split paragraph says
the reference is the profile that gives the whole's verdict.

## Steps

1. **Core, numbers** (`FlatCheck.Core`):
   - `LensProfiles/LensProfile.cs`: `OwnLensId` (`long?`), a last optional constructor parameter;
     `WithLens` carries it. `LcpReader.cs` sets it from `stCamera:LensID` (the parse already done
     for `ids`); lensfun profiles leave it null.
   - `Analysis/VignettingComparison.cs`: `Nearest` → `Deciding`, `AdobeDecides`; the constructor
     picks by R-128 from `shot` (`CandidatesByRangeOnly`, `ChosenLens`, `Raw.LensId`), the NaN rule
     kept; `Verdict = Deciding.Verdict`; the file's "THE VERDICT IS THE NEAREST PROFILE'S" comment
     rewritten to the rule and ADR 0090.
   - `Runs/SplitRun.cs`: the reference is `whole.Profiles.Deciding`.
   - `Logging/LogText.cs`: `nearest=` → `decides=` with `adobeDecides=`.
2. **Core, words** (`Report/`): `StructureReport` - `ProfileRow.GivesVerdict`, the marker, the
   summary and spread sentences above, `VerdictRowNote(c)`, every `c.Nearest` → `c.Deciding`
   (`DepthLightScaleNote`, `DepthDetail`, `Advice`); `SplitReport` - the two Split texts, by
   whether the reference row is Adobe-decided.
3. **Window**: `ViewModels/Rows.cs` (`GivesVerdict`), `Views/StructureView.xaml` (three
   bindings), `StructureTabViewModel` (row note from `StructureReport.VerdictRowNote`,
   `Deciding` in the scale note). The Split tab already takes its texts from `SplitReport`.
4. **Tests**: the R128 tests turn green unchanged (their helper renamed to `GivesVerdict`).
   Renames in `VignettingComparisonTests`, `StructureReportTests`, `SplitRunTests`,
   `ValidationTests`, `StructureTabTests`. Intended test changes, reported at wrap-up:
   - `VignettingComparisonTests.TheVerdictIsTheNearestProfiles_NotTheWorst` moves to a fixture
     with no Adobe row, where the nearest rule still holds;
   - `SplitRunTests.R093_SplitRun_HasOneColumnPerPartTheWholeAndTheReferenceProfile`: its
     "a part's own nearest differs from the reference" clause moves to a no-Adobe fixture,
     the depths measured before choosing it (the Adobe case is R128's Split test);
   - `StructureTabTests.WithARawBesideTheMasterTheVignettingIsComparedWithTheLensProfiles`:
     the summary now says `profilul Adobe`, and its comment says Adobe decides;
   - one window test, `R128_TheStructureTabMarksTheAdobeRowAndSaysSo`, beside it: on a master
     next to the lensfun entry the marked row is Adobe's and its note `Profilul Adobe`.
5. **Goldens**: `FLATCHECK_UPDATE_GOLDEN=1` for `GoldenOutputTests|EnglishOutputTests`; only
   `structure_with_profiles` and `en_structure_with_profiles` may change: the marker moving to
   the Adobe row, the summary, the spread sentence, the corner-error figures of the --light note
   and the depth line's figure and lens. Any other changed line is a finding.
6. **Docs**: R-016 and R-093 amended, R-128 `implemented`; README as above; design notes
   `lens-profiles.md` §11 (+ the §10 paragraph: the four Adobe files, Canon included, are one
   measurement), `modes-structure.md` (the sixth test), `code-map.md` (`Deciding`, `OwnLensId`,
   the Split reference), `flat-sources.md` (a pointer to ADR 0090 under the reference depths);
   `validation.md` + `.json` re-recorded (V-023, V-024, V-026..V-030; the key rename; a History
   line); CHANGELOG `[Unreleased]` Changed; this plan saved as
   `docs/plans/2026-11-02-let-adobe-give-the-depth-verdict.md`.
7. **Commit** on the branch with the gate green, `/wrap-up`, a subagent code review before the
   merge proposal (the owner's practice), then the merge proposal.

## Verification

- While editing: `scripts/verify.ps1 -SkipTests`; then the R128 filter green.
- The full gate `scripts/verify.ps1` (`check-decisions`, `check-design -Since main`, format,
  build with warnings as errors, every test).
- `/validate` with `FLATCHECK_TESTDATA_LARGE` on the masters: drift is expected only on V-023,
  V-024 and V-026..V-030, to the figures measured on 2026-11-02 (V-023 +0.19 ACCEPTABIL, V-029
  +0.15 and V-030 +0.14 ACCEPTABIL, V-024 +0.11, V-026 +0.58, V-027 +0.59, V-028 +0.10); any
  other drift is a finding. The drift table pasted.
- The owner's case end to end: the console on `Tokina\ISO400\f2.8-tableta-lipita-coperti-7-foi`
  with `-o` in the scratchpad (no file of theirs touched): the atx-i row marked, +0.59 REFA.
- The window's Structure card rendered with `FLATCHECK_DUMP_SCREENS` to see the marked row.
