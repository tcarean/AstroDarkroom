# 0010. The check-1 constants (pause, exposure tolerance, bias limit) come from the Smida and library sessions

Date: 2026-10-16
Status: accepted 2026-10-16

## Context

Three constants of `Thresholds.cs` are read by check 1 (R-006, R-007) and were stated without
a reason, which the step-5 documentation audit of 2026-10-16 found: `Th.PauseSeconds` (5 s, a
pause in the cadence that counts as a gap), `Th.ExposureTolerance` (2 %, two exposures that
count as the same setting; also the filter `FrameStack` applies when it drops a frame of other
settings from a stack, R-020) and `Th.BiasMaxExposureSeconds` (1 s, what still counts as a
bias). ADR 0007 covers the thresholds of checks 2 to 8 only. What the two real sessions show
(`docs/validation.md`, V-001 and V-002; `docs/validation/2026-08-12-smida/report.md`,
section 1):

- the Smida darks of 119.2 s start every 120.0 s and the library's darks of 120.2 s every
  121.0 s, so a dark follows the previous one after 0.8 s; EXIF times carry a 1 s granularity,
  so a pause can read -0.2 s (clamped to 0, R-006);
- the Smida lights of the same night run at 127 to 244 s intervals, the dithering and settling
  between frames, so their report counts 31 pauses over 5 s, the longest 125 s;
- the Smida lights are 119.2 s but for one of 118.5 s (0.6 % under), the library's darks are
  120.2 s against Smida's 119.2 s (0.8 % apart), all of them the camera's 120 s setting;
- the Smida biases are 1/20 s; the measured dark current is 0.14 ADU per 120 s (V-003), so a
  5 s frame would carry 0.006 ADU of it against 1.26 ADU of read noise, while its hot pixels
  would carry 1/24 of the darks' amplitude.

## Decision

- **A pause over 5 s is a gap.** On darks it marks an interruption of a series that shoots
  back to back (0.8 s between frames, 1 s of EXIF jitter); on lights it marks every dithering
  and settling pause, which the cadence line then counts, as information, not a verdict.
- **Two exposures within 2 % are the same setting.** The 118.5 s light stays with the 119.2 s
  ones, the library's 120.2 s darks match Smida's 119.2 s, and a frame at another setting
  (ISO or an exposure outside the 2 %) is left out of a stack with `setari diferite`.
- **A bias is at most 1 s.** The owner's measured biases are 1/20 s; at 1 s the dark current
  and the hot pixels a frame carries are 1/120 of the darks', small against the read noise
  the biases are there to measure (R-041).

## Alternatives rejected

- **Deriving the pause threshold from the series' own median interval** (a gap as 1.5 x the
  median): a series with one long pause and few frames moves its own threshold, and the
  lights' dithering pauses would never count; a fixed value reads the same on every session.
- **An exact exposure match**: 118.5 against 119.2 would split a session's own lights, and
  120.2 against 119.2 would keep a library of the same setting from matching.

## Consequences

The three constants stay in `Thresholds.cs`; a change re-runs `/validate` as any constant of
that file does (the rule "Measured on the sessions"). V-001 and V-002 pin the pauses and the
settings they were read from; R-006 and R-007 cite this ADR. Whether the owner shoots biases
longer than 1/20 s, or exposures other than 120 s, is not recorded anywhere; the constants
are set from the two sessions measured.
