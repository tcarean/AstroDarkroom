// RatioAnalyzer.cs - modul 2: testul de rotatie 180, care valideaza SETUP-ul.
//
// Vignetarea si praful sunt fixe pe senzor si se anuleaza in A/B; un gradient extern
// e fix in lume, deci se inverseaza si apare in raport la ~2x amplitudine.

using System;
using System.Linq;

namespace FlatCheck
{
    internal sealed class RatioResult
    {
        internal int Width, Height;
        internal double PtpPct;              // structura ramasa in A/B
        internal double CornerSpreadPct;
        internal Img Ratio;                  // A/B normalizat la mediana 1
        internal string PtpVerdict, CornerVerdict;
        internal string Verdict;
    }

    internal static class RatioAnalyzer
    {
        internal static RatioResult Analyze(Img rawA, Img rawB)
        {
            Img a = Img.Prepare(rawA);
            Img b = Img.Prepare(rawB);
            if (a.H != b.H || a.W != b.W)
                throw new Exception("Cele doua imagini au dimensiuni diferite.");

            var ratio = Img.Divide(a, b);
            double med = Stats.Percentile(ratio.D.ToList(), 50);
            for (int i = 0; i < ratio.D.Length; i++) ratio.D[i] /= med;
            int h = ratio.H, w = ratio.W;

            // raza inscrisa = diagonala intreaga, adica fara mascare: raportul ar trebui
            // sa fie featureless peste tot, nu doar in cercul inscris.
            double ptp = Metrics.LargeScalePtp(ratio, w / 2.0, h / 2.0,
                            Math.Sqrt((double)w * w + (double)h * h)) * 100;
            var (cor, _) = Metrics.CornerStats(ratio);
            double cmean = cor.Values.Average();
            double spread = (cor.Values.Max() - cor.Values.Min()) / cmean * 100;

            string vPtp = Stats.Verdict(ptp, Th.RatioOk, Th.RatioBad);
            string vCorner = Stats.Verdict(spread, Th.RatioOk, Th.RatioBad);

            return new RatioResult
            {
                Width = w,
                Height = h,
                PtpPct = ptp,
                CornerSpreadPct = spread,
                Ratio = ratio,
                PtpVerdict = vPtp,
                CornerVerdict = vCorner,
                Verdict = new[] { vPtp, vCorner }.OrderByDescending(Stats.Rank).First(),
            };
        }
    }
}
