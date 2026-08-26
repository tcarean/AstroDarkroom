// RatioReport.cs - RatioResult -> textul din consola. Doar formatare.

using System.IO;

namespace FlatCheck
{
    internal static class RatioReport
    {
        internal static void Write(RatioResult r, string pathA, string pathB, TextWriter w)
        {
            w.WriteLine($"\n=== TEST ROTATIE 180: {pathA}  /  {pathB} ===");
            w.WriteLine("    Vignetarea si praful sunt fixe pe senzor -> se anuleaza in raport.");
            w.WriteLine("    Orice gradient extern se inverseaza -> apare aici, la ~2x amplitudine.");

            w.WriteLine($"\n    structura in raport A/B : {r.PtpPct,5:F2} %   [{r.PtpVerdict}]");
            w.WriteLine($"    imprastiere 4 colturi   : {r.CornerSpreadPct,5:F2} %   [{r.CornerVerdict}]");
            w.WriteLine("    (eroarea reala in fiecare master e ~jumatate din cifrele astea)");

            w.WriteLine($"\n  VERDICT: {r.Verdict}");
            w.WriteLine(r.Verdict == "OK"
                ? "    Setup-ul de flats e curat. Poti construi biblioteca pe el."
                : "    Setup-ul contamineaza flat-ul. Repara sursa inainte de biblioteca.");
        }
    }
}
