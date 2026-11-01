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

// FlatCheckCli.cs - the testable orchestration: parsing, dispatch by mode, exit code.
// The dispatch order: --lenses -> --synthetic -> --against -> --histeq -> --expo -> --light
// -> structure (the default).
//
// WHAT IS LEFT HERE, after the core became FlatCheck.Core: exactly what is the console's and
// nobody else's - the arguments, the choice of the mode, the writing of the map to disk and the
// exit code. Each mode's chain proper (load -> analyse -> report -> encode the map) sits in
// Runs/, because the WPF application uses it too. See the comment in Runs/RunOutcome.cs for
// why it moved exactly there and not nearer.
//
// EXIT CODES (since phase 8 - before, it was always 0, which made use in scripts impossible;
// the whole table is in docs/design/thresholds.md):
//     0  OK / PERFECT / BUN; --expo exposed right
//     1  error (missing file, wrong argument, invalid FITS, failed fit) or input refused
//        (INAPLICABIL, --expo on a file that is not a raw flat,
//        --synthetic on a cube, without a focal length, f-stop or crop, or on a lens whose
//        file carries no vignetting model)
//     2  SUSPECT / ACCEPTABIL / NEDETERMINAT; --expo acceptable, low or high;
//        --synthetic when the lens cannot be chosen
//     3  PROBLEMA / REFA; --expo too low or too high
// The verdicts are monotonic, so "anything != 0" means "not clean, take a look".
// --histeq, --synthetic and --lenses have no verdict: 0 only means they wrote their
// output (--lenses always exits 0).

using System;
using System.Collections.Generic;
using System.IO;
using NLog;
using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>
    /// The console proper: parses the arguments, dispatches to one mode's run in
    /// <c>Runs/</c>, writes the maps and files to disk and turns the verdict into the exit
    /// code.
    /// </summary>
    internal static class FlatCheckCli
    {
        /// <summary>
        /// Runs the console on a command line: the banner, then the usage, an error or the
        /// mode's report. Every exception of a run is caught and written to
        /// <paramref name="stderr"/> with exit code 1.
        /// </summary>
        /// <param name="args">The command line.</param>
        /// <param name="stdout">Where the banner, the report and the usage go.</param>
        /// <param name="stderr">Where the errors go, prefixed "ERROR: " (Romanian "EROARE: ").</param>
        /// <returns>The exit code: 0 clean or done, 1 error or input refused, 2 look before
        /// deciding, 3 redo (<see cref="ExitFor(string)"/>).</returns>
        internal static int Run(string[] args, TextWriter stdout, TextWriter stderr)
        {
            // The version and the copyright, on the first two lines of every run - --help and
            // the errors included.
            RunReport.WriteBanner(stdout);

            // The language first: the usage and every parse error are already worded in it.
            // With no --lang the run keeps the process's language, English (Ui.Default).
            LanguageArgument lang = LanguageArgument.Extract(args);
            if (lang.BadValue != null)
            {
                Log.Warn("refused: " + LanguageArgument.BadValueMessage(lang.BadValue));
                stderr.WriteLine("ERROR: " + LanguageArgument.BadValueMessage(lang.BadValue));
                return 1;
            }
            using (lang.Language is UiLanguage chosen ? Ui.Use(chosen) : null)
                return RunIn(lang.Rest, stdout, stderr);
        }

        /// <summary>
        /// The run proper, after the banner and the language: the usage, an error or the
        /// mode's report, in <see cref="Ui.Current"/>.
        /// </summary>
        /// <param name="args">The command line without its <c>--lang</c>.</param>
        /// <param name="stdout">Where the report and the usage go.</param>
        /// <param name="stderr">Where the errors go.</param>
        /// <returns>The exit code, as <see cref="Run"/> describes it.</returns>
        static int RunIn(string[] args, TextWriter stdout, TextWriter stderr)
        {
            Options o = Options.Parse(args);
            if (o.Help)
            {
                Log.Info("mode=help");
                WriteUsage(stdout);
                return 0;
            }
            if (o.Error != null)
            {
                // The message is the one the user saw, in the run's language.
                Log.Warn("refused the arguments: " + o.Error);
                stderr.WriteLine(ErrorPrefix + o.Error);
                return 1;
            }

            // A folder means "run on the series", and that makes sense only with --light. The
            // other modes work on a master or on a given frame.
            if (Directory.Exists(o.Image) && !o.Light)
            {
                string message = T(
                    "a folder can be given only with --light, for the run on a series. "
                    + "The other modes expect a file.",
                    "un folder se poate da doar cu --light, pentru rularea pe serie. "
                    + "Celelalte moduri asteapta un fisier.");
                // The message as the user saw it, like the two other refusals (R-110).
                Log.Warn("refused: " + o.Image + " is a folder; " + message);
                stderr.WriteLine(ErrorPrefix + message);
                return 1;
            }

            // The arguments are in the process's first line already; this line says what they
            // came to (the mode is Options' decision, R-003, so the log and the dispatch agree).
            Log.Info("mode=" + ModeName(o.Mode) + " input=" + LogText.Str(o.Image) + " lang=" + (Ui.Current == UiLanguage.Romanian ? "ro" : "en"));
            int code;
            try
            {
                switch (o.Mode)
                {
                    case Mode.Lenses: code = RunLenses(o, stdout); break;
                    case Mode.Synthetic: code = RunSynthetic(o, stdout, stderr); break;
                    case Mode.Against: code = ExitFor(RunAgainst(o, stdout)); break;
                    case Mode.Histeq: code = RunHisteq(o, stdout); break;
                    case Mode.Expo: code = ExitFor(RunExpo(o, stdout)); break;
                    case Mode.Light: code = ExitFor(RunLight(o, stdout)); break;
                    default: code = ExitFor(RunStructure(o, stdout)); break;
                }
            }
            catch (Exception e)
            {
                // stderr keeps its one line; the type, the stack and the inner exceptions go to
                // the log (R-110).
                Log.Error(e, "run failed: mode=" + ModeName(o.Mode));
                stderr.WriteLine(ErrorPrefix + e.Message);
                return 1;
            }
            stdout.WriteLine();
            return code;
        }

        /// <summary>The prefix of every error line on stderr, in the run's language.</summary>
        static string ErrorPrefix => T("ERROR: ", "EROARE: ");

        /// <summary>The diagnostic log: the mode chosen, the refusals, the failures with
        /// their stack, the files written (R-110).</summary>
        static readonly Logger Log = LogManager.GetLogger("Cli");

        /// <summary>A mode's name in the log: its enum name in lower case.</summary>
        /// <param name="mode">The mode.</param>
        /// <returns><c>structure</c>, <c>light</c>, ...</returns>
        static string ModeName(Mode mode) => mode.ToString().ToLowerInvariant();

        /// <summary>
        /// The input refusal, in the core's vocabulary. It exits with 1, the same category as
        /// <see cref="ExposureVerdict.NotRaw"/>: it is not a bad verdict, it is the absence of
        /// a verdict.
        /// </summary>
        internal const string NotApplicable = Stats.NotApplicable;

        /// <summary>The exit code for a verdict word (docs/design/thresholds.md).</summary>
        /// <param name="verdict">A verdict of the structure ladder, of --light's OK/SUSPECT ladder,
        /// <see cref="LightSeriesAnalyzer.Undetermined"/> or <see cref="NotApplicable"/>.</param>
        /// <returns>0 for OK, PERFECT and BUN; 1 for not applicable; 2 for SUSPECT,
        /// ACCEPTABIL and undetermined; 3 for anything else.</returns>
        internal static int ExitFor(string verdict) => verdict switch
        {
            "OK" or "PERFECT" or "BUN" => 0,   // BUN is usable, so it passes
            NotApplicable => 1,                // unsuitable input, not a verdict
            "SUSPECT" or "ACCEPTABIL" or LightSeriesAnalyzer.Undetermined => 2,
            _ => 3,
        };

        /// <summary>The exit code for an exposure verdict.</summary>
        /// <param name="v">The <c>--expo</c> verdict.</param>
        /// <returns>0 for Ok; 1 for a file that is not raw; 2 for acceptable, low or high; 3
        /// for anything else.</returns>
        internal static int ExitFor(ExposureVerdict v) => v switch
        {
            ExposureVerdict.Ok => 0,
            ExposureVerdict.NotRaw => 1,   // the file is not what the mode asks for: not a verdict
            ExposureVerdict.AcceptableLow => 2,
            ExposureVerdict.AcceptableHigh => 2,
            _ => 3,
        };

        /// <summary>
        /// Structure; with --lens or --raw also the comparison of the vignetting with the
        /// lens's profiles, and the verdict (and the exit code) is the combined one. Without
        /// them, exactly what it was.
        /// </summary>
        /// <param name="o">The parsed options.</param>
        /// <param name="w">Where the report is written.</param>
        /// <returns>The run's verdict, for <see cref="ExitFor(string)"/>.</returns>
        static string RunStructure(Options o, TextWriter w)
        {
            StructureQuery query = o.Lens != null || o.Raw != null
                ? new StructureQuery { Sources = Sources(o), RawPath = o.Raw, LensText = o.Lens, Source = o.Source, Focal = o.Focal, FStop = o.FStop, Crop = o.Crop }
                : null;
            var run = StructureRun.Execute(o.Image, w, query);
            SaveMap(o.OutPng, run.Png, w);
            return run.Result.Verdict;
        }

        /// <summary>--light: a folder goes to the series, a file to the single-frame run; the
        /// map at -o unless refused.</summary>
        /// <param name="o">The parsed options.</param>
        /// <param name="w">Where the report is written.</param>
        /// <returns>The verdict, or <see cref="NotApplicable"/> when the field is too
        /// wide.</returns>
        static string RunLight(Options o, TextWriter w)
        {
            if (Directory.Exists(o.Image)) return RunLightSeries(o, w);

            var run = LightRun.Execute(o.Image, o.Fov, w);
            if (run.Refused) return NotApplicable;

            SaveMap(o.OutPng, run.Png, w);
            return run.Result.Verdict;
        }

        /// <summary>--light on a folder: the series matching --pattern; the combined map at
        /// -o unless refused.</summary>
        /// <param name="o">The parsed options; <c>Image</c> is the folder.</param>
        /// <param name="w">Where the rows and the summary are written.</param>
        /// <returns>The series' verdict, or <see cref="NotApplicable"/> when the field is too
        /// wide.</returns>
        static string RunLightSeries(Options o, TextWriter w)
        {
            var run = LightSeriesRun.Execute(o.Image, o.Pattern, o.Fov, w);
            if (run.Refused) return NotApplicable;

            SaveMap(o.OutPng, run.Png, w);
            return run.Result.Verdict;
        }

        /// <summary>--expo: the exposure of a raw flat; no map.</summary>
        /// <param name="o">The parsed options.</param>
        /// <param name="w">Where the report is written.</param>
        /// <returns>The exposure verdict, for <see cref="ExitFor(ExposureVerdict)"/>.</returns>
        static ExposureVerdict RunExpo(Options o, TextWriter w)
            => ExposureRun.Execute(o.Image, o.Sat, o.Black, o.Sky, w).Result.Verdict;

        /// <summary>
        /// It does not go through ExitFor: the mode has no verdict to translate, and an
        /// invented "OK" would put a success where the table of codes promises "usable as it
        /// is". 0 = it wrote the picture; any failure reaches the catch above, with 1.
        /// </summary>
        /// <param name="o">The parsed options.</param>
        /// <param name="w">Where the report is written.</param>
        /// <returns>Always 0.</returns>
        static int RunHisteq(Options o, TextWriter w)
        {
            var run = HistEqRun.Execute(o.Image, w, o.Rainbow);
            SaveMap(o.OutPng, run.Png, w);
            return 0;
        }

        /// <summary>
        /// The profile sources: by default the machine's (the Adobe folders and the lensfun/
        /// beside the exe); with --profiles &lt;dir&gt;, the folder given (with .lcp files
        /// anywhere in it) and its lensfun/ subfolder. That is how the tests run too, on XML
        /// they wrote.
        /// </summary>
        /// <param name="o">The parsed options.</param>
        /// <returns>The located sources, with their catalog.</returns>
        static ProfileSources Sources(Options o)
            => o.Profiles != null
                ? ProfileLocator.Locate(new[] { o.Profiles }, Path.Combine(o.Profiles, ProfileLocator.LensfunFolderName))
                : ProfileLocator.Locate();

        /// <summary>--lenses: the list of the lenses with a profile (all, or those that contain
        /// --lens). Always 0.</summary>
        /// <param name="o">The parsed options.</param>
        /// <param name="w">Where the list is written.</param>
        /// <returns>0.</returns>
        static int RunLenses(Options o, TextWriter w)
        {
            using RunClock clock = RunClock.Start(Log, "lenses", "text=" + LogText.Str(o.Lens) + " source=" + (o.Source.HasValue ? o.Source.Value.ToString() : "both"));
            ProfileSources sources = Sources(o);
            clock.Lap("locate");
            IReadOnlyList<LensChoice> hits = sources.Catalog.Search(o.Lens, o.Source);
            SyntheticReport.WriteList(hits, sources.Catalog, o.Lens, sources, w);
            clock.Lap("list");
            clock.Done("listed " + LogText.Int(hits.Count) + " lenses");
            return 0;
        }

        /// <summary>
        /// --synthetic: like --histeq, without a verdict. 0 = it wrote the flat; 1 = the
        /// reference refused or something not to be found (focal length, aperture, crop); 2 =
        /// the lens could not be chosen (none or too many) - "read the list before you
        /// decide", what 2 means everywhere.
        /// </summary>
        /// <param name="o">The parsed options; <c>Synthetic</c> is the FITS path to
        /// write.</param>
        /// <param name="w">Where the report is written.</param>
        /// <param name="err">Where the refusal's first line goes.</param>
        /// <returns>0, 1 or 2, as above.</returns>
        static int RunSynthetic(Options o, TextWriter w, TextWriter err)
        {
            var req = new SyntheticRequest { LensText = o.Lens, Source = o.Source };
            SyntheticRunOutcome run = SyntheticRun.Execute(o.Image, req, Sources(o), o.Focal, o.FStop, o.Crop, w);
            if (run.Refused)
            {
                err.WriteLine(ErrorPrefix + run.Refusal.Split('\n')[0]);
                return run.RefusedChoice ? 2 : 1;
            }
            using (var fs = new FileStream(o.Synthetic, FileMode.Create, FileAccess.Write))
                SyntheticRun.WriteFits(run, o.Image, fs);
            Log.Info("wrote " + o.Synthetic + " (" + LogText.FileKiB(o.Synthetic) + ")");
            w.WriteLine(SyntheticReport.SavedLine(o.Synthetic));
            return 0;
        }

        /// <summary>--against: the structure of the two flats' ratio; the ratio's map at
        /// -o.</summary>
        /// <param name="o">The parsed options.</param>
        /// <param name="w">Where the report is written.</param>
        /// <returns>The difference's verdict.</returns>
        static string RunAgainst(Options o, TextWriter w)
        {
            var run = AgainstRun.Execute(o.Image, o.Against, w);
            SaveMap(o.OutPng, run.Png, w);
            return run.Result.Verdict;
        }

        /// <summary>Writes a map to disk, overwriting, and announces it.</summary>
        /// <param name="path">Where the PNG goes.</param>
        /// <param name="png">The PNG bytes.</param>
        /// <param name="w">Where the announcement line is written.</param>
        static void SaveMap(string path, byte[] png, TextWriter w)
        {
            File.WriteAllBytes(path, png);
            Log.Info("wrote " + path + " (" + LogText.KiB(png.Length) + ")");
            RunReport.WriteMapSaved(path, w);
        }

        /// <summary>Writes the usage (--help, or no argument), then the GPL warranty
        /// notice.</summary>
        /// <param name="w">Where the text is written.</param>
        static void WriteUsage(TextWriter w)
        {
            if (Ui.Current == UiLanguage.Romanian) WriteUsageRomanian(w);
            else WriteUsageEnglish(w);

            // GPLv3 section 5(d) - the warranty notice. The banner of every run carries only
            // the copyright; the whole disclaimer would thicken every report for nothing, so it
            // sits here, where the user comes to read anyway.
            // The text sits in RunReport.WarrantyLines, because the "Despre" window shows it
            // too.
            foreach (string line in RunReport.WarrantyLines) w.WriteLine(line);
        }

        // The usage is the one text written whole in each language rather than line by line:
        // its columns are aligned by hand, and a pair per line would hide that alignment.

        /// <summary>The usage in English, without the warranty notice.</summary>
        /// <param name="w">Where the text is written.</param>
        static void WriteUsageEnglish(TextWriter w)
        {
            w.WriteLine("Usage:");
            w.WriteLine("  flatcheck <flat.fit>                    # structure test");
            w.WriteLine("  flatcheck <flat.fit> --raw <flat.NEF>   # the same, plus the vignetting against the lens profile named in the raw");
            w.WriteLine("  flatcheck <light.fit> --light           # end-to-end test, one frame");
            w.WriteLine("  flatcheck <folder/> --light             # end-to-end test on the whole series");
            w.WriteLine("  flatcheck <raw_flat.NEF> --expo         # exposure check");
            w.WriteLine("  flatcheck <raw_flat.fit> --expo         # the same, on a raw FITS from Siril");
            w.WriteLine("  flatcheck <sky_flat.NEF> --expo --sky   # exposure for sky flats");
            w.WriteLine("  flatcheck <flat.NEF> --histeq -o <picture.png>   # Siril's \"Histogram\" view, per channel");
            w.WriteLine("  flatcheck <flat.NEF> --histeq --rainbow -o <picture.png>   # the same in Siril's false colours (one layer)");
            w.WriteLine("  flatcheck <reference.fit> --synthetic <synthetic.fit> --lens <text>   # synthetic flat from the lens profile");
            w.WriteLine("  flatcheck --lenses [--lens <text>]      # the lenses with a vignetting profile (Adobe on disk, lensfun/ beside the exe)");
            w.WriteLine("  flatcheck <A.fit> --against <B.fit>     # how much and how two flats differ (e.g. real against synthetic)");
            w.WriteLine("  optional: -o, --out <output.png> | --sat <white> --black <lvl> (for --expo)");
            w.WriteLine("            --pattern <pattern> (for --light on a folder; default pp_light_*.fit)");
            w.WriteLine("            --fov <degrees> (the field's diagonal, when the header does not give it)");
            w.WriteLine("            --source adobe|lensfun, --focal <mm>, --fstop <f>, --crop <factor>, --profiles <folder> (for --synthetic");
            w.WriteLine("                         and for structure with --lens/--raw; the focal length and the aperture come from the raw or");
            w.WriteLine("                         from FOCALLEN when not given; the crop from the lensfun database)");
            w.WriteLine("            --lens <text> | --raw <file> (for structure: compares the master's vignetting with the lens's");
            w.WriteLine("                         profiles - by name, or by the ID in the raw beside the master; the verdict includes them)");
            w.WriteLine("            --lang en|ro (the language of every text: en English, the default; ro Romanian)");
            w.WriteLine();
            w.WriteLine("Accepted: FITS (.fit/.fits/.fts) and camera raw (.nef/.cr2/.cr3/.arw/.dng/.raf/.pef).");
            w.WriteLine("On a raw, --expo takes the exact white and black levels from the file instead of guessing them from the maximum.");
            w.WriteLine();
            w.WriteLine("--light does not work on everything: beyond a ~40-degree diagonal it refuses to answer,");
            w.WriteLine("because there the sky and the vignetting have the same shape. See README.md.");
            w.WriteLine();
            w.WriteLine("--histeq is a picture, not a test: every grey level gets the same number of pixels,");
            w.WriteLine("so it shows the shape of what is not uniform, not how large it is. Exits with 0 once written.");
            w.WriteLine("--rainbow colours that picture with Siril's rainbow table (magenta = lowest level, red = highest):");
            w.WriteLine("the mosaic on a CFA frame, the channel on mono. The colours are only an order, not values.");
            w.WriteLine();
            w.WriteLine("--synthetic writes a radial flat from the lens's published vignetting profile -");
            w.WriteLine("its model, achromatic, with no dust and no light source at all. --against compares it");
            w.WriteLine("with a real flat: the vignetting of the ratio is the difference in stops, the map is its shape.");
            w.WriteLine();
        }

        /// <summary>The usage in Romanian, without the warranty notice.</summary>
        /// <param name="w">Where the text is written.</param>
        static void WriteUsageRomanian(TextWriter w)
        {
            w.WriteLine("Utilizare:");
            w.WriteLine("  flatcheck <flat.fit>                    # test structura");
            w.WriteLine("  flatcheck <flat.fit> --raw <flat.NEF>   # idem, plus vignetarea fata de profilul obiectivului din raw");
            w.WriteLine("  flatcheck <light.fit> --light           # test end-to-end, un cadru");
            w.WriteLine("  flatcheck <folder/> --light             # test end-to-end pe toata seria");
            w.WriteLine("  flatcheck <flat_brut.NEF> --expo        # verificare expunere");
            w.WriteLine("  flatcheck <flat_brut.fit> --expo        # idem, pe un FITS brut din Siril");
            w.WriteLine("  flatcheck <sky_flat.NEF> --expo --sky   # expunere pt flats pe cer");
            w.WriteLine("  flatcheck <flat.NEF> --histeq -o <poza.png>   # vederea \"Histogram\" din Siril, pe canale");
            w.WriteLine("  flatcheck <flat.NEF> --histeq --rainbow -o <poza.png>   # la fel, in culorile false din Siril (un strat)");
            w.WriteLine("  flatcheck <referinta.fit> --synthetic <sintetic.fit> --lens <text>   # flat sintetic din profilul obiectivului");
            w.WriteLine("  flatcheck --lenses [--lens <text>]      # obiectivele cu profil de vignetare (Adobe de pe disc, lensfun/ de langa exe)");
            w.WriteLine("  flatcheck <A.fit> --against <B.fit>     # cat si cum difera doua flaturi (ex. real fata de sintetic)");
            w.WriteLine("  optional: -o, --out <iesire.png> | --sat <white> --black <lvl> (pt --expo)");
            w.WriteLine("            --pattern <tipar> (pt --light pe folder; implicit pp_light_*.fit)");
            w.WriteLine("            --fov <grade> (diagonala campului, daca headerul n-o da)");
            w.WriteLine("            --source adobe|lensfun, --focal <mm>, --fstop <f>, --crop <factor>, --profiles <folder> (pt --synthetic");
            w.WriteLine("                         si pt structura cu --lens/--raw; focala si diafragma vin din raw sau din FOCALLEN");
            w.WriteLine("                         cand nu le dai; crop-ul din baza lensfun)");
            w.WriteLine("            --lens <text> | --raw <fisier> (pt structura: compara vignetarea masterului cu profilele");
            w.WriteLine("                         obiectivului - dupa nume, sau dupa ID-ul din raw-ul de langa master; verdictul le cuprinde)");
            w.WriteLine("            --lang en|ro (limba tuturor textelor: en engleza, implicit; ro romana)");
            w.WriteLine();
            w.WriteLine("Se accepta FITS (.fit/.fits/.fts) si raw de aparat (.nef/.cr2/.cr3/.arw/.dng/.raf/.pef).");
            w.WriteLine("Pe raw, --expo ia white si black exacte din fisier, nu le mai ghiceste din maxim.");
            w.WriteLine();
            w.WriteLine("--light nu merge pe orice: peste ~40 de grade diagonala refuza sa raspunda,");
            w.WriteLine("fiindca acolo cerul si vignetarea au aceeasi forma. Vezi README.md.");
            w.WriteLine();
            w.WriteLine("--histeq e o poza, nu un test: fiecare nivel de gri primeste acelasi numar de pixeli,");
            w.WriteLine("deci se vede forma a ce nu e uniform, nu si cat de mare e. Iese cu 0 cand a scris-o.");
            w.WriteLine("--rainbow coloreaza poza cu tabelul rainbow din Siril (magenta = nivelul cel mai jos, rosu = cel mai sus):");
            w.WriteLine("mozaicul pe un cadru CFA, canalul pe mono. Culorile sunt doar o ordine, nu valori.");
            w.WriteLine();
            w.WriteLine("--synthetic scrie un flat radial din profilul de vignetare publicat al obiectivului -");
            w.WriteLine("modelul lui, acromatic, fara praf si fara nicio sursa de lumina. --against il compara");
            w.WriteLine("cu un flat real: vignetarea raportului e diferenta in stopuri, harta e forma ei.");
            w.WriteLine();
        }
    }
}
