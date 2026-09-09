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

// LightSeriesAnalyzer.cs - modul 3 pe o serie intreaga de cadre.
//
// DE CE UN CADRU SINGUR NU AJUNGE. Componenta radiala raportata de --light e suma a
// doua lucruri: eroarea de flat, care e ACEEASI pe toate cadrele prin constructie
// (acelasi master flat s-a impartit din fiecare), si un termen radial al cerului, care
// se schimba in timpul sesiunii. Pe o serie reala (32 de cadre, 81 de minute, Calea
// Lactee la 20 mm) cifra a baleiat neted de la +3.13 % prin -19.20 % pana la +35.73 %,
// cu doar 1.36 puncte intre cadre consecutive. Tot ce se misca asa e cer.
//
// Media pe serie estimeaza eroarea de flat DOAR daca termenul de cer se mediaza la
// zero. Asta nu e garantat, deci nu se presupune - se verifica, prin trei conditii, si
// daca vreuna pica verdictul e NEDETERMINAT in loc de un numar care arata increzator.
//
// A doua conditie e cea interesanta: daca "eroarea de flat" URMARESTE gradientul de
// cer peste serie, atunci ce se masoara e cerul, fiindca flat-ul nu are cum sa se
// schimbe de la un cadru la altul. Pe seria de mai sus corelatia a iesit -0.94.
//
// CE NU FACE: nu incearca sa extrapoleze la gradient de cer zero. Regresia radPct ~
// planePct pe seria reala da intercept +122 %, o extrapolare mult in afara datelor
// (gradientul observat sta intre 49 si 88 %). Corelatia e buna ca diagnostic si inutila
// ca estimator; nu o transforma in unul.
//
// Combinarea e pura: primeste rezultate deja calculate, nu atinge discul. Incarcarea
// fisierelor sta in FlatCheckCli, ca la toate celelalte moduri.

using System;
using System.Collections.Generic;
using System.Linq;

namespace FlatCheck
{
    internal sealed class LightSeriesResult
    {
        internal string[] Names;
        internal LightResult[] Frames;

        internal double MeanRad, MinRad, MaxRad, StdDev, Sem;
        internal double SkyCorr = double.NaN;   // NaN cand nu se poate calcula
        internal double SkySpan;                // intervalul gradientului de cer, puncte
        internal bool CorrApplies;              // gradientul variaza destul cat sa conteze

        internal Img RadialMap;                 // harta radiala medie peste cadre
        internal string Verdict;
        internal string[] Reasons;              // gol cand verdictul e concludent

        internal int Count => Frames.Length;
        internal bool Conclusive => Reasons.Length == 0;
    }

    internal static class LightSeriesAnalyzer
    {
        internal const string Undetermined = "NEDETERMINAT";

        internal static LightSeriesResult Combine(IReadOnlyList<(string Name, LightResult R)> frames)
        {
            if (frames == null || frames.Count == 0)
                throw new Exception("Serie goala: niciun cadru de analizat.");

            var names = frames.Select(f => f.Name).ToArray();
            var results = frames.Select(f => f.R).ToArray();
            var rad = results.Select(r => r.RadPct).ToArray();
            var plane = results.Select(r => r.PlanePct).ToArray();
            int n = rad.Length;

            double mean = rad.Average();
            double std = StdDev(rad, mean);
            double sem = n >= 2 ? std / Math.Sqrt(n) : double.NaN;

            double skySpan = plane.Max() - plane.Min();
            bool corrApplies = skySpan > Th.LightSeriesSkySpanMin;
            double corr = Pearson(rad, plane);

            // Verdictul se da doar daca TOATE conditiile trec. Comparatiile sunt scrise
            // ca !(x < prag) si nu ca (x >= prag) intentionat: asa NaN cade pe partea
            // proasta, la fel ca in Stats.Verdict. Un sem care iese NaN inseamna ca nu
            // s-a putut calcula imprastierea, ceea ce e un motiv de NEDETERMINAT, nu de
            // trecere tacuta.
            var reasons = new List<string>();
            if (n < Th.LightSeriesMinFrames)
                reasons.Add($"prea putine cadre ({n}, minim {Th.LightSeriesMinFrames})");
            if (!(sem < Th.LightSeriesSemMax))
                reasons.Add("sem");
            if (corrApplies && !(Math.Abs(corr) < Th.LightSeriesSkyCorr))
                reasons.Add("corelatie");

            return new LightSeriesResult
            {
                Names = names,
                Frames = results,
                MeanRad = mean,
                MinRad = rad.Min(),
                MaxRad = rad.Max(),
                StdDev = std,
                Sem = sem,
                SkyCorr = corr,
                SkySpan = skySpan,
                CorrApplies = corrApplies,
                RadialMap = MeanMap(results),
                Reasons = reasons.ToArray(),
                Verdict = reasons.Count > 0
                    ? Undetermined
                    : Stats.Verdict(Math.Abs(mean), Th.LightRadOk, Th.LightRadBad),
            };
        }

        /// Deviatia standard de esantion (ddof = 1), ca numpy.std(ddof=1).
        internal static double StdDev(double[] v, double mean)
        {
            if (v.Length < 2) return double.NaN;
            double s = 0;
            foreach (double x in v) s += (x - mean) * (x - mean);
            return Math.Sqrt(s / (v.Length - 1));
        }

        /// Corelatia Pearson. NaN daca vreuna din serii e constanta - caz in care
        /// intrebarea "se misca impreuna?" nu are raspuns, nu are raspunsul "nu".
        internal static double Pearson(double[] a, double[] b)
        {
            int n = a.Length;
            if (n < 2 || b.Length != n) return double.NaN;
            double ma = a.Average(), mb = b.Average();
            double sab = 0, saa = 0, sbb = 0;
            for (int i = 0; i < n; i++)
            {
                double da = a[i] - ma, db = b[i] - mb;
                sab += da * db; saa += da * da; sbb += db * db;
            }
            double den = Math.Sqrt(saa * sbb);
            return den <= 0 ? double.NaN : sab / den;
        }

        /// Harta radiala medie peste cadre. Toate trebuie sa aiba aceeasi grila de
        /// tile-uri; altfel media nu are sens si e mai bine sa se opreasca aici decat sa
        /// produca o harta amestecata.
        static Img MeanMap(LightResult[] results)
        {
            var first = results[0].RadialMap;
            foreach (var r in results)
                if (r.RadialMap.H != first.H || r.RadialMap.W != first.W)
                    throw new Exception(
                        "Cadrele din serie nu au aceeasi dimensiune - nu pot fi mediate. "
                        + "Foloseste doar cadre din aceeasi sesiune, necropate.");

            var o = new Img(first.H, first.W);
            foreach (var r in results)
                for (int i = 0; i < o.D.Length; i++) o.D[i] += r.RadialMap.D[i];
            for (int i = 0; i < o.D.Length; i++) o.D[i] /= results.Length;
            return o;
        }
    }
}
