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

// LiftTable.cs - liniarizarea unui esantion sRGB, pe doua cai: exact (ColorMath.SrgbToLinear)
// sau ridicat cu o gamma si liniarizat printr-o singura tabela pe cele 65536 de niveluri de
// 16 biti (ADR 0035).
//
// Simularea de print ridica subesantionul de ~56 de ori (scara tintei exacte, ADR 0025) si
// remasoara de fiecare data L*-ul dalelor: liniarizarea fiecarui esantion ridicat cu
// Math.Pow(x, 2.4) insemna 250 de milioane de Pow pe o imagine de 23 MP, 11 secunde din
// cele 17 ale simularii. Tabela tine lin(pow(k / 65535, gamma)) pentru fiecare nivel k si
// interpoleaza liniar intre doua niveluri vecine - PE VALOAREA LINIARA, nu pe cea ridicata:
// rasterul ridicat intermediar (RasterOps.Gamma, aceeasi indexare si aceeasi interpolare, pe
// valoarea ridicata) nu se mai construieste deloc. Masurat pe imaginile de referinta
// (docs/validation.md, 2026-10-09): nicio dala nu se misca cu mai mult de un ulp de float al
// L*-ului ei (7.6e-6 la L* 32..64); pe un esantion izolat cele doua interpolari difera cel
// mai mult in primul pas de 16 biti la gamma mica, unde si interpolarea curbei de putere
// era grosiera. Randul "asa cum e" nu trece prin tabela: ramane pe calea exacta.
//
// Cele doua cai sunt structuri care implementeaza aceeasi interfata, iar buclele peste
// esantioane (DustAnalyzer.PatchLstars, RasterOps.LstarPlane) sunt generice pe ea: JIT-ul
// specializeaza bucla pe structura si inlineaza apelul, deci nu e un delegat pe esantion si
// nici doua copii ale buclei.

using System;

namespace AstroPhotoAnalyzer
{
    /// <summary>Un esantion sRGB codat (0..1) -> luminanta lui liniara, pe una din cele doua cai.</summary>
    internal interface ISrgbLinearizer
    {
        double Linear(float s);
    }

    /// <summary>Calea exacta: ColorMath.SrgbToLinear pe esantionul asa cum e. Masuratorile "asa cum e" trec pe aici.</summary>
    internal readonly struct ExactLinearizer : ISrgbLinearizer
    {
        public double Linear(float s) => ColorMath.SrgbToLinear(s);
    }

    /// <summary>
    /// Esantionul ridicat cu <see cref="Gamma"/> si liniarizat, printr-o tabela pe nivelurile
    /// de 16 biti cu interpolare liniara intre doua niveluri vecine, pe valoarea liniara.
    /// </summary>
    internal readonly struct LiftTable : ISrgbLinearizer
    {
        readonly double[] _lin;

        internal double Gamma { get; }

        internal LiftTable(double gamma)
        {
            Gamma = gamma;
            double[] pow = RasterOps.PowerLut(gamma);
            _lin = new double[pow.Length];
            for (int k = 0; k < pow.Length; k++) _lin[k] = ColorMath.SrgbToLinear(pow[k]);
        }

        public double Linear(float s)
        {
            // Aceeasi indexare ca RasterOps.Gamma: un NaN ar da (int)NaN = int.MinValue; cititorii
            // nu lasa NaN sa treaca (R-013), dar un raster facut in memorie poate.
            double v = (float.IsNaN(s) ? 0 : Math.Clamp(s, 0f, 1f)) * 65535.0;
            int k = (int)v;
            double f = v - k;
            return f == 0 ? _lin[k] : _lin[k] + f * (_lin[k + 1] - _lin[k]);
        }
    }
}
