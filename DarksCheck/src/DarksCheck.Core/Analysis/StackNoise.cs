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
/// count, and the per-pixel noise of a mean stack (sigma / sqrt(N)) and of a median stack
/// (<see cref="Th.MedianNoiseFactor"/> times that). The reference an offset in the master is
/// read against: the offset stays whole in the stack, the noise falls to these.
/// </summary>
internal sealed record StackNoise(double SkyAdu, double SkySigmaAdu, int Lights)
{
    /// <summary>The per-pixel noise of the mean of the lights.</summary>
    internal double MeanStackAdu => SkySigmaAdu / Math.Sqrt(Lights);

    /// <summary>The per-pixel noise of the median of the lights.</summary>
    internal double MedianStackAdu => Th.MedianNoiseFactor * MeanStackAdu;

    /// <summary>The figures of a session's lights on the reference plane; null without lights or without a decoded one.</summary>
    internal static StackNoise? Of(LightsAnalysis? lights, int referencePlane)
    {
        if (lights is null)
        {
            return null;
        }

        List<double> sky = [], sigma = [];
        foreach (LightFigures f in lights.Frames)
        {
            if (f.Error is null && !f.Skipped && referencePlane < f.SkyMedian.Count && !double.IsNaN(f.SkyMedian[referencePlane]))
            {
                sky.Add(f.SkyMedian[referencePlane]);
                sigma.Add(f.SkySigma[referencePlane]);
            }
        }

        return sky.Count == 0 ? null : new StackNoise(Planes.Median(sky), Planes.Median(sigma), sky.Count);
    }
}
