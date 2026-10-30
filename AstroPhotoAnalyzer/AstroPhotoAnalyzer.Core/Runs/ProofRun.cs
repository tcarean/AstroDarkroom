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

// ProofRun.cs - the core's third run: the soft proof of an image already analysed (R-160 to
// R-162, ADR 0066), beside AnalysisRun and CurveRun.
//
// Like the curves it is not a diagnosis and it costs: the window runs it after the analysis
// (ADR 0051), on the result, and the console never calls it. Prepare opens the profile the Dmax
// came from, reduces the image and decides once which pixels leave the paper's gamut (the rule
// of R-144, on the source colours, the same for every intent); Render proofs the reduction
// through the table of an intent, built on its first use and kept on the profile.

using System;
using System.Threading.Tasks;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// What the Soft proof tab works on for one image: the profile with its facts and tables,
    /// the reduced image, the paper-gamut mask; or why there is no proof.
    /// </summary>
    internal sealed class ProofSet
    {
        /// <summary>Why nothing can be proofed, for the user; null when the set is usable.</summary>
        internal string Problem { get; init; }

        internal ProofProfile Profile { get; init; }

        /// <summary>The image reduced to at most ProofMaxSide on its long side.</summary>
        internal Raster Reduced { get; init; }

        /// <summary>Per pixel of <see cref="Reduced"/>, in row order, whether it leaves the paper's gamut (R-144, R-161): the warning's mask.</summary>
        internal bool[] PaperOutside { get; init; }

        /// <summary>The share of <see cref="Reduced"/> outside the paper's gamut, in percent.</summary>
        internal double PctPaper { get; init; }
    }

    /// <summary>Prepares and renders the soft proof of an analysed image (ADR 0066).</summary>
    internal static class ProofRun
    {
        /// <summary>
        /// The integer step that brings the image's long side to at most ProofMaxSide.
        /// </summary>
        /// <param name="img">The image; only its size is read.</param>
        /// <returns>The step, 1 or more.</returns>
        internal static int Stride(Raster img)
            => Math.Max(1, (int)Math.Ceiling(Math.Max(img.W, img.H) / (double)Thresholds.ProofMaxSide));

        /// <summary>
        /// Opens the profile the analysis's Dmax came from, reduces the image and marks the
        /// pixels outside the paper's gamut. Without a profile, or with one that cannot be
        /// proofed, the set carries the reason and nothing else.
        /// </summary>
        /// <param name="a">The analysis, with its raster and parameters.</param>
        /// <returns>The set, or one with Problem set.</returns>
        internal static ProofSet Prepare(AnalysisResult a)
        {
            Parameters prm = a.Parameters;
            string path = prm.DmaxProfile != null && prm.DmaxOverride.HasValue ? prm.DmaxProfilePath : null;
            if (path == null) return new ProofSet { Problem = ProofTexts.NoProfile };
            ProofProfile profile = ProofProfile.Open(path);
            if (profile.Problem != null) return new ProofSet { Problem = profile.Problem };
            GamutTable gamut = GamutAnalyzer.Build(path);
            if (gamut.Problem != null) return new ProofSet { Problem = gamut.Problem };

            Raster reduced = RasterOps.Subsample(a.Raster, Stride(a.Raster));
            int w = reduced.W, h = reduced.H;
            var outside = new bool[w * h];
            long count = 0;
            if (reduced.IsColor)
            {
                float[] R = reduced.Planes[0], G = reduced.Planes[1], B = reduced.Planes[2];
                object gate = new();
                Parallel.For(0, h, () => 0L, (y, _, n) =>
                {
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * w + x;
                        if (GamutAnalyzer.Outside(gamut, R[i], G[i], B[i])) { outside[i] = true; n++; }
                    }
                    return n;
                }, n => { lock (gate) count += n; });
            }
            return new ProofSet { Profile = profile, Reduced = reduced, PaperOutside = outside, PctPaper = outside.Length == 0 ? 0 : 100.0 * count / outside.Length };
        }

        /// <summary>
        /// Proofs the set's reduced image through the table of <paramref name="intent"/>, built
        /// on its first use.
        /// </summary>
        /// <param name="set">A set without a Problem.</param>
        /// <param name="intent">The intent; the perceptual one needs the profile's B2A0.</param>
        /// <param name="simulate">Whether the paper's white and black are shown as they are.</param>
        /// <returns>The proof.</returns>
        internal static SoftProofResult Render(ProofSet set, ProofIntent intent, bool simulate)
        {
            if (set.Problem != null) throw new InvalidOperationException(set.Problem);
            return SoftProof.Render(set.Profile, set.Profile.Table(intent), set.Reduced, simulate);
        }
    }
}
