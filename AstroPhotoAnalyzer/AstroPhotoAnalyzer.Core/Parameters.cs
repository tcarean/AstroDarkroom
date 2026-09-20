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
// nu stie ce Dmax are un baryta - asta e aici, o singura data.

using System;

namespace AstroPhotoAnalyzer
{
    internal enum PrintMedium
    {
        /// <summary>Hartie baryta lucioasa cu pigment (Harman Gloss Baryta, Photo Rag Baryta). Dmax ~2.0.</summary>
        Baryta,

        /// <summary>Canvas mat, latex sau pigment. Dmax ~1.5: negrul cel mai slab dintre cele trei.</summary>
        Canvas,

        /// <summary>C-print fotografic (Fuji Crystal Archive) sub acril. Dmax ~2.2.</summary>
        Acrylic,
    }

    internal sealed record Parameters
    {
        internal PrintMedium Medium { get; init; } = PrintMedium.Baryta;

        /// <summary>Dmax explicit; bate mediul. Null = din mediu.</summary>
        internal double? DmaxOverride { get; init; }

        internal double Dmax => DmaxOverride ?? DmaxOf(Medium);

        /// <summary>Latimile de print candidate, in cm, pentru tabelul de marimi.</summary>
        internal double[] CandidateWidthsCm { get; init; } = { 50, 60, 70, 80, 90, 100 };

        /// <summary>Distanta de privire, in metri. Null = 1.5 x diagonala imaginii printate.</summary>
        internal double? ViewingDistanceM { get; init; }

        internal double PassepartoutCm { get; init; } = 7;
        internal double FrameCm { get; init; } = 3;

        /// <summary>Raportul de aspect tinta (latime/inaltime). Null = cel nativ, fara decupaj.</summary>
        internal double? TargetAspect { get; init; }

        /// <summary>Fundalul-tinta principal al simularii de print.</summary>
        internal double TargetBackground { get; init; } = 0.19;

        /// <summary>Tintele suplimentare din tabelul simularii, pe langa cea principala.</summary>
        internal double[] SimulationTargets { get; init; } = { 0.15, 0.20 };

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
            PrintMedium.Baryta => 2.0,
            PrintMedium.Canvas => 1.5,
            PrintMedium.Acrylic => 2.2,
            _ => throw new ArgumentOutOfRangeException(nameof(m)),
        };

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
