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

// LightReport.cs - LightResult -> the console's text. Formatting only.
//
// The sign of the radial component IS the diagnosis, which is why it is always written
// explicitly: positive = the background falls toward the edges = under-correction; negative =
// it rises = over-correction.

using System;
using System.IO;

namespace FlatCheck
{
    /// <summary>
    /// The words of <c>--light</c> on one frame: the field line, the refusal on a wide field,
    /// the report and the diagnosis read from the sign.
    /// </summary>
    internal static class LightReport
    {
        /// <summary>
        /// The number with its sign written explicitly, like Python's "%+.2f".
        ///
        /// Do NOT use the format with sections "+0.00;-0.00": for negative zero .NET picks the
        /// POSITIVE section (because -0.0 is not &lt; 0) but still adds its minus, and out comes
        /// "-+0.00". It happens exactly on a background with no radial component, that is on the
        /// good case. Python wrote "-0.00" there, so it was also a divergence between the
        /// implementations.
        ///
        /// IsNegative, not "&lt; 0", so as to keep the sign of -0.0 - the same as Python.
        /// </summary>
        /// <param name="v">The value.</param>
        /// <returns>"+" or "-" and the absolute value with two decimals.</returns>
        internal static string Signed(double v)
            => (double.IsNegative(v) ? "-" : "+") + Math.Abs(v).ToString("0.00");

        /// <summary>
        /// The line of the field covered, plus the warning when it is at the limit. Used by the
        /// series report too - that is why it sits here, not inline in Write.
        ///
        /// When the field could not be found out, a line is written that says exactly that. It is
        /// deliberate: unknown does not mean suspect, it means unchecked, and silence would leave
        /// the impression it was checked.
        /// </summary>
        /// <param name="s">The suitability check's answer.</param>
        /// <param name="w">Where to write.</param>
        internal static void WriteSuitability(SuitabilityResult s, TextWriter w)
        {
            if (!double.IsNaN(s.FovDeg))
                w.WriteLine($"    camp diagonal           : {s.FovDeg,5:F1} grade  ({s.Source})");
            else if (!double.IsNaN(s.FocalMm))
                w.WriteLine($"    camp acoperit           : nedeterminat ({s.Source})");
            else
                w.WriteLine("    camp acoperit           : necunoscut (headerul nu da nici focala, nici scara)");

            if (s.Level == FovLevel.Marginal)
            {
                w.WriteLine($"\n    ATENTIE: camp la limita. Separarea plan/radial slabeste peste {Th.LightFovOk:F0}");
                w.WriteLine("    grade diagonala, iar cifra de mai jos poate contine si cer, nu doar flat.");
                w.WriteLine("    Citeste-o ca pe un indiciu, nu ca pe o masuratoare.");
            }
            else if (s.Level == FovLevel.Unknown)
            {
                w.WriteLine("    (nu pot verifica daca testul se aplica; vezi --fov si README)");
            }
        }

        /// <summary>
        /// What is written instead of a report when the field is too wide. The mode gives no
        /// verdict here - it has nothing to measure - so it explains the degeneracy and points
        /// to the test that does answer on such data.
        /// </summary>
        /// <param name="s">The suitability check's answer, of level TooWide.</param>
        /// <param name="path">The file's or folder's path, as the header prints it.</param>
        /// <param name="w">Where to write.</param>
        internal static void WriteTooWide(SuitabilityResult s, string path, TextWriter w)
        {
            w.WriteLine($"\n=== TEST END-TO-END: {path} ===");
            WriteSuitability(s, w);

            w.WriteLine("\n  TESTUL NU SE APLICA PE CADRELE ASTEA");
            w.WriteLine($"    Peste {Th.LightFovMax:F0} de grade diagonala, cupola de poluare luminoasa se");
            w.WriteLine("    CURBEAZA in interiorul cadrului. Partea radial simetrica a acelei curburi");
            w.WriteLine("    are exact forma vignetarii, deci fitul nu le mai poate separa: orice cifra");
            w.WriteLine("    ar iesi ar fi cerul, nu flat-ul.");
            w.WriteLine("    Nu e o limitare a uneltei, e o degenerare geometrica.");
            w.WriteLine("\n    Pentru un setup cu camp larg, testul care raspunde e rotatia la 180 de");
            w.WriteLine("    grade (--ratio): valideaza flat-ul fara sa implice cerul deloc.");
            if (double.IsNaN(s.FovDeg) && !double.IsNaN(s.FocalMm))
            {
                w.WriteLine("\n    Decizia s-a luat doar pe focala, presupunand cel mai mare senzor");
                w.WriteLine("    plauzibil. Daca al tau e mai mic, da campul real cu --fov <grade>.");
            }
        }

        /// <summary>
        /// Writes the report of one frame: the field, the tiles kept, the sky plane (flagged
        /// above <see cref="Th.LightPlaneWarn"/>), the signed radial component with its verdict,
        /// the diagnosis, and the advice to run on the whole series.
        /// </summary>
        /// <param name="r">The background fit of the frame.</param>
        /// <param name="s">The suitability check's answer.</param>
        /// <param name="path">The file's path, as the header prints it.</param>
        /// <param name="w">Where to write.</param>
        internal static void Write(LightResult r, SuitabilityResult s, string path, TextWriter w)
        {
            w.WriteLine($"\n=== TEST END-TO-END: {path} ===");
            w.WriteLine("    Astept un stack CALIBRAT cu flat, dar FARA background extraction.");
            WriteSuitability(s, w);

            w.WriteLine($"\n    tile-uri folosite       : {r.KeptTiles} / {r.TotalTiles} (restul = nebulozitate/stele, respinse)");
            w.WriteLine($"    gradient de cer (plan)  : {r.PlanePct,5:F2} %  <- poluare luminoasa, IGNORA"
                      + (r.PlanePct > Th.LightPlaneWarn ? "  (mare!)" : ""));
            w.WriteLine($"    componenta radiala      : {Signed(r.RadPct),6} %   [{r.Verdict}]  <- ASTA e flat-ul");

            w.WriteLine("\n  INTERPRETARE");
            WriteDiagnosis(r.RadPct, w);

            // A single frame mixes the flat error with the sky's radial term of that moment, and
            // the second changes during the session. On a real series the figure swept 55
            // points in 81 minutes. The mean over the series separates much better, so it is
            // worth saying on every run on a single frame.
            w.WriteLine("\n    Cifra asta e dintr-un singur cadru, deci contine si cerul din acel moment.");
            w.WriteLine("    Da-i folderul in loc de fisier ca sa rulez pe toata seria si sa mediez:");
            w.WriteLine("        flatcheck process/ --light");
        }

        /// <summary>
        /// The diagnosis from the sign of the radial component. It sits apart because the series
        /// report uses it too: the same figure, the same reading, so the same text - if it were
        /// duplicated, the two would diverge at the first rewording.
        /// </summary>
        /// <param name="radPct">The radial component, percent, signed.</param>
        /// <param name="w">Where to write; each line is indented by four spaces.</param>
        internal static void WriteDiagnosis(double radPct, TextWriter w)
        {
            foreach (string line in Diagnosis(radPct).Split('\n')) w.WriteLine("    " + line);
        }

        /// <summary>
        /// The same diagnosis, as a string. The WPF application asks for it, putting it under
        /// the figure instead of writing it to a console; written a second time there, the sign -
        /// which IS the diagnosis - could have ended up explained the other way round.
        /// </summary>
        /// <param name="radPct">The radial component, percent, signed.</param>
        /// <returns>Two lines joined by '\n': below <see cref="Th.LightRadOk"/> in absolute value,
        /// that the background has no radial component; else under- or over-correction by the
        /// sign.</returns>
        internal static string Diagnosis(double radPct)
        {
            if (Math.Abs(radPct) < Th.LightRadOk)
                return "Fondul n-are componenta radiala. Flat-ul face treaba.\n"
                     + "Asta e testul care conteaza - toate celelalte sunt proxy pentru el.";

            if (radPct > 0)
                return "Fondul CADE spre margini => SUB-corectie. Flat-ul nu prinde toata\n"
                     + "vignetarea: alta diafragma decat la lights, sau flat contaminat.";

            return "Fondul CRESTE spre margini => SUPRA-corectie. Cel mai probabil\n"
                 + "flat-darks lipsa/gresite: imparti la un flat care contine offset.";
        }

        // ------------------------------------------------------------ for the window
        // The same field line, warning and refusal as WriteSuitability and WriteTooWide, in the
        // window's layout: no column padding, paragraphs instead of indented lines, and the
        // Ratio tab named where the console names --ratio.

        /// <summary>The field covered, for the window's card.</summary>
        /// <param name="s">The suitability check's answer, or null.</param>
        /// <returns>The diagonal in degrees with its source, "nedeterminat" with the source when
        /// only the focal length is known, a sentence when nothing is known, or "" for
        /// null.</returns>
        internal static string FieldText(SuitabilityResult s)
        {
            if (s == null) return "";
            if (!double.IsNaN(s.FovDeg)) return $"{s.FovDeg:F1} grade diagonala  ({s.Source})";
            if (!double.IsNaN(s.FocalMm)) return $"nedeterminat  ({s.Source})";
            return "necunoscut - headerul nu da nici focala, nici scara";
        }

        /// <summary>The warning under the window's field line: a marginal field's caution, or
        /// the note that the field could not be checked.</summary>
        /// <param name="s">The suitability check's answer, or null.</param>
        /// <returns>The warning, or "" for a suitable field or null.</returns>
        internal static string FieldWarning(SuitabilityResult s)
        {
            if (s == null) return "";

            if (s.Level == FovLevel.Marginal)
                return $"Camp la limita. Separarea plan/radial slabeste peste {Th.LightFovOk:F0} grade "
                     + "diagonala, iar cifra de mai jos poate contine si cer, nu doar flat. "
                     + "Citeste-o ca pe un indiciu, nu ca pe o masuratoare.";

            if (s.Level == FovLevel.Unknown)
                return "Nu pot verifica daca testul se aplica: headerul nu spune destul despre camp. "
                     + "Rezultatul ruleaza oricum, dar necunoscut inseamna neverificat, nu verificat.";

            return "";
        }

        /// <summary>Why the test does not apply on a field wider than
        /// <see cref="Th.LightFovMax"/>, for the window: the geometric degeneracy and the Ratio
        /// tab as the test that answers.</summary>
        /// <returns>Three paragraphs separated by blank lines.</returns>
        internal static string TooWideText()
            => "Peste " + $"{Th.LightFovMax:F0}" + " de grade diagonala, cupola de poluare luminoasa se "
             + "CURBEAZA in interiorul cadrului. Partea radial simetrica a acelei curburi are "
             + "exact forma vignetarii, deci fitul nu le mai poate separa: orice cifra ar iesi "
             + "ar fi cerul, nu flat-ul.\n\n"
             + "Nu e o limitare a uneltei, e o degenerare geometrica.\n\n"
             + "Pe un setup cu camp larg, testul care raspunde e cel din tabul Ratio: "
             + "rotatia la 180 de grade valideaza flat-ul fara sa implice cerul deloc.";
    }
}
