# 2026-10-09 Build LightsCheck

## Goal

A C# application (console and WPF window, kit process, like FlatCheck) that opens a Siril
session folder of light frames and repeats the analysis made by hand on the Veil session of
2026-10-06: focus, exposure and noise, dew, trailing, dithering (how often, how far, in which
directions, where positions repeat), drift, and a list of frames to leave out of the stack
with the reason for each. Any camera, lens and filter; the values a raw does not carry are
inputs with D3300 defaults. The raws are only read.

Approved in chat on 2026-10-09 with the list of what is measured (the owner's four answers:
console too, elongation thresholds 1.35 / 1.6, a plain text list of rejected names, the Siril
session folder as input) and the four decisions below.

## Decisions taken with the plan

1. The readers and the WPF pieces (dark theme, title bar, Mvvm, "Despre") are copied from
   FlatCheck, not shared through a library (ADR 0003).
2. The input is the session folder: `lights/` required (or the raws directly in the folder),
   `biases/` optional; raws and FITS both read (ADR 0004).
3. Version 0.x during the build, 1.0.0 at the first release; GPL-3.0-or-later, the version in
   `Directory.Build.props`.
4. `docs/validation/2026-10-06-veil/frames.csv` from the Python analysis is the numeric oracle
   on the Veil session, with tolerances; the validation cases run outside the gate with
   `LIGHTSCHECK_TESTDATA_LARGE` (ADR 0004).

## Steps

Each step is a task on its own `claude/` branch in its own worktree, built on the previous
step's branch; the owner merges them in order. A code review is asked at every step and what
makes sense is fixed before the commit.

1. **Bootstrap from the kit** (`claude/Bootstrap-LightsCheck-from-the-kit`): copy the kit,
   rename, kit.json, CLAUDE.md, licence, packages (libraw only), the readers copied from
   FlatCheck, the prototype scripts frozen, ADRs 0001 to 0005, the first real feature so no
   sample code stays: the console inventory of a session (R-001 to R-011), the goldens, V-001
   on the Veil session. Gate green. The session restarts in the project folder after the
   merge, so the kit's hooks load.
2. **Measure one light frame** (`claude/Measure-one-light-frame`): `FrameAnalyzer` in the
   core: levels and noise per CFA channel, the interpolated green, the tile background,
   star detection and moments (FWHM, elongation, angle), zones, halo, long trails, the star
   list for registration. Synthetic frames with known answers in the tests; V cases on the
   Veil frames against `frames.csv` (FWHM 0.1 px, elongation 0.05, background 1 ADU).
   Target under 5 s per 24 MP frame on one thread.
3. **Analyze the series** (`claude/Analyze-the-series`): registration (shift vote and
   similarity fit), cadence, trends, drift and rotation, the RA/DEC axes, dithering (events,
   the "every N frames" table, the size and axis tables, coverage, runs, verdict), the frame
   classes and reasons with the thresholds in `Thresholds.cs`, the Romanian report, CSV, the
   text list of rejected names; the console mode; a golden on a synthetic session; validation
   on Veil (the rejected list of the 2026-10-09 report, 47 dither events, 9 without effect).
4. **Add the window** (`claude/Add-the-window`): `LightsCheck.Wpf` with the tabs Rezumat,
   Cadre, Dithering, Calitate, Respinse, Despre; the inputs with D3300 defaults; parallel
   analysis with progress and cancel; the charts drawn as in FlatCheck; the star crops of
   the selected frame; the exports. XAML smoke tests with `LIGHTSCHECK_DUMP_SCREENS`.
5. **Validate, document, release 1.0.0** (`claude/Validate-and-release-1-0-0`): the V cases,
   the design notes, `/docs-health`, CHANGELOG, `release.ps1 -Version 1.0.0`.

## Verification

`scripts/verify.ps1` green at every step; `/validate` with `LIGHTSCHECK_TESTDATA_LARGE` set to
`E:\Photografie\Astro` reproduces the figures of `docs/validation.md`; the rejected list on the
Veil session equals the one in `docs/validation/2026-10-06-veil/raport.md`.

## What deviated

(filled at wrap-up of each step)

- Step 1: the kit's tests of its own scripts, hooks and guide were dropped rather than kept
  (ADR 0001); ImageSharp was not copied (no preview is shown); the kit's `src/` and `tests/`
  layout was kept instead of FlatCheck's root layout (ADR 0001).
- Step 2: the saturation margin became a function of the scale (a float 0..1 FITS would have
  read every pixel as saturated); frames smaller than a tile are one tile.
- Step 3: the dither move threshold is 5 px, not the 3 px the plan's list had: with 3 px the
  Veil session's back-to-back drifts (1 to 7 px) counted as 15 extra moves and the nine presses
  "without effect" of the hand report disappeared (ADR 0007). The default console run is now
  the whole series and the inventory moved behind `--inventory`. The RA axis is read from a
  plate-solved FITS at the session root when there is one (the plan had it as "maybe").
