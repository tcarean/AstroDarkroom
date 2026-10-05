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

// Parameters.cs - ce alege omul: mediul de print, marimile candidate, distanta de
// privire, rama. Cu implicitele din conversatia care a nascut unealta.
//
// Consola si fereastra construiesc amandoua un Parameters si il dau nucleului; niciuna
// nu stie ce Dmax are un baryta - asta e aici, o singura data. De unde vine fiecare
// Dmax: ADR 0028.

using System;
using System.Collections.Generic;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    internal enum PrintMedium
    {
        /// <summary>
        /// Hartie baryta lucioasa cu pigment (Harman Gloss Baryta, Photo Rag Baryta). Dmax 2.4:
        /// masurat, 2.41 fata de albul hartiei in profilul ICC al atelierului Chromatone
        /// pentru Hahnemuhle Photo Rag Baryta pe Canon iPF8300 (ADR 0028).
        /// </summary>
        Baryta,

        /// <summary>
        /// Canvas mat, latex sau pigment. Dmax 1.5, negrul cel mai slab dintre cele trei:
        /// regula din conversatia care a nascut unealta, sprijinita de profilul ICC Harman
        /// Canvas (1.49 fata de albul panzei; ADR 0028).
        /// </summary>
        Canvas,

        /// <summary>
        /// C-print fotografic (Fuji Crystal Archive) sub acril. Dmax 2.2: regula din
        /// conversatia care a nascut unealta, fara o masuratoare in spate (ADR 0028).
        /// </summary>
        Acrylic,
    }

    internal sealed record Parameters
    {
        internal PrintMedium Medium { get; init; } = PrintMedium.Baryta;

        /// <summary>Dmax explicit; bate mediul. Null = din mediu.</summary>
        internal double? DmaxOverride { get; init; }

        internal double Dmax => DmaxOverride ?? DmaxOf(Medium);

        /// <summary>
        /// Dmax-ul e al omului, nu al mediului: un Dmax dat care e chiar cel al mediului nu
        /// schimba nimic si nu se anunta "(explicit)" - fereastra isi arata campul completat
        /// cu Dmax-ul mediului ales, si un raport care l-ar numi explicit ar minti.
        /// </summary>
        internal bool DmaxIsExplicit => DmaxOverride.HasValue && Math.Abs(DmaxOverride.Value - DmaxOf(Medium)) > 1e-9;

        /// <summary>
        /// Numele profilului ICC din care a fost citit Dmax-ul explicit (R-028); null cand
        /// Dmax-ul e al mediului sau scris de om. Doar pentru raport: spune de unde vine cifra.
        /// </summary>
        internal string DmaxProfile { get; init; }

        /// <summary>
        /// Aceiasi parametri, cu Dmax-ul citit din profilul ICC de imprimanta de la
        /// <paramref name="path"/>: negrul dispozitivului fata de albul hartiei, la doua
        /// zecimale (IccPaperBlack). Null, cu motivul in <paramref name="error"/>, cand
        /// profilul nu se poate citi sau nu e al unei imprimante.
        /// </summary>
        internal Parameters WithDmaxFromProfile(string path, out string error)
        {
            IccBlack black = IccPaperBlack.Read(path);
            error = black.Problem;
            return black.Readable ? this with { DmaxOverride = black.Dmax, DmaxProfile = black.Description } : null;
        }

        /// <summary>Latimile de print candidate, in cm, pentru tabelul de marimi.</summary>
        internal double[] CandidateWidthsCm { get; init; } = { 50, 60, 70, 80, 90, 100 };

        /// <summary>Distanta de privire, in metri. Null = 1.5 x diagonala imaginii printate.</summary>
        internal double? ViewingDistanceM { get; init; }

        internal double PassepartoutCm { get; init; } = 7;
        internal double FrameCm { get; init; } = 3;

        /// <summary>Raportul de aspect tinta (latime/inaltime). Null = cel nativ, fara decupaj.</summary>
        internal double? TargetAspect { get; init; }

        /// <summary>
        /// Fundalul-tinta dat de om, optional: adauga randul lui in tabelul simularii si
        /// alege dala de print din Preview. Null = fara rand in plus; Preview-ul ia randul
        /// recomandat (R-023).
        /// </summary>
        internal double? TargetBackground { get; init; }

        /// <summary>
        /// Grila simularii: 0.10 .. 0.20 din 0.01 in 0.01, nivelurile la care se tipareste un
        /// cer de noapte (peste 0.20 un gri spalat). Podeaua de 0.10 e a simularii, nu a
        /// hartiei: era negrul unei hartii de Dmax 2.0 (L* 9), iar pe baryta de 2.4 praful
        /// poate cere mai putin - atunci recomandarea se opreste la 0.10 si raportul spune ca
        /// grila nu coboara mai jos (ADR 0028). Nu e expusa; o tinta in afara ei intra prin
        /// TargetBackground.
        /// </summary>
        internal double[] SimulationTargets { get; init; } = DefaultSimulationTargets();

        static double[] DefaultSimulationTargets()
        {
            var t = new double[11];
            for (int i = 0; i < t.Length; i++) t[i] = Math.Round(0.10 + 0.01 * i, 2);
            return t;
        }

        /// <summary>
        /// Un FITS al carui WCS se imprumuta imaginii analizate (R-027): un TIFF sau PNG
        /// exportat din acelasi stack nu poarta antetul, dar are aceeasi geometrie. Null = WCS-ul
        /// fisierului insusi, daca are.
        /// </summary>
        internal string WcsPath { get; init; }

        /// <summary>Samanta alegerii de dale. Fixa, ca doua rulari pe acelasi fisier sa dea aceleasi cifre.</summary>
        internal int Seed { get; init; } = 0;

        /// <summary>
        /// Regulile care leaga doi parametri intre ei - ce nu poate verifica un camp
        /// singur. Intoarce mesajul pentru om, sau null cand totul e in regula. Consola
        /// si fereastra il cheama amandoua, ca sa refuze aceleasi lucruri cu aceleasi
        /// cuvinte.
        /// </summary>
        internal string Validate()
        {
            double minPassepartout = Thresholds.PassepartoutMinFrameRatio * FrameCm;
            if (PassepartoutCm > 0 && PassepartoutCm < minPassepartout)
                return $"passepartout-ul ({Fmt.F1(PassepartoutCm)} cm) e sub dublul latimii ramei ({Fmt.F1(FrameCm)} cm): "
                     + $"sub {Fmt.F0(Thresholds.PassepartoutMinFrameRatio)} x rama, rama strange imaginea. "
                     + $"Mareste passepartout-ul la cel putin {Fmt.F1(minPassepartout)} cm, ia o rama mai ingusta, sau pune 0 pentru fara passepartout.";
            return null;
        }

        internal static double DmaxOf(PrintMedium m) => m switch
        {
            PrintMedium.Baryta => 2.4,
            PrintMedium.Canvas => 1.5,
            PrintMedium.Acrylic => 2.2,
            _ => throw new ArgumentOutOfRangeException(nameof(m)),
        };

        /// <summary>
        /// Numele tuturor mediilor, in ordinea enum-ului: lista din care consola isi scrie
        /// --help-ul si eroarea de mediu necunoscut, iar fereastra isi umple combo-ul.
        /// </summary>
        internal static IReadOnlyList<string> MediumNames { get; }
            = ((PrintMedium[])Enum.GetValues(typeof(PrintMedium))).Select(NameOf).ToArray();

        /// <summary>Numele din linia de comanda si din fereastra: baryta, canvas, acrilic.</summary>
        internal static PrintMedium MediumFromName(string name) => name?.ToLowerInvariant() switch
        {
            "baryta" => PrintMedium.Baryta,
            "canvas" => PrintMedium.Canvas,
            "acrilic" => PrintMedium.Acrylic,
            _ => throw new ArgumentException("mediu necunoscut: " + name),
        };

        internal static string NameOf(PrintMedium m) => m switch
        {
            PrintMedium.Baryta => "baryta",
            PrintMedium.Canvas => "canvas",
            PrintMedium.Acrylic => "acrilic",
            _ => throw new ArgumentOutOfRangeException(nameof(m)),
        };
    }
}
