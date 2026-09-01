// LightReport.cs - LightResult -> textul din consola. Doar formatare.
//
// Semnul componentei radiale E diagnosticul, de aceea se scrie mereu explicit:
// pozitiv = fondul cade spre margini = sub-corectie; negativ = creste = supra-corectie.

using System;
using System.IO;

namespace FlatCheck
{
    internal static class LightReport
    {
        /// <summary>
        /// Numarul cu semnul scris explicit, ca "%+.2f" din Python.
        ///
        /// NU folosi formatul cu sectiuni "+0.00;-0.00": pentru zero negativ .NET alege
        /// sectiunea POZITIVA (fiindca -0.0 nu e &lt; 0) dar tot adauga minusul lui, si iese
        /// "-+0.00". Se intampla exact pe un fond fara componenta radiala, adica pe cazul
        /// bun. Python scria "-0.00" acolo, deci era si o divergenta intre implementari.
        ///
        /// IsNegative, nu "&lt; 0", ca sa se pastreze semnul lui -0.0 - la fel ca Python.
        /// </summary>
        internal static string Signed(double v)
            => (double.IsNegative(v) ? "-" : "+") + Math.Abs(v).ToString("0.00");

        internal static void Write(LightResult r, string path, TextWriter w)
        {
            w.WriteLine($"\n=== TEST END-TO-END: {path} ===");
            w.WriteLine("    Astept un stack CALIBRAT cu flat, dar FARA background extraction.");

            w.WriteLine($"\n    tile-uri folosite       : {r.KeptTiles} / {r.TotalTiles} (restul = nebulozitate/stele, respinse)");
            w.WriteLine($"    gradient de cer (plan)  : {r.PlanePct,5:F2} %  <- poluare luminoasa, IGNORA");
            w.WriteLine($"    componenta radiala      : {Signed(r.RadPct),6} %   [{r.Verdict}]  <- ASTA e flat-ul");

            w.WriteLine("\n  INTERPRETARE");
            if (Math.Abs(r.RadPct) < Th.LightRadOk)
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
