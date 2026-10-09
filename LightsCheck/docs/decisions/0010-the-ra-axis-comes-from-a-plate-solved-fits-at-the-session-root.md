# 0010. The RA axis comes from a plate-solved FITS at the session root, else from the trails

Date: 2026-10-09
Status: accepted 2026-10-09

## Context

The dithering tables name the image axes as RA and DEC (which axis the mount moved along,
where the backlash sits), and the drift is read per axis (R-025). A raw says nothing about
the sky orientation. The plan listed a plate solve as "maybe". The hand analysis of the Veil
session read the orientation from Siril's stacked `result.fit`, which carries the WCS of its
plate solve; the trailed frames of the same session all streak within a few degrees of the
image's Y axis, which is RA on a mount that tracks badly in RA.

## Decision

`SeriesRun.WcsRaAxis` reads the FITS files at the session root, in name order, and takes the
first whose `CTYPE1` starts with `RA`: the axis of the PC (or CD) matrix row of RA with the
larger coefficient is the RA axis (`PC1_2` over `PC1_1` means Y). Without such a file, the
RA axis is the image axis within `Th.TrailAxisTolerance` (25 degrees) of the mean trail
direction of at least `Th.TrailAxisMinFrames` (2) bad frames whose angle coherence is above
`Th.TrailAxisCoherence` (0.8), stated in the report as an assumption; with neither, the axes
stay unnamed. The source is printed with the axis (`plate solve (WCS)`, or the trails).

## Alternatives rejected

- **An option or an input for the axis.** The owner would have to know it; the solved
  result already sits in every Siril session that was stacked.
- **Plate-solving in LightsCheck.** A catalogue and a solver for one letter of output; the
  session root has the answer when Siril ran.
- **Always from the trails.** A session with no trailed frames (the full-moon Veil-broadband
  session of 2026-07-31, one bad frame) has no answer that way; the WCS has.

## Consequences

V-008 records `raAxis` as `Y` with the WCS source on the Veil session. A session root with a
solved FITS of another target (never borrowed: the file is read where it lies) would name
the axes wrongly; the report prints the source so the reader can tell. The fallback's three
constants are in `Thresholds.cs` under "the RA axis".
