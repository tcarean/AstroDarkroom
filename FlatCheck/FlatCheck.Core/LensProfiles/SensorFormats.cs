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

// The sensor types and their nominal crop - the fallback for a camera the lensfun camera
// database does not have. Six numbers fixed for decades, not a list of cameras: the list of
// cameras is lensfun's (1053 bodies, updated with the database), and a second list of ours would
// have gone stale at the first new model. The nominal crop is within 1-2 % of the real one
// (D3300: 1.523 against 1.5), enough for the corner's radius.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>A sensor format and its nominal crop factor.</summary>
    /// <param name="Label">The format's name, as the report and the window show it.</param>
    /// <param name="Crop">The nominal crop factor against full frame (1.0).</param>
    /// <param name="Note">The sensor's size and the systems that use it.</param>
    internal sealed record SensorFormat(string Label, double Crop, string Note)
    {
        internal string CropText => Crop.ToString("0.0#", CultureInfo.InvariantCulture);
    }

    /// <summary>The six sensor formats, and the nearest one to a crop factor.</summary>
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

        /// <summary>
        /// "full frame 1.0, APS-C 1.5, APS-C Canon 1.6, ..." - for the message that asks for
        /// --crop.
        /// </summary>
        /// <returns>Every format with its crop, comma-separated.</returns>
        internal static string Describe()
            => string.Join(", ", All.Select(f => $"{f.Label} {f.CropText}"));

        /// <summary>
        /// How far from the nominal crop the same format still is, as a fraction: D3300 1.523 is
        /// APS-C 1.5, the Tokina's lensfun 1.61 is APS-C Canon 1.6.
        /// </summary>
        internal const double Tolerance = 0.05;

        /// <summary>
        /// The format whose nominal crop is nearest, within the tolerance, or null (1.15 is
        /// nothing).
        /// </summary>
        /// <param name="crop">The crop factor; 0, negative or NaN gives null.</param>
        /// <returns>The nearest format within <see cref="Tolerance"/>, or null.</returns>
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

        /// <summary>The label of a crop that matches no common format.</summary>
        internal static string Unusual => T("unusual format", "format neobisnuit");

        /// <summary>The format's label, or <see cref="Unusual"/>.</summary>
        /// <param name="crop">The crop factor.</param>
        /// <returns>The nearest format's label, or <see cref="Unusual"/>.</returns>
        internal static string LabelOf(double crop) => Nearest(crop)?.Label ?? Unusual;
    }
}
