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
            w.WriteLine($"    reziduu dupa model rad. : {r.ResidualPtpPct,5:F2} %   [{r.ResidualVerdict}]");
            w.WriteLine($"    offset centru iluminare : {r.CentroidOffsetPct,5:F2} %   [{r.CentroidVerdict}]");
            w.WriteLine($"        varf iluminare = ({r.FitCx:F0}, {r.FitCy:F0})  vs  centru senzor = ({r.SensorCx:F0}, {r.SensorCy:F0})");

            w.WriteLine($"\n  VERDICT: {r.Verdict}");
            if (r.Verdict != "OK")
            {
                w.WriteLine("    Abatere de la simetria radiala => sursa neuniforma, light leak,");
                w.WriteLine("    camera inclinata, sau ambient. NU e vignetare. Vezi harta reziduala.");
            }
        }
    }
}
