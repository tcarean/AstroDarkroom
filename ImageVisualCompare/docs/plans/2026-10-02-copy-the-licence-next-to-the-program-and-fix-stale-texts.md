# 2026-10-02 Copy the licence next to the program; correct the README and four stale comments

Status: asked for by the owner on 2026-10-02, right after the adoption of the kit ("rezolva te
rog si cele 3 lipsuri care mi le-ai zis"), and carried out directly. "The three" are read as the
three findings the adoption's report put to the owner, from the list under "What deviated" in
`docs/plans/2026-10-02-adopt-the-starter-kit.md`:

1. About says the full licence "is in the LICENSE file next to this program", and nothing puts
   it there.
2. Two statements of `README.md` contradict the code: the arrow keys move the divider by "1 px"
   and "10 px" (the code moves it in device-independent pixels, R-031), and "nu stau două perechi
   în memorie simultan" (the current pair stays loaded while a new one decodes, R-008).
3. Four code comments are stale: the summary of `Viewport`, the comment on the sequential decode
   in `ImagePairLoader.LoadAsync`, the comment on the caption colour in `DarkTitleBar.ApplyTo`,
   and the summary of `MemorySnapshot.PrivateBytes`.

The branch starts from `claude/Adopt-the-starter-kit-process` (2471a58), which is not merged yet:
the fixes need its gate, its requirements and its ADRs. The adoption keeps its three commits.

## Steps

1. **The licence** (a behaviour change, so the kit's rules "Requirements first" and "Test before
   fix" apply): a `draft` row R-074; a test `R074_...` in `LicenseHeaderTests` that fails today;
   the root `LICENSE` linked into the Wpf project and copied to its output, which `dotnet
   publish` carries into the publish folder; R-074 `implemented`; ADR 0027 for the choice; a
   `Fixed` line in `CHANGELOG.md`; the design note `versioning-and-licence.md` and the README's
   "Licență" say where the file goes. Checked by hand as well: the application's own build
   folder and a publish folder hold `LICENSE`.
2. **The README**: the arrow keys' step is in logical pixels, the same as physical ones only at
   100 % display scaling; while a new pair decodes, the current one is still in memory. The
   comment in `MainWindow.ShowPair` that said the same as the README is corrected with it.
3. **The four comments**, comments only, no code. The ADRs that recorded them as stale get a
   dated note (the kit's ADR 0024), so the record holds whichever branch is merged first; the
   code map's line on the sequential decode gets a pointer.
4. Gate, the validation cases (they cover `Imaging/`), the application launched once, the design
   notes the branch touched read again. Two commits: the licence, then the texts.

## Verification

`scripts/verify.ps1` green; `R074_...` red before the fix and green after; `LICENSE` present
beside `ImageVisualCompare.Wpf.exe` in `bin/Release/net9.0-windows/` and in a publish folder;
the two validation cases green; `git diff` under `src/` shows one csproj item and comment lines
only.

## What deviated

Done on 2026-10-02 in two commits on `claude/Copy-the-licence-next-to-the-program-and-fix-stale-texts`,
the gate green at each (150 tests), the two validation cases green against their recorded
figures, the application launched on the real pair and looked at.

- **`R074_TheLicenceSitsNextToTheApplication` failed first** ("no LICENSE next to the application
  in ...\tests\ImageVisualCompare.Tests\bin\Release\net9.0-windows") and passed after the item.
  It looks in the test output, where the project reference copies the file; the application's
  own `bin/Release/net9.0-windows/` and a `win-x64` self-contained publish folder were checked by
  hand: both hold `LICENSE`, 35 149 bytes, identical with the root's.
- **Publishing for `win-x64` rewrote two lock files**: `dotnet publish -r win-x64` added a
  `net9.0/win-x64` section to Core's `packages.lock.json` and a `net9.0-windows7.0/win-x64`
  section to the application's. They were restored, since the scratch publish was only a check.
  The same will happen at the first real release: `scripts/release.ps1` publishes with these
  arguments after it edits `CHANGELOG.md` and `Directory.Build.props`, and the `/release` skill's
  step 6 stops when anything else changed; in CI's locked mode the publish would fail instead.
  AstroPhotoAnalyzer and FlatCheck have no runtime section in their lock files either, so this is
  a kit question, left for the owner.
- **The README's table still says "1 px" and "10 px"**; the sentence after it says those are
  logical pixels, 1.5 and 15 screen pixels at 150 % scaling. Its two smaller differences from the
  code, the spacing around `·` in the status bar example and "peste 400%", were not among the
  three and are left.
- **ADRs 0011 and 0015 got dated notes** (the kit's ADR 0024) rather than edits, because the
  adoption branch that holds them may be merged first. The code map's sentence on the sequential
  decode got a pointer.
- **`scripts/check-design.ps1 -Since claude/Adopt-the-starter-kit-process`**, not `-Since main`:
  this branch stands on the unmerged adoption branch, and `main` would list the adoption's notes
  too. The six notes it listed were read against the change; only `versioning-and-licence.md`
  needed new text.
