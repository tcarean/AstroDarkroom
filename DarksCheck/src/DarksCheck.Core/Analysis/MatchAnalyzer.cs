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
/// What a check concluded; the report turns it into a word (OK, ATENTIE, NEMASURAT).
/// </summary>
internal enum Verdict
{
    /// <summary>The check passed.</summary>
    Ok,

    /// <summary>The check found something to look at.</summary>
    Warning,

    /// <summary>The check could not be made: a figure was missing.</summary>
    Unmeasured,

    /// <summary>
    /// The series is compromised and is to be taken again: check 4 when more than
    /// <see cref="Th.RedoOffEndFraction"/> of the frames are off the end of the series (ADR 0038,
    /// ADR 0043).
    /// </summary>
    Redo,
}

/// <summary>
/// The shooting settings of a group of frames: the most frequent exposure and ISO among the
/// readable frames, how many frames carry them, and whether every readable frame does
/// (exposures within <see cref="Th.ExposureTolerance"/> of each other count as one).
/// </summary>
/// <param name="ExposureSeconds">The most frequent exact exposure of the dominant setting (the
/// largest cluster of readable frames), in seconds; null when that cluster's frames do not say
/// it, even if other frames do.</param>
/// <param name="Iso">The dominant setting's ISO; null when that cluster's frames do not say it,
/// even if other frames do.</param>
/// <param name="Frames">How many readable frames carry the dominant setting.</param>
/// <param name="Readable">How many frames of the group were read without an error.</param>
/// <param name="Uniform">True when every readable frame carries the dominant setting (also when
/// none is readable).</param>
internal sealed record GroupSettings(double? ExposureSeconds, double? Iso, int Frames, int Readable, bool Uniform);

/// <summary>
/// The settings of the three groups and the two verdicts of check 1 (R-007): do the darks
/// match the lights in exposure and ISO, and is the bias a bias.
/// </summary>
/// <param name="Darks">The darks' settings; null for an empty group.</param>
/// <param name="Biases">The biases' settings; null for an empty group.</param>
/// <param name="Lights">The lights' settings; null for an empty group.</param>
/// <param name="DarksVsLights">Ok when the exposures match within the tolerance and the ISOs are
/// equal; Unmeasured when either is unknown on either side.</param>
/// <param name="Bias">Ok when the biases' exposure is at most
/// <see cref="Th.BiasMaxExposureSeconds"/>; Unmeasured without one.</param>
internal sealed record MatchAnalysis(GroupSettings? Darks, GroupSettings? Biases, GroupSettings? Lights, Verdict DarksVsLights, Verdict Bias);

/// <summary>Check 1's settings: compares the darks, biases and lights of a session.</summary>
internal static class MatchAnalyzer
{
    /// <summary>The settings of the three groups and check 1's two verdicts.</summary>
    /// <param name="darks">The darks' metadata; not null, may be empty.</param>
    /// <param name="biases">The biases' metadata; not null, may be empty.</param>
    /// <param name="lights">The lights' metadata; not null, may be empty.</param>
    /// <returns>The settings and the verdicts.</returns>
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

    /// <summary>
    /// Two exposures that differ by less than the tolerance, relative to the longer one.
    /// </summary>
    /// <param name="a">One exposure, in seconds.</param>
    /// <param name="b">The other exposure, in seconds.</param>
    /// <returns>True when |a - b| is at most <see cref="Th.ExposureTolerance"/> times the longer
    /// one (the bound included).</returns>
    internal static bool SameExposure(double a, double b) => Math.Abs(a - b) <= Th.ExposureTolerance * Math.Max(a, b);

    /// <summary>
    /// Null for an empty group; the dominant (exposure, ISO) of the readable frames otherwise, null
    /// fields when none carries them.
    /// </summary>
    /// <param name="frames">The group's metadata; not null.</param>
    /// <returns>The group's settings, or null for an empty group.</returns>
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
        // opens it, the next frames join the cluster whose exposure they match. The cluster
        // is named by its most frequent exact exposure, not by the frame that opened it: a
        // session whose first light is the odd 118.5 s one still reads 119.2 s.
        List<Cluster> clusters = [];
        foreach (FrameInfo f in readable)
        {
            Cluster? c = clusters.Find(c => c.Iso == f.Iso && (c.Exposure is double ce && f.ExposureSeconds is double fe ? SameExposure(ce, fe) : c.Exposure == f.ExposureSeconds));
            if (c is null)
            {
                c = new Cluster(f.ExposureSeconds, f.Iso);
                clusters.Add(c);
            }

            c.Add(f.ExposureSeconds);
        }

        Cluster best = clusters.MaxBy(c => c.Count)!;
        return new GroupSettings(best.ModeExposure, best.Iso, best.Count, readable.Count, best.Count == readable.Count);
    }

    /// <summary>
    /// The frames of one (exposure within the tolerance, ISO) setting, with the count of each exact
    /// exposure.
    /// </summary>
    /// <param name="exposure">The exposure of the frame that opened the cluster, in seconds;
    /// null when it has none.</param>
    /// <param name="iso">The cluster's ISO; null when unknown.</param>
    private sealed class Cluster(double? exposure, double? iso)
    {
        private readonly Dictionary<double, int> _exact = [];

        internal double? Exposure { get; } = exposure;
        internal double? Iso { get; } = iso;
        internal int Count { get; private set; }

        /// <summary>
        /// One more frame of the cluster; a frame without an exposure counts but names nothing.
        /// </summary>
        /// <param name="exact">The frame's exact exposure, in seconds; null when unknown.</param>
        internal void Add(double? exact)
        {
            Count++;
            if (exact is double e)
            {
                _exact[e] = _exact.GetValueOrDefault(e) + 1;
            }
        }

        /// <summary>
        /// The most frequent exact exposure of the cluster; the first seen wins a tie (Dictionary
        /// keeps insertion order).
        /// </summary>
        internal double? ModeExposure => _exact.Count == 0 ? null : _exact.MaxBy(kv => kv.Value).Key;
    }
}
