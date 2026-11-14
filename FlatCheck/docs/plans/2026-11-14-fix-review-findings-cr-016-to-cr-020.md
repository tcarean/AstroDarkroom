# Fix the next five review findings (CR-016 to CR-020)

## Context

`docs/review/2026-11-13-code-review.md` holds 60 findings (ADR 0100). CR-001 to CR-015 are
fixed and merged. The owner asked on 2026-11-14 for the next five open ones and for the list
to be marked, so later sessions know what is done: CR-016 to CR-020, all of severity 3 (a
wrong text or a wrong display), all `confirmed`. As before: one branch
(`claude/Fix-FlatCheck-CR-016-to-CR-020`, in its own worktree), one commit per entry with its
requirement, failing test, fix, docs and gate, then a commit that closes the five entries in
the list.

Two choices were put to the owner on 2026-11-14 with the facts; the answers are below. The
texts are new and are shown old against new for approval with this plan.

## What was read (2026-11-14)

All five were read again in today's code; each case becomes the failing test. Nothing was
run: the cases follow from the lines.

- **CR-016.** `SirilSession.MissingInputFolders` lists each input folder without a `catch`.
  Three ways in: (a) the card of a folder without a master (`Preprocess.Attach`, outside
  `OpenAsync`'s `try`): the task faults, nobody awaits it, the Run button stays grey with no
  reason and no log line; (b) a folder WITH a master (`Split.Attach`, inside the `try`): the
  whole folder is refused with the red card "The folder could not be read", though only the
  Split tab needs that folder; (c) the press on Start Split or Run in Siril, when the folder
  became unlistable after the opening.
- **CR-017.** With `--lens <text>` matching nothing, `VignettingComparer.Analyze` gets an
  empty list and picks the reason from the raw alone, so the report says "no raw ... Give
  --lens <text>". The sentence written for a text that matches nothing is reached only when
  lenses were found but none has a vignetting profile, where it is false.
- **CR-018.** The "focal length not known" and "aperture not known" texts are fixed strings
  that say "no raw", also when a raw was read, or when it could not be read.
- **CR-019.** The "crop not known" text prints `ShotIdentity.CropSource`, which in that case
  is always "unknown - the profile's calibration sensor is taken": nothing is taken there, the
  lensfun rows were skipped. `SyntheticReport.CropLine` prints the same string after its own
  "unknown", so the word comes twice.
- **CR-020.** `ExposureReport.VerdictLines` prints `-log2(target / centre)` whenever the
  verdict is TOO HIGH, and clipping alone gives that verdict with the centre below the target.

Goldens: no golden holds any text that changes (searched `FlatCheck.Tests/Golden/`). The one
gap text a golden prints, the aperture gap without a raw, stays letter for letter.

## The owner's two choices

1. **CR-016, a session with a master and an unlistable script input folder:** the session
   opens; only the Split tab says why it cannot start. (Not: keep the red card.)
2. **CR-020, when the figure goes:** whenever it would print negative or as `0.0`; a figure
   appears only from 0.1 stops up. (Not: only at or below the target.)

## The fixes

### 1. CR-016: an input folder that cannot be listed blocks the script, with words (R-080 extended, ADR 0113)

- `FlatCheck.Wpf/Session/SirilSession.cs`: the check of the input folders catches
  `IOException` and `UnauthorizedAccessException` per folder and returns, beside the missing
  or empty ones, the first folder that could not be listed with the exception.
- `FlatCheck.Wpf/ViewModels/PreprocessViewModel.cs` (`ScriptChoiceViewModel.FindBlocker`):
  that folder is a blocker with its own text, and one Warn line with the exception (as the
  unreadable script beside it does). So the card (a), the Split tab (b, c) and the press (c)
  all show it, and a session with a master opens (choice 1).
- `FlatCheck.Wpf/MainWindow.xaml.cs`: the three tasks fired without being awaited
  (`OpenAsync` twice, `ReopenAsync`) go through one small helper that writes an Error line
  with the exception when the task faults (R-110). Nothing new on screen for that.
- New text, `SessionMessages` (the system's own message in the middle, as on the red card):

  | | English | Romanian |
  |---|---|---|
  | new | `The script <script> enters the subfolder flats/, but FlatCheck cannot see what is in it:` / `<the system's message>` / `Check the permissions on that folder, then open the session folder again.` | `Scriptul <script> intra in subfolderul flats/, dar FlatCheck nu poate vedea ce e in el:` / `<mesajul sistemului>` / `Verifica drepturile pe acel folder, apoi deschide din nou folderul sesiunii.` |

- Tests (`MainViewModelTests`, `SplitTabTests`, with the existing `UnlistableFolder.Deny`):
  no master and `flats/` denied: the opening does not fault, the card's blocker names
  `flats/` and the error, the button cannot run, one Warn line carries the exception; a master
  and `lights/` denied: the session opens with its tabs and the Split tab's blocker says the
  same; a faulted detached task leaves one Error line.

### 2. CR-017: a `--lens` text that matches nothing is said so (R-017 extended)

- `FlatCheck.Core/Analysis/VignettingComparison.cs`: two reasons of their own in
  `ComparisonGap`: the text given matched no lens; lenses were found but none has a
  vignetting profile for raw files. `VignettingComparer.Analyze` takes the text
  (`StructureRun.Compare` passes it) and keeps the lenses found without a profile.
- `FlatCheck.Core/Report/StructureReport.cs` (`GapText`):

  | Case | Today | Proposed (English / Romanian) |
  |---|---|---|
  | `--lens tokna`, no raw | `no raw beside the master (in flats/), so no lens ID; a FITS does not say which lens made it. Give --lens <text> or --raw <file>` | `no lens in the list matches --lens "tokna"; --lenses shows the list` / `niciun obiectiv din lista nu se potriveste cu --lens "tokna"; --lenses arata lista` |
  | `--lens tokna`, the raw names a lens | `<the raw's lens>: no lens in the catalog matches it (by ID, name and range). Give --lens <text>` | the same new sentence |
  | one lens found, no vignetting profile | `no lens matches the text given; see --lenses` (or, from a raw, `...no lens in the catalog matches it...`) | `"<lens>" was found, but it has no vignetting profile for raw files (its file describes distortion or colour only, or was made for JPEGs). Give another lens with --lens <text>` / `"<obiectiv>" a fost gasit, dar nu are profil de vignetare pentru fisiere raw (fisierul lui descrie doar distorsiunea sau culoarea, ori e facut pentru JPEG). Da alt obiectiv cu --lens <text>` |
  | several found, none with a profile | as above | `<n> lenses were found, but none has a vignetting profile for raw files (their files describe distortion or colour only, or were made for JPEGs). Give another lens with --lens <text>` / `s-au gasit <n> obiective, dar niciunul nu are profil de vignetare pentru fisiere raw (fisierele lor descriu doar distorsiunea sau culoarea, ori sunt facute pentru JPEG). Da alt obiectiv cu --lens <text>` |

  The words in the parentheses are those of the existing `SyntheticReport.NoProfile`.
- Tests (`StructureReportTests`, `VignettingComparisonTests`, `R017_...`): a text that
  matches nothing, without a raw and with a raw that names a lens; a lens whose Adobe file
  holds only entries made for JPEGs (the fixture of R-134), by text and by the raw's name.

### 3. CR-018: "no raw" is said only when there was none (R-017 and R-106 extended)

- `StructureReport.GapText`, the focal and aperture gaps, by what happened to the raw
  (`ShotIdentity.Raw`, `StructureOutcome.RawError`); the file's name is `ShotIdentity.RawName`,
  or "the raw" / "raw-ul" without one:

  | Case | Today | Proposed (English / Romanian) |
  |---|---|---|
  | focal, no raw | `the focal length is not known - no FOCALLEN in the master, and no raw; give --focal <mm>` | unchanged |
  | focal, a raw was read | the same "and no raw" | `the focal length is not known - no FOCALLEN in the master, and IMG_7290.CR2 does not give it; give --focal <mm>` / `focala nu se stie - nici FOCALLEN in master, si nici IMG_7290.CR2 nu o da; da --focal <mm>` |
  | focal, the raw could not be read | the same "and no raw" | `the focal length is not known - no FOCALLEN in the master, and the raw beside it could not be read (<error>); give --focal <mm>` / `focala nu se stie - nici FOCALLEN in master, iar raw-ul de langa el nu s-a putut citi (<eroare>); da --focal <mm>` |
  | aperture, no raw | `the aperture is not known - Siril does not write it in the master, and there was no raw beside it; give --fstop <f> or --raw <file>` | unchanged (the goldens print it) |
  | aperture, a raw was read | the same "there was no raw" | `the aperture is not known - Siril does not write it in the master, and IMG_7290.CR2 does not give it; give --fstop <f>` / `diafragma nu se stie - Siril n-o scrie in master, si nici IMG_7290.CR2 nu o da; da --fstop <f>` |
  | aperture, the raw could not be read | the same "there was no raw" | `the aperture is not known - Siril does not write it in the master, and the raw beside it could not be read (<error>); give --fstop <f>` / `diafragma nu se stie - Siril n-o scrie in master, iar raw-ul de langa el nu s-a putut citi (<eroare>); da --fstop <f>` |

- Tests (`StructureReportTests`, `R106_...` and `R017_...`): the Canon EOS 40D raw (focal 0,
  aperture 0) with a `--lens` text, with and without FOCALLEN; a raw that throws on reading,
  with a `--lens` text; the two no-raw texts stay.

### 4. CR-019: the "crop not known" text says why, and "unknown" comes once (R-017 extended)

- `StructureReport.GapText`, the crop gap, with the words of the existing
  `SyntheticReport.NoCrop`:

  | Case | Today | Proposed (English / Romanian) |
  |---|---|---|
  | the camera is not in lensfun's list | `lensfun profiles only, and the camera's crop is not known (unknown - the profile's calibration sensor is taken); give --crop <factor>` | `lensfun profiles only, and the camera's crop is not known ("Canon EOS 40D" is not in the lensfun database); give --crop <factor>` / `doar profile lensfun, si crop-ul aparatului nu se stie ("Canon EOS 40D" nu e in baza lensfun); da --crop <factor>` |
  | no file names the camera | the same | `lensfun profiles only, and the camera's crop is not known (the files do not say which camera it is); give --crop <factor>` / `doar profile lensfun, si crop-ul aparatului nu se stie (fisierele nu spun ce aparat e); da --crop <factor>` |

- `FlatCheck.Core/Report/SyntheticReport.cs` (`CropLine`, the `--synthetic` report's row):

  | Today | Proposed (English / Romanian) |
  |---|---|
  | `camera crop        : unknown  (unknown - the profile's calibration sensor is taken; SFF 1.523)` | `camera crop        : unknown  (the profile's calibration sensor is taken; SFF 1.523)` / `crop aparat        : necunoscut  (se ia senzorul de calibrare al profilului; SFF 1.523)` |

  `ShotIdentity.CropSource` itself keeps its text (two tests read it); the two places stop
  printing it where it is wrong or doubled.
- Tests (`StructureReportTests`, `LensProfileTests` or `SyntheticReportTests`, `R017_...`):
  the crop gap with and without a camera name never says "is taken"; the crop row holds
  "unknown" / "necunoscut" once.

### 5. CR-020: no correction figure that says nothing (R-043 extended, ADR 0114)

- `FlatCheck.Core/Report/ExposureReport.cs` (`VerdictLines`, the TOO HIGH branch of a panel
  flat): the figure is printed only when it shows as 0.1 stops or more (choice 2; decided on
  the figure as printed, with the existing `Numbers.RoundsToZero`). Otherwise the sentence the
  sky flat already has. The window shows the same text.

  | Case | Today | Proposed (English / Romanian) |
  |---|---|---|
  | burnt, centre at 55 % | `Lower the exposure by ~-0.1 stops.` | `Lower the exposure.` / `Scade expunerea.` |
  | burnt, centre at 58 % | `Lower the exposure by ~0.0 stops.` | `Lower the exposure.` / `Scade expunerea.` |
  | centre at 81 % | `Lower the exposure by ~0.5 stops.` | unchanged |

  The line "The centre is burnt: ..." follows as today.
- Tests (`ExposureReportTests`, `R043_...`): burnt at 55 % and at 40 %, burnt at 58 %, burnt
  at 62 % (0.1, the figure stays), 81 % without clipping (0.5 stays), in both languages.

### 6. Close the entries

The Status column of the five rows and a `Status:` line per entry (commit, requirement, ADR,
what stays open), as for CR-001 to CR-015.

## Docs in the same commits

`docs/requirements.md` (R-080, R-017, R-106, R-043 extended; no new ID);
`docs/decisions/0113` (an unlistable script input folder blocks the script, not the session)
and `0114` (a correction is printed from 0.1 stops up); `CHANGELOG.md` (five `Fixed` lines);
the design notes that cover the touched files (`two-applications.md`, `modes-structure.md`,
`modes-synthetic.md`, `modes-expo.md`, `logging.md`, `code-map.md` for the new helper and the
two new reasons), checked with `scripts/check-design.ps1 -Since main`; `README.md` where it
quotes one of these texts. This plan saved as
`docs/plans/2026-11-14-fix-review-findings-cr-016-to-cr-020.md`.

## Verification

- Each new test seen failing before its fix, then green; the gate (`scripts/verify.ps1`) at
  every commit; a `--no-incremental -warnaserror` build before the last one.
- Goldens: no line expected to change; any changed line is read and reported, and a parity
  golden line stops the task.
- `/validate` at the end, since files under `Analysis/` and `Report/` are touched; no
  figure's formula changes, so no drift is expected, and the table is pasted.
- By hand in the worktree, with the console built there: `flatcheck <master> --lens tokna`
  prints the new sentence; the window's renders (`FLATCHECK_DUMP_SCREENS`) for the card with
  the new blocker.
- Then a subagent review of the branch, its findings applied minimally, a second review of
  that commit, and the merge proposed and asked.
