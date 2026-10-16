# 0010. The check-1 constants (pause, exposure tolerance, bias limit) come from the Smida and library sessions

Date: 2026-10-16
Status: accepted 2026-10-16

## Context

Check 1 (R-006, R-007) reads three constants of `Thresholds.cs` that ADR 0007 does not cover,
since it records the thresholds of checks 2 to 8 only: `Th.PauseSeconds` (5 s, a gap in the
cadence), `Th.ExposureTolerance` (2 %, two exposures that count as the same) and
`Th.BiasMaxExposureSeconds` (1 s, what still counts as a bias). The step-5 documentation audit
of 2026-10-16 found them stated without a reason.

## Decision

- **A pause over 5 s is a gap.** On the two real sessions (`docs/validation.md`, V-001 and
  V-002) the camera shoots back to back: the Smida darks of 119.2 s start every 120.0 s and
  the library's darks of 120.2 s every 121.0 s, so the pause between the end of one frame and
  the start of the next is 0.8 s, and EXIF times carry a 1 s granularity (a pause can read
  -0.2 s and is clamped to 0, R-006). Five seconds is well above that jitter and below the
  shortest deliberate interruption the owner makes (a battery change, a look at the frames:
  minutes).
- **Two exposures within 2 % are the same setting.** The D3300 records the same 120 s setting
  as 119.2 s (Smida) and 120.2 s (the library), 0.8 % apart; 2 % takes both and still
  separates 120 s from 60 s or 90 s, the owner's other exposures.
- **A bias is at most 1 s.** The owner's biases are 1/20 s to 1/4000 s; a frame of several
  seconds in `biases/` is a short dark, whose dark current would reach the read-noise and
  dark-current figures (R-041).

## Alternatives rejected

- **Deriving the pause threshold from the series' own median interval** (a gap as 1.5 x the
  median): a series with one long pause and few frames moves its own threshold; a fixed value
  reads the same on every session.
- **An exact exposure match**: 119.2 against 120.2 would split a session's own darks from its
  lights.

## Consequences

The three constants stay in `Thresholds.cs`; a change re-runs `/validate` as any constant of
that file does (the rule "Measured on the sessions"). V-001 and V-002 pin the pauses and the
settings they were read from.
