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
internal sealed record ChannelLevel(string Name, char Colour, double Median, int Saturated, double Min, double HighPassSigma);

/// <summary>A long, thin component: a satellite or a plane. Length is the bounding-box diagonal in pixels.</summary>
internal sealed record Trail(double Length, int Area, int X, int Y);

/// <summary>A star kept for registering the frame: its centroid and flux.</summary>
internal sealed record RegistrationStar(double X, double Y, double Flux);

/// <summary>The median FWHM and elongation of the stars of one radial zone; null when the zone holds too few.</summary>
internal sealed record ZoneShape(double? Fwhm, double? Elongation, int Stars);

/// <summary>
/// Everything measured on one light frame (R-012 to R-016). Every figure is in pixels of the
/// frame or ADU above black; nothing here depends on the camera, the lens or the session.
/// </summary>
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
