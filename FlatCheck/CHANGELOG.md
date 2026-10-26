# Changelog

All notable changes to this project. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/);
versions follow [SemVer](https://semver.org/). The version number itself lives in
`Directory.Build.props`; the owner bumps it.

## [Unreleased]

### Added

- Lightroom Classic's and Lightroom's own lens-profile folders,
  `C:\Program Files\Adobe\<app>\Resources\LensProfiles\1.0`, are read too, after the two Camera
  Raw folders (R-107, ADR 0066): a Lightroom-only installation now fills the Adobe half of the
  list, and the 102 files (72 lenses) Lightroom has beyond the DNG Converter's are listed. A file
  present in two folders is read once, from the first; the sources summary says how many
  copies were skipped.
- The window's End to end tab explains itself in place, in both languages (R-105, ADR 0064):
  a line under each of the four series figures saying what it is and how it is judged, a
  tooltip on each column header of the frames table, and a paragraph under the radial map
  saying what the picture is and what its colours mean. The thresholds in the texts are read
  from `Th`, never retyped.
- False colours for the `--histeq` picture, after Siril 1.4.4's rainbow table (R-104, ADR
  0063): `--rainbow` beside `--histeq` writes the mosaic (or the mono channel) coloured from
  magenta at the lowest level to red at the highest, and prints the legend; the window's
  Histogram tab gets a "False colours" chip beside the view chips, on the Mosaic, the channel
  and the mono views, never on RGB. The colours are ranks, not values. `--rainbow` without
  `--histeq` is an error.
- The console speaks English or Romanian, chosen by `--lang en|ro` (or `--lang=ro`), anywhere
  on the line; English when it is not given (R-100, ADR 0062). The usage, the parse errors, the
  reports, the verdict words (`REDO` for `REFA`, `GOOD` for `BUN`...), the refusals and the
  error prefix follow it; the figures and the exit codes do not move. A value other than `en`
  or `ro` stops with an English error and exit 1 (R-102). The Romanian is the text as it was.
- The window speaks English or Romanian too, with the same `--lang` on its command line,
  English by default (R-101): tabs, labels, guides, messages and reports. A value it does not
  understand is said in an English message, and the window goes on in English.

- The window's `Split` tab (R-091 to R-093, ADR 0054): divides the flats of a session into 2
  or 4 consecutive groups, builds a temp session per group (every folder the script enters,
  at most 2 lights, the group's flats), runs the chosen Siril script in each, and compares the
  part masters with the whole session's master: the five tests with a verdict per cell, the
  vignetting depth, one row per lens profile (the Structure tab's list) with the difference
  and its verdict, the whole's nearest profile marked as the reference, and a verdict per
  column, the worse of shape and depth against that reference. Before the run the tab shows
  a disk estimate (copies exact, Siril's `process/` from a measured bytes-per-pixel model,
  free space on the temp drive).
  Saves `Split result.txt` and `flatcheck-split.png` (the residual maps side by side on one
  scale, labelled with a built-in bitmap font, version and date in the title) in the session
  root; the original session is only read. Version 2.3.0 at the owner's request.

- `ParityGoldenTests` (R-098): the 43 scenarios the Python cross-check used to run are golden
  files now, `Golden/parity_*.txt`, captured on 2026-09-26 while `flatcheck.py` still agreed on
  every number (43 scenarios, 0 numeric differences). They carry the prototype's last confirmed
  figures into the gate; a re-approval there is a C#-only decision (ADR 0047).

### Changed

- The second maps the console writes have English names in both languages (R-103): `--ratio`
  writes `<name>-world.png` (was `-lume`), `--smooth` writes `<name>-after.png` (was `-dupa`).
- The files the window writes have English names in both languages (R-101): `Test result.txt`
  (was `Rezultat testare.txt`), `Split result.txt` (was `Rezultat split.txt`),
  `pp_flat_stacked_smoothed.fit` (was
  `_netezit`), `pp_flat_stacked_synthetic.fit` (was `_sintetic`), the split's `part-<i>`
  folders. Files with the old names are no longer recognised: rename them to keep them.
- The window's Expo and End to end tabs share more of their sentences with the console (the
  review of 2026-10-25 after the documentation backfill's area 4): the "not a raw flat"
  reasons on the Expo card are now the console's lines, each one paragraph (`--expo are
  nevoie de...`, where the card used to say `Modul are nevoie de...`), only the way out
  staying the window's; the End to end field line is the console's value unpadded (`30.0
  grade  (WCS)`, `nedeterminat (FOCALLEN=20mm, fara marimea pixelului)`, `necunoscut
  (headerul nu da nici focala, nici scara)`), and a refusal judged from the focal length
  alone now says, as the console does, that the decision assumed the largest plausible
  sensor (R-083). The console texts are unchanged.
- The window lets one heavy run compute at a time, whatever is clicked (ADR 0061, R-081): the
  analyses started by opening a folder and every run a click starts (Ratio, Expo, Histogram,
  Netezire, Sintetic, the Split's analysis) take one gate, so a Histogram or Expo pick during
  the opening chain waits for it as a Ratio pair already did, two clicks wait for each other,
  and a new folder waits for a run of the old one still computing. Measured 2026-10-25 on the
  24 MP masters: 0.7-1.25 GB of peak working set per run alone, 1.3-2.2 GB for two at once.
- While a run waits its turn, the waiting screen says so (`Astept sa se termine analizele
  pornite la deschiderea folderului...`) instead of naming work that has not begun; the run's
  own message takes over once it starts (R-082, R-084, R-085).
- The FITS reader checks the file's magic: a file that does not begin with the `SIMPLE` card
  fails with `FITS: fisierul nu incepe cu SIMPLE.` in both reads, the full load and the
  metadata-only one (`ImageLoader.LoadInfo`), and a header cut before `END` fails in both with
  `FITS: header incomplet.` (R-008, ADR 0057). The metadata read used to answer either with an
  empty header, so the window's Sintetic tab now says why its prefill failed on a master that
  is not a FITS; the console names the real fault on a `.jpg` given by mistake instead of
  `header incomplet` or a parse error further in.
- `--help` names `--out` beside `-o` for the output map, as the README and R-006 do; the
  golden `help.txt` was re-approved for that one line.
- The Python prototype is frozen at 2.2.0 and no longer maintained (ADR 0047, supersedes 0005
  and 0038): rule 16 now asks for `/validate` after a formula change instead of the cross-check,
  `scripts/release.ps1` writes `<Version>` only, and `check-staged.ps1` and the Stop hook no
  longer treat `flatcheck.py` as code. `flatcheck.py` and `crosscheck.py` stay in the repository
  as prior art; `CrossCheckFixtures` can still dump the fixtures for a comparison by hand.

### Fixed

- A FITS whose data area is shorter than its header declares (a copy cut mid-transfer) is
  refused with `FITS: data cut short: the header announces <n> bytes of pixels, the file holds
  only <m>.` and exit 1, instead of an `IndexOutOfRangeException` out of the reader's sample
  loop (R-009). A file missing only its last padding block still loads.
- The window no longer fails to open when Siril's settings folder (`%LOCALAPPDATA%\siril`) or
  an uninstall entry in the registry cannot be read: the Siril lookup treats it as not found,
  as it already did for a missing folder; one unreadable uninstall entry no longer hides the
  entries after it.
- Siril's settings are no longer put at risk by FlatCheck (R-094, ADR 0055): every `siril-cli`
  run the window starts (`Ruleaza in Siril`, each Split part) gets its own byte copy of Siril's
  config (`config.1.4.ini` here), passed with `-i` in `%TEMP%\FlatCheck\siril-<guid>` and
  deleted after the exit; the Siril integration tests run on an empty private copy in their
  own workspace. Without it siril-cli rewrote that file at the end of every script, setting
  `wd` to the session or to a Split temp folder, and a failed save deletes it: measured on
  2026-09-30, 5-10 % of saves on the owner's machine, after which the next run saves Siril's
  defaults. That is how the gate's integration tests lost the owner's selected scripts, StarNet
  and GraXpert paths and focal length on 2026-09-26. When the config exists but cannot be read,
  the run does not start and the message names the file.

### Removed

- `scripts/crosscheck.ps1`, its permission line in `.claude/settings.json`, and the two tests
  that tied `__version__` and `__copyright__` in `flatcheck.py` to the C#.

### Internal

- The build writes the XML documentation file in all five projects, so the gate's compiler
  fails a malformed comment, a tag naming no parameter or an unresolved `cref`; CS1591 stays
  muted and no publish ships the files (ADR 0059). The 34 errors it found on `main` are fixed.
- Every comment is English from now on, tests, XAML and project files included (ADR 0060); the
  documentation backfill translates the Romanian ones in five areas while it writes the XML
  comments the kit's ADR 0035 asks for on every type and method. All five areas done: every
  type and method of the core, the console and the window is documented, and every comment of
  the five projects, tests, XAML and project files included, is English; the rule's temporary
  exception for files in two languages is gone from `CLAUDE.md`.
- The window no longer words or computes anything of its own: the Expo tab's verdict, advice,
  channel lines and caption, the End to end tab's field line, refusal and reasons, and the
  notes under the structure rows moved to `Report/` (`ExposureReport`, `LightReport`,
  `LightSeriesReport`, `StructureReport`), where the window's sentences are the console's
  lines joined: one decision tree for the exposure verdict, one set of lines for the light
  field, refusal and reasons. The Expo tab's advice and the End to end tab's warning and reasons
  now read in the console's words (for example "PREA JOS" then "Creste expunerea cu ~1.5
  stopuri. Sub ~30% in centru ..."); the console output is unchanged, the goldens and the 300
  figures of `/validate` with it. The exposure texts quote `Th.ExpoTooLow`, `Th.ExpoSkyTooHigh`
  and the new `Th.ExpoClipWhite` (98 % of white, which the analyzer reads too) instead of
  literals. The dead `StructureTabViewModel.Pct` is gone, and so is the opening chain's call
  into the Ratio tab: a pick made during the chain now waits for it instead (R-082), as the
  smoothing does, rather than loading two more masters beside it. R-084 now says the Expo tab
  lists camera raws only, as the code and its test always did.
- NuGet's audit advisories (NU1902, NU1903) stay warnings under the gate's `-warnaserror`, and
  each one is judged in `docs/design/dependencies.md` (ADR 0058): the five published against
  ImageSharp 3.1.11 on 2026-10-07 are patched in 4.1.2 only, the line ADR 0010 rejects, and none
  applies to a tool that decodes the JPEG preview of the owner's own raws.
- `Directory.Build.props` declares the `win-x64` runtime the release publishes for, on every
  project but the two test projects, and turns off the single-file analyzer, so a release's
  publish no longer rewrites the lock files and stops the release on them (ADR 0056).
- The kit's ADR 0033: after a regeneration the assistant reads the whole diff of the golden
  files, ties every changed line to the change the task meant to make and reports them at
  wrap-up, instead of waiting for the owner's read; a line the task did not mean to change is a
  finding that stops the task. The 43 parity goldens stay figures: any changed line there is a
  finding and the owner re-approves it (ADR 0047, R-098). The routine regeneration is filtered
  to `GoldenOutputTests`, so it leaves them alone; they are recaptured with
  `--filter ParityGoldenTests` only after the owner's yes, and the change is recorded with the
  divergences in `docs/design/python-parity.md`.
- The kit's ADR 0031: how the code works lives only in the design notes, and the kit's
  `check-design.ps1`, a new step of the gate, checks that every note is in the index and that
  every `Covers:` sentence names its files as paths that exist. It found `Report/AgainstReport.cs`
  in lens-profiles.md and modes-synthetic.md, a file that never existed (the class is in
  `Runs/AgainstRun.cs`); both now say so. code-map.md, known-issues.md, flat-sources.md,
  lens-profiles.md, modes-synthetic.md and png-output.md now name as paths the files they named
  by class or in prose, so `/wrap-up` finds them; `DesignNotes_HaveTheirIndex` keeps the folder. `CLAUDE.md` drops "Code structure in design notes",
  now the kit's rule "Design notes", and `RepoInvariantTests.EveryDesignNote_IsInTheIndex`,
  which the check replaces, is removed.
- The kit's ADR 0030: the kit's rules "Two readings" and "Measured numbers" now hold the
  practice for a design choice with more than one reasonable answer, the kit's new rule
  "Questions to the owner" makes options come with a recommendation and sends a new fact against
  an answer back to the owner, and `/new-feature` goes on without re-asking. `CLAUDE.md` keeps
  what is FlatCheck's own: "Discuss before designing" puts every design to the owner first, not
  only such a choice, and "Measured on the masters" names the real data (the masters and raws
  listed in `docs/validation.md`, and a calibrated light for `--light`).
- The kit's ADR 0029: the rule "Version and year are the owner's" is the kit's now, so it left
  `CLAUDE.md`. `release.ps1` runs only on `main` and releases the version the owner already set
  in `Directory.Build.props` (2.3.0, above the newest heading, 2.2.0) instead of refusing it;
  `/release` checks that HEAD did not move and proposes a commit limited to the two files it
  changed. `git tag` asks the owner first.
- The kit's ADR 0028: the process rules FlatCheck shares with the kit come from the kit's
  `.claude/rules/kit/process.md`, synced like `scripts/`, and `CLAUDE.md` keeps FlatCheck's own,
  each with a name instead of a number ("Ratios", "Golden files", ...), saying which kit rule it
  extends or varies. Step 0 of the gate also compares the process keys of `.claude/settings.json`
  with the kit's, and `attribution` is empty. Design notes, `docs/skills.md` and the notices cite
  rules by name; ADR 0001 and 0049 record the part the kit's ADR 0028 supersedes.
- The kit's ADR 0026 and 0027: the repository's git `pre-commit` hook (`.githooks/`, from the
  kit) refuses a Claude Code session's commit on a `claude/` branch in the shared checkout,
  and the desktop app's worktree option is not used any more, so the rename of generated
  branch names is gone from `CLAUDE.md` and, through `sync.ps1`, from `/wrap-up`. ADR 0044 and 0050 record the part the kit's ADR 0027
  retires; ADR 0046 notes why the hooks path stays absolute.
- ADR headers are checked by the kit's `check-decisions.ps1`, a step of `verify.ps1` (the kit's
  ADR 0024), which replaces `Decisions_AreNumberedOnceAndCarryAStatus`: title, date, status,
  unique numbers, and every supersession written on both ADRs. The parts that 0046, 0047, 0053
  and 0055 replaced without saying so on the headers are now on both (0001, 0007, 0035, 0039,
  0040, 0042, 0048). Kit decisions are cited as "the kit's ADR NNNN" and get no FlatCheck ADR
  (the kit's ADR 0025).
- ADR 0053 and rules 9 and 13 (the kit's ADR 0023): a commit message may be one line of up to
  100 characters, and a CHANGELOG line is a judgment made at `/wrap-up`, not a gate.
  `scripts/check-staged.ps1` and `CheckStagedTests` are gone; `pre-commit`, `pre-merge-commit`
  and the Claude commit gate run `verify.ps1` alone, and the Stop hook no longer asks for a
  changed `CHANGELOG.md`.
- ADR 0013 and ADR 0021 restate, in dated lines, the two reasons that still pointed at
  `crosscheck.py` (the centre convention, the ordinal sort of a `--light` series); the comments
  of `SeriesFinder.Find` and `FilesComeBackSortedOrdinallyByName` say why the sort is ordinal
  now that the cross-check is retired. No behaviour changed.
- Seven code comments pointed at `docs/lens-profiles.md`, `docs/smooth-validation.md` and
  `docs/radial-model-even-term.md`, where the notes lived before ADR 0001 moved them; they now
  name `docs/design/`. Comments only; no figure or test changed.
- `WorstRingStep` in `StructureAnalyzerTests` implements the ring-step definition of ADR 0018
  (80 profile bins, bins under 5000 px excluded) and reproduces the whole-library table of
  `radial-model-even-term.md`; ADR 0017 and ADR 0031 carry dated corrections of three figures
  copied wrongly from the notes, each figure named with the master it was measured on, and the
  comment of `VignettingComparison.cs` says what the "worst profile" rule would really have
  given (ACCEPTABIL / BUN, not REFA).
- The six skills and the `docs-auditor` agent are the kit's copies now, byte for byte, taken
  with `scripts/sync.ps1` (ADR 0049; kit ADR 0018). FlatCheck's own steps (test projects, golden
  and parity scenarios, the numeric paths, the masters' folder, the two publish targets, the
  always-filter for Stryker) are in `docs/skills.md`, one section per skill; `kit.json` gains
  `audit.alsoRead` so the auditor may read `THIRD-PARTY-NOTICES.md` and `Golden/`. Rule 19
  names the three folders.
- `scripts/` is now the kit's copy, byte for byte, taken with `scripts/sync.ps1` (ADR 0048,
  supersedes 0002; kit ADR 0017). FlatCheck's values (solution, `Category!=Validation`, the
  code and test folders, the test data folder and `FLATCHECK_TESTDATA_LARGE`, the two publish
  targets, the kit's path) live in `kit.json`; rule 19 says the scripts are never edited here.
  Step 0 of `verify.ps1` is red on any drift. `release.ps1 -PublishProject` is gone.
  `CheckStagedTests` copies the real `kit.json` beside the script.
- ADR 0044 and the CLAUDE.md workflow: a task branch lives in its own worktree; the shared
  checkout `E:\Photografie\Code` stays on `main` for the owner's merges.
- ADR 0050 and the CLAUDE.md workflow: a worktree the assistant makes is named after its branch
  without `claude/`, so the owner finds it to review before the commit (the kit's ADR 0019).
- `FlatCheck.Tests` now runs under `<InvariantGlobalization>`, like `flatcheck.exe` (ADR 0043,
  R-007). The switch is per executable and did not reach the test host, so the golden files and
  the report tests formatted with the machine's culture: they passed on `en-GB` and 63 of them
  failed under `ro-RO`. `InvariantGlobalizationTests` checks the console's runtimeconfig and the
  test host. No golden file changed.

- `scripts/check-staged.ps1` refused every commit made from a linked worktree that touched a
  FlatCheck file: git runs the hook with `GIT_DIR` set, `git rev-parse --show-toplevel` then
  named the FlatCheck folder as the repository root, and every staged file looked like console
  code. The script now finds the root by walking up to the nearest `.git`; `CheckStagedTests`
  runs it in a throwaway repository with and without `GIT_DIR`.

- Adopted the ClaudeCodeStarterKit process (ADR 0001 to 0006): `scripts/verify.ps1` as the one
  gate (manifest, restore, format, build with warnings as errors, tests), `check-staged.ps1`,
  Claude Code hooks and skills, `docs/requirements.md` with IDs, ADRs under `docs/decisions/`,
  `docs/validation.json` read by `ValidationTests` on the real masters, `RepoInvariantTests`.
  CLAUDE.md shrunk from 2709 lines to the rules; its content moved unchanged into
  `docs/design/`. Central package management with lock files; SDK pinned in `global.json`.
- Fixed the nine build warnings in the test projects (nullable annotations, one swapped
  `Assert.Equal` argument) and ran `dotnet format` once over the solution: whitespace, unused
  usings removed, object initialisers wrapped one member per line, 33 files. No behaviour
  changed; the golden files are untouched.
- `ValidationTests` (trait `Category=Validation`, not in the gate) record 40 figures measured on
  the real masters in `docs/validation.json`; `RepoInvariantTests` tie `docs/requirements.md` to
  the `R###_` test names, check the ADR numbering, the design-note index and the validation
  manifest; 64 existing tests renamed with the ID of the requirement they pin.
- Deleted `.github/copilot-instructions.md` (Azure rules Visual Studio had added).
- Branch names and clean-up, as in the kit's ADR 0013: a branch is named `claude/` plus its task
  in English, a generated name is renamed before the first commit, a branch merged into `main` is
  deleted with `git branch -d`, and `git branch -D` joins the `ask` list. CLAUDE.md Workflow,
  `/wrap-up` step 11, `.claude/settings.json`.

## [2.2.0] - 2026-09-23

The state of the tool when the changelog was started. What 2.x holds, in the order it was built:

- 2.0.0: the core became `FlatCheck.Core` and a second application, the WPF window, appeared
  over it; the console and the window call the same `Runs/` and print the same report.
- The `--histeq` mode (Siril's "Histogram" display), the `--smooth` mode (a second master with
  the diffuser texture removed) with `--scale auto`, the smoothed master listed in the window's
  Histogram tab, the "Ruleaza in Siril" button on the problem card, the master found in
  `masters/` as well as `process/`.
- The synthetic flat from the lens's published vignetting profile (`--synthetic`, `--lenses`,
  `--against`, the Sintetic tab), the sixth structure test (the master's vignetting against the
  lens profiles, `--raw` / `--lens`), the calibration crop as part of a lensfun lens's identity,
  the warning when a request is far from the profile used.
- The radial model on partial rings: symmetrisation, then the even 2θ + 4θ correction with one
  q per r² anchored on the full rings (`docs/design/radial-model-even-term.md`); `ROWORDER`
  honoured when naming the CFA positions of a FITS.

Earlier history (1.0.0 to 1.2.0: the four modes, the residual scale, the `--ratio` rewrite, the
`--light` validity guard and series mode, the fifth test, raw input through libraw, the camera
preview) is in `git log`; no changelog was kept then.
