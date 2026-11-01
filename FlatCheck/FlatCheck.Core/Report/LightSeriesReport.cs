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

// LightSeriesReport.cs - LightSeriesResult -> the console's text, and the End to end tab's
// correlation and reasons in the same words. Formatting only.
//
// Three entry points instead of one: a series of 32 frames takes about two minutes, and sitting
// with an empty console all that time is bad manners for a command-line tool. So
// Runs/LightSeriesRun.cs writes the header, then one row as each frame is ready, then the
// summary, for both applications. The formatting stays entirely here all the same, as for the
// other modes - Report/ holds the reports' text (the error messages are thrown where
// they arise, in Analysis/ and Runs/).

using System;
using System.IO;
using System.Linq;
using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>
    /// The words of <c>--light</c> on a series: the header, one row per frame, and the
    /// summary with the verdict or the reasons it is undetermined.
    /// </summary>
    internal static class LightSeriesReport
    {
        /// <summary>The header of the series, written before the first frame is analysed.</summary>
        /// <param name="dir">The folder, as the header prints it.</param>
        /// <param name="pattern">The file pattern the frames were found with.</param>
        /// <param name="count">How many frames were found.</param>
        /// <param name="s">The suitability check's answer, on the first frame.</param>
        /// <param name="w">Where to write.</param>
        internal static void WriteHeader(
            string dir, string pattern, int count, SuitabilityResult s, TextWriter w)
        {
            w.WriteLine(T($"\n=== END-TO-END TEST (SERIES): {dir} ===", $"\n=== TEST END-TO-END (SERIE): {dir} ==="));
            w.WriteLine(T("    Expecting frames CALIBRATED with the flat, WITHOUT background extraction and NOT REGISTERED.",
                          "    Astept cadre CALIBRATE cu flat, FARA background extraction si NEREGISTRATE."));
            w.WriteLine(T($"    frames found            : {count}  (pattern {pattern})",
                          $"    cadre gasite            : {count}  (tipar {pattern})"));
            LightReport.WriteSuitability(s, w);
            w.WriteLine();
            w.WriteLine(T("    frame                              radial   sky gradient     tiles",
                          "    cadru                              radial   gradient cer     tile-uri"));
        }

        /// <summary>One frame's row, written as soon as the frame is done.</summary>
        /// <param name="name">The frame's file name; cut from the left above 30 characters.</param>
        /// <param name="r">The frame's background fit.</param>
        /// <param name="w">Where to write.</param>
        internal static void WriteRow(string name, LightResult r, TextWriter w)
            => w.WriteLine($"    {Trim(name),-30} {LightReport.Signed(r.RadPct),8} % "
                         + $"{r.PlanePct,10:F2} %  {r.KeptTiles,6}/{r.TotalTiles}");

        /// <summary>
        /// The summary: the mean, the spread, the standard error and the sky correlation, then
        /// the verdict with its diagnosis, or why the mean cannot be read as the flat error.
        /// </summary>
        /// <param name="r">The combined series.</param>
        /// <param name="w">Where to write.</param>
        internal static void WriteSummary(LightSeriesResult r, TextWriter w)
        {
            w.WriteLine(T("\n  SERIES SUMMARY", "\n  SINTEZA PE SERIE"));
            w.WriteLine(T($"    radial component, mean    : {LightReport.Signed(r.MeanRad),7} %",
                          $"    componenta radiala, medie : {LightReport.Signed(r.MeanRad),7} %"));
            w.WriteLine(T($"    spread (min .. max)       : {LightReport.Signed(r.MinRad),7} .. {LightReport.Signed(r.MaxRad)} %",
                          $"    imprastiere (min .. max)  : {LightReport.Signed(r.MinRad),7} .. {LightReport.Signed(r.MaxRad)} %"));
            w.WriteLine(T($"    error of the mean         : {r.Sem,7:F2} points", $"    eroarea mediei            : {r.Sem,7:F2} puncte")
                      + (Failed(r, "sem") ? T($"   <- above the threshold of {Th.LightSeriesSemMax:F2}", $"   <- peste pragul de {Th.LightSeriesSemMax:F2}") : ""));

            if (r.CorrApplies)
                w.WriteLine(T($"    correlation w. sky grad.  : {r.SkyCorr,7:F2}", $"    corelatie cu grad. de cer : {r.SkyCorr,7:F2}")
                          + (Failed(r, "corelatie")
                              ? T($"          <- above the threshold of {Th.LightSeriesSkyCorr:F2}", $"          <- peste pragul de {Th.LightSeriesSkyCorr:F2}")
                              : ""));
            else
                w.WriteLine(T("    correlation w. sky grad.  : ", "    corelatie cu grad. de cer : ") + CorrelationValue(r));

            w.WriteLine($"\n    VERDICT: {VerdictWords.Of(r.Verdict)}");

            if (r.Conclusive)
            {
                LightReport.WriteDiagnosis(r.MeanRad, w);
                w.WriteLine(T($"\n    The mean over {r.Count} frames is the estimate of the flat error: the spread is small,",
                              $"\n    Media pe {r.Count} cadre e estimarea erorii de flat: imprastierea e mica,"));
                w.WriteLine(T("    so the sky's radial term is stable over the series and does not contaminate it.",
                              "    deci termenul radial al cerului e stabil peste serie si nu o contamineaza."));
                return;
            }

            w.WriteLine(T("    The mean can NOT be read as the flat error:", "    Media NU se poate citi ca eroare de flat:"));
            foreach (string reason in r.Reasons) WriteReason(r, reason, w);

            w.WriteLine();
            foreach (string line in SameFlatLines) w.WriteLine("    " + line);
            if (Ui.Current == UiLanguage.Romanian)
            {
                w.WriteLine("    Pe aceste date flat-ul se judeca doar pe testele de structura ale masterului,");
                w.WriteLine("    unde cerul nu intra deloc. Vezi README, sectiunea despre cand se poate");
                w.WriteLine("    folosi --light.");
            }
            else
            {
                w.WriteLine("    On these data the flat is judged only on the structure tests of the master,");
                w.WriteLine("    where the sky does not enter at all. See the README, the section on when");
                w.WriteLine("    --light can be used.");
            }
        }

        /// <summary>One reason the verdict is undetermined, in words.</summary>
        /// <param name="r">The combined series.</param>
        /// <param name="reason">A key of <see cref="LightSeriesResult.Reasons"/>: "sem",
        /// "corelatie", or a text written as it is.</param>
        /// <param name="w">Where to write.</param>
        static void WriteReason(LightSeriesResult r, string reason, TextWriter w)
        {
            string[] lines = ReasonLines(r, reason);
            w.WriteLine("      - " + lines[0]);
            foreach (string line in lines.Skip(1)) w.WriteLine("        " + line);
        }

        /// <summary>One reason the verdict is undetermined, in the console's lines: the console
        /// writes them as an indented bullet, the window joins them into one paragraph.</summary>
        /// <param name="r">The combined series.</param>
        /// <param name="reason">A key of <see cref="LightSeriesResult.Reasons"/>: "sem",
        /// "corelatie", or a text written as it is.</param>
        /// <returns>The reason's lines, the first starting in lower case.</returns>
        static string[] ReasonLines(LightSeriesResult r, string reason) => reason switch
        {
            "sem" => Ui.Current == UiLanguage.Romanian
                ? new[]
                {
                    $"media e prea nesigura: eroarea ei ({r.Sem:F2}) e mai mare chiar decat pragul de OK ({Th.LightRadOk:F2}),",
                    "pentru ca cadrele difera prea mult intre ele. Un verdict aici ar fi",
                    "o parere, nu o masuratoare.",
                }
                : new[]
                {
                    $"the mean is too uncertain: its error ({r.Sem:F2}) is larger even than the OK threshold ({Th.LightRadOk:F2}),",
                    "because the frames differ too much from each other. A verdict here would be",
                    "an opinion, not a measurement.",
                },
            "corelatie" => Ui.Current == UiLanguage.Romanian
                ? new[]
                {
                    $"cifra radiala se misca odata cu cerul (corelatie {r.SkyCorr:F2}, pe un cer care",
                    $"s-a schimbat cu {r.SkySpan:F0} puncte): ce se masoara e cerul, nu flat-ul.",
                }
                : new[]
                {
                    $"the radial figure moves together with the sky (correlation {r.SkyCorr:F2}, on a sky",
                    $"that changed by {r.SkySpan:F0} points): what is measured is the sky, not the flat.",
                },
            _ => new[] { reason + "." },
        };

        /// <summary>Why everything that moves during a series is sky, in both applications; the
        /// console then points at the structure tests, the window at the Structure tab.</summary>
        static string[] SameFlatLines => new[]
        {
            T("The same flat was applied to every frame, so its error is the same in all of them;",
              "Acelasi flat s-a aplicat pe fiecare cadru, deci greseala lui e aceeasi in toate;"),
            T("what changes from one frame to the next comes from the sky.",
              "ce se schimba de la un cadru la altul vine din cer."),
        };

        /// <summary>Whether a condition failed.</summary>
        /// <param name="r">The combined series.</param>
        /// <param name="reason">The reason's key.</param>
        /// <returns>True when <paramref name="reason"/> is among the series' reasons.</returns>
        static bool Failed(LightSeriesResult r, string reason) => r.Reasons.Contains(reason);

        /// <summary>
        /// Long file names would break the columns; they are cut from the left, keeping the
        /// tail, because the frame's number sits there.
        /// </summary>
        /// <param name="name">The file name.</param>
        /// <returns>The name, or "..." and its last 27 characters when it is longer than
        /// 30.</returns>
        static string Trim(string name)
            => name.Length <= 30 ? name : "..." + name.Substring(name.Length - 27);

        // ------------------------------------------------------------ for the window
        // The same correlation figure and the same reasons as WriteSummary, in the window's
        // layout: no column indentation, paragraphs, and the Structure tab named where the
        // console names the structure tests.

        /// <summary>The sky correlation for the window's figures, or why it does not
        /// apply.</summary>
        /// <param name="r">The combined series.</param>
        /// <returns>The correlation with two decimals, or "nu se aplica" with the gradient's
        /// span in points.</returns>
        internal static string CorrelationValue(LightSeriesResult r)
            => r.CorrApplies
                ? $"{r.SkyCorr:F2}"
                : T($"does not apply (the gradient varies by only {r.SkySpan:F1} points)",
                    $"nu se aplica (gradientul variaza doar {r.SkySpan:F1} puncte)");

        /// <summary>
        /// Why the mean cannot be read as the flat error, for the window. The same list of
        /// reasons as in the console report, only without the column indentation.
        /// </summary>
        /// <param name="r">The combined series; not conclusive.</param>
        /// <returns>One paragraph per reason, then the closing paragraph pointing at the
        /// Structure tab, separated by blank lines.</returns>
        internal static string ReasonsText(LightSeriesResult r)
        {
            var parts = new System.Collections.Generic.List<string>();

            foreach (string reason in r.Reasons)
            {
                string text = string.Join(" ", ReasonLines(r, reason));
                parts.Add(char.ToUpperInvariant(text[0]) + text.Substring(1));
            }

            parts.Add(string.Join(" ", SameFlatLines)
                    + T(" On these data the flat is judged only on the Structure tab, where the sky does not enter at all.",
                        " Pe aceste date flat-ul se judeca doar in tabul Structura, unde cerul nu intra deloc."));

            return string.Join("\n\n", parts);
        }
    }
}
