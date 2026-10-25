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
using System.Text.RegularExpressions;
using static DarksCheck.Core.Ui;

namespace DarksCheck.Core;

/// <summary>
/// How a set of lights (or darks) matches a dark master, from their amplitude ratio (R-062).
/// </summary>
internal enum MatchVerdict
{
    /// <summary>
    /// Within <see cref="Th.MatchDirectLow"/> .. <see cref="Th.MatchDirectHigh"/>: subtract the
    /// dark as it is.
    /// </summary>
    Direct,

    /// <summary>
    /// Within <see cref="Th.MatchOptLow"/> .. <see cref="Th.MatchOptHigh"/>: subtract with -opt,
    /// which scales it.
    /// </summary>
    WithOpt,

    /// <summary>
    /// Down to <see cref="Th.MatchCosmeticLow"/>: the master is much warmer, good for the hot-pixel
    /// positions only (-cc=dark).
    /// </summary>
    CosmeticOnly,

    /// <summary>Too far either way.</summary>
    Unmatched,

    /// <summary>No pixel to measure on, or no ratio at all.</summary>
    Unmeasured,
}

/// <summary>
/// The index of a master (R-060, ADR 0004): the median amplitude above black, in ADU
/// equivalent, of the unsaturated pixels ranked from <see cref="Th.IndexRankLowPerMpx"/> to
/// <see cref="Th.IndexRankHighPerMpx"/> per megapixel by descending excess; the ranks used,
/// the band's size and the pixels ranked.
/// </summary>
/// <param name="Index">The index, in ADU equivalent; NaN when every pixel is
/// saturated.</param>
/// <param name="RankLow">The band's first rank (0-based, by descending excess).</param>
/// <param name="RankHigh">The rank after the band's last.</param>
/// <param name="BandPixels">The band's size, <paramref name="RankHigh"/> -
/// <paramref name="RankLow"/>; 0 when nothing was ranked.</param>
/// <param name="Ranked">How many unsaturated pixels were ranked.</param>
internal sealed record IndexFigures(double Index, int RankLow, int RankHigh, int BandPixels, int Ranked)
{
    /// <summary>The token of the label: `idx25`.</summary>
    internal string Tag => double.IsNaN(Index) ? "idx-" : "idx" + Math.Round(Index, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
}

/// <summary>
/// The amplitude ratio of one set over a reference master on the pixels they share (R-062, R-063):
/// the median and quartiles, the temperature offset by the doubling rule, the verdict.
/// </summary>
/// <param name="Pixels">How many shared pixels the ratio is read on (1 for a chained
/// ratio).</param>
/// <param name="Ratio">The median ratio; NaN when unmeasured.</param>
/// <param name="Q25">The 25th percentile of the ratios.</param>
/// <param name="Q75">The 75th percentile of the ratios.</param>
/// <param name="DeltaCelsius">The temperature offset, in degrees Celsius; NaN for a ratio that
/// is not positive.</param>
/// <param name="Verdict">The verdict of the median ratio.</param>
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
internal static partial class IndexAnalyzer
{
    /// <summary>The index of a median master.</summary>
    /// <param name="median">The median master, row-major, in raw ADU with the black; not
    /// null.</param>
    /// <param name="black">The black level, in raw ADU.</param>
    /// <param name="white">The white level, in raw ADU; a pixel at the white minus 1 or above is
    /// left out.</param>
    /// <param name="norm">The normalisation factor from raw ADU to ADU equivalent.</param>
    /// <param name="w">The mosaic's width, for the megapixels.</param>
    /// <param name="h">The mosaic's height, for the megapixels.</param>
    /// <returns>The index and its ranks.</returns>
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
    /// <param name="own">The set's master (or the lights' accumulated levels), row-major, in raw
    /// ADU with the black; not null.</param>
    /// <param name="ownBlack">Its black level, in raw ADU.</param>
    /// <param name="ownWhite">Its white level, in raw ADU.</param>
    /// <param name="ownNorm">Its normalisation factor.</param>
    /// <param name="reference">The reference master, row-major, as many values; not null.</param>
    /// <param name="refBlack">Its black level, in raw ADU.</param>
    /// <param name="refWhite">Its white level, in raw ADU.</param>
    /// <param name="refNorm">Its normalisation factor.</param>
    /// <returns>One ratio per shared pixel, in mosaic order; empty when none is shared.</returns>
    /// <exception cref="InvalidOperationException">The two have a different number of
    /// pixels.</exception>
    internal static float[] SharedRatios(IReadOnlyList<float> own, double ownBlack, double ownWhite, double ownNorm, IReadOnlyList<float> reference, double refBlack, double refWhite, double refNorm)
    {
        ArgumentNullException.ThrowIfNull(own);
        ArgumentNullException.ThrowIfNull(reference);
        if (own.Count != reference.Count)
        {
            throw new InvalidOperationException(T("the masters do not have the same size", "masterele nu au aceeasi dimensiune"));
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

    /// <summary>
    /// The median and quartiles of the ratios, the temperature offset `D x log2(ratio)` with the
    /// settings' D (the default when null) and the verdict.
    /// </summary>
    /// <param name="ratios">The per-pixel ratios; not null.</param>
    /// <param name="normalisation">The settings; null for the defaults.</param>
    /// <returns>The figures, or <see cref="MatchFigures.None"/> for no ratio.</returns>
    internal static MatchFigures Match(float[] ratios, Normalisation? normalisation = null)
    {
        ArgumentNullException.ThrowIfNull(ratios);
        if (ratios.Length == 0)
        {
            return MatchFigures.None;
        }

        double median = Planes.Median(ratios);
        double dT = (normalisation ?? Normalisation.Default).Degrees(median);
        return new MatchFigures(ratios.Length, median, Planes.Percentile(ratios, 25), Planes.Percentile(ratios, 75), dT, Judge(median));
    }

    /// <summary>
    /// The lights' equivalent index: the darks' index times the lights-over-dark ratio (R-064); NaN
    /// when either is not measured.
    /// </summary>
    /// <param name="index">The darks' index; not null.</param>
    /// <param name="lightsOverDark">The lights over the darks' master; not null.</param>
    /// <returns>The equivalent index, in ADU equivalent, or NaN.</returns>
    internal static double EquivalentIndex(IndexFigures index, MatchFigures lightsOverDark)
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(lightsOverDark);
        return lightsOverDark.Verdict == MatchVerdict.Unmeasured ? double.NaN : index.Index * lightsOverDark.Ratio;
    }

    /// <summary>
    /// Two ratios chained through a common master, (a / m) x (m / b) = a / b, judged as one
    /// (R-064); <see cref="MatchFigures.None"/> when either is not measured.
    /// </summary>
    /// <param name="aOverM">The first set over the common master; not null.</param>
    /// <param name="mOverB">The common master over the second; not null.</param>
    /// <param name="normalisation">The settings; null for the defaults.</param>
    /// <returns>The chained ratio's figures, read on one value.</returns>
    internal static MatchFigures Chain(MatchFigures aOverM, MatchFigures mOverB, Normalisation? normalisation = null)
    {
        ArgumentNullException.ThrowIfNull(aOverM);
        ArgumentNullException.ThrowIfNull(mOverB);
        return aOverM.Verdict == MatchVerdict.Unmeasured || mOverB.Verdict == MatchVerdict.Unmeasured
            ? MatchFigures.None
            : Match([(float)(aOverM.Ratio * mOverB.Ratio)], normalisation);
    }

    /// <summary>
    /// The verdict of a ratio: NaN is unmeasured; a ratio at or under 0 is a measurement, the set
    /// shows none of the master's hot pixels, and is unmatched.
    /// </summary>
    /// <param name="ratio">The median ratio.</param>
    /// <returns>The verdict.</returns>
    internal static MatchVerdict Judge(double ratio)
    {
        if (double.IsNaN(ratio))
        {
            return MatchVerdict.Unmeasured;
        }

        if (ratio <= 0)
        {
            return MatchVerdict.Unmatched;
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
    /// library kept as `name/darks/`) and the darks folder itself otherwise. A name that
    /// already ends in an earlier label's suffix (`-idx` and digits, the owner having renamed
    /// the folder on it) loses that suffix first, so the label stays one suffix long (ADR 0013).
    /// </summary>
    /// <param name="session">The session or library; not null.</param>
    /// <param name="index">The darks' index; not null.</param>
    /// <returns>The label, such as `Smida-idx25`.</returns>
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

        return WithoutLabelSuffix(name) + "-" + index.Tag;
    }

    /// <summary>
    /// The name without one trailing tag of an earlier label, `-idx` and digits or `-idx-` (the
    /// unmeasured tag), whatever its case; a name with none, with `-idx` not at the end, or that
    /// is nothing but the tag (nothing would remain in front of the new one) is kept as it is.
    /// </summary>
    /// <param name="name">The folder name; not null.</param>
    /// <returns>The name without the tag.</returns>
    internal static string WithoutLabelSuffix(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return LabelSuffix().Replace(name, string.Empty);
    }

    /// <summary>The trailing tag of an earlier label, after at least one character.</summary>
    /// <returns>The source-generated expression.</returns>
    [GeneratedRegex("(?<=.)-idx(?:[0-9]+|-)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LabelSuffix();
}

/// <summary>
/// A second set of darks given with `--against` (R-063): its folder, frames and settings, its
/// index, the session's darks over it on the shared pixels, the session's lights over it
/// through the darks (<see cref="MatchFigures.None"/> without lights, R-064), and how the two
/// hot-pixel sets overlap at <see cref="Th.PartnerOverlapAdu"/>; its frames that failed to
/// decode or had other settings (not the skipped ones) are counted with the first error.
/// </summary>
/// <param name="Folder">The partner's folder.</param>
/// <param name="Frames">How many darks it holds.</param>
/// <param name="Used">How many decoded.</param>
/// <param name="Norm">Its normalisation factor.</param>
/// <param name="Iso">The first decoded dark's ISO; null when unknown.</param>
/// <param name="ExposureSeconds">The first decoded dark's exposure, in seconds; null when
/// unknown.</param>
/// <param name="Index">Its master's index.</param>
/// <param name="Label">Its label.</param>
/// <param name="OwnOverPartner">The session's darks over the partner's master.</param>
/// <param name="LightsOverPartner">The session's lights over the partner's master, through the
/// darks.</param>
/// <param name="Failed">How many of its darks failed (not the skipped ones).</param>
/// <param name="FirstError">"file: error" of the first that failed; null when none
/// did.</param>
/// <param name="OwnHot50">The session master's pixels above
/// <see cref="Th.PartnerOverlapAdu"/>.</param>
/// <param name="OwnHot50InPartner50">Of those, the ones also above it in the partner's.</param>
/// <param name="PartnerHot50">The partner master's pixels above it.</param>
/// <param name="PartnerHot50InOwn20">Of those, the ones above <see cref="Th.HotAdu"/> in the
/// session's.</param>
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
    int Failed,
    string? FirstError,
    int OwnHot50,
    int OwnHot50InPartner50,
    int PartnerHot50,
    int PartnerHot50InOwn20);
