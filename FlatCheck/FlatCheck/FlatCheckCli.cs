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
// Ordinea de dispecerat: --expo -> --ratio -> --light -> structura (implicit).
//
// CODURI DE IESIRE (din faza 8 - inainte era mereu 0, ceea ce facea imposibila
// folosirea in scripturi):
//     0  OK
//     1  eroare (fisier lipsa, argument gresit, FITS invalid, fit esuat)
//     2  SUSPECT
//     3  PROBLEMA
// Verdictele sunt monotone, deci "orice != 0" inseamna "nu e curat, uita-te".
//
// Fiecare mod e acelasi lant: incarca FITS -> analizor pur (Analysis/) -> raport
// (Report/) -> harta PNG. Doar stratul asta atinge discul; analizoarele sunt Img -> record,
// iar PngWriter nu tipareste nimic - mesajul "harta salvata" se scrie de aici.

using System;
using System.Collections.Generic;
using System.IO;

namespace FlatCheck
{
    internal static class FlatCheckCli
    {
        internal static int Run(string[] args, TextWriter stdout, TextWriter stderr)
        {
            // Versiunea, pe prima linie a oricarei rulari - inclusiv --help si erorile -
            // ca un raport copiat undeva sa spuna singur ce versiune l-a produs. Sub ea,
            // notita de copyright ceruta de GPLv3 (vezi LICENSE).
            stdout.WriteLine(VersionInfo.Banner);
            stdout.WriteLine(VersionInfo.Copyright);

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
                if (o.Expo) code = ExitFor(RunExpo(o, stdout));
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
        /// Nu e un verdict, e un refuz: intrarea nu e potrivita pentru testul cerut.
        /// Iese cu 1, aceeasi categorie cu <see cref="ExposureVerdict.NotRaw"/>, si se
        /// deosebeste astfel de NEDETERMINAT, care inseamna "a rulat, dar nu concluzioneaza".
        /// </summary>
        internal const string NotApplicable = "INAPLICABIL";

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
            var r = StructureAnalyzer.Analyze(ImageLoader.LoadImage(o.Image));
            StructureReport.Write(r, o.Image, w);
            SaveMap(o.OutPng, r.Residual, w);
            return r.Verdict;
        }

        static string RunRatio(Options o, TextWriter w)
        {
            var r = RatioAnalyzer.Analyze(ImageLoader.LoadImage(o.Image), ImageLoader.LoadImage(o.Ratio));
            RatioReport.Write(r, o.Image, o.Ratio, w);
            SaveMap(o.OutPng, r.Ratio, w);
            return r.Verdict;
        }

        static string RunLight(Options o, TextWriter w)
        {
            if (Directory.Exists(o.Image)) return RunLightSeries(o, w);

            LoadedFrame f = ImageLoader.Load(o.Image);

            // Garda inainte de orice calcul: pe un camp prea larg cifra n-ar insemna
            // nimic, deci nu se produce deloc, in loc sa se produca si sa se explice.
            var s = LightSuitability.Check(f.Header, o.Fov);
            if (!s.CanRun) { LightReport.WriteTooWide(s, o.Image, w); return NotApplicable; }

            var r = LightAnalyzer.Analyze(f.Image);
            LightReport.Write(r, s, o.Image, w);
            SaveMap(o.OutPng, r.RadialMap, w);
            return r.Verdict;
        }

        /// <summary>
        /// --light pe toate cadrele dintr-un folder. Un cadru singur amesteca eroarea de
        /// flat cu termenul radial al cerului din acel moment; media pe serie ii separa,
        /// dar numai daca e concludenta - vezi LightSeriesAnalyzer.
        ///
        /// Cadrele se incarca pe rand si nu se pastreaza: un cadru de 24 MP ocupa sute de
        /// MB, deci 32 de cadre tinute simultan ar fi de neexecutat.
        /// </summary>
        static string RunLightSeries(Options o, TextWriter w)
        {
            var files = FindSeries(o.Image, o.Pattern);
            if (files.Count == 0)
                throw new Exception(
                    $"Niciun fisier care sa se potriveasca cu '{o.Pattern}' in {o.Image}. "
                    + "Da alt tipar cu --pattern.");

            // Aplicabilitatea se decide pe primul cadru: toate vin din aceeasi sesiune,
            // deci au aceeasi focala si aceeasi scara.
            LoadedFrame first = ImageLoader.Load(files[0]);
            var s = LightSuitability.Check(first.Header, o.Fov);
            if (!s.CanRun) { LightReport.WriteTooWide(s, o.Image, w); return NotApplicable; }

            LightSeriesReport.WriteHeader(o.Image, o.Pattern, files.Count, s, w);

            var frames = new List<(string, LightResult)>();
            for (int i = 0; i < files.Count; i++)
            {
                Img img = first?.Image ?? ImageLoader.LoadImage(files[i]);
                first = null;                       // eliberat imediat, vezi mai sus
                var r = LightAnalyzer.Analyze(img);
                string name = Path.GetFileName(files[i]);
                LightSeriesReport.WriteRow(name, r, w);
                frames.Add((name, r));
            }

            var series = LightSeriesAnalyzer.Combine(frames);
            LightSeriesReport.WriteSummary(series, w);
            SaveMap(o.OutPng, series.RadialMap, w);
            return series.Verdict;
        }

        /// <summary>
        /// Fisierele din folder care se potrivesc cu tiparul, sortate.
        ///
        /// Enumerare completa + potrivire in proces, NU Directory.GetFiles(dir, pattern):
        /// potrivirea Win32 mai prinde si extensii mai lungi din mostenirea 8.3, deci
        /// "pp_light_*.fit" ar inghiti si ".fits". Aici tiparul inseamna acelasi lucru pe
        /// orice sistem, si acelasi lucru ca in flatcheck.py.
        ///
        /// Sortarea e ordinala pe numele fisierului, identica cu sorted() din Python -
        /// altfel tabelul ar iesi in ordini diferite intre implementari si crosscheck-ul
        /// ar raporta diferente care nu exista.
        /// </summary>
        internal static List<string> FindSeries(string dir, string pattern)
        {
            var hits = new List<string>();
            foreach (string p in Directory.EnumerateFiles(dir))
                if (GlobMatch(Path.GetFileName(p), pattern)) hits.Add(p);
            hits.Sort((a, b) => string.CompareOrdinal(Path.GetFileName(a), Path.GetFileName(b)));
            return hits;
        }

        /// <summary>
        /// Potrivire de tipar cu '*' si '?', fara distinctie de litere mari/mici.
        /// Clasele de caractere ('[abc]') NU sunt suportate - sunt tratate literal, in
        /// ambele implementari, ca sa nu apara o divergenta tacuta.
        /// </summary>
        internal static bool GlobMatch(string name, string pattern)
        {
            int n = 0, p = 0, starP = -1, starN = 0;
            while (n < name.Length)
            {
                if (p < pattern.Length && (pattern[p] == '?' || Eq(pattern[p], name[n])))
                { p++; n++; }
                else if (p < pattern.Length && pattern[p] == '*')
                { starP = p++; starN = n; }
                else if (starP >= 0)
                { p = starP + 1; n = ++starN; }
                else return false;
            }
            while (p < pattern.Length && pattern[p] == '*') p++;
            return p == pattern.Length;
        }

        static bool Eq(char a, char b) => char.ToLowerInvariant(a) == char.ToLowerInvariant(b);

        static ExposureVerdict RunExpo(Options o, TextWriter w)
        {
            // NU Prepare - modul asta vrea CFA brut, la scala ADU originala.
            //
            // Nicio ramificatie dupa format aici: ImageLoader a decis deja. Un raw duce o
            // scala exacta, un FITS duce null si atunci analizorul o ghiceste din maxim -
            // dar calculul de dupa e acelasi, bit cu bit.
            LoadedFrame f = ImageLoader.Load(o.Image);

            var r = ExposureAnalyzer.Analyze(f.Image, o.Sat, o.Black, o.Sky, f.Scale);
            ExposureReport.Write(r, o.Image, w);
            return r.Verdict;
        }

        static void SaveMap(string path, Img map, TextWriter w)
        {
            PngWriter.WriteResidualMap(path, map);
            w.WriteLine($"\n    -> harta salvata: {path}");
        }

        static void WriteUsage(TextWriter w)
        {
            w.WriteLine("Utilizare:");
            w.WriteLine("  flatcheck <flat.fit>                    # test structura");
            w.WriteLine("  flatcheck <A.fit> --ratio <B.fit>       # test rotatie 180");
            w.WriteLine("  flatcheck <light.fit> --light           # test end-to-end, un cadru");
            w.WriteLine("  flatcheck <folder/> --light             # test end-to-end pe toata seria");
            w.WriteLine("  flatcheck <flat_brut.NEF> --expo        # verificare expunere");
            w.WriteLine("  flatcheck <flat_brut.fit> --expo        # idem, pe un FITS brut din Siril");
            w.WriteLine("  flatcheck <sky_flat.NEF> --expo --sky   # expunere pt flats pe cer");
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

            // GPLv3 sectiunea 5(d) - notita de garantie. Banner-ul de la fiecare rulare
            // poarta doar copyright-ul; disclaimerul intreg ar ingrosa fiecare raport
            // degeaba, deci sta aici, unde omul vine oricum sa citeasca.
            w.WriteLine("Acest program vine FARA NICIO GARANTIE, nici macar cea implicita de");
            w.WriteLine("vandabilitate sau potrivire pentru un scop anume. E software liber:");
            w.WriteLine("il poti redistribui si modifica in termenii GPLv3 (sau, la alegerea");
            w.WriteLine("ta, orice versiune ulterioara). Textul complet e in fisierul LICENSE,");
            w.WriteLine("sau la <https://www.gnu.org/licenses/gpl-3.0.html>.");
        }
    }
}
