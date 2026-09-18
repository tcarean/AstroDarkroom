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

// Tipurile de senzor si crop-ul lor nominal - rezerva pentru un aparat pe care baza de
// camere lensfun nu-l are. Sase numere fixe de decenii, nu o lista de aparate: lista de
// aparate o tine lensfun (1053 de corpuri, actualizata cu baza), si o a doua lista a
// noastra s-ar fi invechit la primul model nou. Crop-ul nominal e la 1-2 % de cel real
// (D3300: 1.523 fata de 1.5), destul pentru raza coltului.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FlatCheck
{
    internal sealed record SensorFormat(string Label, double Crop, string Note)
    {
        internal string CropText => Crop.ToString("0.0#", CultureInfo.InvariantCulture);
    }

    internal static class SensorFormats
    {
        internal static readonly IReadOnlyList<SensorFormat> All = new[]
        {
            new SensorFormat("full frame", 1.0, "36 x 24 mm: Nikon FX, Canon EF full frame, Sony A7, Pentax K-1"),
            new SensorFormat("APS-C", 1.5, "23.5 x 15.6 mm: Nikon DX, Sony E, Pentax, Fujifilm X"),
            new SensorFormat("APS-C Canon", 1.6, "22.3 x 14.9 mm: Canon EF-S / EF-M / RF-S"),
            new SensorFormat("APS-H", 1.3, "27.9 x 18.6 mm: Canon 1D"),
            new SensorFormat("Micro 4/3", 2.0, "17.3 x 13 mm: Olympus, OM System, Panasonic"),
            new SensorFormat("1\"", 2.7, "13.2 x 8.8 mm: Sony RX100, Nikon 1"),
        };

        /// <summary>"full frame 1.0, APS-C 1.5, APS-C Canon 1.6, ..." - pentru mesajul care cere --crop.</summary>
        internal static string Describe()
            => string.Join(", ", All.Select(f => $"{f.Label} {f.CropText}"));

        /// <summary>Cat de departe de crop-ul nominal mai e acelasi format: D3300 1.523 e APS-C 1.5, Tokina lensfun 1.61 e APS-C Canon 1.6.</summary>
        internal const double Tolerance = 0.05;

        /// <summary>Formatul al carui crop nominal e cel mai aproape, in toleranta, sau null (1.15 nu e nimic).</summary>
        internal static SensorFormat Nearest(double crop)
        {
            if (!(crop > 0)) return null;
            SensorFormat best = null;
            double bestRel = double.MaxValue;
            foreach (SensorFormat f in All)
            {
                double rel = Math.Abs(f.Crop - crop) / f.Crop;
                if (rel < bestRel) { bestRel = rel; best = f; }
            }
            return bestRel <= Tolerance ? best : null;
        }

        internal const string Unusual = "format neobisnuit";

        /// <summary>Eticheta formatului, sau "format neobisnuit".</summary>
        internal static string LabelOf(double crop) => Nearest(crop)?.Label ?? Unusual;
    }
}
