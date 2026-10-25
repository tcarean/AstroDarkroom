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

// ExposureAnalyzer.cs - mode 4: is the RAW flat exposed right?
//
// Runs on a SINGLE uncalibrated raw flat, with the CFA mosaic intact and the original ADU
// scale. The deciding metric is the median of a CENTRAL 20%x20% window of the brightest
// channel, NOT the global median: a flat has by definition a wide distribution (that IS the
// vignetting), and the global median is pulled down by the corners and would trick you into
// overexposing until the centre clips.
//
// Beside the figure that decides, the mode also gathers the DISTRIBUTION of every channel - see
// LevelHistogram below. A median says where a channel sits; it does not say whether the peak
// still has room before saturation, how far the corners fall below the centre, or whether a
// tail already touches white. The histograms do not vote and change no threshold: they are the
// same data, seen.

using System;
using System.Collections.Generic;
using System.Linq;
using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>
    /// The verdict of <c>--expo</c> on the median of the central window, read on
    /// <see cref="ExposureBands"/>.
    /// </summary>
    internal enum ExposureVerdict
    {
        NotRaw,          // the sanity gate cut the verdict
        TooLow,
        AcceptableLow,   // on a panel only; a sky flat has no such band
        Ok,
        AcceptableHigh,
        TooHigh,
    }

    /// <summary>
    /// The ADU scale read FROM the file, when the file knows it: a camera raw carries exact
    /// white and black levels in its metadata. A FITS has no such thing - stacking and
    /// conversion lose them - so there the scale stays guessed from the maximum (see AutoWhite).
    /// </summary>
    internal sealed class ExposureScale
    {
        internal readonly double White, Black;
        internal readonly string Source;

        /// <summary>A scale read from a file.</summary>
        /// <param name="white">The white (saturation) level, ADU.</param>
        /// <param name="black">The black level, ADU.</param>
        /// <param name="source">Where the scale came from, as the report prints it.</param>
        internal ExposureScale(double white, double black, string source)
        {
            White = white; Black = black; Source = source;
        }
    }

    /// <summary>
    /// The distribution of one channel on the verdict's PERCENT axis: 200 bins of 0.5 %
    /// between 0 and 100 % of (white - black).
    ///
    /// The axis is chosen on purpose to be the same one the figures and the thresholds of
    /// <see cref="ExposureBands"/> sit on: that way the bands become plain intervals on the
    /// drawing, and two files or two channels compare without any conversion. Levels below
    /// black and those at or above white gather in the bin at the end, so a saturated tail shows
    /// as mass in the last bin instead of vanishing from the graph.
    /// </summary>
    internal sealed class LevelHistogram
    {
        internal const int Bins = 200;

        internal readonly int[] Counts = new int[Bins];
        internal int Total;

        /// <summary>
        /// The bin of a level; below black -> the first, at or above white -> the last.
        /// </summary>
        /// <param name="pct">The level, percent of (white - black); any value.</param>
        /// <returns>The bin index, 0..<see cref="Bins"/> - 1.</returns>
        internal static int BinOf(double pct)
        {
            int b = (int)(pct * Bins / 100.0);
            if (b < 0) return 0;
            return b >= Bins ? Bins - 1 : b;
        }

        /// <summary>Counts one level into its bin.</summary>
        /// <param name="pct">The level, percent of (white - black); any value.</param>
        internal void Add(double pct)
        {
            Counts[BinOf(pct)]++;
            Total++;
        }
    }

    /// <summary>
    /// One channel with everything that can be said about its level. On a CFA frame there are
    /// four, one per Bayer position; on a mono/processed one there is a single one, over the
    /// whole frame. All the percentages are of (white - black), like the rest of the mode.
    /// </summary>
    internal sealed class ChannelLevels
    {
        /// <summary>0..3 = CFA[y,x] in the order (y&amp;1)*2 + (x&amp;1); 0 on mono.</summary>
        internal int Position;

        /// <summary>
        /// "R", "G1", "G2", "B" when the order is known; else "CFA[0,1]". "mono" on mono.
        /// </summary>
        internal string Label;

        /// <summary>'R', 'G' or 'B'; '\0' when the file did not say the order.</summary>
        internal char Colour;

        /// <summary>
        /// The median of the WHOLE channel - pulled down by the corners, so informative.
        /// </summary>
        internal double MedianPct;

        /// <summary>
        /// The top tail of the whole channel and how much of it is already saturated.
        /// </summary>
        internal double P99Pct, ClipPct;

        /// <summary>
        /// The same central 20 % window as for the decision, but on this channel.
        /// </summary>
        internal double CentreMedianPct, CentreP1Pct, CentreP99Pct;

        internal bool IsBrightest;

        internal LevelHistogram Whole, Centre;
    }

    /// <summary>
    /// The levels of the camera's preview: three channels, no mosaic and no verdict. The same
    /// bins as for the raw, so that the two can be read side by side.
    /// </summary>
    internal sealed class PreviewLevels
    {
        /// <summary>Exactly 3: R, G, B, in that order.</summary>
        internal ChannelLevels[] Channels;

        internal string Source;
    }

    /// <summary>
    /// Everything <see cref="ExposureAnalyzer.Analyze"/> measured on one raw flat: the scale,
    /// the per-channel levels, the deciding central median and the verdict.
    /// </summary>
    internal sealed class ExposureResult
    {
        internal double White, Black, Range;
        internal string ScaleSource;            // "--sat=..." or "auto-detectat din max=..."
        internal double[] ChannelMedians;       // 4 Bayer positions, in raw ADU
        internal double CfaSpread;              // max/min over the 4
        internal bool IsCfa;
        internal int BrightestChannel;
        internal double CentreMedianPct;        // THIS decides the exposure
        internal double GlobalMedianPct;        // informative
        internal double CentreClipPct;
        internal bool LevelTooLow;              // < 1% -> calibrated/normalised
        internal bool LooksDebayered;           // CFA spread < 1.02
        internal bool IsSky;
        internal ExposureVerdict Verdict;

        /// <summary>
        /// The colour order read from the file, or null when the file does not say it.
        /// </summary>
        internal CfaPattern Cfa;

        /// <summary>4 channels on a CFA frame, 1 on a mono one. Never null.</summary>
        internal ChannelLevels[] Channels;

        /// <summary>
        /// The bands the verdict was given on. They sit in the result, the caller does not
        /// choose them again: the window draws exactly the band that voted.
        /// </summary>
        internal ExposureBands Bands;

        /// <summary>
        /// What the camera showed on its screen, or null when the file carried no preview or
        /// nobody asked for it. It enters no verdict.
        /// </summary>
        internal PreviewLevels Preview;

        internal bool IsRaw => !(LevelTooLow || LooksDebayered);

        /// <summary>
        /// The name of a Bayer position: the colour when the file said the order, the position
        /// when not. Never guessed - the order differs from camera to camera.
        /// </summary>
        /// <param name="i">The Bayer position, 0..3, as <see cref="Mosaic.PositionOf"/> numbers
        /// it.</param>
        /// <returns>"R", "G1", "G2" or "B" when the order is known, else "CFA[y,x]".</returns>
        internal string ChannelLabel(int i) => Mosaic.Label(Cfa, i);
    }

    /// <summary>
    /// Mode 4, <c>--expo</c>: measures the level of one raw flat and gives the exposure
    /// verdict on the central window of its brightest channel.
    /// </summary>
    internal static class ExposureAnalyzer
    {
        /// <summary>
        /// The scale is guessed from the maximum; if it is wrong, --sat / --black.
        /// </summary>
        /// <param name="max">The largest pixel value of the frame, ADU (or 0..1
        /// normalised).</param>
        /// <returns>The guessed white level: 1, 4095, 16383 or 65535.</returns>
        internal static double AutoWhite(double max)
        {
            if (max <= 1.5) return 1.0;
            if (max <= 4096 * 1.05) return 4095.0;
            if (max <= 16384 * 1.05) return 16383.0;
            return 65535.0;
        }

        /// <summary>
        /// Measures one raw flat: the scale, the four Bayer channels (or one, on a frame that
        /// is no mosaic), their histograms, the median of the central window of the brightest
        /// channel, and the verdict on <see cref="ExposureBands"/>.
        /// </summary>
        /// <param name="data">The raw flat's pixels, in the file's own levels (ADU).</param>
        /// <param name="satOverride">The white level given with --sat, or null to take it from
        /// the file or guess it.</param>
        /// <param name="blackOverride">The black level given with --black, or null to take it
        /// from the file (0 when the file carries none).</param>
        /// <param name="sky">True for a sky flat, whose acceptable bands differ from a
        /// panel's.</param>
        /// <param name="fileScale">
        /// The scale read from the file, or null if the file does not carry one (FITS). The
        /// order of precedence is flatcheck.py's: --sat beats the file, the file beats the guess.
        /// </param>
        /// <param name="cfa">
        /// The colour order read from the file, or null when the file does not say it. It
        /// changes no figure - only how the channels are named in the report and the window.
        /// </param>
        /// <param name="preview">
        /// The camera's preview, decoded, or null. It produces only
        /// <see cref="ExposureResult.Preview"/>; nothing in the verdict touches it.
        /// </param>
        /// <returns>The measurements and the verdict; never null.</returns>
        internal static ExposureResult Analyze(Img data, double? satOverride, double? blackOverride,
                                               bool sky, ExposureScale fileScale = null,
                                               CfaPattern cfa = null, PreviewImage preview = null)
        {
            double mx = data.D.Max();
            double white;
            string src;
            if (satOverride.HasValue) { white = satOverride.Value; src = $"--sat={white}"; }
            else if (fileScale != null) { white = fileScale.White; src = fileScale.Source; }
            else
            {
                white = AutoWhite(mx);
                src = T($"auto-detected from max={mx:F0} (use --sat/--black if it is wrong)",
                        $"auto-detectat din max={mx:F0} (foloseste --sat/--black daca e gresit)");
            }
            double black = blackOverride ?? fileScale?.Black ?? 0.0;
            double rng = white - black;

            // the 4 Bayer positions. The brightest channel clips first, so it is the limiting one.
            // The histograms fill in the SAME pass: the pixels are visited once anyway.
            var ch = new List<double>[4];
            var wholeHist = new LevelHistogram[4];
            for (int i = 0; i < 4; i++) { ch[i] = new List<double>(); wholeHist[i] = new LevelHistogram(); }
            for (int y = 0; y < data.H; y++)
                for (int x = 0; x < data.W; x++)
                {
                    int c = Mosaic.PositionOf(y, x);
                    double v = data.At(y, x);
                    ch[c].Add(v);
                    wholeHist[c].Add((v - black) / rng * 100);
                }
            var meds = ch.Select(c => Stats.Percentile(c, 50)).ToArray();
            double mmax = meds.Max(), mmin = meds.Min();
            double cfaSpread = mmax / Math.Max(mmin, 1e-9);
            bool isCfa = Mosaic.LooksCfa(cfaSpread);
            int bright = Array.IndexOf(meds, mmax);

            // the central 20%x20% window, on EACH Bayer position rebuilt as a sub-image.
            // The decision stays the brightest channel's - cenVals below is its very list -
            // but the other three are gathered because the histogram shows them all, and three
            // windows of 4 % of the frame cost nothing.
            int chH = data.H / 2, chW = data.W / 2;
            int cy0 = (int)(chH * 0.4), cy1 = (int)(chH * 0.6);
            int cx0 = (int)(chW * 0.4), cx1 = (int)(chW * 0.6);
            var cenPer = new List<double>[4];
            var centreHist = new LevelHistogram[4];
            for (int i = 0; i < 4; i++)
            {
                cenPer[i] = new List<double>();
                centreHist[i] = new LevelHistogram();
                int oy = i / 2, ox = i % 2;
                for (int y = cy0; y < cy1; y++)
                    for (int x = cx0; x < cx1; x++)
                    {
                        double v = data.At(y * 2 + oy, x * 2 + ox);
                        cenPer[i].Add(v);
                        centreHist[i].Add((v - black) / rng * 100);
                    }
            }
            var cenVals = cenPer[bright];
            double cenMed = (Stats.Percentile(cenVals, 50) - black) / rng * 100;
            double globPct = (mmax - black) / rng * 100;
            double cenClip = cenVals.Count(v => v >= Th.ExpoClipWhite * white) / (double)cenVals.Count * 100;

            if (!isCfa)
            {
                // mono/processed: the centre is taken on the whole image, not on the sub-mosaic.
                // A new list, not cenVals.Clear(): cenVals points to cenPer[bright], and
                // clearing it would silently erase a channel's data.
                cenVals = new List<double>();
                int my0 = (int)(data.H * 0.4), my1 = (int)(data.H * 0.6);
                int mx0 = (int)(data.W * 0.4), mx1 = (int)(data.W * 0.6);
                for (int y = my0; y < my1; y++)
                    for (int x = mx0; x < mx1; x++)
                        cenVals.Add(data.At(y, x));
                cenMed = (Stats.Percentile(cenVals, 50) - black) / rng * 100;
                globPct = (Stats.Percentile(data.D, 50) - black) / rng * 100;
                cenClip = cenVals.Count(v => v >= Th.ExpoClipWhite * white) / (double)cenVals.Count * 100;
            }

            var r = new ExposureResult
            {
                White = white,
                Black = black,
                Range = rng,
                ScaleSource = src,
                ChannelMedians = meds,
                CfaSpread = cfaSpread,
                IsCfa = isCfa,
                BrightestChannel = bright,
                CentreMedianPct = cenMed,
                GlobalMedianPct = globPct,
                CentreClipPct = cenClip,
                LevelTooLow = cenMed < 1.0,
                LooksDebayered = cfaSpread < 1.02,
                IsSky = sky,
                Cfa = cfa,
                Bands = ExposureBands.For(sky),
            };
            r.Channels = isCfa
                ? CfaChannels(r, ch, wholeHist, cenPer, centreHist)
                : MonoChannel(r, data, cenVals);
            r.Preview = preview != null ? AnalyzePreview(preview) : null;
            r.Verdict = Classify(r);
            return r;
        }

        /// <summary>
        /// The preview's histograms, on the axis 0..100 % of 255. It takes NO thresholds and
        /// gives NO verdict: what shows here went through white balance, gamma and the tone
        /// curve, so it no longer has any link to how much light fell on the sensor. It exists
        /// to be put beside the real level - the difference is what misleads.
        ///
        /// The central window is the same fraction as for the raw, 0.4..0.6 on both axes. It is
        /// invariant to the orientations a preview can have - a 180-degree rotation maps it
        /// onto itself, a 90-degree one swaps W and H but not the fractions - and it does not
        /// care that the preview is a few pixels narrower than the visible area of the raw
        /// (6000 against 6016 on the D3300). Both look like bugs to whoever has not measured
        /// them; they are not.
        /// </summary>
        /// <param name="p">The decoded preview, 8 bits per channel.</param>
        /// <returns>Three channels, R, G, B, with the brightest marked.</returns>
        internal static PreviewLevels AnalyzePreview(PreviewImage p)
        {
            const double White = 255.0;
            int cy0 = (int)(p.H * 0.4), cy1 = (int)(p.H * 0.6);
            int cx0 = (int)(p.W * 0.4), cx1 = (int)(p.W * 0.6);

            var channels = new ChannelLevels[3];
            double bestMedian = double.NegativeInfinity;
            int best = 0;

            for (int c = 0; c < 3; c++)
            {
                var whole = new LevelHistogram();
                var centre = new LevelHistogram();
                var all = new double[p.W * p.H];
                var mid = new List<double>(Math.Max(0, (cy1 - cy0) * (cx1 - cx0)));
                int clipped = 0;

                for (int y = 0; y < p.H; y++)
                    for (int x = 0; x < p.W; x++)
                    {
                        double v = p.At(y, x, c);
                        double pct = v / White * 100;
                        all[y * p.W + x] = v;
                        whole.Add(pct);
                        if (v >= Th.ExpoClipWhite * White) clipped++;
                        if (y >= cy0 && y < cy1 && x >= cx0 && x < cx1)
                        {
                            mid.Add(v);
                            centre.Add(pct);
                        }
                    }

                double[] sortedAll = Stats.Sorted(all);
                double[] sortedMid = Stats.Sorted(mid.ToArray());
                double median = Stats.OfSorted(sortedAll, 50) / White * 100;

                channels[c] = new ChannelLevels
                {
                    Position = c,
                    Label = "RGB"[c].ToString(),
                    Colour = "RGB"[c],
                    MedianPct = median,
                    P99Pct = Stats.OfSorted(sortedAll, 99) / White * 100,
                    ClipPct = all.Length == 0 ? 0 : clipped / (double)all.Length * 100,
                    CentreMedianPct = Stats.OfSorted(sortedMid, 50) / White * 100,
                    CentreP1Pct = Stats.OfSorted(sortedMid, 1) / White * 100,
                    CentreP99Pct = Stats.OfSorted(sortedMid, 99) / White * 100,
                    Whole = whole,
                    Centre = centre,
                };

                if (median > bestMedian) { bestMedian = median; best = c; }
            }

            // "The brightest" only to be consistent with the raw rows; on this side it decides
            // nothing, and the window does not single it out.
            channels[best].IsBrightest = true;

            return new PreviewLevels { Channels = channels, Source = p.Source };
        }

        /// <summary>A percentage of (white - black), the scale the whole mode sits on.</summary>
        /// <param name="r">The result holding the scale.</param>
        /// <param name="adu">The level, ADU.</param>
        /// <returns>The level in percent of the range; below 0 under black, above 100 over
        /// white.</returns>
        static double Pct(ExposureResult r, double adu) => (adu - r.Black) / r.Range * 100;

        /// <summary>The share of values at or above <see cref="Th.ExpoClipWhite"/> of the white
        /// level.</summary>
        /// <param name="vals">The values, ADU.</param>
        /// <param name="white">The white level, ADU.</param>
        /// <returns>The share in percent; 0 for an empty list.</returns>
        static double ClipShare(IReadOnlyList<double> vals, double white)
        {
            if (vals.Count == 0) return 0;
            int n = 0;
            for (int i = 0; i < vals.Count; i++) if (vals[i] >= Th.ExpoClipWhite * white) n++;
            return n / (double)vals.Count * 100;
        }

        /// <summary>The levels of the four Bayer channels of a CFA frame.</summary>
        /// <param name="r">The result so far: scale, medians, brightest channel, CFA order.</param>
        /// <param name="ch">The pixels of each Bayer position, ADU, indexed 0..3.</param>
        /// <param name="whole">The histogram of each whole channel.</param>
        /// <param name="cen">The pixels of each channel's central window, ADU.</param>
        /// <param name="centre">The histogram of each channel's central window.</param>
        /// <returns>Four channels, indexed by Bayer position.</returns>
        static ChannelLevels[] CfaChannels(ExposureResult r, List<double>[] ch,
            LevelHistogram[] whole, List<double>[] cen, LevelHistogram[] centre)
        {
            var o = new ChannelLevels[4];
            for (int i = 0; i < 4; i++)
            {
                // A single sort per set, then three percentiles from it.
                double[] all = Stats.Sorted(ch[i].ToArray());
                double[] mid = Stats.Sorted(cen[i].ToArray());

                o[i] = new ChannelLevels
                {
                    Position = i,
                    Label = r.ChannelLabel(i),
                    Colour = r.Cfa != null ? r.Cfa.Colours[i] : '\0',
                    MedianPct = Pct(r, r.ChannelMedians[i]),
                    P99Pct = Pct(r, Stats.OfSorted(all, 99)),
                    ClipPct = ClipShare(ch[i], r.White),
                    CentreMedianPct = Pct(r, Stats.OfSorted(mid, 50)),
                    CentreP1Pct = Pct(r, Stats.OfSorted(mid, 1)),
                    CentreP99Pct = Pct(r, Stats.OfSorted(mid, 99)),
                    IsBrightest = i == r.BrightestChannel,
                    Whole = whole[i],
                    Centre = centre[i],
                };
            }
            return o;
        }

        /// <summary>
        /// A mono or already processed frame: a single channel, over the whole frame. It is not
        /// named with a colour, because there is no mosaic left to read one from.
        /// </summary>
        /// <param name="r">The result so far: scale, global and central medians.</param>
        /// <param name="data">The frame's pixels, ADU.</param>
        /// <param name="cen">The pixels of the central window, ADU.</param>
        /// <returns>One channel, labelled "mono".</returns>
        static ChannelLevels[] MonoChannel(ExposureResult r, Img data, List<double> cen)
        {
            var whole = new LevelHistogram();
            foreach (double v in data.D) whole.Add(Pct(r, v));

            var centre = new LevelHistogram();
            foreach (double v in cen) centre.Add(Pct(r, v));

            double[] all = Stats.Sorted(data.D);
            double[] mid = Stats.Sorted(cen.ToArray());

            return new[]
            {
                new ChannelLevels
                {
                    Position = 0,
                    Label = "mono",
                    Colour = '\0',
                    MedianPct = r.GlobalMedianPct,
                    P99Pct = Pct(r, Stats.OfSorted(all, 99)),
                    ClipPct = ClipShare(data.D, r.White),
                    CentreMedianPct = r.CentreMedianPct,
                    CentreP1Pct = Pct(r, Stats.OfSorted(mid, 1)),
                    CentreP99Pct = Pct(r, Stats.OfSorted(mid, 99)),
                    IsBrightest = true,
                    Whole = whole,
                    Centre = centre,
                },
            };
        }

        /// <summary>
        /// A single scale, read from <see cref="ExposureBands"/>. A separate branch for the sky
        /// is no longer needed: there the target starts right at the too-low threshold
        /// (TooLow == Lo), so the "acceptable, but rather low" band comes out empty by itself -
        /// a sky flat MUST stay modest, because the sky fades continuously and is full of faint
        /// stars, and the master's SNR comes from the NUMBER of frames.
        /// </summary>
        /// <param name="r">The measured result, with its bands.</param>
        /// <returns>
        /// <see cref="ExposureVerdict.NotRaw"/> when the frame is no raw; else the band of the
        /// central median, with clipping above <see cref="Th.ExpoClip"/> counted as too high.
        /// </returns>
        static ExposureVerdict Classify(ExposureResult r)
        {
            if (!r.IsRaw) return ExposureVerdict.NotRaw;

            ExposureBands b = r.Bands;
            bool clipping = r.CentreClipPct > Th.ExpoClip;
            double cen = r.CentreMedianPct;

            if (clipping || cen > b.TooHigh) return ExposureVerdict.TooHigh;
            if (cen < b.TooLow) return ExposureVerdict.TooLow;
            if (cen < b.Lo) return ExposureVerdict.AcceptableLow;
            if (cen <= b.Hi) return ExposureVerdict.Ok;
            return ExposureVerdict.AcceptableHigh;
        }
    }
}
