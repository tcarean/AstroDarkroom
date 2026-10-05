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

// AnalyzerCli.cs - orchestrarea testabila: parsare, rulare, cod de iesire.
//
// Ce e AICI, si al nimanui altcuiva: argumentele, validarea intrarii, scrierea
// raportului pe disc si codul de iesire. Lantul propriu-zis (incarca -> masoara ->
// recomanda -> formateaza) sta in Runs/ din nucleu, fiindca il foloseste si fereastra.
//
// CODURI DE IESIRE:
//     0  OK - nicio recomandare peste "Ok"
//     1  eroare (fisier lipsa, argument gresit, format necitit, imagine liniara)
//     2  ATENTIE - cel putin o recomandare de nivel "Atentie"
//     3  PROBLEMA - cel putin una de nivel "Problema"
// Verdictele sunt monotone, deci "orice != 0" inseamna "nu e curat, uita-te".

using System;
using System.IO;

namespace AstroPhotoAnalyzer
{
    internal static class AnalyzerCli
    {
        internal static int Run(string[] args, TextWriter stdout, TextWriter stderr)
        {
            // Versiunea si copyright-ul, pe primele doua linii ale oricarei rulari -
            // inclusiv --help si erorile.
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
                    using var f = new StreamWriter(o.Out, false);
                    ReportWriter.WriteFull(result, f);
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

        internal static int ExitFor(Verdict v) => v switch
        {
            Verdict.Ok => 0,
            Verdict.Attention => 2,
            _ => 3,
        };

        /// <summary>
        /// Ce a tastat omul, tradus in ce intelege nucleul. Un Options lipsa inseamna
        /// implicitul din Parameters - consola nu-si tine copia ei de implicite.
        /// </summary>
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
            w.WriteLine("  --icc <profil.icc>                  profilul ICC al hartiei pe imprimanta ei: Dmax-ul se citeste din el, in locul mediului");
            w.WriteLine("  --distance <m>                      distanta de privire, in metri (implicit 1.5 x diagonala)");
            w.WriteLine("  --sizes <cm,cm,...>                 latimile de print candidate (implicit 50,60,70,80,90,100)");
            w.WriteLine("  --aspect <l:i>                      raportul de aspect tinta, ex. 50:35 (implicit cel nativ)");
            w.WriteLine("  --target-bg <0..1>                  o tinta in plus in simularea de print, pe langa grila 0.10..0.20");
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
