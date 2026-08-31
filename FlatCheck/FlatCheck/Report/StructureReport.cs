// StructureReport.cs - StructureResult -> textul din consola. Doar formatare.

using System.IO;

namespace FlatCheck
{
    internal static class StructureReport
    {
        internal static void Write(StructureResult r, string path, TextWriter w)
        {
            w.WriteLine($"\n=== STRUCTURA: {path} ===");
            w.WriteLine($"    dimensiune analiza : {r.Width} x {r.Height} (dupa 2x2 + downsample)");
            w.WriteLine($"    median / max ADU   : {r.RawMedian:F0} / {r.RawMax:F0}");

            w.WriteLine("\n  VIGNETARE (informativ, nu e un test)");
            w.WriteLine($"    centru -> colt          : {r.VignetteStops:F2} stopuri (colt = {r.CornerMean / r.CentreMedian * 100:F0}% din centru)");

            w.WriteLine("\n  TESTE");
            w.WriteLine($"    imprastiere 4 colturi   : {r.CornerSpreadPct,5:F2} %   [{r.CornerVerdict}]");
            foreach (var kv in r.Corners)
                w.WriteLine($"        {kv.Key,-14}: {kv.Value / r.CornerMean * 100,6:F2} % din media colturilor");
            w.WriteLine($"    asimetrie azimutala max : {r.AzimuthalMaxPct,5:F2} %   [{r.AzimuthalVerdict}]");
            w.WriteLine($"    reziduu dupa model rad. : {r.ResidualPtpPct,5:F2} %   [{r.ResidualVerdict}]  <- CEL MAI IMPORTANT");
            w.WriteLine($"    offset centru iluminare : {r.CentroidOffsetPct,5:F2} %   [{r.CentroidVerdict}]");
            w.WriteLine($"        varf iluminare = ({r.FitCx:F0}, {r.FitCy:F0})  vs  centru senzor = ({r.SensorCx:F0}, {r.SensorCy:F0})");

            WriteScale(w);

            w.WriteLine($"\n  VERDICT: {r.Verdict}");
            WriteAdvice(r.Verdict, w);
        }

        /// <summary>
        /// Scara comuna a celor trei teste de abatere de la simetria radiala. Nu se
        /// aplica offsetului centrului de iluminare, care masoara o pozitie.
        /// </summary>
        static void WriteScale(TextWriter w)
        {
            w.WriteLine("\n  SCARA celor trei teste de abatere de mai sus");
            Band(w, $"sub {Th.ResidPerfect:F0} %", "calitate perfecta");
            Band(w, $"{Th.ResidPerfect:F0} - {Th.ResidUsable:F0} %", "utilizabil; background extraction scoate ce ramane");
            Band(w, $"{Th.ResidUsable:F0} - {Th.ResidCheck:F0} %", "utilizabil, dar confirma cu --light inainte de un stretch dur");
            Band(w, $"peste {Th.ResidCheck:F0} %", "refa flat-urile");
            w.WriteLine("    Cel mai important e \"reziduu dupa model rad.\": masoara direct cat din");
            w.WriteLine("    cadru NU se explica printr-un model pur radial, adica exact eroarea de");
            w.WriteLine("    flat. Imprastierea colturilor si asimetria azimutala sunt vederi");
            w.WriteLine("    partiale ale aceleiasi abateri.");
        }

        static void Band(TextWriter w, string range, string meaning)
            => w.WriteLine($"    {range,-13}{meaning}");

        static void WriteAdvice(string verdict, TextWriter w)
        {
            switch (verdict)
            {
                case "PERFECT":
                    w.WriteLine("    Abaterea de la simetria radiala e sub zgomot. Flat-ul e curat.");
                    break;
                case "BUN":
                    w.WriteLine("    Utilizabil ca atare. Ce ramane sta in intervalul pe care background");
                    w.WriteLine("    extraction il scoate fara efort.");
                    break;
                case "ACCEPTABIL":
                    w.WriteLine("    Utilizabil, dar ruleaza modul --light pe un light calibrat inainte de");
                    w.WriteLine("    un stretch dur, ca sa confirmi ca flat-ul chiar corecteaza.");
                    break;
                default:
                    w.WriteLine("    Abatere prea mare de la simetria radiala => sursa neuniforma, light");
                    w.WriteLine("    leak, camera inclinata, sau ambient. NU e vignetare. Refa flat-urile.");
                    w.WriteLine("    Vezi harta reziduala pentru forma problemei.");
                    break;
            }
        }
    }
}
