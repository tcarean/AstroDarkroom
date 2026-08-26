// ExposureAnalyzer.cs - modul 4: e flat-ul BRUT expus corect?
//
// Ruleaza pe un SINGUR flat brut necalibrat, cu mozaicul CFA intact si scala ADU
// originala. Metrica de decizie e mediana unei ferestre CENTRALE de 20%x20% din canalul
// cel mai luminos, NU mediana globala: un flat are prin definitie o distributie larga
// (asta E vignetarea), iar mediana globala e trasa in jos de colturi si te-ar pacali sa
// supraexpui pana clipeaza centrul.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck
{
    internal enum ExposureVerdict
    {
        NotRaw,          // gate-ul de sanitate a taiat verdictul
        TooLow,
        AcceptableLow,   // doar pe panou; sky flat n-are banda asta
        Ok,
        AcceptableHigh,
        TooHigh,
    }

    internal sealed class ExposureResult
    {
        internal double White, Black, Range;
        internal string ScaleSource;            // "--sat=..." sau "auto-detectat din max=..."
        internal double[] ChannelMedians;       // 4 pozitii Bayer, in ADU brut
        internal double CfaSpread;              // max/min peste cele 4
        internal bool IsCfa;
        internal int BrightestChannel;
        internal double CentreMedianPct;        // ASTA decide expunerea
        internal double GlobalMedianPct;        // informativ
        internal double CentreClipPct;
        internal bool LevelTooLow;              // < 1% -> calibrat/normalizat
        internal bool LooksDebayered;           // spread CFA < 1.02
        internal bool IsSky;
        internal ExposureVerdict Verdict;

        internal bool IsRaw => !(LevelTooLow || LooksDebayered);
    }

    internal static class ExposureAnalyzer
    {
        internal const double CenLo = 50.0, CenHi = 65.0;   // tinta pt panou
        internal const double SkyLo = 15.0, SkyHi = 50.0;   // tinta pt sky flat
        internal const double SkyTooHigh = 60.0;
        internal const double TooLowBand = 30.0, TooHighBand = 80.0;

        /// <summary>Scala se ghiceste din maxim; daca greseste, --sat / --black.</summary>
        internal static double AutoWhite(double max)
        {
            if (max <= 1.5) return 1.0;
            if (max <= 4096 * 1.05) return 4095.0;
            if (max <= 16384 * 1.05) return 16383.0;
            return 65535.0;
        }

        internal static ExposureResult Analyze(Img data, double? satOverride, double? blackOverride, bool sky)
        {
            double mx = data.D.Max();
            double white;
            string src;
            if (satOverride.HasValue) { white = satOverride.Value; src = $"--sat={white}"; }
            else
            {
                white = AutoWhite(mx);
                src = $"auto-detectat din max={mx:F0} (foloseste --sat/--black daca e gresit)";
            }
            double black = blackOverride ?? 0.0;
            double rng = white - black;

            // cele 4 pozitii Bayer. Canalul cel mai luminos clipeaza primul, deci e cel limitant.
            var ch = new List<double>[4];
            for (int i = 0; i < 4; i++) ch[i] = new List<double>();
            for (int y = 0; y < data.H; y++)
                for (int x = 0; x < data.W; x++)
                    ch[(y & 1) * 2 + (x & 1)].Add(data.At(y, x));
            var meds = ch.Select(c => Stats.Percentile(c, 50)).ToArray();
            double mmax = meds.Max(), mmin = meds.Min();
            double cfaSpread = mmax / Math.Max(mmin, 1e-9);
            bool isCfa = cfaSpread > 1.15;
            int bright = Array.IndexOf(meds, mmax);

            // fereastra centrala 20%x20% din canalul cel mai luminos, refacut ca sub-imagine
            int chH = data.H / 2, chW = data.W / 2;
            int oy = bright / 2, ox = bright % 2;
            var cenVals = new List<double>();
            int cy0 = (int)(chH * 0.4), cy1 = (int)(chH * 0.6);
            int cx0 = (int)(chW * 0.4), cx1 = (int)(chW * 0.6);
            for (int y = cy0; y < cy1; y++)
                for (int x = cx0; x < cx1; x++)
                    cenVals.Add(data.At(y * 2 + oy, x * 2 + ox));
            double cenMed = (Stats.Percentile(cenVals, 50) - black) / rng * 100;
            double globPct = (mmax - black) / rng * 100;
            double cenClip = cenVals.Count(v => v >= 0.98 * white) / (double)cenVals.Count * 100;

            if (!isCfa)
            {
                // mono/procesat: centrul se ia pe intreaga imagine, nu pe sub-mozaic
                cenVals.Clear();
                int my0 = (int)(data.H * 0.4), my1 = (int)(data.H * 0.6);
                int mx0 = (int)(data.W * 0.4), mx1 = (int)(data.W * 0.6);
                for (int y = my0; y < my1; y++)
                    for (int x = mx0; x < mx1; x++)
                        cenVals.Add(data.At(y, x));
                cenMed = (Stats.Percentile(cenVals, 50) - black) / rng * 100;
                globPct = (Stats.Percentile(data.D.ToList(), 50) - black) / rng * 100;
                cenClip = cenVals.Count(v => v >= 0.98 * white) / (double)cenVals.Count * 100;
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
            };
            r.Verdict = Classify(r);
            return r;
        }

        static ExposureVerdict Classify(ExposureResult r)
        {
            if (!r.IsRaw) return ExposureVerdict.NotRaw;

            bool clipping = r.CentreClipPct > Th.ExpoClip;
            double cen = r.CentreMedianPct;

            if (r.IsSky)
            {
                // Sky flats: un nivel modest e CORECT. Cerul se intuneca continuu si e plin
                // de stele slabe, iar SNR-ul masterului vine din NUMARUL de cadre.
                if (clipping || cen > SkyTooHigh) return ExposureVerdict.TooHigh;
                if (cen < SkyLo) return ExposureVerdict.TooLow;
                if (cen <= SkyHi) return ExposureVerdict.Ok;
                return ExposureVerdict.AcceptableHigh;
            }

            if (clipping || cen > TooHighBand) return ExposureVerdict.TooHigh;
            if (cen < TooLowBand) return ExposureVerdict.TooLow;
            if (cen < CenLo) return ExposureVerdict.AcceptableLow;
            if (cen <= CenHi) return ExposureVerdict.Ok;
            return ExposureVerdict.AcceptableHigh;
        }
    }
}
