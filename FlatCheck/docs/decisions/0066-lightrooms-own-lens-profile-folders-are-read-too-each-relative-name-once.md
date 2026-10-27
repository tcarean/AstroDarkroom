# 0066. Lightroom's own lens-profile folders are read too, each relative name once, first folder wins

Date: 2026-10-26
Status: accepted 2026-10-26
Supersedes: 0028 in part (the two Adobe folders)

## Context

ADR 0028 reads the Adobe profiles from `C:\ProgramData\Adobe\CameraRaw\LensProfiles\1.0` and
`%APPDATA%\Adobe\CameraRaw\LensProfiles` only, and every text (the README, the notices, the two
report messages, the AstroDarkroom prerequisites) said Lightroom installs them there. The owner
measured on 2026-10-26 that it does not, and asked for the options with a recommendation.

Measured on the owner's computer, 2026-10-26 (the figures are also in
`docs/design/lens-profiles.md`, section 10):

- `C:\ProgramData\Adobe\CameraRaw\LensProfiles\1.0`: 3531 `.lcp`, 446 MB, the folder created
  2026-04-16 by the Adobe DNG Converter 18.3, newest file 2026-06-04. `%APPDATA%`'s folder is
  empty. Photoshop and the Camera Raw plug-in are not installed, so Camera Raw's own writing
  to that folder is known from Adobe community answers only, not measured.
- `C:\Program Files\Adobe\Adobe Lightroom Classic\Resources\LensProfiles\1.0` and
  `...\Adobe Lightroom CC\Resources\LensProfiles\1.0`: 3633 each, 451 MB, newest 2026-09-29
  (Lightroom Classic 15.6), identical to each other by name. Every ProgramData name exists in
  Lightroom's folder and 200 sampled common files hash identically; the 102 extra are newer
  bodies, mostly phone cameras. The owner's three lenses have the same files in both.
- Enumerating one folder by name costs about 0.5 s. `flatcheck --lenses`, warm, takes 1.5 s
  before the change and 1.6 s after; the first cold start after the build took 11 s.
- The catalogue dedupes lenses (`LensCatalog.Lenses`, by the record key) but not files:
  `ProfilesOf` concatenates the profiles of every stub of a lens, so two folders holding the
  same file would list and parse every common profile twice (210 instead of 105 for the
  Tokina 11-20).

The two options put to the owner: (a) correct the texts to Camera Raw (Photoshop) and the free
DNG Converter; (b) also read Lightroom's folders, and the root README's Lightroom row becomes
"yes". The recommendation was (b), on the cost: a glob, a dedup rule and the texts (a) would
need anyway; the owner chose (b) the same day.

## Decision

- `ProfileLocator.DefaultAdobeDirectories` is, in search order: the Camera Raw folder under
  ProgramData, the user's under `%APPDATA%`, then `AdobeAppDirectories(C:\Program Files\Adobe)`,
  the `Resources\LensProfiles\1.0` of every application folder there that has one, in ordinal
  order of the application folder. `Program Files (x86)` is not searched: Lightroom is 64-bit
  only. A root that cannot be listed gives no folders, not an error, like a missing one.
- `Locate` keeps a set of each file's path relative to its own search folder, compared without
  case. A relative name already seen under an earlier folder is skipped before a stub is made
  and counted in `ProfileSources.AdobeFilesDuplicate`; `AdobeFiles` counts the files kept. So
  a lens present in two folders is listed once and parsed once, from the first folder. The
  user's own profiles under `%APPDATA%` sit one level deeper than the installed ones in Adobe's
  layout (`LensProfiles\1.0\<maker>\...` against the installed root, which is the `1.0` itself),
  so under that layout they never collide, which is right, they are theirs; a user's copy put
  directly under `LensProfiles\<maker>\` with an installed file's name is a duplicate of it and
  the installed one wins. A folder whose enumeration fails part-way (a subfolder without list
  rights) keeps the files found before the failure and loses the rest, without an error, like a
  root that cannot be listed.
- The sources summary adds ` (N duplicates skipped)` / ` (N duplicate sarite)` only when N is
  not zero, and the Adobe paragraph of `--lenses` and of the window's Sintetic tab says a file
  present in two folders is read once, from the first. `--profiles <dir>` still replaces the
  whole list, so the goldens, which run with one folder, do not move.
- The texts keep naming Lightroom, Camera Raw and the DNG Converter as the programs that bring
  the profiles, which is now true, and name the third folder: FlatCheck's README, the notices,
  AstroDarkroom's README and the root README, whose Lightroom row says "yes".

## Alternatives rejected

- **Texts only, option (a).** Honest and an hour of work, but a Lightroom-only subscriber, a
  common case, would keep the lensfun half of the list alone while the suite's prerequisites
  tell them Adobe's profiles are read from the computer.
- **Naming the applications (`Adobe Lightroom Classic`, `Adobe Lightroom CC`) instead of a
  glob.** Adobe renames its folders (Lightroom's is `Adobe Lightroom CC` today); the glob
  costs one directory listing.
- **Deduplicating by file name alone.** Adobe's layout puts the maker folder above the file
  and the same lens name can exist under two makers' folders (`Rokinon` and `Samyang`); the
  relative path keeps them apart.
- **Letting the newest copy win.** Lightroom's copies are byte-identical to the DNG
  Converter's where names match; first-wins is deterministic and keeps today's behaviour for
  the files already read.
- **Counting the duplicates in `AdobeFiles`.** The summary would say "3633 lenses from 10797
  files" and the window the same; the kept count is what the catalogue holds.

## Consequences

`DefaultAdobeDirectories` now touches the disk (`Directory.Exists` on the Program Files
folders), as `Locate` always did. The summary's duplicate count is large on a machine with both
sources (7164 on the owner's), and says so plainly. The AstroDarkroom ADR 0008 measurement
sentence and its rejected alternative "Lightroom with no" carry dated notes pointing here. A
Windows layout again: on another platform the Program Files folder is empty and nothing changes.
- Corrected 2026-10-27: the sentence of the Decision on a folder whose enumeration fails
  part-way was never true; the enumeration was made eager inside the root's `try`, so the whole
  root was lost, with a Warn line. Since the review of 2026-10-27 the enumeration skips the
  unlistable subfolder itself and every other file of the root is read; a root that cannot be
  listed is still lost whole, with its Warn line (`docs/design/lens-profiles.md`).
