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
/// Measures one light frame (R-012 to R-016): the levels of the four CFA positions, the
/// green channel's background and noise, the stars and their shapes, the long trails, and
/// the star list a later registration uses. The method is the Python prototype's
/// (`prototype/analyze.py`), step for step; docs/design/frame-analysis.md says why each step.
/// </summary>
internal static class FrameAnalyzer
{
    internal static FrameMeasurement Analyze(Img mosaic, AduScale scale, CfaPattern? cfa)
    {
        ArgumentNullException.ThrowIfNull(mosaic);
        ArgumentNullException.ThrowIfNull(scale);
        Raster raw = Raster.FromImg(mosaic, scale.Black);
        return Analyze(raw, scale, cfa);
    }

    /// <summary>The same, on a raster already above its black level.</summary>
    internal static FrameMeasurement Analyze(Raster raw, AduScale scale, CfaPattern? cfa)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(scale);
        int h = raw.H, w = raw.W;
        double top = scale.White - scale.Black;

        // Levels per CFA position (R-012), the median read at every 4th pixel of the position.
        List<ChannelLevel> channels = ChannelLevels(raw, cfa, top);

        // The green channel, its tile background and the residual d = g - bg (R-013).
        Raster g = Raster.InterpolateGreen(raw, cfa);
        Raster tiles = Raster.TileMedians(g, Th.BackgroundTile, Th.BackgroundSampleStep);
        Raster bg = Raster.BilinearResize(tiles, h, w);
        double[] tileValues = Array.ConvertAll(tiles.D, v => (double)v);
        double bgMedian = Raster.Percentile(tileValues, 50);
        double bgMin = tileValues.Min(), bgMax = tileValues.Max();
        double bgCentre = Mean(tiles, tiles.H / 2 - 2, tiles.H / 2 + 2, tiles.W / 2 - 2, tiles.W / 2 + 2);
        double[] corners =
        [
            Mean(tiles, 0, 3, 0, 3), Mean(tiles, 0, 3, tiles.W - 3, tiles.W),
            Mean(tiles, tiles.H - 3, tiles.H, 0, 3), Mean(tiles, tiles.H - 3, tiles.H, tiles.W - 3, tiles.W),
        ];
        Raster d = g == raw ? Copy(g) : g;   // never subtract into the mosaic the levels were read from
        Raster.SubtractInPlace(d, bg);
        double sigma = Raster.CentralMadSigma(d, Th.NoiseWindowHalf, Th.NoiseSampleStep);

        // Detection on the smoothed residual (R-014).
        Raster sm = Raster.Gaussian(d, Th.DetectionSmoothSigma);
        double smSigma = Raster.CentralMadSigma(sm, Th.NoiseWindowHalf, Th.NoiseSampleStep);
        List<Component> comps = Components.Find(sm, Th.DetectionSigmas * smSigma, d, raw);

        // Trails (R-015): big, long and thin.
        List<Trail> trails = [];
        foreach (Component c in comps)
        {
            if (c.Area <= Th.TrailMinArea)
            {
                continue;
            }

            double length = Math.Sqrt((double)c.Height * c.Height + (double)c.Width * c.Width);
            if (length > Th.TrailMinLength && c.Area < Th.TrailMaxFill * c.Height * c.Width + Th.TrailFillSlack)
            {
                trails.Add(new Trail(length, c.Area, c.X0, c.Y0));
            }
        }

        // Stars: the components of star-like area, once in flux order for both lists below.
        List<Component> stars = comps.Where(c => c.Area >= Th.MinStarArea && c.Area <= Th.MaxStarArea)
            .OrderByDescending(c => c.Flux).ToList();
        int bright = stars.Count(c => c.Peak > Th.BrightPeakSigmas * sigma);
        double margin = Th.SaturationMargin(top);
        int saturated = comps.Count(c => c.PeakRaw >= top - margin);

        // Shapes of the brightest unsaturated stars.
        List<StarShape> shapes = [];
        foreach (Component c in stars
            .Where(c => c.Peak < Th.ShapePeakMaxFraction * top && c.Peak > Th.ShapePeakSigmas * sigma)
            .Take(Th.ShapeStars))
        {
            StarShape? s = StarShapes.Measure(d, c.CentroidY, c.CentroidX, Th.MomentRadius, Th.MomentIterations, c.Peak, c.Area);
            if (s is not null && s.Fwhm >= Th.MinFwhm && s.Fwhm <= Th.MaxFwhm)
            {
                shapes.Add(s);
            }
        }

        List<double> halos = [];
        foreach (StarShape s in shapes.Take(Th.HaloStars))
        {
            double? hl = StarShapes.Halo(d, s.Y, s.X, Th.HaloCore, Th.HaloInner, Th.HaloOuter);
            if (hl is double v)
            {
                halos.Add(v);
            }
        }

        // The registration list: the brightest stars, saturated ones included (R-016).
        List<RegistrationStar> reg = stars.Take(Th.RegistrationStars)
            .Select(c => new RegistrationStar(c.CentroidX, c.CentroidY, c.Flux)).ToList();

        // Summary figures.
        double[] fwhm = shapes.Select(s => s.Fwhm).ToArray();
        double[] elong = shapes.Select(s => s.Elongation).ToArray();
        double? angle = null, coherence = null;
        double[] votes = shapes.Where(s => s.Elongation > Th.AngleVoteRatio).Select(s => s.AngleDeg).ToArray();
        if (votes.Length > Th.AngleVoteMin - 1)
        {
            double sc = 0, ss = 0;
            foreach (double a in votes)
            {
                double two = 2 * a * Math.PI / 180;
                sc += Math.Cos(two);
                ss += Math.Sin(two);
            }

            sc /= votes.Length;
            ss /= votes.Length;
            coherence = Math.Sqrt(sc * sc + ss * ss);
            angle = 0.5 * Math.Atan2(ss, sc) * 180 / Math.PI;
        }

        double halfDiagonal = Math.Sqrt(h * h / 4.0 + w * w / 4.0);
        ZoneShape Zone(Func<double, bool> pick)
        {
            List<StarShape> inZone = shapes.Where(s => pick(Math.Sqrt((s.Y - h / 2.0) * (s.Y - h / 2.0) + (s.X - w / 2.0) * (s.X - w / 2.0)) / halfDiagonal)).ToList();
            return inZone.Count >= Th.ZoneStarsMin
                ? new ZoneShape(Raster.Percentile(inZone.Select(s => s.Fwhm).ToList(), 50), Raster.Percentile(inZone.Select(s => s.Elongation).ToList(), 50), inZone.Count)
                : new ZoneShape(null, null, inZone.Count);
        }

        return new FrameMeasurement(
            h, w, scale, channels,
            bgMedian, bgMin, bgMax, bgCentre, corners, sigma,
            stars.Count, bright, saturated, shapes,
            fwhm.Length > 0 ? Raster.Percentile(fwhm, 50) : null,
            fwhm.Length > 0 ? Raster.Percentile(fwhm, 90) : null,
            elong.Length > 0 ? Raster.Percentile(elong, 50) : null,
            elong.Length > 0 ? Raster.Percentile(elong, 90) : null,
            elong.Length > 0 ? elong.Count(e => e > Th.ElongatedRatio) / (double)elong.Length : null,
            angle, coherence,
            Zone(r => r < Th.CentreZoneFraction),
            Zone(r => r >= Th.CentreZoneFraction && r < Th.MidZoneFraction),
            Zone(r => r >= Th.MidZoneFraction),
            halos.Count > 0 ? Raster.Percentile(halos, 50) : null,
            trails, reg);
    }

    private static List<ChannelLevel> ChannelLevels(Raster raw, CfaPattern? cfa, double top)
    {
        double margin = Th.SaturationMargin(top);
        List<ChannelLevel> levels = new(4);
        for (int p = 0; p < 4; p++)
        {
            int py = p / 2, px = p % 2;
            int rows = (raw.H - py + 1) / 2, cols = (raw.W - px + 1) / 2;
            List<double> sample = new((rows / Th.BackgroundSampleStep + 1) * (cols / Th.BackgroundSampleStep + 1));
            int sat = 0;
            double min = double.MaxValue;
            for (int y = py; y < raw.H; y += 2)
            {
                int row = y * raw.W;
                for (int x = px; x < raw.W; x += 2)
                {
                    float v = raw.D[row + x];
                    if (v >= top - margin)
                    {
                        sat++;
                    }

                    if (v < min)
                    {
                        min = v;
                    }
                }

                if (((y - py) / 2) % Th.BackgroundSampleStep == 0)
                {
                    for (int x = px; x < raw.W; x += 2 * Th.BackgroundSampleStep)
                    {
                        sample.Add(raw.D[row + x]);
                    }
                }
            }

            levels.Add(new ChannelLevel(Mosaic.Label(cfa, p), Mosaic.ColourOf(cfa, p), Raster.Percentile(sample, 50), sat, min,
                NoiseAnalyzer.HighPassSigma(raw, py, px)));
        }

        return levels;
    }

    private static double Mean(Raster t, int y0, int y1, int x0, int x1)
    {
        y0 = Math.Max(0, y0);
        x0 = Math.Max(0, x0);
        y1 = Math.Min(t.H, y1);
        x1 = Math.Min(t.W, x1);
        double s = 0;
        int n = 0;
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                s += t.D[y * t.W + x];
                n++;
            }
        }

        return n > 0 ? s / n : double.NaN;
    }

    private static Raster Copy(Raster a)
    {
        Raster o = new(a.H, a.W);
        Array.Copy(a.D, o.D, a.D.Length);
        return o;
    }
}
