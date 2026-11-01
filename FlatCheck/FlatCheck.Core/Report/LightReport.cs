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

// LightReport.cs - LightResult -> the console's text, and the End to end tab's field line,
// warning and refusal in the same words. Formatting only.
//
// The sign of the radial component IS the diagnosis, which is why it is always written
// explicitly: positive = the background falls toward the edges = under-correction; negative =
// it rises = over-correction.

using System;
using System.IO;
using static FlatCheck.Ui;

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
            string label = double.IsNaN(s.FovDeg) ? T("field covered", "camp acoperit") : T("diagonal field", "camp diagonal");
            w.WriteLine($"    {label,-24}: {FieldValue(s, 5)}");

            if (s.Level == FovLevel.Marginal)
            {
                w.WriteLine();
                foreach (string line in MarginalLines()) w.WriteLine("    " + line);
            }
            else if (s.Level == FovLevel.Unknown)
            {
                w.WriteLine(T("    (cannot check whether the test applies; see --fov and the README)",
                              "    (nu pot verifica daca testul se aplica; vezi --fov si README)"));
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
            w.WriteLine(T($"\n=== END-TO-END TEST: {path} ===", $"\n=== TEST END-TO-END: {path} ==="));
            WriteSuitability(s, w);

            w.WriteLine(T("\n  THE TEST DOES NOT APPLY TO THESE FRAMES", "\n  TESTUL NU SE APLICA PE CADRELE ASTEA"));
            foreach (string line in DegeneracyLines()) w.WriteLine("    " + line);
            w.WriteLine("    " + NotALimitation);
            w.WriteLine(T("\n    On a wide field the flat is judged only on the structure tests of the master,",
                          "\n    Pe un camp larg flat-ul se judeca doar pe testele de structura ale masterului,"));
            w.WriteLine(T("    where the sky does not enter at all.",
                          "    unde cerul nu intra deloc."));
            if (JudgedOnTheFocalLengthAlone(s))
            {
                w.WriteLine("\n    " + FocalOnlyLines[0]);
                w.WriteLine("    " + FocalOnlyLines[1] + " " + FovAdvice);
            }
        }

        /// <summary>Whether the field was judged from the focal length alone, on the largest
        /// plausible sensor (<see cref="LightSuitability.Check"/>'s fourth layer): no field, a
        /// focal length.</summary>
        /// <param name="s">The suitability check's answer.</param>
        /// <returns>True when the field is NaN and the focal length is not.</returns>
        static bool JudgedOnTheFocalLengthAlone(SuitabilityResult s)
            => double.IsNaN(s.FovDeg) && !double.IsNaN(s.FocalMm);

        /// <summary>The note of a refusal judged on the focal length alone, in both applications,
        /// as the console's two lines: the window joins them into one sentence.</summary>
        static string[] FocalOnlyLines => new[]
        {
            T("The field was judged from the focal length alone, as if the sensor were full-frame,",
              "Campul a fost apreciat doar dupa focala, ca si cum senzorul ar fi full-frame,"),
            T("the largest possible.", "cel mai mare posibil."),
        };

        /// <summary>The way out the console adds to that note; the window has no control for
        /// it, so it does not say it.</summary>
        static string FovAdvice => T("If yours is smaller, give the real field with --fov <degrees>.",
                                     "Daca al tau e mai mic, da campul real cu --fov <grade>.");

        /// <summary>
        /// The value of the field line, the one three-way choice both applications print: the
        /// diagonal in degrees with its source when the field is known, "nedeterminat" with the
        /// source when only the focal length is (the field depends on the sensor, so no figure is
        /// invented), and "necunoscut" with the reason when the header says neither.
        /// </summary>
        /// <param name="s">The suitability check's answer; not null.</param>
        /// <param name="width">The minimum width of the degrees figure, padded with spaces on the
        /// left: 5 for the console's column, 0 for the window.</param>
        /// <returns>The value, without the label.</returns>
        static string FieldValue(SuitabilityResult s, int width)
        {
            if (!double.IsNaN(s.FovDeg))
                return T($"{s.FovDeg.ToString("F1").PadLeft(width)} degrees  ({s.Source})",
                         $"{s.FovDeg.ToString("F1").PadLeft(width)} grade  ({s.Source})");
            if (!double.IsNaN(s.FocalMm)) return T($"undetermined ({s.Source})", $"nedeterminat ({s.Source})");
            return T("unknown (the header gives neither the focal length nor the scale)",
                     "necunoscut (headerul nu da nici focala, nici scara)");
        }

        /// <summary>The warning of a field at the limit, in the console's lines: the console
        /// indents them, the window joins them into one paragraph.</summary>
        /// <returns>Three lines; the first quotes <see cref="Th.LightFovOk"/>.</returns>
        static string[] MarginalLines() => new[]
        {
            T($"WARNING: the field is at the limit. Beyond {Th.LightFovOk:F0} degrees of diagonal the sky and the",
              $"ATENTIE: campul e la limita. Peste {Th.LightFovOk:F0} de grade pe diagonala cerul si vignetarea"),
            T("vignetting start to look alike, and the figure below may hold sky too, not only flat.",
              "incep sa semene, iar cifra de mai jos poate contine si cer, nu doar flat."),
            T("Take it as a hint, not as a measurement.",
              "Ia-o ca pe un indiciu, nu ca pe o masuratoare."),
        };

        /// <summary>Why a field wider than <see cref="Th.LightFovMax"/> cannot be measured, in
        /// the console's lines: the console indents them, the window joins them into one
        /// paragraph.</summary>
        /// <returns>Four lines of one paragraph.</returns>
        static string[] DegeneracyLines() => new[]
        {
            T($"The frame covers too wide a field. Beyond {Th.LightFovMax:F0} degrees of diagonal the sky's light",
              $"Cadrul acopera un camp prea larg. Peste {Th.LightFovMax:F0} de grade pe diagonala lumina cerului"),
            T("is no longer even: it is brighter toward the horizon and fades toward the centre, like a dome,",
              "nu mai e uniforma: e mai luminoasa spre orizont si scade spre centru, ca o cupola,"),
            T("which is exactly the shape the vignetting has. The two cannot be told apart, however good",
              "adica exact forma pe care o are si vignetarea. Cele doua nu se pot desparti, oricat"),
            T("the program: any figure that came out would be the sky, not the flat.",
              "de bun ar fi programul: orice cifra ar iesi ar fi cerul, nu flat-ul."),
        };

        /// <summary>The sentence after the degeneracy, in both applications.</summary>
        static string NotALimitation => T("It is not a shortcoming of the program; that is the geometry.",
                                          "Nu e o lipsa a programului; asa e geometria.");

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
            w.WriteLine(T($"\n=== END-TO-END TEST: {path} ===", $"\n=== TEST END-TO-END: {path} ==="));
            w.WriteLine(T("    Expecting a stack CALIBRATED with the flat, but WITHOUT background extraction.",
                          "    Astept un stack CALIBRAT cu flat, dar FARA background extraction."));
            WriteSuitability(s, w);

            w.WriteLine(T($"\n    tiles used              : {r.KeptTiles} / {r.TotalTiles} (the rest = nebulosity/stars, rejected)",
                          $"\n    tile-uri folosite       : {r.KeptTiles} / {r.TotalTiles} (restul = nebulozitate/stele, respinse)"));
            w.WriteLine(T($"    sky gradient (plane)    : {r.PlanePct,5:F2} %  <- light pollution, IGNORE",
                          $"    gradient de cer (plan)  : {r.PlanePct,5:F2} %  <- poluare luminoasa, IGNORA")
                      + (r.PlanePct > Th.LightPlaneWarn ? T("  (large!)", "  (mare!)") : ""));
            w.WriteLine(T($"    radial component        : {Signed(r.RadPct),6} %   [{VerdictWords.Of(r.Verdict)}]  <- THIS is the flat",
                          $"    componenta radiala      : {Signed(r.RadPct),6} %   [{VerdictWords.Of(r.Verdict)}]  <- ASTA e flat-ul"));

            w.WriteLine(T("\n  INTERPRETATION", "\n  INTERPRETARE"));
            WriteDiagnosis(r.RadPct, w);

            // A single frame mixes the flat error with the sky's radial term of that moment, and
            // the second changes during the session. On a real series the figure swept 55
            // points in 81 minutes. The mean over the series separates much better, so it is
            // worth saying on every run on a single frame.
            w.WriteLine(T("\n    This figure is from a single frame, so it holds the sky of that moment too.",
                          "\n    Cifra asta e dintr-un singur cadru, deci contine si cerul din acel moment."));
            w.WriteLine(T("    Give the folder instead of the file to run on the whole series and average:",
                          "    Da-i folderul in loc de fisier ca sa rulez pe toata seria si sa mediez:"));
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
                return T("The sky background stayed as bright in the corners as at the centre: the flat removed\n"
                         + "the vignetting as it should. This is the real proof; the structure tests only anticipate it.",
                         "Fondul cerului a ramas la fel de luminos din centru pana in colturi: flat-ul a scos\n"
                         + "vignetarea cum trebuie. Asta e proba adevarata; testele de structura doar o anticipeaza.");

            if (radPct > 0)
                return T("The corners stayed darker than the centre: the flat did not remove all the vignetting.\n"
                         + "Usually the flats were shot at another aperture than the lights, or the flat holds something that is not vignetting.",
                         "Colturile au ramas mai intunecate decat centrul: flat-ul nu a scos toata vignetarea.\n"
                         + "De obicei flat-urile au fost facute la alta diafragma decat lights-urile, sau flat-ul are in el ceva ce nu e vignetare.");

            return T("The corners came out brighter than the centre: the flat corrected too much. Almost always\n"
                     + "the flat-darks or the bias are missing, so the flat still holds an offset and the division pulls the corners up.",
                     "Colturile au iesit mai luminoase decat centrul: flat-ul a corectat prea mult. Aproape intotdeauna\n"
                     + "lipsesc flat-dark-urile sau bias-ul, asa ca flat-ul mai contine un offset, iar impartirea trage colturile in sus.");
        }

        // ------------------------------------------------------------ for the window
        // The same field line, warning and refusal as WriteSuitability and WriteTooWide, in the
        // window's layout: no column padding, paragraphs instead of indented lines, and the
        // Structure tab named where the console names the structure tests.

        /// <summary>The field covered, for the window's card: the console's value
        /// (<see cref="FieldValue"/>), unpadded.</summary>
        /// <param name="s">The suitability check's answer, or null.</param>
        /// <returns>The diagonal in degrees with its source, "nedeterminat" with the source when
        /// only the focal length is known, "necunoscut" with the reason when nothing is known,
        /// or "" for null.</returns>
        internal static string FieldText(SuitabilityResult s) => s == null ? "" : FieldValue(s, 0);

        /// <summary>The warning under the window's field line: a marginal field's caution, or
        /// the note that the field could not be checked.</summary>
        /// <param name="s">The suitability check's answer, or null.</param>
        /// <returns>The warning, or "" for a suitable field or null.</returns>
        internal static string FieldWarning(SuitabilityResult s)
        {
            if (s == null) return "";

            if (s.Level == FovLevel.Marginal)
                return string.Join(" ", MarginalLines());

            if (s.Level == FovLevel.Unknown)
                return T("How wide the field is cannot be told: the file gives neither the focal length nor the "
                         + "scale. The test runs anyway, but without this check; unknown does not mean fine.",
                         "Nu stiu cat de larg e campul: fisierul nu spune nici focala, nici scara. Testul "
                         + "ruleaza oricum, dar fara aceasta verificare; necunoscut nu inseamna in regula.");

            return "";
        }

        /// <summary>Why the test does not apply on a field wider than
        /// <see cref="Th.LightFovMax"/>, for the window: the geometric degeneracy, the Structure
        /// tab as what is left to judge the flat on and, when the field was judged from the focal length
        /// alone, the console's note that the decision assumed the largest plausible sensor -
        /// without its <c>--fov</c> way out, which the window has no control for.</summary>
        /// <param name="s">The suitability check's answer, of level TooWide.</param>
        /// <returns>Three paragraphs separated by blank lines, a fourth on the focal length
        /// alone.</returns>
        internal static string TooWideText(SuitabilityResult s)
            => string.Join(" ", DegeneracyLines()) + "\n\n" + NotALimitation + "\n\n"
             + T("On a wide field the flat is judged only on the Structure tab, where the sky does not enter at all.",
                 "Pe un camp larg flat-ul se judeca doar in tabul Structura, unde cerul nu intra deloc.")
             + (JudgedOnTheFocalLengthAlone(s) ? "\n\n" + string.Join(" ", FocalOnlyLines) : "");
    }
}
