# Take FlatCheck's lens-on-every-mount hunk into the LightsCheck copies, then measure the crop-derived pixel pitch

Branch `claude/Take-the-lens-hunk-from-FlatCheck`, worktree
`LightsCheck/.claude/worktrees/Take-the-lens-hunk-from-FlatCheck` (made, on main 6bbf7df). Saved as
`docs/plans/2026-10-27-take-the-lens-hunk-from-flatcheck.md` once approved.

## Context

FlatCheck's task "Identify the lens on every mount" (its ADR 0068, R-108, merged in main by 78da298)
taught `RawReader` to read the lens on Canon, Sony and Pentax raws, not only Nikon's: the maker's ID
by mount (`LensIdOf`), the mount chosen (`MountOf`) and named (`MountLabel`), the range with Canon's
quirks (`RangeOf`), the EXIF lens name, and `.pef` as a raw extension. LightsCheck's `RawReader.cs`
and `ImageLoader.cs` are "adapted" copies (ADR 0003) and still carry `NikonLensId` only; the owner
asked for the hunk to be recopied, with comments that describe LightsCheck, tests on fabricated
frames (no raw in the suite), then a measurement, not an implementation, of a crop-derived pixel
pitch as the default in place of the D3300's 3.92 µm.

Measured today in `E:\Photografie\Code`:

- FlatCheck's `RawReader.cs` and `ImageLoader.cs` have not changed since 78da298 (only 3509278,
  a text fix elsewhere, is newer in `FlatCheck.Core`). The lens hunk is 9a5cc52 + 50cd7ef + 9bc4e94.
- `RangeOf` calls `LensSpec.Close` (1 mm or 5 % of the larger focal), a type of FlatCheck's
  `LensProfiles/` that LightsCheck has no use for: the copy needs a local two-line `Close`.
- DarksCheck took the same hunk on 2026-10-26 (a6023ef on `claude/Take-the-lens-on-every-mount-hunk`,
  its ADR 0023, merge proposed): its `RawReader.cs` is LightsCheck's copy with the hunk already
  applied, the `Close` helper inlined and the comments adapted, so it is the template for the
  code and the ADR wording here; its `FrameInfo` and tests (`RawReaderTests` with the four static
  theories on fabricated values) are the template for ours.
- FlatCheck's R-009 (the cut-data refusal, b9b20a6 + 469da9b) is already whole in the copy
  (ADR 0021: the check before the sample loop and `DataCut`). The only R-009 residue not taken is
  one sentence of 469da9b on `LoadHeader`'s doc comment ("It does not check the data area: a file
  whose pixels are cut short has an intact, usable header, so this read answers it and only the
  full load refuses it"). FlatCheck's SIMPLE-magic check in the same file is its R-008 / ADR 0057,
  a behaviour change ADRs 0018 and 0021 left for a task of its own; it is **not** taken here
  (assumption stated; say so if you meant it).
- Nothing in the window shows the lens; the goldens' lens line comes from FITS fixtures
  (`135 mm (as shot)`), so no golden moves. `README.md` line 247 quotes the raw wording
  (`Nikon ID 74`) and line 42 the extension list.

## Steps (one commit each unless noted)

1. **Requirements first** (`docs/requirements.md`): new `R-065` (Reading frames): the lens ID by
   mount, the mount label, the name, the range rule, `.pef`, and the inventory's lens cell wording
   below, with FlatCheck's measured values as the example; `R-002` adds `.pef`; `R-003` and `R-007`
   say "lens ID, name and mount" instead of "Nikon lens ID" (R-007's example gains `Nikon F`).
2. **Tests first, fabricated** (`tests/LightsCheck.Tests/`): `RawReaderTests` gets `x.pef` in the
   extensions theory and FlatCheck's four static theories (`R065_...`: `LensIdOf` with the D3300
   signature 5356031691206446338 and the Canon/Sony/Pentax values, `MountOf`, `MountLabel` with
   the no-programming-name cases, `RangeOf` with the Canon short-end rule), and the gated
   real-raw test asserts `LensId`. New `InventoryReportTests`: `FrameInfo.From` on a fabricated
   `LoadedFrame` per make carries `LensId`/`LensMount`/`LensName`; `InventoryReport.Write` on an
   `Inventory` built from fabricated `FrameInfo`s (the constructor the existing tests use,
   `SessionFolderTests`, plus `CadenceAnalyzer.Analyze`; `SessionFolder` via `SessionFixture`)
   prints the lens line in both languages for: D3300 with the Samyang (ID 74, Nikon F), Canon 7D II
   (name, ID 198, Canon EF), Pentax K-70 (ID 2263, Pentax K, no range), Sony name without ID,
   and a FITS frame (focal only, unchanged text). Run before the code: red.
3. **The hunk** (`src/LightsCheck.Core/Imaging/RawReader.cs`, `ImageLoader.cs`): `.pef` in
   `Extensions` and the doc comments ("seven"); `FillShootingInfo` with the lens block in a
   try/catch as FlatCheck's (the shot fields LightsCheck added stay); `LensIdOf`, `MountLabel`,
   `MountOf`, `RangeOf` token for token, plus the local `Close`; `LoadedFrame.NikonLensId` becomes
   `LensId` beside `LensMount` and `LensName`. Comments describe LightsCheck: no synthetic flat, no
   Adobe profiles; what the fields are for here is the inventory's lens line. `FrameInfo`:
   `LensId`, `LensMount`, `LensName` in place of `NikonLensId`. `InventoryReport.Lens`: first frame
   with a focal, an ID or a name; after the shot and the chip's range, the lens as the raw names it:
   `"EF50mm f/1.4 USM" (ID 198, Canon EF)`, `ID 74 (Nikon F)`, `"FE 16-35mm F2.8 GM" (Sony E)`,
   `ID 2263 (Pentax K)`; a mount alone prints nothing (same words in Romanian, `ID` is the same).
   `FitsReader.cs`: the one R-009 sentence on `LoadHeader`, in the copy's own words since its
   comment has no ADR 0057 to lean on; the marker line names it.
4. **Docs**: ADR 0025 (the hunk taken, the comments LightsCheck's, the `Close` deviation, the
   residue, the SIMPLE check still out); dated notes on ADR 0003 (`NikonLensId` renamed) and 0021
   (the residue); `docs/design/imaging.md` (fields, the lens block, `.pef`, what differs) and
   `session-and-console.md` (the lens line); `README.md` lines 42 and 247; CHANGELOG Added
   (`.pef`; the lens's name and mount on every make) and Changed (`Nikon ID 74` reads
   `ID 74 (Nikon F)`).
5. **Measure the crop-derived pitch, present, do not implement** (figures already taken with a
   throwaway libraw probe in the scratchpad, on the first light of each session; recorded in the
   plan's last section at wrap-up and in the final message, for the owner's decision):
   - D3300 NEFs: visible mosaic 6016 x 4016, no margins; libraw's `MakerNotes.CameraFormat` is
     `Unknown` (0) on both sessions, so libraw's own format is no source; the raws carry EXIF
     `FocalLengthIn35mmFormat`: 202 for 135 mm (ratio 1.496), 30 for 20 mm (1.500).
   - lensfun's camera table (FlatCheck's `lensfun/slr-nikon.xml`, 1053 bodies, 5.1 MB, CC BY-SA
     3.0): D3300 `cropfactor` 1.523. FlatCheck's nominal fallback (`SensorFormats`): APS-C 1.5.
     Nikon's spec: 23.5 x 15.6 mm → 23.5 / 6016 = 3.906 µm; today's 3.92 is 23.5 / 6000.
   - Pitch by `36 / crop / width` (the crop is diagonal-based, so the correct form is
     `43.27 / crop / sqrt(W² + H²)`; equal on a 3:2 sensor, 4 % apart on a 4:3 one):
     lensfun 1.523 → 3.93 µm (+0.3 % on today's); the raw's 35 mm-equivalent 1.5 → 3.99 µm
     (+1.8 %); own list with Nikon's 23.5 mm → 3.91 µm (−0.3 %).
   - What would move in `docs/validation.json` (arithmetic from the scale, to confirm by
     `/validate` if adopted): Veil `13.29 px = 79.6"` → 79.8" (lensfun) or 81.0" (35 mm-eq);
     Smida's 40.4 "/px → 40.5 or 41.1, its drift −2.8 "/min → −2.8 or −2.9; polar error 4.5'–6.7'
     stays or becomes 4.6'–6.8'. Pixel-unit figures and verdicts do not move.
   - Options for the crop source: (A) FlatCheck's lensfun camera table, shipped beside the exe
     (0.3 %, 1053 bodies; a second 5 MB CC BY-SA copy in the monorepo, a notices entry, a camera
     reader copied from `LensfunReader`); (B) an own list of bodies with their sensor size (exact
     where listed, stale elsewhere, nothing for an unlisted body); (C) the raw's own
     `FocalLengthIn35mmFormat / FocalLength` (no data, no licence, every maker writes it; the
     maker's nominal crop, 1.8 % on the D3300; 0 on a body with no lens data → fall back).
     Recommendation: C as the derived default, 3.92 kept only when the raw gives no 35 mm figure
     and no FITS carries XPIXSZ, `--pixel` overriding as now, the report's pixel source naming it;
     A later if 0.3 % ever matters. The owner decides; nothing of this is implemented here.

## Verification

`scripts/verify.ps1` green in the worktree (never in the main checkout); the new tests red before
step 3 and green after; `dotnet build --no-incremental -warnaserror --no-restore` before the final
verify; no golden changes (`GoldenOutputTests` untouched); `/validate` not needed, no numeric code
changes. The diff read whole before each commit.

## What deviated

- Steps 1 to 4 landed in one commit, not one each: the tests name `LensId`, `LensMount` and
  `LensName`, so the test project does not compile without the code (45 errors before the code,
  recorded as the red state), and a commit that does not build is no step.
- "No golden moves" was wrong for `--help`: the console builds the extension list of its usage
  text from `RawReader.RawExtensions`, so `help.txt` and `en_help.txt` each changed one line,
  `.pef` added to the list (read whole; both lines are this task's `.pef`).
- Two `IDE0055` formatting errors on the widened `FrameInfo` constructor calls, fixed by
  `dotnet format`.

## Follow-ups (not here)

FlatCheck's SIMPLE check (R-008 / ADR 0057) for LightsCheck's `FitsReader`; the pitch default,
once the owner chooses. DarksCheck's copy is already done (its branch awaits the owner's merge).
