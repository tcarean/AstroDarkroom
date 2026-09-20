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

// ImageLoader.cs - SINGURUL loc care decide ce cititor deschide un fisier.
//
// De aici in jos nimic nu mai stie de unde au venit pixelii: analizoarele primesc un
// Raster, rapoartele primesc un rezultat. Daca decizia asta se mai imprastie o data
// prin cod, urmatoarea diferenta de comportament intre formate va aparea tacut.
//
// Spre deosebire de FlatCheck, o extensie necunoscuta e o EROARE, nu un fallback pe
// FITS: aici formatele sunt toate cu extensie proprie, si un ".psd" cazut pe cititorul
// FITS ar iesi ca "header incomplet" in loc de "format neacceptat".

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    internal static class ImageLoader
    {
        static readonly IFrameReader[] Readers = { new FitsReader(), new ImageSharpReader() };

        /// <summary>Toate extensiile acceptate, pentru mesaje si pentru filtrul de fisiere al ferestrei.</summary>
        internal static IReadOnlyList<string> Extensions
            => Readers.SelectMany(r => r.Extensions).ToArray();

        internal static Raster Load(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("fisierul nu exista: " + path, path);

            IFrameReader reader = Readers.FirstOrDefault(r => r.Handles(path));
            if (reader == null)
                throw new Exception($"format neacceptat: '{Path.GetExtension(path)}'. Se accepta {string.Join(", ", Extensions)}.");

            return reader.Load(path);
        }
    }
}
