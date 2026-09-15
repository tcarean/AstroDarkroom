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

// FlatCheckCli.cs - orchestrarea testabila: parsare, dispecerat pe moduri, cod de iesire.
// Ordinea de dispecerat: --histeq -> --expo -> --ratio -> --light -> structura (implicit).
//
// CE A MAI RAMAS AICI, dupa ce nucleul a devenit FlatCheck.Core: exact ce e al consolei
// si al nimanui altcuiva - argumentele, alegerea modului, scrierea hartii pe disc si
// codul de iesire. Lantul propriu-zis al fiecarui mod (incarca -> analizeaza ->
// raporteaza -> encodeaza harta) sta in Runs/, fiindca il foloseste si aplicatia WPF.
// Vezi comentariul din Runs/RunOutcome.cs pentru de ce s-a mutat exact acolo si nu mai
// aproape.
//
// CODURI DE IESIRE (din faza 8 - inainte era mereu 0, ceea ce facea imposibila
// folosirea in scripturi):
//     0  OK
//     1  eroare (fisier lipsa, argument gresit, FITS invalid, fit esuat)
//     2  SUSPECT
//     3  PROBLEMA
// Verdictele sunt monotone, deci "orice != 0" inseamna "nu e curat, uita-te".
// --histeq n-are verdict: 0 inseamna doar ca a scris poza.

using System;
using System.IO;

namespace FlatCheck
{
    internal static class FlatCheckCli
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

            // Un folder inseamna "ruleaza pe serie", si asta are sens doar la --light.
            // Celelalte moduri lucreaza pe un master sau pe un cadru anume.
            if (Directory.Exists(o.Image) && !o.Light)
            {
                stderr.WriteLine("EROARE: un folder se poate da doar cu --light, pentru rularea pe serie. "
                               + "Celelalte moduri asteapta un fisier.");
                return 1;
            }

            int code;
            try
            {
                if (o.Histeq) code = RunHisteq(o, stdout);
                else if (o.Expo) code = ExitFor(RunExpo(o, stdout));
                else if (o.Ratio != null) code = ExitFor(RunRatio(o, stdout));
                else if (o.Light) code = ExitFor(RunLight(o, stdout));
                else code = ExitFor(RunStructure(o, stdout));
            }
            catch (Exception e)
            {
                stderr.WriteLine("EROARE: " + e.Message);
                return 1;
            }
            stdout.WriteLine();
            return code;
        }

        /// <summary>
        /// Refuzul de intrare, in vocabularul nucleului. Iese cu 1, aceeasi categorie cu
        /// <see cref="ExposureVerdict.NotRaw"/>: nu e un verdict prost, e absenta unui
        /// verdict.
        /// </summary>
        internal const string NotApplicable = Stats.NotApplicable;

        internal static int ExitFor(string verdict) => verdict switch
        {
            "OK" or "PERFECT" or "BUN" => 0,   // BUN e utilizabil, deci trece
            NotApplicable => 1,                // intrare nepotrivita, nu verdict
            "SUSPECT" or "ACCEPTABIL" or LightSeriesAnalyzer.Undetermined => 2,
            _ => 3,
        };

        internal static int ExitFor(ExposureVerdict v) => v switch
        {
            ExposureVerdict.Ok => 0,
            ExposureVerdict.NotRaw => 1,   // fisierul nu e ce cere modul: nu e un verdict
            ExposureVerdict.AcceptableLow => 2,
            ExposureVerdict.AcceptableHigh => 2,
            _ => 3,
        };

        static string RunStructure(Options o, TextWriter w)
        {
            var run = StructureRun.Execute(o.Image, w);
            SaveMap(o.OutPng, run.Png, w);
            return run.Result.Verdict;
        }

        static string RunRatio(Options o, TextWriter w)
        {
            var run = RatioRun.Execute(o.Image, o.Ratio, w);
            SaveMap(o.OutPng, run.Png, w);
            // Modul desparte masterul in doua componente, deci are doua forme de aratat.
            // A doua merge langa prima, cu un sufix, ca sa nu ceara inca un argument.
            SaveMap(WorldMapPath(o.OutPng), run.SecondaryPng, w);
            return run.Result.Verdict;
        }

        /// <summary>
        /// Calea hartii componentei fixe in lume: acelasi nume, cu "-lume" inainte de
        /// extensie. "harta.png" -> "harta-lume.png".
        /// </summary>
        internal static string WorldMapPath(string path)
        {
            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path) + "-lume" + Path.GetExtension(path);
            return string.IsNullOrEmpty(dir) ? name : Path.Combine(dir, name);
        }

        static string RunLight(Options o, TextWriter w)
        {
            if (Directory.Exists(o.Image)) return RunLightSeries(o, w);

            var run = LightRun.Execute(o.Image, o.Fov, w);
            if (run.Refused) return NotApplicable;

            SaveMap(o.OutPng, run.Png, w);
            return run.Result.Verdict;
        }

        static string RunLightSeries(Options o, TextWriter w)
        {
            var run = LightSeriesRun.Execute(o.Image, o.Pattern, o.Fov, w);
            if (run.Refused) return NotApplicable;

            SaveMap(o.OutPng, run.Png, w);
            return run.Result.Verdict;
        }

        static ExposureVerdict RunExpo(Options o, TextWriter w)
            => ExposureRun.Execute(o.Image, o.Sat, o.Black, o.Sky, w).Result.Verdict;

        /// <summary>
        /// Nu trece prin ExitFor: modul n-are verdict de tradus, iar un "OK" inventat ar
        /// pune un succes acolo unde tabelul codurilor promite "utilizabil ca atare".
        /// 0 = a scris poza; orice esec ajunge la catch-ul de mai sus, cu 1.
        /// </summary>
        static int RunHisteq(Options o, TextWriter w)
        {
            var run = HistEqRun.Execute(o.Image, w);
            SaveMap(o.OutPng, run.Png, w);
            return 0;
        }

        static void SaveMap(string path, byte[] png, TextWriter w)
        {
            File.WriteAllBytes(path, png);
            RunReport.WriteMapSaved(path, w);
        }

        static void WriteUsage(TextWriter w)
        {
            w.WriteLine("Utilizare:");
            w.WriteLine("  flatcheck <flat.fit>                    # test structura");
            w.WriteLine("  flatcheck <A.fit> --ratio <B.fit>       # atribuire prin rotatie 180");
            w.WriteLine("  flatcheck <light.fit> --light           # test end-to-end, un cadru");
            w.WriteLine("  flatcheck <folder/> --light             # test end-to-end pe toata seria");
            w.WriteLine("  flatcheck <flat_brut.NEF> --expo        # verificare expunere");
            w.WriteLine("  flatcheck <flat_brut.fit> --expo        # idem, pe un FITS brut din Siril");
            w.WriteLine("  flatcheck <sky_flat.NEF> --expo --sky   # expunere pt flats pe cer");
            w.WriteLine("  flatcheck <flat.NEF> --histeq -o <poza.png>   # vederea \"Histogram\" din Siril, pe canale");
            w.WriteLine("  optional: -o <iesire.png> | --sat <white> --black <lvl> (pt --expo)");
            w.WriteLine("            --pattern <tipar> (pt --light pe folder; implicit pp_light_*.fit)");
            w.WriteLine("            --fov <grade> (diagonala campului, daca headerul n-o da)");
            w.WriteLine();
            w.WriteLine("Se accepta FITS (.fit/.fits/.fts) si raw de aparat (.nef/.cr2/.cr3/.arw/.dng/.raf).");
            w.WriteLine("Pe raw, --expo ia white si black exacte din fisier, nu le mai ghiceste din maxim.");
            w.WriteLine();
            w.WriteLine("--light nu merge pe orice: peste ~40 de grade diagonala refuza sa raspunda,");
            w.WriteLine("fiindca acolo cerul si vignetarea au aceeasi forma. Vezi README.md.");
            w.WriteLine();
            w.WriteLine("--histeq e o poza, nu un test: fiecare nivel de gri primeste acelasi numar de pixeli,");
            w.WriteLine("deci se vede forma a ce nu e uniform, nu si cat de mare e. Iese cu 0 cand a scris-o.");
            w.WriteLine();

            // GPLv3 sectiunea 5(d) - notita de garantie. Banner-ul de la fiecare rulare
            // poarta doar copyright-ul; disclaimerul intreg ar ingrosa fiecare raport
            // degeaba, deci sta aici, unde omul vine oricum sa citeasca.
            // Textul sta in RunReport.WarrantyLines, fiindca il arata si fereastra "Despre".
            foreach (string line in RunReport.WarrantyLines) w.WriteLine(line);
        }
    }
}
