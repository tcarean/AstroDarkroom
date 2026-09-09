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

// RatioAnalyzer.cs - modul 2: ATRIBUIRE prin rotatie la 180 de grade.
//
// CE INTREABA MODUL, si de ce s-a schimbat intrebarea. Pana la rescrierea asta modul
// raspundea la "e setup-ul de flats curat?" si dadea OK / SUSPECT / PROBLEMA. Nu putea.
// Masurat pe trei perechi reale de mastere, cu geometrie cunoscuta cu certitudine:
//
//   - doua stive rotite corect, cu un gradient real de cer -> SUSPECT, "repara sursa".
//     Dar gradientul fusese deja anulat din master prin chiar rotatia aia. Corect ca
//     masuratoare, neactionabil ca sfat.
//   - doua stive rotite corect, dintr-o sesiune al carei master are 11% de structura
//     ne-radiala -> OK, "poti construi biblioteca pe el". Defectul era fix pe aparat,
//     iar la ce e fix pe aparat testul asta e orb prin constructie.
//   - doua stive din ACEEASI orientare, deci fara niciun test efectuat -> tot OK,
//     "poti construi biblioteca pe el".
//
// Doua verzi false din trei. Cauza nu e calibrarea pragurilor: doua stive din aceeasi
// orientare si doua stive rotite dintr-un setup cu adevarat curat produc date statistic
// IDENTICE - in ambele cazuri A/B e numai zgomot. Informatia care le-ar deosebi nu e in
// pixeli, deci niciun prag nu o poate scoate.
//
// Ce se poate insa calcula din cele doua fisiere, si ce nici un alt mod nu poate:
//
//     S = (A + B) / 2     partea fixa pe APARAT - gradientul extern se anuleaza,
//                         fiindca in B e inversat. Asta E masterul pe care il obtii
//                         stivuind ambele orientari, deci verdictul lui e verdictul
//                         flat-ului tau. (Verificat: 4.02% calculat aici vs 4.00%
//                         masurat pe pp_flat_stacked.fit cu toate cele 30 de cadre.)
//
//     W = A / B           partea fixa in LUME, la amplitudine dubla. Informativa: spune
//                         cat ti-a scos rotatia din master, nu daca setup-ul e bun.
//
// Deci modul nu mai da verdict despre setup. Da verdictul de STRUCTURA pe S - aceeasi
// baterie ca modul 1, refolosita intreaga - si raporteaza W ca informatie.
//
// BARA DE EROARE, si de ce nu e calibrata. log(A/B) trebuie sa fie exact antisimetric la
// o rotatie de 180: o contaminare fixa in lume intra INTEGRAL in partea impara si ZERO
// in cea para. Zgomotul se imparte in doua cu varianta egala. Deci partea para e un
// estimator nedeplasat al zgomotului din partea impara, construit din aceleasi date,
// fara nicio presupunere si fara nicio constanta de reglat. Validat pe o pereche fara
// rotatie, unde raportul impar/par a iesit 1.17x, fata de 2.59x pe una rotita.
//
// CE NU POATE MODUL, si trebuie spus in raport fiindca nu se vede din cifre:
//   - nu poate verifica din pixeli ca ai rotit intre cele doua stive;
//   - nu poate spune daca partea fixa pe aparat e obiectivul (inofensiva, se imparte
//     corect din lights) sau ceva PE obiectiv - curea, condens, parasolara stramba -
//     care se roteste odata cu aparatul si arata identic, dar la lights nu mai e acolo.
//
// Pur: Img x Img -> RatioResult. Textul e in Report/RatioReport.cs.

using System;
using System.Linq;

namespace FlatCheck
{
    internal sealed class RatioResult
    {
        /// <summary>
        /// Structura masterului combinat (A+B)/2, adica a partii fixe pe aparat.
        /// Verdictul modului vine de aici.
        /// </summary>
        internal StructureResult Sensor;

        internal double WorldPtpPct;      // gradient extern PER ORIENTARE (impar / 2)
        internal double WorldErrPct;      // bara lui de eroare (par / 2)
        internal double WorldCornerPct;   // imprastierea colturilor lui W, per orientare
        internal string WorldLabel;       // eticheta informativa pe Th.RatioOk/Bad

        internal Img World;               // A/B normalizat la mediana 1, pentru harta

        // Metadate din headerele FITS, umplute de RatioRun. 0 / null = necunoscut.
        // Nu sunt masuratori - sunt singurul loc din raport in care se vede daca cele
        // doua stive chiar pot fi din doua orientari diferite.
        internal int StackCountA, StackCountB;
        internal string DateObsA, DateObsB;

        internal string Verdict => Sensor.Verdict;
    }

    internal static class RatioAnalyzer
    {
        internal static RatioResult Analyze(Img rawA, Img rawB)
        {
            Img a = Img.Prepare(rawA);
            Img b = Img.Prepare(rawB);
            if (a.H != b.H || a.W != b.W)
                throw new Exception("Cele doua imagini au dimensiuni diferite.");

            // Normalizarea fiecareia la mediana 1 inainte de mediere nu e cosmetica: e
            // ce face Siril cu "multiplicative normalized input". Fara ea, pe un cer de
            // amurg care scade 12% intre cele doua stive, media ar cantari A mai greu
            // decat B si gradientul nu s-ar mai anula complet.
            Normalise(a);
            Normalise(b);

            // ---- partea fixa pe aparat: media celor doua orientari ----
            var s = new Img(a.H, a.W);
            for (int i = 0; i < s.D.Length; i++) s.D[i] = (a.D[i] + b.D[i]) / 2;

            // Deja pregatit; Prepare fiind liniara, media rasterelor pregatite e acelasi
            // lucru cu pregatirea mediei, dar la a treizecea parte din memorie.
            var sensor = StructureAnalyzer.AnalyzePrepared(s);

            // ---- partea fixa in lume: raportul ----
            Img world = Img.Divide(a, b);
            Normalise(world);

            var (odd, even) = SplitByRotation(world);

            // Impartit la 2: W poarta contaminarea la amplitudine dubla, fiindca in B e
            // inversata. Cifrele raportate sunt per orientare, adica cat e in fiecare
            // master luat separat.
            double worldPtp = Ptp(odd) / 2 * 100;
            double worldErr = Ptp(even) / 2 * 100;

            var (cor, _) = Metrics.CornerStats(world);
            double cmean = cor.Values.Average();
            double worldCorner = (cor.Values.Max() - cor.Values.Min()) / cmean * 100 / 2;

            return new RatioResult
            {
                Sensor = sensor,
                WorldPtpPct = worldPtp,
                WorldErrPct = worldErr,
                WorldCornerPct = worldCorner,
                WorldLabel = Stats.Verdict(worldPtp, Th.RatioOk, Th.RatioBad),
                World = world,
            };
        }

        static void Normalise(Img img)
        {
            double med = Stats.Percentile(img.D, 50);
            if (med <= 0) return;
            for (int i = 0; i < img.D.Length; i++) img.D[i] /= med;
        }

        /// <summary>
        /// Descompune log(W) in partea care se inverseaza la rotatia de 180 si cea care
        /// nu se inverseaza. Pe grila de dale, nu pe pixeli: fara medierea pe dale ambele
        /// jumatati ar fi dominate de zgomotul de fotoni, care se imparte egal, si
        /// raportul lor ar fi mereu 1 indiferent de semnal.
        ///
        /// In log, nu pe raportul direct: x si 1/x nu sunt antisimetrice, doar log-ul e.
        /// La amplitudinile astea diferenta e de ordinul a 0.02%, dar log-ul e cel corect
        /// si nu costa nimic.
        /// </summary>
        static (Img odd, Img even) SplitByRotation(Img world)
        {
            var log = new Img(world.H, world.W);
            for (int i = 0; i < log.D.Length; i++)
                // Prag pe ambele capete: Divide taie doar numitorul, deci un pixel nul
                // la numarator ar da un log salbatic care ar trage media dalei lui.
                log.D[i] = Math.Log(Math.Min(Math.Max(world.D[i], 0.01), 100.0));

            // Taiere CENTRATA inainte de dale, ca rotatia de 180 sa cada exact peste
            // grila. BlockMean arunca blocurile partiale doar de la capat - vezi
            // Img.CropCentred pentru cat costa daca nu se corecteaza.
            int k = Metrics.TileSize(log);
            Img t = Img.BlockMean(Img.CropCentred(log, log.H / k * k, log.W / k * k), k);
            Img r = Img.Rotate180(t);

            var odd = new Img(t.H, t.W);
            var even = new Img(t.H, t.W);
            for (int i = 0; i < t.D.Length; i++)
            {
                odd.D[i] = (t.D[i] - r.D[i]) / 2;
                even.D[i] = (t.D[i] + r.D[i]) / 2;
            }
            return (odd, even);
        }

        static double Ptp(Img t)
            => t.D.Length < 10
                ? double.NaN
                : Stats.Percentile(t.D, 95) - Stats.Percentile(t.D, 5);
    }
}
