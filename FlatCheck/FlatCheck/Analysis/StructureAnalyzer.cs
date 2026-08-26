// StructureAnalyzer.cs - modul 1: e master flat-ul simetric radial?
//
// Pur: Img -> StructureResult. Nu scrie nimic, nu citeste fisiere. Textul e in
// Report/StructureReport.cs.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck
{
    internal sealed class StructureResult
    {
        internal int Width, Height;                  // rasterul de analiza (dupa Prepare)
        internal double RawMedian, RawMax;           // pe rasterul original, informativ
        internal double FitCx, FitCy;                // varful de iluminare fitat
        internal double SensorCx, SensorCy;          // centrul senzorului (w/2, h/2)
        internal double CentroidOffsetPct;           // % din semidiagonala
        internal Dictionary<string, double> Corners; // medianele celor 4 colturi
        internal double CornerMean, CentreMedian;
        internal double VignetteStops;               // informativ, NU e un test
        internal double CornerSpreadPct;
        internal double AzimuthalMaxPct;
        internal double ResidualPtpPct;
        internal Img Residual;                       // img / model radial, pentru harta
        internal string CornerVerdict, AzimuthalVerdict, ResidualVerdict, CentroidVerdict;
        internal string Verdict;                     // cel mai prost dintre cele patru
    }

    internal static class StructureAnalyzer
    {
        internal static StructureResult Analyze(Img raw)
        {
            Img img = Img.Prepare(raw);
            int h = img.H, w = img.W;

            // ATENTIE: modelul radial si asimetria se calculeaza in jurul centrului
            // SENZORULUI, nu al varfului fitat. Centrarea pe varf ar absorbi exact
            // gradientul liniar cautat, iar testul s-ar autoanula. FitCenter serveste
            // doar la raportarea offsetului, ca metrica separata.
            double cx = w / 2.0, cy = h / 2.0;
            var (fx, fy) = Metrics.FitCenter(img);
            double off = Math.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy))
                       / Math.Sqrt(cx * cx + cy * cy) * 100;

            var (cor, ctr) = Metrics.CornerStats(img);
            double cmean = cor.Values.Average();
            double vignStops = Math.Log2(ctr / cmean);
            double spread = (cor.Values.Max() - cor.Values.Min()) / cmean * 100;

            var (_, _, model) = Metrics.RadialModel(img, cx, cy);
            var resid = Img.Divide(img, model);
            double rIns = Math.Min(Math.Min(cx, cy), Math.Min(w - cx, h - cy));
            double resPtp = Metrics.LargeScalePtp(resid, cx, cy, rIns) * 100;

            var az = Metrics.AzimuthalAsymmetry(img, cx, cy);
            double azMax = az.Count > 0 ? az.Max(v => v.ptp) : double.NaN;

            string vCorner = Stats.Verdict(spread, Th.CornerOk, Th.CornerBad);
            string vAzim = Stats.Verdict(azMax, Th.AzimOk, Th.AzimBad);
            string vResid = Stats.Verdict(resPtp, Th.AzimOk, Th.AzimBad);
            string vCentroid = Stats.Verdict(off, Th.CentroidOk, Th.CentroidBad);

            return new StructureResult
            {
                Width = w,
                Height = h,
                RawMedian = Stats.Percentile(raw.D.ToList(), 50),
                RawMax = raw.D.Max(),
                FitCx = fx,
                FitCy = fy,
                SensorCx = cx,
                SensorCy = cy,
                CentroidOffsetPct = off,
                Corners = cor,
                CornerMean = cmean,
                CentreMedian = ctr,
                VignetteStops = vignStops,
                CornerSpreadPct = spread,
                AzimuthalMaxPct = azMax,
                ResidualPtpPct = resPtp,
                Residual = resid,
                CornerVerdict = vCorner,
                AzimuthalVerdict = vAzim,
                ResidualVerdict = vResid,
                CentroidVerdict = vCentroid,
                Verdict = new[] { vCorner, vAzim, vResid, vCentroid }
                    .OrderByDescending(Stats.Rank).First(),
            };
        }
    }
}
