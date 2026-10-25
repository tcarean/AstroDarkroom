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
/// One CFA position: its name (R, G1, G2, B, or CFA[y,x]), its colour, its median above
/// black, its saturated pixels, its minimum, and the sigma-clipped standard deviation of its
/// high-pass residual (pixel minus the 5 x 5 median) in the central window: the frame's total
/// noise on that channel, read noise included (R-024).
/// </summary>
/// <param name="Name">
/// The position's name: R, G1, G2 or B from the colour order, CFA[y,x] without one.
/// </param>
/// <param name="Colour">
/// 'R', 'G' or 'B'; the null character when the colour order is unknown.
/// </param>
/// <param name="Median">
/// The median of the position, in ADU above black, read at every
/// <see cref="Th.BackgroundSampleStep"/>-th row and column of it.
/// </param>
/// <param name="Saturated">
/// How many pixels of the position are within <see cref="Th.SaturationMargin"/> of the top of the
/// scale.
/// </param>
/// <param name="Min">The lowest pixel of the position, in ADU above black.</param>
/// <param name="HighPassSigma">
/// The clipped standard deviation of the high-pass residual, in ADU.
/// </param>
internal sealed record ChannelLevel(string Name, char Colour, double Median, int Saturated, double Min, double HighPassSigma);

/// <summary>
/// A long, thin component: a satellite or a plane. Length is the bounding-box diagonal in
/// pixels.
/// </summary>
/// <param name="Length">The diagonal of the component's bounding box, in pixels.</param>
/// <param name="Area">The component's pixel count.</param>
/// <param name="X">The left column of the bounding box, in pixels.</param>
/// <param name="Y">The top row of the bounding box, in pixels.</param>
internal sealed record Trail(double Length, int Area, int X, int Y);

/// <summary>A star kept for registering the frame: its centroid and flux.</summary>
/// <param name="X">The flux-weighted centroid's column, in pixels.</param>
/// <param name="Y">The flux-weighted centroid's row, in pixels.</param>
/// <param name="Flux">
/// The sum of the background-subtracted values over the component, in ADU.
/// </param>
internal sealed record RegistrationStar(double X, double Y, double Flux);

/// <summary>
/// The median FWHM and elongation of the stars of one radial zone; null when the zone holds
/// too few.
/// </summary>
/// <param name="Fwhm">
/// The median FWHM, in pixels; null under <see cref="Th.ZoneStarsMin"/> stars.
/// </param>
/// <param name="Elongation">
/// The median ratio of the major to the minor axis; null as <paramref name="Fwhm"/>.
/// </param>
/// <param name="Stars">How many measured stars fall in the zone, however few.</param>
internal sealed record ZoneShape(double? Fwhm, double? Elongation, int Stars);

/// <summary>
/// Everything measured on one light frame (R-012 to R-016). Every figure is in pixels of the
/// frame or ADU above black; nothing here depends on the camera, the lens or the session.
/// </summary>
/// <param name="Height">The frame's height, in pixels.</param>
/// <param name="Width">The frame's width, in pixels.</param>
/// <param name="Scale">The black and white levels the frame was measured with, in ADU.</param>
/// <param name="Channels">The four CFA positions' levels, in position order.</param>
/// <param name="BackgroundMedian">
/// The median of the green's tile medians, in ADU above black.
/// </param>
/// <param name="BackgroundMin">The lowest tile median, in ADU above black.</param>
/// <param name="BackgroundMax">The highest tile median, in ADU above black.</param>
/// <param name="BackgroundCentre">
/// The mean of the central 4 x 4 tile medians, in ADU above black.
/// </param>
/// <param name="BackgroundCorners">
/// The means of the four 3 x 3 corner blocks of tile medians, in ADU above black: top left, top
/// right, bottom left, bottom right.
/// </param>
/// <param name="NoiseSigma">
/// 1.4826 times the MAD of the residual (green minus background) in the central window, in ADU.
/// </param>
/// <param name="Detected">
/// The detected components of star-like area, <see cref="Th.MinStarArea"/> to
/// <see cref="Th.MaxStarArea"/> pixels.
/// </param>
/// <param name="DetectedBright">
/// Those whose peak exceeds <see cref="Th.BrightPeakSigmas"/> times <paramref name="NoiseSigma"/>.
/// </param>
/// <param name="SaturatedStars">
/// The detected components, of any area, whose highest value above black is within
/// <see cref="Th.SaturationMargin"/> of the top of the scale.
/// </param>
/// <param name="Shapes">
/// The measured stars kept (FWHM within <see cref="Th.MinFwhm"/> to <see cref="Th.MaxFwhm"/>),
/// brightest first.
/// </param>
/// <param name="FwhmMedian">
/// The median FWHM of <paramref name="Shapes"/>, in pixels; null without a shape.
/// </param>
/// <param name="FwhmP90">The 90th percentile of that FWHM, in pixels; null without a shape.</param>
/// <param name="ElongationMedian">
/// The median elongation (major over minor axis) of <paramref name="Shapes"/>; null without a
/// shape.
/// </param>
/// <param name="ElongationP90">
/// The 90th percentile of that elongation; null without a shape.
/// </param>
/// <param name="ElongatedFraction">
/// The fraction (0..1) of <paramref name="Shapes"/> more elongated than
/// <see cref="Th.ElongatedRatio"/>; null without a shape.
/// </param>
/// <param name="AngleDeg">
/// The mean angle of the major axes of the stars more elongated than
/// <see cref="Th.AngleVoteRatio"/>, in degrees from the x axis (x right, y down), -90 to 90;
/// null under <see cref="Th.AngleVoteMin"/> voters.
/// </param>
/// <param name="AngleCoherence">
/// The length of the mean of the doubled angles' unit vectors, 0..1 (1 = every voter agrees); null
/// as <paramref name="AngleDeg"/>.
/// </param>
/// <param name="Centre">
/// The stars closer to the centre than <see cref="Th.CentreZoneFraction"/> of the half-diagonal.
/// </param>
/// <param name="Mid">
/// The stars from <see cref="Th.CentreZoneFraction"/> to <see cref="Th.MidZoneFraction"/> of the
/// half-diagonal.
/// </param>
/// <param name="Edge">
/// The stars from <see cref="Th.MidZoneFraction"/> of the half-diagonal outward.
/// </param>
/// <param name="Halo">
/// The median halo (annulus flux over core flux) of up to <see cref="Th.HaloStars"/> of the
/// brightest shapes; null when none could be measured.
/// </param>
/// <param name="Trails">The long, thin components found (R-015).</param>
/// <param name="RegistrationStars">
/// Up to <see cref="Th.RegistrationStars"/> star components, the brightest by flux first, saturated
/// ones included (R-016).
/// </param>
internal sealed record FrameMeasurement(
    int Height,
    int Width,
    AduScale Scale,
    IReadOnlyList<ChannelLevel> Channels,
    double BackgroundMedian,
    double BackgroundMin,
    double BackgroundMax,
    double BackgroundCentre,
    IReadOnlyList<double> BackgroundCorners,
    double NoiseSigma,
    int Detected,
    int DetectedBright,
    int SaturatedStars,
    IReadOnlyList<StarShape> Shapes,
    double? FwhmMedian,
    double? FwhmP90,
    double? ElongationMedian,
    double? ElongationP90,
    double? ElongatedFraction,
    double? AngleDeg,
    double? AngleCoherence,
    ZoneShape Centre,
    ZoneShape Mid,
    ZoneShape Edge,
    double? Halo,
    IReadOnlyList<Trail> Trails,
    IReadOnlyList<RegistrationStar> RegistrationStars)
{
    /// <summary>The level of the brightest channel, the one that saturates first.</summary>
    internal ChannelLevel BrightestChannel => Channels.MaxBy(c => c.Median)!;
}
