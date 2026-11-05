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

// SoftProof.cs - the soft proof (R-160 to R-162, ADR 0066): what the paper shows of each sRGB
// colour, through the ICC printer profile's tables, and how that is put on an sRGB screen.
// Which pixels leave the paper's gamut is GamutAnalyzer's rule (R-144), decided by ProofRun.
//
// THE CHAIN. Source sRGB -> XYZ D50 -> (black point compensation towards the paper, for the
// relative intent) -> the profile's table towards the printer of the intent (B2A0 perceptual,
// B2A1 relative) -> device values -> the relative colorimetric table back (A2B1): the colour the
// paper shows, relative to its white. That colour is what a ProofTable stores, one table per
// intent, on a grid of ProofGrid nodes per channel; a pixel reads it trilinearly. The display
// step is applied after the interpolation and takes one parameter, "simulate": true, the
// absolute scaling by the media white of the profile's wtpt tag, so the paper's white and black
// show as they are, which is what the window always asks for; false, the paper's black scaled
// to the screen's black and its white to the screen's white (black point compensation towards
// the screen), kept for the figures of V-015 (ADR 0066). Then XYZ D50 -> linear sRGB through
// the Bradford-adapted matrix -> encoded.
//
// WHY A TABLE. The package makes the trip in 20 to 40 microseconds per colour: 35 s on a 1.4 MP
// reduction, 2 s for 65^3 nodes with the rows in parallel. Measured on 2026-10-30, 65 nodes read
// trilinearly stay within Delta E76 1.0 of the exact proof at the 99th percentile on both lab
// profiles; 33, the gamut figure's grid, reach 5 on the canvas (ADR 0066).
//
// WHY THE TABLE HOLDS XYZ AND NOT THE DISPLAY COLOUR. The display step is a per-pixel scaling:
// one table per intent serves both values of the parameter.
//
// Black point compensation is the ICC one (ISO 18619), in XYZ: XYZ' = XYZ x (W - K) / W + K
// towards the paper and its inverse towards the screen, W the D50 white and K the device black
// through A2B1 taken as the neutral of its Y, the black IccPaperBlack reads (ADR 0030). The
// package has none, so it is written here.

using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Wacton.Unicolour;
using Wacton.Unicolour.Icc;

namespace AstroPhotoAnalyzer
{
    /// <summary>The rendering intent of the soft proof (R-160).</summary>
    internal enum ProofIntent
    {
        /// <summary>The profile's perceptual table (B2A0), Lightroom's default.</summary>
        Perceptual,

        /// <summary>
        /// The colorimetric table (B2A1) with black point compensation towards the paper, as
        /// Lightroom's Relative.
        /// </summary>
        Relative,

        /// <summary>
        /// The colorimetric table without compensation: every tone darker than the paper's black
        /// prints as that black, the Tonuri tab's model (R-045).
        /// </summary>
        RelativeNoBpc,
    }

    /// <summary>
    /// The eight corners of the cell of a cube of n nodes per channel around an sRGB colour,
    /// with their weights: the one trilinear read the gamut table and the proof table share.
    /// </summary>
    internal static class Trilinear
    {
        /// <summary>
        /// Fills the indices of the eight surrounding nodes, in the order
        /// (r * n + g) * n + b with the first channel slowest, and their trilinear weights,
        /// which sum to 1; at a node one weight is 1.
        /// </summary>
        /// <param name="n">The nodes per channel, at least 2.</param>
        /// <param name="r">The encoded red, 0..1; clamped to that range.</param>
        /// <param name="g">The encoded green, 0..1; clamped to that range.</param>
        /// <param name="b">The encoded blue, 0..1; clamped to that range.</param>
        /// <param name="index">Receives the eight node indices; at least 8 long.</param>
        /// <param name="weight">Receives the eight weights; at least 8 long.</param>
        internal static void Corners(int n, float r, float g, float b, Span<int> index, Span<float> weight)
        {
            float fr = Math.Clamp(r, 0f, 1f) * (n - 1), fg = Math.Clamp(g, 0f, 1f) * (n - 1), fb = Math.Clamp(b, 0f, 1f) * (n - 1);
            int ir = Math.Min((int)fr, n - 2), ig = Math.Min((int)fg, n - 2), ib = Math.Min((int)fb, n - 2);
            float tr = fr - ir, tg = fg - ig, tb = fb - ib;
            for (int k = 0; k < 8; k++)
            {
                int dr = k & 1, dg = (k >> 1) & 1, db = (k >> 2) & 1;
                weight[k] = (dr == 1 ? tr : 1 - tr) * (dg == 1 ? tg : 1 - tg) * (db == 1 ? tb : 1 - tb);
                index[k] = ((ir + dr) * n + ig + dg) * n + ib + db;
            }
        }
    }

    /// <summary>
    /// The colour the paper shows for each node of the sRGB cube, in XYZ relative to the paper's
    /// white (D50), through the tables of one intent; or why it could not be built.
    /// </summary>
    internal sealed class ProofTable
    {
        internal int N;
        internal float[] X, Y, Z;
        internal ProofIntent Intent;

        /// <summary>Why the table could not be built, for the user; null when it could.</summary>
        internal string Problem;
    }

    /// <summary>
    /// A printer profile opened for the soft proof: its facts (the media white, the paper black,
    /// which tables it has) and its tables, one per intent, built on demand and kept.
    /// </summary>
    internal sealed class ProofProfile
    {
        internal static readonly double[] D50 = { 0.9642, 1.0, 0.8249 };

        internal string Path { get; private set; }

        /// <summary>The name in the 'desc' tag; the file's name when the profile has none.</summary>
        internal string Description { get; private set; }

        /// <summary>Why the profile cannot be proofed, for the user; null when it can.</summary>
        internal string Problem { get; private set; }

        /// <summary>
        /// The exception behind <see cref="Problem"/>, for the diagnostic log, which writes its
        /// stack (ADR 0069); null when the problem had none or there is no problem.
        /// </summary>
        internal Exception Cause { get; private set; }

        /// <summary>The media white of the 'wtpt' tag, XYZ; D50 when the tag is absent.</summary>
        internal double[] MediaWhite { get; private set; }

        /// <summary>
        /// The device black through the colorimetric table, as the neutral of its Y (XYZ relative
        /// to the paper's white): what the black point compensation scales with.
        /// </summary>
        internal double[] PaperBlack { get; private set; }

        /// <summary>The same black in L*, and its Dmax, as IccPaperBlack reads them (R-028).</summary>
        internal double PaperBlackLstar { get; private set; }
        internal double Dmax { get; private set; }

        /// <summary>The media white in L*a*b* against D50: what the image's white becomes under the simulation.</summary>
        internal (double L, double A, double B) PaperWhiteLab { get; private set; }

        /// <summary>
        /// The display colour of sRGB white with the simulation on, encoded sRGB 0..1: the margin
        /// of the proof (R-163).
        /// </summary>
        internal float[] PaperWhiteSrgb { get; private set; }

        Configuration _relative, _perceptual, _back;
        readonly ConcurrentDictionary<ProofIntent, Lazy<ProofTable>> _tables = new();

        /// <summary>Only <see cref="Open"/> and <see cref="ForTests"/> build one.</summary>
        ProofProfile() { }

        /// <summary>
        /// A profile with its facts alone, for a test of the display step and the warnings on a
        /// hand-built table: no file, no tables of its own.
        /// </summary>
        /// <param name="mediaWhite">The media white, XYZ.</param>
        /// <param name="paperBlack">The paper black, XYZ relative to the white.</param>
        /// <returns>The profile; <see cref="Table"/> gives a table with a problem.</returns>
        internal static ProofProfile ForTests(double[] mediaWhite, double[] paperBlack)
        {
            var p = new ProofProfile { Description = "test", MediaWhite = mediaWhite, PaperBlack = paperBlack };
            p.PaperBlackLstar = ColorMath.LstarOfY(paperBlack[1]);
            p.Dmax = paperBlack[1] > 0 ? Math.Round(-Math.Log10(paperBlack[1]), 2) : double.PositiveInfinity;
            p.FinishFacts();
            return p;
        }

        /// <summary>
        /// The device black of a profile through its colorimetric table, as the neutral of its
        /// Y: the black the compensation scales with. The black point compensation of ISO 18619
        /// (lcms too) takes the black so, not with the chromaticity the table gives it: the
        /// scaling is then one factor on X, Y and Z, and a slightly coloured black does not tint
        /// the shadows. The L* and the Dmax are the same either way. Shared with the gamut
        /// figure (ADR 0081), so the proof's compensation and the figure's cannot drift apart.
        /// </summary>
        /// <param name="back">The package's configuration with the profile's relative tables.</param>
        /// <param name="dataColourSpace">The profile header's device space, "RGB " or "CMYK".</param>
        /// <returns>
        /// The black's XYZ relative to the paper's white; null when the table gives no black
        /// between 0 and the white (a damaged profile).
        /// </returns>
        internal static double[] NeutralBlack(Configuration back, string dataColourSpace)
        {
            double[] device = dataColourSpace.Trim() == "CMYK" ? new[] { 1.0, 1.0, 1.0, 1.0 } : new double[3];
            var k = new Unicolour(back, new Channels(device)).Xyz;
            if (double.IsNaN(k.Y) || k.Y < 0 || k.Y >= 1) return null;
            return new[] { D50[0] * k.Y, k.Y, D50[2] * k.Y };
        }

        /// <summary>
        /// The black point compensation towards the paper: XYZ x (W - K) / W + K, W the D50
        /// white, K the paper's black, so that the image's black lands on the paper's.
        /// </summary>
        /// <param name="x">The colour's X, relative to D50.</param>
        /// <param name="y">The colour's Y.</param>
        /// <param name="z">The colour's Z.</param>
        /// <param name="k">The paper's black, XYZ, as <see cref="NeutralBlack"/> gives it.</param>
        /// <returns>The compensated XYZ.</returns>
        internal static (double X, double Y, double Z) Compensate(double x, double y, double z, double[] k)
            => (x * (D50[0] - k[0]) / D50[0] + k[0], y * (D50[1] - k[1]) / D50[1] + k[1], z * (D50[2] - k[2]) / D50[2] + k[2]);

        /// <summary>
        /// The compensation undone, towards the screen: (XYZ - K) x W / (W - K), so that the
        /// paper's black is the screen's black and its white the screen's white.
        /// </summary>
        /// <param name="x">The paper colour's X, relative to the paper's white.</param>
        /// <param name="y">The paper colour's Y.</param>
        /// <param name="z">The paper colour's Z.</param>
        /// <param name="k">The paper's black, XYZ, as <see cref="NeutralBlack"/> gives it.</param>
        /// <returns>The XYZ scaled back.</returns>
        internal static (double X, double Y, double Z) Uncompensate(double x, double y, double z, double[] k)
            => ((x - k[0]) * D50[0] / (D50[0] - k[0]), (y - k[1]) * D50[1] / (D50[1] - k[1]), (z - k[2]) * D50[2] / (D50[2] - k[2]));

        /// <summary>
        /// Opens the profile at <paramref name="path"/> for the proof: the checks of
        /// <see cref="IccPaperBlack.Read(string)"/> (a readable printer profile with an RGB or CMYK
        /// device), then the tables towards the printer (B2A0 and B2A1, both required), the media
        /// white and the device black. Every refusal is a <see cref="Problem"/>, never an
        /// exception.
        /// </summary>
        /// <param name="path">The profile's file.</param>
        /// <returns>The profile, with Problem set when it cannot be proofed.</returns>
        internal static ProofProfile Open(string path)
        {
            IccBlack black = IccPaperBlack.Read(path);
            var p = new ProofProfile { Path = path, Description = black.Description, PaperBlackLstar = black.BlackLstar, Dmax = black.Dmax, MediaWhite = (double[])D50.Clone() };
            if (!black.Readable) { p.Problem = black.Problem; return p; }
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                string name = System.IO.Path.GetFileName(path);
                var profile = new Profile(bytes, name);
                bool hasB2A0 = false, hasB2A1 = false;
                foreach (object o in (IEnumerable)profile.Tags)
                {
                    var tag = (Tag)o;
                    switch (tag.Signature)
                    {
                        case "B2A0": hasB2A0 = true; break;
                        case "B2A1": hasB2A1 = true; break;
                        case "wtpt":
                            // XYZType: the signature, four reserved bytes, then X, Y, Z as s15Fixed16.
                            if (tag.Data != null && tag.Data.Length >= 20)
                                p.MediaWhite = new[] { S15(tag.Data, 8), S15(tag.Data, 12), S15(tag.Data, 16) };
                            break;
                    }
                }
                // B2A0 is mandatory in a printer profile and the package refuses one without it;
                // B2A1 is what the relative intents and the gamut figure use. Both, or no proof.
                if (!hasB2A0 || !hasB2A1) { p.Problem = NoB2A(hasB2A0 ? "B2A1" : "B2A0"); return p; }
                if (p.MediaWhite[1] <= 0) p.MediaWhite = (double[])D50.Clone();

                var relative = new IccConfiguration(profile, Intent.RelativeColorimetric, name);
                if (!string.IsNullOrEmpty(relative.Error)) { p.Problem = Unreadable + relative.Error; return p; }
                p._relative = p._back = new Configuration(xyzConfig: XyzConfiguration.D50, iccConfig: relative);
                var perceptual = new IccConfiguration(profile, Intent.Perceptual, name);
                if (!string.IsNullOrEmpty(perceptual.Error)) { p.Problem = Unreadable + perceptual.Error; return p; }
                p._perceptual = new Configuration(xyzConfig: XyzConfiguration.D50, iccConfig: perceptual);
                p.PaperBlack = NeutralBlack(p._back, profile.Header.DataColourSpace);
                if (p.PaperBlack == null) { p.Problem = BlackUnusable; return p; }
                p.FinishFacts();
                return p;
            }
            catch (Exception e)
            {
                p.Problem = Unreadable + (e.InnerException ?? e).Message;
                p.Cause = e;
                return p;
            }
        }

        /// <summary>The paper white's L*a*b* and display colour, from the media white.</summary>
        void FinishFacts()
        {
            PaperWhiteLab = ColorMath.LabOfXyz(MediaWhite[0], MediaWhite[1], MediaWhite[2], D50[0], D50[1], D50[2]);
            var (r, g, b) = SoftProof.Display(this, D50[0], D50[1], D50[2], simulate: true);
            PaperWhiteSrgb = new[] { r, g, b };
        }

        /// <summary>
        /// The table of one intent, built on the first call and kept; the rows in parallel.
        /// </summary>
        /// <param name="intent">The intent.</param>
        /// <returns>
        /// The table; with Problem set when the profile has none (the perceptual one on a profile
        /// without B2A0, any on a profile with a Problem).
        /// </returns>
        internal ProofTable Table(ProofIntent intent)
            => _tables.GetOrAdd(intent, i => new Lazy<ProofTable>(() => Build(i), LazyThreadSafetyMode.ExecutionAndPublication)).Value;

        /// <summary>Builds the table of one intent on a grid of ProofGrid nodes.</summary>
        /// <param name="intent">The intent.</param>
        /// <returns>The table, or one with Problem set.</returns>
        ProofTable Build(ProofIntent intent)
        {
            if (Problem != null || _back == null) return new ProofTable { Intent = intent, Problem = Problem ?? NoB2A("B2A") };
            Configuration forward = intent == ProofIntent.Perceptual ? _perceptual : _relative;
            bool bpc = intent == ProofIntent.Relative;
            int n = Thresholds.ProofGrid;
            var t = new ProofTable { N = n, Intent = intent, X = new float[n * n * n], Y = new float[n * n * n], Z = new float[n * n * n] };
            double[] k = PaperBlack;
            int failed = 0;
            Parallel.For(0, n, i =>
            {
                for (int j = 0; j < n; j++)
                    for (int l = 0; l < n; l++)
                    {
                        var source = new Unicolour(forward, ColourSpace.Rgb, i / (n - 1.0), j / (n - 1.0), l / (n - 1.0));
                        if (bpc)
                        {
                            var xyz = source.Xyz;
                            var (x, y, z) = Compensate(xyz.X, xyz.Y, xyz.Z, k);
                            source = new Unicolour(forward, ColourSpace.Xyz, x, y, z);
                        }
                        var paper = new Unicolour(_back, source.Icc).Xyz;
                        int at = (i * n + j) * n + l;
                        t.X[at] = (float)paper.X; t.Y[at] = (float)paper.Y; t.Z[at] = (float)paper.Z;
                        if (double.IsNaN(paper.Y)) Interlocked.Exchange(ref failed, 1);
                    }
            });
            return failed == 1 ? new ProofTable { Intent = intent, Problem = NoB2A(intent == ProofIntent.Perceptual ? "B2A0" : "B2A1") } : t;
        }

        /// <summary>An s15Fixed16 number of a tag's data.</summary>
        /// <param name="data">The tag's bytes.</param>
        /// <param name="at">The number's offset, in bytes.</param>
        /// <returns>The value.</returns>
        static double S15(byte[] data, int at) => BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(at, 4)) / 65536.0;

        /// <summary>The refusal of a profile missing a table, naming it.</summary>
        /// <param name="table">The table's tag: B2A0, B2A1, A2B1, or B2A for either.</param>
        /// <returns>The sentence, in the interface language.</returns>
        static string NoB2A(string table) => Tr.Pick($"the profile lacks the part that prepares the colours for the printer (the {table} table)", $"profilului ii lipseste partea care pregateste culorile pentru imprimanta (tabela {table})");
        static string Unreadable => Tr.Pick("the profile cannot be read: ", "profilul nu se poate citi: ");
        internal static string BlackUnusable => Tr.Pick("the profile does not show properly how black the paper prints (the A2B1 table gives no black between 0 and the paper white); the profile is probably damaged",
            "profilul nu arata corect cat de negru tipareste hartia (tabela A2B1 nu da un negru intre 0 si albul hartiei); profilul e probabil stricat");
    }

    /// <summary>The proof of a reduced image: the proofed image, and how it was made.</summary>
    internal sealed record SoftProofResult
    {
        /// <summary>The proofed image, three planes, encoded sRGB 0..1.</summary>
        internal Raster Image { get; init; }

        internal ProofIntent Intent { get; init; }
        internal bool Simulated { get; init; }
    }

    /// <summary>The soft proof of an image through a profile's table (R-160).</summary>
    internal static class SoftProof
    {
        // XYZ D50 to linear sRGB: the sRGB matrix adapted to D50 with Bradford, the one the ICC
        // world uses for an sRGB working space.
        const double M00 = 3.1338561, M01 = -1.6168667, M02 = -0.4906146;
        const double M10 = -0.9787684, M11 = 1.9161415, M12 = 0.0334540;
        const double M20 = 0.0719453, M21 = -0.2289914, M22 = 1.4052427;

        /// <summary>
        /// The display step of one paper colour: the scaling the simulation decides (the paper's
        /// black and white to the screen's, or the absolute scaling by the media white), then the
        /// Bradford-adapted sRGB matrix and the encoding, the channels clamped to 0..1. A colour
        /// the screen cannot show is clipped: with an sRGB source and a profile that only pulls
        /// colours inward, none leaves it but a bluish paper white, by less than the eye sees
        /// (ADR 0066).
        /// </summary>
        /// <param name="p">The profile, for its media white and paper black.</param>
        /// <param name="x">The paper colour's X, relative to the paper's white.</param>
        /// <param name="y">The paper colour's Y.</param>
        /// <param name="z">The paper colour's Z.</param>
        /// <param name="simulate">Whether the paper's white and black are shown as they are.</param>
        /// <returns>The encoded sRGB, each 0..1.</returns>
        internal static (float R, float G, float B) Display(ProofProfile p, double x, double y, double z, bool simulate)
        {
            double[] w = ProofProfile.D50, k = p.PaperBlack, m = p.MediaWhite;
            if (simulate)
            {
                x *= m[0] / w[0]; y *= m[1] / w[1]; z *= m[2] / w[2];
            }
            else (x, y, z) = ProofProfile.Uncompensate(x, y, z, k);
            double rl = M00 * x + M01 * y + M02 * z;
            double gl = M10 * x + M11 * y + M12 * z;
            double bl = M20 * x + M21 * y + M22 * z;
            return ((float)ColorMath.LinearToSrgb(Math.Clamp(rl, 0, 1)), (float)ColorMath.LinearToSrgb(Math.Clamp(gl, 0, 1)), (float)ColorMath.LinearToSrgb(Math.Clamp(bl, 0, 1)));
        }

        /// <summary>
        /// Proofs <paramref name="img"/> through <paramref name="t"/>: each pixel's paper colour
        /// read trilinearly, then the display step of <see cref="Display"/>; the rows in
        /// parallel. A mono image goes through as grey; the result always has three planes.
        /// </summary>
        /// <param name="p">The profile the table belongs to.</param>
        /// <param name="t">The table of the intent; one with a Problem is refused.</param>
        /// <param name="img">The image, 0..1, already reduced.</param>
        /// <param name="simulate">Whether the paper's white and black are shown as they are.</param>
        /// <returns>The proofed image.</returns>
        internal static SoftProofResult Render(ProofProfile p, ProofTable t, Raster img, bool simulate)
        {
            if (t.Problem != null) throw new ArgumentException(t.Problem, nameof(t));
            int w = img.W, h = img.H;
            var o = new Raster(h, w, 3);
            float[] R = img.Planes[0], G = img.IsColor ? img.Planes[1] : img.Planes[0], B = img.IsColor ? img.Planes[2] : img.Planes[0];
            float[] oR = o.Planes[0], oG = o.Planes[1], oB = o.Planes[2];
            Parallel.For(0, h, y =>
            {
                Span<int> index = stackalloc int[8];
                Span<float> weight = stackalloc float[8];
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    Trilinear.Corners(t.N, R[i], G[i], B[i], index, weight);
                    double px = 0, py = 0, pz = 0;
                    for (int c = 0; c < 8; c++)
                    {
                        px += weight[c] * t.X[index[c]];
                        py += weight[c] * t.Y[index[c]];
                        pz += weight[c] * t.Z[index[c]];
                    }
                    var (r, g, b) = Display(p, px, py, pz, simulate);
                    oR[i] = r; oG[i] = g; oB[i] = b;
                }
            });
            return new SoftProofResult { Image = o, Intent = t.Intent, Simulated = simulate };
        }
    }
}
