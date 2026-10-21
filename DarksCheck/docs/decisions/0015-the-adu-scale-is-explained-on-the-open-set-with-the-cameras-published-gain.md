# 0015. The ADU scale is explained on the open set, with the camera's published gain

Date: 2026-10-20
Status: accepted 2026-10-20

## Context

After ADR 0014 named 0.20 ADU-eq of constant light on the leak set, the owner asked how so
little can matter and what the ADU scale is. The answer is arithmetic on figures the result
already holds (the file's range, the normalisation, the quantisation step, the read noise, the
dark current, the thresholds in raw ADU, what was found, the lights' sky and the stack's noise)
plus one figure the tool cannot measure: the electrons per ADU. The owner wants the answer in
the application, computed on the open set, in a tab named "Explicatii scara ADU", and the
electrons from a good source.

The source: photonstophotos.net (Bill Claff), "DxOMark Derived Sensor Characteristics" and
"DxOMark Derived Gain versus Measured ISO", read on 2026-10-20 from the pages' own data (the
chart's series `Nikon D3300_12`, gain = 2^y at nominal ISO 100 .. 25600 with y = 3.136,
2.096, 1.048, 0.074, -0.928, -1.921, -2.942, -3.893, -4.922; the table row: full well 34,446
e-, read noise 2.0 e-, QE 56 %, unity ISO 841 measured). The site says the method derives the
gain from DxOMark's SNR curves and overestimates it on small, noisy sensors. It agrees with the
owner's own biases: 3.0 ADU of read noise at ISO 1600 x 0.53 e-/ADU = 1.6 e-, against the
source's 2.0 e-. Nothing closer exists for this camera: sensorgen.info is gone and the forums
quote figures of unknown origin.

## Decision

1. **One source of words, in the core.** `ScaleReport` builds the rows and the four paragraphs
   from `DarkSeriesResult`; the console prints the block `EXPLICATII SCARA ADU` at the end of
   the report (so `darkscheck-raport.txt` carries it) and the window's tab binds the same rows
   and paragraphs (R-078, R-080). The window computes nothing (the rule "Computed only in the
   core").
2. **The camera's published figures live in `CameraFacts`**, one camera so far, with the
   provenance and the caveat in the words every time they are printed
   (`photonstophotos.net din curbele DxOMark, citit 2026-10-20; metoda supraestimeaza castigul
   pe senzorii mici`). The gain at the set's ISO is interpolated on the log2 scale between the
   listed stops; a camera the table does not know reads `necunoscut`, never a guess. The
   figures are not measured by the tool, so no validation case pins them; this ADR is their
   record, and a better source replaces them with a dated note here.
3. **What the rows say.** The thresholds are printed in raw ADU on the set, so the owner sees
   that 0.15 ADU-eq is 0.60 ADU at ISO 1600; `gasit` carries what check 2 found in ADU-eq, raw
   ADU and electrons; the sky and the stack noise rows need lights and say so without them.
4. **What the paragraphs say**, with the set's numbers: ADU against ADU-eq; why an offset in
   the master is not noise and does not average out in the stack; 1 ADU against the sky and the
   stack's noise; the thresholds, "measured" against "visible".

## Alternatives rejected

- **A page in the README or the About window.** The table must be computed on the open set;
  a static page cannot carry the set's numbers.
- **A card in Rezumat.** The owner asked for a tab; the Rezumat's cards are the checks'
  verdicts, and the scale is a help, not a verdict.
- **Measuring the gain from the data** (photon transfer on the lights or the biases). Needs
  flats or pairs at several levels the session does not have; the published figure, marked as
  such, serves the explanation.
- **Leaving the electrons out.** The owner asked for the figure when a good source exists; the
  provenance in the words keeps it honest.

## Consequences

- `CameraFacts`, `ScaleReport`, the block in `DarkReport.Write`, the tab `ScaleView`, the rows
  and paragraphs on `MainViewModel`; `R080_...` and `R078_...` guard them; the goldens carry
  the block.
- A second camera means a second entry in `CameraFacts` with its own source and date.
