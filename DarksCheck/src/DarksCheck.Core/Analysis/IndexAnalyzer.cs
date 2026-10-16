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

using System.Globalization;

namespace DarksCheck.Core;

/// <summary>How a set of lights (or darks) matches a dark master, from their amplitude ratio (R-062).</summary>
internal enum MatchVerdict
{
    /// <summary>Within <see cref="Th.MatchDirectLow"/> .. <see cref="Th.MatchDirectHigh"/>: subtract the dark as it is.</summary>
    Direct,

    /// <summary>Within <see cref="Th.MatchOptLow"/> .. <see cref="Th.MatchOptHigh"/>: subtract with -opt, which scales it.</summary>
    WithOpt,

    /// <summary>Down to <see cref="Th.MatchCosmeticLow"/>: the master is much warmer, good for the hot-pixel positions only (-cc=dark).</summary>
    CosmeticOnly,

    /// <summary>Too far either way.</summary>
    Unmatched,

    /// <summary>No shared pixel to measure on.</summary>
    Unmeasured,
}

/// <summary>
/// The index of a master (R-060, ADR 0004): the median amplitude above black, in ADU
/// equivalent, of the unsaturated pixels ranked from <see cref="Th.IndexRankLowPerMpx"/> to
/// <see cref="Th.IndexRankHighPerMpx"/> per megapixel by descending excess; the ranks used,
/// the band's size and the pixels ranked.
/// </summary>
internal sealed record IndexFigures(double Index, int RankLow, int RankHigh, int BandPixels, int Ranked)
{
    /// <summary>The token of the label: `idx25`.</summary>
    internal string Tag => double.IsNaN(Index) ? "idx-" : "idx" + Math.Round(Index, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
}

/// <summary>The amplitude ratio of one set over a reference master on the pixels they share (R-062, R-063): the median and quartiles, the temperature offset by the doubling rule, the verdict.</summary>
internal sealed record MatchFigures(int Pixels, double Ratio, double Q25, double Q75, double DeltaCelsius, MatchVerdict Verdict)
{
    internal static readonly MatchFigures None = new(0, double.NaN, double.NaN, double.NaN, double.NaN, MatchVerdict.Unmeasured);
}

/// <summary>
/// The index of a master and the matching of two sets (ADR 0004, ADR 0008): no reference file,
/// the index is a rank statistic of the master itself and the matching a ratio over the pixels
/// both masters have above <see cref="Th.MatchMinAdu"/> (a set of lights is read at the dark
/// master's pixels above it), both normalised to ISO 400 and 120 s.
/// </summary>
internal static class IndexAnalyzer
{
    internal static IndexFigures RankIndex(IReadOnlyList<float> median, double black, double white, double norm, int w, int h)
    {
        ArgumentNullException.ThrowIfNull(median);
        float satLimit = (float)(white - 1);
        float[] ex = new float[median.Count];
        int n = 0;
        for (int i = 0; i < median.Count; i++)
        {
            float v = median[i];
            if (v < satLimit)
            {
                ex[n++] = (float)(v - black);
            }
        }

        double mpx = w * (double)h / 1e6;
        int kLo = Math.Max(1, (int)Math.Round(Th.IndexRankLowPerMpx * mpx));
        int kHi = Math.Max(kLo + 1, (int)Math.Round(Th.IndexRankHighPerMpx * mpx));
        if (n == 0)
        {
            return new IndexFigures(double.NaN, kLo, kHi, 0, 0);
        }

        // the band is the descending ranks kLo .. kHi - 1 (0-based); its median is the element
        // at the middle rank, found by one selection on the ascending order
        kHi = Math.Min(kHi, n);
        kLo = Math.Min(kLo, kHi - 1);
        int midRank = (kLo + kHi - 1) / 2;
        Array.Resize(ref ex, n);
        double mid = Planes.Select(ex, n - 1 - midRank);
        return new IndexFigures(mid / norm, kLo, kHi, kHi - kLo, n);
    }

    /// <summary>
    /// The per-pixel ratio (own excess / own norm) over (reference excess / reference norm) on
    /// the pixels both masters have above <see cref="Th.MatchMinAdu"/> ADU-eq, neither near
    /// saturation (ADR 0008: a pixel hot in one master only puts an amplitude over noise, and
    /// the weak pixels of a warm master grow faster than the strong ones); both masters the
    /// same size.
    /// </summary>
    internal static float[] SharedRatios(IReadOnlyList<float> own, double ownBlack, double ownWhite, double ownNorm, IReadOnlyList<float> reference, double refBlack, double refWhite, double refNorm)
    {
        ArgumentNullException.ThrowIfNull(own);
        ArgumentNullException.ThrowIfNull(reference);
        if (own.Count != reference.Count)
        {
            throw new InvalidOperationException("masterele nu au aceeasi dimensiune");
        }

        double refMin = DarkSeriesAnalyzer.HotLevel(Th.MatchMinAdu, refNorm), ownMin = DarkSeriesAnalyzer.HotLevel(Th.MatchMinAdu, ownNorm);
        double ownMax = Th.AmplitudeMaxFraction * ownWhite, refMax = Th.AmplitudeMaxFraction * refWhite;
        List<float> ratios = [];
        for (int i = 0; i < own.Count; i++)
        {
            double r = reference[i] - refBlack, o = own[i] - ownBlack;
            if (r > refMin && o > ownMin && reference[i] < refMax && own[i] < ownMax)
            {
                ratios.Add((float)((o / ownNorm) / (r / refNorm)));
            }
        }

        return [.. ratios];
    }

    /// <summary>The median and quartiles of the ratios, the temperature offset `D x log2(ratio)` and the verdict.</summary>
    internal static MatchFigures Match(float[] ratios)
    {
        ArgumentNullException.ThrowIfNull(ratios);
        if (ratios.Length == 0)
        {
            return MatchFigures.None;
        }

        double median = Planes.Median(ratios);
        double dT = median > 0 ? Th.DoublingCelsius * Math.Log2(median) : double.NaN;
        return new MatchFigures(ratios.Length, median, Planes.Percentile(ratios, 25), Planes.Percentile(ratios, 75), dT, Judge(median));
    }

    /// <summary>The lights' equivalent index: the darks' index times the lights-over-dark ratio (R-064); NaN when either is not measured.</summary>
    internal static double EquivalentIndex(IndexFigures index, MatchFigures lightsOverDark)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(lightsOverDark);
        return lightsOverDark.Verdict == MatchVerdict.Unmeasured ? double.NaN : index.Index * lightsOverDark.Ratio;
    }

    /// <summary>Two ratios chained through a common master, (a / m) x (m / b) = a / b, judged as one (R-064); <see cref="MatchFigures.None"/> when either is not measured.</summary>
    internal static MatchFigures Chain(MatchFigures aOverM, MatchFigures mOverB)
    {
        ArgumentNullException.ThrowIfNull(aOverM);
        ArgumentNullException.ThrowIfNull(mOverB);
        return aOverM.Verdict == MatchVerdict.Unmeasured || mOverB.Verdict == MatchVerdict.Unmeasured
            ? MatchFigures.None
            : Match([(float)(aOverM.Ratio * mOverB.Ratio)]);
    }

    internal static MatchVerdict Judge(double ratio)
    {
        if (double.IsNaN(ratio) || ratio <= 0)
        {
            return MatchVerdict.Unmeasured;
        }

        if (ratio >= Th.MatchDirectLow && ratio <= Th.MatchDirectHigh)
        {
            return MatchVerdict.Direct;
        }

        if (ratio >= Th.MatchOptLow && ratio <= Th.MatchOptHigh)
        {
            return MatchVerdict.WithOpt;
        }

        return ratio >= Th.MatchCosmeticLow && ratio < Th.MatchOptLow ? MatchVerdict.CosmeticOnly : MatchVerdict.Unmatched;
    }

    /// <summary>
    /// The label the owner puts in the folder's name (R-061): the folder's name plus the tag,
    /// the folder being the darks' parent when the darks sit in a `darks` subfolder (a
    /// library kept as `name/darks/`) and the darks folder itself otherwise.
    /// </summary>
    internal static string Label(SessionFolder session, IndexFigures index)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(index);
        string dir = session.DarksDir;
        string name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.Equals(name, SessionFolder.DarksName, StringComparison.OrdinalIgnoreCase))
        {
            string? parent = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            name = parent is null ? name : Path.GetFileName(parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }

        return name + "-" + index.Tag;
    }
}

/// <summary>
/// A second set of darks given with `--against` (R-063): its folder, frames and settings, its
/// index, the session's darks over it on the shared pixels, the session's lights over it
/// through the darks (<see cref="MatchFigures.None"/> without lights, R-064), and how the two
/// hot-pixel sets overlap at <see cref="Th.PartnerOverlapAdu"/>.
/// </summary>
internal sealed record PartnerAnalysis(
    string Folder,
    int Frames,
    int Used,
    double Norm,
    double? Iso,
    double? ExposureSeconds,
    IndexFigures Index,
    string Label,
    MatchFigures OwnOverPartner,
    MatchFigures LightsOverPartner,
    int OwnHot50,
    int OwnHot50InPartner50,
    int PartnerHot50,
    int PartnerHot50InOwn20);
