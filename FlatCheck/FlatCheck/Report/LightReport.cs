// LightReport.cs - LightResult -> textul din consola. Doar formatare.
//
// Semnul componentei radiale E diagnosticul, de aceea formatul e "+0.00;-0.00":
// pozitiv = fondul cade spre margini = sub-corectie; negativ = creste = supra-corectie.

using System;
using System.IO;

namespace FlatCheck
{
    internal static class LightReport
    {
        internal static void Write(LightResult r, string path, TextWriter w)
        {
            w.WriteLine($"\n=== TEST END-TO-END: {path} ===");
            w.WriteLine("    Astept un stack CALIBRAT cu flat, dar FARA background extraction.");

            w.WriteLine($"\n    tile-uri folosite       : {r.KeptTiles} / {r.TotalTiles} (restul = nebulozitate/stele, respinse)");
            w.WriteLine($"    gradient de cer (plan)  : {r.PlanePct,5:F2} %  <- poluare luminoasa, IGNORA");
            w.WriteLine($"    componenta radiala      : {r.RadPct,6:+0.00;-0.00} %   [{r.Verdict}]  <- ASTA e flat-ul");

            w.WriteLine("\n  INTERPRETARE");
            if (Math.Abs(r.RadPct) < LightAnalyzer.RadOk)
            {
                w.WriteLine("    Fondul n-are componenta radiala. Flat-ul face treaba.");
                w.WriteLine("    Asta e testul care conteaza - toate celelalte sunt proxy pentru el.");
            }
            else if (r.RadPct > 0)
            {
                w.WriteLine("    Fondul CADE spre margini => SUB-corectie. Flat-ul nu prinde toata");
                w.WriteLine("    vignetarea: alta diafragma decat la lights, sau flat contaminat.");
            }
            else
            {
                w.WriteLine("    Fondul CRESTE spre margini => SUPRA-corectie. Cel mai probabil");
                w.WriteLine("    flat-darks lipsa/gresite: imparti la un flat care contine offset.");
            }
        }
    }
}
