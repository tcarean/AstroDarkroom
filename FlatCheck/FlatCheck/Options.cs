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
// Din faza 8 flagurile pot veni oriunde: "flatcheck --expo f.fit" si
// "flatcheck f.fit --expo" sunt echivalente. Inainte args[0] era INTOTDEAUNA calea
// imaginii, deci un flag pus primul era inghitit ca nume de fisier si esua cu
// "Could not find file '--expo'".
//
// Parse nu arunca: intoarce un Options cu Error setat, iar FlatCheckCli il scrie pe
// stderr si iese cu 1.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FlatCheck
{
    internal sealed class Options
    {
        internal string Image;
        internal string Ratio;
        internal bool Light;
        internal bool Expo;
        internal bool Sky;

        // Modul 5: vederea "Histogram" din Siril. Nu are verdict si nu are optiuni
        // proprii; -o e unde se scrie poza.
        internal bool Histeq;

        // Modul 6: flat-ul netezit. Valoarea e CALEA fisierului FITS de scris - explicit,
        // fara nume implicit: un mod care scrie 96 MB langa master trebuie sa i se spuna
        // unde. -o ramane harta. --scale e scara de netezire, in pixeli de senzor.
        internal string Smooth;
        internal double ScalePx = SmoothAnalyzer.DefaultScalePx;

        // Flatul sintetic si comparatia. --lenses n-are nevoie de fisier de intrare.
        internal string Synthetic;
        internal string Lens;
        internal ProfileSource? Source;
        internal double? Focal;
        internal double? FStop;
        internal double? Crop;
        internal string Profiles;
        internal bool Lenses;
        internal string Against;

        // --scale auto: unealta propune scara (SmoothScaleAdvisor) si netezeste la ea;
        // fara textura sau nedeterminat, nu scrie nimic si iese cu 2.
        internal bool ScaleAuto;
        internal string OutPng = "flatcheck.png";
        internal double? Sat;
        internal double? Black;

        // Tiparul de fisiere pentru --light pe un folder. Implicitul si motivul lui stau
        // in SeriesFinder, langa potrivirea propriu-zisa - aceeasi conventie o foloseste
        // si aplicatia WPF, care n-are linie de comanda de unde sa-l primeasca.
        internal string Pattern = SeriesFinder.DefaultPattern;

        // Campul diagonal, in grade, declarat de om. Portita pentru cand headerul nu
        // spune destul (DSLR fara XPIXSZ) sau cand presupunerea conservatoare de
        // full-frame nedreptateste un senzor mic. Nu e un flag de fortare: da unealtei
        // adevarul, nu ii cere sa-l ignore.
        internal double? Fov;

        internal bool Help;
        internal string Error;

        static readonly HashSet<string> BoolFlags = new()
        { "--light", "--expo", "--sky", "--histeq", "--lenses", "-h", "--help" };

        static readonly HashSet<string> ValueFlags = new()
        { "--ratio", "-o", "--out", "--sat", "--black", "--pattern", "--fov", "--smooth", "--scale",
          "--synthetic", "--lens", "--source", "--focal", "--fstop", "--crop", "--profiles", "--against" };

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

                if (BoolFlags.Contains(a))
                {
                    if (a == "--light") o.Light = true;
                    else if (a == "--expo") o.Expo = true;
                    else if (a == "--sky") o.Sky = true;
                    else if (a == "--histeq") o.Histeq = true;
                    else if (a == "--lenses") o.Lenses = true;
                    continue;
                }

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
                        case "--ratio": o.Ratio = v; break;
                        case "-o":
                        case "--out": o.OutPng = v; break;
                        case "--sat":
                            if (!TryNum(v, out var sat)) return o.Fail($"--sat asteapta un numar, a primit '{v}'");
                            o.Sat = sat; break;
                        case "--black":
                            if (!TryNum(v, out var blk)) return o.Fail($"--black asteapta un numar, a primit '{v}'");
                            o.Black = blk; break;
                        case "--pattern":
                            if (v.Length == 0) return o.Fail("--pattern asteapta un tipar, a primit sirul gol");
                            o.Pattern = v; break;
                        case "--fov":
                            if (!TryNum(v, out var fov)) return o.Fail($"--fov asteapta un numar, a primit '{v}'");
                            if (fov <= 0 || fov >= 180)
                                return o.Fail($"--fov asteapta grade intre 0 si 180, a primit {v}");
                            o.Fov = fov; break;
                        case "--smooth":
                            if (v.Length == 0) return o.Fail("--smooth asteapta calea fisierului FITS de scris, a primit sirul gol");
                            o.Smooth = v; break;
                        case "--scale":
                            if (string.Equals(v, "auto", StringComparison.OrdinalIgnoreCase)) { o.ScaleAuto = true; break; }
                            if (!TryNum(v, out var scale)) return o.Fail($"--scale asteapta un numar sau 'auto', a primit '{v}'");
                            if (scale < 50 || scale > 2000)
                                return o.Fail($"--scale asteapta pixeli de senzor intre 50 si 2000, a primit {v}");
                            o.ScalePx = scale; break;
                        case "--synthetic":
                            if (v.Length == 0) return o.Fail("--synthetic asteapta calea fisierului FITS de scris, a primit sirul gol");
                            o.Synthetic = v; break;
                        case "--lens": o.Lens = v; break;
                        case "--source":
                            if (string.Equals(v, "adobe", StringComparison.OrdinalIgnoreCase)) o.Source = ProfileSource.Adobe;
                            else if (string.Equals(v, "lensfun", StringComparison.OrdinalIgnoreCase)) o.Source = ProfileSource.Lensfun;
                            else return o.Fail($"--source asteapta 'adobe' sau 'lensfun', a primit '{v}'");
                            break;
                        case "--focal":
                            if (!TryNum(v, out var focal) || focal <= 0) return o.Fail($"--focal asteapta milimetri, a primit '{v}'");
                            o.Focal = focal; break;
                        case "--fstop":
                            if (!TryNum(v, out var fstop) || fstop <= 0) return o.Fail($"--fstop asteapta un f-numar, a primit '{v}'");
                            o.FStop = fstop; break;
                        case "--crop":
                            if (!TryNum(v, out var cropf) || cropf <= 0) return o.Fail($"--crop asteapta un crop factor, a primit '{v}'");
                            o.Crop = cropf; break;
                        case "--profiles":
                            if (v.Length == 0) return o.Fail("--profiles asteapta un folder, a primit sirul gol");
                            o.Profiles = v; break;
                        case "--against":
                            if (v.Length == 0) return o.Fail("--against asteapta un al doilea flat, a primit sirul gol");
                            o.Against = v; break;
                    }
                    continue;
                }

                // un token care incepe cu '-' si nu e un numar e un flag scris gresit
                if (a.Length > 1 && a[0] == '-' && !TryNum(a, out _))
                    return o.Fail($"optiune necunoscuta: {a}");

                positional.Add(a);
            }

            // --lenses e o lista, nu o analiza: n-are nevoie de fisier de intrare.
            if (positional.Count == 0 && !o.Lenses) return o.Fail("lipseste fisierul de intrare");
            if (positional.Count > 1)
                return o.Fail("astept un singur fisier de intrare, am primit: " + string.Join(", ", positional));
            o.Image = positional.Count > 0 ? positional[0] : null;

            // Fisierul netezit nu se scrie peste intrare: ar distruge singurul master
            // fotografiat, si exact el trebuie sa ramana, ca sa se vada ce s-a scos.
            if (o.Smooth != null && SamePath(o.Smooth, o.Image))
                return o.Fail("--smooth nu poate scrie peste fisierul de intrare; da alta cale");
            if (o.Synthetic != null && SamePath(o.Synthetic, o.Image))
                return o.Fail("--synthetic nu poate scrie peste cadrul de referinta; da alta cale");

            // --sat 0 ar face rng = 0 si toate procentele ar iesi NaN/infinit
            double black = o.Black ?? 0.0;
            if (o.Sat.HasValue && o.Sat.Value <= black)
                return o.Fail($"--sat ({o.Sat.Value:F0}) trebuie sa fie mai mare decat --black ({black:F0})");

            return o;
        }

        Options Fail(string message) { Error = message; return this; }

        /// <summary>Aceeasi cale pe disc, dupa normalizare; fara distinctie de litere pe Windows.</summary>
        static bool SamePath(string a, string b)
        {
            if (a == null || b == null) return false;
            string fa, fb;
            try { fa = System.IO.Path.GetFullPath(a); fb = System.IO.Path.GetFullPath(b); }
            catch (Exception) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
            var cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return string.Equals(fa, fb, cmp);
        }

        static bool TryNum(string s, out double v)
            => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v);
    }
}
