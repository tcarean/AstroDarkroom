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

// Modelul de vignetare al unui profil de obiectiv, ca valoare: factorul V(x, y) cu care
// obiectivul intuneca un punct al senzorului fata de axa. Doua conventii de coordonate,
// amandoua verificate din surse inainte de a fi scrise aici (2026-09-17):
//
//  - Adobe (LCP): V = 1 + a1 r^2 + a2 r^4 + a3 r^6, cu r^2 = xd^2 + yd^2 si
//    xd = (u - u0) / fx, yd = (v - v0) / fy - pixelul minus punctul principal, impartit la
//    focala in pixeli ("Adobe Camera Model", sectiunea Vignette Model, si descriptorii de
//    la pagina 18: FocalLengthX = fx / Dmax, ImageXCenter = u0 / Dmax, Dmax = latura lunga
//    a imaginii in pixeli; cand FocalLengthX lipseste, focala e cea nominala in mm, si cum
//    latura lunga a senzorului e 36 / SensorFormatFactor mm, fx / Dmax = f * SFF / 36).
//    Pe scurt: r = distanta in mm / focala in mm. Verificat pe profilul Tokinei 11-20:
//    tabelele "piecewise" din acelasi fisier au ultimul punct la r = 0.7094, adica exact
//    coltul DX (14.1 mm / 20 mm = 0.705), si acolo tabelul da 0.603 iar polinomul 0.597.
//    "Note that these model parameters are identical for all color channels" - modelul e
//    acromatic, si raportul o spune.
//
//    Distanta in mm vine din crop-ul APARATULUI TAU (latura lunga = 36 / crop), nu din
//    SFF-ul profilului: SFF-ul e senzorul pe care Adobe a calibrat profilul, si prima
//    versiune il lua drept senzorul tuturor. Pe D3300 cu profilul DX coincideau la 1.5 %;
//    pe un full frame cu un profil calibrat pe DX, sau invers, coltul ar fi cazut la raza
//    gresita fara niciun avertisment. Focala in pixeli a profilului (FocalLengthX) e in
//    unitati de Dmax ale aparatului de calibrare, deci se trece prin mm (FocalX * 36 / SFF),
//    iar descentrarea (ImageXCenter) e a obiectivului, tot in mm. Fara crop se ia SFF-ul,
//    ca inainte, si raportul o spune. Capatul tabelului "piecewise", r = 0.7094, cade la
//    14.19 mm: exact semidiagonala unui senzor de 36 / 1.523 = 23.64 mm, nu a celui de
//    24 mm pe care il implica SFF 1.5 (14.42 mm, r = 0.721) - crop-ul real e ce a folosit
//    si Adobe.
//
//  - lensfun ("pa", Pablo D'Angelo / Hugin): acelasi polinom, dar r e normalizat la
//    semidiagonala unui cadru 36 x 24 impartit la crop-ul aparatului de CALIBRARE
//    (mod-color.cpp: hugin_scale_in_millimeters = hypot(36, 24) / CropFactor / 2), deci
//    r = distanta in mm / (21.63 mm / crop_calibrare). Pe un aparat cu alt crop coltul
//    poate cadea dincolo de r = 1 (D3300 1.523 fata de 1.61 la Tokina: 1.05, o
//    extrapolare mica pe care raportul o mentioneaza). Distanta in mm se ia din crop-ul
//    aparatului tau: latura lunga = 36 / crop.
//
// In ambele cazuri polinomul E factorul de vignetare: inmultit aplica vignetarea, impartit
// o corecteaza (lensfun: Cd = Cs * (...) la "vignetting", / la "devignetting"). Flatul
// sintetic e chiar V, cu 1.0 pe axa.
//
// Coordonata de intrare e comuna: (xN, yN) = pozitia pixelului fata de CENTRUL imaginii, in
// unitati de Dmax. E ce stie generatorul fara sa cunoasca senzorul; conversia in mm o face
// modelul, fiecare dupa conventia lui.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace FlatCheck
{
    internal enum VignettingModelKind { Adobe, Lensfun }

    internal sealed class VignettingModel
    {
        internal readonly VignettingModelKind Kind;

        /// <summary>a1..a3 (Adobe) sau k1..k3 (lensfun); null cand profilul are doar tabel.</summary>
        internal readonly double[] Coefficients;

        /// <summary>Tabelul (r, V) al unui profil Adobe "piecewise", pe aceeasi axa r; null altfel.</summary>
        internal readonly IReadOnlyList<(double R, double V)> Table;

        /// <summary>Focala nominala in mm (cea din profil).</summary>
        internal readonly double FocalMm;

        /// <summary>Adobe: SensorFormatFactor al profilului. lensfun: crop-ul aparatului de calibrare.</summary>
        internal readonly double CropFactor;

        /// <summary>Adobe: fx / Dmax, fy / Dmax daca profilul le da; altfel f * SFF / 36.</summary>
        internal readonly double? FocalX, FocalY;

        /// <summary>Adobe: u0 / Dmax, v0 / Dmax; 0.5 cand lipsesc.</summary>
        internal readonly double CenterX, CenterY;

        internal VignettingModel(VignettingModelKind kind, double[] coefficients, IReadOnlyList<(double R, double V)> table,
                                 double focalMm, double cropFactor,
                                 double? focalX = null, double? focalY = null, double centerX = 0.5, double centerY = 0.5)
        {
            if (coefficients == null && (table == null || table.Count < 2))
                throw new ArgumentException("un model de vignetare are coeficienti sau un tabel cu cel putin doua puncte");
            if (focalMm <= 0) throw new ArgumentException("focala trebuie sa fie pozitiva");
            if (cropFactor <= 0) throw new ArgumentException("crop-ul trebuie sa fie pozitiv");
            Kind = kind;
            Coefficients = coefficients;
            Table = table;
            FocalMm = focalMm;
            CropFactor = cropFactor;
            FocalX = focalX;
            FocalY = focalY;
            CenterX = centerX;
            CenterY = centerY;
        }

        /// <summary>
        /// Factorul de vignetare in punctul (xN, yN): pozitia fata de centrul imaginii, in
        /// unitati de Dmax (latura lunga in pixeli). <paramref name="cameraCrop"/> e crop-ul
        /// aparatului pe care se aplica: lensfun il cere, Adobe il foloseste cand e dat si
        /// ia SFF-ul profilului cand nu e (0).
        /// </summary>
        internal double Factor(double xN, double yN, double cameraCrop)
        {
            double r = Radius(xN, yN, cameraCrop);
            return Coefficients != null ? Polynomial(r * r) : Interpolate(r);
        }

        /// <summary>r in unitatile modelului, pentru raport (unde cade coltul, de exemplu).</summary>
        internal double Radius(double xN, double yN, double cameraCrop)
        {
            if (Kind == VignettingModelKind.Adobe)
            {
                // Totul in mm: cadrul tau e lat de 36 / crop (36 / SFF fara crop), focala in
                // pixeli a profilului e in Dmax-ul aparatului de calibrare (36 / SFF mm), si
                // descentrarea la fel. La crop == SFF se reduce la (xN + 0.5 - CenterX) / fx.
                double calibMm = 36.0 / CropFactor;
                double longSideMm = 36.0 / (cameraCrop > 0 ? cameraCrop : CropFactor);
                double fxMm = FocalX.HasValue ? FocalX.Value * calibMm : FocalMm;
                double fyMm = FocalY.HasValue ? FocalY.Value * calibMm : fxMm;
                double xd = (xN * longSideMm - (CenterX - 0.5) * calibMm) / fxMm;
                double yd = (yN * longSideMm - (CenterY - 0.5) * calibMm) / fyMm;
                return Math.Sqrt(xd * xd + yd * yd);
            }
            else
            {
                if (cameraCrop <= 0) throw new ArgumentException("modelul lensfun are nevoie de crop-ul aparatului");
                double longSideMm = 36.0 / cameraCrop;
                double xMm = xN * longSideMm, yMm = yN * longSideMm;
                double huginMm = Math.Sqrt(36.0 * 36.0 + 24.0 * 24.0) / CropFactor / 2.0;
                return Math.Sqrt(xMm * xMm + yMm * yMm) / huginMm;
            }
        }

        /// <summary>
        /// Raza, in unitatile modelului, a coltului senzorului pe care a fost calibrat profilul:
        /// 1 la lensfun prin definitie, semidiagonala lui 36 x 24 / SFF peste focala la Adobe.
        /// Dincolo de ea polinomul extrapoleaza, si un polinom de gradul sase extrapolat cu
        /// 50 % (un full frame pe un profil DX) da valori fara sens, chiar negative.
        /// </summary>
        internal double CalibrationCornerRadius()
        {
            if (Kind != VignettingModelKind.Adobe) return 1.0;
            double calibMm = 36.0 / CropFactor;
            double fxMm = FocalX.HasValue ? FocalX.Value * calibMm : FocalMm;
            return Math.Sqrt(36.0 * 36.0 + 24.0 * 24.0) / 2.0 / CropFactor / fxMm;
        }

        /// <summary>Coltul unui cadru w x h fata de coltul senzorului de calibrare; peste 1 = extrapolare.</summary>
        internal double Extrapolation(int w, int h, double cameraCrop)
        {
            double dmax = Math.Max(w, h);
            return Radius(w / 2.0 / dmax, h / 2.0 / dmax, cameraCrop) / CalibrationCornerRadius();
        }

        double Polynomial(double r2)
        {
            double v = 1, p = 1;
            foreach (double c in Coefficients) { p *= r2; v += c * p; }
            return v;
        }

        double Interpolate(double r)
        {
            if (r <= Table[0].R) return Table[0].V;
            for (int i = 1; i < Table.Count; i++)
                if (r <= Table[i].R)
                {
                    double t = (r - Table[i - 1].R) / (Table[i].R - Table[i - 1].R);
                    return Table[i - 1].V + t * (Table[i].V - Table[i - 1].V);
                }
            return Table[Table.Count - 1].V;
        }

        internal string Describe()
        {
            string coeffs = Coefficients != null
                ? string.Join(", ", Array.ConvertAll(Coefficients, c => c.ToString("0.000000", CultureInfo.InvariantCulture)))
                : $"tabel cu {Table.Count} puncte";
            return Kind == VignettingModelKind.Adobe
                ? $"Adobe, V = 1 + a1 r^2 + a2 r^4 + a3 r^6 in unitati de focala; a = {coeffs}; SFF {CropFactor.ToString("0.###", CultureInfo.InvariantCulture)}"
                  + (FocalX.HasValue || CenterX != 0.5 || CenterY != 0.5
                        ? $"; fx/Dmax {(FocalX ?? 0).ToString("0.####", CultureInfo.InvariantCulture)}, centru ({CenterX.ToString("0.####", CultureInfo.InvariantCulture)}, {CenterY.ToString("0.####", CultureInfo.InvariantCulture)})"
                        : "")
                : $"lensfun pa, V = 1 + k1 r^2 + k2 r^4 + k3 r^6, r la semidiagonala 36x24 / {CropFactor.ToString("0.###", CultureInfo.InvariantCulture)}; k = {coeffs}";
        }
    }
}
