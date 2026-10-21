# 0016. The masters' noise stands beside every light found, and the window shows both in bold

Date: 2026-10-21
Status: accepted 2026-10-21

## Context

After ADR 0015 the owner's question stayed: why does 0.8 ADU of light matter when a dark has
7.3 ADU of noise per pixel? The figure to compare with is not a frame's noise but what is left
in the masters: the dark master's per-pixel noise (check 8 prints it, 1.43 ADU on the leak
set, the median of 30 frames) and the lights stack's (sigma / sqrt(N), 3.3 ADU at Smida for
the mean, 1.25 x that for the median). An offset in the master stays whole in the stack while
the noise falls to these, and the ratio grows again with the square root of the pixels looked
at together. The two noises were printed far from the light check 2 finds and never as a
ratio. The owner asked for them beside each light found, as a ratio, and for the noises and
the light figures to be bold in the window.

## Decision

1. **The emphasis travels as data.** `ReportRow` carries `Bold`, the value's substrings the
   window renders bold; the console prints the value as it is, so the goldens and the saved
   report carry no markers. The window's `Emphasis` attached property rebuilds a `TextBlock`'s
   inlines as plain and bold runs from `Rest` and `Bold`; every tab that lists rows uses it
   through the shared `ReportRow` template (R-071). A substring is marked only where it stands
   as a figure of its own (not the tail of a longer number, not glued to a sign) and a row's
   runs are rebuilt once per binding pass.
2. **The stack's noise is one figure in `Analysis`**: `StackNoise.Of` gives the lights' sky
   median and sigma on the reference plane, their count, and the mean and median stack's
   per-pixel noise as the lights' share (sigma / sqrt(N), x `Th.MedianNoiseFactor` for the
   median) plus the dark master's noise in quadrature, since the master is subtracted from
   every light alike and does not average out (the code review of 2026-10-21 caught the
   omission: at Smida 3.34 becomes 3.36 ADU, on a 30-light session at ISO 1600 with the leak
   set's master 3.65 would become 3.92). The dark master's noise is `NoiseVsN.MasterNoise`,
   already check 8's.
3. **Where the ratios go.** Check 2's `lumina constanta` and `lumina pe cadre` rows, its
   verdict's constant-light sentence and the scale's `gasit` end with the light in raw ADU as a
   fraction of the dark master's noise and, with lights, of the stack's
   (`; +0.79 ADU e 0.55 din zgomotul pe pixel al masterului dark (1.43 ADU) si 0.24 din al
   stack-ului de lights (3.3 ADU)`); the scale section's `zgomotul stack-ului` row becomes
   `zgomot in master` with both masters; the second paragraph names the master's noise and the
   50 x 50 patch. The figures bold: the masters' noise wherever it is printed (check 8's
   `master`, `zgomot in master`, the ratios) and the light found (ADU-eq and raw ADU).

## Alternatives rejected

- **Markers in the text** (`**1.43 ADU**`), stripped by the console: the console's words are
  the report, the goldens would carry the markers or a stripping step, and the window would
  parse text the core already knows the structure of.
- **A separate "relevance" verdict** on the ratio: the owner wants the comparison in sight, not
  another threshold; the thresholds of ADR 0014 stand and the ratio explains them.
- **The stack's noise from the lights' local sigma** (the detector's scale): the sky sigma is
  the per-pixel noise of a light; the local sigma is scaled for the hot-pixel detector.

## Consequences

- `StackNoise`, `ReportRow.Bold`, `ScaleReport.AgainstMasters`, `Emphasis`, `RowVm.Bold`, the
  `ReportRow` template; `R029_...`, `R080_...` and `R071_...` guard the words, the bold lists
  and the rendering; the goldens carry the `zgomot in master` row.
- A later row that wants emphasis names its substrings in `Bold`; no other mechanism.
