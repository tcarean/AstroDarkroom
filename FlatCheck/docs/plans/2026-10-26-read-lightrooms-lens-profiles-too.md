# Read Lightroom's lens-profile folders too, once per file

Approved 2026-10-26. Branch `claude/Read-Lightrooms-lens-profiles-too`, worktree
`FlatCheck/.claude/worktrees/Read-Lightrooms-lens-profiles-too`, cut from `main` at b086f2c.

## Context

FlatCheck reads Adobe's `.lcp` lens profiles from `C:\ProgramData\Adobe\CameraRaw\LensProfiles\1.0`
and `%APPDATA%\Adobe\CameraRaw\LensProfiles` (ADR 0028, R-070), and every text says Lightroom
installs them there. Measured 2026-10-26 on the owner's computer: that ProgramData folder (3531
files) was written by the Adobe DNG Converter; Lightroom Classic 15.6 and Lightroom keep their own
3633 profiles in `C:\Program Files\Adobe\<app>\Resources\LensProfiles\1.0`, which FlatCheck did
not read. Lightroom's set is a strict superset of the ProgramData one (all 3531 names present,
byte-identical on a 200-file sample; 102 newer, mostly phone cameras). Enumerating one folder by
name costs about 0.5 s. The catalogue dedupes lenses (`LensCatalog.Lenses`) but not files:
`ProfilesOf` concatenates every stub of a lens, so two folders with the same file would list and
parse every common profile twice.

The owner chose option (b): read the Program Files folders too, dedupe by relative name, correct
the texts in FlatCheck and AstroDarkroom, flip the root README row to "yes". The Bitbucket README
is on `main` (7647a8f), so the row flips in this branch. The decision is ADR 0065.

## 1. FlatCheck, the behaviour (requirement first, then test, then code)

- `docs/requirements.md`: R-070 names the third folder; new R-106 for the order, the first-wins
  rule by relative name without case, the counts and the summary text.
- `FlatCheck.Tests/LensProfileTests.cs`: `R106_Locate_ReadsTheSameRelativeNameOnceFromTheFirstFolder`
  (two folders, the same relative name differing by case, the copy not even XML, one file only in
  the second), `R106_AdobeAppDirectories_AreTheExistingProfileFoldersInOrdinalOrder` (a fake
  Program Files tree), `R106_DefaultAdobeDirectories_StartWithCameraRawThenTheProgramFilesApps`
  (this machine, Windows only), `R106_SourcesSummary_NamesTheDuplicatesSkippedOnlyWhenThereAreAny`
  (both languages). The goldens run with `--profiles <one dir>` and do not move.
- `FlatCheck.Core/LensProfiles/ProfileLocator.cs`: `AdobeAppDirectories(root)`,
  `DefaultAdobeDirectories` extended, `Locate` with the `HashSet` of relative paths,
  `ProfileSources.AdobeFilesDuplicate`.
- `FlatCheck.Core/Report/SyntheticReport.cs`: `SourcesSummary` and `AdobeWhere` texts in both
  languages. `StructureReport.cs` stays. The window shows them unchanged.
- Docs: ADR 0065 (ADR 0028's Status points at it), `docs/design/lens-profiles.md` §5 and §10,
  `README.md` mode 7, `THIRD-PARTY-NOTICES.md`, `CHANGELOG.md`. No formula moved: no `/validate`.

## 2. AstroDarkroom

- `README.md`, the Adobe bullet: the three folders, and "Lightroom keeps its own copy ... not
  read" dropped. `Prerequisites.cs`, R-009 and `LanguageTests` already name Lightroom and stay.
- ADR 0008: dated `(Restated 2026-10-26: ...)` notes on the measurement sentence and on the
  rejected alternative, pointing at FlatCheck ADR 0065 (the kit's ADR 0024: a supporting reason
  that no longer holds while the decision stands gets a note, not a superseding ADR). The plan
  as approved named an ADR 0010; the kit's rule picks the note.
- `CHANGELOG.md` Changed line.

## 3. Root `README.md`

- The folder list: a third bullet, `C:\Program Files\Adobe\<app>\Resources\LensProfiles\1.0`,
  Lightroom Classic's and Lightroom's own copies.
- The table row: Lightroom Classic and Lightroom, "yes: from its own program folder". The
  "simplest way is the free DNG Converter" sentence stays true and stays.

## Verification

- The R106 tests, then the FlatCheck gate (goldens unchanged) and AstroDarkroom's gate on its
  commit; `scripts/check-design.ps1 -Since main`.
- `flatcheck --lenses` on the owner's machine, Release build, before and after:

```
before:  Adobe  : 2998 lenses from 3531 files                              1.5 s warm
after:   Adobe  : 3070 lenses from 3633 files (7164 duplicates skipped)    1.6 s warm, 11 s cold
--lenses --lens "Tokina 11-20": the four Adobe rows keep their 105 profiles (diff = lines 5-6 only)
--lang ro:  Adobe  : 3070 obiective din 3633 fisiere (7164 duplicate sarite)
```
