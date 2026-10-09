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

#nullable disable // copied from FlatCheck 2.3.0 unchanged, which compiles without nullable annotations (ADR 0003)

// IFrameReader.cs - contractul pe care il implementeaza fiecare format de intrare.
//
// Implementarile sunt singurele care stiu un format anume. ImageLoader alege intre ele
// prin contractul asta si nu cunoaste niciuna pe nume, deci un format nou se adauga fara
// sa se atinga dispecerul - si, mai important, fara ca vreo diferenta de comportament
// intre formate sa se poata strecura in el.

namespace LightsCheck.Core
{
    internal interface IFrameReader
    {
        /// <summary>
        /// Revendica fisierul? Decizia e pe EXTENSIE, nu pe continut. Daca s-ar adulmeca
        /// octetii, un raw pe care libraw nu-l stie ar putea cadea tacut pe cititorul
        /// FITS si ar iesi de acolo ca zgomot, in loc de eroare.
        /// </summary>
        bool Handles(string path);

        LoadedFrame Load(string path);

        /// <summary>
        /// Acelasi cadru, dar FARA pixeli: <see cref="LoadedFrame.Image"/> ramane null si
        /// se completeaza doar ce spune fisierul despre el - headerul, focala, diafragma,
        /// aparatul, obiectivul. Exista fiindca identificarea obiectivului si preumplerea
        /// tabului Sintetic au nevoie de metadate, si o decodare de 24 MP pentru o
        /// diafragma e o risipa care obliga la asteptari dupa lantul de deschidere.
        /// </summary>
        LoadedFrame LoadInfo(string path);
    }
}
