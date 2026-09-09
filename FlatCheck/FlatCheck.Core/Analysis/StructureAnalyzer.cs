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
        internal double CentroidOffsetPct;           // % din semidiagonala, INFORMATIV
        internal bool CentroidValid;                 // false => varful fitat nu e utilizabil
        internal double TiltPct;                     // amplitudinea termenului liniar, %
        internal Dictionary<string, double> Corners; // medianele celor 4 colturi
        internal double CornerMean, CentreMedian;
        internal double VignetteStops;               // informativ, NU e un test
        internal double CornerSpreadPct;
        internal double AzimuthalMaxPct;
        internal double ResidualPtpPct;
        internal Img Residual;                       // img / model radial, pentru harta
        internal string CornerVerdict, AzimuthalVerdict, ResidualVerdict, TiltVerdict;
        internal string Verdict;                     // cel mai prost dintre cele patru
    }

    internal static class StructureAnalyzer
    {
        internal static StructureResult Analyze(Img raw) => Run(raw, Img.Prepare(raw));

        /// <summary>
        /// Aceeasi analiza, pe un raster deja trecut prin <see cref="Img.Prepare"/>.
        ///
        /// Exista pentru modul de atribuire, care isi construieste intrarea - media
        /// celor doua orientari - si ar plati de doua ori memoria daca ar trebui sa o
        /// tina la rezolutie intreaga. Prepare fiind numai medii de blocuri, e liniara:
        /// media rasterelor pregatite e acelasi lucru cu pregatirea mediei, la o
        /// treizecime din memorie.
        ///
        /// RawMedian/RawMax ies atunci de pe rasterul pregatit si normalizat, deci nu
        /// mai inseamna ADU. Nu se tiparesc: raportul de atribuire cheama
        /// StructureReport.WriteBody, care nu include randul acela.
        /// </summary>
        internal static StructureResult AnalyzePrepared(Img prepared) => Run(prepared, prepared);

        static StructureResult Run(Img raw, Img img)
        {
            int h = img.H, w = img.W;

            // ATENTIE: modelul radial si asimetria se calculeaza in jurul centrului
            // SENZORULUI, nu al varfului fitat. Centrarea pe varf ar absorbi exact
            // gradientul liniar cautat, iar testul s-ar autoanula.
            double cx = w / 2.0, cy = h / 2.0;
            // Din acelasi fit ies doua lucruri: inclinarea, care e un test, si offsetul
            // varfului, care e doar informativ - nu e scale-invariant, si peste un prag
            // nici macar monoton. Vezi Metrics.FitCenter pentru de ce.
            var (fx, fy, tilt, centroidValid) = Metrics.FitCenter(img);
            double off = Math.Sqrt((fx - cx) * (fx - cx) + (fy - cy) * (fy - cy))
                       / Math.Sqrt(cx * cx + cy * cy) * 100;

            var (cor, ctr) = Metrics.CornerStats(img);
            double cmean = cor.Values.Average();
            double vignStops = Math.Log2(ctr / cmean);
            double spread = (cor.Values.Max() - cor.Values.Min()) / cmean * 100;

            // Profilul se ia din rasterul SIMETRIZAT la 180 de grade, nu din img: altfel
            // mediana pe inel sare dincolo de cercul inscris si harta capata inele
            // concentrice care nu sunt in flat. Vezi Img.Symmetrise pentru mecanism.
            // Reziduul ramane insa al lui img - simetrizarea ar anula tocmai gradientul
            // pe care harta trebuie sa-l arate.
            var (_, _, model) = Metrics.RadialModel(Img.Symmetrise(img), cx, cy);
            var resid = Img.Divide(img, model);
            double rIns = Math.Min(Math.Min(cx, cy), Math.Min(w - cx, h - cy));
            double resPtp = Metrics.LargeScalePtp(resid, cx, cy, rIns) * 100;

            var az = Metrics.AzimuthalAsymmetry(img, cx, cy);
            double azMax = az.Count > 0 ? az.Max(v => v.ptp) : double.NaN;

            // Toate patru masoara aceeasi abatere de la simetria radiala, din unghiuri
            // diferite, si toate patru sunt amplitudini fractionare in procente - deci
            // impart scara de reziduu. Offsetul centrului masoara altceva, o pozitie, si
            // nu mai da verdict deloc: e raportat informativ.
            string vCorner = Stats.ResidualVerdict(spread);
            string vAzim = Stats.ResidualVerdict(azMax);
            string vResid = Stats.ResidualVerdict(resPtp);
            string vTilt = Stats.ResidualVerdict(tilt);

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
                CentroidValid = centroidValid,
                TiltPct = tilt,
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
                TiltVerdict = vTilt,
                // verdictul general e cel mai prost dintre cele patru, exprimat in
                // vocabularul benzilor de reziduu
                Verdict = Stats.ResidualBands[
                    new[] { vCorner, vAzim, vResid, vTilt }.Max(Stats.Severity)],
            };
        }
    }
}
