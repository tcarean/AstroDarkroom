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

// IFrameReader.cs - contractul pe care il implementeaza fiecare format de intrare.
//
// Implementarile sunt singurele care stiu un format anume. ImageLoader alege intre ele
// prin contractul asta si nu cunoaste niciuna pe nume, deci un format nou se adauga fara
// sa se atinga dispecerul - si, mai important, fara ca vreo diferenta de comportament
// intre formate sa se poata strecura in el.

using System.Collections.Generic;

namespace AstroPhotoAnalyzer
{
    internal interface IFrameReader
    {
        /// <summary>
        /// Revendica fisierul? Decizia e pe EXTENSIE, nu pe continut. Daca s-ar adulmeca
        /// octetii, un fisier stricat ar putea cadea tacut pe alt cititor si ar iesi de
        /// acolo ca zgomot, in loc de eroare.
        /// </summary>
        bool Handles(string path);

        /// <summary>Extensiile revendicate, cu punct, cu minuscule - pentru mesaje si pentru filtrele de fisiere ale ferestrei.</summary>
        IReadOnlyList<string> Extensions { get; }

        Raster Load(string path);
    }
}
