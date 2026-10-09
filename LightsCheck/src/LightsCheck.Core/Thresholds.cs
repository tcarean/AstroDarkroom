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

    /// <summary>Every n-th pixel of a tile enters its median: the 128 x 128 tile is read at 32 x 32.</summary>
    internal const int BackgroundSampleStep = 4;

    /// <summary>Half-side of the central window the noise is read in, in pixels (2000 x 2000 on a 24 MP frame).</summary>
    internal const int NoiseWindowHalf = 1000;

    /// <summary>Every n-th pixel of the noise window enters the median absolute deviation.</summary>
    internal const int NoiseSampleStep = 3;

    /// <summary>Sigma of the Gaussian the detection image is smoothed with, in pixels.</summary>
    internal const double DetectionSmoothSigma = 1.0;

    // ---- detection (R-014)

    /// <summary>Detection threshold on the smoothed image, in sigmas of that image.</summary>
    internal const double DetectionSigmas = 5.0;

    /// <summary>A component is a star when its area is in [MinStarArea, MaxStarArea] pixels.</summary>
    internal const int MinStarArea = 5;
    internal const int MaxStarArea = 400;

    /// <summary>A "bright" star peaks above this many sigmas of the frame's noise.</summary>
    internal const double BrightPeakSigmas = 50.0;

    /// <summary>A star measured for shape peaks above this many sigmas ...</summary>
    internal const double ShapePeakSigmas = 20.0;

    /// <summary>... and below this fraction of the scale, so a saturated star does not vote on shape.</summary>
    internal const double ShapePeakMaxFraction = 0.8;

    /// <summary>How many stars, the brightest unsaturated, are measured for shape.</summary>
    internal const int ShapeStars = 400;

    /// <summary>Radius of the circular window the moments are taken in, in pixels.</summary>
    internal const int MomentRadius = 6;

    /// <summary>How many centroid iterations the moments take.</summary>
    internal const int MomentIterations = 3;

    /// <summary>A shape measurement is kept when its FWHM is in [MinFwhm, MaxFwhm] pixels.</summary>
    internal const double MinFwhm = 1.0;
    internal const double MaxFwhm = 15.0;

    /// <summary>A star counts as elongated at or above this ratio when the fraction of elongated stars is reported.</summary>
    internal const double ElongatedRatio = 1.3;

    /// <summary>Stars at or above this ratio vote on the elongation angle.</summary>
    internal const double AngleVoteRatio = 1.2;

    /// <summary>At least this many stars must vote for an angle to be reported.</summary>
    internal const int AngleVoteMin = 6;

    /// <summary>Zones for the FWHM by radius, as fractions of the half-diagonal (1000 and 2200 px on a 6016 x 4016 frame).</summary>
    internal const double CentreZoneFraction = 0.2765;
    internal const double MidZoneFraction = 0.6082;

    /// <summary>At least this many stars in a zone for its median to be reported.</summary>
    internal const int ZoneStarsMin = 5;

    /// <summary>The halo ratio is read on this many of the brightest measured stars.</summary>
    internal const int HaloStars = 40;

    /// <summary>Halo: the flux in the annulus (HaloInner, HaloOuter] over the flux within HaloCore, in pixels.</summary>
    internal const double HaloCore = 3.0;
    internal const double HaloInner = 5.0;
    internal const double HaloOuter = 10.0;

    /// <summary>
    /// A pixel within this many ADU of the top of the scale is saturated: 1 ADU on an integer
    /// scale (a raw, a 16-bit FITS), the same fraction of the scale (1/4096, the 12-bit raw's
    /// one count) on a float frame whose scale is 0..1, where a 1 ADU margin would cover it all.
    /// </summary>
    internal static double SaturationMargin(double top) => top > 2 ? 1.0 : top / 4096.0;

    // ---- trails (R-015)

    /// <summary>A trail is a component of more than this many pixels ...</summary>
    internal const int TrailMinArea = 250;

    /// <summary>... whose bounding-box diagonal is longer than this, in pixels ...</summary>
    internal const double TrailMinLength = 80.0;

    /// <summary>... and that fills less than TrailMaxFill of its bounding box plus TrailFillSlack pixels (a line, not a blob).</summary>
    internal const double TrailMaxFill = 0.25;
    internal const int TrailFillSlack = 60;

    // ---- registration (R-016)

    /// <summary>How many stars, the brightest, saturated included, are kept for registering the frame.</summary>
    internal const int RegistrationStars = 600;
}
