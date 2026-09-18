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

// Flatul sintetic: rasterul V(x, y) al unui model de vignetare, la marimea cadrului de
// referinta, cu 1.0 pe axa. Acromatic prin constructie - aceeasi valoare in toate cele
// patru pozitii Bayer - fiindca modelele publicate au un singur set de parametri pentru
// toate canalele ("identical for all color channels", documentul Adobe). Pur: geometrie
// si model in, raster out.
//
// Pe langa raster se masoara, din model, adancimea in cateva puncte ale cadrului - coltul
// (centrul cutiei de 5 % pe care o foloseste CornerStats), marginea lunga, marginea scurta,
// jumatatea semidiagonalei - ca raportul sa spuna cat de adanca e vignetarea pe care o
// scrie, in stopuri, in aceleasi puncte in care s-au comparat masterele reale cu cele doua
// baze (CLAUDE.md, "Synthetic flats").

using System;

namespace FlatCheck
{
    internal sealed class SyntheticFlatResult
    {
        internal readonly Img Flat;
        internal readonly LensProfile Profile;
        internal readonly double CameraCrop;
        internal readonly int Width, Height;
        /// <summary>Adancimea in stopuri (pozitiv = mai intunecat decat axa) in punctele de referinta.</summary>
        internal readonly double CornerStops, LongEdgeStops, ShortEdgeStops, HalfDiagonalStops;
        /// <summary>Raza coltului in unitatile modelului; peste 1 la lensfun inseamna extrapolare.</summary>
        internal readonly double CornerRadius;
        /// <summary>Coltul tau fata de coltul senzorului de calibrare: peste 1 = extrapolare, peste 1.1 = una mare.</summary>
        internal double Extrapolation => CornerRadius / Profile.Model.CalibrationCornerRadius();
        internal const double SmallExtrapolation = 1.1;

        internal SyntheticFlatResult(Img flat, LensProfile profile, double cameraCrop, int w, int h,
                                     double corner, double longEdge, double shortEdge, double halfDiag, double cornerRadius)
        {
            Flat = flat;
            Profile = profile;
            CameraCrop = cameraCrop;
            Width = w;
            Height = h;
            CornerStops = corner;
            LongEdgeStops = longEdge;
            ShortEdgeStops = shortEdge;
            HalfDiagonalStops = halfDiag;
            CornerRadius = cornerRadius;
        }
    }

    internal static class SyntheticFlatBuilder
    {
        /// <summary>Fractiunea din semilatura la care sta centrul cutiei de colt de 5 % (CornerStats).</summary>
        internal const double CornerBoxFraction = 0.95;

        internal static SyntheticFlatResult Build(LensProfile profile, int w, int h, double cameraCrop)
        {
            if (w < 2 || h < 2) throw new ArgumentException("cadrul de referinta e prea mic");
            VignettingModel m = profile.Model;
            double dmax = Math.Max(w, h);
            var flat = new Img(h, w);
            for (int y = 0; y < h; y++)
            {
                double yN = (y + 0.5 - h / 2.0) / dmax;
                for (int x = 0; x < w; x++)
                {
                    double xN = (x + 0.5 - w / 2.0) / dmax;
                    flat.Set(y, x, m.Factor(xN, yN, cameraCrop));
                }
            }
            double halfW = w / 2.0 / dmax, halfH = h / 2.0 / dmax;
            double axis = m.Factor(0, 0, cameraCrop);
            double Stops(double xN, double yN) => Math.Log2(axis / m.Factor(xN, yN, cameraCrop));
            double f = CornerBoxFraction;
            return new SyntheticFlatResult(flat, profile, cameraCrop, w, h,
                Stops(f * halfW, f * halfH),
                Stops(f * halfW, 0),
                Stops(0, f * halfH),
                Stops(0.5 * halfW, 0.5 * halfH),
                m.Radius(halfW, halfH, cameraCrop));
        }
    }
}
