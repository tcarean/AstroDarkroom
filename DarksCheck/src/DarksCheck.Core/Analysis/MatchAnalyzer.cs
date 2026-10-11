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

/// <summary>What a check concluded; the report turns it into a word (OK, ATENTIE, NEMASURAT).</summary>
internal enum Verdict
{
    Ok,
    Warning,
    Unmeasured,
}

/// <summary>
/// The shooting settings of a group of frames: the most frequent exposure and ISO among the
/// readable frames, how many frames carry them, and whether every readable frame does
/// (exposures within <see cref="Th.ExposureTolerance"/> of each other count as one).
/// </summary>
internal sealed record GroupSettings(double? ExposureSeconds, double? Iso, int Frames, int Readable, bool Uniform);

/// <summary>
/// The settings of the three groups and the two verdicts of check 1 (R-007): do the darks
/// match the lights in exposure and ISO, and is the bias a bias.
/// </summary>
internal sealed record MatchAnalysis(GroupSettings? Darks, GroupSettings? Biases, GroupSettings? Lights, Verdict DarksVsLights, Verdict Bias);

internal static class MatchAnalyzer
{
    internal static MatchAnalysis Analyze(IReadOnlyList<FrameInfo> darks, IReadOnlyList<FrameInfo> biases, IReadOnlyList<FrameInfo> lights)
    {
        GroupSettings? d = Settings(darks);
        GroupSettings? b = Settings(biases);
        GroupSettings? l = Settings(lights);

        // Both settings must be known on both sides: an unknown ISO is not a match, it is
        // a comparison that could not be made.
        Verdict darksVsLights = Verdict.Unmeasured;
        if (d?.ExposureSeconds is double de && l?.ExposureSeconds is double le && d.Iso is double di && l.Iso is double li)
        {
            darksVsLights = SameExposure(de, le) && di == li ? Verdict.Ok : Verdict.Warning;
        }

        Verdict bias = Verdict.Unmeasured;
        if (b?.ExposureSeconds is double be)
        {
            bias = be <= Th.BiasMaxExposureSeconds ? Verdict.Ok : Verdict.Warning;
        }

        return new MatchAnalysis(d, b, l, darksVsLights, bias);
    }

    /// <summary>Two exposures that differ by less than the tolerance, relative to the longer one.</summary>
    internal static bool SameExposure(double a, double b) => Math.Abs(a - b) <= Th.ExposureTolerance * Math.Max(a, b);

    /// <summary>Null for an empty group; the dominant (exposure, ISO) of the readable frames otherwise, null fields when none carries them.</summary>
    internal static GroupSettings? Settings(IReadOnlyList<FrameInfo> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count == 0)
        {
            return null;
        }

        List<FrameInfo> readable = frames.Where(f => f.Error is null).ToList();
        if (readable.Count == 0)
        {
            return new GroupSettings(null, null, 0, 0, true);
        }

        // Group by ISO and by exposure within the tolerance: the first frame of a cluster
        // names it, the next frames join the cluster whose exposure they match.
        List<(double? Exposure, double? Iso, int Count)> clusters = [];
        foreach (FrameInfo f in readable)
        {
            int at = clusters.FindIndex(c => c.Iso == f.Iso && (c.Exposure is double ce && f.ExposureSeconds is double fe ? SameExposure(ce, fe) : c.Exposure == f.ExposureSeconds));
            if (at < 0)
            {
                clusters.Add((f.ExposureSeconds, f.Iso, 1));
            }
            else
            {
                clusters[at] = (clusters[at].Exposure, clusters[at].Iso, clusters[at].Count + 1);
            }
        }

        (double? exposure, double? iso, int count) = clusters.OrderByDescending(c => c.Count).First();
        return new GroupSettings(exposure, iso, count, readable.Count, count == readable.Count);
    }
}
