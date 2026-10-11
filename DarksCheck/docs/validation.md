# Validation

The project's memory of "how well it works, measured on what". Every case lives in
`validation.json` (read by `ValidationTests`, trait `Category=Validation`, not in the gate,
ADR 0006) and is explained here. A number changes only after a re-measurement, with a dated
line under History saying old value, new value and why. The sessions are whole folders of
camera raws under `E:\Photografie\Astro`, the folder `DARKSCHECK_TESTDATA_LARGE` names; a
case names its session relative to it. A case whose session is absent returns early with a
message.

The hand analysis these cases reproduce is `docs/validation/2026-08-12-smida/report.md` (the
report delivered on 2026-10-11), with its figures in `results.json`,
`results_supplementary.json` and the CSV files beside it, from the Python scripts in
`prototype/`, which read the same libraw through rawpy.

## Kinds of case

| Kind | What runs | What is recorded | Tolerance |
| --- | --- | --- | --- |
| `inventory` | `InventoryRun` on the session | the counts of darks, biases and lights, the camera, the dominant exposure and ISO of the darks, the start of the first dark, the span and the median interval of the darks, their long pauses, the two verdicts of check 1 | 0.01 s exposure, 1 s span, 0.1 s interval; counts and words exact |

## Cases

| ID | Session | Kind | Figures | Measured on |
| --- | --- | --- | --- | --- |
| V-001 | `Milkyway-Core/2026-08-12-Smida` (Nikon D3300, Tokina 11-20 at 20 mm, SA GTi) | inventory | 20 darks, 30 biases, 32 lights, `Nikon D3300`, darks 119.2 s at ISO 400, first dark 2026-08-13 00:48:53, span 2399.2 s, interval 120.0 s, no pause over 5 s, darks vs lights `Ok`, bias `Ok` (0.05 s) | 2026-10-11 |
| V-002 | `Flats and Darks/Darks/2026-10-10-ISO1600-120s-15.8celsius/darks` (the library, Nikon D3300, 15.2-16.5 °C room) | inventory | 30 darks, no biases, no lights, 120.2 s at ISO 1600, first dark 2026-10-10 23:03:24, span 3628.2 s, interval 121.0 s, no pause over 5 s, both verdicts `Unmeasured` | 2026-10-11 |

## History

- 2026-10-11: V-001 and V-002 recorded from the EXIF read of the Python prototype
  (`prototype/nefexif.py`, `docs/validation/2026-08-12-smida/*_per_frame.csv`) and confirmed
  by the console on the same folders.
