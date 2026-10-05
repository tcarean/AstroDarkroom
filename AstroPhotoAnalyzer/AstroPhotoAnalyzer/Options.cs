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

// Options.cs - parsarea argumentelor din linia de comanda.
//
// Flagurile pot veni oriunde: "AstroPhotoAnalyzer --medium canvas poza.tif" si
// "AstroPhotoAnalyzer poza.tif --medium canvas" sunt echivalente. Parse nu arunca:
// intoarce un Options cu Error setat, iar AnalyzerCli il scrie pe stderr si iese cu 1.
//
// Valorile de aici sunt DOAR ce a tastat omul, verificate sintactic. Ce inseamna ele
// pentru analiza (Dmax-ul unui mediu, distanta implicita de privire) se decide in
// nucleu, in Parameters - consola nu stie ce e un baryta.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    internal sealed class Options
    {
        internal string Image;

        /// <summary>Mediul de print, ca text: baryta, canvas, acrilic. Null = implicitul nucleului.</summary>
        internal string Medium;

        /// <summary>Dmax explicit, care bate mediul. Null = din mediu.</summary>
        internal double? Dmax;

        /// <summary>Distanta de privire, in metri. Null = nucleul o deduce din diagonala.</summary>
        internal double? Distance;

        /// <summary>Latimile de print candidate, in cm. Null = lista implicita a nucleului.</summary>
        internal double[] Sizes;

        /// <summary>Raportul de aspect tinta, latime/inaltime. Null = cel nativ al imaginii.</summary>
        internal double? Aspect;

        /// <summary>Fundalul-tinta pentru simularea de print (0..1). Null = implicitul nucleului.</summary>
        internal double? TargetBg;

        internal double? PassepartoutCm;
        internal double? FrameCm;

        /// <summary>FITS-ul al carui WCS se imprumuta imaginii (R-027). Null = WCS-ul fisierului, daca are.</summary>
        internal string Wcs;

        /// <summary>Profilul ICC de imprimanta din care se citeste Dmax-ul (R-028); exclude --dmax.</summary>
        internal string Icc;

        /// <summary>Unde se scrie si raportul, pe langa stdout. Null = doar stdout.</summary>
        internal string Out;

        internal bool Help;
        internal string Error;

        static readonly HashSet<string> BoolFlags = new() { "-h", "--help" };

        static readonly HashSet<string> ValueFlags = new()
        { "--medium", "--dmax", "--distance", "--sizes", "--aspect", "--target-bg",
          "--passepartout", "--frame", "--wcs", "--icc", "-o", "--out" };

        internal static readonly string[] Media = Parameters.MediumNames.ToArray();

        internal static Options Parse(string[] args)
        {
            var o = new Options();
            if (args.Length == 0 || args.Contains("-h") || args.Contains("--help"))
            {
                o.Help = true;
                return o;
            }

            var positional = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];

                if (BoolFlags.Contains(a)) continue;

                if (ValueFlags.Contains(a))
                {
                    if (i + 1 >= args.Length)
                        return o.Fail($"lipseste valoarea pentru {a}");
                    string v = args[i + 1];
                    if (BoolFlags.Contains(v) || ValueFlags.Contains(v))
                        return o.Fail($"{a} asteapta o valoare, dar a primit flagul {v}");
                    i++;

                    switch (a)
                    {
                        case "--medium":
                            string m = v.ToLowerInvariant();
                            if (Array.IndexOf(Media, m) < 0)
                                return o.Fail($"--medium asteapta {string.Join(", ", Media)}; a primit '{v}'");
                            o.Medium = m; break;
                        case "--dmax":
                            if (!TryNum(v, out var dmax) || dmax <= 0 || dmax > Thresholds.DmaxMax)
                                return o.Fail($"--dmax asteapta un numar intre 0 si {Fmt.F0(Thresholds.DmaxMax)}, a primit '{v}'");
                            o.Dmax = dmax; break;
                        case "--distance":
                            if (!TryNum(v, out var dist) || dist <= 0)
                                return o.Fail($"--distance asteapta metri, un numar pozitiv, a primit '{v}'");
                            o.Distance = dist; break;
                        case "--sizes":
                            var sizes = new List<double>();
                            foreach (string s in v.Split(',', StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (!TryNum(s.Trim(), out var cm) || cm <= 0)
                                    return o.Fail($"--sizes asteapta latimi in cm separate prin virgula, a primit '{v}'");
                                sizes.Add(cm);
                            }
                            if (sizes.Count == 0) return o.Fail("--sizes asteapta cel putin o latime");
                            o.Sizes = sizes.ToArray(); break;
                        case "--aspect":
                            if (!TryAspect(v, out var asp))
                                return o.Fail($"--aspect asteapta latime:inaltime (ex. 50:35), a primit '{v}'");
                            o.Aspect = asp; break;
                        case "--target-bg":
                            if (!TryNum(v, out var bg) || bg <= 0 || bg >= 1)
                                return o.Fail($"--target-bg asteapta un numar intre 0 si 1, a primit '{v}'");
                            o.TargetBg = bg; break;
                        case "--passepartout":
                            if (!TryNum(v, out var pp) || pp < 0)
                                return o.Fail($"--passepartout asteapta cm, un numar >= 0, a primit '{v}'");
                            o.PassepartoutCm = pp; break;
                        case "--frame":
                            if (!TryNum(v, out var fr) || fr < 0)
                                return o.Fail($"--frame asteapta cm, un numar >= 0, a primit '{v}'");
                            o.FrameCm = fr; break;
                        case "--wcs":
                            if (v.Length == 0) return o.Fail("--wcs asteapta calea unui FITS plate-solved");
                            o.Wcs = v; break;
                        case "--icc":
                            if (v.Length == 0) return o.Fail("--icc asteapta calea unui profil ICC de imprimanta");
                            o.Icc = v; break;
                        case "-o":
                        case "--out":
                            if (v.Length == 0) return o.Fail("-o asteapta calea fisierului de raport");
                            o.Out = v; break;
                    }
                    continue;
                }

                if (a.StartsWith("-"))
                    return o.Fail($"optiune necunoscuta: {a}");

                positional.Add(a);
            }

            if (positional.Count == 0) return o.Fail("lipseste fisierul de analizat");
            if (positional.Count > 1) return o.Fail("un singur fisier de analizat; a primit " + positional.Count);
            if (o.Icc != null && o.Dmax.HasValue) return o.Fail("--icc si --dmax dau amandoua Dmax-ul hartiei; alege unul");
            o.Image = positional[0];
            return o;
        }

        Options Fail(string message)
        {
            Error = message;
            return this;
        }

        static bool TryNum(string s, out double v)
            => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

        /// <summary>"50:35" sau "50x35" -> 50/35. Ambele forme, fiindca amandoua se tasteaza natural.</summary>
        static bool TryAspect(string s, out double ratio)
        {
            ratio = 0;
            string[] parts = s.Split(new[] { ':', 'x', 'X' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return false;
            if (!TryNum(parts[0], out var w) || !TryNum(parts[1], out var h)) return false;
            if (w <= 0 || h <= 0) return false;
            ratio = w / h;
            return true;
        }
    }
}
