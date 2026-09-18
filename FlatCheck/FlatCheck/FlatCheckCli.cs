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
// Ordinea de dispecerat: --smooth -> --histeq -> --expo -> --ratio -> --light -> structura (implicit).
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
                if (o.Lenses) code = RunLenses(o, stdout);
                else if (o.Synthetic != null) code = RunSynthetic(o, stdout, stderr);
                else if (o.Against != null) code = ExitFor(RunAgainst(o, stdout));
                else if (o.Smooth != null) code = RunSmooth(o, stdout, stderr);
                else if (o.Histeq) code = RunHisteq(o, stdout);
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

        /// <summary>
        /// Structura; cu --lens sau --raw si comparatia vignetarii cu profilele obiectivului,
        /// iar verdictul (si codul de iesire) e cel combinat. Fara ele, exact ce era.
        /// </summary>
        static string RunStructure(Options o, TextWriter w)
        {
            StructureQuery query = o.Lens != null || o.Raw != null
                ? new StructureQuery { Sources = Sources(o), RawPath = o.Raw, LensText = o.Lens, Source = o.Source, Focal = o.Focal, FStop = o.FStop, Crop = o.Crop }
                : null;
            var run = StructureRun.Execute(o.Image, w, query);
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

        /// <summary>
        /// Ca --histeq, nu trece prin ExitFor: n-are verdict. 0 = a scris flat-ul netezit
        /// si hartile; 1 = a refuzat intrarea (raw, cub) sau a esuat. Fisierul FITS se
        /// scrie INAINTE de harti: el e produsul, hartile sunt explicatia lui.
        /// </summary>
        static int RunSmooth(Options o, TextWriter w, TextWriter err)
        {
            SmoothRunOutcome run;
            if (o.ScaleAuto)
            {
                // Propunerea intai. Fara textura sau nedeterminat: niciun fisier, cod 2 -
                // nu e o eroare, e "citeste asta inainte sa decizi", ce inseamna 2 peste tot.
                var advice = SmoothRun.Advise(o.Image, w);
                if (advice.Refused)
                {
                    err.WriteLine("EROARE: " + advice.Refusal.Split('\n')[0]);
                    return 1;
                }
                if (advice.Advice.Kind != SmoothAdviceKind.Proposed) return 2;
                run = SmoothRun.Execute(advice.Frame, o.Image, advice.Advice.ScalePx, w);
            }
            else
            {
                run = SmoothRun.Execute(o.Image, o.ScalePx, w);
                if (run.Refused)
                {
                    err.WriteLine("EROARE: " + run.Refusal.Split('\n')[0]);
                    return 1;
                }
            }
            using (var fs = new FileStream(o.Smooth, FileMode.Create, FileAccess.Write))
                SmoothRun.WriteFits(run, o.Image, fs);
            w.WriteLine(SmoothReport.SavedLine(o.Smooth));
            SaveMap(o.OutPng, run.Png, w);
            SaveMap(AfterMapPath(o.OutPng), run.SecondaryPng, w);
            return 0;
        }

        /// <summary>
        /// Sursele de profile: implicit cele ale masinii (folderele Adobe si lensfun/ de langa
        /// exe); cu --profiles <dir>, folderul dat (cu .lcp-uri oriunde in el) si subfolderul
        /// lui lensfun/. Asa ruleaza si testele, pe XML scris de ele.
        /// </summary>
        static ProfileSources Sources(Options o)
            => o.Profiles != null
                ? ProfileLocator.Locate(new[] { o.Profiles }, Path.Combine(o.Profiles, ProfileLocator.LensfunFolderName))
                : ProfileLocator.Locate();

        /// <summary>--lenses: lista obiectivelor cu profil (toate, sau cele care contin --lens). Mereu 0.</summary>
        static int RunLenses(Options o, TextWriter w)
        {
            ProfileSources sources = Sources(o);
            SyntheticReport.WriteList(sources.Catalog.Search(o.Lens, o.Source), sources.Catalog, o.Lens, sources, w);
            return 0;
        }

        /// <summary>
        /// --synthetic: ca --smooth, fara verdict. 0 = a scris flatul; 1 = referinta refuzata
        /// sau ceva de neaflat (focala, diafragma, crop); 2 = obiectivul nu s-a putut alege
        /// (nimic sau prea multe) - "citeste lista inainte sa decizi", ce inseamna 2 peste tot.
        /// </summary>
        static int RunSynthetic(Options o, TextWriter w, TextWriter err)
        {
            var req = new SyntheticRequest { LensText = o.Lens, Source = o.Source };
            SyntheticRunOutcome run = SyntheticRun.Execute(o.Image, req, Sources(o), o.Focal, o.FStop, o.Crop, w);
            if (run.Refused)
            {
                err.WriteLine("EROARE: " + run.Refusal.Split('\n')[0]);
                return run.RefusedChoice ? 2 : 1;
            }
            using (var fs = new FileStream(o.Synthetic, FileMode.Create, FileAccess.Write))
                SyntheticRun.WriteFits(run, o.Image, fs);
            w.WriteLine(SyntheticReport.SavedLine(o.Synthetic));
            return 0;
        }

        /// <summary>--against: structura pe raportul celor doua flaturi; harta raportului la -o.</summary>
        static string RunAgainst(Options o, TextWriter w)
        {
            var run = AgainstRun.Execute(o.Image, o.Against, w);
            SaveMap(o.OutPng, run.Png, w);
            return run.Result.Verdict;
        }

        /// <summary>
        /// Calea hartii reziduului flat-ului netezit: acelasi nume, cu "-dupa" inainte de
        /// extensie. "harta.png" -> "harta-dupa.png". Aceeasi conventie ca WorldMapPath.
        /// </summary>
        internal static string AfterMapPath(string path)
        {
            string dir = Path.GetDirectoryName(path);
            string name = Path.GetFileNameWithoutExtension(path) + "-dupa" + Path.GetExtension(path);
            return string.IsNullOrEmpty(dir) ? name : Path.Combine(dir, name);
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
            w.WriteLine("  flatcheck <flat.fit> --raw <flat.NEF>   # idem, plus vignetarea fata de profilul obiectivului din raw");
            w.WriteLine("  flatcheck <A.fit> --ratio <B.fit>       # atribuire prin rotatie 180");
            w.WriteLine("  flatcheck <light.fit> --light           # test end-to-end, un cadru");
            w.WriteLine("  flatcheck <folder/> --light             # test end-to-end pe toata seria");
            w.WriteLine("  flatcheck <flat_brut.NEF> --expo        # verificare expunere");
            w.WriteLine("  flatcheck <flat_brut.fit> --expo        # idem, pe un FITS brut din Siril");
            w.WriteLine("  flatcheck <sky_flat.NEF> --expo --sky   # expunere pt flats pe cer");
            w.WriteLine("  flatcheck <flat.NEF> --histeq -o <poza.png>   # vederea \"Histogram\" din Siril, pe canale");
            w.WriteLine("  flatcheck <flat.fit> --smooth <netezit.fit>   # flat netezit: fara textura, praf, pete");
            w.WriteLine("  flatcheck <referinta.fit> --synthetic <sintetic.fit> --lens <text>   # flat sintetic din profilul obiectivului");
            w.WriteLine("  flatcheck --lenses [--lens <text>]      # obiectivele cu profil de vignetare (Adobe de pe disc, lensfun/ de langa exe)");
            w.WriteLine("  flatcheck <A.fit> --against <B.fit>     # cat si cum difera doua flaturi (ex. real fata de sintetic)");
            w.WriteLine("  optional: -o <iesire.png> | --sat <white> --black <lvl> (pt --expo)");
            w.WriteLine("            --pattern <tipar> (pt --light pe folder; implicit pp_light_*.fit)");
            w.WriteLine("            --fov <grade> (diagonala campului, daca headerul n-o da)");
            w.WriteLine($"            --scale <px>|auto (pt --smooth: scara de netezire pe senzor, implicit {SmoothAnalyzer.DefaultScalePx:F0};");
            w.WriteLine("                         'auto' o propune din master si netezeste la ea, sau iese cu 2 fara fisier)");
            w.WriteLine("            --source adobe|lensfun, --focal <mm>, --fstop <f>, --crop <factor>, --profiles <folder> (pt --synthetic");
            w.WriteLine("                         si pt structura cu --lens/--raw; focala si diafragma vin din raw sau din FOCALLEN");
            w.WriteLine("                         cand nu le dai; crop-ul din baza lensfun)");
            w.WriteLine("            --lens <text> | --raw <fisier> (pt structura: compara vignetarea masterului cu profilele");
            w.WriteLine("                         obiectivului - dupa nume, sau dupa ID-ul din raw-ul de langa master; verdictul le cuprinde)");
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
            w.WriteLine("--smooth scrie un al doilea master care pastreaza tot ce e neted si scoate textura");
            w.WriteLine("difuzorului, praful si petele. Nu da verdict; raporteaza CE A SCOS, si scrie harta");
            w.WriteLine("acelei componente la -o (plus reziduul flat-ului netezit, cu sufixul -dupa).");
            w.WriteLine();
            w.WriteLine("--synthetic scrie un flat radial din profilul de vignetare publicat al obiectivului -");
            w.WriteLine("modelul lui, acromatic, fara praf si fara nicio sursa de lumina. --against il compara");
            w.WriteLine("cu un flat real: vignetarea raportului e diferenta in stopuri, harta e forma ei.");
            w.WriteLine();

            // GPLv3 sectiunea 5(d) - notita de garantie. Banner-ul de la fiecare rulare
            // poarta doar copyright-ul; disclaimerul intreg ar ingrosa fiecare raport
            // degeaba, deci sta aici, unde omul vine oricum sa citeasca.
            // Textul sta in RunReport.WarrantyLines, fiindca il arata si fereastra "Despre".
            foreach (string line in RunReport.WarrantyLines) w.WriteLine(line);
        }
    }
}
