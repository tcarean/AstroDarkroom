# Code review of the whole project, 2026-11-13

The list of what the review found, to be fixed one entry at a time in the order of the summary
table. The decision to hold it, the severity scale and how an entry is closed are ADR 0052.

## How it was done

The code was written by an earlier model and had almost no review. On 2026-11-13 it measured,
blank lines included, 19,331 lines (`src/DarksCheck.Core/` 13,544, `src/DarksCheck.Cli/` 530,
`src/DarksCheck.Wpf/` 5,257) and 11,698 of tests: too much for one careful reading, so it was
split into nine areas. Each area was read whole by a reviewer with a clean context (a subagent
on this session's model), which first read `CLAUDE.md`, the area's design notes and its
requirements, so that what is deliberate was not reported, and read the area's tests for one
question: can this test fail, and does it check what its name says. A reviewer changed nothing
and built nothing. Every finding it returned was then checked by the session that wrote this
list: the cited lines were opened and the case followed through the callers. The seams between
areas (what `Runs/` hands the console and the window, the language scope, where the files of
`--out` can land) were read by the session itself, before the reports came in.

| # | Area | Lines | Reported | In this list as |
|---|------|------:|---------:|-----------------|
| 1 | The dark series | 2,209 | 11 | CR-005, 008, 011, 012, 013, 016, 018, 022, 030, 054, 064 |
| 2 | Biases, leaks, verdicts, thresholds | 1,724 | 6 | CR-003, 007, 010, 014, 015, 070 |
| 3 | The lights, the index, the run | 1,468 | 11 | CR-002, 004, 006, 013, 022, 055, 062, 063, 064; CP-3 |
| 4 | The report of the dark series | 2,318 | 16 | CR-004, 028, 030, 032, 034, 037, 038, 039, 040, 050, 051, 052, 053, 054, 068 |
| 5 | Session, console, language | 2,195 | 16 | CR-001, 009, 012, 013, 017, 019, 026, 028, 041, 042, 049, 053, 057, 058, 059, 061 |
| 6 | Files written, scale, log | 1,892 | 17 | CR-019, 020, 027, 028, 029, 032, 033, 034, 035, 036, 046, 051, 054, 059, 060; CP-1, CP-6 |
| 7 | Imaging (the copied files) | 2,299 | 13 | CP-1 to CP-12 |
| 8 | Window: view-models and shell | 2,463 | 12 | CR-021, 022, 031, 047, 048, 056, 063, 064, 065, 066, 067 |
| 9 | Window: views, presentation, theme | 2,763 | 8 | CR-023, 024, 025, 043, 044, 045, 069 |
| - | Seams, read by the session | - | 3 | CR-019, 022, 049 |

110 findings were reported and one was rejected at the check (area 8: a changed setting of the
top bar applies only at the next run and a stopped run has no rerun button, which ADR 0048
decided and which makes no figure wrong). The same defect seen from two areas became one entry
and small related ones were grouped, which gives the 75 entries below (18 of severity 1, 3 of
severity 2, 35 of severity 3, 19 of severity 4) and twelve more for the copied files. Of the 75,
48 are `confirmed`, 13 `to measure` and 14 `owner`; of the twelve, 7, 4 and 1. The three seam
findings were each found by a reviewer too. The entries on tests and on notes (CR-072 to CR-075)
gather what every area reported in its short lists. That almost nothing was rejected says the
reviewers were careful, not that every entry is proved: the check was a reading, not a run, and
nothing in the project was built or run. What a reading cannot settle is marked. The lists
inside CR-073 to CR-075 and the cleanup section were spot-checked, not opened line by line.

Out of scope: `scripts/` and `.claude/` (the kit's), `prototype/` (frozen, ADR 0003), and how
strong the tests are line by line (`/mutate` measures that).

Six reviewers worked outside the repository, read-only. Area 1 fuzzed a Python port of
`Planes.Select`, `Median` and `Percentile` on 200,000 arrays against a sort (no mismatch) and
removed it. Area 2 replicated `FrameLightOf` and the intercept of `SidesOf` in throw-away
Python, to put figures on CR-014 and CR-015. Area 3 ran a small F# script (`dotnet fsi`) for
the tag texts and the rank band. Area 4 compared, by script, the figures of each Romanian
golden with its English twin. Area 7 read the public sources of LibRaw 0.21.1 through a web
fetch that returns a summary with quoted fragments, listed by reflection the calls of the
shipped `Sdcb.LibRaw` binding, and checked two pieces of arithmetic. Area 8 read two source
files of WPF 8 (dotnet/wpf) for the folder dialog. Their figures are quoted as theirs.

Line numbers are those of commit `10aa28de`. A task finds its lines again by the names the
entry gives. A path that starts with `Analysis/`, `Report/`, `Runs/`, `Session/`,
`Imaging/` or `Language/` is under `src/DarksCheck.Core/`.

## How to read an entry

- **Severity.** 1 = a wrong figure or a wrong verdict: a dark set kept or redone wrongly, a
  wrong index or label, a number shown that is not the session's. 2 = a crash, a hang, or a file
  written where it must not be. 3 = a wrong text or a wrong display. 4 = robustness and
  cleanliness.
- **Reach.** Who meets it: *common* (an ordinary run), *some setups* (another camera, capture
  program, format or session shape than NEF darks, biases and lights of a Nikon D3300 at one
  ISO and exposure), *slip* (a mistyped argument, a wrong click, a stray file), *rare*,
  *crafted* (a broken or hostile file).
- **Evidence.** *confirmed*: the lines were read and the case follows from them. *to measure*:
  the code was read, but the case rests on something only a run, a screenshot or an outside
  source can show; the task starts by showing it. *owner*: the code does what is described and
  follows a requirement, a note or an ADR; whether that is a defect is the owner's call.
- **Status.** `open`, then `fixed <commit>` or `rejected: <reason>`, set by the task that
  closes the entry.

An entry gives the direction of a fix, never the fix. A task takes one entry (or the group one
entry names), starts from the requirement and a failing test (the kit's rules "Requirements
first" and "Test before fix"), and a formula change re-runs `/validate` (the rule "Measured on
the sessions"). A text an entry would change is shown to the owner first, original and proposal
side by side (the rule "Plain words"); a golden line it changes is reported at wrap-up (the
rule "Golden files"). The texts quoted in the entries are the English ones; the Romanian of
each pair has the same defect unless the entry says otherwise.

## Summary

| ID | Sev | Reach | Evidence | Title | Status |
|----|----:|-------|----------|-------|--------|
| CR-001 | 1 | some setups | confirmed | Sidecar and hidden files with a frame extension are frames; over the cap they take the places of darks | fixed 26c91d91 |
| CR-002 | 1 | some setups | confirmed | A set that does not say its ISO is compared at factor 1 with a set that does | fixed c1670706 |
| CR-003 | 1 | some setups | confirmed | The noise of the lights' stack adds the lights' ADU to the darks' ADU when their settings differ | fixed 7ae97219 |
| CR-004 | 1 | some setups | confirmed | Lights that do not show the dark's hot pixels get an index of `idx-0` or `idx0`, and a lights-only session is labelled with it | fixed 215e8591 |
| CR-005 | 1 | some setups | confirmed | A set whose central pixels all sit above the hot threshold reads a master noise of 0.00 ADU, and check 9 says OK | fixed c9a1efbe |
| CR-006 | 1 | some setups | owner | `OK: the dark can be subtracted as it is` is said from the normalised ratio, also when the lights are at another ISO or exposure | fixed b7e970b0 |
| CR-007 | 1 | some setups | owner | Without lights, check 9 asks for a master noise under one value step, which on a camera with steps of 1 means a hundred darks | fixed 2def04f8 |
| CR-008 | 1 | some setups | owner | With 61 to 119 darks only 31 to 60 are read | fixed 7e8d7a2f |
| CR-009 | 1 | some setups | owner | FITS without an ISO: a known exposure mismatch reads UNMEASURED | fixed bfe04ca6 |
| CR-010 | 1 | slip | confirmed | Biases at another ISO than the darks are subtracted as they are | fixed a047f9ea |
| CR-011 | 1 | slip | confirmed | FITS darks take their black from biases that are then refused for their size | open |
| CR-012 | 1 | slip | owner | Check 1 judges the dominant setting, the analysis keeps the first file's | open |
| CR-013 | 1 | rare | owner | Frames are taken in file-name order as if it were shooting order | open |
| CR-014 | 1 | rare | to measure | When about a third of the frames at one end carry light, no frame is named and check 3 can read OK | open |
| CR-015 | 1 | rare | to measure | A warming that rests on one cold frame gives the constant light an error several times too small | open |
| CR-016 | 1 | rare | confirmed | A frame is checked against the first frame's size, ISO and exposure, never against its black and white | open |
| CR-017 | 1 | rare | owner | `--lights DIR` falls back to DIR's own FITS, so a stacked `result.fit` is measured as the one light | open |
| CR-018 | 1 | rare | owner | The master's noise rests on the first two decoded frames alone | open |
| CR-019 | 2 | slip | confirmed | `--out` is refused by the spelling of the folder given, not by the folder | open |
| CR-020 | 2 | rare | confirmed | After a partner that failed, the window's export can write into the partner's folder | open |
| CR-021 | 2 | rare | to measure | "Write the files..." can close the window when the session folder is unreachable or relative | open |
| CR-022 | 3 | common | confirmed | Stop shows nothing, and the measuring steps ignore it | open |
| CR-023 | 3 | common | to measure | The Lights tab cannot scroll: the lower rows of check 2 and the lights table are cut off | open |
| CR-024 | 3 | common | to measure | The Frames table loses its `>20` column and the reason of an unread frame on a window under about 1440 wide | open |
| CR-025 | 3 | common | confirmed | The charts' tick labels are rounded more coarsely than their gridlines | open |
| CR-026 | 3 | common | confirmed | The OK line of `match` says "the same exposure (X s)" when the lights' differs by up to 2 % | open |
| CR-027 | 3 | common | confirmed | The console's report and the written report point at "the Maps tab" | open |
| CR-028 | 3 | common | confirmed | A count of one gets a plural word | open |
| CR-029 | 3 | common | owner | One report gives two figures for the noise of a light | open |
| CR-030 | 3 | common | owner | The count of pixels that "jump between two levels" takes in every strong steady hot pixel | open |
| CR-031 | 3 | common | owner | The export dialog opens inside the folder the export refuses, and the refusal names a console option | open |
| CR-032 | 3 | some setups | confirmed | The hot-pixel limit the texts name is not the one the counts were taken at | open |
| CR-033 | 3 | some setups | confirmed | An export into a folder that holds an earlier export leaves that export's files beside the new ones | open |
| CR-034 | 3 | some setups | confirmed | Frames without a colour order: the CSV header breaks and `CFA[0,1]` reaches the texts | open |
| CR-035 | 3 | some setups | confirmed | A float FITS is described as "16 bits"; the float wording of R-080 can never appear | open |
| CR-036 | 3 | some setups | confirmed | With a sky at or under the black, the sky row gives figures while the paragraph says the session has no lights | open |
| CR-037 | 3 | some setups | confirmed | Check 7 prints an empty warning when an edge is over the threshold but is not glow | open |
| CR-038 | 3 | some setups | confirmed | Check 7 calls the worst banding "row" when it is the columns' | open |
| CR-039 | 3 | some setups | confirmed | The doubling-rule sentence rounds the user's setting to a whole degree | open |
| CR-040 | 3 | some setups | confirmed | A small factor prints as "0.00 ADU" | open |
| CR-041 | 3 | some setups | confirmed | File names over 20 characters are cut in the inventory tables | open |
| CR-042 | 3 | some setups | to measure | A folder name with letters outside the console's code page prints as `?`, the label included | open |
| CR-043 | 3 | some setups | to measure | Guide labels of the charts are drawn over one another when their lines come close | open |
| CR-044 | 3 | some setups | confirmed | The bold of a figure stops in the middle of `ADU-eq` | open |
| CR-045 | 3 | some setups | to measure | A figure wider than its table cell loses its first characters, not its last | open |
| CR-046 | 3 | some setups | owner | "About as much as a flat is off on average" follows whatever percentage the sky gives | open |
| CR-047 | 3 | slip | confirmed | A lights folder that cannot be used leaves the window on the problem card with no way out | open |
| CR-048 | 3 | slip | confirmed | A relative folder on the window's command line stays relative | open |
| CR-049 | 3 | slip | confirmed | A lights-only session with a bad `--against`: the console's error lacks `the partner:` | open |
| CR-050 | 3 | slip | to measure | A folder whose name starts with a verdict word gives the label row a verdict | open |
| CR-051 | 3 | rare | confirmed | "The session has no biases" and "no lights" are said of sessions that have them | open |
| CR-052 | 3 | rare | confirmed | "The series started cold / warm" is said of frames at the end, or on the other side | open |
| CR-053 | 3 | rare | confirmed | A figure rounded at a limit contradicts the word beside it | open |
| CR-054 | 3 | rare | confirmed | The step row reads "go up 0 by 0 ADU" when no step was found | open |
| CR-055 | 3 | rare | confirmed | A library kept as `darks\` at a drive root is labelled `-idx25` | open |
| CR-056 | 3 | rare | owner | An exception outside the six handled types ends the run with an empty window and no word | open |
| CR-057 | 4 | slip | confirmed | `--out` takes an option as its folder; `--inventory` drops `--threads` and `--max-frames` in silence | open |
| CR-058 | 4 | slip | confirmed | An `--out` that cannot be created is found only after the whole analysis | open |
| CR-059 | 4 | rare | confirmed | After Ctrl+C the log has no last line, and an export under way stays half-written | open |
| CR-060 | 4 | rare | confirmed | A failed export leaves a mixed set of files, and the console names none of those it wrote | open |
| CR-061 | 4 | slip | confirmed | A folder that is not a folder of darks opens as a library | open |
| CR-062 | 4 | slip | confirmed | A partner that is the open folder itself is read again and reported as a match | open |
| CR-063 | 4 | common | confirmed | Progress counts can arrive out of order, so a phase can end on "31 / 32" | open |
| CR-064 | 4 | some setups | to measure | Memory: the thread count has no upper bound, the frame cap counts files, and a Debug build keeps the released stacks | open |
| CR-065 | 4 | slip | confirmed | "Infinity" and "1e999" pass as valid settings | open |
| CR-066 | 4 | rare | to measure | Without a local application data folder the settings would be written relative to the current directory | open |
| CR-067 | 4 | some setups | to measure | Restore Down can put the title bar above a low screen | open |
| CR-068 | 4 | some setups | confirmed | Table columns that overflow | open |
| CR-069 | 4 | common | to measure | The long hints close after five seconds; Tab stops on things that are not controls and the focus mark is black on dark | open |
| CR-070 | 4 | - | confirmed | Thresholds typed as numbers in the code and the texts | open |
| CR-071 | 4 | - | owner | Figures and verdicts decided in the report, not in the analysis | open |
| CR-072 | 4 | - | confirmed | No gate test decodes a raw, reads a pixel of a map, or has frames without a colour order | open |
| CR-073 | 4 | - | confirmed | Core tests that cannot fail or do not check what their name says | open |
| CR-074 | 4 | - | confirmed | Window tests that cannot fail or do not check what their name says | open |
| CR-075 | 4 | - | confirmed | Design notes, requirements and comments that describe other code | open |
| CR-076 | 1 | some setups | owner | Sets that give the exposure and no ISO are compared as made at the same settings, though their exposures are known to differ | fixed 91508347 |
| CR-077 | 1 | rare | confirmed | Darks without an ISO between lights and a partner at two ISOs: the chained verdict can read `as it is`, and two rows take the darks' ISO two ways | fixed 581dd50a |

The files copied from LightsCheck have their own entries, CP-1 to CP-12, in their own section;
five of them are of severity 1.

| ID | Sev | Reach | Evidence | Title | Status |
|----|----:|-------|----------|-------|--------|
| CP-1 | 1 | some setups | confirmed | A FITS is read with its rows in file order, so a Siril FITS is upside down against the raw | open |
| CP-2 | 1 | some setups | to measure | A DNG whose black level is a 2 x 2 pattern is read with black 0 | open |
| CP-3 | 1 | some setups | to measure | A Fujifilm X-Trans raw is accepted, and check 2 treats 2 x 2 positions as colours | open |
| CP-4 | 1 | some setups | owner | A raw's four channel blacks are folded into one number | open |
| CP-11 | 1 | rare | confirmed | A header number with a comma or a D exponent is read wrong | open |
| CP-8 | 2 | crafted | confirmed | A FITS with a negative axis hangs the run | open |
| CP-6 | 3 | common | to measure | The CSV shows a raw's exposure with single-precision noise | open |
| CP-5 | 3 | rare | to measure | A raw stamped in the hour the clocks skip comes back an hour off | open |
| CP-9 | 3 | rare | confirmed | A table or compressed HDU is read as an image | open |
| CP-10 | 3 | rare | confirmed | Two header parsers: the inventory and the stack can disagree, and a cut header looks whole | open |
| CP-7 | 3 | crafted | confirmed | A damaged raw's row carries the binding's own English text | open |
| CP-12 | 4 | crafted | confirmed | Colour orders that are not Bayer pass | open |

## Severity 1: a wrong figure or a wrong verdict

### CR-001 Sidecar and hidden files with a frame extension are frames; over the cap they take the places of darks

- Where: `Session/SessionFolder.cs:213-215` (`FramesIn`); the cap at `Analysis/FrameStack.cs:167`
  and `:180`.
- What happens: `Directory.GetFiles(dir)` and the extension test alone; no attribute and no
  leading `._` or `.` is looked at. `._X.NEF` sorts before `DSC_...`.
- Case: a card a Mac has touched leaves `._DSC_0034.NEF` beside every file. 20 darks: `darks\
  (40 frames .NEF)`, rows 1..20 are `error:`, a WARNING, exit 1. 35 darks with 35 such files:
  n = 70 is over the cap of 60, so one in two is read: 18 sidecars and 17 of the 35 darks; 18
  good darks are listed as `skipped`, and the master, the noise and check 9 are those of 17.
- Should: such files are not listed.
- Test: none. `._DSC_0001.fit` added to a fixture, asserting the count.
- Evidence: confirmed. Same as LightsCheck's CR-032.
- Status: fixed 26c91d91

### CR-002 A set that does not say its ISO is compared at factor 1 with a set that does

- Where: `src/DarksCheck.Core/Analysis/LightsAnalyzer.cs:221-223` (`Analyze`), `:497`
  (`Measure`); `Runs/DarkSeriesRun.cs:426`, `:442` (`MatchPartner`);
  `Analysis/Normalisation.cs:41-47` (`NormOf` returns 1 without an ISO or an exposure).
- What happens: only one direction is handled: a light that does not say its settings takes the
  darks' (`assumed ? darks.Norm : NormOf(frame.Info)`). When the darks are the silent side their
  factor is 1 and the lights keep their own. `MatchPartner` has no assumption at all.
- Case: (a) darks as FITS with `EXPTIME` and no `ISOSPEED`, lights NEF at ISO 1600 and 120 s,
  same temperature: ratio (e/4)/(d/1) = 0.25, `do not match ... too warm`, the lights' index a
  quarter of the darks'. (b) NEF darks at ISO 1600 against a FITS partner without `ISOSPEED`:
  `darks / partner` 0.25, -12 degrees, beside a `partner` row that prints `settings unknown`.
  (c) For any set without ISO the INDEX row still says `worked out for ISO 400 and 120 s`.
- Should: about 1.00, or unmeasured with the reason.
- Deliberate: ADR 0004 says "1 when the files do not say"; R-052 covers the silent light only.
- Test: `R052_TheRatioIsReadPerLightAndNormalisedByTheLightsOwnSettings` covers lights without
  ISO. `WithDarkSeries(..., iso: null)` with lights at ISO 800; a partner built with `iso: null`.
- Evidence: confirmed.
- Status: fixed c1670706

### CR-003 The noise of the lights' stack adds the lights' ADU to the darks' ADU when their settings differ

- Where: `src/DarksCheck.Core/Analysis/StackNoise.cs:39-48` (`LightsShareAdu`, `MeanStackAdu`,
  `MedianStackAdu`) and `:59-77` (`Of`); read by `Report/ScaleReport.cs:276-295`
  (`AgainstMasters`) and by the leak and scale rows of `Report/DarkReport.cs`.
- What happens: `StackNoise.Of` takes the median of the lights' sky sigma, in the lights' own
  raw ADU, and the dark master's noise, in the darks' raw ADU, and `MeanStackAdu` adds the two
  in quadrature with nothing between them. Check 9 does the same comparison with both noises at
  the darks' settings (`DarkVerdicts.cs:351-358`, through `LightsAnalyzer.NoiseOnPlane`).
- Case: darks at ISO 400, 120 s, master noise 0.40 ADU; 32 lights given with `--lights` at ISO
  800, 120 s, sky sigma 12 ADU. Printed: the stack of the 32 lights has 2.2 ADU (from
  sqrt(2.12^2 + 0.40^2)). In one unit it is 2.27 of the lights' ADU or 1.13 of the darks'. A
  constant light of +0.79 ADU in the darks is printed as 0.37 times the stack's noise; it is 0.70.
- Should: both noises at one setting, as check 9 has them.
- Deliberate: ADR 0051 (decision 3) leaves the scale section and the leak rows on the sky
  sigma, "a task of its own"; nothing speaks of lights at other settings than the darks.
- Test: none; `R080_TheScaleSectionExplainsTheAduOfTheOpenSet` has lights at the darks'
  settings. Lights at twice the ISO should read the same ratio as lights at the same ISO with
  half the sky sigma.
- Evidence: confirmed.
- Status: fixed 7ae97219 (the unit; which noise of a light the scale section takes is still CR-029)

### CR-004 Lights that do not show the dark's hot pixels get an index of `idx-0` or `idx0`, and a lights-only session is labelled with it

- Where: `Analysis/IndexAnalyzer.cs:220-225` (`EquivalentIndex`), `:79` (`TagFor`), `:343`
  (`LabelSuffix`); `Runs/DarkSeriesRun.cs:340-345`.
- What happens: `EquivalentIndex` returns NaN only for `Unmeasured`. A ratio at or under 0 is
  `Unmatched` (`Judge`, `:270`), so the index is the darks' times a negative or near-zero ratio,
  and `TagFor` rounds it and prints the sign.
- Case: a lights-only session against a library of idx67, lights with in-camera noise reduction
  (the case R-052 names). The night's ratio is -0.004: `index lights : idx-0 (-0.00 times the
  dark)`, `label : <session>-idx-0`. Renamed to that label, the next run does not strip it and
  reads `<session>-idx-0-idx29`.
- Should: `idx-` (R-066: "`-idx-` when it cannot be measured") and no `index lights` row.
- Also: `Report/DarkReport.cs:419-430` (`LightsLike`) returns nothing only without lights or
  with an unmeasured index, while `MatchText` and `TraceRow` know the absent case. A ratio of
  0.01: the verdict says the dark's hot pixels do not show, and two rows above it `index lights
  : idx0 (0.01 times the dark, that is 39.9 degrees colder than it)`.
- Test: `R052_LightsWithoutTheMastersHotPixelsAreUnmatchedNotUnmeasured` asserts
  `IsNaN(r.LightsIndex) || r.LightsIndex <= 0.05 * r.Index.Index`, which accepts any negative.
- Evidence: confirmed (the tag texts by the reviewer's script; the code read).
- Status: fixed 215e8591

### CR-005 A set whose central pixels all sit above the hot threshold reads a master noise of 0.00 ADU, and check 9 says OK

- Where: `src/DarksCheck.Core/Analysis/DarkSeriesAnalyzer.cs:933-941` (the quiet mask:
  `excess < hotT`), `:1007-1033` (`MeanDiff`), `:994-996` (`NoiseCurve`);
  `Analysis/Planes.cs:110-132` (`ClippedSigma`) and `:146-171` (`Std` returns 0 when nothing is kept).
- What happens: the noise curve reads only the crop pixels under the hot threshold. With none,
  `MeanDiff` returns an empty array, and `ClippedSigma` of an empty array returns sigma 0 (its
  comment says "both NaN for an empty plane"). So the temporal sigma is 0, not NaN, and
  `MasterNoise = 1.2533 x 0 / sqrt(n) = 0`.
- Case: raw darks of an older or warm sensor with a mean dark current of 25 ADU-eq above the
  black: every pixel is "hot", and check 9 reads `OK: 30 frames are enough; the master's noise
  (0.00 ADU) is under the file's value step`. `DarkVerdicts.cs:354` drops the lights' rule
  because `perFrame > 0` is false. With part of the crop over the threshold the noise is read
  on the truncated low half and comes out too small.
- Should: an unmeasured noise (NaN reaches UNMEASURED in `EnoughOf`), or a quiet mask relative
  to the master's own level.
- Test: none. A series with `Pedestal = 230` against biases at 200.
- Evidence: confirmed on the code path; which cameras reach it is not known.
- Status: fixed c9a1efbe

### CR-006 `OK: the dark can be subtracted as it is` is said from the normalised ratio, also when the lights are at another ISO or exposure

- Where: `Analysis/LightsAnalyzer.cs:497` (the ratio divides each side by its own factor),
  `:348-384` (`TracesOf`); `Analysis/IndexAnalyzer.cs:263-286` (`Judge`); the words in
  `Report/DarkReport.cs` (`MatchText`).
- Case: darks at ISO 400 and 120 s, lights at ISO 800 and 120 s, same temperature (the fixture
  of the R052 test): ratio 1.00, `OK: the dark can be subtracted as it is`, `what is left: after
  subtraction nothing is left to see`. In the files each hot pixel is twice as high in the
  light. Check 1 says WARNING on the settings in the same report.
- Should: with unequal settings the verdict cannot be "as it is" (the owner's call; R-052 and
  ADR 0004 set the normalised ratio, nothing sets the words for unequal settings).
- Evidence: owner.
- Status: fixed b7e970b0 (the owner's word of 2026-11-14: never "as it is" at other settings, the advice given, the mismatch large and red in the window; the partner too; ADR 0055)

### CR-007 Without lights, check 9 asks for a master noise under one value step, which on a camera with steps of 1 means a hundred darks

- Where: `src/DarksCheck.Core/Analysis/DarkVerdicts.cs:363-366` and `:396` (`EnoughOf`),
  `Th.MasterNoiseVsStep`.
- What happens: without measured lights `needed = max(Th.MinDarks, ceil((perFrame /
  (Th.MasterNoiseVsStep x step))^2))`, with `perFrame` 1.2533 x the frame-to-frame sigma.
- Case: the rule passes 30 darks only while the frame-to-frame sigma is under 4.37 steps. On the
  D3300 (step 2) that is 8.7 ADU, never reached. On a camera whose values go 1 by 1, with a
  frame-to-frame sigma of 9 ADU (illustrative), 30 darks read WARNING with 128 needed; the same
  darks opened with their lights would be judged on the stack (ADR 0051). Every library folder
  is judged this way, since a library has no lights.
- Should: the owner's call. R-024 and ADR 0051 decision 3 say "without lights the rule of
  before stands"; the outcome turns on the camera's step, not on the darks.
- Test: `R024_TheNoiseOfTheMeanFollowsSqrtNAndTenCleanFramesAreEnough` pins the rule.
- Evidence: owner.
- Status: fixed 2def04f8 (the owner chose the minimum of ten alone without lights; ADR 0056)

### CR-008 With 61 to 119 darks only 31 to 60 are read

- Where: `Analysis/FrameStack.cs:167`, `:180-185` (`Load`: `step = ceil(n / maxFrames)`, only
  `i % step == 0` decoded).
- Case: 61 darks whose lights need 39: 31 are stacked and check 9 reads `WARNING: 31 frames are
  few ... 39 are needed`, while the folder's own master of 61 is enough. The index and the
  master's noise are those of 31. An even spread of exactly `maxFrames` files would read 60.
- Deliberate: R-027 and ADR 0007 ("every k-th file"); the header says how many were read.
- Test: `R027_AboveTheCapOnlyEveryKthFrameIsDecoded` pins it.
- Evidence: owner.
- Status: fixed 7e8d7a2f (the owner chose exactly the cap, evenly spread from the first file to the last; ADR 0057)

### CR-009 FITS without an ISO: a known exposure mismatch reads UNMEASURED

- Where: `Analysis/MatchAnalyzer.cs:87-91` (`Analyze`).
- What happens: the verdict is computed only when exposure and ISO are known on both sides.
- Case: darks of 300 s and lights of 120 s from a capture program that writes `GAIN` and no
  `ISOSPEED`. The rows show `300.0 s` and `120.0 s`; `match` says `UNMEASURED: the frames do not
  state the exposure or the ISO`.
- Should: WARNING on the exposure, which is known (the owner's call: R-007 says UNMEASURED
  "when either side lacks the exposure or the ISO").
- Test: `R007_WithoutExposuresOrIsoTheMatchIsUnmeasured` pins equal exposures only.
- Evidence: owner.
- Status: fixed bfe04ca6 (the owner chose a WARNING on a setting known to differ; ADR 0058)

### CR-010 Biases at another ISO than the darks are subtracted as they are

- Where: `src/DarksCheck.Core/Analysis/BiasAnalyzer.cs:225-249` (`DarkMinusBias`, only the size
  is compared, `:229`); `Analysis/DarkVerdicts.cs:270-271` (the bias regions to `EdgeSplitsOf`
  and `SidesOf`) and `:296-297` (`overRead`); `Analysis/MatchAnalyzer.cs:95-99` (the bias is
  judged on its exposure alone).
- What happens: nothing compares the biases' ISO (or bit depth) with the darks'. `Judge` takes
  `sqrt(temporal^2 - read^2)` with the darks' noise at one ISO and the biases' read noise at
  another, subtracts the bias master's map from the darks' in raw ADU, and takes the bias
  regions' offsets as the "readout" part of an edge. Only the scale section names the biases'
  ISO (`ScaleReport.cs:374-376`).
- Case: darks at ISO 1600, `biases\` at ISO 400. Check 5 prints the noise added during the
  exposure from a read noise of ISO 400 under darks whose own is several ADU: too large, with no
  word. The `dark - bias` map and the sides "after the bias" subtract a readout pattern of
  another gain. ADR 0012's table has the bottom edge at 0.38 ADU in ISO 400 biases and 0.58 in
  ISO 1600: a difference as large as `Th.LeakConstantAdu` (0.15).
- Should: a WARNING and the biases left out, as for another size (R-041), or the mismatch said
  in check 1.
- Test: none. A session fixture with biases at another ISO than the darks.
- Evidence: confirmed that nothing compares the settings; whether a verdict flips on real
  files is to measure.
- Status: fixed a047f9ea (the biases are left out before they are read, as those of another size; ADR 0059; no real biases of another ISO were measured)

### CR-011 FITS darks take their black from biases that are then refused for their size

- Where: `Runs/DarkSeriesRun.cs:203-205` (`biasBlack` set when the biases are measured), `:231`
  (passed to the darks), `:250-267` (the size check comes only after, in
  `BiasAnalyzer.DarkMinusBias`, `BiasAnalyzer.cs:229-231`); applied at `FrameStack.cs:247-250`.
- Case: FITS darks at pedestal 200 with biases of another camera or crop at pedestal 1000: the
  run reports the biases as left out, yet every figure of the darks is measured against black
  1000 (no hot pixel, every pixel "dead"), and the header says `the black taken from the
  biases`.
- Should: the darks' own median, as without biases (R-041: "left out of every figure").
- Test: `R041_BiasesOfAnotherSizeAreReportedAndLeftOut` uses pedestal 200 for both groups, so it
  cannot see this.
- Evidence: confirmed.
- Status: open

### CR-012 Check 1 judges the dominant setting, the analysis keeps the first file's

- Where: `Analysis/MatchAnalyzer.cs:135-149` (`Settings`, the largest cluster) against
  `Analysis/FrameStack.cs:195-203` (`firstInfo ??= info`, `SameSettings(info, firstInfo)`).
- What happens: the inventory names the largest cluster of (exposure, ISO); the stack takes the
  first decoded file's settings and drops every other frame as `settings differ from the first
  frame`.
- Case: `darks\` holds `DSC_0033.NEF` at 1 s, then 20 frames at 120 s. Header: `darks exposure:
  119.2 s at ISO 400 (20 of 21 frames; ...)`, `match: OK`. Analysis: `frames read 1 of 21`, 20
  rows of `settings differ`, every check, the index and the label from one 1 s frame, exit 1.
- Should: both read the dominant setting, the stray frame left out.
- Deliberate: R-020 says "the first file sets"; R-007 says "dominant". Each as written; together
  they contradict on this input.
- Test: `R007_TheOddFrameFirstDoesNotNameTheGroup` covers the inventory only.
- Evidence: owner.
- Status: open

### CR-013 Frames are taken in file-name order as if it were shooting order

- Where: `src/DarksCheck.Core/Session/SessionFolder.cs:211-218` (`FramesIn`);
  `Analysis/CadenceAnalyzer.cs:82-123` (`Analyze`: `start ??= t`, `Math.Max(0, ...)` at `:106`);
  check 4 reads the end of the series in list order (`DarkSeriesAnalyzer.cs`, `WarmingOf`).
- What happens: `FramesIn` sorts by file name only, and no later step sorts by time, even when
  every frame has one. The cadence takes the first of the list as the start and clamps a
  backward step to 0; check 4 takes the last five of the list as the end of the series.
- Case: 20 darks renamed in Explorer to `dark (1).NEF` .. `dark (20).NEF`: the order is 1,
  10..19, 2, 20, 3..9. A back-to-back series reads several gaps over 5 s and backward steps
  print `0 s`; the "end of the series" is frames 5..9 in time, so the warming, the frames to
  discard and OK / WARNING / REDO are read on a scrambled series. The camera's counter going
  from `DSC_9999` to `DSC_0001` inside a series does the same.
- Should: by time when every frame has one, or at least a line that the times go back.
- Deliberate: R-002 and the note say "sorted by file name"; R-023 knows "a stamp goes back in
  series order" only for the fit's time axis.
- Also: `Analysis/DarkSeriesAnalyzer.cs:1044-1104` (`WarmingOf`) takes `ok[0]` as the first
  frame, `ok[^1]` as the last and the last five of the list as the end of the series. 30 darks
  named `dark_1.nef` to `dark_30.nef` sort 1, 10..19, 2, 20..29, 3, 30, 4..9: the "last five"
  are frames 5 to 9 in time, still warming. `DSC_9994..9999` followed by `DSC_0001..0006`: a
  warming series reads "the camera cooled" and the row prints a negative time.
  `Analysis/LightsAnalyzer.cs:300`, `:312`: "in the first light ... in the last" are the first
  and the last by name.
- Test: `R002_RawsWinOverFitsInTheSameFolderAndSortByName` pins the name order; none sets names
  against times.
- Evidence: owner (the order and the cadence were read; the verdict of check 4 on such a set
  needs a run). Same as LightsCheck's CR-010.
- Status: open

### CR-014 When about a third of the frames at one end carry light, no frame is named and check 3 can read OK

- Where: `src/DarksCheck.Core/Analysis/LeakAnalyzer.cs:146-206` (`FrameLightOf`), `:346-379`
  (`TheilSen`).
- What happens: the frame mean is fitted on the warming by the median of all pairwise slopes,
  the frames over `max(5 x scale, 0.1 x norm)` are left out and the line is fitted again. The
  median of pairwise slopes breaks down near 29 % of bad points: once the pairs that join a
  clean frame to a lit one outnumber the others, the line tilts through the lit frames, the
  scale grows, and the first pass finds nothing over the limit (`next.SequenceEqual(kept)`,
  `:173`).
- Case (the reviewer's Python replica; ratio rising evenly 0.8 to 1.2, scatter 0.01 ADU, the
  last k frames +0.6 ADU-eq, five seeds): 2 lit of 6, 3 of 8, 3 of 9, 7 of 20, 11 of 30: never
  named. 2 of 7, 3 of 10, 6 of 20, 10 of 30: always named. A lamp lights the frame evenly, so
  the map's edges do not depart either: `no frame is brighter than the warming explains`, OK.
- Should: the frames named, or the fit said to be untrustworthy.
- Deliberate: no. ADR 0012 says Theil-Sen "is not tilted" on the leak set (5 to 7 of 30). The
  limit is known in one comment, `DarkSeriesTests.cs:831-833`.
- Test: `R028_TheFitIsNotTiltedByLitFramesAtTheWarmEnd` uses 4 lit of 12, the last proportion
  that works. The same test with 3 of 8.
- Evidence: to measure (the mechanism was read; the table is the reviewer's replica).
- Status: open

### CR-015 A warming that rests on one cold frame gives the constant light an error several times too small

- Where: `src/DarksCheck.Core/Analysis/LeakAnalyzer.cs:268-292` (`SidesOf`, the error at
  `:286`), `:302-312` (`KeptForSplit`, the span at `:304`), `:188-205` (`RatioSpan = hi - lo`);
  judged by `LeakVerdict.IsConstantLight`.
- What happens: the line is Theil-Sen, its error the least-squares formula `scale x
  sqrt(1/n + mean(x)^2 / Sxx)`. One far frame makes `Sxx` large and the error small, but a
  median of pairwise slopes gains nothing from it. The gate `RatioSpan >= Th.LeakFitMinSpan` is
  max minus min, so one frame opens it.
- Case (the reviewer's replica: 30 frames, the first at ratio 0.5, the other 29 between 1.00
  and 1.03, no light): with a per-frame scatter of 0.02 ADU-eq the intercept scatters by 0.30
  while the row prints about +-0.04, and a side passes `> 0.15 and > 3 x error` in about 20 %
  of series. R-023's own examples include a first frame at 0.5 of the master.
- Should: UNMEASURED, or an error that matches the fit.
- Deliberate: ADR 0014 sets the formula, ADR 0012 the span of 0.1; neither considers a span
  one frame makes.
- Test: `R029_ASeriesThatDidNotWarmLeavesTheSplitUnmeasured` covers a flat series only.
- Evidence: to measure (the real per-frame scatter of a side decides how often).
- Status: open

### CR-016 A frame is checked against the first frame's size, ISO and exposure, never against its black and white

- Where: `Analysis/FrameStack.cs:190-204` (`Decode`: `geometry ??=` the first frame's), `:449-485`
  (`ToUshort`: the "0..1 or ADU" decision of a float FITS is taken per frame).
- Case: 12-bit and 14-bit NEF of one body in one folder (same size, ISO, exposure): the later
  frames are measured against the first's black (150 against 600). A float FITS set on 0..1 in
  which one frame holds a value just over 1: that frame is "kept in ADU" and rounds to zeros.
- Should: such a frame listed as "another scale", like another size.
- Evidence: confirmed on the code; the camera case is plausible only.
- Status: open

### CR-017 `--lights DIR` falls back to DIR's own FITS, so a stacked `result.fit` is measured as the one light

- Where: `Session/SessionFolder.cs:177-195` (`ChosenLights`).
- What happens: when `DIR\lights` is missing or holds no frame, the frames of DIR itself are
  taken. For the darks `Open` refuses exactly this ("never a fallback to the root, which holds
  the stacked results").
- Case: `darkscheck <library> --lights E:\MySession`, whose `lights\` is empty (raws archived)
  and whose root holds `result.fit`: `lights: E:\MySession (1 frame .fit; chosen by you)` and a
  check 2 on the stack.
- Should: `the chosen lights: no light in ...\lights` (the owner's call: R-055 states the
  fallback, for a bare folder of lights).
- Test: `R055_*` cover a folder with `lights\` and a bare folder, not a session root with an
  empty `lights\`.
- Evidence: owner. Kin to LightsCheck's CR-003.
- Status: open

### CR-018 The master's noise rests on the first two decoded frames alone

- Where: `Analysis/DarkSeriesAnalyzer.cs:945-955`, `:994-996` (`NoiseCurve`: the temporal sigma
  is the n = 1 point, frame 1 minus frame 2).
- Case: a first frame with noise 9 among frames of noise 3: a temporal sigma of 6.7 instead of
  3, so check 9's frames needed come out about five times too many. Check 5 names the frame as
  an outlier; check 9 does not leave it out. `BiasAnalyzer` takes the median of the consecutive
  pairs for the read noise.
- Deliberate: R-024 ("the n = 1 point is the temporal sigma") and the prototype.
- Evidence: owner.
- Status: open

## Severity 2: a crash, a hang, or a file written where it must not be

### CR-019 `--out` is refused by the spelling of the folder given, not by the folder

- Where: `src/DarksCheck.Cli/Program.cs:165-183`; `Report/DarkOutputs.cs:134-149`
  (`CheckOutDir`), called again by `WriteAll` (`:61-77`), so the window's export has the same.
- What happens: a string-prefix test of the full output path against `session.Root`, the
  partner's root and `ChosenLightsRoot`, each the folder as it was given.
- Case: `darkscheck E:\lib\X\darks --out E:\lib\X\report` writes into `X` (R-061 treats
  `<name>\darks\` as the library `<name>`), while `darkscheck E:\lib\X --out E:\lib\X\report` is
  refused. `--lights E:\S\lights --out E:\S\report` writes into the session `S`; `--lights E:\S`
  refuses it. A second name of the same folder (a `subst` drive, a junction, an 8.3 name) passes.
- Should: the first two refused as their other spellings are. No frame is touched and the files
  do not land among frames.
- Also: `--out \\?\E:\S\out` passes too, since `Path.GetFullPath` leaves such a path as it is.
- Test: `R026_OutInsideTheSessionIsRefused_Exit1` uses the session root only.
- Evidence: confirmed.
- Status: open

### CR-020 After a partner that failed, the window's export can write into the partner's folder

- Where: `Report/DarkOutputs.cs:62-71` (`WriteAll` refuses the partner only through `r.Partner`
  and `r.DarksFromPartner`); `Runs/DarkSeriesRun.cs:316-328` (a partner that throws leaves
  `partner = null` and `PartnerError`); `src/DarksCheck.Wpf/ViewModels/MainViewModel.cs:718-736`
  (`Export`). The console is safe: `Program.cs:165-177` checks against the opened partner first.
- Case: in the window, a partner whose darks are another size: the run ends with the partner's
  error in the report. `Write the files...` with the partner's folder picked: seven files are
  written into the library.
- Should: the `--out` refusal.
- Test: `R074_ExportWritesTheFilesIntoTheChosenFolderOnly` covers the session only.
- Evidence: confirmed on the code path; not run.
- Status: open

### CR-021 "Write the files..." can close the window when the session folder is unreachable or relative

- Where: `src/DarksCheck.Wpf/MainWindow.xaml.cs:205-222` (`ExportFiles`): `dlg.InitialDirectory =
  _vm.Folder` with no `Directory.Exists` and no `try`; the two sibling dialogs guard it
  (`ChooseLights`, `:110`).
- Case: a session on a share or a card that went away after the run; or the window started as
  `DarksCheck.Wpf.exe .`. The reviewer read WPF 8's source (`CommonItemDialog`, `ShellProvider`,
  dotnet/wpf release/8.0): `InitialDirectory` is passed as it is and only "not found" is
  absorbed; another failure throws from the click handler and the window closes with the result.
- Should: the dialog opened without a start folder (R-074: "an error becomes a line there, not a
  crash").
- Evidence: to measure (which error Windows gives for an offline share and for `.`); the
  missing guard is confirmed.
- Status: open

## Severity 3: a wrong text or a wrong display

### CR-022 Stop shows nothing, and the measuring steps ignore it

- Where: `Runs/DarkSeriesRun.cs:134-135` (the promise: "Stops the decoding and the measuring"),
  `:204`, `:233`, `:439`; `DarkSeriesAnalyzer.Analyze`, `BiasAnalyzer.Analyze` and `MedianMaster`
  take no token, and `Run` never checks it between steps.
- Case: a library of 60 darks, Stop pressed after the reading: the whole measuring runs and
  `Run` returns a result, which the window throws away on its own token. With ADR 0048, choosing
  a partner or lights during the measuring starts the new run at once while the old one still
  holds its stack: two stacks of up to 2.9 GB each.
- Also: in the window, `src/DarksCheck.Wpf/MainWindow.xaml.cs:80-84` (Stop only cancels) and
  `ViewModels/MainViewModel.cs:559-570` (after the cancel the progress callback drops every
  report, so the line and the bar freeze), `:574-582` (a run that finishes after Stop is thrown
  away). A library of 30 darks, Stop at `Measuring the darks: 7 / 30`: the screen stays on `7 /
  30`, the whole measuring runs, and only then `The analysis was stopped` appears; the same
  while the session is opened (every file's header is read, with no token).
  `R070_StoppingTheRunLeavesNoResultAndSaysSo` cancels before the pool work starts. Same as
  LightsCheck's CR-022.
- Test: no core test passes a token to `DarkSeriesRun.Run`.
- Evidence: confirmed.
- Status: open

### CR-023 The Lights tab cannot scroll: the lower rows of check 2 and the lights table are cut off

- Where: `src/DarksCheck.Wpf/Views/LightsView.xaml:78-83` (the root `Grid`, rows `Auto`, `Auto`,
  `*`), `:99-120` (the check 2 card and its chart), `:122-161` (the lights table, whose
  `ScrollViewer` at `:157` is the tab's only one).
- What happens: the tab is one `Grid` with no `ScrollViewer` around it. The card of the lights
  from another folder and the card of check 2 take what they need; the table gets what is left.
- Case: a session with lights, the window at its own design size of 1360 x 820
  (`MainWindow.xaml:29`): the tab has about 635 px of height, the first card takes about 170,
  and the check 2 card (nine rows, two five-line tables, three long sentences in a column about
  420 px wide) needs about 790 by the reviewer's estimate. The card is cut from about `lights
  against dark` down, the verdict included, and `LIGHTS TABLE` gets no height. Maximised on
  1920 x 1080 the card fits and about 47 px are left for the table.
- Should: every row and the table reachable at any size down to the minimum height of 560.
- Test: `WindowSmokeTests` renders this tab at a height of 1000 and `LanguageWindowTests` at
  1800, which hides it. The view laid out at 1360 x 820, asserting the table's `ScrollViewer`
  has a height and the verdict row lies inside the view.
- Evidence: to measure (the missing scroll was read; the heights are estimates, a screenshot at
  1360 x 820 on a session with lights settles them).
- Status: open

### CR-024 The Frames table loses its `>20` column and the reason of an unread frame on a window under about 1440 wide

- Where: `src/DarksCheck.Wpf/Views/FramesView.xaml:71-85` and `:127-141` (twelve fixed columns
  that add up to 792 px, then `*`), `:104-107` (`Skipped` and `Error` in the `*` column),
  `:113-116` (the tab's two columns, `3*` and `2*`).
- What happens: the table card gets three fifths of the tab's width; the rows' `ScrollViewer`
  has no horizontal scrolling, so what does not fit is clipped. The reason a frame was skipped
  or failed sits in the thirteenth column, which gets what is left.
- Case: the window 1360 wide (the design size): the table has about 748 px, the `>20` column
  spans 744 .. 792, so its header and every count are out of sight, and a dark that could not
  be read shows its number and its name and nothing else. At 1280 wide (1080 lines at 150 %)
  the `side` column is cut too. At 1920 wide all twelve show.
- Should: every column visible, or a horizontal scroll bar, and the reason shown across its row.
- Test: `R071_EveryColumnHeaderOfTheTablesHasAHint` renders at 1360 and reads the hints, not the
  positions.
- Evidence: to measure (the widths were added up; a screenshot at 1360 settles it). Kin to
  LightsCheck's CR-024.
- Status: open

### CR-025 The charts' tick labels are rounded more coarsely than their gridlines

- Where: `src/DarksCheck.Wpf/Presentation/Charts.cs:116-120` (`ChartElement.Num`), `:284-290`
  (the five ticks of `SeriesChart.OnRender`), `:269-270` (the 8 % padding).
- What happens: the five gridlines stand at `yMin + range x i / 4` on a range padded by 8 %, so
  never at round values; the label takes 0 decimals when the step is 1 or more, 1 from 0.1.
- Case: the lights chart with every light at or under 1.4: lines at 0.378, 0.756, 1.134, 1.512,
  labelled `0.4`, `0.8`, `1.1`, `1.5`; the gridline labelled `1.1` sits about 4 px above the
  green guide `1.1 as it is`. The chart of how much each frame differs, with a guide at 5.4:
  lines at 1.458, 2.916, 4.374, 5.832, labelled `1`, `3`, `4`, `6` on evenly spaced lines.
- Should: ticks at round values, or labels with the decimals their values need.
- Test: `TheChartRendersWithGapsGuidesAndAReference` only renders.
- Evidence: confirmed. Same as LightsCheck's CR-026.
- Status: open

### CR-026 The OK line of `match` says "the same exposure (X s)" when the lights' differs by up to 2 %

- Where: `Report/InventoryReport.cs:274-279` (`DarksVsLights`, case `Verdict.Ok`).
- Case: darks 120.2 s, lights 119.2 s (ADR 0010's own pair): rows `darks exposure: 120.2 s`,
  `lights exposure: 119.2 s`, then `OK: the darks have the same exposure (120.2 s) and the same
  ISO (...) as the lights`.
- Should: both named, or "within 2 %". A text change: shown to the owner first.
- Test: `R007_DarksThatMatchTheLightsAreOk` uses equal exposures.
- Evidence: confirmed.
- Status: open

### CR-027 The console's report and the written report point at "the Maps tab"

- Where: `Report/ScaleReport.cs:194`, `:197` (`On the map of the Maps tab`); golden
  `en_lights.txt:135`.
- Evidence: confirmed. A text change: shown to the owner first.
- Status: open

### CR-028 A count of one gets a plural word

- Where: `src/DarksCheck.Core/Report/DarkReport.cs:229-231` (`1 pixels above 20 ADU-eq`; the
  helper `Pixels(n)` at `:2035` knows the singular and is not used), `:1663` (`1 of 1 show`),
  `:1664` (`1 are missing from 2 or more`), `:1011-1012` (`1 frame has more light ...; take them
  out of darks\`), `:1559-1560` (`from 1 pairs of biases`), `:1398-1399` (`the 1 frames of this
  series`), `:1861-1862` and `:1999-2003` (`the stack of the 1 lights`);
  `Report/ScaleReport.cs:133-134`, `:176-177`, `:188`, `:190`, `:313`, `:320-321` (`made of 1
  frames`, `from 1 lights`); `Report/InventoryReport.cs:227` (`3 of 4 frames; the others have
  another exposure`).
- What happens: the counts are put before a fixed plural, in both languages. R-062 asks for
  `1 cadru`, `1 pixel` at a count of one.
- Case: a master with one hot pixel; one lit frame; two biases; one light; one odd frame of four.
- Test: the goldens have 0 or 20 at these places; `R007_AGroupWithMixedExposuresSaysSo` pins
  the plural for one frame.
- Evidence: confirmed. A text change: shown to the owner first. Same as LightsCheck's CR-019.
- Status: open

### CR-029 One report gives two figures for the noise of a light

- Where: `Analysis/StackNoise.cs:59-77` (the median of the sky sigma) against check 9's
  `StackLimit.LightNoise` (the median of the local sigma).
- Case: the golden `en_lights.txt`: check 9 says `the stack of the 5 lights (6.7 ADU of noise
  each)` and `the stack 17.9 % noisier`; the scale section `6.3 ADU on one light` and a stack
  from which 20.6 % follows.
- Deliberate: ADR 0051 (decision 3) leaves the scale section on the sky sigma as "a task of its
  own". The entry is that task; CR-003 (the unit mix) is fixed with it.
- Evidence: owner.
- Status: open

### CR-030 The count of pixels that "jump between two levels" takes in every strong steady hot pixel

- Where: `Analysis/DarkSeriesAnalyzer.cs:878-886` (`Stability`: temporal std over `6 x` a
  frame's noise); the words `Report/DarkReport.cs:1658-1666` (`Stability`).
- Case: on the D3300 at ISO 400 the limit is 9 ADU. A steady hot pixel of 200 ADU has a shot
  noise of 9.8 ADU, plus the series' warming: it is counted, and the row says it jumps between
  two levels. In `Golden/en_redo.txt` the hot pixels `vary by 45.34 ADU` because the camera
  cooled; on G1 all twenty would read as jumping.
- Deliberate: the count is R-031 and the prototype; the wording ADR 0030 / 0033.
- Evidence: owner.
- Status: open

### CR-031 The export dialog opens inside the folder the export refuses, and the refusal names a console option

- Where: `MainWindow.xaml.cs:207-211`; `Report/DarkOutputs.cs:134-149`; `MainViewModel.cs:731-735`.
- Case: after any result, `Write the files...` and `Select Folder` at once, or a new subfolder
  there: refused with `ERROR: --out cannot be the session's folder or one inside it`, an option
  the window's user never typed.
- Deliberate: R-074 ("proposing the session root", "refused with the `--out` message").
- Evidence: owner.
- Status: open

### CR-032 The hot-pixel limit the texts name is not the one the counts were taken at

- Where: `src/DarksCheck.Core/Report/ScaleReport.cs:128-130` (`Rows`, `this set's limits`) and
  `:194-197` (fourth paragraph); the limit used is `DarkSeriesAnalyzer.HotLevel`
  (`Analysis/DarkSeriesAnalyzer.cs:706`): `Math.Max(levelEq * norm, Th.MinHotAdu)`.
- What happens: the row prints `Th.HotAdu * g.Norm` without the floor of 5 raw ADU the analysis
  applies; the limit really used is kept in `HotStability.ThresholdAdu`, which the row does not
  read.
- Case: darks at ISO 400 and 20 s (factor 0.167): `hot pixel: above 20 ADU-eq (3 ADU in the
  file)`, while pixels were counted above 5 ADU, which is 30 ADU-eq. At ISO 100 and 30 s:
  `(1 ADU in the file)`; the real limit is 5 ADU, 80 ADU-eq. The same floor stands behind the
  shared pixels of the partner match and the overlap levels, whose texts say 20 and 50 ADU-eq.
- Should: the limit used, in both units.
- Also: `Report/DarkReport.cs:225-231`, `:1601-1611` (`Levels`): at ISO 400 and 30 s (factor
  0.25) 10 ADU-eq is lifted to 5 ADU in the file, which is 20 ADU-eq, and the row reads `above
  10: 800; above 20: 800`, the same count twice.
- Test: `R080_TheScaleSectionExplainsTheAduOfTheOpenSet` runs at factor 1 only.
- Evidence: confirmed.
- Status: open

### CR-033 An export into a folder that holds an earlier export leaves that export's files beside the new ones

- Where: `Report/DarkOutputs.cs:79-121` (`WriteAll`).
- What happens: the report, the darks CSV and the dark map are always written over same-named
  files; the biases CSV, the lights CSV and the two bias maps only when the run has them.
  Nothing removes or names files of the fixed names already there.
- Case: session A (biases and lights) exported into `D:\out`, then library B (neither) into
  `D:\out`: the report, `darkscheck-darks.csv` and the dark map are B's; `darkscheck-biases.csv`,
  `darkscheck-lights.csv` and two maps are still A's, and nothing says so. The same on a second
  run of one session whose biases or lights failed.
- Should: the stale files of the set removed, or the user told.
- Test: none. Two `WriteAll` into one folder, the second without biases.
- Evidence: confirmed.
- Status: open

### CR-034 Frames without a colour order: the CSV header breaks and `CFA[0,1]` reaches the texts

- Where: `Report/DarkOutputs.cs:177-184` (`DarksCsv`), `:222-227`, `:261-267`; the name from
  `Imaging/Mosaic.cs:139` (`Label`: `CFA[0,0]`).
- What happens: the header cells are `{plane}_median` and so on, joined with commas, not quoted;
  for an unknown colour order the plane's name holds a comma.
- Case: FITS darks without `BAYERPAT` (a mono camera, another capture program) with `--out`: the
  header of `darkscheck-darks.csv` has 80 more fields than each row (32 in the biases', 12 in
  the lights'); a spreadsheet shifts every column after `mean_above_black`.
- Also: the same name reaches the report's texts (`Report/DarkReport.cs:130`, `:180-186`,
  `:566`, `:1552`, `:1659`, through `PlaneNames` and `ScaleReport.PlaneWords`): `the first frame
  has 23 different values (the pixels CFA[0,1])`, `5 pixels CFA[0,1] jump between two levels`.
  R-082 lists `CFA` among the words no row may hold.
- Test: every fixture is RGGB. A FITS series without `BAYERPAT`, header fields == row fields.
- Evidence: confirmed.
- Status: open

### CR-035 A float FITS is described as "16 bits"; the float wording of R-080 can never appear

- Where: `Report/ScaleReport.cs:42` (`MinIntegerWhite`), `:97`, `:113-117`, `:158-160`; the cause
  `Analysis/FrameStack.cs:461-474` (`ToUshort` sets the white to 65535 for every float FITS).
- Case: a float FITS on 0..1: the header line says `FITS of 32 bits with decimals, scaled to
  0 .. 65535`, the scale row `values from 0 to 65535 ADU (16 bits)`. R-080 wants `values with
  decimals (floating point), from 0 to <white>`. The branch is dead code.
- Test: none contains `floating point` or `virgula mobila`.
- Evidence: confirmed.
- Status: open

### CR-036 With a sky at or under the black, the sky row gives figures while the paragraph says the session has no lights

- Where: `Report/ScaleReport.cs:132-135` (row `sky`) against `:185-192` (`st.SkyAdu > 0`).
- Case: lights whose median on the reference plane equals the black (narrowband or very short
  lights; FITS lights that take the darks' black): the row reads `the sky is at 0.0 ADU above the
  black, with 3.1 ADU of noise`, and the paragraph `the session has no lights ... Open the
  session together with its lights`.
- Evidence: confirmed.
- Status: open

### CR-037 Check 7 prints an empty warning when an edge is over the threshold but is not glow

- Where: `Report/DarkReport.cs:1731-1745` (`Pattern`); the verdict
  `Analysis/DarkVerdicts.cs:308-311`.
- What happens: the verdict is Warning when the banding passes `Th.BandingAdu` or the map's
  offset passes `Th.LeakAdu`; `GlowAdu` is `leak.Split?.GlowAdu ?? offset`, so with a measured
  split it holds only the glow part. The words test `|GlowAdu| > Th.LeakAdu` again; with neither
  flag set both parts are empty.
- Case: R-029's own example, darks warming 0.8 to 1.2 with a constant +3 ADU left edge: `WARNING:
  ; without darks this pattern stays in the lights`.
- Should: name what exceeds (R-040), or not warn.
- Test: `R040_FixedRowBandingOnEveryFrameIsAPattern` covers the banding only.
- Evidence: confirmed.
- Status: open

### CR-038 Check 7 calls the worst banding "row" when it is the columns'

- Where: `Report/DarkReport.cs:1738-1743`; `BandingP2pAdu` is the largest of rows and columns
  (`DarkVerdicts.cs:308`).
- Case: 2.5 on the columns and 1.0 on the rows: `WARNING: fixed banding of 2.50 ADU-eq between
  the brightest and the darkest row`, the row above showing 1.00 for the rows. In every
  synthetic golden the columns are already the larger.
- Evidence: confirmed.
- Status: open

### CR-039 The doubling-rule sentence rounds the user's setting to a whole degree

- Where: `Report/DarkReport.cs:1369-1371` (`F(nz.DoublingCelsius, 0)`).
- Case: the setting 5.5 prints `the thermal signal doubles every 6 degrees`, while every degree
  of the report is worked out with 5.5. The gain setting beside it prints two decimals and
  "(your setting)".
- Evidence: confirmed.
- Status: open

### CR-040 A small factor prints as "0.00 ADU"

- Where: `Report/DarkReport.cs:899` (`F(d.Norm, 2)`), `:287-288`; `ScaleReport.cs:122`, `:169`.
- Case: ISO 400 at 0.5 s (norm 0.0042): `1 ADU-eq : 0.00 ADU in the file`. An ISO or exposure
  present but 0 makes `NormOf` return 1 and the row reads `1.00 ADU in the file at ISO 0`
  instead of the "files do not give" sentence (line 890 tests only for null).
- Evidence: confirmed.
- Status: open

### CR-041 File names over 20 characters are cut in the inventory tables

- Where: `Report/InventoryReport.cs:146` (`WriteTable`).
- Case: `Dark_120.0s_Bin1_gain100_20241012-213455_0001.fit`: every row reads
  `Dark_120.0s_Bin1_gai`, and an `error:` row no longer says which file failed.
- Evidence: confirmed.
- Status: open

### CR-042 A folder name with letters outside the console's code page prints as `?`, the label included

- Where: `src/DarksCheck.Cli/Program.cs:46-81` (`Main`); `Console.OutputEncoding` is set nowhere
  under `src/` (searched).
- Case: `darkscheck "E:\Astro\Smida"` spelled with a comma-below S would print `=== SESSION:
  E:\Astro\?mida ===` and a label `?mida-idx25`, the name the owner is meant to copy; a
  redirected `> report.txt` is in the OEM page while `--out`'s report is UTF-8.
- Evidence: to measure (a run from an ordinary cmd window on such a folder).
- Status: open

### CR-043 Guide labels of the charts are drawn over one another when their lines come close

- Where: `src/DarksCheck.Wpf/Presentation/Charts.cs:356-363` (the guides of
  `SeriesChart.OnRender`: each label right-aligned above its line, on a dark backing, in list
  order); the guides from `ViewModels/MainViewModel.cs:856-865` and `:880-887`.
- Case: lights whose band is widened to its cap: `LightsAnalyzer.cs:374` gives `DirectLow =
  max(0.7, ...)`, exactly `Th.MatchOptLow`, so the green line `0.70 as it is` is overdrawn by
  the yellow `0.7 with -opt` and its label covered, while the sentence above says "Green
  between 0.70 and ...". Lights at three times the dark: the labels at 0.7, 0.9 and 1.1 are
  about 12 px apart and clip each other. A steady series whose end is within 0.03 of the
  master: `the master` and `end of series 1.02` are under 7 px apart.
- Should: labels moved apart, or coincident guides under one label.
- Evidence: to measure (the coincident 0.70 case follows from the code; the near cases need a
  screenshot). Same as LightsCheck's CR-035.
- Status: open

### CR-044 The bold of a figure stops in the middle of `ADU-eq`

- Where: `src/DarksCheck.Wpf/Presentation/Emphasis.cs:94` (`Segments`: a mark is bold where it
  is not preceded by a digit, a point or a sign and not followed by a digit); the marks from
  `Report/ScaleReport.cs:310-313` (`MasterNoiseRow`).
- Case: a master noise of 0.60 ADU with a factor of 0.9933 (darks of 119.2 s): the row reads
  `... still has 0.60 ADU (0.60 ADU-eq) of noise per pixel` and the only mark is `0.60 ADU`. Out
  comes bold `0.60 ADU`, then, inside the brackets, bold `0.60 ADU` followed by plain `-eq`.
- Should: only the first bold (ADR 0016: a mark counts "only where it stands as a figure of its
  own").
- Test: `MainViewModelTests` tests `Segments` for a leading and a trailing digit only.
  `Emphasis.Segments("0.60 ADU (0.60 ADU-eq)", ["0.60 ADU"])` gives two bold pieces.
- Evidence: confirmed.
- Status: open

### CR-045 A figure wider than its table cell loses its first characters, not its last

- Where: `src/DarksCheck.Wpf/Views/FramesView.xaml:30-36` (the style `Cell`: right-aligned, no
  trimming) with the widths at `:71-85`; the same style in `LightsView.xaml:29-35`.
- Case: the `>20` cell holds five characters; a count of 1800000 (plausible on data scaled to 16
  bits, where 20 ADU is inside the noise) shows as `800000`. The `light` and `side` cells hold
  six: `-1234.56` shows as `1234.56`, the sign gone. On the D3300 the figures fit.
- Should: the whole figure, or an ellipsis with a tooltip, as the file name has.
- Evidence: to measure (a synthetic row with a seven-digit count; which end is cut).
- Status: open

### CR-046 "About as much as a flat is off on average" follows whatever percentage the sky gives

- Where: `Report/ScaleReport.cs:187`, `:189`; and `:181`, `:184` (`the X ADU of noise of one dark
  hides any error below 1 ADU`, said also when X is under 1).
- Case: a sky at 30 ADU: `1 ADU is 3.33 % of the sky, about as much as a flat is off on
  average`; a sky at 4000 ADU: 0.03 %, the same comparison.
- Evidence: owner (texts approved under ADR 0028; no ADR speaks of the range).
- Status: open

### CR-047 A lights folder that cannot be used leaves the window on the problem card with no way out

- Where: `MainViewModel.cs:529-545` (`OpenAsync` keeps the choice for the same folder), `:693-698`;
  `MainWindow.xaml:134-146` (the card's two buttons: another folder, partner); the control that
  clears the choice is on the Lights tab, hidden without a result.
- Case: with a result on screen, `Choose the lights...` and a wrong folder (one with only
  JPEGs): the result is gone and the card shows `the chosen lights: no light in ...`. `Choose
  another folder...` with the same session fails the same way, and so does `Choose partner...`.
  The only escape is to open a different folder first, then the session again.
- Should: the card offers to drop the chosen lights, or the choice is dropped when it fails.
- Test: `R122_...` ends by asserting the card appears.
- Evidence: confirmed.
- Status: open

### CR-048 A relative folder on the window's command line stays relative

- Where: `src/DarksCheck.Wpf/App.xaml.cs:153-169` (`FolderFromArgs`, "as given");
  `MainViewModel.cs:547`, `:670` (`RerunAsync` passes it again).
- Case: from `E:\Astro\S` start `DarksCheck.Wpf.exe .`: the top bar and the log read `.`. Every
  rerun (a lights or partner choice) resolves it against the current directory of that moment;
  WPF 8's folder dialog does not ask Windows to keep the current directory, so a rerun could
  analyse another folder under a top bar that still says `.`.
- Evidence: confirmed that it stays relative; whether the dialog moves the current directory is
  to measure. Same as LightsCheck's CR-050.
- Status: open

### CR-049 A lights-only session with a bad `--against`: the console's error lacks `the partner:`

- Where: `src/DarksCheck.Cli/Program.cs:165` (the console opens the partner itself, so
  `SessionFolder.Open`'s bare message is printed; the prefixed one of `Runs/DarkSeriesRun.cs:177`
  is not reached from the console).
- Case: `darkscheck <lights-only> --against E:\typo --lang ro` prints `EROARE: nu gasesc
  folderul E:\typo`; R-054 says `partenerul: <its message>` in the console too.
- Test: only the window's (`MainViewModelTests`).
- Evidence: confirmed.
- Status: open

### CR-050 A folder whose name starts with a verdict word gives the label row a verdict

- Where: `Report/ReportRows.cs:98-110` (`VerdictOf`: the verdict is read off the start of any
  row's value); the `label` row `DarkReport.cs:451`.
- Case: a folder named `OK darks 2C`: the label row has the verdict `OK` and the rest `darks
  2C-idx25`.
- Evidence: confirmed in the core; what the window draws (a chip) is to measure.
- Status: open

### CR-051 "The session has no biases" and "no lights" are said of sessions that have them

- Where: `Report/ScaleReport.cs:369-382` (`ReadNoise`), `:135`, `:191-192`, `:325`.
- Case: (a) a session with one bias: the read noise is NaN under two frames and the row says
  there are no biases. (b) Biases of another size: `r.Biases` is null, the header has a WARNING
  about them, the row says there are none. (c) Lights that all fail: `no lights in the session`.
- Should: the reason. Same kind as LightsCheck's CR-038.
- Also: `Report/DarkReport.cs:1545-1561` (`ReadNoise`): one bias prints `read noise : R -  G1 -
  G2 -  B - ADU (from 0 pairs of biases ...)`; biases of another size read `UNMEASURED: the
  session has no biases` under a header that warns they could not be used (`:1547-1550`, also
  `:984`).
- Evidence: confirmed.
- Status: open

### CR-052 "The series started cold / warm" is said of frames at the end, or on the other side

- Where: `Report/DarkReport.cs:1421-1427` (`Drift`): one clause chosen by `FarthestRatio < 1` for
  all the frames outside 0.7 .. 1.4, wherever they stand.
- Case: `Golden/en_redo.txt:31` and `redo.txt:31`: `5 frames (1 .. 3, 11, 12) are too far from
  the master ... (the farthest 1.93): the series started warm; discard them`; frames 11 and 12
  are the last two, on the cold side. A golden line that states it: reported, per the rule
  "Golden files".
- Test: `R023_WhenTheLastFramesShowNoHotPixelsTheSeriesHasNoEnd` asserts only the start of the
  verdict.
- Evidence: confirmed.
- Status: open

### CR-053 A figure rounded at a limit contradicts the word beside it

- Where: `src/DarksCheck.Core/Report/DarkReport.cs:400-401` and `:485-486` (`442 of 443
  (100 %)`), `:1208-1209` (a span of 0.097: `vary by only 0.10 of the master, at least 0.1 is
  needed`), `:1029-1030` (an offset of 1.003: `by +1.00 ADU-eq ..., over the threshold of
  1.0`), `:1425` (0.698: `outside 0.7 .. 1.4; the farthest 0.70`), `:366-368` and `:840` (a
  trace of 0.997: `1.00 of the noise of a light` under "under the noise of a light"),
  `:2008-2009` (1.996: `(2.00 ADU) is under the file's value step (2 ADU)`), `:679-686` (frames
  at +1.96 and +2.04 degrees both show `+2.0`, one with the note);
  `Analysis/CadenceAnalyzer.cs:108` with `Report/InventoryReport.cs:154`, `:244`, `:258` (a gap
  of 5.4 s is counted as "over 5 s" and printed as `5 s`).
- What happens: the test is on the exact value, the text prints it rounded. Two such cases are
  already guarded (`EndDegrees`, `StackPercent`; R-023 and R-024 ask for it there).
- Test: none at these edges.
- Evidence: confirmed. Same as LightsCheck's CR-017 and CR-040.
- Status: open

### CR-054 The step row reads "go up 0 by 0 ADU" when no step was found

- Where: `Report/ScaleReport.cs:123`, `:170`, `:173`; `Analysis/DarkSeriesAnalyzer.cs:493-521`
  (`Quantisation` returns 0 under two values near the black); also `DarkReport.cs:130`.
- Case: values more than 40 ADU apart near the black (10-bit data stored times 64), or a
  constant frame.
- Also: `Report/DarkReport.cs:130-131`, `:1923-1925`, `:2008-2012` print the same 0, while check
  9 judges with 1 (`Analysis/DarkVerdicts.cs:341`): `the master's noise (0.40 ADU) is under the
  file's value step (0 ADU)`.
- Evidence: confirmed on the text; the input is rare.
- Status: open

### CR-055 A library kept as `darks\` at a drive root is labelled `-idx25`

- Where: `Analysis/IndexAnalyzer.cs:313-325` (`Label`): for `E:\darks` the parent is `E:\`,
  whose file name is empty.
- Should: the folder's own name when the parent has none (R-061 wants a name in front of the tag).
- Evidence: confirmed.
- Status: open

### CR-056 An exception outside the six handled types ends the run with an empty window and no word

- Where: `MainViewModel.cs:589`, `:603-621` (anything else is logged and leaves in a task nobody
  awaits; `finally` sets `IsRunning = false`); the four states of `MainWindow.xaml:114-161` are
  then all hidden.
- Case: an `OutOfMemoryException` from a full-frame array in `DarkSeriesAnalyzer.Analyze` after
  `MAX FRAMES` was raised: the wait screen vanishes and the window shows the folder name over an
  empty area.
- Deliberate: `window.md` ("anything else is a bug and propagates"); "propagates" here means
  nothing on screen.
- Evidence: owner.
- Status: open

## Severity 4: robustness and cleanliness

### CR-057 `--out` takes an option as its folder; `--inventory` drops `--threads` and `--max-frames` in silence

- Where: `src/DarksCheck.Cli/Options.cs:79-87` (the value is `args[++i]` whatever it is) and
  `:167-170`.
- Case: `darkscheck E:\S --out --inventory` runs the whole analysis and writes into a folder
  named `--inventory` in the working folder. `darkscheck X --inventory --threads 4 --max-frames
  5` is accepted, both ignored, while `--out` and `--against` with `--inventory` are refused. A
  repeated option keeps the last value without a word. `--lang` takes `--lang=ro` and any case;
  every other option is case-sensitive with no `=` form (`--out=DIR` reads `unknown option`).
- Evidence: confirmed. Same as LightsCheck's CR-045.
- Status: open

### CR-058 An `--out` that cannot be created is found only after the whole analysis

- Where: `src/DarksCheck.Cli/Program.cs:163-183` against `Report/DarkOutputs.cs:79`.
- Case: `--out Q:\reports` with no drive Q:, or a path that is a file: minutes of decoding and
  the report first, then `ERROR: Could not find a part of the path ...`, exit 1. The comment at
  `Program.cs:163` says a bad `--out` "is refused before the minutes of decoding".
- Evidence: confirmed.
- Status: open

### CR-059 After Ctrl+C the log has no last line, and an export under way stays half-written

- Where: `src/DarksCheck.Cli/Program.cs:63-80` (`Main`); no `Console.CancelKeyPress` or
  `ProcessExit` handler anywhere under `src/` (searched); the run is called without a
  cancellation token (`Program.cs:186`).
- Evidence: confirmed for the missing line. Same as LightsCheck's CR-049.
- Status: open

### CR-060 A failed export leaves a mixed set of files, and the console names none of those it wrote

- Where: `Report/DarkOutputs.cs:82-119`; `src/DarksCheck.Cli/Program.cs:193-213`.
- What happens: the files are written one after the other onto their final names; the first
  failure leaves the ones before new and the ones after old. The console prints its `-> written:`
  lines only after `WriteAll` returns; the written files are named only in the log.
- Case: `darkscheck-darks.csv` of an earlier export open in Excel: the report is replaced, the
  CSV write throws, `ERROR: The process cannot access the file ...`, exit 1; nothing on screen
  says the report was replaced.
- Test: `R101_TheExportTellsEachFileAsSoonAsItIsWritten` never makes a write fail.
- Evidence: confirmed. Same as LightsCheck's CR-048.
- Status: open

### CR-061 A folder that is not a folder of darks opens as a library

- Where: `Session/SessionFolder.cs:121-137` (`Open`): only subfolders named `lights`, `biases`
  or `flats` mark a session; the folder's own name is not looked at.
- Case: `darkscheck E:\S\lights` analyses the lights as darks and proposes the label
  `lights-idx...`. A session root whose only FITS is `result.fit`, with subfolders named
  otherwise (`process\`, `masters\`), reads `result.fit` as its one dark.
- Evidence: confirmed.
- Status: open

### CR-062 A partner that is the open folder itself is read again and reported as a match

- Where: `Runs/DarkSeriesRun.cs:318-322`, `:425`; no guard in `Program.cs:165` or
  `MainViewModel.ChoosePartnerAsync`.
- Case: `darkscheck X --against X`: the darks are decoded twice, INDEX prints `darks / partner
  ... 1.00 times`, `shared pixels 100 %`, `partner verdict : OK`. ADR 0011 removed this
  self-comparison for the lights-only case "since a ratio of 1.00 on every pixel says nothing".
- Evidence: confirmed.
- Status: open

### CR-063 Progress counts can arrive out of order, so a phase can end on "31 / 32"

- Where: `Analysis/LightsAnalyzer.cs:250`, `Analysis/FrameStack.cs:183`, `:224`
  (`progress?.Report((Interlocked.Increment(ref done), n))`: the count is taken and reported in
  two steps from the parallel bodies).
- Also: `Analysis/DarkSeriesAnalyzer.cs:472` has the same pattern; and during the masters, the
  first part of the measuring, the line still reads `Reading the darks: 20 / 20`.
- Evidence: confirmed. Same as LightsCheck's CR-043.
- Status: open

### CR-064 Memory: the thread count has no upper bound, the frame cap counts files, and a Debug build keeps the released stacks

- Where: `src/DarksCheck.Core/Runs/DarkSeriesRun.cs:42` (`EffectiveThreads`: half the
  processors, no upper bound; `--threads` in `Options.cs:88-96` and the window's field in
  `MainViewModel.cs:464`, `:767` have none either, and the window stores the text);
  `Analysis/LightsAnalyzer.cs:203`; `Analysis/FrameStack.cs:167`, `:216-222` (the cap of 60
  counts frames whatever their size); `Runs/DarkSeriesRun.cs:203-215` and `:230-239` (the
  stacks are locals of `Run`, followed by `GC.Collect()`).
- Case 1: `40` typed for `4` in the window, or 32 threads on a machine with 16 GB: each light
  in flight holds about 0.5 GB (the decoder's doubles, the mosaic, two full-size rasters). An
  out-of-memory in a parallel body is caught as that frame's "error" and the night is read on
  the rest.
- Case 2: 60 darks of a 61 Mpx camera are 7.3 GB of mosaics plus the masters (ADR 0007 sizes
  the cap for 24 Mpx).
- Case 3: unoptimised code keeps every local alive to the end of the method, so a Debug run
  with 30 biases, 60 darks and a partner of 60 would hold three stacks where the note promises
  one. `CLAUDE.md`'s Run command (`dotnet run --project src/DarksCheck.Cli`) is a Debug build.
- Also: a FITS frame in flight holds the file's bytes and two arrays of doubles, about 480 to
  530 MB at 24 Mpx, where ADR 0007 and `FrameStack.cs:141` count 194 MB.
- Evidence: to measure (the missing bounds were read; the peaks need runs with the log at
  Debug, which prints the working set). Same as LightsCheck's CR-046.
- Status: open

### CR-065 "Infinity" and "1e999" pass as valid settings

- Where: `MainViewModel.cs:773` (`ParseDouble`: parses and `v > 0`), `:459-461`.
- Case: `1e999` in `SIGNAL / ISO STOP` with darks at ISO 800: an infinite factor, every ADU-eq
  0, the index and the verdicts follow; no red border. No upper range either (`20` for `2.0`).
- Evidence: confirmed for the parse; the figures downstream were not traced.
- Status: open

### CR-066 Without a local application data folder the settings would be written relative to the current directory

- Where: `src/DarksCheck.Wpf/UserSettings.cs:52-53`, `:87-93`.
- Case: `GetFolderPath` returns an empty string: `DarksCheck\settings.json` under the current
  directory, which can be the session folder (the rule "Read-only input").
- Evidence: to measure.
- Status: open

### CR-067 Restore Down can put the title bar above a low screen

- Where: `MainWindow.xaml:29-32` (`Height="820" Width="1360"`, centred, maximised at start).
- Case: 1920 x 1080 at 150 % (work area 1280 x 680): the restored window's top is about 70 px
  above the screen. Same as LightsCheck's CR-037.
- Evidence: to measure.
- Status: open

### CR-068 Table columns that overflow

- Where: `Report/DarkReport.cs:83`, `:550`.
- Case: the `>20` column is 4 wide and shifts from 10000 pixels; `low` and `high` are 5 wide (a
  negative ratio, or one of 10 and up); `sky` from 10000.0, `mean` from 1000.00.
- Evidence: confirmed.
- Status: open

### CR-069 The long hints close after five seconds; Tab stops on things that are not controls and the focus mark is black on dark

- Where: `src/DarksCheck.Wpf/Theme/Dark.xaml:81-106` (the `ToolTip` style; nothing sets
  `ToolTipService.ShowDuration`, searched); `Theme/Rows.xaml:86` (the chip's `ContentControl`)
  and `:91` (the lines' `ItemsControl`), focusable by default, while `:105` switches it off for
  the table's; `Theme/Dark.xaml:189-218`, `:246-270`, `:329-370` (templates with no focus cue).
- Case: the hint of the `side` header (about 330 characters) and the table note on the last
  header (about 700) vanish after 5 s. On the Summary tab, Tab from the tab header to the
  dropdown of check 9, the tab's only control, stops on every row's line list and every chip,
  about 70 stops, each marked by the system's dotted black frame on #2B2B2F. The six charts and
  the three maps give a screen reader nothing.
- Evidence: to measure (the defaults were read; a run confirms the count and the look). Kin to
  LightsCheck's CR-055 and CR-056.
- Status: open

### CR-070 Thresholds typed as numbers in the code and the texts

- Where: `src/DarksCheck.Core/Analysis/LeakAnalyzer.cs:137` (`MinKept = 3`) and
  `Report/DarkReport.cs:1232` (`SplitUnmeasured(l, 3)`): the split's minimum typed twice, tied
  by nothing; `Report/DarkReport.cs:1385` (three times the time constant, the 5-minute step),
  `:1439` and `:1466` (0.05 for "level with", beside `Th.SameTemperatureCelsius`), `:355`
  (0.005), `:1663` ("half" for `Th.HotKeepFraction`), `:1143` (8 lit frames shown), `:911` (the
  two tolerances of `AtReference`); `Report/ScaleReport.cs:118-122`, `:169`, `:172` (`ISO 400
  and 120 s`, where `DarkReport` reads `Th.NormIso`); `Report/DarkOutputs.cs:179` (the header
  names `above10 .. above500`, while the cells come from `Th.HotLevelsAdu`);
  `src/DarksCheck.Wpf/ViewModels/WindowTexts.cs:144`, `:147` ("the five thresholds");
  `src/DarksCheck.Wpf/Views/HotPixelsView.xaml:37`, `:47` and `LightsView.xaml:106` (the check
  numbers `6.`, `9.` and `2.` typed in headings; a renumbering like ADR 0046's must touch them).
- Case: `MinKept` changed to 4: with three frames left the split is empty, but
  `SplitUnmeasured(l, 3)` returns null and the row is built as if it were measured.
- Should: one home, `Thresholds.cs`, as its summary says.
- Evidence: confirmed. Same as LightsCheck's CR-054.
- Status: open

### CR-071 Figures and verdicts decided in the report, not in the analysis

- Where: `src/DarksCheck.Core/Report/DarkReport.cs:681` (the off-end test taken again), `:1023`
  (`|offset| > Th.LeakAdu`), `:1733-1734` (the banding and glow flags, the cause of CR-037),
  `:1289` ("same temperature"); figures computed there: the per-frame degrees from the end
  (`:679`), the per-frame side figure (`:677`), the read-noise pairs (`:1559`), the halves of
  the master row (`:1954-1955`); `Report/ScaleReport.cs:84-99` (the bit depth, the gain).
- What happens: the report is in the core, so the rule "Computed only in the core" holds; but a
  decision taken twice can come out two ways, as check 7's empty warning shows.
- Should: the owner's call how far the wording code may decide.
- Evidence: owner. Same as LightsCheck's CR-057.
- Status: open

### CR-072 No gate test decodes a raw, reads a pixel of a map, or has frames without a colour order

- Where: `tests/DarksCheck.Tests/RawReaderTests.cs:77-123`
  (`R008_ARealRawDecodesToAnUndebayeredMosaicWithItsScaleAndMetadata` returns at once without
  `DARKSCHECK_NEF`, so `Decode`, `ReadInfo` and `FillShootingInfo` run in no gate test; given a
  dark NEF, this tool's own subject, it fails at `:97-99`, which demands that the four positions'
  medians differ by more than 5 %); `tests/DarksCheck.Tests/SyntheticDarks.cs:63` (every
  fixture is RGGB, at factor 1 or a whole stop, with names in time order);
  `AcceptanceTests.R026_OutWritesTheReportTheCsvFilesAndTheMaps` (the maps checked by existence
  and eight signature bytes).
- What happens: the gaps the entries above sit in: no test with frames without `BAYERPAT`, with
  a factor under 0.5, with names against times, with biases of another ISO or pedestal than the
  darks, with one bias, one light or one dark in the texts, with a token given to
  `DarkSeriesRun.Run`, with a write that fails, with the window laid out at its design size.
- Evidence: confirmed. Kin to LightsCheck's CR-058.
- Status: open

### CR-073 Core tests that cannot fail or do not check what their name says

- Where, all under `tests/DarksCheck.Tests/`:
  - `AcceptanceTests.R005_NumbersUseTheDotWhateverTheCulture`: the project has
    `InvariantGlobalization` and the test sets no culture.
  - `AcceptanceTests.R003_ACorruptRawIsListedWithItsErrorAndTheRunGoesOn_Exit1`: the corrupt
    `.NEF` makes the fixture's `.fit` drop out (raws win), so "the run goes on" is not exercised.
  - `DarkSeriesTests.R027_ACorruptFirstFileLeavesTheGeometryToTheNextInOrder`: the four good
    frames are alike; "the next in order" is not checked.
  - `DarkSeriesTests.R020_LevelsFloorAtFiveAduAndTheDeadLimitIsRawWhateverTheNormalisation`: the
    dead pixel sits 200 under black, so a scaled limit also gives one.
  - `DarkSeriesTests.R021_ABrighterEdgeIsALeakAndTheBiasTakesTheOffsetOut`: the biases are flat.
  - `DarkSeriesTests.R041_BiasesOfAnotherSizeAreReportedAndLeftOut`: "left out" is checked on
    `Biases` and `DarkCurrent` only (CR-011).
  - `DarkSeriesTests.R028_TheFitIsNotTiltedByLitFramesAtTheWarmEnd`: sits on the last
    proportion that works (CR-014). `R029_TheSidesNeedFiveFramesLeftAfterTheLitOnes` sets
    `LitFrames` by hand on the record.
  - `DarkSeriesTests.R023_WhenTheLastFramesShowNoHotPixelsTheSeriesHasNoEnd`: `StartsWith` stops
    before the clause that is wrong (CR-052).
  - `DarkSeriesTests.R080_TheScaleSectionExplainsTheAduOfTheOpenSet`, line 908: `line.Length <=
    96 || line.Contains(':')` lets any line with a colon pass.
  - `LightsAndIndexTests.R052_LightsWithoutTheMastersHotPixelsAreUnmatchedNotUnmeasured`: the
    index assert accepts NaN and any negative (CR-004).
  - `LightsAndIndexTests.R060_TheIndexIsTheMedianOfTheRankBandOfTheUnsaturatedExcess`, last
    block: the comment says "the single strongest pixel", the asserts pin ranks 1 .. 2, and
    `Assert.Contains("... : idx1", ...)` matches any tag starting with `idx1`.
  - `LoggingTests.R101_TheExportTellsEachFileAsSoonAsItIsWritten`: would pass if every callback
    came after the last file.
  - `EnglishGoldenOutputTests.Check`: the two languages' numbers are matched without their
    signs and sorted, so a sign or an order that differs passes.
  - `ThirdPartyTests.R010_EveryComponentHasAHeadingInTheNoticesFile`: `h.Contains(c.Name)`, so
    `LibRaw` is satisfied by `## Sdcb.LibRaw`.
  - `VersionTests.NoProjectOverridesTheSharedVersionOrCopyright` returns in silence when a
    project file is not found; `VersionTests.TheBannerIsTheNameAndTheVersion` repeats the
    expression it tests; `RepoInvariantTests.Manifest_ListsEveryFixtureInRepo` checks nothing
    without a `fixtures` folder; `LicenseHeaderTests.TheScanSeesEveryProjectAndThePrototype`
    guards four of the seven scanned folders.
  - `RawReaderTests.AFileThatIsNotActuallyARawIsRefused`: `ThrowsAny<Exception>`, so the
    "libraw cannot be loaded" exception passes too.
    `R008_AFabricatedFrameOfAnyMakeCarriesItsLensIntoFrameInfo` runs only `FrameInfo.From`.
  - `FitsReaderTests.Load_StillReturnsTheSameRasterAsLoadWithHeader` compares a call with
    itself; `LoadWithHeader_ReturnsTheCardsOfTheHduThatHeldTheImage` has one HDU.
  - `CfaPatternTests.TheCropOriginShiftsTheMosaicPhase` pins the order for odd margins, which
    the shipped libraw never passes; `RefusesAnythingThatIsNotABayerOrder` has no case with two
    greens and no red or no blue.
- Evidence: confirmed for the ones named in an entry above, whose lines the session opened; the
  others were spot-checked, not opened one by one. Kin to LightsCheck's CR-059.
- Status: open

### CR-074 Window tests that cannot fail or do not check what their name says

- Where, all under `tests/DarksCheck.Wpf.Tests/`:
  - `MainViewModelTests.TheNumbersAreParsedWithTheInvariantCulture`: sets no culture; its last
    assert tests .NET.
  - `CultureTests.ForcingInvariantNumbersAlsoCoversPoolThreads`: the culture flows into
    `Task.Run` anyway, so it would pass with the line it is named for removed (to measure).
  - `MainViewModelTests.R070_StoppingTheRunLeavesNoResultAndSaysSo` and
    `WindowLoggingTests.R101_ACancelledRunIsLoggedAsCancelledNotAsAFailure`: both cancel before
    the pool work starts.
  - `WindowLoggingTests.R101_ASupersededRunIsKeptInTheLogThoughTheScreenShowsNothing`: the first
    run is replaced before it starts; the branches for a replaced run's result or failure are
    not reached.
  - `MainViewModelTests.R070_OpeningAFolderRunsTheWholeAnalysisByItself`:
    `Assert.InRange(vm.Progress, 0, 1)` cannot fail.
  - `AboutWindowFitTests.R121_TheWorkAreaIsTheOneOfTheWindowsScreen`: cannot tell Windows'
    answer from WPF's on a one-screen PC, and compares two rectangles for exact equality.
  - `LanguageWindowTests.R085_...`, `R086_...`, `R087_TheIndexTabIsInPlainWords` and
    `R094_EveryTabRendersInEnglish`: the rows' text is set by `Emphasis` in a deferred
    dispatcher call the tests never pump, so those text blocks are empty when read (read, not
    run); the words are still covered through `vm.Report`.
  - `WindowSmokeTests.TheChartRendersWithGapsGuidesAndAReference`: no tick, guide or point
    position is asserted. `R071_EveryTabRendersWithRealResultsAndNoBindingErrors` renders at
    1360 x 1000 or taller, which hides CR-023 and CR-024.
- Evidence: confirmed by the reviewers' reading; spot-checked by the session. Kin to
  LightsCheck's CR-060.
- Status: open

### CR-075 Design notes, requirements and comments that describe other code

- Where:
  - `src/DarksCheck.Core/Analysis/DarkVerdicts.cs:263`, `:276`, `:291`, `:306`, `:313`:
    `Judge`'s comments keep the old check numbers (2, 3, 4, 6, 7) beside `// 9.` (ADR 0046).
  - `Analysis/Planes.cs:108-109`: says both figures are NaN for an empty plane; the sigma is 0
    (CR-005).
  - `Thresholds.cs:375-379` (`MatchMinAdu`): says the lights are matched above it; they use
    `Th.HotAdu` (both 20).
  - `Analysis/CadenceAnalyzer.cs`: `frames` documented "in shooting order"; every caller passes
    name order (CR-013).
  - `Runs/DarkSeriesRun.cs:134-135`: `cancel` "stops the decoding and the measuring" (CR-022).
  - `src/DarksCheck.Cli/Program.cs:163-164`: a bad `--out` "is refused before the minutes of
    decoding" (CR-058); the usage's exit-code line names one cause of exit 1 of five.
  - `Version.cs:25`: "nothing is hard-coded here" above the fallback copyright line (R-011).
  - `Imaging/RawReader.cs:313-314` ("the sum is what rawpy reads"), `:291-292` and
    `Imaging/CfaPattern.cs:92-95` (the margins shift the phase; the shipped libraw passes even
    margins), `:404-405` (the example contradicts `imaging.md:44-46`); `Imaging/FitsReader.cs:18`
    ("copied ... unchanged" above a line that names two changes).
  - `src/DarksCheck.Wpf/ViewModels/MainViewModel.cs:224-228`, `:745` (the partner "stored"; ADR
    0041 removed that), `:408-411` ("and how many lights are measured"), `:514-520` ("any other
    exception propagates", CR-056); `MainWindow.xaml:104`; `Theme/Dark.xaml:26`, `:524` ("derived
    from Stats.Severity", which DarksCheck does not have); the chart sentences say "dotted" for
    lines drawn dashed.
  - `docs/design/lights-and-index.md`: under 0.1 Mpx "the band collapses to the strongest
    pixel" (the code takes the second strongest; R-060 says the same); names
    `FrameStack.NormOf` (the code calls `Normalisation.NormOf`); says `Unmeasured` for a ratio
    that is not positive (the code and R-062 say `Unmatched`).
  - `docs/design/session-and-console.md`: a folder with the flag is lights-only "whether its own
    `lights\` holds frames or the lights were chosen"; the code also needs `lights\` to exist.
  - `docs/design/window.md:5` (R-122 and R-123 are the window's too), `:21` ("the six tab
    views"; there are seven), "with the path unescaped" (no path is stored any more).
  - `docs/design/imaging.md:5-7` ("unchanged but for the namespace", then six hunks), `:28`
    (`LoadInfo` reads the primary HDU's header only), `:95-98` (the time claim CP-5 disproves).
  - `docs/requirements.md`: R-004 quotes `folderul e gol; da calea ...` (the code and R-088:
    `calea folderului e goala`); R-053 gives the progress line as `masurare lights: 3 / 32`
    (the code and R-025: `Se masoara lights-urile`); R-082's example names check 4's first row
    `pixeli fierbinti` (it is `sfarsitul seriei`).
- Evidence: confirmed for the code comments the session opened (`DarkVerdicts`, `Planes`,
  `Program`, `DarkSeriesRun`, `RawReader`); the notes and requirements were spot-checked.
  Same as LightsCheck's CR-061.
- Status: open

## The files copied from LightsCheck

The ten files of `src/DarksCheck.Core/Imaging/` are copies: from LightsCheck, which copied
FlatCheck (ADR 0003). A defect there is fixed in its source first, then the copy is refreshed
(the rule "Copied files stay copies"); the task that takes a `CP` entry starts in the source
project, with the entry of that project's list that names it. Both source lists are of the same
day: `LightsCheck/docs/review/2026-11-13-code-review.md` and FlatCheck's. What the defect does
here is said for darks, which are not lights: a dark's whole signal sits a few ADU above the
black.

The copy also lacks three fixes its sources already have: the `SIMPLE` check of a FITS
(FlatCheck's ADR 0057; LightsCheck took it, so ADR 0003's condition for dropping the
`FrameInfo.Read` detour is met), the row flip (CP-1) and the refusal of a compressed FITS
(CP-9). FlatCheck's CR-032 (the colour order shifted by libraw's margins) does not hold with
the shipped libraw, by the area 7 reviewer's reading of `open.cpp`: an odd margin is moved by
one before the sizes are saved, so the offsets passed at `RawReader.cs:294` are always even.

### CP-1 A FITS is read with its rows in file order, so a Siril FITS is upside down against the raw

- Severity 1, some setups (FITS frames from Siril beside raws; severity 3 for the names alone).
- Where: `src/DarksCheck.Core/Imaging/FitsReader.cs:313-337` (`LoadFull`), `:126-134` (`Load`);
  `Imaging/CfaPattern.cs:158-163` (`ForRowOrder`, which only moves the Bayer phase); shown by
  `Report/DarkOutputs.cs:358-367` (`MapRaster`) and the side names of check 3.
- What happens: row 0 of the raster is the file's first row. Siril writes the sensor's bottom
  row first (`ROWORDER = 'BOTTOM-UP'`); a raw through libraw has the sensor's top row first.
- Case 1: NEF darks with `--lights` pointing at Siril FITS lights, or a FITS library as partner:
  the sizes match, so nothing is refused, and a hot pixel at row y of the master sits at row
  H-1-y of the other side. Check 2 reads "the lights do not show the dark's hot pixels", or the
  partner is matched on no shared pixel, for a master that fits.
- Case 2: one dark set with light at the eyepiece side, once as NEF and once as the FITS Siril
  made of it: the NEF run names the `top` side and draws the band at the top of the map, the
  FITS run says `bottom`.
- Should: the same way up from both.
- Source: FlatCheck changed its reader on 2026-11-09 (its ADR 0094, R-130): the file's first
  row goes last, and `ForRowOrder` answers for that raster. LightsCheck's copy has not taken
  it. Case 1 is a known limit "not addressed" in ADR 0008; R-012 states the file-order reading.
- Test: `FitsReaderTests.Load_StoresPixelsRowMajor` and
  `ABottomUpFrameOfEvenHeightShiftsTheOrderByOneRow` pin today's behaviour; no test reads a
  pixel of a map.
- Evidence: confirmed (the reading order; the two report texts need a run to quote).
- Status: open

### CP-2 A DNG whose black level is a 2 x 2 pattern is read with black 0

- Severity 1, some setups (a DNG with `BlackLevelRepeatDim 2 2`: Pentax, Leica, Ricoh, phones,
  Adobe DNG Converter).
- Where: `Imaging/RawReader.cs:313-318` (`Decode`: `black + cblack[0..3]`).
- What happens: by the reviewer's reading of LibRaw 0.21.1, such a black lands in `cblack[6+]`
  with `black` and `cblack[0..3]` at 0, and nothing folds it back.
- Case: on darks this is the worst entry of the section: the header reads values from 0, each
  pixel's level above black is the pedestal (64 to 1024) plus the dark current, so the hot
  counts are the whole sensor and the index and its label are the pedestal's.
- Source: LightsCheck's CR-009.
- Evidence: to measure (one decode of such a DNG with the log at Debug shows `black` and `per
  channel`).
- Status: open

### CP-3 A Fujifilm X-Trans raw is accepted, and check 2 treats 2 x 2 positions as colours

- Severity 1, some setups (X-Trans bodies).
- Where: `Imaging/CfaPattern.cs:108` (`FromLibRaw` returns null for the 6 x 6 mask),
  `Imaging/RawReader.cs:279-294` (the mosaic is still returned); read by
  `Analysis/LightsAnalyzer.cs:420-442` (`Measure`) and `:535-560` (`LocalExcess`).
- Case: on the darks themselves nearly harmless (a dark has no colour; the planes are named
  `CFA[0,0]` and so on). On the lights of check 2 the "eight same-colour neighbours" two pixels
  away are other colours: with a sky of 500 (G), 350 (B) and 250 (R) ADU a green pixel's local
  excess is about +75 with no hot pixel there, the local sigma about 100, and the night's
  ratio and verdict are noise, without a warning.
- Should: refused, or check 2 unmeasured, for a mosaic that is not 2 x 2.
- Source: LightsCheck's CR-008.
- Evidence: to measure (one X-Trans session).
- Status: open

### CP-4 A raw's four channel blacks are folded into one number

- Severity 1, some setups (a camera whose channels do not share one black), owner.
- Where: `Imaging/RawReader.cs:313-318`, `:335` (the median of the four, which with four values
  is the mean of the two middle ones); used as one black for the whole stack, from the first
  decoded frame (`Analysis/FrameStack.cs:195`, `:243`).
- Case: a body where libraw gives per-channel blacks a few ADU apart and measures them per file
  (Canon is the usual example): with 2046, 2048, 2048, 2049 the red plane reads 2 ADU low and
  the blue 1 ADU high, on darks whose whole signal is a few ADU. On the D3300 the four are
  equal (150).
- Deliberate: R-008 ("the median of the four channel blacks") and `imaging.md`.
- Evidence: owner; the size of the effect is to measure (one CR2 dark with the log at Debug).
- Status: open

### CP-5 A raw stamped in the hour the clocks skip comes back an hour off

- Severity 3, rare.
- Where: `Imaging/RawReader.cs:400-402` (libraw's time through the PC's zone and back).
- Case: a series through 03:00 to 04:00 on the last Sunday of March: one pause comes out about
  an hour long, so check 1 reports a gap that did not happen, and the series' minutes get a
  one-hour jump. `docs/design/imaging.md:95-98` says the round trip gives back the camera's
  clock to the second. A shell that sets `TZ` would shift every raw's time.
- Source: LightsCheck's CR-044.
- Evidence: to measure.
- Status: open

### CP-6 The CSV shows a raw's exposure with single-precision noise

- Severity 3, common.
- Where: `Imaging/RawReader.cs:396-399` (libraw's single-precision `Shutter` widened to
  double); written by `Report/DarkOutputs.cs:189`, `:230`, `:270` with the round-trip format
  (`:377`).
- Case: NEF darks of 119.2 s write `119.19999694824219` in `exposure_s`; a 1/4000 s bias
  `0.0002500000118743628`. The reports and the log hide it; the factor moves by 2.6e-8.
- Source: LightsCheck's CR-028.
- Evidence: to measure (one NEF with `--out`).
- Status: open

### CP-7 A damaged raw's row carries the binding's own English text

- Severity 3, crafted (a copy cut by a bad card is the ordinary way).
- Where: `Imaging/RawReader.cs:107-120` (`Load`), `:129-142` (`LoadInfo`): only the "libraw
  cannot be loaded" exceptions are caught and reworded.
- Case: a NEF of zeros in `darks/`: the row reads `error:` followed by `Failed opening file:
  ...` or `ErrorCode: ...`, English in a Romanian run and no plain words.
- Source: LightsCheck has a hunk for it (`RawReader.Unreadable`) that `imaging.md:74-79` says is
  not taken here because it serves `--frame`.
- Test: `RawReaderTests.AFileThatIsNotActuallyARawIsRefused` accepts any exception.
- Evidence: confirmed on the path; the exact text needs a run.
- Status: open

### CP-8 A FITS with a negative axis hangs the run

- Severity 2, crafted.
- Where: `Imaging/FitsReader.cs:284-304`, `:340` (`NAXISn` and `BITPIX` are not checked).
- Case: `BITPIX = 8, NAXIS1 = -2880, NAXIS2 = 2` after a one-block header: the data size is
  -2880, the position returns to 0 and the loop never ends. As the first file it hangs the
  caller (`FrameStack.cs:230-233`); later it holds one worker for ever, and Stop cannot end it.
  A missing `BITPIX` with large sides is an out-of-memory, caught here as that frame's row.
- Source: LightsCheck's CP-1 (FlatCheck's CR-013).
- Evidence: confirmed (the arithmetic was followed).
- Status: open

### CP-9 A table or compressed HDU is read as an image

- Severity 3, rare (only such a file that keeps a `.fit` name is listed).
- Where: `Imaging/FitsReader.cs:284-338` (no `XTENSION`, `PCOUNT` or `ZIMAGE` is looked at).
- Case: a whole report on an 8-column raster of bytes, its white 255 and its black its own
  median.
- Source: LightsCheck's CP-2; FlatCheck fixed its reader (its R-136, ADR 0105).
- Evidence: confirmed.
- Status: open

### CP-10 Two header parsers: the inventory and the stack can disagree, and a cut header looks whole

- Severity 3, rare.
- Where: `Imaging/FitsReader.cs:78-112` (`LoadHeader`: the primary HDU only, its own value
  parser, and whatever it read when the file ends before `END`, `:89`) against `:264-282`.
- Case: an empty primary HDU plus an image extension: the inventory and check 1 show no time,
  exposure or ISO, while the stack normalises with the extension's values. A header cut before
  `END` gives an inventory row that looks whole (LightsCheck's copy now throws there).
- Source: LightsCheck's CP-3 and CP-4.
- Evidence: confirmed.
- Status: open

### CP-11 A header number with a comma or a D exponent is read wrong

- Severity 1, rare (a writer other than Siril).
- Where: `Imaging/FitsReader.cs:399-407` (`HInt`, `HDbl`: `NumberStyles.Any`).
- Case: `EXPTIME = 120,2` reads 1202 s, so the factor, the hot levels and the index are ten
  times off. `BZERO = 3.2768D+04` (valid FITS) falls back to 0, so unsigned 16-bit data is read
  as signed and a dark's 150 ADU is clamped to 0: a frame of zeros with no error.
- Source: LightsCheck's CP-6.
- Evidence: confirmed.
- Status: open

### CP-12 Colour orders that are not Bayer pass

- Severity 4, crafted.
- Where: `Imaging/CfaPattern.cs:73-89`.
- Case: `BAYERPAT = 'GGRB'` names the positions G1, G2, R, B and makes position 0 the reference
  plane. Names only; no crash.
- Source: LightsCheck's CP-5.
- Evidence: confirmed.
- Status: open

## Cleanup

Small things with no wrong output today. A task that touches the file may take them along;
none needs a task of its own. Spot-checked by the session, not opened one by one.

- Dead or unused: the static `FrameStack.NormOf(FrameInfo)` (`FrameStack.cs:428`);
  `PlateauFit`'s `Rms`, which its only caller discards (`DarkSeriesAnalyzer.cs:1212`);
  `LightsAnalysis.HotCountHistogram`, computed over every pixel and read only by tests;
  `Cadence.IntegrationSeconds` and `DutyPct`; `LanguageArgument.Given`; the parameter `inv` of
  `InventoryReport.WriteTable`; the float branch and `MinIntegerWhite` of `ScaleReport` (dead
  through CR-035); the `catch (ObjectDisposedException)` of `MainViewModel.CancelPending`;
  `MainViewModel.Tag`; the `Bars` branch of `SeriesChart`, `ChartPoint.Label`,
  `VerdictPalette.Fair` and its `ERROR` branch; the theme's `MetricRowTemplate`, `Number`, `Not`
  and the ListBox and CheckBox styles; the relaxed JSON encoder of `UserSettings`.
- In the copied files, with no caller outside `Imaging/` (most say so in their comments;
  `ImageLoader.LoadImage` and `FitsReader.FitsExtensions` do not): `BitmapFont`, much of `Img`,
  `Mosaic` and `Stats`, `PngWriter.Colormap` and `Write`.
- Done twice: `MedianMaster` fills a mean and a deviation raster it throws away (192 MB at 24
  Mpx) while the partner's stack is in memory, and `MatchPartner` checks for the error it
  throws two lines later; the two `Planes.Extract` overloads; the `MapRegion` switch
  (`DarkSeriesAnalyzer.cs:81-91`, `:221-231`); `Th.OverlapLevelsAdu`, which repeats the first
  four of `Th.HotLevelsAdu` by value; `DriftVerdict.FramesWithRatio`, recounted with the filter
  `WarmingOf` used; `FrameLight.FramesKept` against `KeptForSplit`; the `1 ADU-eq` row of
  `ScaleReport` against `DarkReport.Normalisation`; `ScaleReport.Rows` and `Paragraphs`, which
  each call `Figures.Of`; the list `.fit, .fits, .fts` typed in `SessionFolder.cs:144` and
  `:192`; the styles `Cell`, `LeftCell`, `Head`, `LeftHead` in `FramesView.xaml` and
  `LightsView.xaml`; the attention colour typed as hex in `FramesView.xaml:49-50` and
  `Dark.xaml:55`; the Bayer chain in `FitsReader.cs:132-134` and `:169-171`; the guard and
  catch list in `RawReader.cs:109-119` and `:131-141`.
- Fragile: `Th.HotLevelIndex` searches on every read; `DarkSeriesAnalysis.PlaneNames` builds a
  list on every read; `RankIndex` rounds the ranks to even while the tag rounds half up;
  `MatchFigures.Pixels` holds the number of lights for the lights; `DarkOutputs.N` would print
  `Infinity`; a map without regions (frames under 32 px) reads check 3 as unmeasured even when
  frames were named lit; `EnoughVerdictText` says "a single frame is not a series" for any
  unmeasured cause; the settings file is not written atomically, and two windows overwrite each
  other's four values; `--lang` takes `--lang=ro` and any case while every other option is
  case-sensitive with no `=` form; the window project sets `ApplicationHighDpiMode`, a Windows
  Forms property, and has no manifest, so it is probably system-DPI aware (to measure);
  `ScaleView.xaml:24` puts a margin where every other tab uses padding; the tooltip of `Write
  the files...` does not name the chosen lights' folder, which the export also refuses;
  `MainWindow.xaml:27-28` declares two prefixes for one namespace.
- Not settled, noted so the next reader does not start from nothing: whether a D3300 FITS that
  another program scaled to 16 bits gets its electrons 16 times too high (`CameraFacts.For`
  matches the name alone and the table is per 12-bit ADU); whether an idle window that holds an
  old log file goes on writing into it after another process's rolling deletes it; whether a
  cut uncompressed raw comes back with its lower rows at zero and no error; a float FITS with
  NaN samples, cast to 16 bits at `FrameStack.cs:480-481`.

## What the review found sound

So that a later reader does not go over the same ground: the formulas of the estimators, the
masters, the stability, the noise curve and the fit of the series' end match the design note,
R-020 to R-041 and the prototype; `Planes.Select`, `Median` and `Percentile` agree with a sort
on 200,000 random arrays (area 1's port). Every verdict's boundary agrees with the requirement
that states it, and every constant of `Thresholds.cs` is used and, but for CR-070, not typed
again (area 2). The local detector at the image edges matches the prototype (area 3). Every
line `DarkReport` writes carries the same figures in the same order in both languages, the
tables' headers end where their figures end, and no old check number is left in a user text
(area 4). `--out` inside the session root is refused before the analysis, and again by
`WriteAll` (area 5, the seams). The log's folder and fallback, its share mode for a second
instance, its hooks and `RunClock`; the CSV cell counts and the invariant culture in every
cell; the PNG encoder (area 6). The readers are stateless, each raw load has its own libraw
context, and no handle leaks (area 7). Overlapping runs cannot be reached from the screen: the
inputs are disabled while a run goes; a new result leaves no stale row; the view-model computes
no figure (area 8). Every binding path of the seven views, all 92 language pairs of the XAML
and every theme resource (area 9).

## Found later

Entries added after the review, by the task that met them (ADR 0052, point 7).

### CR-076 Sets that give the exposure and no ISO are compared as made at the same settings, though their exposures are known to differ

- Found: 2026-11-14, by the two reviews of the fixes of CR-006 to CR-010.
- Where: `Analysis/Normalisation.cs` (`Says`: a factor needs the ISO and the exposure, both;
  `NormOf` returns 1 otherwise); `Analysis/LightsAnalyzer.cs` (`FactorOf`, and
  `LightFigures.OtherSettings`, which asks both sides to say both settings);
  `Runs/DarkSeriesRun.cs` (`MatchPartner`: `ownSays`, `partnerSays`).
- What happens: a set whose frames give the exposure and no ISO (a capture program that writes
  `GAIN` and no `ISOSPEED`) has no factor, so it is compared with another set as made at the
  same settings (ADR 0053), and the known exposure is not used. Since ADR 0055 such a pair is
  not "at other settings", because nothing was brought from one to the other.
- Case: (a) darks of 300 s and lights of 120 s, no ISO anywhere, the hot pixels as high in both
  files: check 1 reads a red `WARNING: the darks (300.0 s) do not have the lights' exposure
  (120.0 s)`, and check 2 reads `OK: the dark can be subtracted as it is ... the dark is at the
  same temperature`, with a WARNING row that the lights were taken at the darks' settings. The
  heights in the files do match, so the subtraction is right as far as the hot pixels go; the
  temperature is not known, the index and the degrees are in the file's ADU. (b) the session's
  darks of 120 s against a partner of 300 s, no ISO on either side: no row says that the
  exposures differ (check 1 does not look at a partner), the partner's row prints `unknown
  settings`, and the verdict can read `OK: the partner can be subtracted as it is`.
- Should: the owner's call. One direction: when neither set gives the ISO and both give the
  exposure, take the ISOs as equal and bring the sets to one exposure, with a row that says so;
  the pair is then judged as any pair at other settings (ADR 0055). Another: keep the
  comparison and say the known difference in the verdict.
- Deliberate: ADR 0053 ("a set that does not say its settings is compared as made at the other
  set's"); ADR 0055, decision 1; ADR 0058 names case (a) in its consequences.
- Test: `R052_SetsThatDoNotSayBothSettingsAreNotAtOtherSettings` pins that such sets are not at
  other settings and that nothing is said to be worked out; no test pins the verdicts above.
- Evidence: owner.
- Status: fixed 91508347 (the owner chose: a setting both sets give is used, one a set does not give is taken from the other, one neither gives is taken as the same and said in a WARNING row; ADR 0060)

### CR-077 Darks without an ISO between lights and a partner at two ISOs: the chained verdict can read `as it is`, and two rows take the darks' ISO two ways

- Found: 2026-11-14, by the review of the fix of CR-076.
- Where: `Runs/DarkSeriesRun.cs` (`MatchPartner`: the chained row, `ownAt`, `partnerAt` and
  each light's place; `IndexAnalyzer.Chain`); ADR 0060, decision 5.
- What happens: the lights are compared with the partner through the session's darks, one pair
  at a time. A setting the darks do not give is taken from the lights in the first pair and
  from the partner in the second, so it drops out of both, and a setting the lights and the
  partner both give is never compared between the two.
- Case: darks of 120 s without an ISO, lights at ISO 800, a partner at ISO 400, all as high in
  the files. Check 2 says `the darks do not give ...; what they do not give was taken as the
  lights'`, INDEX says `the session's darks do not give ...; what they do not give was taken
  as the partner's`, the chained ratio reads 1.00 and the verdict `OK: the partner can be
  subtracted as it is ... at the same temperature`. From the two files alone the lights are
  at twice the partner's ISO: the ratio is 0.50, the partner 6 degrees warmer, and the pair is
  at other settings (ADR 0055). The same holds for the exposure.
- Should: the lights against the partner on the settings those two give, a missing one taken
  through the darks: the chained ratio in the files over the factor of the light over the
  partner, the product of the two pairs' ratios kept when all three give both settings, so
  that no figure of such sessions moves.
- Deliberate: the rule of the second review of CR-006 to CR-010 (ADR 0055, decision 1: a set
  that does not say both its settings stands, in the chained row, where the set it was compared
  with stands), which the fix of CR-076 had kept setting by setting.
- Test: `R064_ASetThatDoesNotSayItsSettingsStandsWhereTheOneItWasComparedWithStands` pinned
  `LightsOther` null for this case, and not the verdict; the fix split it, and
  `R064_TheLightsAreComparedWithThePartnerOnTheSettingsThoseTwoGive` holds the case now.
- Evidence: confirmed by reading; the verdict's words were not run.
- Status: fixed 581dd50a (the lights are compared with the partner on the settings those two give, a missing one taken from the darks; the two rows stay, each true of its own pair; ADR 0060, decision 5)

## How the list is used

One entry per task, in the table's order unless the owner picks another; an entry that names a
group is one task. The task follows the kit's rules "Requirements first" and "Test before fix",
and the rules "Measured on the sessions", "Plain words" and "Golden files". An entry marked
*to measure* starts by showing the case; one marked *owner* starts with the question to the
owner. A `CP` entry starts in the source project. The task closes its entry here, in the
summary table and in the entry: `fixed <commit>`, or `rejected: <reason>`. The list is not
rewritten otherwise; a defect found later is a new entry at the end.
