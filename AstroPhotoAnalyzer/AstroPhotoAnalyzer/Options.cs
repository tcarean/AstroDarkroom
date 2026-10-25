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
// The flags may come anywhere: "AstroPhotoAnalyzer --medium canvas poza.tif" and
// "AstroPhotoAnalyzer poza.tif --medium canvas" are equivalent. Parse does not throw: it
// returns an Options with Error set, and AnalyzerCli writes it to stderr and exits with 1.
//
// The values here are ONLY what the user typed, checked for syntax. What they mean for the
// analysis (a medium's Dmax, the default viewing distance) is decided in the core, in
// Parameters - the console does not know what a baryta is.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// The command line as the user typed it, checked for syntax only: the image, one field per
    /// flag (null for a flag not given, which means the core's default), Help, and Error, the
    /// first problem found. Built by <see cref="Parse"/>.
    /// </summary>
    internal sealed class Options
    {
        internal string Image;

        /// <summary>
        /// The print medium, as text: baryta, canvas, acrilic. Null = the core's default.
        /// </summary>
        internal string Medium;

        /// <summary>An explicit Dmax, which beats the medium. Null = from the medium.</summary>
        internal double? Dmax;

        /// <summary>
        /// The viewing distance, in metres. Null = the core infers it from the diagonal.
        /// </summary>
        internal double? Distance;

        /// <summary>
        /// The candidate print widths, in cm. Null = the core's default list.
        /// </summary>
        internal double[] Sizes;

        /// <summary>
        /// The target aspect ratio, width/height. Null = the image's native one.
        /// </summary>
        internal double? Aspect;

        /// <summary>
        /// The target background for the print simulation, strictly between 0 and 1 after the
        /// core's rounding (R-023). Null = the core's default.
        /// </summary>
        internal double? TargetBg;

        internal double? PassepartoutCm;
        internal double? FrameCm;

        /// <summary>
        /// The FITS whose WCS is lent to the image (R-027). Null = the file's own WCS, if it has
        /// one.
        /// </summary>
        internal string Wcs;

        /// <summary>
        /// The ICC printer profile the Dmax is read from (R-028); it excludes --dmax.
        /// </summary>
        internal string Icc;

        /// <summary>Where the report is also written, besides stdout. Null = stdout only.</summary>
        internal string Out;

        internal bool Help;
        internal string Error;

        static readonly HashSet<string> BoolFlags = new() { "-h", "--help" };

        static readonly HashSet<string> ValueFlags = new()
        { "--medium", "--dmax", "--distance", "--sizes", "--aspect", "--target-bg",
          "--passepartout", "--frame", "--wcs", "--icc", "-o", "--out" };

        internal static readonly string[] Media = Parameters.MediumNames.ToArray();

        /// <summary>
        /// Parses the command line, flags before or after the image; it does not throw on any
        /// input.
        /// </summary>
        /// <param name="args">The arguments, without the program's name; not null.</param>
        /// <returns>
        /// Help set when there are no arguments or -h or --help is among them; otherwise Error
        /// set to the first problem, a sentence in the interface language without the "ERROR: "
        /// prefix; otherwise
        /// the image and the values of the flags given.
        /// </returns>
        internal static Options Parse(string[] args)
        {
            // --lang is AnalyzerCli's, read before this parse so that its messages are already in
            // the language chosen (R-150); here it is as if it were not typed.
            args = UiLanguageArgs.Strip(args);
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
                        return o.Fail(Tr.Pick($"the value for {a} is missing", $"lipseste valoarea pentru {a}"));
                    string v = args[i + 1];
                    if (BoolFlags.Contains(v) || ValueFlags.Contains(v))
                        return o.Fail(Tr.Pick($"{a} expects a value, but got the flag {v}", $"{a} asteapta o valoare, dar a primit flagul {v}"));
                    i++;

                    switch (a)
                    {
                        case "--medium":
                            string m = v.ToLowerInvariant();
                            if (Array.IndexOf(Media, m) < 0)
                                return o.Fail(Tr.Pick($"--medium expects {string.Join(", ", Media)}; got '{v}'", $"--medium asteapta {string.Join(", ", Media)}; a primit '{v}'"));
                            o.Medium = m; break;
                        case "--dmax":
                            if (!TryNum(v, out var dmax) || dmax <= 0 || dmax > Thresholds.DmaxMax)
                                return o.Fail(Tr.Pick($"--dmax expects a number between 0 and {Fmt.F0(Thresholds.DmaxMax)}, got '{v}'", $"--dmax asteapta un numar intre 0 si {Fmt.F0(Thresholds.DmaxMax)}, a primit '{v}'"));
                            o.Dmax = dmax; break;
                        case "--distance":
                            if (!TryNum(v, out var dist) || dist <= 0)
                                return o.Fail(Tr.Pick($"--distance expects metres, a positive number, got '{v}'", $"--distance asteapta metri, un numar pozitiv, a primit '{v}'"));
                            o.Distance = dist; break;
                        case "--sizes":
                            var sizes = new List<double>();
                            foreach (string s in v.Split(',', StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (!TryNum(s.Trim(), out var cm) || cm <= 0)
                                    return o.Fail(Tr.Pick($"--sizes expects widths in cm separated by commas, got '{v}'", $"--sizes asteapta latimi in cm separate prin virgula, a primit '{v}'"));
                                sizes.Add(cm);
                            }
                            if (sizes.Count == 0) return o.Fail(Tr.Pick("--sizes expects at least one width", "--sizes asteapta cel putin o latime"));
                            o.Sizes = sizes.ToArray(); break;
                        case "--aspect":
                            if (!TryAspect(v, out var asp))
                                return o.Fail(Tr.Pick($"--aspect expects width:height (e.g. 50:35), got '{v}'", $"--aspect asteapta latime:inaltime (ex. 50:35), a primit '{v}'"));
                            o.Aspect = asp; break;
                        case "--target-bg":
                            // The range and the rounding are the core's to judge, with the same
                            // message as the window's (R-023).
                            if (!TryNum(v, out var bg))
                                return o.Fail(Tr.Pick($"--target-bg expects a number strictly between 0 and 1, got '{v}'", $"--target-bg asteapta un numar strict intre 0 si 1, a primit '{v}'"));
                            string bgError = Parameters.TargetBackgroundError(bg);
                            if (bgError != null) return o.Fail("--target-bg: " + bgError);
                            o.TargetBg = bg; break;
                        case "--passepartout":
                            if (!TryNum(v, out var pp) || pp < 0)
                                return o.Fail(Tr.Pick($"--passepartout expects cm, a number >= 0, got '{v}'", $"--passepartout asteapta cm, un numar >= 0, a primit '{v}'"));
                            o.PassepartoutCm = pp; break;
                        case "--frame":
                            if (!TryNum(v, out var fr) || fr < 0)
                                return o.Fail(Tr.Pick($"--frame expects cm, a number >= 0, got '{v}'", $"--frame asteapta cm, un numar >= 0, a primit '{v}'"));
                            o.FrameCm = fr; break;
                        case "--wcs":
                            if (v.Length == 0) return o.Fail(Tr.Pick("--wcs expects the path of a plate-solved FITS", "--wcs asteapta calea unui FITS plate-solved"));
                            o.Wcs = v; break;
                        case "--icc":
                            if (v.Length == 0) return o.Fail(Tr.Pick("--icc expects the path of a printer ICC profile", "--icc asteapta calea unui profil ICC de imprimanta"));
                            o.Icc = v; break;
                        case "-o":
                        case "--out":
                            if (v.Length == 0) return o.Fail(Tr.Pick("-o expects the path of the report file", "-o asteapta calea fisierului de raport"));
                            o.Out = v; break;
                    }
                    continue;
                }

                if (a.StartsWith("-"))
                    return o.Fail(Tr.Pick($"unknown option: {a}", $"optiune necunoscuta: {a}"));

                positional.Add(a);
            }

            if (positional.Count == 0) return o.Fail(Tr.Pick("the file to analyse is missing", "lipseste fisierul de analizat"));
            if (positional.Count > 1) return o.Fail(Tr.Pick("only one file to analyse; got ", "un singur fisier de analizat; a primit ") + positional.Count);
            if (o.Icc != null && o.Dmax.HasValue) return o.Fail(Tr.Pick("--icc and --dmax both give the paper's Dmax; choose one", "--icc si --dmax dau amandoua Dmax-ul hartiei; alege unul"));
            o.Image = positional[0];
            return o;
        }

        /// <summary>Records the problem in <see cref="Error"/>.</summary>
        /// <param name="message">
        /// The problem, a sentence in the interface language without the "ERROR: " prefix
        /// AnalyzerCli adds.
        /// </param>
        /// <returns>This instance, for Parse to return.</returns>
        Options Fail(string message)
        {
            Error = message;
            return this;
        }

        /// <summary>
        /// Reads a number in the invariant culture (a decimal point, no thousands separator).
        /// "NaN", "Infinity" and "-Infinity" read too, as those values; the callers' range checks
        /// refuse the infinities they bound, --target-bg's (TargetBackgroundError) refuses NaN,
        /// and what passes them is refused by the core's Parameters.Validate, which AnalyzerCli
        /// calls before reading the image (R-024).
        /// </summary>
        /// <param name="s">The text typed.</param>
        /// <param name="v">The number read; 0 when the text does not read.</param>
        /// <returns><c>true</c> when the text reads as a double.</returns>
        static bool TryNum(string s, out double v)
            => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

        /// <summary>
        /// "50:35" or "50x35" -> 50/35. Both forms, because both are typed naturally.
        /// </summary>
        /// <param name="s">
        /// The text typed: two numbers separated by ':', 'x' or 'X' (empty parts are dropped).
        /// </param>
        /// <param name="ratio">
        /// The width over the height; 0 when the text is refused, and also when the quotient
        /// underflows.
        /// </param>
        /// <returns>
        /// <c>true</c> unless the text has not exactly two parts, a part does not read
        /// (TryNum), or a part is not a finite positive number: "NaN" and "Infinity" read, but a
        /// side that is one is refused here, where the typed text is at hand for the message,
        /// rather than as the 0, infinity or NaN ratio it would give (R-024, ADR 0045).
        /// </returns>
        static bool TryAspect(string s, out double ratio)
        {
            ratio = 0;
            string[] parts = s.Split(new[] { ':', 'x', 'X' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return false;
            if (!TryNum(parts[0], out var w) || !TryNum(parts[1], out var h)) return false;
            if (!double.IsFinite(w) || !double.IsFinite(h) || w <= 0 || h <= 0) return false;
            ratio = w / h;
            return true;
        }
    }
}
