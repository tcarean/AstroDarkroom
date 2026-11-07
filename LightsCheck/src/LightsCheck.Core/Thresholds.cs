// SPDX-License-Identifier: GPL-3.0-or-later
//
// Copyright (C) 2026 Tudor Carean
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

namespace LightsCheck.Core;

/// <summary>
/// Every threshold and tuning constant of the analysis, in one place, with the value the
/// Python prototype used on the Veil session (ADR 0006). The meaning of each is in
/// docs/requirements.md; the reasons in docs/design/frame-analysis.md.
/// </summary>
internal static class Th
{
    // ---- background and noise (R-013)

    /// <summary>Side of the tiles whose medians make the background grid, in pixels.</summary>
    internal const int BackgroundTile = 128;

    /// <summary>
    /// Every n-th pixel of a tile enters its median: the 128 x 128 tile is read at 32 x 32.
    /// </summary>
    internal const int BackgroundSampleStep = 4;

    /// <summary>
    /// Half-side of the central window the noise is read in, in pixels (2000 x 2000 on a 24 MP
    /// frame).
    /// </summary>
    internal const int NoiseWindowHalf = 1000;

    /// <summary>
    /// Every n-th pixel of the noise window enters the median absolute deviation.
    /// </summary>
    internal const int NoiseSampleStep = 3;

    /// <summary>Sigma of the Gaussian the detection image is smoothed with, in pixels.</summary>
    internal const double DetectionSmoothSigma = 1.0;

    // ---- detection (R-014)

    /// <summary>Detection threshold on the smoothed image, in sigmas of that image.</summary>
    internal const double DetectionSigmas = 5.0;

    /// <summary>
    /// A component is a star when its area is in [MinStarArea, MaxStarArea] pixels.
    /// </summary>
    internal const int MinStarArea = 5;
    internal const int MaxStarArea = 400;

    /// <summary>A "bright" star peaks above this many sigmas of the frame's noise.</summary>
    internal const double BrightPeakSigmas = 50.0;

    /// <summary>A star measured for shape peaks above this many sigmas ...</summary>
    internal const double ShapePeakSigmas = 20.0;

    /// <summary>
    /// ... and below this fraction of the scale, so a saturated star does not vote on shape.
    /// </summary>
    internal const double ShapePeakMaxFraction = 0.8;

    /// <summary>How many stars, the brightest unsaturated, are measured for shape.</summary>
    internal const int ShapeStars = 400;

    /// <summary>Radius of the circular window the moments are taken in, in pixels.</summary>
    internal const int MomentRadius = 6;

    /// <summary>How many centroid iterations the moments take.</summary>
    internal const int MomentIterations = 3;

    /// <summary>
    /// A shape measurement is kept when its FWHM is in [MinFwhm, MaxFwhm] pixels.
    /// </summary>
    internal const double MinFwhm = 1.0;
    internal const double MaxFwhm = 15.0;

    /// <summary>
    /// A star counts as elongated above this ratio when the fraction of elongated stars is
    /// reported.
    /// </summary>
    internal const double ElongatedRatio = 1.3;

    /// <summary>Stars above this ratio vote on the elongation angle.</summary>
    internal const double AngleVoteRatio = 1.2;

    /// <summary>At least this many stars must vote for an angle to be reported.</summary>
    internal const int AngleVoteMin = 6;

    /// <summary>
    /// Zones for the FWHM by radius, as fractions of the half-diagonal (1000 and 2200 px on a 6016
    /// x 4016 frame).
    /// </summary>
    internal const double CentreZoneFraction = 0.2765;
    internal const double MidZoneFraction = 0.6082;

    /// <summary>At least this many stars in a zone for its median to be reported.</summary>
    internal const int ZoneStarsMin = 5;

    /// <summary>The halo ratio is read on this many of the brightest measured stars.</summary>
    internal const int HaloStars = 40;

    /// <summary>
    /// Halo: the flux in the annulus (HaloInner, HaloOuter] over the flux within HaloCore, in
    /// pixels.
    /// </summary>
    internal const double HaloCore = 3.0;
    internal const double HaloInner = 5.0;
    internal const double HaloOuter = 10.0;

    /// <summary>
    /// A pixel within this many ADU of the top of the scale is saturated: 1 ADU on an integer
    /// scale (a raw, a 16-bit FITS), the same fraction of the scale (1/4096, the 12-bit raw's
    /// one count) on a float frame whose scale is 0..1, where a 1 ADU margin would cover it all.
    /// </summary>
    /// <param name="top">
    /// The top of the frame's scale above black, in ADU (or 1.0 on a 0..1 float frame).
    /// </param>
    /// <returns>
    /// The margin in the scale's units: 1, or <paramref name="top"/>/4096 when the top is 2 or
    /// less.
    /// </returns>
    internal static double SaturationMargin(double top) => top > 2 ? 1.0 : top / 4096.0;

    // ---- trails (R-015)

    /// <summary>A trail is a component of more than this many pixels ...</summary>
    internal const int TrailMinArea = 250;

    /// <summary>... whose bounding-box diagonal is longer than this, in pixels ...</summary>
    internal const double TrailMinLength = 80.0;

    /// <summary>
    /// ... and that fills less than TrailMaxFill of its bounding box plus TrailFillSlack pixels (a
    /// line, not a blob).
    /// </summary>
    internal const double TrailMaxFill = 0.25;
    internal const int TrailFillSlack = 60;

    // ---- registration (R-016, R-018)

    /// <summary>
    /// How many stars, the brightest, saturated included, are kept for registering the frame.
    /// </summary>
    internal const int RegistrationStars = 600;

    /// <summary>The brightest stars of each list that vote on the coarse shift.</summary>
    internal const int VoteStars = 300;

    /// <summary>The vote histogram covers +-VoteRange px in bins of VoteBin px.</summary>
    internal const int VoteRange = 2000;
    internal const int VoteBin = 8;

    /// <summary>
    /// Match tolerance of the first iteration and of the following ones, in pixels; how many
    /// iterations.
    /// </summary>
    internal const double MatchToleranceCoarse = 8.0;
    internal const double MatchToleranceFine = 3.0;
    internal const int MatchIterations = 4;

    /// <summary>Fewer matched stars than this and the registration failed.</summary>
    internal const int MinMatches = 10;

    // ---- dithering (R-019 to R-021)

    /// <summary>
    /// A step between consecutive frames at or above this many pixels is a dither move. Not
    /// 3 px: on the Veil session the frames shot back to back, with no move at all, drifted
    /// 1 to 7 px in two minutes (tracking and backlash), and 3 px counted 15 of those as moves.
    /// </summary>
    internal const double DitherStep = 5.0;

    /// <summary>
    /// A pause longer than this many seconds before a frame is a deliberate pause (a manual
    /// dither).
    /// </summary>
    internal const double DitherPause = 5.0;

    /// <summary>The size classes of the moves: [5, 10), [10, 20), [20, ...) px.</summary>
    internal static readonly double[] DitherSizeBounds = [10.0, 20.0];

    /// <summary>A move counts on an axis when its component is at least this many pixels.</summary>
    internal const double AxisStep = 3.0;

    /// <summary>
    /// Coverage radii: another frame within these many pixels is the same spot, nearly, or close.
    /// </summary>
    internal const double CoverageTight = 3.0;
    internal const double CoverageMid = 5.0;
    internal const double CoverageLoose = 10.0;

    /// <summary>
    /// A run is this many consecutive frames or more within a radius of its first.
    /// </summary>
    internal const int RunMinFrames = 3;

    /// <summary>
    /// Hot pixels: fine while no more than this many consecutive frames sit within CoverageTight of
    /// the first of them.
    /// </summary>
    internal const int HotPixelRunMax = 3;

    // ---- walking noise against the drift, and the sensor's pattern (R-043, R-046, R-084)

    /// <summary>
    /// The dash the drift draws inside the longest run at CoverageLoose (the drift per exposure
    /// times the run's frames minus one) under which nothing walks, whatever the dithering: a
    /// rule of thumb placed between the 1.9 px of the Smida session (20 mm, a clean stack) and
    /// the 11 px of Veil (135 mm, worms), measured on 2026-10-10 (ADR 0015); to tune on more
    /// sessions.
    /// </summary>
    internal const double WalkingStreakVisible = 3.0;

    /// <summary>
    /// Walking noise: from this fraction of the registered frames up, the frames inside the
    /// stretches whose dash reaches WalkingStreakVisible are too many for the stacking's
    /// rejection to drop their streak (R-084): a rule of thumb placed between the 8 % of the
    /// Veil session of 2026-11-06 (a clean stack) and the 61 % of the Veil session of
    /// 2026-10-06 (worms), measured on 2026-11-07 (ADR 0039); to tune on more sessions.
    /// </summary>
    internal const double WalkingSharedFraction = 0.25;

    /// <summary>
    /// The pattern piles in place when the fraction of one frame's pattern the stack keeps
    /// (PatternSurviving, R-048) is above this factor times the ideal 1 / n (R-084): placed
    /// between the 1.37 of the Veil session of 2026-10-06 and the 7.5 of Smida, measured on
    /// 2026-11-07 (ADR 0039).
    /// </summary>
    internal const double PatternPilingFactor = 1.5;

    // ---- the drift pairs and the polar alignment (R-049, R-050, ADR 0016)

    /// <summary>
    /// The drift is read over the pairs of consecutive frames with no pause above DitherPause
    /// between them, where nothing was pressed; with fewer than this many such pairs it falls
    /// back to the pairs that were not a move, approximate (on Veil nine presses of 3 to 5 px
    /// halved the RA drift that way, measured on 2026-10-24).
    /// </summary>
    internal const int DriftMinPairs = 3;

    /// <summary>
    /// The drift the polar alignment can cause in one exposure, as a fraction of the session
    /// FWHM, under which it costs nothing: a smear of a third of the FWHM adds under 1.03 of
    /// elongation (a Gaussian of sigma s smeared over L reads sqrt(s^2 + L^2 / 12) / s).
    /// </summary>
    internal const double PolarDriftFwhmFraction = 1.0 / 3;

    /// <summary>
    /// The registered span, in hours, under which the field rotation is not read for the polar
    /// error's upper bound: the per-frame rotation scatters by about 10" once the dithers'
    /// projection is taken out (Veil, 2026-10-24), so an hour at 1'/h is six times the noise
    /// and a ten-minute run would read noise as arcminutes per hour.
    /// </summary>
    internal const double PolarRotationMinHours = 1.0;

    /// <summary>
    /// The side of the central square of the raw mosaic kept from every light for the sensor's
    /// pattern (R-046); the whole frame when it is smaller.
    /// </summary>
    internal const int PatternCrop = 512;

    /// <summary>
    /// A pixel of the pattern above this many sigmas of one frame's high-pass noise is hot:
    /// sigma clipping rejects it when the frames moved. The pattern below that is reported as
    /// a sigma, not a count: a count at 3 sigma sat on the steep tail of the Veil pattern, where
    /// a 20 % change of the noise estimate moved it four times (ADR 0015).
    /// </summary>
    internal const double PatternHotSigma = 10.0;

    /// <summary>
    /// Two frames closer than this share the sensor's pattern pixel for pixel: their pattern adds
    /// up instead of averaging out (R-048).
    /// </summary>
    internal const double PatternSharePx = 1.5;

    /// <summary>
    /// The per-pixel median across fewer frames than this still carries too much of the frames' own
    /// noise to read the pattern.
    /// </summary>
    internal const int PatternMinFrames = 7;

    /// <summary>
    /// The median is clean of stars while the largest cluster of frames at CoverageTight holds
    /// under this fraction of the registered frames.
    /// </summary>
    internal const double PatternClusterFraction = 0.5;

    // ---- frame classes (R-022)

    /// <summary>
    /// Median elongation at or above which a frame is bad, and at or above which it is marginal.
    /// </summary>
    internal const double ElongationBad = 1.6;
    internal const double ElongationMarginal = 1.35;

    /// <summary>
    /// A frame whose median FWHM exceeds this factor of the session's median of the good frames is
    /// soft.
    /// </summary>
    internal const double SoftFwhmFactor = 1.3;

    /// <summary>
    /// A frame with fewer detected stars than this fraction of the session's median has clouds or
    /// haze.
    /// </summary>
    internal const double CloudStarFraction = 0.6;

    /// <summary>
    /// A frame exposed under this fraction of the session's usual exposure is bad (R-085): it
    /// brings almost no signal, and Siril's normalisation would stretch its noise up to the
    /// others' level. Half, from the owner's call on the Veil session of 2026-11-06, where a
    /// 34.9 s frame among 120 s ones (29 %) leaves and a 137.5 s one stays (ADR 0040).
    /// </summary>
    internal const double ExposureShortFraction = 0.5;

    /// <summary>
    /// An exposure within this many percent of the session's usual one counts as the usual one
    /// (R-085): the raws write the shutter time with small differences a camera does not mean.
    /// </summary>
    internal const double ExposureSamePct = 2.0;

    /// <summary>
    /// A frame whose green background differs from the median of its neighbours by more than this
    /// percentage has a background jump.
    /// </summary>
    internal const double BackgroundJumpPct = 8.0;

    /// <summary>
    /// How many frames on each side make the neighbourhood of the background jump test.
    /// </summary>
    internal const int BackgroundNeighbours = 2;

    // ---- trends (R-023)

    /// <summary>
    /// How many good frames at each end of the night make the first/last comparison.
    /// </summary>
    internal const int TrendFrames = 10;

    /// <summary>
    /// Dew: the median FWHM of the last good frames exceeds the first by this fraction ...
    /// </summary>
    internal const double DewFwhmRise = 0.15;

    /// <summary>... and the halo ratio rose by this much.</summary>
    internal const double DewHaloRise = 0.02;

    /// <summary>
    /// Focus held when the median FWHM moved by less than this fraction between the ends.
    /// </summary>
    internal const double FocusHeldFraction = 0.10;

    /// <summary>
    /// The read-noise swamping ratio (shot variance over read variance) under which a channel is
    /// read-noise limited.
    /// </summary>
    internal const double SwampingRatioOk = 10.0;

    // ---- the RA axis (R-025)

    /// <summary>
    /// At least this many bad frames with a coherent trail direction (coherence above
    /// TrailAxisCoherence) vote on the RA axis ...
    /// </summary>
    internal const int TrailAxisMinFrames = 2;
    internal const double TrailAxisCoherence = 0.8;

    /// <summary>
    /// ... and their mean direction must be within this many degrees of an image axis.
    /// </summary>
    internal const double TrailAxisTolerance = 25.0;
}
