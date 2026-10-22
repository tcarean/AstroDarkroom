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

// DarkNebulaCatalogue.cs - Lynds' Catalogue of Dark Nebulae (LDN, 1962), embedded as a text
// resource (Input/ldn.txt) and read once.
//
// The data are the published ones: the centre in B1950, the area in square degrees, the
// opacity class and the Barnard numbers the cloud is identified with. The centre is precessed to
// J2000 when read, because Siril's WCS is in J2000. A cloud's shape is not in the catalogue,
// only its area: the tool treats it as a disc of the same area (ADR 0025). Licence and
// provenance: THIRD-PARTY-NOTICES.md and ThirdParty.cs.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// A cloud of the catalogue, with its centre in J2000 (degrees) and the radius of the disc of
    /// the same area.
    /// </summary>
    /// <param name="Ldn">The cloud's LDN number.</param>
    /// <param name="Ra">The right ascension of the centre, J2000, in degrees.</param>
    /// <param name="Dec">The declination of the centre, J2000, in degrees.</param>
    /// <param name="AreaDeg2">The area, in square degrees.</param>
    /// <param name="Opacity">The opacity class, 1 (faintest) to 6; 0 when not given.</param>
    /// <param name="Barnard">
    /// The Barnard numbers of the same cloud, as text ("119a" is one); empty when there are none.
    /// </param>
    internal sealed record DarkNebula(int Ldn, double Ra, double Dec, double AreaDeg2, int Opacity, IReadOnlyList<string> Barnard)
    {
        internal double RadiusDeg => Astrometry.RadiusOfArea(AreaDeg2);

        /// <summary>"LDN 42 (B78)", "LDN 303 (B304, B301, B297)", "LDN 621".</summary>
        internal string Name => Barnard.Count == 0
            ? "LDN " + Ldn.ToString(CultureInfo.InvariantCulture)
            : "LDN " + Ldn.ToString(CultureInfo.InvariantCulture) + " (" + string.Join(", ", Barnard.Select(b => "B" + b)) + ")";
    }

    /// <summary>Lynds' catalogue, parsed once from the core's embedded resource.</summary>
    internal static class DarkNebulaCatalogue
    {
        internal const string ResourceName = "ldn.txt";

        static readonly Lazy<IReadOnlyList<DarkNebula>> Loaded = new(LoadEmbedded);

        /// <summary>All the 1787 numbered rows of the catalogue, in the file's order.</summary>
        internal static IReadOnlyList<DarkNebula> All => Loaded.Value;

        /// <summary>
        /// Parses the embedded resource; throws when the core was built without it.
        /// </summary>
        /// <returns>The catalogue's rows, in the file's order.</returns>
        static IReadOnlyList<DarkNebula> LoadEmbedded()
        {
            using Stream s = typeof(DarkNebulaCatalogue).Assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException("resursa " + ResourceName + " lipseste din nucleu");
            using var r = new StreamReader(s);
            return Load(r);
        }

        /// <summary>
        /// Reads the resource's format: lines with '#' are comments; otherwise six columns
        /// separated by tabs: LDN, RA B1950 "h m.m", Dec B1950 "d m" with its sign, the area, the
        /// opacity, the Barnard numbers separated by spaces (may be empty; "119a" is a valid
        /// name). Throws on a row with fewer than 5 columns or a number it cannot read.
        /// </summary>
        /// <param name="reader">The text, read to its end.</param>
        /// <returns>The rows, in the text's order, their centres precessed to J2000.</returns>
        internal static IReadOnlyList<DarkNebula> Load(TextReader reader)
        {
            var list = new List<DarkNebula>();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0 || line[0] == '#') continue;
                string[] f = line.Split('\t');
                if (f.Length < 5) throw new FormatException("rand de catalog cu mai putin de 5 coloane: " + line);
                int ldn = int.Parse(f[0], CultureInfo.InvariantCulture);
                double ra1950 = ParseRa(f[1]), dec1950 = ParseDec(f[2]);
                double area = double.Parse(f[3], CultureInfo.InvariantCulture);
                int opacity = f[4].Length == 0 ? 0 : int.Parse(f[4], CultureInfo.InvariantCulture);
                // A Barnard number may carry a letter ("119a"): it stays text.
                string[] barnard = f.Length > 5
                    ? f[5].Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    : Array.Empty<string>();
                var (ra, dec) = Astrometry.B1950ToJ2000(ra1950, dec1950);
                list.Add(new DarkNebula(ldn, ra, dec, area, opacity, barnard));
            }
            return list;
        }

        /// <summary>"17 30.0" (hours, decimal minutes) in degrees.</summary>
        /// <param name="s">
        /// The right ascension as the catalogue writes it; the minutes may be absent.
        /// </param>
        /// <returns>The right ascension, in degrees.</returns>
        static double ParseRa(string s)
        {
            string[] p = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            double h = double.Parse(p[0], CultureInfo.InvariantCulture), m = p.Length > 1 ? double.Parse(p[1], CultureInfo.InvariantCulture) : 0;
            return (h + m / 60) * 15;
        }

        /// <summary>
        /// "-25 40" (signed degrees, minutes) in degrees; the sign is taken from the text, so
        /// that "-00 30" stays negative.
        /// </summary>
        /// <param name="s">
        /// The declination as the catalogue writes it; the minutes may be absent.
        /// </param>
        /// <returns>The declination, in degrees.</returns>
        static double ParseDec(string s)
        {
            string[] p = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            bool negative = p[0].StartsWith("-", StringComparison.Ordinal);
            double d = Math.Abs(double.Parse(p[0], CultureInfo.InvariantCulture)), m = p.Length > 1 ? double.Parse(p[1], CultureInfo.InvariantCulture) : 0;
            double v = d + m / 60;
            return negative ? -v : v;
        }
    }
}
