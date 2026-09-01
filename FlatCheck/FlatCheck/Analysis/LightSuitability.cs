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

// LightSuitability.cs - poate testul --light sa spuna ceva despre cadrul asta?
//
// Modul --light separa fondul in "plan de poluare luminoasa" + "termeni radiali =
// eroare de flat". Separarea tine cat timp cupola de poluare e cvasi-liniara peste
// cadru. Pe un camp larg ea se CURBEAZA in interiorul cadrului, iar partea radial
// simetrica a acelei curburi are exact forma vignetarii: doua lucruri cu aceeasi forma
// nu se pot separa printr-un fit, oricat de bine scris.
//
// Nu e o limitare a implementarii, e o degenerare geometrica. De aceea peste
// Th.LightFovMax modul REFUZA sa raspunda, in loc sa dea un raspuns prost - exact cazul
// care a motivat fisierul asta: 20 mm pe APS-C (~70 grade diagonala) raporta
// "+3.13 % SUSPECT, SUB-corectie" pe un flat care nu avea nimic radial gresit.
//
// Campul se deriva pe straturi, in ordinea increderii. Ultimul strat conteaza cel mai
// mult ca decizie de proiectare: cand nu se poate afla nimic, modul CONTINUA cu o nota,
// nu refuza. Altfel orice FITS cu header minimal - inclusiv toate fixturile sintetice
// din teste - ar deveni imposibil de analizat.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace FlatCheck
{
    internal enum FovLevel
    {
        Ok,          // camp ingust: separarea plan/radial e valida
        Marginal,    // se poate rula, dar cu avertisment
        TooWide,     // degenerare geometrica: testul nu se aplica
        Unknown,     // headerul nu spune destul; se ruleaza, cu o nota
    }

    internal sealed class SuitabilityResult
    {
        internal FovLevel Level;
        internal double FovDeg = double.NaN;    // diagonala, grade. NaN = nu s-a calculat
        internal double FocalMm = double.NaN;   // doar cand decizia s-a luat pe focala
        internal string Source;                 // de unde vine cifra, pentru raport

        internal bool CanRun => Level != FovLevel.TooWide;
    }

    internal static class LightSuitability
    {
        internal static SuitabilityResult Check(
            IReadOnlyDictionary<string, string> header, double? fovOverride)
        {
            // 1. Declarat de om. Bate orice autodetectie - exista tocmai pentru cazul in
            //    care straturile de mai jos nu pot sti (DSLR fara XPIXSZ, senzor mic).
            if (fovOverride.HasValue)
                return FromFov(fovOverride.Value, "--fov");

            if (header == null) return Unknown();

            int n1 = FitsReader.HInt(header, "NAXIS1", 0);
            int n2 = FitsReader.HInt(header, "NAXIS2", 0);
            if (n1 <= 0 || n2 <= 0) return Unknown();
            double diagPx = Math.Sqrt((double)n1 * n1 + (double)n2 * n2);

            // 2. Plate solve: scara in grade/pixel e masurata, nu dedusa. Cea mai buna
            //    sursa cand exista. CDELT1 daca e prezent, altfel norma coloanei matricei
            //    CD (CD1_1, CD2_1), care e acelasi lucru scris altfel.
            double scale = Math.Abs(FitsReader.HDbl(header, "CDELT1", 0.0));
            if (scale <= 0)
            {
                double cd11 = FitsReader.HDbl(header, "CD1_1", 0.0);
                double cd21 = FitsReader.HDbl(header, "CD2_1", 0.0);
                if (cd11 != 0 || cd21 != 0) scale = Math.Sqrt(cd11 * cd11 + cd21 * cd21);
            }
            if (scale > 0) return FromFov(diagPx * scale, "WCS");

            double focal = FitsReader.HDbl(header, "FOCALLEN", 0.0);
            if (focal <= 0) return Unknown();

            // 3. Marimea pixelului + focala: campul real, exact. Siril scrie XPIXSZ pentru
            //    camere astro. Pentru DSLR-uri nu il scrie, deci stratul asta sare des.
            double xpix = FitsReader.HDbl(header, "XPIXSZ", 0.0);
            double ypix = FitsReader.HDbl(header, "YPIXSZ", xpix);
            if (xpix > 0 && ypix > 0)
            {
                double xbin = Math.Max(1, FitsReader.HInt(header, "XBINNING", 1));
                double ybin = Math.Max(1, FitsReader.HInt(header, "YBINNING", 1));
                double wMm = n1 * xbin * xpix / 1000.0;
                double hMm = n2 * ybin * ypix / 1000.0;
                double diagMm = Math.Sqrt(wMm * wMm + hMm * hMm);
                double fov = 2 * Math.Atan(diagMm / (2 * focal)) * 180.0 / Math.PI;
                return FromFov(fov, $"FOCALLEN={Num(focal)}mm + XPIXSZ");
            }

            // 4. Doar focala. Nu se poate calcula campul - depinde de senzor - deci NU se
            //    raporteaza o cifra de camp, ca ar fi inventata. Se compara focala cu
            //    pragurile calculate pentru cel mai mare senzor plauzibil (full-frame),
            //    deci judecata e conservatoare: pe un senzor mai mic campul real e mai
            //    ingust decat presupune pragul. Pentru cazul ala exista --fov.
            var level = focal >= Th.LightFocalOk ? FovLevel.Ok
                      : focal >= Th.LightFocalMin ? FovLevel.Marginal
                      : FovLevel.TooWide;
            return new SuitabilityResult
            {
                Level = level,
                FocalMm = focal,
                Source = $"FOCALLEN={Num(focal)}mm, fara marimea pixelului",
            };
        }

        static SuitabilityResult FromFov(double fovDeg, string source) => new()
        {
            Level = fovDeg < Th.LightFovOk ? FovLevel.Ok
                  : fovDeg < Th.LightFovMax ? FovLevel.Marginal
                  : FovLevel.TooWide,
            FovDeg = fovDeg,
            Source = source,
        };

        static SuitabilityResult Unknown() => new() { Level = FovLevel.Unknown };

        /// Focala fara zecimale inutile: "20mm", nu "20.0mm".
        static string Num(double v)
            => v.ToString(Math.Abs(v - Math.Round(v)) < 1e-9 ? "0" : "0.0",
                          CultureInfo.InvariantCulture);
    }
}
