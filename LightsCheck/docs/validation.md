# Validation

The project's memory of "how well it works, measured on what". Every case lives in
`validation.json` (read by `ValidationTests`, trait `Category=Validation`, not in the gate,
ADR 0004) and is explained here. A number changes only after a re-measurement, with a dated
line under History saying old value, new value and why. The sessions are whole folders of
camera raws under `E:\Photografie\Astro`, the folder `LIGHTSCHECK_TESTDATA_LARGE` names; a case
names its session relative to it. A case whose session is absent returns early with a message.

The hand analysis these cases reproduce is `docs/validation/2026-10-06-veil/raport.md` (the
report delivered on 2026-10-09) and `frames.csv` (its per-frame figures, from the Python
scripts in `prototype/`, which read the same libraw through rawpy).

## Kinds of case

| Kind | What runs | What is recorded | Tolerance |
| --- | --- | --- | --- |
| `inventory` | `InventoryRun` on the session | the counts of lights and biases, the camera, the exposure and ISO of the first frame, the start of the first frame, the span, the duty | 0.01 s exposure, 1 s span, 0.05 points duty |

## Cases

| ID | Session | Kind | Figures | Measured on |
| --- | --- | --- | --- | --- |
| V-001 | `Veil/2026-10-06-3h` (Nikon D3300, Samyang 135 f/2.8, L-eNhance, SA GTi) | inventory | 87 lights, 30 biases, `Nikon D3300`, 120.2 s at ISO 1600, first start 2026-10-06 23:35:03, span 12166.2 s (3 h 22 min), duty 86.06 % (10470.4 s of shutter: 86 x 120.2 + 133.2) | 2026-10-09 |

The span and the duty come from the console's run on the session on 2026-10-09:
`2026-10-06 23:35:03 -> 2026-10-07 02:57:49 (3 h 22 min); integrare 2 h 54 min din 87 cadre
(86 % din interval)`; the Python analysis of the same day gave the same interval from the EXIF
times with subseconds (`raport.md`: 23:35:03 - 02:57:49, 86 %). Frame 75 carries a 133.2 s
shutter in its EXIF (libraw and PIL agree, `frames.csv` has it); the console prints
`expuneri diferite` for the session, and the report's "+11 % background on frame 75" is that
longer exposure (133.2 / 120.2 = 1.108), not a passing light, a correction noted in the copy
of the report.

## How to re-measure

```bash
LIGHTSCHECK_TESTDATA_LARGE=E:\Photografie\Astro dotnet test LightsCheck.sln -c Release --filter "Category=Validation"
```

Or `/validate` in Claude Code, which prints expected versus measured for every case and stops on
drift instead of editing the figures.

## History

- 2026-10-09: V-001 recorded from the console's first run on the Veil session. The camera
  string was first written as `NIKON D3300` from the EXIF `Model`; libraw normalises the make
  and model, and the measured value `Nikon D3300` replaced it before the case was accepted.
  The duty was first written as 85.96 % from 87 x 120.2 s; the measured 86.06 % counts frame
  75's 133.2 s, and replaced it the same way.
