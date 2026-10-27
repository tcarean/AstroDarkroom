# 0002. The tool measures lights, lists what to leave out, and never modifies a frame

Date: 2026-10-09
Status: accepted 2026-10-09; the D3300's 3.92 um as the default pixel size superseded by 0026

## Context

The owner's request of 2026-10-09: an application that opens the raws of a session and
repeats the analysis of the Veil lights (focus, exposure, dew, trailing, dithering frequency
and amount, a list of frames to drop with the reason), usable with any camera, lens and
filter, with the values a raw does not carry (pixel size, read noise) entered in the window,
D3300 defaults filled in. The instruction that came with the photos: they are only analysed,
never modified, under no circumstances.

## Decision

LightsCheck reads frames and writes reports: text, CSV, PNG charts and a list of file names to
leave out, all beside the session or where the user asks, never inside `lights/` or
`biases/`, and never a Siril `.seq` (the owner chose a plain list of names on 2026-10-09).
It decides numbers and verdicts; it does not move, rename, convert or delete a frame, and does
not stack. What a raw does not say is an input with a default, never a guess: the pixel size
(3.92 um for the D3300), the read noise (measured from `biases/` when present), the focal
length (from the raw, overridable for a lens without a chip).

## Alternatives rejected

- **Writing the selection into Siril's `.seq`.** Asked and declined for now: it writes into
  `process/`, and a list of names is enough to apply by hand.
- **Moving rejected frames to a subfolder.** Convenient, but it is a modification of the
  session the owner ruled out.
- **Guessing the pixel size from the camera model.** A table of cameras goes stale and a wrong
  entry would look measured; a field with a default is honest.

## Consequences

Every requirement that writes a file names where it writes. The window's inputs carry the
D3300 defaults and are plain fields, so any camera works by typing its values.
