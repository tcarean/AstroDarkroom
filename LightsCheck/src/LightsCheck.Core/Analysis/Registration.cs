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
/// Where a frame sits against the reference (R-018): the displacement of the reference's
/// centre in this frame (so a positive Dx means the stars moved right, the pointing moved
/// left), the rotation and scale of the similarity, the matched stars, the coarse votes and
/// the fit's RMS residual. Ok is false when too few stars matched.
/// </summary>
internal sealed record RegistrationResult(bool Ok, int Matches, int Votes, double Dx, double Dy, double RotationDeg, double Scale, double Rms)
{
    internal static RegistrationResult Failed(int votes) => new(false, 0, votes, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
}

/// <summary>
/// Registers one frame's star list to the reference's: a vote over all pairwise offsets
/// gives the coarse shift, then nearest-neighbour matches at a shrinking tolerance feed a
/// least-squares similarity (shift, rotation, scale). The prototype's `reg.py`, step for step.
/// </summary>
internal static class Registration
{
    internal static RegistrationResult Register(IReadOnlyList<RegistrationStar> reference, IReadOnlyList<RegistrationStar> frame, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(frame);
        if (reference.Count < Th.MinMatches || frame.Count < Th.MinMatches)
        {
            return RegistrationResult.Failed(0);
        }

        (double dx, double dy, int votes) = Vote(reference, frame);
        double[] sol = [1, 0, dx, dy];
        Grid grid = new(frame, Th.MatchToleranceCoarse);
        int matched = 0;
        double rms = 0;
        for (int it = 0; it < Th.MatchIterations; it++)
        {
            double tol = it == 0 ? Th.MatchToleranceCoarse : Th.MatchToleranceFine;
            List<(double X, double Y, double U, double V)> pairs = [];
            foreach (RegistrationStar r in reference)
            {
                double px = sol[0] * r.X - sol[1] * r.Y + sol[2];
                double py = sol[1] * r.X + sol[0] * r.Y + sol[3];
                RegistrationStar? n = grid.Nearest(px, py, tol);
                if (n is not null)
                {
                    pairs.Add((r.X, r.Y, n.X, n.Y));
                }
            }

            if (pairs.Count < Th.MinMatches)
            {
                return RegistrationResult.Failed(votes);
            }

            sol = FitSimilarity(pairs, out rms);
            matched = pairs.Count;
        }

        double a = sol[0], b = sol[1];
        double cx = width / 2.0, cy = height / 2.0;
        double ncx = a * cx - b * cy + sol[2], ncy = b * cx + a * cy + sol[3];
        return new RegistrationResult(true, matched, votes, ncx - cx, ncy - cy, Math.Atan2(b, a) * 180 / Math.PI, Math.Sqrt(a * a + b * b), rms);
    }

    /// <summary>The most voted offset between the brightest stars of both lists, in bins of Th.VoteBin px over +-Th.VoteRange.</summary>
    private static (double Dx, double Dy, int Votes) Vote(IReadOnlyList<RegistrationStar> reference, IReadOnlyList<RegistrationStar> frame)
    {
        int n = 2 * Th.VoteRange / Th.VoteBin;
        int[] hist = new int[n * n];
        int nr = Math.Min(reference.Count, Th.VoteStars), nf = Math.Min(frame.Count, Th.VoteStars);
        for (int i = 0; i < nf; i++)
        {
            for (int j = 0; j < nr; j++)
            {
                double ox = frame[i].X - reference[j].X, oy = frame[i].Y - reference[j].Y;
                int bx = (int)Math.Floor((ox + Th.VoteRange) / Th.VoteBin), by = (int)Math.Floor((oy + Th.VoteRange) / Th.VoteBin);
                if (bx >= 0 && bx < n && by >= 0 && by < n)
                {
                    hist[by * n + bx]++;
                }
            }
        }

        int best = 0;
        for (int i = 1; i < hist.Length; i++)
        {
            if (hist[i] > hist[best])
            {
                best = i;
            }
        }

        int bbx = best % n, bby = best / n;
        return ((bbx + 0.5) * Th.VoteBin - Th.VoteRange, (bby + 0.5) * Th.VoteBin - Th.VoteRange, hist[best]);
    }

    /// <summary>
    /// Least squares for (u, v) = (a x - b y + tx, b x + a y + ty): the normal equations of the
    /// four unknowns, solved by Gaussian elimination; the RMS of the residual distances.
    /// </summary>
    internal static double[] FitSimilarity(IReadOnlyList<(double X, double Y, double U, double V)> pairs, out double rms)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        double[,] ata = new double[4, 4];
        double[] atb = new double[4];
        foreach ((double x, double y, double u, double v) in pairs)
        {
            double[] r1 = [x, -y, 1, 0], r2 = [y, x, 0, 1];
            for (int i = 0; i < 4; i++)
            {
                atb[i] += r1[i] * u + r2[i] * v;
                for (int j = 0; j < 4; j++)
                {
                    ata[i, j] += r1[i] * r1[j] + r2[i] * r2[j];
                }
            }
        }

        double[] sol = Stats.Solve(ata, atb, 4);
        double sum = 0;
        foreach ((double x, double y, double u, double v) in pairs)
        {
            double du = sol[0] * x - sol[1] * y + sol[2] - u, dv = sol[1] * x + sol[0] * y + sol[3] - v;
            sum += du * du + dv * dv;
        }

        rms = Math.Sqrt(sum / pairs.Count);
        return sol;
    }

    /// <summary>A hash grid of stars for nearest-neighbour queries within a tolerance.</summary>
    private sealed class Grid
    {
        private readonly Dictionary<(int, int), List<RegistrationStar>> _cells = [];
        private readonly double _cell;

        internal Grid(IReadOnlyList<RegistrationStar> stars, double cell)
        {
            _cell = cell;
            foreach (RegistrationStar s in stars)
            {
                (int, int) k = ((int)Math.Floor(s.X / cell), (int)Math.Floor(s.Y / cell));
                if (!_cells.TryGetValue(k, out List<RegistrationStar>? list))
                {
                    _cells[k] = list = [];
                }

                list.Add(s);
            }
        }

        internal RegistrationStar? Nearest(double x, double y, double tol)
        {
            int cx = (int)Math.Floor(x / _cell), cy = (int)Math.Floor(y / _cell);
            int reach = (int)Math.Ceiling(tol / _cell);
            RegistrationStar? best = null;
            double bestD = tol * tol;
            for (int j = cy - reach; j <= cy + reach; j++)
            {
                for (int i = cx - reach; i <= cx + reach; i++)
                {
                    if (!_cells.TryGetValue((i, j), out List<RegistrationStar>? list))
                    {
                        continue;
                    }

                    foreach (RegistrationStar s in list)
                    {
                        double d = (s.X - x) * (s.X - x) + (s.Y - y) * (s.Y - y);
                        if (d <= bestD)
                        {
                            bestD = d;
                            best = s;
                        }
                    }
                }
            }

            return best;
        }
    }
}
