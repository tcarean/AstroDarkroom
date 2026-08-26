// FlatCheckCli.cs - orchestrarea testabila: parsare, dispecerat pe moduri, cod de iesire.
// Ordinea de dispecerat: --expo -> --ratio -> --light -> structura (implicit).
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

            try
            {
                if (o.Expo) RunExpo(o, stdout);
                else if (o.Ratio != null) RunRatio(o, stdout);
                else if (o.Light) RunLight(o, stdout);
                else RunStructure(o, stdout);
            }
            catch (Exception e)
            {
                stderr.WriteLine("EROARE: " + e.Message);
                return 1;
            }
            stdout.WriteLine();
            return 0;
        }

        // NOTA (problema cunoscuta 2): verdictul e calculat si tiparit, dar codul de
        // iesire ramane 0 chiar si pentru PROBLEMA. Faza 8 il leaga.
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

        static void RunLight(Options o, TextWriter w)
        {
            var r = LightAnalyzer.Analyze(FitsReader.Load(o.Image));
            LightReport.Write(r, o.Image, w);
            SaveMap(o.OutPng, r.RadialMap, w);
        }

        static void RunExpo(Options o, TextWriter w)
        {
            // NU Prepare - modul asta vrea CFA brut, la scala ADU originala.
            var r = ExposureAnalyzer.Analyze(FitsReader.Load(o.Image), o.Sat, o.Black, o.Sky);
            ExposureReport.Write(r, o.Image, w);
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
