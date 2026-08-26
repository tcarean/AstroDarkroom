// ExposureReport.cs - ExposureResult -> textul din consola. Doar formatare.
//
// Corectiile sugerate sunt log2(57/centru) stopuri - 57 fiind mijlocul benzii tinta
// 50-65 - si log2(30/centru) pentru sky flats.

using System;
using System.IO;

namespace FlatCheck
{
    internal static class ExposureReport
    {
        static readonly string[] ChannelNames = { "CFA[0,0]", "CFA[0,1]", "CFA[1,0]", "CFA[1,1]" };

        internal static void Write(ExposureResult r, string path, TextWriter w)
        {
            w.WriteLine($"\n=== EXPUNERE: {path} ===");
            w.WriteLine($"    saturatie (white) : {r.White:F0}   black : {r.Black:F0}   sursa: {r.ScaleSource}");

            if (r.IsCfa)
            {
                w.WriteLine("    nivel median GLOBAL per canal CFA (informativ; nu stiu care e R/G/B):");
                for (int i = 0; i < 4; i++)
                {
                    string mk = i == r.BrightestChannel ? "  <- cel mai luminos" : "";
                    w.WriteLine($"        {ChannelNames[i]}: {(r.ChannelMedians[i] - r.Black) / r.Range * 100,5:F1}%{mk}");
                }
            }
            else
            {
                w.WriteLine("    imagine mono/procesata (fara CFA)");
            }

            w.WriteLine($"    canal cel mai luminos, mediana GLOBALA : {r.GlobalMedianPct,5:F1}%  (trasa in jos de colturi)");
            w.WriteLine($"    canal cel mai luminos, CENTRU 20%      : {r.CentreMedianPct,5:F1}%  <- ASTA decide expunerea");
            w.WriteLine($"    clipping in centru (>=98% white)       : {r.CentreClipPct:F3}%");

            if (!r.IsRaw)
            {
                WriteNotRaw(r, w);
                return;
            }

            w.WriteLine();

            if (r.IsSky) WriteSky(r, w);
            else WritePanel(r, w);
        }

        static void WriteNotRaw(ExposureResult r, TextWriter w)
        {
            w.WriteLine();
            w.WriteLine("    !! FISIERUL NU PARE UN FLAT BRUT !!");
            if (r.LevelTooLow)
            {
                w.WriteLine($"    Nivelul e ~{r.CentreMedianPct:F1}% (langa zero/negativ) -> flat-dark deja scazut,");
                w.WriteLine("    sau imagine normalizata. --expo are nevoie de nivelul ADU original.");
            }
            if (r.LooksDebayered)
            {
                w.WriteLine($"    Cele 4 canale CFA sunt quasi-identice (spread {r.CfaSpread:F3}) -> fisierul");
                w.WriteLine("    e DEBAYERIZAT. --expo are nevoie de mozaicul Bayer intact.");
            }
            w.WriteLine();
            w.WriteLine("    Cauza tipica: ai deschis NEF-ul in Siril si ai dat Save (debayerizeaza).");
            w.WriteLine("    SOLUTIE: produ un FITS brut cu tab-ul Conversion din Siril (NU Open+Save),");
            w.WriteLine("    care pastreaza mozaicul CFA nedebayerizat. (Sau ruleaza pe NEF in Python.)");
        }

        static void WriteSky(ExposureResult r, TextWriter w)
        {
            bool clipping = r.CentreClipPct > Th.ExpoClip;
            w.WriteLine("    [mod --sky: praguri pt flats pe cer, nu pe panou]");
            switch (r.Verdict)
            {
                case ExposureVerdict.TooHigh:
                    w.WriteLine("    VERDICT: PREA SUS" + (clipping ? " (CLIPPING in centru!)" : " (risc stele in cadru)"));
                    w.WriteLine("    Scade expunerea.");
                    break;
                case ExposureVerdict.TooLow:
                    w.WriteLine("    VERDICT: PREA JOS pentru sky flat");
                    w.WriteLine($"    Creste cu ~{Math.Log2(Th.ExpoSkyTarget / r.CentreMedianPct):F1} stopuri (dar repede, cerul se stinge).");
                    break;
                case ExposureVerdict.Ok:
                    w.WriteLine($"    VERDICT: OK pentru sky flat  (centru {Th.ExpoSkyLo:F0}-{Th.ExpoSkyHi:F0}%)");
                    w.WriteLine("    La sky flat nivelul modest e corect. SNR-ul vine din numarul de cadre,");
                    w.WriteLine("    nu din a urca expunerea (ai risca stele). NU forta mai sus.");
                    break;
                default:
                    w.WriteLine("    VERDICT: cam sus, dar acceptabil");
                    w.WriteLine("    Sub 60% e ok; peste, risti stele. Nu urca mai mult.");
                    break;
            }
        }

        static void WritePanel(ExposureResult r, TextWriter w)
        {
            bool clipping = r.CentreClipPct > Th.ExpoClip;
            double cen = r.CentreMedianPct;
            switch (r.Verdict)
            {
                case ExposureVerdict.TooHigh:
                    {
                        double stops = cen > 0 ? Math.Log2(Th.ExpoTarget / cen) : -1;
                        w.WriteLine("    VERDICT: PREA SUS" + (clipping ? " (CLIPPING in centru!)" : ""));
                        w.WriteLine($"    Scade expunerea cu ~{-stops:F1} stopuri.");
                        if (clipping)
                            w.WriteLine("    Centrul clipeaza -> pierzi info de vignetare exact unde semnalul e maxim.");
                        break;
                    }
                case ExposureVerdict.TooLow:
                    w.WriteLine("    VERDICT: PREA JOS");
                    w.WriteLine($"    Creste expunerea cu ~{Math.Log2(Th.ExpoTarget / cen):F1} stopuri.");
                    w.WriteLine("    Sub ~30% in centru masterul iese zgomotos (zgomot fixed-pattern).");
                    break;
                case ExposureVerdict.AcceptableLow:
                    w.WriteLine("    VERDICT: ACCEPTABIL, dar cam jos");
                    w.WriteLine($"    Ai putea creste cu ~{Math.Log2(Th.ExpoTarget / cen):F1} stopuri.");
                    break;
                case ExposureVerdict.Ok:
                    w.WriteLine($"    VERDICT: OK  (centrul in tinta {Th.ExpoLo:F0}-{Th.ExpoHi:F0}%)");
                    break;
                default:
                    w.WriteLine("    VERDICT: ACCEPTABIL, dar cam sus");
                    w.WriteLine($"    Ai putea scadea cu ~{-Math.Log2(Th.ExpoTarget / cen):F1} stopuri.");
                    break;
            }
        }
    }
}
