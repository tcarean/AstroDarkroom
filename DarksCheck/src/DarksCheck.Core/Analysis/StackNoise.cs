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

namespace DarksCheck.Core;

/// <summary>
/// What is left of the noise in a stack of the session's lights (R-080, ADR 0016): the median
/// over the decoded lights of the sky median and the sky sigma on the reference plane, their
/// count, and the per-pixel noise of a mean stack and of a median stack: the lights' share,
/// sigma / sqrt(N) (x <see cref="Th.MedianNoiseFactor"/> for the median), plus the dark
/// master's own noise in quadrature, since the master is subtracted identically from every
/// light and does not average out. The reference an offset in the master is read against:
/// the offset stays whole in the stack, the noise falls to these. Every figure is in the darks'
/// raw ADU: a light at other settings than the darks is brought to theirs first
/// (<see cref="LightFigures.ToDarks"/>), so the two noises add in one unit.
/// </summary>
/// <param name="SkyAdu">The median of the lights' sky medians on the reference plane, above
/// black, in raw ADU of the darks.</param>
/// <param name="SkySigmaAdu">The median of the lights' sky sigmas on the reference plane, in raw
/// ADU of the darks.</param>
/// <param name="Lights">How many lights gave both figures, at least 1.</param>
/// <param name="MasterNoiseAdu">The dark master's per-pixel noise, in raw ADU of the darks; 0
/// when unmeasured.</param>
internal sealed record StackNoise(double SkyAdu, double SkySigmaAdu, int Lights, double MasterNoiseAdu)
{
    /// <summary>
    /// True when a light is at other settings than the darks, so its figures were brought to the
    /// darks' before they entered the medians (<see cref="LightFigures.ToDarks"/> is not 1).
    /// </summary>
    internal bool Scaled { get; init; }

    /// <summary>
    /// How far from 1 a light's <see cref="LightFigures.ToDarks"/> must be for the light to count
    /// as being at other settings than the darks: above the rounding of two equal factors.
    /// </summary>
    private const double ScaledTolerance = 1e-9;

    /// <summary>The lights' share of the mean stack's per-pixel noise, sigma / sqrt(N).</summary>
    internal double LightsShareAdu => SkySigmaAdu / Math.Sqrt(Lights);

    /// <summary>
    /// The per-pixel noise of the mean of the calibrated lights: the lights' share and the master's
    /// noise in quadrature.
    /// </summary>
    internal double MeanStackAdu => Math.Sqrt(LightsShareAdu * LightsShareAdu + MasterNoiseAdu * MasterNoiseAdu);

    /// <summary>The per-pixel noise of the median of the calibrated lights.</summary>
    internal double MedianStackAdu => Math.Sqrt(Math.Pow(Th.MedianNoiseFactor * LightsShareAdu, 2) + MasterNoiseAdu * MasterNoiseAdu);

    /// <summary>
    /// The figures of a session's lights on the reference plane, with the dark master's noise (0
    /// when unmeasured); null without lights or without a decoded one.
    /// </summary>
    /// <param name="lights">The lights' analysis; null when the session has no lights.</param>
    /// <param name="referencePlane">The CFA position (0..3) the figures are read on.</param>
    /// <param name="masterNoiseAdu">The dark master's per-pixel noise in raw ADU; NaN counts as
    /// 0.</param>
    /// <returns>The stack's figures, or null.</returns>
    internal static StackNoise? Of(LightsAnalysis? lights, int referencePlane, double masterNoiseAdu)
    {
        if (lights is null)
        {
            return null;
        }

        List<double> sky = [], sigma = [];
        bool scaled = false;
        foreach (LightFigures f in lights.Frames)
        {
            if (f.Error is null && referencePlane < f.SkyMedian.Count && !double.IsNaN(f.SkyMedian[referencePlane]))
            {
                // each light's ADU at the darks' settings, as check 9 has its noise (R-024): the
                // master's noise below is in the darks' ADU, and the two add in one unit
                sky.Add(f.SkyMedian[referencePlane] * f.ToDarks);
                sigma.Add(f.SkySigma[referencePlane] * f.ToDarks);
                scaled |= Math.Abs(f.ToDarks - 1) > ScaledTolerance;
            }
        }

        return sky.Count == 0 ? null : new StackNoise(Planes.Median(sky), Planes.Median(sigma), sky.Count, double.IsNaN(masterNoiseAdu) ? 0 : masterNoiseAdu) { Scaled = scaled };
    }
}
