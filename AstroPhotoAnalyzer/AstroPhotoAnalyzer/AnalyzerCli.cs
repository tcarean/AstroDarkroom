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

// AnalyzerCli.cs - the testable orchestration: parsing, running, exit code.
//
// What is HERE, and nobody else's: the arguments, the checks on the input file, writing the
// report to disk and the exit code. The rules between parameters are the core's
// (Parameters.Validate, which the window calls too), and so is the chain proper (load ->
// measure -> recommend -> format), in the core's Runs/, because the window uses it too.
//
// EXIT CODES:
//     0  OK - no recommendation above "Ok"
//     1  error (missing file or a folder, wrong argument, invalid parameter combination,
//        unusable ICC profile, format not read, linear image, -o file not written)
//     2  ATENTIE - at least one recommendation of level "Atentie"
//     3  PROBLEMA - at least one of level "Problema"
// The verdicts are monotone, so "anything != 0" means "not clean, take a look".

using System;
using System.IO;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// The console's whole run, on writers a test can pass: the banner, the arguments, the
    /// checks before any reading, the analysis, the report on stdout and in the -o file, and the
    /// exit code.
    /// </summary>
    internal static class AnalyzerCli
    {
        /// <summary>
        /// Runs the console once: the banner on lines 1-2, then --help, or an error, or the
        /// report. Every error goes to <paramref name="stderr"/> as "EROARE: ..." with exit code
        /// 1; an exception from the analysis or from writing the -o file is caught and reported
        /// so.
        /// </summary>
        /// <param name="args">
        /// The command-line arguments, without the program's name; not null.
        /// </param>
        /// <param name="stdout">Where the banner, the help and the report go.</param>
        /// <param name="stderr">Where the errors go.</param>
        /// <returns>
        /// 0 for --help or no recommendation above Ok, 1 for an error (the report may already be
        /// on stdout when only the -o file failed), 2 for an Attention, 3 for a Problem.
        /// </returns>
        internal static int Run(string[] args, TextWriter stdout, TextWriter stderr)
        {
            // The version and the copyright, on the first two lines of every run - --help and
            // the errors included.
            RunReport.WriteBanner(stdout);

            Options o = Options.Parse(args);
            if (o.Help)
            {
                WriteUsage(stdout);
                return 0;
            }
            if (o.Error != null)
            {
                stderr.WriteLine("EROARE: " + o.Error);
                return 1;
            }
            if (Directory.Exists(o.Image))
            {
                stderr.WriteLine("EROARE: unealta analizeaza un fisier, nu un folder: " + o.Image);
                return 1;
            }
            if (!File.Exists(o.Image))
            {
                stderr.WriteLine("EROARE: fisierul nu exista: " + o.Image);
                return 1;
            }

            Parameters p = ParametersFrom(o);
            if (o.Icc != null)
            {
                p = p.WithDmaxFromProfile(o.Icc, out string profileError);
                if (p == null)
                {
                    stderr.WriteLine("EROARE: --icc: " + profileError);
                    return 1;
                }
            }
            string invalid = p.Validate();
            if (invalid != null)
            {
                stderr.WriteLine("EROARE: " + invalid);
                return 1;
            }

            AnalysisResult result;
            try
            {
                result = AnalysisRun.Execute(o.Image, p);
            }
            catch (Exception e)
            {
                stderr.WriteLine("EROARE: " + e.Message);
                return 1;
            }

            stdout.Write(result.ReportBody);
            stdout.WriteLine();

            if (o.Out != null)
            {
                try
                {
                    ReportWriter.SaveFull(result, o.Out);
                    stdout.WriteLine("    -> raport salvat: " + o.Out);
                }
                catch (Exception e)
                {
                    stderr.WriteLine("EROARE: nu s-a putut scrie raportul: " + e.Message);
                    return 1;
                }
            }

            return ExitFor(result.Overall);
        }

        /// <summary>The exit code of a run's overall verdict.</summary>
        /// <param name="v">The worst verdict of the recommendations.</param>
        /// <returns>0 for Ok, 2 for Attention, 3 for Problem (and any other value).</returns>
        internal static int ExitFor(Verdict v) => v switch
        {
            Verdict.Ok => 0,
            Verdict.Attention => 2,
            _ => 3,
        };

        /// <summary>
        /// What the user typed, translated into what the core understands. A value missing from
        /// the Options means the default in Parameters - the run takes no default of its own. The
        /// --help text of WriteUsage does type most of them by hand (the widths, the passepartout,
        /// the frame, the 1.5 x diagonal, the grid, the 3 decimals) and must follow a change
        /// there.
        /// </summary>
        /// <param name="o">The options parsed, with no Error; their medium a known name.</param>
        /// <returns>The parameters, before the ICC profile is read and before Validate.</returns>
        internal static Parameters ParametersFrom(Options o)
        {
            var d = new Parameters();
            return new Parameters
            {
                Medium = o.Medium != null ? Parameters.MediumFromName(o.Medium) : d.Medium,
                DmaxOverride = o.Dmax,
                ViewingDistanceM = o.Distance,
                CandidateWidthsCm = o.Sizes ?? d.CandidateWidthsCm,
                TargetAspect = o.Aspect,
                TargetBackground = o.TargetBg,
                PassepartoutCm = o.PassepartoutCm ?? d.PassepartoutCm,
                FrameCm = o.FrameCm ?? d.FrameCm,
                WcsPath = o.Wcs,
            };
        }

        /// <summary>
        /// Writes the --help text, in Romanian: what the tool does, the options with their
        /// defaults (the media's names and the default medium from Parameters), the exit codes
        /// and the GPLv3 warranty lines.
        /// </summary>
        /// <param name="w">The writer, the console's stdout.</param>
        internal static void WriteUsage(TextWriter w)
        {
            w.WriteLine();
            w.WriteLine("Masoara o imagine astro finita (TIFF, PNG sau FITS stretch-uit) si spune");
            w.WriteLine("cum sa fie trimisa la print: format, fundal, culoare, marime, denoise.");
            w.WriteLine();
            w.WriteLine("Folosire:");
            w.WriteLine("  AstroPhotoAnalyzer <imagine> [optiuni]");
            w.WriteLine();
            w.WriteLine("Optiuni:");
            w.WriteLine($"  --medium <{string.Join("|", Options.Media)}>   mediul de print; da Dmax-ul hartiei (implicit {Parameters.NameOf(new Parameters().Medium)})");
            w.WriteLine("  --dmax <n>                          Dmax explicit, in locul mediului (ex. 2.0)");
            w.WriteLine("  --icc <profil.icc>                  profilul ICC al hartiei pe imprimanta ei; da Dmax-ul, in locul mediului");
            w.WriteLine("  --distance <m>                      distanta de privire, in metri (implicit 1.5 x diagonala)");
            w.WriteLine("  --sizes <cm,cm,...>                 latimile de print candidate (implicit 50,60,70,80,90,100)");
            w.WriteLine("  --aspect <l:i>                      raportul de aspect tinta, ex. 50:35 (implicit cel nativ)");
            w.WriteLine("  --target-bg <0..1>                  o tinta in plus in simularea de print, pe langa grila 0.05..0.20 (strict intre 0 si 1, 3 zecimale)");
            w.WriteLine("  --passepartout <cm>                 latimea passepartout-ului (implicit 7; cel putin dublul ramei, 0 = fara)");
            w.WriteLine("  --frame <cm>                        latimea ramei (implicit 3)");
            w.WriteLine("  --wcs <fisier.fit>                  FITS plate-solved de aceeasi marime, al carui WCS se foloseste pentru norii de praf");
            w.WriteLine("  -o, --out <fisier>                  scrie raportul si in fisier, pe langa stdout");
            w.WriteLine("  -h, --help                          acest text");
            w.WriteLine();
            w.WriteLine("Coduri de iesire: 0 ok, 1 eroare, 2 atentie, 3 problema.");
            w.WriteLine();
            foreach (string line in RunReport.WarrantyLines) w.WriteLine(line);
        }
    }
}
