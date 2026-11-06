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
        /// each cell: the median. ADR 0033 took the 25th percentile so that an object covering
        /// half a cell would not lift the model into itself, but on a Milky Way frame without a
        /// WCS the 25th percentile sinks into the Great Rift, which is then never under the sky:
        /// on the owner's Smida frame of 2026-08-12 the dust class was 0.9 % of the patches
        /// against 9.5 % with the WCS. With the median, and SkyNoiseK 3, the two paths agree on
        /// that frame (dust 9.8 % against 9.5 %, L* 4.90 against 4.85) and on the reference
        /// FITS run without its WCS (8.2 against 7.9); on the deep-sky frames the verdict's dust
        /// does not move and the nebula class shrinks by about a third. Measured on 2026-11-06
        /// (ADR 0085).
        /// </summary>
        internal const double SkyModelPercentile = 50;

        /// <summary>
        /// The sky's noise over the patches: the robust sigma (1.4826 x MAD) of the difference
        /// patch - modelled sky, after one cleaning pass that drops the patches over
        /// SkyNoiseClipK sigma (the object inflates the MAD: on Orion starless the sigma falls
        /// from 2.95 to 1.75 L* with the pass; a second pass would shrink it to 1.46, too much).
        /// </summary>
        internal const double SkyNoiseClipK = 3;

        /// <summary>
        /// A patch is nebula when it is over the modelled sky by more than this many sigma of the
        /// sky, and dust when it is under it by as much. The sigma is measured on the 32 px
        /// patches, where pixel noise is averaged away (0 to 10 levels of added noise move it
        /// from 1.17 to 1.33 L* on a denoised frame), so it follows the stretch and the stars,
        /// not the denoising. ADR 0033 took 4 with the 25th-percentile model; with the median
        /// model (SkyModelPercentile), 3 puts the dust class of the Smida Milky Way frame on
        /// the band path's (9.8 % of the patches, L* 4.90, against 9.5 % and 4.85; with 4, 7.2 %
        /// and 4.20), finding 73 % of the band path's dust patches. The bright-nebula threshold
        /// has no constant: it is the Otsu threshold of the tail above. Measured on 2026-11-06
        /// (ADR 0085).
        /// </summary>
        internal const double SkyNoiseK = 3;

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
        /// How many clouds, from the darkest, the report names (Anexa B and the "fiindca") and the
        /// map window labels on the image; its list shows them all.
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
        /// without the core - at every one of PlateauPositions random positions from seed 1
        /// (ADR 0060; until then up to 120 tiles). They are the other acceptance script's
        /// criteria; on the reference image 20 of the 5000 positions pass, and give 2.68 px
        /// (V-001).
        /// </summary>
        internal const double PlateauTileMaxRangeLevels = 6;
        internal const double PlateauTileMaxMeanLevels = 70;
        internal const int PlateauSeed = 1;

        /// <summary>
        /// How many random positions the plateau tries, every one of them: the share of the frame
        /// a count of tiles stands for is that count over this (the draws are uniform), so the
        /// shares have the same precision on every image (ADR 0060). 5000 positions of 64 px are
        /// about 90 % of a 23 MP frame's area, drawn with replacement.
        /// </summary>
        internal const int PlateauPositions = 5000;

        /// <summary>
        /// The banding verdict (ADR 0060): a risk when the clean tiles whose plateau is longer
        /// than PlateauVisibleMm at the print's dpi are more than this share of the frame, in
        /// percent. A tenth of a percent is about five tiles of 64 px on a 23 MP frame, 4 cm2 on
        /// an 80 cm print: a contour is a contour wherever the eye lands, and the cure (16 bits,
        /// or dither at the export) is cheap. Measured on 2026-10-27 over the whole grid: the
        /// reference image 0.00 % (25 clean tiles, none over 1 mm at 202 dpi); the owner's
        /// 2026-08-12 core denoised by Syqon at 0.5 / 0.75 / 1: 0.18 / 0.24 / 0.44 % at 185 dpi,
        /// the share growing with the denoise while the mean plateau (43 / 16 / 4.7 px) shrank,
        /// which is why the mean no longer decides; the four FITS frames of the validation cases
        /// (the plate-solved core, whose sky keeps a noise of 2.0 LSB, Rosetta, Orion, the Veil):
        /// no clean tile at all.
        /// </summary>
        internal const double PlateauFlatFrameWarnPct = 0.1;

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
        /// A plateau of identical values longer than this, on paper, is a visible contour: a tile
        /// whose plateau passes it at the print's dpi counts towards PlateauFlatFrameWarnPct.
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
        /// ... and this many times the border's robust sigma: 1.4826 x the median absolute
        /// deviation of the 96 border pixels about a plane fitted to them, so that a nebula's
        /// slope is not read as noise (the raw deviation about the median lost 14 of the 64 stars
        /// of the DarkSky golden scene and a quarter of the reference image's 7163, against an
        /// eighth about the plane). The absolute floor stays beside it, for the denoised image
        /// whose sigma is near 0 (ADR 0054). The floor alone let the grain of the stretched noise
        /// through: its window maxima stand 3 to 4 sigmas over the border median, and the floor
        /// (3000/65535 = 0.046) sits on the expected maximum of 625 noise samples (0.044 on the
        /// starless reference FITS). Measured on 2026-10-27 on the aligned Rosetta pair (a kept
        /// bump is a star when the frame without stars is lower at its peak by at least half the
        /// amplitude): stars have a median SNR of 12.5 (p10 4.6), grain 3.2 (p90 4.3). "Stars"
        /// left on the starless frames at 8 / 10 / 12 / 15 sigmas, with the raw deviation:
        /// starless_rbf0.5_stretch0.17.fit 42 / 9 / 2 / 0 (6297 before),
        /// starless_rbf0.5_stretch0.1.fit 52 / 11 / 5 / 0, Rosetta 2 / 1 / 1 / 0 (2858 before),
        /// the Veil 2 / 0 / 0 / 0 (22 485 before); about the plane, at 10 / 12 / 15: the 0.17
        /// FITS 14 / 5 / 0, the Veil 5 / 1 / 0, Rosetta 2 / 1 / 0, the 0.1 FITS 8 at 12, and the
        /// Rosetta frame with stars keeps 649 stars and 1 grain at 12. So 12 keeps every starless
        /// frame under half of StarMinCount (at most 8, on the 0.1 FITS) and costs about 10 % of
        /// the faint stars against 10, the median FWHM of the reference image moving under
        /// 0.05 px. The owner chose 12 on 2026-10-27, and the plane the same day.
        /// </summary>
        internal const double StarMinSnr = 12;

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

        /// <summary>
        /// Under this many measured stars per megapixel of the frame the FWHM is not to be
        /// trusted either (ADR 0062): a count alone cannot tell a few dozen soft blobs a denoiser
        /// left on a starless 23 MP frame from as many stars on a small crop. Measured on
        /// 2026-10-27: frames with stars at 76 (Rosetta), 136 (Orion), 272 (the reference
        /// image) and 332 (Andromeda) per megapixel; the owner's 2026-08-12 core denoised by
        /// Syqon at its strongest, starless, at 2.1 (48 blobs of median FWHM 6.98 px, which gave
        /// a "real detail" of 836 px across the width), and every other starless frame under 1.
        /// </summary>
        internal const double StarMinPerMegapixel = 10;

        /// <summary>Under this many stars in a region, its FWHM is not reported.</summary>
        internal const int RegionMinStars = 5;

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

        /// <summary>
        /// The background's colour = the sky patches of R-044 (ADR 0055) without the bottom band
        /// (a fraction of the height), where the horizon and its pollution sit.
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
        /// An edge region is measured on its sky pixels alone (the patches R-044 calls sky) when
        /// they are at least this share of it; under it, on every pixel, and the row says so
        /// (ADR 0056). Measured on 2026-10-27 on five real images: the lowest sky share of a
        /// region was 41 %, the bottom edge of the two Milky Way cores, where the horizon glow
        /// and the Rift's foot are classed band, dust and bright cloud; the glow's blue deficit
        /// kept the flag on the sky pixels there. A quarter leaves room under that and keeps a
        /// glow bright enough to be classed nebula over most of a region from vanishing with it.
        /// </summary>
        internal const double RegionSkyMinFraction = 0.25;

        /// <summary>
        /// A cast is a sky colour farther than this from a grey of the same lightness, in CIE
        /// L*a*b* chroma (sqrt(a*^2 + b*^2)), ADR 0057. Two is about the smallest difference an
        /// eye sees beside a grey reference (2.3 is the usual figure); on a print the only
        /// reference is the paper's white, far away in lightness, and a sky's cast begins to
        /// read at 3 to 5. Measured on 2026-10-27 on the five real images' sky colours (ADR
        /// 0055): 1.0 (the reference image), 1.4 (Rosetta), 1.8 (the 2026-08-12 core, calibrated
        /// photometrically and neutral to the eye), 2.0 (the plate-solved FITS), 4.8 (the Veil,
        /// plainly green). The rule before it, an excess of 1 level of green or of red over blue
        /// (a* 0.7 at any sky level), flagged four of the five; a gamma lift keeps the channels'
        /// ratio, so the cast does not grow with the background as that rule assumed (the
        /// 2026-08-12 sky lifted to L* 25 stays at chroma 2.0).
        /// </summary>
        internal const double CastWarnChroma = 3.0;

        /// <summary>
        /// A corner this many levels over the background is a residual gradient or horizon glow.
        /// </summary>
        internal const double CornerGradientLevels = 3.0;

        /// <summary>
        /// A departure is COLOURED if the difference between the channels (max - min of the
        /// delta) exceeds CornerGradientLevels and exceeds this fraction of the mean absolute
        /// departure. Measured on the reference image against the darkest quarter (ADR 0014):
        /// the horizon corner had R +9, B -10 (coloured, pollution); the corner the Milky Way
        /// enters +11 / +10 / +16 (neutral - it is the object, not a gradient). A rule that
        /// mistakes the object for the defect is wrong more often than it is useful, so neutral =
        /// informative, coloured = attention. Against the sky patches (ADR 0055), each region on
        /// its own sky pixels (ADR 0056), the same corners read +4.1 / -4.0 / -17.1 (coloured)
        /// and +4.2 / +2.9 / +5.6 (neutral: the band's own, bluer, pixels no longer count).
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
        /// The paper's gamut (R-144, ADR 0081): a colour is outside when the trip sRGB - printer -
        /// back, with the black point compensation towards the paper and back, moves it by more
        /// than this, the whole difference (Delta E76 on L*, a*, b*), lightness included. Until
        /// 2026-11-05 the rule counted a*, b* alone, over 3 (the Delta E of the study of
        /// 2026-10-05), and missed the dark saturated dust the paper prints lighter. Measured on
        /// 2026-11-03 against Lightroom Classic's destination gamut warning (relative, BPC) on the
        /// Smida Milky Way TIFF through the WhiteWall Photo Rag Baryta profile: Lightroom marks
        /// 0.93 % of the image; this rule at 4, exact per pixel, marks 1.33 %, hits 72 % of
        /// Lightroom's pixels and half of its own are Lightroom's: IoU 0.42, the best of every
        /// rule tried (at 3 it is 0.26 with 3.1 % marked, at 5 it is 0.36; the chroma rule 0.19
        /// with 0.45 %; the profile's own gamt tag 0.03 with 36 %); as built, with the hybrid of
        /// ADR 0042, 1.37 %, 75 % and IoU 0.48. Lightroom's rule is not published.
        /// </summary>
        internal const double CurveGamutDeltaE = 4;

        /// <summary>
        /// The nodes per channel of the round-trip table through the profile; 33 means 35,937
        /// colours passed once through the package (~25 microseconds each); the pixels are read
        /// by interpolation, except those within CurveGamutExactMargin of the threshold, which
        /// take the exact trip once per 8-bit colour (ADR 0042).
        /// </summary>
        internal const int CurveGamutGrid = 33;

        /// <summary>
        /// How close to CurveGamutDeltaE a pixel's interpolated displacement must be for the
        /// exact trip through the profile to decide instead of the interpolation (R-144, ADR
        /// 0042). The grid alone rounds the gamut's edge inward and counts too few, by up to 2.4
        /// points on a class (the reference image's dust on baryta: 0.0 % against 2.4 % exact;
        /// 0.45 and 2.2 points even with 65 nodes). With the margin 0.5 the sky on canvas stays
        /// 0.5 points under exact; with 1.0 every class is within three decimals of the exact
        /// figure, and once per 8-bit colour, within 0.2 points (measured 2026-10-23 on the
        /// plate-solved FITS with both of the workshop's profiles and on de_printat.png, under
        /// the chroma rule of the time). Re-measured on 2026-11-05 under the whole-difference
        /// rule with the compensation (ADR 0081), the margin still 1.0: the hybrid is within 0.14
        /// points of the exact per-pixel figure on every class (the FITS on baryta: all 1.943
        /// against 1.917, dust 11.10 against 10.96; on canvas all 2.430 against 2.494, dust 15.25
        /// against 15.35; de_printat.png all 0.292 against 0.236, dust 0.55 against 0.45), with
        /// 5 170, 7 347 and 1 916 exact trips per measure.
        /// </summary>
        internal const double CurveGamutExactMargin = 1.0;

        /// <summary>
        /// The nodes per channel of the soft proof's table (R-160, ADR 0066): the colour the paper
        /// shows for each sRGB node, through the profile's tables of the intent, read per pixel by
        /// trilinear interpolation. Measured on 2026-10-30 against the exact per-pixel proof on
        /// 8 000 colours, half of them dark and nearly neutral as a sky is: 65 nodes stay within
        /// Delta E76 1.0 at the 99th percentile on both lab profiles (max 0.7 on the baryta, 1.4
        /// on the canvas), where 33 nodes reach 2.4 to 5.2 at the maximum on the canvas; a table
        /// of 65 builds in 1.6 to 2.2 s with the rows in parallel.
        /// </summary>
        internal const int ProofGrid = 65;

        /// <summary>
        /// The longest side of the reduced copy the soft proof is made on (R-160). The preview's
        /// PreviewMaxSide (1600) fits a Full HD render; the proof is shown full screen on a 4K
        /// screen too, so a long side under the screen's would be enlarged there. 2400 is a presumption
        /// about the screen, not a measurement; a 23 MP image reduces to 2.7 MP.
        /// </summary>
        internal const int ProofMaxSide = 2400;

        /// <summary>
        /// The nodes of the paper fit's table over L* (R-175, ADR 0083): the largest chroma the
        /// paper holds, searched exactly at each node (ten bisections through the profile) and
        /// read bilinearly between them. 101 nodes, one per unit of L*: with 121 hue nodes the
        /// table builds in 1.3 s with the rows in parallel (measured 2026-11-06 on the lab's
        /// baryta profile) and differs from the exact per-pixel search by 0.004 in the chroma
        /// factor at the median, 0.025 at most, on the two baryta profiles.
        /// </summary>
        internal const int GamutFitLstarNodes = 101;

        /// <summary>
        /// The nodes of the paper fit's table over the hue, 0 to 360 degrees inclusive: 121, one
        /// per 3 degrees (R-175, ADR 0083; the measurement of GamutFitLstarNodes).
        /// </summary>
        internal const int GamutFitHueNodes = 121;

        /// <summary>
        /// The band of chroma, in units, around the table's largest chroma within which the exact
        /// rule through the profile is searched for a pixel that would land near the edge (R-175,
        /// ADR 0083): every pixel whose chroma reaches within this of the table's edge, or passes
        /// it, gets the search along its own hue at its own L*; the others keep the table's
        /// answer, which is within 0.025 of the exact search in the chroma factor on the baryta
        /// profiles and 0.28 at most on the canvas (measured 2026-11-06), so a band of 0.6 holds
        /// the true edge on every measured pair. The table alone, fitting onto its own edge,
        /// leaves 0.19 % of the plate-solved FITS outside on baryta, 0.59 % on canvas and 0.36 %
        /// of the Smida TIFF on the WhiteWall baryta; with the search, 0.001 %, 0.003 % and
        /// 0.000 % (the table in docs/design/paper-fit.md).
        /// </summary>
        internal const double GamutFitEdgeMargin = 0.6;

        /// <summary>
        /// The knee of the Exacta variant (R-176): 1, a hard fit, only the pixels outside moved,
        /// onto the edge. Measured 2026-11-06 with the probe of ADR 0083 (a quarter reduction) on
        /// the plate-solved FITS through the lab's baryta: touches 1.88 % of the pixels, the chroma
        /// given up 0.70 at the median, 4.2 at the 99 % mark; on the Smida TIFF through the
        /// WhiteWall baryta 1.37 %, 1.85 and 14.1. V-016 holds the built code's figures.
        /// </summary>
        internal const double GamutFitKneeExact = 1.0;

        /// <summary>
        /// The knee of the Lina variant (R-176): the chroma is compressed smoothly from this share
        /// of the edge, so that a gradient of growing saturation keeps its steps instead of
        /// flattening on the edge. Measured 2026-11-06 with the probe of ADR 0083: touches 4.06 %
        /// of the FITS's pixels (2.26 % of the TIFF's), the chroma given up 0.47 (0.74) at the
        /// median; V-016 reads 3.81 % and 0.41 on the tonal subsample.
        /// </summary>
        internal const double GamutFitKneeSoft = 0.9;

        /// <summary>
        /// The knee of the Foarte lina variant (R-176): from this share of the edge. Measured
        /// 2026-11-06 with the probe of ADR 0083: touches 6.11 % of the FITS's pixels (3.50 % of
        /// the TIFF's), the chroma given up 0.65 (0.53) at the median (V-016: 6.22 % and 0.58);
        /// from 0.7 it would touch 8.54 % (5.34 %) for no further gain in the fit, which is why
        /// the gentlest variant stops here.
        /// </summary>
        internal const double GamutFitKneeSofter = 0.8;

        /// <summary>
        /// A pixel whose chroma is under this is grey for the paper fit: never touched, never
        /// outside by the chroma (R-175). A quarter of an 8-bit step's chroma near black.
        /// </summary>
        internal const double GamutFitMinChroma = 0.05;

        /// <summary>
        /// The share of the pixels outside that the recipe's tone band must hold (R-176): the band
        /// is "under L* x" with x the smallest whole L* holding this share of them. 90 %: the
        /// outside pixels of the measured images are the dark dust, 97 to 99 % under L* 15, and
        /// a band that holds all of them would reach the few saturated bright ones.
        /// </summary>
        internal const double GamutFitBandShare = 0.90;

        /// <summary>
        /// The share of the band's pixels outside that the recipe's uniform saturation factor must
        /// bring in (R-176); the pixels outside above the band are out of the mask's reach and are
        /// counted in what is left. 99 %: the last pixels are the most saturated, whose factor would
        /// desaturate the whole band for nothing (measured 2026-11-06 on the Smida TIFF: a factor
        /// of 0.6 under L* 15 leaves 12 % of them, 0.75 everywhere 33 %).
        /// </summary>
        internal const double GamutFitRecipeShare = 0.99;

        /// <summary>
        /// The step of the recipe's saturation factor (R-176): 5 %, the resolution of a
        /// saturation slider a person sets by hand.
        /// </summary>
        internal const double GamutFitRecipeStep = 0.05;
    }
}
