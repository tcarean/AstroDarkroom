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
// through the table of an intent, built on its first use and kept on the profile. PrepareOther
// does the same for a second image the tab compares the first with (R-165, ADR 0091), from the
// first image's set: the same profile object and gamut table, its own reduction and mask.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NLog;

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

        /// <summary>The table the mask was decided with; a second image is judged by the same one (R-165).</summary>
        internal GamutTable Gamut { get; init; }

        /// <summary>The width of the image before the reduction, in pixels.</summary>
        internal int SourceW { get; init; }

        /// <summary>The height of the image before the reduction, in pixels.</summary>
        internal int SourceH { get; init; }
    }

    /// <summary>Prepares and renders the soft proof of an analysed image (ADR 0066).</summary>
    internal static class ProofRun
    {
        /// <summary>
        /// The diagnostic log: the preparation's start and done lines with the time of each
        /// stage, a profile or a table that cannot be used with its cause, each render at Debug
        /// (R-171, ADR 0069).
        /// </summary>
        static readonly Logger Log = LogManager.GetLogger("ProofRun");

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
            using RunClock clock = RunClock.Start(Log, "proof", "path=" + LogText.Str(a.Path) + " profile=" + LogText.Str(path) + " stride=" + LogText.Int(Stride(a.Raster)));
            // A refusal is the run's outcome, written as the tab shows it.
            if (path == null)
            {
                clock.Done("refused: " + ProofTexts.NoProfile);
                return new ProofSet { Problem = ProofTexts.NoProfile };
            }
            ProofProfile profile = ProofProfile.Open(path);
            clock.Lap("profile");
            if (profile.Problem != null)
            {
                Log.Warn(profile.Cause, "profile " + path + " cannot be proofed: " + LogText.Str(profile.Problem));
                clock.Done("refused: " + LogText.Str(profile.Problem));
                return new ProofSet { Problem = profile.Problem };
            }
            GamutTable gamut = GamutAnalyzer.Build(path);
            clock.Lap("gamut");
            if (gamut.Problem != null)
            {
                Log.Warn(gamut.Cause, "the gamut table of " + path + " cannot be built: " + LogText.Str(gamut.Problem));
                clock.Done("refused: " + LogText.Str(gamut.Problem));
                return new ProofSet { Problem = gamut.Problem };
            }

            ProofSet set = Reduce(profile, gamut, a.Raster);
            clock.Lap("mask");
            clock.Done("outside=" + LogText.Num(set.PctPaper) + " % reduced=" + LogText.Int(set.Reduced.W) + "x" + LogText.Int(set.Reduced.H) + " description=" + LogText.Str(profile.Description)
                + " dmax=" + LogText.Num(profile.Dmax) + " paperBlackLstar=" + LogText.Num(profile.PaperBlackLstar));
            return set;
        }

        /// <summary>
        /// Reduces an image and marks the pixels of the reduction the paper cannot hold (R-161):
        /// the usable set of one image, the analysed one or the one it is compared with.
        /// </summary>
        /// <param name="profile">The profile the proof goes through; kept on the set as it is.</param>
        /// <param name="gamut">The gamut table of the same profile, without a problem.</param>
        /// <param name="img">The whole image, 0..1; a mono one has nothing outside.</param>
        /// <returns>The set, without a problem.</returns>
        static ProofSet Reduce(ProofProfile profile, GamutTable gamut, Raster img)
        {
            Raster reduced = RasterOps.Subsample(img, Stride(img));
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
            return new ProofSet
            {
                Profile = profile,
                Gamut = gamut,
                Reduced = reduced,
                PaperOutside = outside,
                PctPaper = outside.Length == 0 ? 0 : 100.0 * count / outside.Length,
                SourceW = img.W,
                SourceH = img.H,
            };
        }

        /// <summary>
        /// Reads another image file and prepares it for the comparison with the analysed one
        /// (R-165), as <see cref="PrepareOther(ProofSet, Raster, string)"/> does. A file that is
        /// not there, of a kind the program does not read, or that its reader cannot read gives a
        /// set with the reason as its Problem, never an exception.
        /// </summary>
        /// <param name="first">The analysed image's set, without a Problem.</param>
        /// <param name="path">The other image's file.</param>
        /// <returns>The other image's set, or one with Problem set.</returns>
        internal static ProofSet PrepareOther(ProofSet first, string path)
        {
            if (first.Problem != null) throw new InvalidOperationException(first.Problem);
            using RunClock clock = StartOther(first, path);
            Raster other;
            try
            {
                other = ImageLoader.Load(path);
            }
            catch (Exception e)
            {
                // The loader's and the readers' messages are written for the user already.
                Log.Warn(e, "the image to compare with cannot be read: " + path);
                clock.Done("refused: " + LogText.Str(e.Message));
                return new ProofSet { Problem = e.Message };
            }
            clock.Lap("load");
            return Other(first, other, clock);
        }

        /// <summary>
        /// Prepares another image for the comparison with the analysed one (R-165): refused when
        /// its size is not the analysed image's or when it looks linear (R-015); otherwise reduced
        /// by the same rule and its own pixels marked by the same gamut table, on a set that
        /// holds the analysed image's own profile object, so each intent's table is built once
        /// for both.
        /// </summary>
        /// <param name="first">The analysed image's set, without a Problem.</param>
        /// <param name="other">The other image, 0..1.</param>
        /// <param name="path">The other image's path, for the diagnostic log.</param>
        /// <returns>The other image's set, or one with Problem set.</returns>
        internal static ProofSet PrepareOther(ProofSet first, Raster other, string path)
        {
            if (first.Problem != null) throw new InvalidOperationException(first.Problem);
            using RunClock clock = StartOther(first, path);
            return Other(first, other, clock);
        }

        /// <summary>Starts the clock of a second image's preparation, named <c>proof-other</c>.</summary>
        /// <param name="first">The analysed image's set.</param>
        /// <param name="path">The other image's path.</param>
        /// <returns>The clock, to hold in a <c>using</c>.</returns>
        static RunClock StartOther(ProofSet first, string path)
            => RunClock.Start(Log, "proof-other", "path=" + LogText.Str(path) + " against=" + LogText.Int(first.SourceW) + "x" + LogText.Int(first.SourceH));

        /// <summary>The body of the two <c>PrepareOther</c>: the refusals, then the reduction and the mask.</summary>
        /// <param name="first">The analysed image's set, without a Problem.</param>
        /// <param name="other">The other image, 0..1.</param>
        /// <param name="clock">The run's clock; its done line is written here.</param>
        /// <returns>The other image's set, or one with Problem set.</returns>
        static ProofSet Other(ProofSet first, Raster other, RunClock clock)
        {
            if (other.W != first.SourceW || other.H != first.SourceH)
            {
                clock.Done("refused: another size, " + LogText.Int(other.W) + "x" + LogText.Int(other.H));
                return new ProofSet { Problem = ProofTexts.SizeMismatch(first.SourceW, first.SourceH, other.W, other.H) };
            }
            if (RasterOps.LooksLinear(other, out double median))
            {
                clock.Done("refused: the image looks linear, median " + LogText.Num(median) + " under " + LogText.Num(Thresholds.LinearMedianMax));
                return new ProofSet { Problem = AnalysisRun.LinearRefusal(median) };
            }
            ProofSet set = Reduce(first.Profile, first.Gamut, other);
            clock.Lap("mask");
            clock.Done("outside=" + LogText.Num(set.PctPaper) + " % reduced=" + LogText.Int(set.Reduced.W) + "x" + LogText.Int(set.Reduced.H));
            return set;
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
            var clock = Stopwatch.StartNew();
            SoftProofResult proof = SoftProof.Render(set.Profile, set.Profile.Table(intent), set.Reduced, simulate);
            Log.Debug("proof rendered intent=" + intent + " simulate=" + LogText.Yes(simulate) + " in " + LogText.Ms(clock.ElapsedMilliseconds));
            return proof;
        }
    }
}
