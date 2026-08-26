// FlatCheckCli.cs - orchestrarea testabila: parsare, dispecerat pe moduri, cod de iesire.
// Ordinea de dispecerat: --expo -> --ratio -> --light -> structura (implicit).

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
                if (o.Expo) Core.ModeExpo(o.Image, o.Sat, o.Black, o.Sky, stdout);
                else if (o.Ratio != null) Core.ModeRatio(o.Image, o.Ratio, o.OutPng, stdout);
                else if (o.Light) Core.ModeLight(o.Image, o.OutPng, stdout);
                else Core.ModeStructure(o.Image, o.OutPng, stdout);
            }
            catch (Exception e)
            {
                stderr.WriteLine("EROARE: " + e.Message);
                return 1;
            }
            stdout.WriteLine();
            return 0;
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
