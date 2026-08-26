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
using System.IO;

namespace FlatCheck
{
    internal static class FlatCheckCli
    {
        internal static int Run(string[] args, TextWriter stdout, TextWriter stderr)
        {
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

        internal static int ExitFor(string verdict) => verdict switch
        {
            "OK" => 0,
            "SUSPECT" => 2,
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
            var r = StructureAnalyzer.Analyze(FitsReader.Load(o.Image));
            StructureReport.Write(r, o.Image, w);
            SaveMap(o.OutPng, r.Residual, w);
            return r.Verdict;
        }

        static string RunRatio(Options o, TextWriter w)
        {
            var r = RatioAnalyzer.Analyze(FitsReader.Load(o.Image), FitsReader.Load(o.Ratio));
            RatioReport.Write(r, o.Image, o.Ratio, w);
            SaveMap(o.OutPng, r.Ratio, w);
            return r.Verdict;
        }

        static string RunLight(Options o, TextWriter w)
        {
            var r = LightAnalyzer.Analyze(FitsReader.Load(o.Image));
            LightReport.Write(r, o.Image, w);
            SaveMap(o.OutPng, r.RadialMap, w);
            return r.Verdict;
        }

        static ExposureVerdict RunExpo(Options o, TextWriter w)
        {
            // NU Prepare - modul asta vrea CFA brut, la scala ADU originala.
            var r = ExposureAnalyzer.Analyze(FitsReader.Load(o.Image), o.Sat, o.Black, o.Sky);
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
            w.WriteLine("  flatcheck <light.fit> --light           # test end-to-end");
            w.WriteLine("  flatcheck <flat_brut.fit> --expo        # verificare expunere");
            w.WriteLine("  flatcheck <sky_flat.fit> --expo --sky   # expunere pt flats pe cer");
            w.WriteLine("  optional: -o <iesire.png> | --sat <white> --black <lvl> (pt --expo)");
        }
    }
}
