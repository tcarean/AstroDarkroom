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

// RatioReport.cs - RatioResult -> the console's text. Formatting only.
//
// Five pieces are exposed as values, not only written: Provenance, WorldValue,
// Precondition, WorldMeaning and SensorCaveat; beside them WorldLine, the console's form of
// WorldValue, and WorldNote and WorldCornerNote, which only the window shows. The same
// sentences appear in the window - under the verdict, beside the informative figure, in the
// provenance card - and two wordings of the same warning would have diverged exactly where it
// matters not to diverge.

using System.IO;
using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>
    /// The words of <c>--ratio</c>: the console report of a <see cref="RatioResult"/> and the
    /// sentences the window shows beside its figures.
    /// </summary>
    internal static class RatioReport
    {
        /// <summary>
        /// Writes the console report: the two stacks with their provenance, the precondition,
        /// the world-fixed component, then the structure of the combined master.
        /// </summary>
        /// <param name="r">The attribution result, with its header metadata filled.</param>
        /// <param name="pathA">The first master's path, as printed.</param>
        /// <param name="pathB">The second master's path, as printed.</param>
        /// <param name="w">Where to write.</param>
        internal static void Write(RatioResult r, string pathA, string pathB, TextWriter w)
        {
            w.WriteLine(T("\n=== ATTRIBUTION BY 180 ROTATION ===", "\n=== ATRIBUIRE PRIN ROTATIE 180 ==="));
            w.WriteLine($"    A : {pathA}");
            w.WriteLine($"        {Provenance(r.StackCountA, r.DateObsA)}");
            w.WriteLine($"    B : {pathB}");
            w.WriteLine($"        {Provenance(r.StackCountB, r.DateObsB)}");
            Indent(Precondition(), w);

            w.WriteLine(T("\n  COMPONENT FIXED IN THE WORLD  (informative - not a verdict)",
                          "\n  COMPONENTA FIXA IN LUME  (informativ - nu e un verdict)"));
            w.WriteLine(T($"    external grad./orientation  : {WorldLine(r)}",
                          $"    gradient extern / orientare : {WorldLine(r)}"));
            w.WriteLine(T($"    corner spread               : {r.WorldCornerPct,5:F2} %",
                          $"    imprastiere colturi         : {r.WorldCornerPct,5:F2} %"));
            Indent(WorldMeaning(), w);

            w.WriteLine(T("\n  STRUCTURE OF THE COMBINED MASTER (A+B)/2 - the part fixed to the camera",
                          "\n  STRUCTURA MASTERULUI COMBINAT (A+B)/2 - partea fixa pe aparat"));
            w.WriteLine(T($"    analysis size      : {r.Sensor.Width} x {r.Sensor.Height} (after 2x2 + downsample)",
                          $"    dimensiune analiza : {r.Sensor.Width} x {r.Sensor.Height} (dupa 2x2 + downsample)"));
            Indent(SensorCaveat(), w);

            StructureReport.WriteBody(r.Sensor, w);
        }

        /// <summary>
        /// Where each stack comes from. The only place in the report where one can see whether
        /// the two CAN really be from two different orientations: two stacks that start one
        /// right after the other had no time to be rotated. It is not an automatic rule -
        /// cadences vary - but it is the fact that was missing completely.
        /// </summary>
        /// <param name="stackCount">How many frames were stacked; 0 when unknown.</param>
        /// <param name="dateObs">The first exposure's DATE-OBS; null or empty when unknown.</param>
        /// <returns>The frame count and the first exposure's date, or that each is
        /// unknown.</returns>
        internal static string Provenance(int stackCount, string dateObs)
        {
            string frames = stackCount > 0
                ? T($"{stackCount} frames stacked", $"{stackCount} cadre stivuite")
                : T("number of frames unknown", "numar de cadre necunoscut");
            string when = string.IsNullOrEmpty(dateObs)
                ? T("date unknown", "data necunoscuta")
                : T($"first exposure {dateObs}", $"prima expunere {dateObs}");
            return $"{frames}, {when}";
        }

        /// <summary>What the mode assumes and cannot check. It appears in the window too.</summary>
        /// <returns>Four lines joined by '\n'.</returns>
        internal static string Precondition()
            => T("The two stacks must be from TWO orientations 180 degrees apart, with\n"
                 + "the camera rotated about the optical axis and nothing else changed. The tool can NOT\n"
                 + "check that from the pixels - check the rows above. Two stacks\n"
                 + "from the same orientation give a confident and meaningless result.",
                 "Cele doua stive trebuie sa fie din DOUA orientari la 180 de grade, cu\n"
                 + "aparatul rotit in jurul axei optice si nimic altceva schimbat. Tool-ul NU\n"
                 + "poate verifica asta din pixeli - verifica randurile de mai sus. Doua stive\n"
                 + "din aceeasi orientare dau un rezultat increzator si fara sens.");

        /// <summary>
        /// The informative figure with its error bar, without the label. The window shows it this
        /// way and puts the label apart, as the coloured pill - that is why there are two pieces
        /// and not one: otherwise the verdict would appear twice on the same row.
        /// </summary>
        /// <param name="r">The attribution result.</param>
        /// <returns>"N.NN % +/- N.NN %", per orientation.</returns>
        internal static string WorldValue(RatioResult r)
            => $"{r.WorldPtpPct,5:F2} % +/- {r.WorldErrPct:F2} %";

        /// <summary>
        /// The same figure with the label glued on, the way the console wants it.
        /// </summary>
        /// <param name="r">The attribution result.</param>
        /// <returns>The value and the label in brackets.</returns>
        internal static string WorldLine(RatioResult r)
            => $"{WorldValue(r)}   [{VerdictWords.Of(r.WorldLabel)}]";

        /// <summary>
        /// What the figure of the row "gradient extern / orientare" is and what the label beside
        /// it is. NO CALLER IN THE CONSOLE, like StructureReport.TestNote: the console already has
        /// "(informativ - nu e un verdict)" in the section's title, and the window puts the note
        /// under the row, where the title is far away and the coloured pill looks like a verdict.
        ///
        /// The thresholds are INTERPOLATED from Th.RatioOk / Th.RatioBad, not written as literals,
        /// for the same reason as in StructureReport.LocalSpotNote.
        /// </summary>
        /// <returns>One sentence, without a final full stop.</returns>
        internal static string WorldNote()
            => T("how much external gradient each orientation carried on its own: half of what "
                 + "the ratio A/B shows, because the rotation inverts it and A/B counts it twice. "
                 + "The label is informative and does not vote: OK under "
                 + $"{Th.RatioOk:F0} %, SUSPECT up to {Th.RatioBad:F0} %, {VerdictWords.Of("PROBLEMA")} above",
                 "cat gradient extern a purtat fiecare orientare in parte: jumatate din ce "
                 + "arata raportul A/B, fiindca rotatia il inverseaza si A/B il numara de doua ori. "
                 + "Eticheta e informativa si nu voteaza: OK sub "
                 + $"{Th.RatioOk:F0} %, SUSPECT pana la {Th.RatioBad:F0} %, PROBLEMA peste");

        /// <summary>
        /// The row "imprastiere colturi" of the world-fixed component. It sat on screen without
        /// a single word. No caller in the console either.
        /// </summary>
        /// <returns>One sentence, without a final full stop.</returns>
        internal static string WorldCornerNote()
            => T("the same external gradient, read only in the four corners of the ratio A/B: "
                 + "how much the brightest corner differs from the darkest, again per orientation. "
                 + "The partial view of the row above, as the corner spread is for the residual",
                 "acelasi gradient extern, citit doar in cele patru colturi ale raportului A/B: "
                 + "cat difera coltul cel mai luminos de cel mai intunecat, tot per orientare. "
                 + "Vederea partiala a randului de sus, cum e imprastierea colturilor pentru reziduu");

        /// <summary>What the world-fixed figure and its error bar mean for your master.</summary>
        /// <returns>Four lines joined by '\n'.</returns>
        internal static string WorldMeaning()
            => T("That much the rotation removed from the master, if you stacked both orientations. The error\n"
                 + "bar is the part of A/B that does NOT invert with the rotation, so the noise\n"
                 + "of the measurement: when the figure and its error are comparable, nothing was detected\n"
                 + "above the noise.",
                 "Atat a scos rotatia din master, daca ai stivuit ambele orientari. Bara de\n"
                 + "eroare e partea din A/B care NU se inverseaza la rotatie, deci zgomotul\n"
                 + "masuratorii: cand cifra si eroarea ei sunt comparabile, nu s-a detectat\n"
                 + "nimic peste zgomot.");

        /// <summary>
        /// The limit that matters most and does not show from the figures: "fixed to the camera"
        /// does not mean "harmless". It appears in the window too, under the verdict.
        /// </summary>
        /// <returns>Five lines joined by '\n'.</returns>
        internal static string SensorCaveat()
            => T("What follows is what was left after everything the rotation could remove was removed. Being\n"
                 + "fixed to the camera, it divides out of the lights correctly - BUT only if it was the same\n"
                 + "when you shot the lights. The lens's vignetting was. A strap\n"
                 + "fallen over the lens, condensation or a crooked lens hood rotate with the\n"
                 + "camera too, look identical here, and are no longer there for the lights.",
                 "Ce urmeaza e ce a ramas dupa ce s-a scos tot ce putea scoate rotatia. Fiind\n"
                 + "fix pe aparat, se imparte corect din lights - DAR numai daca era la fel si\n"
                 + "cand ai fotografiat lights-urile. Vignetarea obiectivului era. O curea\n"
                 + "cazuta pe lentila, condensul sau o parasolara stramba se rotesc si ele cu\n"
                 + "aparatul, arata identic aici, si la lights nu mai sunt acolo.");

        /// <summary>Writes each line of a text indented by four spaces.</summary>
        /// <param name="text">The lines, joined by '\n'.</param>
        /// <param name="w">Where to write.</param>
        static void Indent(string text, TextWriter w)
        {
            foreach (string line in text.Split('\n'))
                w.WriteLine("    " + line);
        }
    }
}
