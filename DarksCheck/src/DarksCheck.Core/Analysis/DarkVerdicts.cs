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

/// <summary>Where on the map the largest departure from the centre sits.</summary>
internal enum MapRegion
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
    Top,
    Bottom,
    Left,
    Right,
}

/// <summary>Check 2: the largest edge or corner offset from the centre, in ADU equivalent, on the dark-minus-bias map when there is one, else on the dark master (R-021).</summary>
internal sealed record LeakVerdict(Verdict Verdict, double OffsetAdu, MapRegion Where, bool FromDarkMinusBias);

/// <summary>Check 3: the temperature offset between the first and the last dark, read from the hot pixels (R-023).</summary>
internal sealed record DriftVerdict(Verdict Verdict, double DeltaCelsius, double Minutes, double FirstRatio, double LastRatio);

/// <summary>Check 4: the per-frame residual noise on the reference plane, the frames that stand out, the read noise and the temporal noise over it (dark current and quantisation), the banding against white noise (R-022).</summary>
internal sealed record NoiseVerdict(Verdict Verdict, double MedianSigma, IReadOnlyList<int> OutlierFrames, double? ReadNoise, double? NoiseOverRead, double RowBandingAdu, double RowBandingRatio, double ColBandingRatio);

/// <summary>Check 6: the structured banding of the master and the glow at its edges (R-040).</summary>
internal sealed record PatternVerdict(Verdict Verdict, double BandingP2pAdu, double GlowAdu);

/// <summary>Check 7: dead and saturated pixels on the master (R-032).</summary>
internal sealed record DeadSatVerdict(Verdict Verdict, int Dead, int Saturated);

/// <summary>Check 8: whether the frames are enough: at least <see cref="Th.MinDarks"/>, and the median master's noise under the quantisation step; the frames that would do both (R-024).</summary>
internal sealed record EnoughVerdict(Verdict Verdict, int Frames, double MasterNoise, int QuantStep, int FramesNeeded, bool TooFew);

internal sealed record DarkVerdicts(LeakVerdict Leak, DriftVerdict Drift, NoiseVerdict Noise, PatternVerdict Pattern, DeadSatVerdict DeadSat, EnoughVerdict Enough);

/// <summary>Turns the figures of a dark series (and its biases, when there are any) into the six verdicts; every threshold is in <see cref="Th"/>.</summary>
internal static class DarkVerdictAnalyzer
{
    internal static DarkVerdicts Judge(DarkSeriesAnalysis d, BiasAnalysis? b, DarkCurrent? dc, int refPlane)
    {
        ArgumentNullException.ThrowIfNull(d);
        double norm = d.Norm;

        // 2. leak: the map with the bias taken out when there is one
        Regions r = dc is not null ? dc.Maps[refPlane].Regions : d.MeanMaps[refPlane].Regions;
        (double offset, MapRegion where) = Largest(r);
        offset /= norm;
        LeakVerdict leak = new(double.IsNaN(offset) ? Verdict.Unmeasured : Math.Abs(offset) <= Th.LeakAdu ? Verdict.Ok : Verdict.Warning, offset, where, dc is not null);

        // 3. drift
        Warming wm = d.Warming;
        DriftVerdict drift = new(double.IsNaN(wm.DeltaCelsius) ? Verdict.Unmeasured : Math.Abs(wm.DeltaCelsius) <= Th.DriftCelsius ? Verdict.Ok : Verdict.Warning,
            wm.DeltaCelsius, wm.Minutes, wm.FirstRatio, wm.LastRatio);

        // 4. noise
        DarkFrameFigures[] ok = d.Frames.Where(f => f.Error is null).ToArray();
        float[] residual = ok.Select(f => (float)f.Residuals[refPlane].Sigma).ToArray();
        double medianSigma = residual.Length == 0 ? double.NaN : Planes.Median(residual);
        int[] outliers = ok.Where(f => f.Residuals[refPlane].Sigma > Th.NoiseOutlierFactor * medianSigma).Select(f => f.Info.Index).ToArray();
        double? read = b is null || double.IsNaN(b.ReadNoise[refPlane]) ? null : b.ReadNoise[refPlane];
        double? overRead = read is double rn && !double.IsNaN(d.Noise.TemporalSigma) ? Math.Sqrt(Math.Max(d.Noise.TemporalSigma * d.Noise.TemporalSigma - rn * rn, 0)) : null;
        // One frame has no residual against its own median (it is zero by construction):
        // check 4 is unmeasured, as checks 3 and 8 are.
        bool series = residual.Length >= 2;
        double rowAdu = series ? Med(ok.Select(f => f.Residuals[refPlane].RowStd)) : double.NaN;
        double rowRatio = series ? Med(ok.Select(f => f.Residuals[refPlane].RowExpected > 0 ? f.Residuals[refPlane].RowStd / f.Residuals[refPlane].RowExpected : double.NaN)) : double.NaN;
        double colRatio = series ? Med(ok.Select(f => f.Residuals[refPlane].ColExpected > 0 ? f.Residuals[refPlane].ColStd / f.Residuals[refPlane].ColExpected : double.NaN)) : double.NaN;
        NoiseVerdict noise = new(!series ? Verdict.Unmeasured : outliers.Length == 0 ? Verdict.Ok : Verdict.Warning, series ? medianSigma : double.NaN, series ? outliers : [], read, overRead, rowAdu, rowRatio, colRatio);

        // 6. pattern: the worst structured banding over the planes, and the glow of the leak map
        double p2p = d.Banding.Max(x => Math.Max(x.RowP2p, x.ColP2p)) / norm;
        Verdict patternVerdict = double.IsNaN(p2p) || double.IsNaN(offset) ? Verdict.Unmeasured
            : p2p <= Th.BandingAdu && Math.Abs(offset) <= Th.LeakAdu ? Verdict.Ok : Verdict.Warning;
        PatternVerdict pattern = new(patternVerdict, p2p, offset);

        // 7. dead and saturated
        int dead = d.Master.Sum(m => m.Low), sat = d.Master.Sum(m => m.Saturated);
        DeadSatVerdict deadSat = new(dead == 0 && sat == 0 ? Verdict.Ok : Verdict.Warning, dead, sat);

        // 8. enough frames: at least MinDarks for an honest median, and the median master's
        // noise (median factor x temporal sigma / sqrt(N)) under the quantisation step
        double step = d.QuantStep > 0 ? d.QuantStep : 1;
        double masterNoise = d.Noise.MasterNoise;
        double perFrame = masterNoise * Math.Sqrt(d.Used);
        int needed = double.IsNaN(masterNoise) ? Th.MinDarks : Math.Max(Th.MinDarks, (int)Math.Ceiling(Math.Pow(perFrame / (Th.MasterNoiseVsStep * step), 2)));
        bool tooFew = d.Used < Th.MinDarks;
        Verdict enoughVerdict = d.Used < 2 || double.IsNaN(masterNoise) ? Verdict.Unmeasured
            : tooFew || masterNoise > Th.MasterNoiseVsStep * step ? Verdict.Warning : Verdict.Ok;
        EnoughVerdict enough = new(enoughVerdict, d.Used, masterNoise, d.QuantStep, needed, tooFew);

        return new DarkVerdicts(leak, drift, noise, pattern, deadSat, enough);
    }

    private static double Med(IEnumerable<double> values)
    {
        float[] v = values.Where(x => !double.IsNaN(x) && !double.IsInfinity(x)).Select(x => (float)x).ToArray();
        return v.Length == 0 ? double.NaN : Planes.Median(v);
    }

    /// <summary>The largest departure from the centre over the four edges and the four corners, signed, with its region.</summary>
    private static (double Offset, MapRegion Where) Largest(Regions r)
    {
        (double, MapRegion)[] d =
        [
            (r.Top - r.Centre, MapRegion.Top), (r.Bottom - r.Centre, MapRegion.Bottom),
            (r.Left - r.Centre, MapRegion.Left), (r.Right - r.Centre, MapRegion.Right),
            (r.TopLeft - r.Centre, MapRegion.TopLeft), (r.TopRight - r.Centre, MapRegion.TopRight),
            (r.BottomLeft - r.Centre, MapRegion.BottomLeft), (r.BottomRight - r.Centre, MapRegion.BottomRight),
        ];
        return d.MaxBy(x => double.IsNaN(x.Item1) ? double.NegativeInfinity : Math.Abs(x.Item1));
    }
}
