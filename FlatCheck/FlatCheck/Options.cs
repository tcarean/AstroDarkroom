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

// Options.cs - the parsing of the command-line arguments.
//
// Since phase 8 the flags may come anywhere: "flatcheck --expo f.fit" and
// "flatcheck f.fit --expo" are equivalent. Before, args[0] was ALWAYS the image's path, so a
// flag put first was swallowed as a file name and failed with
// "Could not find file '--expo'".
//
// Parse does not throw: it returns an Options with Error set, and FlatCheckCli writes it to
// stderr and exits with 1.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>
    /// The console's command line, parsed: the input file, the mode flags and their values,
    /// or the error that stopped the parse.
    /// </summary>
    internal sealed class Options
    {
        internal string Image;
        internal string Ratio;
        internal bool Light;
        internal bool Expo;
        internal bool Sky;

        // Mode 5: Siril's "Histogram" view. It has no verdict; -o is where the picture is
        // written, and --rainbow colours it with Siril's false-colour table (the one-layer
        // view, ADR 0063). --rainbow alone is an error: it modifies this mode's picture only.
        internal bool Histeq;
        internal bool Rainbow;

        // Mode 6: the smoothed flat. The value is the PATH of the FITS file to write -
        // explicit, with no default name: a mode that writes 96 MB beside the master must be
        // told where. -o stays the map. --scale is the smoothing scale, in sensor pixels.
        internal string Smooth;
        internal double ScalePx = SmoothAnalyzer.DefaultScalePx;

        // The synthetic flat and the comparison. --lenses needs no input file.
        internal string Synthetic;
        internal string Lens;
        internal ProfileSource? Source;
        internal double? Focal;
        internal double? FStop;
        internal double? Crop;
        internal string Profiles;
        internal bool Lenses;
        internal string Against;

        /// <summary>
        /// A raw from beside the master, for the structure mode: the aperture and the lens ID
        /// are read from it, as in the window, and the master's vignetting is compared with the
        /// lens's profiles. With --lens instead, the lenses come from the text.
        /// </summary>
        internal string Raw;

        // --scale auto: the tool proposes the scale (SmoothScaleAdvisor) and smooths at it;
        // with no texture or undetermined, it writes nothing and exits with 2.
        internal bool ScaleAuto;
        internal string OutPng = "flatcheck.png";
        internal double? Sat;
        internal double? Black;

        // The file pattern for --light on a folder. The default and its reason sit in
        // SeriesFinder, beside the matching itself - the WPF application uses the same
        // convention, and it has no command line to receive it from.
        internal string Pattern = SeriesFinder.DefaultPattern;

        // The diagonal field, in degrees, declared by the user. The way out for when the header
        // does not say enough (a DSLR without XPIXSZ) or when the conservative full-frame
        // assumption does a small sensor an injustice. It is not a force flag: it tells the
        // tool the truth, it does not ask it to ignore it.
        internal double? Fov;

        internal bool Help;
        internal string Error;

        static readonly HashSet<string> BoolFlags = new()
        { "--light", "--expo", "--sky", "--histeq", "--rainbow", "--lenses", "-h", "--help" };

        static readonly HashSet<string> ValueFlags = new()
        { "--ratio", "-o", "--out", "--sat", "--black", "--pattern", "--fov", "--smooth", "--scale",
          "--synthetic", "--lens", "--source", "--focal", "--fstop", "--crop", "--profiles", "--against", "--raw" };

        /// <summary>
        /// Parses the command line: flags anywhere, one positional input file (none needed with
        /// --lenses), each value checked for its type and range. Does not throw.
        /// </summary>
        /// <param name="args">The arguments as the process received them.</param>
        /// <returns>The options; <see cref="Help"/> set for no argument or -h/--help, and
        /// <see cref="Error"/> set, in <see cref="Ui.Current"/>, when the line is wrong.</returns>
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
                    else if (a == "--rainbow") o.Rainbow = true;
                    else if (a == "--lenses") o.Lenses = true;
                    continue;
                }

                if (ValueFlags.Contains(a))
                {
                    if (i + 1 >= args.Length)
                        return o.Fail(T($"missing value for {a}", $"lipseste valoarea pentru {a}"));
                    string v = args[i + 1];
                    if (BoolFlags.Contains(v) || ValueFlags.Contains(v))
                        return o.Fail(T($"{a} expects a value, but got the flag {v}", $"{a} asteapta o valoare, dar a primit flagul {v}"));
                    i++;

                    switch (a)
                    {
                        case "--ratio": o.Ratio = v; break;
                        case "-o":
                        case "--out": o.OutPng = v; break;
                        case "--sat":
                            if (!TryNum(v, out var sat)) return o.Fail(T($"--sat expects a number, got '{v}'", $"--sat asteapta un numar, a primit '{v}'"));
                            o.Sat = sat; break;
                        case "--black":
                            if (!TryNum(v, out var blk)) return o.Fail(T($"--black expects a number, got '{v}'", $"--black asteapta un numar, a primit '{v}'"));
                            o.Black = blk; break;
                        case "--pattern":
                            if (v.Length == 0) return o.Fail(T("--pattern expects a file pattern, got an empty string", "--pattern asteapta un tipar, a primit sirul gol"));
                            o.Pattern = v; break;
                        case "--fov":
                            if (!TryNum(v, out var fov)) return o.Fail(T($"--fov expects a number, got '{v}'", $"--fov asteapta un numar, a primit '{v}'"));
                            if (fov <= 0 || fov >= 180)
                                return o.Fail(T($"--fov expects degrees between 0 and 180, got {v}", $"--fov asteapta grade intre 0 si 180, a primit {v}"));
                            o.Fov = fov; break;
                        case "--smooth":
                            if (v.Length == 0) return o.Fail(T("--smooth expects the path of the FITS file to write, got an empty string", "--smooth asteapta calea fisierului FITS de scris, a primit sirul gol"));
                            o.Smooth = v; break;
                        case "--scale":
                            if (string.Equals(v, "auto", StringComparison.OrdinalIgnoreCase)) { o.ScaleAuto = true; break; }
                            if (!TryNum(v, out var scale)) return o.Fail(T($"--scale expects a number or 'auto', got '{v}'", $"--scale asteapta un numar sau 'auto', a primit '{v}'"));
                            if (scale < 50 || scale > 2000)
                                return o.Fail(T($"--scale expects sensor pixels between 50 and 2000, got {v}", $"--scale asteapta pixeli de senzor intre 50 si 2000, a primit {v}"));
                            o.ScalePx = scale; break;
                        case "--synthetic":
                            if (v.Length == 0) return o.Fail(T("--synthetic expects the path of the FITS file to write, got an empty string", "--synthetic asteapta calea fisierului FITS de scris, a primit sirul gol"));
                            o.Synthetic = v; break;
                        case "--lens": o.Lens = v; break;
                        case "--source":
                            if (string.Equals(v, "adobe", StringComparison.OrdinalIgnoreCase)) o.Source = ProfileSource.Adobe;
                            else if (string.Equals(v, "lensfun", StringComparison.OrdinalIgnoreCase)) o.Source = ProfileSource.Lensfun;
                            else return o.Fail(T($"--source expects 'adobe' or 'lensfun', got '{v}'", $"--source asteapta 'adobe' sau 'lensfun', a primit '{v}'"));
                            break;
                        case "--focal":
                            if (!TryNum(v, out var focal) || focal <= 0) return o.Fail(T($"--focal expects millimetres, got '{v}'", $"--focal asteapta milimetri, a primit '{v}'"));
                            o.Focal = focal; break;
                        case "--fstop":
                            if (!TryNum(v, out var fstop) || fstop <= 0) return o.Fail(T($"--fstop expects an f-number, got '{v}'", $"--fstop asteapta un f-numar, a primit '{v}'"));
                            o.FStop = fstop; break;
                        case "--crop":
                            if (!TryNum(v, out var cropf) || cropf <= 0) return o.Fail(T($"--crop expects a crop factor, got '{v}'", $"--crop asteapta un crop factor, a primit '{v}'"));
                            o.Crop = cropf; break;
                        case "--profiles":
                            if (v.Length == 0) return o.Fail(T("--profiles expects a folder, got an empty string", "--profiles asteapta un folder, a primit sirul gol"));
                            o.Profiles = v; break;
                        case "--raw":
                            if (v.Length == 0) return o.Fail(T("--raw expects a raw file, got an empty string", "--raw asteapta un fisier raw, a primit sirul gol"));
                            o.Raw = v; break;
                        case "--against":
                            if (v.Length == 0) return o.Fail(T("--against expects a second flat, got an empty string", "--against asteapta un al doilea flat, a primit sirul gol"));
                            o.Against = v; break;
                    }
                    continue;
                }

                // a token that starts with '-' and is not a number is a misspelt flag
                if (a.Length > 1 && a[0] == '-' && !TryNum(a, out _))
                    return o.Fail(T($"unknown option: {a}", $"optiune necunoscuta: {a}"));

                positional.Add(a);
            }

            // --lenses is a list, not an analysis: it needs no input file.
            if (positional.Count == 0 && !o.Lenses) return o.Fail(T("missing input file", "lipseste fisierul de intrare"));
            if (positional.Count > 1)
                return o.Fail(T("expected a single input file, got: ", "astept un singur fisier de intrare, am primit: ") + string.Join(", ", positional));
            o.Image = positional.Count > 0 ? positional[0] : null;

            // The smoothed file is not written over the input: it would destroy the only
            // master photographed, and exactly that one must stay, so one can see what was
            // removed.
            if (o.Smooth != null && SamePath(o.Smooth, o.Image))
                return o.Fail(T("--smooth cannot write over the input file; give another path", "--smooth nu poate scrie peste fisierul de intrare; da alta cale"));
            if (o.Synthetic != null && SamePath(o.Synthetic, o.Image))
                return o.Fail(T("--synthetic cannot write over the reference frame; give another path", "--synthetic nu poate scrie peste cadrul de referinta; da alta cale"));

            // --rainbow modifies the --histeq picture and nothing else: alone it is a typo, and
            // a plain picture written in silence would hide it (R-104).
            if (o.Rainbow && !o.Histeq)
                return o.Fail(T("--rainbow goes with --histeq: it colours that mode's picture", "--rainbow merge cu --histeq: coloreaza poza acelui mod"));

            // --sat 0 would make rng = 0 and every percentage would come out NaN/infinite
            double black = o.Black ?? 0.0;
            if (o.Sat.HasValue && o.Sat.Value <= black)
                return o.Fail(T($"--sat ({o.Sat.Value:F0}) must be greater than --black ({black:F0})", $"--sat ({o.Sat.Value:F0}) trebuie sa fie mai mare decat --black ({black:F0})"));

            return o;
        }

        /// <summary>Records the parse error.</summary>
        /// <param name="message">The error, in the run's language, as the user will read it.</param>
        /// <returns>This instance, so a parse can end with <c>return o.Fail(...)</c>.</returns>
        Options Fail(string message) { Error = message; return this; }

        /// <summary>The same path on disk, after normalisation; case-insensitive on
        /// Windows.</summary>
        /// <param name="a">A path, or null.</param>
        /// <param name="b">Another path, or null.</param>
        /// <returns>True when both name the same file; false when either is null. A path
        /// that cannot be normalised is compared as text, ignoring case.</returns>
        static bool SamePath(string a, string b)
        {
            if (a == null || b == null) return false;
            string fa, fb;
            try { fa = System.IO.Path.GetFullPath(a); fb = System.IO.Path.GetFullPath(b); }
            catch (Exception) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
            var cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            return string.Equals(fa, fb, cmp);
        }

        /// <summary>Parses a number in the invariant culture (a dot for the decimals).</summary>
        /// <param name="s">The text.</param>
        /// <param name="v">The number, or 0 when the text is not one.</param>
        /// <returns>True when the text is a number.</returns>
        static bool TryNum(string s, out double v)
            => double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v);
    }
}
