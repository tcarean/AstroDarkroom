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
using NLog;

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
        /// The diagnostic log's console lines (R-171): the mode, every refusal as the user reads
        /// it, a run that failed with its exception, the report written. A static field: this
        /// type is first used after <c>Program.Main</c> configured the log (ADR 0069).
        /// </summary>
        static readonly Logger Log = LogManager.GetLogger("Cli");

        /// <summary>
        /// Runs the console once, in the language of --lang, English without one (R-151): the
        /// banner on lines 1-2, then --help, or an error, or the report. Every error goes to
        /// <paramref name="stderr"/> as "ERROR: ..." ("EROARE: ..." in Romanian) with exit code
        /// 1, a wrong --lang included, in English (R-152); an exception from the analysis or from
        /// writing the -o file is caught and reported so.
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
            UiLanguageArgs.Find(args, out UiLanguage? language, out string languageError);
            using (Tr.Use(language ?? UiLanguage.English))
                return RunInLanguage(args, languageError, stdout, stderr);
        }

        /// <summary>
        /// <see cref="Run"/> once the language is in use: the banner, then the --lang error, or
        /// the rest of the run. Every refusal, the failure of a run and the report written also
        /// go to the diagnostic log, the refusals as the user reads them, the failure with its
        /// exception, whose stack stderr does not show (R-171).
        /// </summary>
        /// <param name="args">The command-line arguments, --lang included; not null.</param>
        /// <param name="languageError">
        /// The problem with --lang, an English sentence; null when there is none.
        /// </param>
        /// <param name="stdout">Where the banner, the help and the report go.</param>
        /// <param name="stderr">Where the errors go.</param>
        /// <returns>The exit code, as <see cref="Run"/> describes it.</returns>
        static int RunInLanguage(string[] args, string languageError, TextWriter stdout, TextWriter stderr)
        {
            // The version and the copyright, on the first two lines of every run - --help and
            // the errors included.
            RunReport.WriteBanner(stdout);
            string error = Tr.Pick("ERROR: ", "EROARE: ");

            if (languageError != null)
            {
                Log.Warn("refused: " + languageError);
                stderr.WriteLine(error + languageError);
                return 1;
            }

            Options o = Options.Parse(args);
            if (o.Help)
            {
                Log.Info("mode=help");
                WriteUsage(stdout);
                return 0;
            }
            if (o.Error != null)
            {
                Log.Warn("refused the arguments: " + o.Error);
                stderr.WriteLine(error + o.Error);
                return 1;
            }

            // The arguments are on the log's first line already; the parameters are on the run's.
            Log.Info("mode=analysis image=" + o.Image + " out=" + LogText.Str(o.Out) + " lang=" + (Tr.Current == UiLanguage.Romanian ? "ro" : "en"));
            if (Directory.Exists(o.Image))
                return Refuse(stderr, error, Tr.Pick("the tool analyses a file, not a folder: ", "unealta analizeaza un fisier, nu un folder: ") + o.Image);
            if (!File.Exists(o.Image))
                return Refuse(stderr, error, Tr.Pick("the file does not exist: ", "fisierul nu exista: ") + o.Image);

            Parameters p = ParametersFrom(o);
            if (o.Icc != null)
            {
                p = p.WithDmaxFromProfile(o.Icc, out string profileError);
                if (p == null) return Refuse(stderr, error, "--icc: " + profileError);
            }
            string invalid = p.Validate();
            if (invalid != null) return Refuse(stderr, error, invalid);

            AnalysisResult result;
            try
            {
                result = AnalysisRun.Execute(o.Image, p);
            }
            catch (RefusedImageException e)
            {
                // A linear image is a refusal, not a failure: the run's done line has its figures.
                return Refuse(stderr, error, e.Message);
            }
            catch (Exception e)
            {
                // The stack lives in the log; stderr keeps its one line.
                Log.Error(e, "run failed: mode=analysis");
                stderr.WriteLine(error + e.Message);
                return 1;
            }

            stdout.Write(result.ReportBody);
            stdout.WriteLine();

            if (o.Out != null)
            {
                try
                {
                    ReportWriter.SaveFull(result, o.Out);
                    Log.Info(LogText.Wrote(o.Out));
                    stdout.WriteLine(Tr.Pick("    -> report saved: ", "    -> raport salvat: ") + o.Out);
                }
                catch (Exception e)
                {
                    Log.Error(e, "the report could not be written: " + o.Out);
                    stderr.WriteLine(error + Tr.Pick("the report could not be written: ", "nu s-a putut scrie raportul: ") + e.Message);
                    return 1;
                }
            }

            return ExitFor(result.Overall);
        }

        /// <summary>
        /// A refusal before or instead of a report: one line on stderr, the same in the
        /// diagnostic log at Warn, and the exit code 1.
        /// </summary>
        /// <param name="stderr">Where the errors go.</param>
        /// <param name="error">The error prefix in the interface language: <c>ERROR: </c> or <c>EROARE: </c>.</param>
        /// <param name="message">Why, as the user reads it.</param>
        /// <returns>1, the exit code of an error.</returns>
        static int Refuse(TextWriter stderr, string error, string message)
        {
            Log.Warn("refused: " + message);
            stderr.WriteLine(error + message);
            return 1;
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
        /// Writes the --help text, in the interface language: what the tool does, the options
        /// with their defaults (the media's names and the default medium from Parameters), the
        /// exit codes and the GPLv3 warranty lines.
        /// </summary>
        /// <param name="w">The writer, the console's stdout.</param>
        internal static void WriteUsage(TextWriter w)
        {
            string media = string.Join("|", Options.Media);
            string medium = Parameters.NameOf(new Parameters().Medium);
            w.WriteLine();
            w.WriteLine(Tr.Pick("Measures a finished astro image (a stretched TIFF, PNG or FITS) and says",
                                "Masoara o imagine astro finita (TIFF, PNG sau FITS stretch-uit) si spune"));
            w.WriteLine(Tr.Pick("how to send it to print: format, background, colour, size, denoise.",
                                "cum sa fie trimisa la print: format, fundal, culoare, marime, denoise."));
            w.WriteLine();
            w.WriteLine(Tr.Pick("Usage:", "Folosire:"));
            w.WriteLine(Tr.Pick("  AstroPhotoAnalyzer <image> [options]", "  AstroPhotoAnalyzer <imagine> [optiuni]"));
            w.WriteLine();
            w.WriteLine(Tr.Pick("Options:", "Optiuni:"));
            w.WriteLine(Tr.Pick($"  --medium <{media}>   the print medium; gives the paper's Dmax (default {medium})",
                                $"  --medium <{media}>   mediul de print; da Dmax-ul hartiei (implicit {medium})"));
            w.WriteLine(Tr.Pick("  --dmax <n>                          an explicit Dmax, instead of the medium's (e.g. 2.0)",
                                "  --dmax <n>                          Dmax explicit, in locul mediului (ex. 2.0)"));
            w.WriteLine(Tr.Pick("  --icc <profile.icc>                 the ICC profile of the paper on its printer; gives the Dmax, instead of the medium",
                                "  --icc <profil.icc>                  profilul ICC al hartiei pe imprimanta ei; da Dmax-ul, in locul mediului"));
            w.WriteLine(Tr.Pick("  --distance <m>                      the viewing distance, in metres (default 1.5 x the diagonal)",
                                "  --distance <m>                      distanta de privire, in metri (implicit 1.5 x diagonala)"));
            w.WriteLine(Tr.Pick("  --sizes <cm,cm,...>                 the candidate print widths (default 50,60,70,80,90,100)",
                                "  --sizes <cm,cm,...>                 latimile de print candidate (implicit 50,60,70,80,90,100)"));
            w.WriteLine(Tr.Pick("  --aspect <w:h>                      the target aspect ratio, e.g. 50:35 (default the native one)",
                                "  --aspect <l:i>                      raportul de aspect tinta, ex. 50:35 (implicit cel nativ)"));
            w.WriteLine(Tr.Pick("  --target-bg <0..1>                  one more target in the print simulation, besides the 0.05..0.20 grid (strictly between 0 and 1, 3 decimals)",
                                "  --target-bg <0..1>                  o tinta in plus in simularea de print, pe langa grila 0.05..0.20 (strict intre 0 si 1, 3 zecimale)"));
            w.WriteLine(Tr.Pick("  --passepartout <cm>                 the width of the passepartout (default 7; at least twice the frame, 0 = none)",
                                "  --passepartout <cm>                 latimea passepartout-ului (implicit 7; cel putin dublul ramei, 0 = fara)"));
            w.WriteLine(Tr.Pick("  --frame <cm>                        the width of the frame (default 3)",
                                "  --frame <cm>                        latimea ramei (implicit 3)"));
            w.WriteLine(Tr.Pick("  --wcs <file.fit>                    a plate-solved FITS of the same size, whose WCS is used for the dust clouds",
                                "  --wcs <fisier.fit>                  FITS plate-solved de aceeasi marime, al carui WCS se foloseste pentru norii de praf"));
            w.WriteLine(Tr.Pick("  --lang <en|ro>                      the interface language: en English, ro Romanian (default en)",
                                "  --lang <en|ro>                      limba interfetei: en engleza, ro romana (implicit en)"));
            w.WriteLine(Tr.Pick("  -o, --out <file>                    also writes the report to a file, besides stdout",
                                "  -o, --out <fisier>                  scrie raportul si in fisier, pe langa stdout"));
            w.WriteLine(Tr.Pick("  -h, --help                          this text",
                                "  -h, --help                          acest text"));
            w.WriteLine();
            w.WriteLine(Tr.Pick("Exit codes: 0 ok, 1 error, 2 caution, 3 problem.",
                                "Coduri de iesire: 0 ok, 1 eroare, 2 atentie, 3 problema."));
            w.WriteLine();
            foreach (string line in RunReport.WarrantyLines) w.WriteLine(line);
        }
    }
}
