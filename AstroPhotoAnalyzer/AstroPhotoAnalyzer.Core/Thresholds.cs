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

// Thresholds.cs - every threshold, in one place, each with its reason.
//
// They are empirical rules, not physical constants. Almost all of them came from measuring a
// single image (de_printat.png, the Milky Way, 16 bits) in the conversation that gave birth to
// the tool, and they are a starting point to calibrate on more. Whoever changes one changes its
// comment too.

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// The thresholds every measurement and every text reads, each a named constant with the
    /// reason for its value: an empirical rule of thumb, not a physical constant (ADR 0012).
    /// </summary>
    internal static class Thresholds
    {
        // ------------------------------------------------------------ input

        /// <summary>
        /// Under this median (on the reference channel, 0..1) the image is probably linear. A
        /// linear stack sits at 0.001-0.01; a finished image, even a dark one, at 0.08-0.25. The
        /// tool refuses with a message; it does not measure wrongly.
        /// </summary>
        internal const double LinearMedianMax = 0.02;

        /// <summary>
        /// Over this fraction of samples with value == high_byte * 257, a 16-bit file is a scaled
        /// 8-bit one. On real 16 bits the fraction is ~0.4 % (coincidences); on an upscale it is
        /// 100 %.
        /// </summary>
        internal const double UpscaledFraction = 0.5;

        // ------------------------------------------------------------ tones

        /// <summary>
        /// The levels (out of 255) for the dark counts, for context: the samples of every channel
        /// whose 8-bit level is under the threshold (TonalAnalyzer.Level8Under).
        /// </summary>
        internal const int DarkLevelA = 32;
        internal const int DarkLevelB = 64;

        /// <summary>
        /// The visibility threshold: under this L* the paper separates the tones, but the eye in
        /// room light barely tells them apart - the dim light and the reflections off the paper
        /// and the glass lay a veil over the black. It belongs to the viewing, not to the paper,
        /// so it does not move with the Dmax. A rule of thumb from the conversation that gave
        /// birth to the tool, with no measurement behind it; a grey of L* 15 is the level 0.148,
        /// that is PrintBackgroundLo below, the same rule on the other scale. A sky under it is
        /// an Attention, and the simulation's recommended row has it at or above (ADR 0029).
        /// </summary>
        internal const double LowSeparationLstar = 15;

        /// <summary>Over this L* a pixel is "bright" - the core, the stars.</summary>
        internal const double HighlightLstar = 50;

        /// <summary>
        /// Where the sky usually sits (0..1) in an image made for the screen, and where it must
        /// sit for paper. Rules of thumb from the conversation that gave birth to the tool, not
        /// measurements: on screen a sky at 0.05-0.10 looks "deep black" and good; on paper
        /// everything under its black prints as the same black, so the print version wants the
        /// sky at 0.15-0.25, a dark grey. The reference image had 0.103 (V-001), and on the
        /// plate-solved FITS of the same sky, at Dmax 2.0, the dust reaches paper black at 0.184,
        /// the row 0.19 (V-004). For the window's help texts only; no rule reads them.
        /// </summary>
        internal const double ScreenBackgroundLo = 0.05;
        internal const double ScreenBackgroundHi = 0.10;
        internal const double PrintBackgroundLo = 0.15;
        internal const double PrintBackgroundHi = 0.25;

        // ------------------------------------------------------------ dust and sky (ADR 0025)

        /// <summary>
        /// The side of the patch the dust and the sky are measured on, in pixels of the image:
        /// the L* of a patch's mean linear luminance, not of a pixel, so that noise and isolated
        /// specks do not move it. 32 px are 4 mm of paper at 200 dpi, about what the eye
        /// integrates over a spot. Measured on the parked branch: 16, 32 and 64 px give 0.166,
        /// 0.162, 0.157 on the same image, so the side barely matters.
        /// </summary>
        internal const int DustPatchPx = 32;

        /// <summary>
        /// The L* of a catalogued cloud, for its row of Anexa B: this percentile (a fraction) of
        /// its patches - the cloud's core, not the edges of the disc, which mix with the sky.
        /// </summary>
        internal const double DustCloudFraction = 0.10;

        // ---- the image's classes: dust, sky, band, bright clouds, peaks (ADR 0026)

        /// <summary>
        /// The Milky Way band: the patches with galactic latitude |b| under this many degrees.
        /// Measured on the reference image: at 10 degrees the band is 33 % of the frame and the
        /// Great Rift falls wholly inside it; at 12 degrees, 40 %, with more clean sky inside.
        /// </summary>
        internal const double BandGalacticLatitudeDeg = 10;

        /// <summary>
        /// The band's classes make sense only with the band AND sky outside it, each with at
        /// least this share of the patches: a deep-sky frame wholly inside the band (M8) has
        /// nothing to model the sky from, one wholly outside it (a galaxy) has no local level of
        /// the band. Under the threshold, as without a WCS, the classes are made relative to the
        /// modelled sky (ADR 0033), and the report says why.
        /// </summary>
        internal const double BandMinFraction = 0.10;

        /// <summary>
        /// The band's classes also need a wide field: the patches must span at least this many
        /// degrees of galactic latitude, the band's whole width (2 x BandGalacticLatitudeDeg).
        /// Under that the band cannot show as a strip with sky beside it, however many patches
        /// lie on each side of |b| = 10: on Veil_Starless.fit (9.6 x 6.4 degrees, b from -14.6 to
        /// -3.6, 65 % of the patches "band") the L* profile by |b| is flat (9.1-10.5 in every
        /// 2-degree bin) and the geometric band is 0.5 L* over the sky outside it, 0.6 sigma; on
        /// the reference image (68 degrees of latitude) it climbs from 15 to 25.8 L*, 4.1 L* over
        /// the sky. Such a frame is deep-sky inside the band and takes the sky path (ADR 0037).
        /// </summary>
        internal const double BandMinLatitudeSpanDeg = 20;

        // ---- the classes relative to the modelled sky, without the band (ADR 0033)

        /// <summary>
        /// Without the band, the sky is modelled from ALL the patches, with this percentile of
        /// each cell instead of the median: an object covering half a cell would lift the median
        /// into itself, while the 25th percentile stays on the sky. Chosen on 2026-10-06 on
        /// Rosetta, Orion, Andromeda and the two reference images.
        /// </summary>
        internal const double SkyModelLowPercentile = 25;

        /// <summary>
        /// The sky's noise over the patches: the robust sigma (1.4826 x MAD) of the difference
        /// patch - modelled sky, after one cleaning pass that drops the patches over
        /// SkyNoiseClipK sigma (the object inflates the MAD: on Orion starless the sigma falls
        /// from 2.95 to 1.75 L* with the pass; a second pass would shrink it to 1.46, too much).
        /// </summary>
        internal const double SkyNoiseClipK = 3;

        /// <summary>
        /// A patch is nebula when it is over the modelled sky by more than this many sigma of the
        /// sky, and dust when it is under it by as much. Between 3 and 5 the nebula fraction moves
        /// by 2 % (Rosetta: 14.1 / 12.3 / 10.8 %) and the anchors by under 1.5 L*; 4 is the
        /// middle. The bright-nebula threshold has no constant: it is the Otsu threshold of the
        /// tail above. Measured on 2026-10-06 (ADR 0033).
        /// </summary>
        internal const double SkyNoiseK = 4;

        /// <summary>
        /// The bright nebula's Otsu threshold leaves at least this share of the nebula patches
        /// above it: Otsu maximises the variance between classes, and a few star patches tens of
        /// L* over a faint nebula would be the upper "class", leaving the bright nebula empty
        /// (found at the review of 2026-10-06). On the four images with nebula the upper class is
        /// 15-30 % of the tail (reference 15 %, Orion 28 %, Rosetta 30 %), so 5 % touches no real
        /// split.
        /// </summary>
        internal const double BrightClassMinTailFraction = 0.05;

        /// <summary>
        /// The dust class decides the verdict only when it has at least this share of the
        /// patches; under the threshold, the dust is the darkest sky (SkyDarkestFraction). With
        /// the band, the Great Rift is 9.4 % of the frame; without the band, the dust class finds
        /// only cores or artefacts: 0.8 % on de_printat.png (the Pipe's cores, p25 at L* 2.5,
        /// which ADR 0025 rejected as dust), 0.4 % on Rosetta (the nebula's edge, brighter than
        /// the sky), 0-2.7 % on the others. The owner chose 5 % on 2026-10-06 (ADR 0033).
        /// </summary>
        internal const double DustClassMinFraction = 0.05;

        /// <summary>
        /// On the band path, dust is a patch at least this many L* units under the band's local
        /// level (or under the modelled sky); the sky path uses SkyNoiseK instead (ADR 0033).
        /// "Darker than the band would be there", not "dark in general". On the
        /// reference image the dust-sky difference measured on the Great Rift is 3-6 L*; with 4,
        /// 5, 6 the dust is 11, 9, 8 % of the frame. 5 is the middle.
        /// </summary>
        internal const double DustDepthLstar = 5;

        /// <summary>
        /// A bright cloud is a band patch this many L* units over the band's local level: twice
        /// the dust's depth.
        /// </summary>
        internal const double BrightCloudLstar = 10;

        /// <summary>
        /// The peaks: the brightest this many (a fraction) of the frame's patches, the core and
        /// the brightest clouds.
        /// </summary>
        internal const double PeakFraction = 0.001;

        /// <summary>
        /// The band's local level: the median over a window of this many patches (15 x 32 px =
        /// 480 px on the reference image), then smoothed over BandLocalSmoothPatches: structures
        /// of hundreds of pixels stay, filaments and noise do not.
        /// </summary>
        internal const int BandLocalWindowPatches = 15;
        internal const int BandLocalSmoothPatches = 2;

        /// <summary>
        /// The sky model: the median of the patches OUTSIDE the band over cells of this many
        /// patches, the cells with fewer than SkyModelCellMinPatches sky patches taken from the
        /// nearest one, interpolated linearly to the patches' resolution and smoothed over
        /// SkyModelSmoothPatches. A smooth model, not a constant, so that the gradient (the
        /// top-left corner of the reference image, 11.7 against 24.3 L* at the other end) does
        /// not become dust.
        /// </summary>
        internal const int SkyModelCellPatches = 20;
        internal const int SkyModelCellMinPatches = 20;
        internal const int SkyModelSmoothPatches = 3;

        /// <summary>
        /// The dust's L* for the verdict: this percentile (a fraction) of the patches of the dust
        /// class. Three quarters of the dust keep tones above paper black; the darkest quarter
        /// (the Pipe's cores, black on screen too) prints as flat black. On the reference image
        /// 10 % / 25 % / the median give targets of 0.213 / 0.188 / 0.160; the owner chose 25 % on
        /// 2026-10-02 (ADR 0026).
        /// </summary>
        internal const double DustClassFraction = 0.25;

        /// <summary>
        /// A cloud with at least this many patches gets a row of its own (L*, exact target) and a
        /// circle on the map: 9 patches = 3 x 3 = a square degree at 42"/px, enough for its
        /// percentile not to be a single patch. On the plate-solved FITS: 114 of the 397 clouds
        /// with a patch in the frame (V-004).
        /// </summary>
        internal const int DustCloudMinPatches = 9;

        /// <summary>
        /// The darkest sky: this percentile (a fraction) of the patches of the sky class. The
        /// verdict's dust is the lower of the dust class's DustClassFraction percentile (when the
        /// class decides) and this sky: the darkest extended area, whatever it is. 1 % is the
        /// smallest fraction at which the statistic rests on hundreds of patches and not on a
        /// spot; until ADR 0033 it was the same fraction, over all the frame's patches
        /// (2026-10-02). On the six images of 2026-10-06 the darkest sky differs from the old
        /// rule by under 0.2 L*.
        /// </summary>
        internal const double SkyDarkestFraction = 0.01;

        /// <summary>
        /// How many clouds, from the darkest, the report names (Anexa B and the "fiindca"); the
        /// window shows them all.
        /// </summary>
        internal const int DustCloudReportRows = 15;

        /// <summary>
        /// The ladder the exact target is looked for on: lifts step by step, from Lo to Hi, with
        /// linear interpolation between two steps. 0.05 is under any screen background; 0.60 is a
        /// light grey sky, above which nobody prints; 0.01 is the step of the table's grid, and
        /// the dust's statistic is smooth over it (the interpolation's error, under 0.001).
        /// </summary>
        internal const double ExactTargetLo = 0.05;

        /// <summary>
        /// The largest Dmax accepted, from the user (--dmax, the window's field) or from an ICC
        /// profile: over 4 the reflectance is under 0.01 %, blacker than any printed surface (the
        /// best glossy papers reach ~2.5-2.9). A profile that gives more is damaged.
        /// </summary>
        internal const double DmaxMax = 4;
        internal const double ExactTargetHi = 0.60;
        internal const double ExactTargetStep = 0.01;

        /// <summary>
        /// How far the Dmax moves for the two neighbouring exact targets of Anexa B: the target
        /// moves by ~0.02 per 0.1 of Dmax (measured on the reference image: 1.9 / 2.0 / 2.1 give
        /// 0.208 / 0.187 / 0.166), and the Dmax of a paper on a given printer is rarely known
        /// better than that.
        /// </summary>
        internal const double DmaxSensitivityStep = 0.1;

        /// <summary>
        /// The longest side of the image in the map and preview windows: enough for a maximised
        /// 1080p screen, small enough for a gamma curve and the L* of every pixel to take under a
        /// second.
        /// </summary>
        internal const int PreviewMaxSide = 1600;

        // ------------------------------------------------------------ subsampling

        /// <summary>
        /// The [::s, ::s] step of each measurement - the one of the acceptance scripts.
        /// </summary>
        internal const int TonalStride = 4;
        internal const int PercentileStride = 2;
        internal const int BackgroundStride = 3;

        // ------------------------------------------------------------ noise

        internal const int NoiseTileSize = 64;
        internal const int NoiseTileCandidates = 400;

        /// <summary>
        /// A tile is "smooth" for the noise if max-min over it is at most this many levels out of
        /// 255 (the plateau's tiles need strictly under theirs). It is 6000 out of 65535 - the
        /// acceptance script's threshold - and wide on purpose: the difference between
        /// neighbouring pixels removes a slow gradient, so a tile with a little structure still
        /// measures the noise right. (The plateau has another, stricter threshold.)
        /// </summary>
        internal const double NoiseTileMaxRangeLevels = 6000.0 / 257;

        /// <summary>
        /// Under this many smooth tiles the sigma is not to be trusted, and the report says so.
        /// </summary>
        internal const int NoiseMinTiles = 10;

        /// <summary>
        /// The PLATEAU's tiles (stage 3): range under 6 levels and mean under 70 - clean sky,
        /// without the core - up to 120 tiles from seed 1. They are the other acceptance script's
        /// criteria; on the reference image 20 tiles pass in PlateauMaxAttempts tries, and give
        /// 2.68 px (V-001).
        /// </summary>
        internal const double PlateauTileMaxRangeLevels = 6;
        internal const double PlateauTileMaxMeanLevels = 70;
        internal const int PlateauTileCount = 120;
        internal const int PlateauSeed = 1;

        /// <summary>
        /// Under this sigma (in 8-bit LSB) the noise no longer dithers the quantization: a risk of
        /// banding.
        /// </summary>
        internal const double NoiseBandingLsb = 0.5;

        /// <summary>
        /// The good interval for the print version: small enough to be invisible on paper, large
        /// enough to dither.
        /// </summary>
        internal const double NoisePrintLoLsb = 0.8;
        internal const double NoisePrintHiLsb = 1.5;

        /// <summary>Over this sigma the image is under-denoised for display at 100 %.</summary>
        internal const double NoiseUnderDenoisedLsb = 2.0;

        // ------------------------------------------------------------ export

        /// <summary>
        /// A plateau of identical values longer than this, on paper, is a visible contour.
        /// </summary>
        internal const double PlateauVisibleMm = 1.0;

        /// <summary>
        /// A JPEG whose maximum error is over this many levels is not recommended.
        /// </summary>
        internal const double JpegMaxErrorReject = 10;

        /// <summary>
        /// The samples with a JPEG error over this many levels are counted and located.
        /// </summary>
        internal const double JpegErrorCountLevels = 2;

        /// <summary>
        /// Over this percentage of samples with an error over JpegErrorCountLevels, q100 gets
        /// "Atentie" even when the maximum error is acceptable: the compression hits too widely,
        /// usually in the dark sky. The rule from the first image, as is the 2-level threshold.
        /// </summary>
        internal const double JpegOverThresholdWarnPct = 5;

        /// <summary>
        /// The gamma of the shadow-lift stress test (x^(1/g)): a typical lab auto-correct.
        /// </summary>
        internal const double ShadowLiftGamma = 1.9;

        /// <summary>
        /// The levels strictly between which the histogram's gaps are counted after the lift,
        /// from the first level used there to the last.
        /// </summary>
        internal const int ShadowLiftLevelLo = 8;
        internal const int ShadowLiftLevelHi = 120;

        /// <summary>
        /// Over this many levels missing on the 8-bit path more than on the 16-bit one, the 16-bit
        /// master is mandatory for any edit.
        /// </summary>
        internal const int MissingLevelsWarn = 20;

        // ------------------------------------------------------------ stars

        /// <summary>
        /// The window the local maxima are looked for in (side, px, odd): StarAnalyzer reads it
        /// from here (its half is the border left out); among equal maxima in the window only the
        /// first in the scanning order counts (ADR 0041).
        /// </summary>
        internal const int StarWindow = 25;

        /// <summary>
        /// A star must be this far over the window's base (the median of its border), in 16-bit
        /// units ...
        /// </summary>
        internal const double StarMinAmplitude16 = 3000;

        /// <summary>
        /// ... and under this peak, so as not to be saturated (the profile of a saturated star has
        /// no half).
        /// </summary>
        internal const double StarMaxPeak16 = 60000;

        /// <summary>
        /// The candidates are looked for over the plane's mean + the minimum amplitude of a star.
        /// The acceptance script used 3 x the mean - fine on a sky at 0.10, but at a background
        /// of 0.25 (the print version!) the threshold climbs to 0.75 and stars of 0.55 no longer
        /// pass. The condition here is the necessary one; the real filter is the amplitude over
        /// the local base, which applies anyway.
        /// </summary>
        internal const double StarCandidateAboveMean16 = StarMinAmplitude16;

        /// <summary>
        /// The accepted widths at half maximum; outside them it is a blob or a hot pixel.
        /// </summary>
        internal const double StarFwhmMin = 0.8;
        internal const double StarFwhmMax = 15;

        /// <summary>Over this corner/centre ratio, the corners are soft.</summary>
        internal const double CornerSoftRatio = 1.4;

        /// <summary>The side of a corner region, as a fraction of the image's side.</summary>
        internal const double RegionFraction = 0.2;

        /// <summary>
        /// The side of the central region, as a fraction. Larger than the corner's, so that it
        /// holds enough stars: the centre of an astrophoto is often the core, where the stars are
        /// lost in the nebulosity, and a median of three stars is not a median.
        /// </summary>
        internal const double CenterFraction = 0.4;

        /// <summary>Under this many measured stars the global FWHM is not to be trusted.</summary>
        internal const int StarMinCount = 20;

        /// <summary>Under this many stars in a region, its FWHM is not reported.</summary>
        internal const int RegionMinStars = 5;

        /// <summary>
        /// How many candidates are tried for the plateau's tiles before giving up.
        /// </summary>
        internal const int PlateauMaxAttempts = 5000;

        /// <summary>
        /// Without a FWHM (too few stars) the detail is not measured, only the pixel grid: the
        /// pixel's pitch must be under what the eye resolves. Without a given distance, the
        /// threshold is the labs' convention for a large "quality" print (150-200 dpi): at 180
        /// dpi the pitch is 0.14 mm, invisible from ~0.5 m - arm's length, when you lean in to
        /// look. With a given distance the threshold is the physical one, 25.4 / (0.29 x d): 300
        /// dpi = 0.29 m (reading), 180 = 0.49 m, 100 = 0.88 m, 60 = 1.46 m (the sofa). It is a
        /// limit of the file, not of the detail - a soft nebula may need much less - so it does
        /// not recommend a row.
        /// </summary>
        internal const double FallbackMinDpi = 180;

        /// <summary>
        /// The JPEG qualities of the table. 100 is the only recommendable one; 95 and 90 show why.
        /// </summary>
        internal static readonly int[] JpegQualities = { 100, 95, 90 };

        // ------------------------------------------------------------ background

        /// <summary>The background = the darkest this many pixels (a fraction) ...</summary>
        internal const double BackgroundDarkestFraction = 0.25;

        /// <summary>
        /// ... without the bottom band (a fraction of the height), where the horizon and its
        /// pollution sit.
        /// </summary>
        internal const double BackgroundExcludeBottomFraction = 0.25;

        /// <summary>
        /// The edge regions: the corner is this much of the height x this much of the width (the
        /// region of the acceptance script); the sides are bands of the same thickness, between
        /// the corners.
        /// </summary>
        internal const double EdgeRegionHeightFraction = 0.15;
        internal const double EdgeRegionWidthFraction = 0.20;

        /// <summary>
        /// A cast of over this many levels on the background shows once the background is lifted.
        /// </summary>
        internal const double CastWarnLevels = 1.0;

        /// <summary>
        /// A corner this many levels over the background is a residual gradient or horizon glow.
        /// </summary>
        internal const double CornerGradientLevels = 3.0;

        /// <summary>
        /// A departure is COLOURED if the difference between the channels (max - min of the
        /// delta) exceeds CornerGradientLevels and exceeds this fraction of the mean absolute
        /// departure. Measured on the reference image: the horizon corner has R +9, B -10
        /// (coloured, pollution); the corner the Milky Way enters has +11 / +10 / +16 (neutral -
        /// it is the object, not a gradient). A rule that mistakes the object for the defect is
        /// wrong more often than it is useful, so neutral = informative, coloured = attention.
        /// </summary>
        internal const double CornerColourSpreadRatio = 0.5;

        // ------------------------------------------------------------ size

        /// <summary>
        /// The eye's acuity: 1 minute of arc = 0.29 mm at 1 m. It scales linearly with distance.
        /// </summary>
        internal const double AcuityMmAt1m = 0.29;

        /// <summary>
        /// The default viewing distance of a picture: this many times its diagonal. The print and
        /// framing rule of thumb, "1-2 x the diagonal", has two marks: 1 x the diagonal is the
        /// distance from which the perspective is the lens's (the perspective centre of a normal
        /// lens, whose focal length equals the frame's diagonal), and 2 x is gallery practice,
        /// from where the picture is taken in whole without moving the head. At 1.5 x the picture
        /// subtends ~37 degrees on the diagonal: whole in the central field, but close enough to
        /// look at the detail. It is a presumption about the viewer, not a measurement of the
        /// image; the diagonal is the image's, not the frame's.
        /// </summary>
        internal const double ViewingDistanceFactor = 1.5;

        /// <summary>
        /// At the NATURAL distance, "the star is a point" does not depend on the size: both the
        /// distance from which the star becomes a point and the natural distance grow in
        /// proportion to the width, and their ratio is
        /// fwhm x 1000 / (Wpx x 0.29 x 1.5 x sqrt(1 + r^2)) - a constant of the image. On the
        /// reference image it is 1.018: every size "failed" by 2 %, and the rule fell on the
        /// smallest; with a FWHM of 3.00 instead of 3.13 they would all have passed. A tolerance
        /// of 15 % (about the scatter of the FWHM estimator) makes the test answer the question it
        /// actually can: is the image sharp enough for its natural distance, yes or no. The wall
        /// chooses the size.
        /// </summary>
        internal const double NaturalDistanceTolerance = 1.15;

        /// <summary>
        /// Over this width (cm) the glass, the weight and the transport intervene, not the file.
        /// </summary>
        internal const double OversizeCm = 100;

        /// <summary>
        /// The pixel pitch of a common monitor (96-110 ppi), for the Preview's step-back rule: at
        /// 100 % the file is seen as a print of Wpx x this. A presumption about the screen, not a
        /// measurement; a 27" 4K has 0.16 mm, and the view is that much harsher.
        /// </summary>
        internal const double ScreenPixelMm = 0.25;

        /// <summary>
        /// The passepartout must be at least this many times the frame's width: under that the
        /// frame "squeezes" the image and the white around it has nothing left to do. The
        /// framers' rule; 0 means no passepartout and is not checked.
        /// </summary>
        internal const double PassepartoutMinFrameRatio = 2;

        /// <summary>
        /// The "classic" reference density, only to show that it is not the real limit.
        /// </summary>
        internal const double ReferenceDpi = 300;

        // ------------------------------------------------------------ suggested curves (ADR 0032)

        /// <summary>
        /// The "band" anchor of a curve, with classes: this percentile of the patches of the band
        /// class. Not the median: the band's median sits close to the sky (a wide band takes in
        /// its faint part), and the steep part of the curve would start from the sky. Measured on
        /// 2026-10-05 on starless_rbf0.5_stretch0.17.fit: the 80th percentile gives 0.306, the
        /// 0.32 point of the owner's old curve; with the median (0.225) the noise in the sky grows
        /// by 1.40 x instead of 1.26.
        /// </summary>
        internal const double CurveBandClassPercentile = 80;

        /// <summary>
        /// The "bright clouds" anchor, with classes: the class's 75th percentile gives 0.543 on
        /// the same FITS (the old curve: 0.55). The same two percentiles on the classes relative
        /// to the sky (nebula, bright nebula): on the 0.17 FITS without geometry they give L* 32.5
        /// / 57.3 against 33.2 / 57.7 with the band (ADR 0033).
        /// </summary>
        internal const double CurveCloudClassPercentile = 75;

        /// <summary>
        /// The slopes of the three segments above the sky, those of the owner's old curve for the
        /// 0.17 FITS (0.17 -> 0.165, 0.32 -> 0.42, 0.55 -> 0.76, 0.72 -> 0.92): sky-band,
        /// band-clouds, clouds-peaks. They come from a single curve on a single image; the ratio
        /// between them is the recipe, their size is scaled.
        /// </summary>
        internal const double CurveSlopeSkyBand = 1.70;
        internal const double CurveSlopeBandCloud = 1.48;
        internal const double CurveSlopeCloudPeak = 0.94;

        /// <summary>
        /// Where the peaks go at most: 0.92 in the old curve, with room for a shoulder up to
        /// white.
        /// </summary>
        internal const double CurvePeakTarget = 0.92;

        /// <summary>
        /// The strengths Blanda, Medie, Puternica: how much of the way from the identity to the
        /// whole curve. Chosen on the sheets of the study of 2026-10-05: on the 0.17 FITS the
        /// steps move the bright clouds by ~4 L* each (56, 60, 64), enough to tell apart when
        /// browsing; the first attempt's steps of 0.15 (1 L* at the band) could not be told apart.
        /// </summary>
        internal static readonly double[] CurveStrengths = { 0.5, 0.75, 1.0 };

        /// <summary>
        /// Maxima is offered only when its peaks, with the factor capped so that the spline stays
        /// within 0..1 (ADR 0039), climb by more than this over Puternica's. A choice, not a
        /// measurement: 0.02 of the scale is under 2 L* at the peaks, less than a strength step;
        /// on the 0.17 FITS Puternica reaches 0.919 and Maxima would add nothing.
        /// </summary>
        internal const double CurveMaximaMinGain = 0.02;

        /// <summary>
        /// A pixel is "clipped" when one of its channels is at or over this level; the threshold
        /// of the study of 2026-10-05.
        /// </summary>
        internal const double CurveClipLevel = 0.995;

        /// <summary>
        /// The shadow chart of the curve window goes from 0 to this many times the input of the
        /// curve's fourth point, the band's (the bright clouds' when the dust's point is the
        /// origin, CurveAdvisor.ShadowChartLimit): the dust and the sky sit in the first 10-20 %
        /// of the axis and do not show on the whole chart.
        /// </summary>
        internal const double CurveShadowChartFactor = 1.25;

        /// <summary>
        /// The size of the previews while the window has not yet said how large the gallery area
        /// is (in the tests, or before the first layout): what fits on a Full HD screen under the
        /// parameter bar. A presumption about the screen, not a measurement.
        /// </summary>
        internal const int CurvePreviewDefaultWidth = 1600;
        internal const int CurvePreviewDefaultHeight = 900;

        /// <summary>How many points the curve sampled for a chart has.</summary>
        internal const int CurveChartSamples = 257;

        /// <summary>
        /// The paper's gamut (R-144): a colour is outside when the trip sRGB - printer - back moves
        /// its a*, b* by more than this. Chroma only: the paper black is judged by the Tonuri tab.
        /// 3 is the Delta E threshold of the study of 2026-10-05.
        /// </summary>
        internal const double CurveGamutDeltaAb = 3;

        /// <summary>
        /// The nodes per channel of the round-trip table through the profile; 33 means 35,937
        /// colours passed once through the package (~25 microseconds each); the pixels are read
        /// by interpolation, except those within CurveGamutExactMargin of the threshold, which
        /// take the exact trip once per 8-bit colour (ADR 0042).
        /// </summary>
        internal const int CurveGamutGrid = 33;

        /// <summary>
        /// How close to CurveGamutDeltaAb a pixel's interpolated displacement must be for the
        /// exact trip through the profile to decide instead of the interpolation
        /// (R-144, ADR 0042). The grid alone rounds the gamut's edge inward and counts too few,
        /// by up to 2.4 points on a class (the reference image's dust on baryta: 0.0 % against
        /// 2.4 % exact; 0.45 and 2.2 points even with 65 nodes). With the margin 0.5 the sky on
        /// canvas stays 0.5 points under exact; with 1.0 every class is within three decimals of
        /// the exact figure, and once per 8-bit colour, within 0.2 points (measured 2026-10-23 on
        /// the plate-solved FITS with both of the workshop's profiles and on de_printat.png).
        /// </summary>
        internal const double CurveGamutExactMargin = 1.0;
    }
}
