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

            // Toate trei masoara aceeasi abatere de la simetria radiala, deci impart
            // scara de reziduu. Offsetul centrului masoara altceva - o pozitie - si
            // ramane pe OK/SUSPECT/PROBLEMA.
            string vCorner = Stats.ResidualVerdict(spread);
            string vAzim = Stats.ResidualVerdict(azMax);
            string vResid = Stats.ResidualVerdict(resPtp);
            string vCentroid = Stats.Verdict(off, Th.CentroidOk, Th.CentroidBad);

            return new StructureResult
            {
                Width = w,
                Height = h,
                RawMedian = Stats.Percentile(raw.D, 50),
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
                // verdictul general e cel mai prost dintre cele patru, exprimat in
                // vocabularul benzilor de reziduu
                Verdict = Stats.ResidualBands[
                    new[] { vCorner, vAzim, vResid, vCentroid }.Max(Stats.Severity)],
            };
        }
    }
}
