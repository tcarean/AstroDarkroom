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
/// <param name="Ok">
/// Whether the frame was registered: every round matched at least <see cref="Th.MinMatches"/>
/// stars, and the last one at least <see cref="Th.MinMatchFraction"/> of the shorter list.
/// </param>
/// <param name="Matches">The pairs the last round matched; 0 on a failure.</param>
/// <param name="Votes">
/// The votes of the coarse offset's bin; 0 when a list was too short to vote.
/// </param>
/// <param name="Dx">
/// Where the reference's centre lands in this frame minus where it is in the reference, along x, in
/// pixels; NaN on a failure.
/// </param>
/// <param name="Dy">The same along y, in pixels; NaN on a failure.</param>
/// <param name="RotationDeg">The similarity's rotation, in degrees; NaN on a failure.</param>
/// <param name="Scale">
/// The similarity's scale, 1 for the same plate scale; NaN on a failure.
/// </param>
/// <param name="Rms">
/// The root mean square of the last fit's residual distances, in pixels; NaN on a failure.
/// </param>
internal sealed record RegistrationResult(bool Ok, int Matches, int Votes, double Dx, double Dy, double RotationDeg, double Scale, double Rms)
{
    /// <summary>
    /// Whether the frame, which could not be registered as it is, registers once its stars are
    /// turned by 180 degrees about the frame's centre: a frame taken after a meridian flip
    /// (R-087). Never true on a registered frame.
    /// </summary>
    internal bool Flipped { get; init; }

    /// <summary>A registration that failed.</summary>
    /// <param name="votes">
    /// The coarse votes reached before the failure, 0 when there were none.
    /// </param>
    /// <returns>A result with <see cref="Ok"/> false, no match and NaN figures.</returns>
    internal static RegistrationResult Failed(int votes) => new(false, 0, votes, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
}

/// <summary>
/// Registers one frame's star list to the reference's: a vote over all pairwise offsets
/// gives the coarse shift, then nearest-neighbour matches at a shrinking tolerance feed a
/// least-squares similarity (shift, rotation, scale). The prototype's `reg.py`, step for step,
/// with two additions of 2026-11-13 (ADR 0049): a fit on a small share of the stars fails, and
/// a frame that fails is tried once more turned upside down, to tell a meridian flip.
/// </summary>
internal static class Registration
{
    /// <summary>
    /// Registers <paramref name="frame"/>'s stars to <paramref name="reference"/>'s
    /// (<see cref="Align"/>). A frame that fails is aligned once more with its stars turned by
    /// 180 degrees about the frame's centre: when that succeeds the frame was taken after a
    /// meridian flip, and the failed result says so (R-087); it still has no position.
    /// </summary>
    /// <param name="reference">
    /// The reference frame's registration stars, brightest first. Not null.
    /// </param>
    /// <param name="frame">This frame's registration stars, brightest first. Not null.</param>
    /// <param name="width">The frame's width, in pixels, for its centre.</param>
    /// <param name="height">The frame's height, in pixels, for its centre.</param>
    /// <returns>
    /// The shift of the centre, the rotation, the scale and the fit; otherwise a failed result,
    /// with <see cref="RegistrationResult.Flipped"/> set when only the turned frame aligns.
    /// </returns>
    internal static RegistrationResult Register(IReadOnlyList<RegistrationStar> reference, IReadOnlyList<RegistrationStar> frame, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(frame);
        RegistrationResult direct = Align(reference, frame, width, height);
        if (direct.Ok)
        {
            return direct;
        }

        // The same field turned upside down has no single shift, so the vote finds none; turned
        // back it is a frame like any other, and the fit's own shift takes up the half pixel
        // between the turn about (width, height) / 2 and the centre of the pixel grid.
        List<RegistrationStar> turned = frame.Select(s => new RegistrationStar(width - s.X, height - s.Y, s.Flux)).ToList();
        return Align(reference, turned, width, height).Ok ? direct with { Flipped = true } : direct;
    }

    /// <summary>
    /// One registration of <paramref name="frame"/>'s stars to <paramref name="reference"/>'s:
    /// the coarse vote, then <see cref="Th.MatchIterations"/> rounds of matching (the first at
    /// <see cref="Th.MatchToleranceCoarse"/>, the others at <see cref="Th.MatchToleranceFine"/>)
    /// and fitting. The last round must match <see cref="Th.MinMatchFraction"/> of the shorter
    /// list: a fit that holds on a handful of stars of hundreds is a coincidence, with a wrong
    /// shift and rotation (R-018).
    /// </summary>
    /// <param name="reference">The reference frame's registration stars, brightest first.</param>
    /// <param name="frame">This frame's registration stars, brightest first.</param>
    /// <param name="width">The frame's width, in pixels, for its centre.</param>
    /// <param name="height">The frame's height, in pixels, for its centre.</param>
    /// <returns>
    /// The shift of the centre, the rotation, the scale and the fit; a failed result when either
    /// list or any round holds fewer than <see cref="Th.MinMatches"/> stars, when a round's fit
    /// has no solution, or when the last round holds too small a share of the shorter list.
    /// </returns>
    private static RegistrationResult Align(IReadOnlyList<RegistrationStar> reference, IReadOnlyList<RegistrationStar> frame, int width, int height)
    {
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

            // Pairs that all sit at one point give the fit nothing to solve: a frame that could
            // not be aligned, not an exception that ends the run (ADR 0062).
            if (FitSimilarity(pairs, out rms) is not double[] fit)
            {
                return RegistrationResult.Failed(votes);
            }

            sol = fit;
            matched = pairs.Count;
        }

        if (matched < Th.MinMatchFraction * Math.Min(reference.Count, frame.Count))
        {
            return RegistrationResult.Failed(votes);
        }

        double a = sol[0], b = sol[1];
        double cx = width / 2.0, cy = height / 2.0;
        double ncx = a * cx - b * cy + sol[2], ncy = b * cx + a * cy + sol[3];
        return new RegistrationResult(true, matched, votes, ncx - cx, ncy - cy, Math.Atan2(b, a) * 180 / Math.PI, Math.Sqrt(a * a + b * b), rms);
    }

    /// <summary>
    /// The most voted offset between the brightest stars of both lists, in bins of Th.VoteBin
    /// px over +-Th.VoteRange.
    /// </summary>
    /// <param name="reference">
    /// The reference's stars; the first <see cref="Th.VoteStars"/> vote.
    /// </param>
    /// <param name="frame">The frame's stars; the first <see cref="Th.VoteStars"/> vote.</param>
    /// <returns>
    /// The centre of the winning bin (frame minus reference, in pixels) and its votes; the first
    /// bin wins a tie.
    /// </returns>
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
    /// <param name="pairs">
    /// The matched pairs: (x, y) in the reference, (u, v) in the frame, in pixels. Not null.
    /// </param>
    /// <param name="rms">
    /// The root mean square of the residual distances, in pixels; NaN without a solution.
    /// </param>
    /// <returns>
    /// (a, b, tx, ty); null when the pairs make the normal equations singular (all at one point),
    /// for which <see cref="Stats.Solve"/> has no solution.
    /// </returns>
    internal static double[]? FitSimilarity(IReadOnlyList<(double X, double Y, double U, double V)> pairs, out double rms)
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

        // Stats.cs is compiled without nullable annotations: Solve gives null for a singular system.
        double[]? sol = Stats.Solve(ata, atb, 4);
        if (sol is null)
        {
            rms = double.NaN;
            return null;
        }

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

        /// <summary>
        /// Files every star in the square cell of side <paramref name="cell"/> its position falls
        /// in.
        /// </summary>
        /// <param name="stars">The stars.</param>
        /// <param name="cell">The cell's side, in pixels, above 0.</param>
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

        /// <summary>The star nearest a point, within a tolerance.</summary>
        /// <param name="x">The point's column, in pixels.</param>
        /// <param name="y">The point's row, in pixels.</param>
        /// <param name="tol">The largest distance, in pixels, included.</param>
        /// <returns>The nearest star; null when none is within <paramref name="tol"/>.</returns>
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
