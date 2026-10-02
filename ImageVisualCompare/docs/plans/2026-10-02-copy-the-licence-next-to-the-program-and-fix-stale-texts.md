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

(filled at wrap-up)
