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

// DarkNebulaCatalogue.cs - catalogul Lynds al nebuloaselor intunecate (LDN, 1962),
// imbarcat ca resursa text (Input/ldn.txt) si citit o singura data.
//
// Datele sunt cele publicate: centrul in B1950, aria in grade patrate, clasa de
// opacitate si numerele Barnard cu care se identifica norul. Centrul se preceseaza in
// J2000 la citire, fiindca WCS-ul lui Siril e in J2000. Forma unui nor nu e in catalog,
// doar aria: unealta il trateaza ca un disc de aceeasi arie (ADR 0025). Licenta si
// provenienta: THIRD-PARTY-NOTICES.md si ThirdParty.cs.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>Un nor din catalog, cu centrul in J2000 (grade) si raza discului de aceeasi arie.</summary>
    internal sealed record DarkNebula(int Ldn, double Ra, double Dec, double AreaDeg2, int Opacity, IReadOnlyList<string> Barnard)
    {
        internal double RadiusDeg => Astrometry.RadiusOfArea(AreaDeg2);

        /// <summary>"LDN 42 (B78)", "LDN 303 (B304, B301, B297)", "LDN 621".</summary>
        internal string Name => Barnard.Count == 0
            ? "LDN " + Ldn.ToString(CultureInfo.InvariantCulture)
            : "LDN " + Ldn.ToString(CultureInfo.InvariantCulture) + " (" + string.Join(", ", Barnard.Select(b => "B" + b)) + ")";
    }

    internal static class DarkNebulaCatalogue
    {
        internal const string ResourceName = "ldn.txt";

        static readonly Lazy<IReadOnlyList<DarkNebula>> Loaded = new(LoadEmbedded);

        /// <summary>Toate cele 1787 de randuri numerotate ale catalogului, in ordinea fisierului.</summary>
        internal static IReadOnlyList<DarkNebula> All => Loaded.Value;

        static IReadOnlyList<DarkNebula> LoadEmbedded()
        {
            using Stream s = typeof(DarkNebulaCatalogue).Assembly.GetManifestResourceStream(ResourceName)
                ?? throw new InvalidOperationException("resursa " + ResourceName + " lipseste din nucleu");
            using var r = new StreamReader(s);
            return Load(r);
        }

        /// <summary>
        /// Citeste formatul resursei: linii cu '#' sunt comentarii; altfel sase coloane
        /// despartite de tab: LDN, RA B1950 "h m.m", Dec B1950 "d m" cu semn, aria, opacitatea,
        /// numerele Barnard despartite de spatii (poate fi gol; "119a" e un nume valid).
        /// </summary>
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
                // Un numar Barnard poate purta o litera ("119a"): ramane text.
                string[] barnard = f.Length > 5
                    ? f[5].Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    : Array.Empty<string>();
                var (ra, dec) = Astrometry.B1950ToJ2000(ra1950, dec1950);
                list.Add(new DarkNebula(ldn, ra, dec, area, opacity, barnard));
            }
            return list;
        }

        /// <summary>"17 30.0" (ore, minute zecimale) in grade.</summary>
        static double ParseRa(string s)
        {
            string[] p = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            double h = double.Parse(p[0], CultureInfo.InvariantCulture), m = p.Length > 1 ? double.Parse(p[1], CultureInfo.InvariantCulture) : 0;
            return (h + m / 60) * 15;
        }

        /// <summary>"-25 40" (grade cu semn, minute) in grade; semnul se ia din text, ca "-00 30" sa ramana negativ.</summary>
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
