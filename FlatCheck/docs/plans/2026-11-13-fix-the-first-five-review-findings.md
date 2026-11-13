# Fix the first five review findings (CR-001 to CR-005)

## Context

`docs/review/2026-11-13-code-review.md` lists 60 findings of the whole-code review (ADR 0100),
all `open`. The owner asked for the first five open ones to be fixed in this session and marked
in the list, so later sessions know what is done. They are the first five of severity 1 (a wrong
figure or a wrong verdict): CR-001 to CR-005.

ADR 0100 says one entry per task; the owner asked for five in one session, so this is one branch
with **one commit per entry**, each with its own requirement, failing test, fix, docs and gate,
and a last commit that closes the five entries in the list with their hashes.

The four design choices were measured and put to the owner on 2026-11-13; all four answers are
below (rule "Discuss before designing"). CR-003 has no choice to make.

## What was measured (2026-11-13, read-only scripts, this machine)

- **CR-001**, the shipped lensfun table, 1053 cameras with a crop, each looked up by its own
  names: today's rule lands on another body 402 times, 137 of them with a crop more than 3 % off
  (`Nikon D750` -> D7500 1.534, `Canon EOS R5` -> R50 V 1.613, `Pentax K-1` -> K100D Super,
  `QHY5III678C` -> Pentax Q 5.53, `---` -> a Kodak). The rule chosen: 1 other body (a duplicate
  alias), 0 wrong crops; `NIKON D3300`, `Nikon Corporation NIKON D3300`, `D3300`, `Canon EOS 7D
  Mark II`, `Sony ILCE-7M3`, `Pentax K-70`, `Canon EOS R5m2` found as before.
- **CR-004**, the 3531 Adobe files under ProgramData: every file is raw or non-raw throughout
  (`stCamera:CameraRawProfile` never mixed, never missing). With parentheses: 2409 ` - RAW`
  (all raw), 434 without a suffix (391 non-raw, **43 raw**: Leica S, Lensbaby Velvet, none with
  a ` - RAW` sibling), 2 ` - Raw`. Of today's 3002 lens identities, 529 merge a raw file with a
  non-raw one and 189 hold non-raw files only (iPads, phones, compacts). So the name cannot
  tell; the flag inside can.
- **CR-005**, the 3242 distinct lens names of both sources: the largest true maximum aperture is
  f/11 (Canon RF 600 and 800); every value read above it is a model code or a focal length (14,
  22, 28, 32, 35, 45, 200, 601, 707, 770, 810, 828), 37 names in all; the smallest read is 0.8.

## The fixes

Work in a worktree: `git worktree add .claude/worktrees/Fix-the-first-five-review-findings -b
claude/Fix-the-first-five-review-findings main` (from `FlatCheck/`). Each fix: requirement, then
the failing test seen red, then the code, then the note, ADR and CHANGELOG line, then the commit
(the hook runs the gate).

### 1. CR-001: the camera lookup (new R-133, ADR 0101)

Owner's choice: **the exact name first, then a table name found whole, word for word, in the
file's name; the longest wins; never a table name that merely contains the file's name.**

- `FlatCheck.Core/LensProfiles/LensfunReader.cs`, `LensfunData.CameraOf` (made `internal`):
  names compared as words (runs of letters and digits, upper-cased). A name with no letter or
  digit finds nothing. Two entries of one name keep the first, as today.
- `FlatCheck.Core/LensProfiles/LensIdentification.cs` (`Identify`, about line 201): the crop's
  source and note print the model matched in the table, not the name asked for.
- Tests (`FlatCheck.Tests/LensProfileTests.cs`): `R133_...` on a small table holding the pairs
  of the review (D750 / D7500, D700 / D7000, D80 / D800E, Z f / Z fc, EOS R5 / R50 V, K-1 /
  K100D Super, Pentax Q against a `QHY...` name, a name of punctuation only), and one that
  walks the shipped `lensfun/` folder: every camera, by each of its names, gets its own crop.
  The existing `Lensfun_CropOfMatches...` stays untouched and green (the fixture has the alias).

### 2. CR-002: numbers on the console (R-005 extended, ADR 0102)

Owner's choice: **a comma is refused with the option's error**, as `process.md` already says.

- `FlatCheck/Options.cs`, `TryNum`: `NumberStyles.Float` and `double.IsFinite`. No message
  changes: `--fstop 2,8` gives the existing `--fstop expects an f-number, got '2,8'`, exit 1;
  the same for `--sat`, `--black`, `--fov`, `--focal`, `--crop`, and for `NaN` / `Infinity`.
- Tests (`FlatCheck.Tests/OptionsTests.cs`): `R005_...` theory over the six options with a
  comma, `NaN`, `Infinity`, `-Infinity`; `1e3` and `2.8` still parse.

### 3. CR-003: the Split tab follows the Structure tab (R-093 extended, no ADR)

- `FlatCheck.Wpf/ViewModels/SplitTabViewModel.cs`: `Attach` takes the folder's
  `SessionSettings` (the same instance the Structure tab writes, so a flip made there is seen at
  Start); the analysis builds the query only when the box "no filters" is on, and puts
  `LensKey = settings.Lens` on it, as `StructureTabViewModel.QueryAsync` and `Compares` do.
  Box off: no reference profile, the five tests alone, which R-093 already defines.
- `FlatCheck.Wpf/ViewModels/MainViewModel.cs:280`: passes `settings`.
- Tests (`FlatCheck.Wpf.Tests/SplitTabTests.cs`): `R093_...` case A (a chosen lens: the query
  carries its key and the table's reference is that lens) and case B (box off: no profile row,
  verdict of the five tests).

### 4. CR-004: Adobe's non-raw profiles (new R-134, ADR 0103)

Owner's choice: **by what the file says**: the reader skips an entry whose
`stCamera:CameraRawProfile` is `False`; a missing flag is kept.

- `FlatCheck.Core/LensProfiles/LcpReader.cs`, `ReadXml`: the skip.
- `FlatCheck.Core/LensProfiles/LensProfile.cs`, `AdobeProfileFile.FromName`: the `RAW` suffix
  compared without case (` - Raw.lcp` is no longer a lens of its own). The name rule is
  otherwise unchanged, so the existing ` - JPEG` test row stays green.
- `FlatCheck.Core/Report/SyntheticReport.cs`, `NoProfile`: a lens left with no profile may now
  be a non-raw one, so the reason is widened (existing tests assert the unchanged beginning):
  - now: `... has no vignetting profile (its file describes distortion or colour only); choose
    another lens.` / `... nu are profil de vignetare (fisierul lui descrie doar distorsiunea sau
    culoarea); alege alt obiectiv.`
  - proposed: `... has no vignetting profile for raw files (its file describes distortion or
    colour only, or was made for JPEGs); choose another lens.` / `... nu are profil de vignetare
    pentru fisiere raw (fisierul lui descrie doar distorsiunea sau culoarea, ori e facut pentru
    JPEG); alege alt obiectiv.`
- Tests: `R134_...` in `LensProfileTests.cs`: a non-raw entry is not read; a lens made of
  `X (L) - RAW.lcp` plus `X (L).lcp` (non-raw, shallower) gives the raw file's profile from
  `Nearest`; a suffix-less raw file still gives its profiles; ` - Raw` joins ` - RAW`.

### 5. CR-005: the aperture read from a lens name (R-018 extended, ADR 0104)

Owner's choice: **a number followed by `mm` is a focal length and the search goes on; a value
outside f/0.7 to f/11 means the name does not say**, which contradicts nothing.

- `FlatCheck.Core/LensProfiles/LensProfile.cs`, `LensSpec.Parse` / `ApertureRx`.
- `FlatCheck.Core/Thresholds.cs`: `Th.LensNameApertureMin = 0.7`, `Th.LensNameApertureMax = 11`,
  with the measurement in the comment (rule "Ratios": no literal in code).
- Tests: new rows `R018_...` (`TAMRON SP 35mm F_1.4 Di USD F045` -> none, `Zeiss Distagon T 21
  f28 ZE` -> none, `Zeiss Otus 55 f14 ZE` -> none, `smc Pentax-F 28mm f/2.8` -> 2.8, `Pentax-F
  28-80mm f/3.5-4.5` -> 3.5, `Canon RF 800mm F11 IS STM` -> 11, `FinePix F810 & compatibles` ->
  none), and `Narrow` keeps the Tamron for a raw that says 35 mm f/1.4. The existing theory
  stays untouched.
- CR-014 (non-ASCII digits, same lines) stays open for its own task.

### 6. Close the entries

`docs/review/2026-11-13-code-review.md`: the Status column of the five rows becomes
`fixed <hash>`, and each entry gets one closing line (the hash, the requirement, the ADR).
Nothing else in the list changes; CR-060 (stale notes) stays open.

## Docs in the same commits

- `docs/requirements.md` (R-005, R-018, R-093 extended; R-133, R-134 new),
  `docs/decisions/0101..0104`, `CHANGELOG.md` (five `Fixed` lines).
- `docs/design/lens-profiles.md` section 5 (the camera rule, the raw flag, the ", JPEG" sentence
  the review found wrong, the aperture rule) and section 10 (the measured counts);
  `docs/design/process.md` (non-finite values); the Split note found through
  `docs/design/README.md`. `README.md` checked at wrap-up.
- This plan saved as `docs/plans/2026-11-13-fix-the-first-five-review-findings.md`.

## Verification

- Each new test is run and seen failing before its fix, then green.
- `scripts/verify.ps1` green before every commit (the commit hook runs it); before the last one
  a `--no-incremental -warnaserror` build.
- Goldens: no line is expected to change (the fixtures' camera is the table's own spelling, and
  no golden holds the widened refusal). Any changed line is read and reported; a parity golden
  line that changes is a finding and stops the task.
- `/validate` on the real masters after fixes 1, 4 and 5, since they decide which profile is
  compared (V-016 to V-049). A figure that moves is reported with its cause and nothing in
  `docs/validation.json` is edited without the owner's word.
- At the end: the diff read back and summarised, a subagent review of the branch offered, then
  the merge proposed and asked. No version bump (the owner's); five fixes seem to warrant a
  patch version.
