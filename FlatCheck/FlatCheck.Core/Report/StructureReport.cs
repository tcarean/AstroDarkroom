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

// StructureReport.cs - StructureResult -> the console's text, and the pieces the window shows
// beside it (advice, notes, the profile card, the corner shares). Formatting only.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static FlatCheck.Ui;

namespace FlatCheck
{
    /// <summary>
    /// A row of the table "the vignetting against the lens's profile", ready formatted: the
    /// source (Adobe / lensfun), the lens, the mount and the calibration (Where), the profile
    /// used, its depth, the difference from the master, the error at the corner and the verdict;
    /// IsNearest on the one that gives the verdict, Note with the calibration format against the
    /// sensor (IsWarning when it is a large extrapolation).
    /// </summary>
    /// <param name="Key">The lens's catalog key.</param>
    /// <param name="Source">"Adobe" or "lensfun", as displayed.</param>
    /// <param name="Lens">The lens's name.</param>
    /// <param name="Where">The mount and the calibration it was profiled on.</param>
    /// <param name="Profile">The profile's settings (focal length, aperture).</param>
    /// <param name="Depth">The profile's depth at the corner, in stops and percent.</param>
    /// <param name="Diff">The master's depth minus the profile's, in stops, signed.</param>
    /// <param name="Error">The corner error that difference leaves, in percent.</param>
    /// <param name="Verdict">The row's verdict, on the residual scale.</param>
    /// <param name="IsNearest">True on the row that gives the comparison's verdict.</param>
    /// <param name="Note">The calibration format against the sensor, and any mismatch
    /// warning.</param>
    /// <param name="IsWarning">True when the note is a warning: a large extrapolation or a
    /// profile far from the shot's settings.</param>
    internal sealed record ProfileRow(string Key, string Source, string Lens, string Where, string Profile,
                                      string Depth, string Diff, string Error, string Verdict,
                                      bool IsNearest, string Note, bool IsWarning);

    /// <summary>
    /// The words of the structure mode: the console report of a <see cref="StructureResult"/>
    /// (and of a run with its lens-profile comparison), and every sentence the window shows
    /// beside the tests, the scale and the verdict.
    /// </summary>
    internal static class StructureReport
    {
        /// <summary>Writes the report of a structure result without a profile comparison.</summary>
        /// <param name="r">The structure result.</param>
        /// <param name="path">The master's path, as the header prints it.</param>
        /// <param name="w">Where to write.</param>
        internal static void Write(StructureResult r, string path, TextWriter w)
            => Write(new StructureOutcome(r, null, null, null), path, w);

        /// <summary>
        /// With the vignetting comparison, when the run had one; otherwise identical to the variant
        /// above.
        /// </summary>
        /// <param name="o">The whole run: the structure result and, when made, the
        /// comparison.</param>
        /// <param name="path">The master's path, as the header prints it.</param>
        /// <param name="w">Where to write.</param>
        internal static void Write(StructureOutcome o, string path, TextWriter w)
        {
            StructureResult r = o.Structure;
            w.WriteLine(T($"\n=== STRUCTURE: {path} ===", $"\n=== STRUCTURA: {path} ==="));
            w.WriteLine(T($"    analysis size      : {r.Width} x {r.Height} (after 2x2 + downsample)",
                          $"    dimensiune analiza : {r.Width} x {r.Height} (dupa 2x2 + downsample)"));
            w.WriteLine($"    median / max ADU   : {Levels(r)}");
            WriteBody(r, w, o);
        }

        /// <summary>
        /// "median / max", formatted by the file's scale. Siril writes the master as a float
        /// divided by 65535, so the median is ~0.014 - and with zero decimals the line came out
        /// "0 / 0", as if the file were empty. The threshold of 1.5 is the same one --expo
        /// uses to recognise a normalised file. The window shows it too.
        /// </summary>
        /// <param name="r">The structure result, with RawMedian and RawMax.</param>
        /// <returns>The two levels: whole numbers on an ADU file; four decimals and a note on a
        /// normalised one (RawMax at most 1.5).</returns>
        internal static string Levels(StructureResult r)
        {
            bool normalised = r.RawMax <= 1.5;
            string fmt = normalised ? "F4" : "F0";
            string line = $"{r.RawMedian.ToString(fmt)} / {r.RawMax.ToString(fmt)}";
            return normalised ? line + T(" (normalised float 0..1)", " (float normalizat 0..1)") : line;
        }

        /// <summary>
        /// Everything that follows the header: the informative figures, the five tests, the
        /// scale and the verdict, with the profile comparison's block and verdict when the run
        /// had one.
        /// </summary>
        /// <param name="r">The structure battery's result to write.</param>
        /// <param name="w">Where the text goes.</param>
        /// <param name="o">
        /// The whole run, when it had a comparison with the profiles: its block is written after
        /// the scale, and the verdict and the advice are its own (the worst of shape and depth).
        /// Null or without a comparison = the five tests and nothing more.
        /// </param>
        static void WriteBody(StructureResult r, TextWriter w, StructureOutcome o)
        {
            WriteInformative(r, w);

            w.WriteLine(T("\n  TESTS", "\n  TESTE"));
            w.WriteLine($"    {TestLabel(nameof(StructureResult.CornerSpreadPct)),-24}: {r.CornerSpreadPct,5:F2} %   [{VerdictWords.Of(r.CornerVerdict)}]");
            foreach (var kv in r.Corners)
                w.WriteLine(T($"        {CornerName(kv.Key),-14}: {CornerSharePct(r, kv.Value),6:F2} % of the corners' mean",
                              $"        {CornerName(kv.Key),-14}: {CornerSharePct(r, kv.Value),6:F2} % din media colturilor"));
            w.WriteLine($"    {TestLabel(nameof(StructureResult.AzimuthalMaxPct)),-24}: {r.AzimuthalMaxPct,5:F2} %   [{VerdictWords.Of(r.AzimuthalVerdict)}]");
            w.WriteLine($"    {TestLabel(nameof(StructureResult.ResidualPtpPct)),-24}: {r.ResidualPtpPct,5:F2} %   [{VerdictWords.Of(r.ResidualVerdict)}]  "
                        + T("<- THE MOST IMPORTANT", "<- CEL MAI IMPORTANT"));
            w.WriteLine($"    {TestLabel(nameof(StructureResult.TiltPct)),-24}: {r.TiltPct,5:F2} %   [{VerdictWords.Of(r.TiltVerdict)}]");
            w.WriteLine($"    {TestLabel(nameof(StructureResult.MaxTilePct)),-24}: {r.MaxTilePct,5:F2} %   [{VerdictWords.Of(r.MaxTileVerdict)}]");

            WriteScale(w);

            if (o?.Profiles != null) WriteProfiles(o, w);

            w.WriteLine($"\n  VERDICT: {VerdictWords.Of(o?.Verdict ?? r.Verdict)}");
            if (o != null && o.Compared)
            {
                Indent(VerdictDetail(o), w);
                Indent(Advice(o), w);
            }
            else WriteAdvice(r.Verdict, w);
        }

        // ------------------------------------------------ the vignetting against the lens's
        // profile

        /// <summary>The block's title; the window puts it on its card.</summary>
        internal static string ProfilesHeading
            => T("VIGNETTING against the lens profile", "VIGNETAREA fata de profilul obiectivului");

        /// <summary>
        /// The block of the profile comparison: the master's depth, one row per profile, the
        /// summary and the notes - or why it could not compare.
        /// </summary>
        /// <param name="o">The run, with a non-null comparison.</param>
        /// <param name="w">Where to write.</param>
        static void WriteProfiles(StructureOutcome o, TextWriter w)
        {
            VignettingComparison c = o.Profiles;
            w.WriteLine($"\n  {ProfilesHeading}");
            if (!c.Compared)
            {
                Indent(T("cannot compare: ", "nu se poate compara: ") + GapText(o), w);
                Indent(GapMeaning(), w);
                return;
            }
            w.WriteLine($"    master                  : {MasterDepth(c)}");
            foreach (ProfileRow row in ProfileRows(c))
            {
                w.WriteLine($"    {row.Source,-7} {row.Lens}  [{row.Where}]");
                w.WriteLine(T($"        profile {row.Profile,-23}: {row.Depth}   difference {row.Diff} = {row.Error} [{VerdictWords.Of(row.Verdict)}]",
                              $"        profil {row.Profile,-24}: {row.Depth}   diferenta {row.Diff} = {row.Error} [{VerdictWords.Of(row.Verdict)}]")
                          + (!row.IsNearest ? ""
                             : o.Shot?.ChosenLens != null ? T("  <- the lens you chose, gives the verdict", "  <- obiectivul ales de tine, da verdictul")
                             : T("  <- the nearest, gives the verdict", "  <- cel mai apropiat, da verdictul")));
                if (row.IsWarning) w.WriteLine("        " + row.Note);
            }
            Indent(ComparisonSummary(c), w);
            w.WriteLine($"    ({AgainstReport.DepthScale()})");
            Indent(DepthScaleNote(), w);
            Indent(DepthLightScaleNote(c.Nearest.DiffStops), w);
            Indent(ComparisonNote(), w);
        }

        /// <summary>
        /// What the --light mode would read from the same difference, and why the depth verdict
        /// does not sit on its scale: the error at the corner, its word on --light's 2 / 5 % scale
        /// (through Stats.DepthOnLightScale, so with its thresholds, not copied), and the
        /// reference's uncertainty, Th.ProfileDatabaseSpreadStops, translated into the same
        /// corner error. The two scales judge the same quantity with different uncertainties; the
        /// report shows both instead of choosing for the reader. Printed by the console under the
        /// depth scale and by --against under its depth row; the Structure tab shows it in the
        /// profile card, under the comparison's summary. On NaN it says it cannot be read, like
        /// the rest of the block.
        /// </summary>
        /// <param name="diffStops">The master's depth minus the profile's, stops; NaN when
        /// undetermined.</param>
        /// <returns>Four lines joined by '\n'; two on NaN.</returns>
        internal static string DepthLightScaleNote(double diffStops)
        {
            double spread = Stats.DepthErrorPct(Th.ProfileDatabaseSpreadStops);
            string reference = T(
                $"But the two profile databases differ from each other by {Th.ProfileDatabaseSpreadStops:F1} stops, that is {spread:F0} % in the corner,\n"
                + "so the verdict here cannot be finer than that and stays on the five tests' scale.",
                $"Dar cele doua baze de profile difera intre ele cu {Th.ProfileDatabaseSpreadStops:F1} stopuri, adica {spread:F0} % in colt,\n"
                + "asa ca verdictul de aici nu poate fi mai fin de atat si ramane pe scara celor cinci teste.");
            if (double.IsNaN(diffStops))
                return T("The End-to-end test would have nothing to read from it (undetermined). ",
                         "Testul End-to-end nu ar avea ce citi din ea (nedeterminata). ") + reference;
            double err = Stats.DepthErrorPct(diffStops);
            string word = VerdictWords.Of(Stats.DepthOnLightScale(diffStops));
            return T($"In the corner of a light calibrated with this master the error would be {err:F1} %. The End-to-end test,\n"
                     + $"which measures the same thing directly in the light, would call it {word} ({Th.LightRadOk:F0} % OK, {Th.LightRadBad:F0} % {VerdictWords.Of("PROBLEMA")}).\n",
                     $"In coltul unui light calibrat cu acest master eroarea ar fi {err:F1} %. Testul End-to-end,\n"
                     + $"care masoara acelasi lucru direct in light, i-ar spune {word} ({Th.LightRadOk:F0} % OK, {Th.LightRadBad:F0} % PROBLEMA).\n")
                 + reference;
        }

        /// <summary>
        /// The depth scale as data, for the window's one verdict scale (R-126): the same four
        /// bands as the residual scale, with the interval in stops, shown beside the five tests'
        /// interval in percent and their meaning. The thresholds are still Th.Resid*, passed
        /// through Stats.StopsForError - not a second list of numbers.
        /// </summary>
        /// <returns>Four rows, PERFECT to REFA: the band and its interval in stops.</returns>
        internal static IReadOnlyList<(string Band, string Range)> DepthScaleRows()
        {
            string S(double pct) => Stats.StopsForError(pct).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            return new[]
            {
                ("PERFECT", T($"under {S(Th.ResidPerfect)} stops", $"sub {S(Th.ResidPerfect)} stopuri")),
                ("BUN", T($"{S(Th.ResidPerfect)} - {S(Th.ResidUsable)} stops", $"{S(Th.ResidPerfect)} - {S(Th.ResidUsable)} stopuri")),
                ("ACCEPTABIL", T($"{S(Th.ResidUsable)} - {S(Th.ResidCheck)} stops", $"{S(Th.ResidUsable)} - {S(Th.ResidCheck)} stopuri")),
                ("REFA", T($"over {S(Th.ResidCheck)} stops", $"peste {S(Th.ResidCheck)} stopuri")),
            };
        }

        /// <summary>
        /// Where the depth scale comes from: it is not a new threshold, it is the five tests' scale
        /// translated into stops. The console prints it in the profile block; the window puts it
        /// under its one verdict scale (R-126).
        /// </summary>
        /// <returns>Three lines joined by '\n'.</returns>
        internal static string DepthScaleNote()
            => T($"The same scale as the five tests' ({Th.ResidPerfect:F0} / {Th.ResidUsable:F0} / {Th.ResidCheck:F0} %), applied to the error the depth\n"
                 + "difference leaves in the corner of a light calibrated with the master, and translated\n"
                 + "into stops. It is not a new threshold.",
                 $"Aceeasi scara ca la cele cinci teste ({Th.ResidPerfect:F0} / {Th.ResidUsable:F0} / {Th.ResidCheck:F0} %), aplicata erorii pe care\n"
                 + "diferenta de adancime o lasa in coltul unui light calibrat cu masterul, si tradusa in\n"
                 + "stopuri. Nu e un prag nou.");

        /// <summary>
        /// "1.26 stopuri (colt = 42 % din centru)" - the master's depth, as the informative row
        /// measures it too.
        /// </summary>
        /// <param name="c">The comparison.</param>
        /// <returns>The master's depth in stops and as the corner's percent of the
        /// centre.</returns>
        internal static string MasterDepth(VignettingComparison c) => Depth(c.MasterStops);

        /// <summary>
        /// The master's depth without a comparison (R-118): the same figure, from the structure
        /// result itself - what the comparison carries as <see cref="VignettingComparison.MasterStops"/>.
        /// </summary>
        /// <param name="r">The five tests on the master.</param>
        /// <returns>The master's depth in stops and as the corner's percent of the
        /// centre.</returns>
        internal static string MasterDepth(StructureResult r) => Depth(r.VignetteStops);

        /// <summary>
        /// Why nothing is compared when a filter was on the lens (R-118, ADR 0078): the
        /// paragraph under the master's depth on the window's card. The console never prints it:
        /// its user leaves <c>--raw</c> out.
        /// </summary>
        /// <returns>The paragraph, in the interface language.</returns>
        internal static string FilteredLensNote()
            => T("With a filter on the lens the comparison has no meaning: any filter changes the vignetting "
               + "(a mount shades the corners, a glass darkens the edges), so the master's vignetting is no "
               + "longer the one in the lens's profile. The verdict is the five tests' alone; the depth above "
               + "is for information only.",
                 "Cu un filtru pe obiectiv comparatia nu are sens: orice filtru schimba vignetarea (inelul "
               + "umbreste colturile, sticla intuneca marginile), deci vignetarea masterului nu mai e cea din "
               + "profilul obiectivului. Verdictul e doar al celor cinci teste; adancimea de mai sus e doar "
               + "informativa.");

        /// <summary>A depth in stops, with the corner's level as a percent of the centre.</summary>
        /// <param name="stops">The depth, stops.</param>
        /// <returns>"N.NN stopuri (colt = N % din centru)".</returns>
        static string Depth(double stops)
            => T($"{stops:F2} stops (corner = {100 * Math.Pow(2, -stops):F0} % of centre)",
                 $"{stops:F2} stopuri (colt = {100 * Math.Pow(2, -stops):F0} % din centru)");

        /// <summary>
        /// One row per profile, ready formatted, for the console and for the window.
        /// </summary>
        /// <param name="c">The comparison.</param>
        /// <returns>The rows, in the comparison's order.</returns>
        internal static IReadOnlyList<ProfileRow> ProfileRows(VignettingComparison c)
        {
            var rows = new List<ProfileRow>();
            foreach (ProfileDepth d in c.Profiles)
            {
                LensProfile p = d.Profile;
                bool warning = d.Extrapolation > SyntheticFlatResult.SmallExtrapolation;
                string note = SyntheticReport.FormatMatch(p.Model, d.CameraCrop, d.Extrapolation);
                // The profile is the nearest to the raw's focal length and aperture; when it is far
                // (a raw at 20 mm on a lens with profiles up to 16), the row says so.
                string mismatch = c.Shot?.FocalMm != null && c.Shot.FStop != null
                    ? SyntheticReport.ProfileMismatch(p, c.Shot.FocalMm.Value, c.Shot.FStop.Value, d.LensProfiles) : null;
                if (mismatch != null) { note += T(" WARNING: ", " ATENTIE: ") + mismatch; warning = true; }
                rows.Add(new ProfileRow(p.Lens.Key, p.Lens.SourceLabel, p.Lens.Name, p.Lens.Where, p.Settings,
                    Depth(d.CornerStops), T($"{d.DiffStops:+0.00;-0.00} stops", $"{d.DiffStops:+0.00;-0.00} stopuri"), $"{d.ErrorPct:F1} %", d.Verdict,
                    ReferenceEquals(d, c.Nearest), note, warning));
            }
            return rows;
        }

        /// <summary>
        /// Whose the verdict is and how much the profiles differ among themselves. "The nearest",
        /// not "the worst": the two databases differ from each other by ~0.2 stops on the same
        /// lens, and a master that matches one of them is not wrong.
        /// </summary>
        /// <param name="c">The comparison, with at least one row.</param>
        /// <returns>Two or three lines joined by '\n'.</returns>
        internal static string ComparisonSummary(VignettingComparison c)
        {
            // With a lens chosen (R-121) the list is one row by the person's hand, not by the
            // databases: the verdict is "the lens you chose's", the chosen note says what was
            // left out, and the single-profile sentence, which would blame a missing second
            // database, is not printed.
            bool chosen = c.Shot?.ChosenLens != null;
            string who = $"{c.Nearest.Profile.Lens.SourceLabel} {c.Nearest.Profile.Lens.Title}";
            string whose = chosen
                ? T($"given by the profile of the lens you chose ({who})", $"dat de profilul obiectivului ales de tine ({who})")
                : T($"given by the nearest profile ({who})", $"dat de profilul cel mai apropiat ({who})");
            string verdict = T(
                $"the comparison's verdict: {VerdictWords.Of(c.Verdict)}, {whose}: "
                + $"{c.Nearest.DiffStops:+0.00;-0.00} stops against the profile = {c.Nearest.ErrorPct:F1} % error in the corner of a light calibrated with the master",
                $"verdictul comparatiei: {VerdictWords.Of(c.Verdict)}, {whose}: "
                + $"{c.Nearest.DiffStops:+0.00;-0.00} stopuri fata de profil = {c.Nearest.ErrorPct:F1} % eroare in coltul unui light calibrat cu masterul");
            string spread = c.Profiles.Count > 1
                ? T($"\nthe {c.Profiles.Count} profiles differ from each other by {c.SpreadStops:F2} stops in the corner; a master that matches one of them is not wrong",
                    $"\ncele {c.Profiles.Count} profile difera intre ele cu {c.SpreadStops:F2} stopuri in colt; un master care coincide cu unul din ele nu e gresit")
                : chosen ? ""
                : T("\na single profile: without a second profile database there is no telling how sure it is (they usually differ by ~0.2 stops)",
                    "\nun singur profil: fara o a doua baza de profile nu se stie cat de sigur e (de obicei difera cu ~0.2 stopuri)");
            return verdict + spread + (chosen ? ChosenLensNote(c.Shot) : RangeOnlyNote(c.Shot));
        }

        /// <summary>
        /// When the person chose the lens among the candidates found by range alone (R-121):
        /// which lens was compared, why there was a choice to make, and which lenses of the
        /// range were left out. Printed in place of <see cref="RangeOnlyNote"/>.
        /// </summary>
        /// <param name="shot">The shot's identity, with <see cref="ShotIdentity.ChosenLens"/>
        /// set; null, or none chosen, gives "".</param>
        /// <returns>The note, starting with '\n'; "" without a chosen lens.</returns>
        internal static string ChosenLensNote(ShotIdentity shot)
        {
            if (shot?.ChosenLens == null) return "";
            LensChoice chosen = shot.ChosenLens;
            string others = string.Join(", ", shot.Candidates.Where(c => c.Key != chosen.Key).Select(c => $"{c.SourceLabel} {c.Title}"));
            string leftOut = others.Length == 0 ? ""
                : shot.CandidatesByRangeOnly
                ? T($" The other lenses of that range were left out: {others}.", $" Celelalte obiective cu aceasta gama au fost lasate deoparte: {others}.")
                : T($" The other candidates were left out: {others}.", $" Celelalte candidate au fost lasate deoparte: {others}.");
            // Why there was a choice: on a list by range alone the file could not tell the lens;
            // on any other list (a caller narrowing an ID-found list by key) that sentence
            // would be false, so only the choice and what it left out are said.
            string why = shot.CandidatesByRangeOnly
                ? T($": the file only says {shot.RawLensLabel}, a manual lens or one unknown to the profile databases, so the lens could not be told on its own.",
                    $": din fisier se stie doar {shot.RawLensLabel}, un obiectiv manual sau unul necunoscut bazelor de profile, deci obiectivul nu se putea afla singur.")
                : ".";
            return T($"\ncompared with the lens you chose, {chosen.SourceLabel} {chosen.Title}" + why + leftOut,
                     $"\ncomparat cu obiectivul ales de tine, {chosen.SourceLabel} {chosen.Title}" + why + leftOut);
        }

        /// <summary>
        /// The window's line above its lens picker (R-121), on a list found by range alone with
        /// more than one lens: why there is a choice to make and what it does.
        /// </summary>
        /// <param name="shot">The shot's identity; null, or a list not by range, gives "".</param>
        /// <returns>The paragraph, in the interface language; "" when there is nothing to choose.</returns>
        internal static string LensChoicePrompt(ShotIdentity shot)
            => shot != null && shot.OffersLensChoice
                ? T($"The file only says {shot.RawLensLabel}: a manual lens or one the profile databases do not know, so the list holds every lens "
                    + "with that range for your camera's mount. Choose yours and the verdict is given on its profile; without a choice the nearest profile decides.",
                    $"Din fisier se stie doar {shot.RawLensLabel}: un obiectiv manual sau unul pe care bazele de profile nu il cunosc, asa ca lista are toate "
                    + "obiectivele cu aceasta gama pentru montura camerei tale. Alege-l pe al tau si verdictul se da pe profilul lui; fara alegere decide profilul cel mai apropiat.")
                : "";

        /// <summary>
        /// When the lenses were found only by their range (a manual lens, no usable ID): the list
        /// may include another lens with the same range, and the person must know.
        /// </summary>
        /// <param name="shot">The shot's identity; null when unknown.</param>
        /// <returns>The note, starting with '\n'; "" when the lenses were matched by ID.</returns>
        internal static string RangeOnlyNote(ShotIdentity shot)
            => shot != null && shot.CandidatesByRangeOnly
                ? T($"\nthe lenses above are all those with {shot.RawLensLabel} for this mount: a manual lens or one unknown to the profile databases. "
                    + "If another one with the same range is among them, read your lens's row, not the nearest one.",
                    $"\nobiectivele de mai sus sunt toate cele cu {shot.RawLensLabel} pentru aceasta montura: un obiectiv manual sau unul necunoscut bazelor de profile. "
                    + "Daca printre ele e si altul cu aceeasi gama, citeste randul obiectivului tau, nu pe cel mai apropiat.")
                : "";

        /// <summary>
        /// What the comparison can and cannot say. The window shows it too, under the table.
        /// </summary>
        /// <returns>Three lines joined by '\n'.</returns>
        internal static string ComparisonNote()
            => T("The difference is symmetric about the centre, like the vignetting, so the five tests and the map do not\n"
                 + "see it: this is the only place where a source weaker seen at an angle (plus) or one that fills the corners (minus) shows.\n"
                 + "The profile describes the lens model, not your copy, and ignores colour; the master is taken as the mean of the sensor's four colours.",
                 "Diferenta e simetrica in jurul centrului, ca vignetarea, deci cele cinci teste si harta nu o vad:\n"
                 + "aici e singurul loc unde se vede o sursa mai slaba vazuta din unghi (plus) sau una care umple colturile (minus).\n"
                 + "Profilul descrie modelul obiectivului, nu exemplarul tau, si nu tine cont de culoare; masterul e luat ca medie a celor patru culori ale senzorului.");

        /// <summary>Why it could not compare, in words.</summary>
        /// <summary>
        /// "Give --lens &lt;text&gt; with --focal &lt;mm&gt; and --fstop &lt;f&gt;" - what the console
        /// needs once the raw has said nothing: the lens by name, and whichever of the focal
        /// length (FOCALLEN may carry it) and the aperture (Siril never writes it) is unknown too.
        /// </summary>
        /// <param name="shot">The shot's identity, for what is still unknown; null asks for
        /// all three.</param>
        /// <returns>The remedy sentence, without a final full stop.</returns>
        internal static string RawWithoutLensRemedy(ShotIdentity shot)
        {
            var missing = new List<string>();
            if (shot?.FocalMm == null) missing.Add("--focal <mm>");
            if (shot?.FStop == null) missing.Add("--fstop <f>");
            string with = missing.Count == 0 ? "" : T(" with ", " cu ") + string.Join(T(" and ", " si "), missing);
            return T("Give --lens <text>", "Da --lens <text>") + with;
        }

        /// <param name="o">The run, with a comparison that was not made.</param>
        /// <returns>The reason, with the option that fixes it; "" for None.</returns>
        internal static string GapText(StructureOutcome o)
        {
            VignettingComparison c = o.Profiles;
            ShotIdentity shot = c.Shot;
            switch (c.Gap)
            {
                case ComparisonGap.NoProfiles:
                    return T("no lens profile on this machine - neither Adobe (Lightroom / Camera Raw / DNG Converter) nor the lensfun/ folder beside the program",
                             "niciun profil de obiectiv pe masina - nici Adobe (Lightroom / Camera Raw / DNG Converter), nici folderul lensfun/ langa program");
                case ComparisonGap.NoRaw:
                    return o.RawError != null
                        ? T($"the raw beside the master could not be read ({o.RawError}), so the lens is not known",
                            $"raw-ul de langa master nu s-a putut citi ({o.RawError}), deci nu se stie obiectivul")
                        : T("no raw beside the master (in flats/), so no lens ID; a FITS does not say which lens made it. Give --lens <text> or --raw <file>",
                            "niciun raw langa master (in flats/), deci niciun ID de obiectiv; un FITS nu spune ce obiectiv l-a facut. Da --lens <text> sau --raw <fisier>");
                case ComparisonGap.RawWithoutLens:
                    return SyntheticReport.RawWithoutLensNote(shot?.RawName) + ". " + RawWithoutLensRemedy(shot);
                case ComparisonGap.NoLensMatch:
                    return shot != null && shot.RawNamesLens
                        ? T($"{shot.RawLensLabel}: no lens in the catalog matches it (by ID, name and range). Give --lens <text>",
                            $"{shot.RawLensLabel}: niciun obiectiv din catalog nu i se potriveste (dupa ID, nume si gama). Da --lens <text>")
                        : T("no lens matches the text given; see --lenses",
                            "niciun obiectiv nu se potriveste cu textul dat; vezi --lenses");
                case ComparisonGap.NoFocal:
                    return T("the focal length is not known - no FOCALLEN in the master, and no raw; give --focal <mm>",
                             "focala nu se stie - nici FOCALLEN in master, nici raw; da --focal <mm>");
                case ComparisonGap.NoFStop:
                    return T("the aperture is not known - Siril does not write it in the master, and there was no raw beside it; give --fstop <f> or --raw <file>",
                             "diafragma nu se stie - Siril n-o scrie in master, si n-a fost niciun raw langa el; da --fstop <f> sau --raw <fisier>");
                case ComparisonGap.NoCrop:
                    return T($"lensfun profiles only, and the camera's crop is not known ({shot?.CropSource}); give --crop <factor>",
                             $"doar profile lensfun, si crop-ul aparatului nu se stie ({shot?.CropSource}); da --crop <factor>");
                default:
                    return "";
            }
        }

        /// <summary>
        /// What the comparison would have shown, so that it is known what is missing.
        /// </summary>
        /// <returns>Two lines joined by '\n'.</returns>
        internal static string GapMeaning()
            => T("The comparison would have said whether the master's vignetting is as deep as the lens's: a surplus symmetric\n"
                 + "about the centre, from a panel seen at an angle or from the sky's dome, goes unnoticed through the five tests.",
                 "Comparatia ar fi spus daca vignetarea masterului e cat a obiectivului: un surplus simetric in jurul\n"
                 + "centrului, de la un panou vazut din unghi sau de la cupola cerului, trece neobservat prin cele cinci teste.");

        // ------------------------------------------------ without Adobe's profiles (R-113)
        //
        // The window's card only (ADR 0073): the console's report does not print these, so the
        // saved report and the goldens stay as they were.

        /// <summary>Adobe's page for the free DNG Converter, with its Windows downloads
        /// (checked in a browser on 2026-10-28; the older helpx address redirects here).</summary>
        internal const string AdobeDngConverterUrl = "https://helpx.adobe.com/camera-raw/desktop/dng-and-file-formats/adobe-dng-converter.html";

        /// <summary>
        /// The red paragraph at the top of the window's profile card when the search found no
        /// Adobe file: that the profiles are not on this computer and, when lensfun's lenses
        /// were read, what that leaves. A search that found Adobe's files has nothing to say,
        /// and neither has a run that searched nothing (no raw beside the master).
        /// </summary>
        /// <param name="s">The sources the run searched; null when it searched
        /// nothing.</param>
        /// <returns>Two sentences, one when no lensfun lens was read either (the card's gap then
        /// says no profile is on the machine); "" when <paramref name="s"/> is null or holds at
        /// least one Adobe file.</returns>
        internal static string AdobeMissing(ProfileSources s)
        {
            if (s == null || s.AdobeFiles > 0) return "";
            string missing = T("Adobe's lens profiles were not found on this computer.",
                               "Profilele de obiectiv Adobe nu au fost gasite pe acest calculator.");
            // The gap's own condition: no lensfun lens read, whether the folder is missing,
            // cannot be listed (R-115) or none of its files could be read (it then counts as
            // present).
            return s.LensfunLenses == 0 ? missing
                 : missing + " " + T("Without them the lens is looked for in lensfun alone, the profile database that ships with "
                                     + "FlatCheck: one database instead of two, and if the raw does not give the lens's name (a Nikon "
                                     + "writes only a code), it is found by its focal range alone.",
                                     "Fara ele obiectivul e cautat doar in lensfun, baza de profile livrata cu FlatCheck: o singura "
                                     + "baza in loc de doua, iar daca raw-ul nu spune numele obiectivului (un Nikon scrie doar un "
                                     + "cod), el e gasit doar dupa gama de focale.");
        }

        /// <summary>
        /// What to install to get Adobe's profiles, and that FlatCheck must then restart: the
        /// window locates the profiles once while it runs. Shown under <see cref="AdobeMissing"/>,
        /// above the link to <see cref="AdobeDngConverterUrl"/>.
        /// </summary>
        /// <returns>Two sentences.</returns>
        internal static string AdobeInstall()
            => T("To get them, install the free Adobe DNG Converter, from the page below; Lightroom Classic, Lightroom and "
                 + "Photoshop's Camera Raw install them too. Then restart FlatCheck: it looks for them only once while it runs.",
                 "Ca sa le ai, instaleaza Adobe DNG Converter, gratuit, de pe pagina de mai jos; le instaleaza si Lightroom Classic, "
                 + "Lightroom si Camera Raw din Photoshop. Apoi reporneste FlatCheck: le cauta o singura data cat timp ruleaza.");

        /// <summary>The folders the search looked in, for the line under the link.</summary>
        /// <param name="s">The sources the run searched; null when it searched
        /// nothing.</param>
        /// <returns>"Searched in: " and the folders, comma-separated; "" whenever
        /// <see cref="AdobeMissing"/> is.</returns>
        internal static string AdobeSearched(ProfileSources s)
            => s == null || s.AdobeFiles > 0 ? ""
             : T("Searched in: ", "Cautate in: ") + string.Join(", ", s.AdobeDirectories);

        /// <summary>
        /// The two parts of the verdict, on two rows, as the console prints them under VERDICT.
        /// </summary>
        /// <param name="o">The run, with a comparison made.</param>
        /// <returns>The shape row, the depth row and <see cref="WorseOfBoth"/>, joined by
        /// '\n'.</returns>
        internal static string VerdictDetail(StructureOutcome o)
            => T($"shape     : {ShapeDetail(o)}\ndepth     : {DepthDetail(o)}\n{WorseOfBoth}",
                 $"forma     : {ShapeDetail(o)}\nadancimea : {DepthDetail(o)}\n{WorseOfBoth}");

        /// <summary>
        /// "PERFECT - cele cinci teste". Separate, so that the
        /// window can put its own label in front.
        /// </summary>
        /// <param name="o">The run.</param>
        /// <returns>The shape verdict and what it covers.</returns>
        internal static string ShapeDetail(StructureOutcome o)
            => T($"{VerdictWords.Of(o.Structure.Verdict)} - the five tests",
                 $"{VerdictWords.Of(o.Structure.Verdict)} - cele cinci teste");

        /// <summary>
        /// "REFA - cu +0.34 stopuri fata de profilul lensfun Tokina ..., adica 27.0 % eroare in
        /// coltul light-ului calibrat".
        /// </summary>
        /// <param name="o">The run, with a comparison made.</param>
        /// <returns>The depth verdict, the difference against the nearest profile and its corner
        /// error.</returns>
        internal static string DepthDetail(StructureOutcome o)
        {
            ProfileDepth n = o.Profiles.Nearest;
            return T($"{VerdictWords.Of(o.Profiles.Verdict)} - by {n.DiffStops:+0.00;-0.00} stops against the {n.Profile.Lens.SourceLabel} {n.Profile.Lens.Title} profile, that is {n.ErrorPct:F1} % error in the corner of the calibrated light",
                     $"{VerdictWords.Of(o.Profiles.Verdict)} - cu {n.DiffStops:+0.00;-0.00} stopuri fata de profilul {n.Profile.Lens.SourceLabel} {n.Profile.Lens.Title}, adica {n.ErrorPct:F1} % eroare in coltul light-ului calibrat");
        }

        /// <summary>The line under the two parts of the verdict: the verdict is the worse.</summary>
        internal static string WorseOfBoth => T("The verdict is the worse of the two.", "Verdictul e cel mai rau dintre cele doua.");

        /// <summary>
        /// The run's advice: the depth's when it worsened the verdict, otherwise the shape's.
        /// </summary>
        /// <param name="o">The run, with a comparison made.</param>
        /// <returns>The advice, lines joined by '\n'.</returns>
        internal static string Advice(StructureOutcome o)
            => o.DepthCarriesTheVerdict ? DepthAdvice(o.Profiles.Nearest.DiffStops, o.Profiles.Verdict) : Advice(o.Structure.Verdict);

        /// <summary>
        /// What the person should do when the master's vignetting is not the lens's. The sign is
        /// the diagnosis: plus = the master falls more than the model, so the SOURCE falls with
        /// the angle (measured on the tablet masters: cos^2 of the field angle, +0.57 stops, made
        /// radial by the 90-degree rotation); minus = the source fills the corners (the sky's dome
        /// brightens toward the horizon all around the zenith, ambient light, a reflection).
        /// More rotations do not help: the excess rotates with the camera or is symmetrised by
        /// the rotation.
        /// </summary>
        /// <param name="diffStops">The master's depth minus the nearest profile's, stops.</param>
        /// <param name="verdict">The depth verdict; "REFA" adds the advice to reshoot.</param>
        /// <returns>The cause by the sign and its weight, lines joined by '\n'.</returns>
        internal static string DepthAdvice(double diffStops, string verdict)
        {
            string cause = diffStops > 0
                ? T("The master darkens toward the corner MORE than the lens profile says: the light source is\n"
                    + "weaker seen at an angle, a panel (monitor, tablet) too close or without a real diffuser; matte\n"
                    + "sheets do not diffuse. A real diffuser helps (opal acrylic, a thick stack of white paper)\n"
                    + "or a white wall lit from the side; more rotations do not help.",
                    "Masterul se intuneca spre colt MAI MULT decat spune profilul obiectivului: sursa de\n"
                    + "lumina e mai slaba vazuta din unghi, un panou (monitor, tableta) prea aproape sau fara un\n"
                    + "difuzor adevarat; foile mate nu difuzeaza. Ajuta un difuzor adevarat (acril opal, un teanc\n"
                    + "gros de hartie alba) sau un perete alb luminat din lateral; mai multe rotiri nu ajuta.")
                : T("The master darkens toward the corner LESS than the lens profile says: something fills the\n"
                    + "corners with light, the dome of the sky (it brightens toward the horizon all around), light\n"
                    + "around the source, a reflection in the lens hood. Shield the lens from everything that is not the\n"
                    + "source; on the sky, avoid the zenith and wait for a more uniform sky.",
                    "Masterul se intuneca spre colt MAI PUTIN decat spune profilul obiectivului: ceva umple\n"
                    + "colturile cu lumina, cupola cerului (se lumineaza spre orizont de jur imprejur), lumina din\n"
                    + "jurul sursei, o reflexie in parasolar. Fereste obiectivul de tot ce nu e sursa; pe cer,\n"
                    + "evita zenitul si asteapta un cer mai uniform.");
            string weight = verdict == "REFA"
                ? T("\nThe difference is symmetric about the centre, like the vignetting, so the five tests do not\n"
                    + "see it; background extraction does not remove it either. Redo the flats.",
                    "\nDiferenta e simetrica in jurul centrului, ca vignetarea, deci cele cinci teste nu o vad;\n"
                    + "nici extragerea de fond nu o scoate. Refa flat-urile.")
                : T($"\nThe difference is symmetric about the centre, like the vignetting, so the five tests do not\n"
                    + $"see it. Below {VerdictWords.Of("ACCEPTABIL")} it cannot be told from the profile's uncertainty (the two profile\n"
                    + "databases differ from each other by ~0.2 stops).",
                    "\nDiferenta e simetrica in jurul centrului, ca vignetarea, deci cele cinci teste nu o vad.\n"
                    + "Sub ACCEPTABIL nu se poate deosebi de nesiguranta profilului (cele doua baze de profile\n"
                    + "difera intre ele cu ~0.2 stopuri).");
            return cause + weight;
        }

        /// <summary>
        /// The quantities that are NOT tests. Vignetting is a property of the
        /// lens, not an error. The offset of the illumination centre was a
        /// test too until 1.2.0 and no longer is: being ~ -b/(2d), that is the ratio of the
        /// gradient to the curvature, it is not scale-invariant - and above the threshold at which
        /// the vertex leaves the frame, not even monotonic. See Metrics.FitCenter.
        /// </summary>
        /// <param name="r">The structure result.</param>
        /// <param name="w">Where to write.</param>
        static void WriteInformative(StructureResult r, TextWriter w)
        {
            w.WriteLine(T("\n  INFORMATIVE (not tests)", "\n  INFORMATIV (nu sunt teste)"));
            w.WriteLine(T($"    vignetting to corner    : {r.VignetteStops:F2} stops (corner = {CornerOverCentrePct(r):F0}% of centre)",
                          $"    vignetare centru->colt  : {r.VignetteStops:F2} stopuri (colt = {CornerOverCentrePct(r):F0}% din centru)"));
            if (r.CentroidValid)
            {
                w.WriteLine(T($"    illum. centre offset    : {r.CentroidOffsetPct,5:F2} %",
                              $"    offset centru iluminare : {r.CentroidOffsetPct,5:F2} %"));
                w.WriteLine(T($"        illumination peak = ({r.FitCx:F0}, {r.FitCy:F0})  vs  sensor centre = ({r.SensorCx:F0}, {r.SensorCy:F0})",
                              $"        varf iluminare = ({r.FitCx:F0}, {r.FitCy:F0})  vs  centru senzor = ({r.SensorCx:F0}, {r.SensorCy:F0})"));
            }
            else
            {
                w.WriteLine(T("    illum. centre offset    : undetermined (the brightest point is outside the frame)",
                              "    offset centru iluminare : nedeterminat (punctul cel mai luminos e in afara cadrului)"));
            }
            if (Ui.Current == UiLanguage.Romanian)
            {
                w.WriteLine("        Cifra e doar orientativa: pe un obiectiv aproape fara vignetare creste mult");
                w.WriteLine("        fara ca eroarea sa creasca, iar cand punctul cel mai luminos iese din cadru e");
                w.WriteLine("        nedeterminata. Cat de mare e dezechilibrul spune \"inclinare\".");
            }
            else
            {
                w.WriteLine("        The figure is only indicative: on a lens with almost no vignetting it grows a lot");
                w.WriteLine("        without the error growing, and once the brightest point leaves the frame it reads");
                w.WriteLine("        undetermined. How large the imbalance is, \"tilt\" says.");
            }
            w.WriteLine(T($"    local spots (dust etc.) : {LocalSpotValue(r)}",
                          $"    pete locale (praf etc.) : {LocalSpotValue(r)}"));
            foreach (string line in LocalSpotNote().Split('\n')) w.WriteLine("        " + line);
            string warning = LocalSpotWarning(r);
            if (warning != null) Indent(warning, w);
        }

        /// <summary>
        /// The "pete locale" figure: the signed size, what the sign means, and the position in
        /// the analysis raster.
        ///
        /// The sign is named right on the row, not left to be inferred. A minus on a line called
        /// "pete locale" looks like a calculation error if you do not know that the direction is
        /// precisely the information - the first question asked after the figure appeared was
        /// whether it can be negative.
        /// </summary>
        /// <param name="r">The structure result.</param>
        /// <returns>The signed percentage, the sign's word and the position.</returns>
        internal static string LocalSpotValue(StructureResult r)
            => LocalSpotValue(r.LocalSpotPct, r.LocalSpotX, r.LocalSpotY);

        /// <summary>
        /// The wording of a spot given by its figures; the overload above reads them from the
        /// result.
        /// </summary>
        /// <param name="pct">The spot's depth, percent, signed.</param>
        /// <param name="x">Its column, analysis raster pixels.</param>
        /// <param name="y">Its row, analysis raster pixels.</param>
        /// <returns>"+N.NN % (mai luminos) la (x, y)", with no word at zero.</returns>
        internal static string LocalSpotValue(double pct, int x, int y)
            => LocalSpotSize(pct) + T($" at ({x}, {y})", $" la ({x}, {y})");

        /// <summary>
        /// The spot's signed size and the sign's word, without the position - the cell of
        /// <c>--against</c>'s table, whose note gives the positions.
        /// </summary>
        /// <param name="pct">The spot's depth, percent, signed.</param>
        /// <returns>"+N.NN % (brighter)", with no word at zero.</returns>
        internal static string LocalSpotSize(double pct)
        {
            // What rounds to zero IS SHOWN as zero: .NET keeps the sign of a negative zero and
            // "-0.00001" came out "-+0.00" (the format's positive section, with the negative
            // zero's minus in front). Measured on a golden of the smoothing mode of the time.
            double shown = Math.Abs(pct) < 0.005 ? 0.0 : pct;
            return $"{shown:+0.00;-0.00} %{SignWord(shown)}";
        }

        /// <summary>
        /// A test's label, keyed by the name of the figure in <see cref="StructureResult"/>,
        /// like <see cref="TestNote"/>. It is what WriteBody prints in front of the figure; the
        /// against and split reports reuse it, so that no report names a test differently.
        /// </summary>
        /// <param name="metric">One of <see cref="TestKeys"/>.</param>
        /// <returns>The label; null for an unknown key.</returns>
        internal static string TestLabel(string metric) => metric switch
        {
            nameof(StructureResult.CornerSpreadPct) => T("spread of the 4 corners", "imprastiere 4 colturi"),
            nameof(StructureResult.AzimuthalMaxPct) => T("max azimuthal asymmetry", "asimetrie azimutala max"),
            nameof(StructureResult.ResidualPtpPct) => T("residual (radial model)", "reziduu dupa model rad."),
            nameof(StructureResult.TiltPct) => T("tilt (gradient)", "inclinare (gradient)"),
            nameof(StructureResult.MaxTilePct) => T("worst spot (one tile)", "pata maxima (o dala)"),
            _ => null,
        };

        /// <summary>
        /// The printed name of a corner of <see cref="StructureResult.Corners"/>, whose keys are
        /// the Romanian names they always were.
        /// </summary>
        /// <param name="key">A corner key, "stanga-sus" and the three others.</param>
        /// <returns>The corner's name in the run's language; an unknown key as it is.</returns>
        internal static string CornerName(string key) => T(EnglishCorner(key), key);

        /// <summary>A corner's key in English, the one name the diagnostic log writes whatever
        /// the language (R-110); the Romanian name is the key itself.</summary>
        /// <param name="key">A corner key, "stanga-sus" and the three others.</param>
        /// <returns>The English name; an unknown key as it is.</returns>
        internal static string EnglishCorner(string key) => key switch
        {
            "stanga-sus" => "top-left",
            "dreapta-sus" => "top-right",
            "stanga-jos" => "bottom-left",
            "dreapta-jos" => "bottom-right",
            _ => key,
        };

        /// <summary>The keys of the five tests, in the report's order.</summary>
        internal static readonly string[] TestKeys =
        {
            nameof(StructureResult.CornerSpreadPct), nameof(StructureResult.AzimuthalMaxPct),
            nameof(StructureResult.ResidualPtpPct), nameof(StructureResult.TiltPct),
            nameof(StructureResult.MaxTilePct),
        };

        /// <summary>
        /// The figure and the verdict of the test <paramref name="metric"/> of
        /// <paramref name="r"/>.
        /// </summary>
        /// <param name="r">The structure result.</param>
        /// <param name="metric">One of <see cref="TestKeys"/>; any other throws an
        /// ArgumentException.</param>
        /// <returns>The value, percent, and its verdict word.</returns>
        internal static (double Value, string Verdict) Test(StructureResult r, string metric) => metric switch
        {
            nameof(StructureResult.CornerSpreadPct) => (r.CornerSpreadPct, r.CornerVerdict),
            nameof(StructureResult.AzimuthalMaxPct) => (r.AzimuthalMaxPct, r.AzimuthalVerdict),
            nameof(StructureResult.ResidualPtpPct) => (r.ResidualPtpPct, r.ResidualVerdict),
            nameof(StructureResult.TiltPct) => (r.TiltPct, r.TiltVerdict),
            nameof(StructureResult.MaxTilePct) => (r.MaxTilePct, r.MaxTileVerdict),
            _ => throw new ArgumentException(T("unknown test: ", "test necunoscut: ") + metric),
        };

        /// <summary>
        /// The direction of the deviation, in words. Empty at exactly zero (a constant residual),
        /// where "brighter" would be a statement about nothing.
        /// </summary>
        /// <param name="pct">The spot's depth as shown, percent, signed.</param>
        /// <returns>" (mai intunecat)", " (mai luminos)", or "" at zero.</returns>
        static string SignWord(double pct)
            => pct < 0 ? T(" (darker)", " (mai intunecat)") : pct > 0 ? T(" (brighter)", " (mai luminos)") : "";

        /// <summary>
        /// What the figure is, what its sign means, and from what value a warning follows.
        ///
        /// The threshold is INTERPOLATED from <see cref="Th.LocalSpotWarn"/>, not written as a
        /// literal: otherwise a recalibration of the threshold would leave the text stating the
        /// old value, and this very text is the only place from which the user learns from where
        /// on the figure matters. Pinned by <c>TheLocalSpotNoteQuotesTheThresholdFromTh</c>.
        ///
        /// Returned as a string, like <see cref="Advice(StructureOutcome)"/>: the window shows it
        /// too, under the row.
        /// </summary>
        /// <returns>Four lines joined by '\n'.</returns>
        internal static string LocalSpotNote()
            => T("The most pronounced small spot, after the large shapes are taken out.\n"
                 + "The sign says which kind: minus = darker than its surroundings (dust,\n"
                 + "condensation, something in front of the lens), plus = brighter (light leak, reflection).\n"
                 + $"Above {Th.LocalSpotWarn:F0} % a warning follows; below that the figure is informative only.",
                 "Cea mai pronuntata pata mica, dupa ce se scot formele mari.\n"
                 + "Semnul spune ce fel: minus = mai intunecata decat imprejurimile (praf,\n"
                 + "condens, ceva in fata obiectivului), plus = mai luminoasa (scurgere de lumina, reflexie).\n"
                 + $"Peste {Th.LocalSpotWarn:F0} % urmeaza o atentionare; sub atat cifra e doar informativa.");

        /// <summary>A corner's level as a percentage of the four corners' mean - the ratio of the
        /// corner rows, in the console and in the window.</summary>
        /// <param name="r">The structure result.</param>
        /// <param name="corner">The corner's level, in the raster's units.</param>
        /// <returns>The percentage; 100 for a corner at the mean.</returns>
        static double CornerSharePct(StructureResult r, double corner) => corner / r.CornerMean * 100;

        /// <summary>The corners' mean as a percentage of the centre's median - the "colt = N %
        /// din centru" of the vignetting row, in the console and in the window.</summary>
        /// <param name="r">The structure result.</param>
        /// <returns>The percentage; 100 without vignetting.</returns>
        static double CornerOverCentrePct(StructureResult r) => r.CornerMean / r.CentreMedian * 100;

        /// <summary>The four corners, each as a percentage of their mean, on one line - the
        /// window's version of the corner rows Write prints under the corner spread.</summary>
        /// <param name="r">The structure result.</param>
        /// <returns>Each corner's name and percentage with one decimal, separated by three
        /// spaces.</returns>
        internal static string CornerShares(StructureResult r)
            => string.Join("   ", r.Corners.Select(
                c => $"{CornerName(c.Key)} {CornerSharePct(r, c.Value):F1} %"));

        /// <summary>The note under the window's vignetting row: the corner as a percentage of
        /// the centre, as WriteInformative prints it, and why it is not an error.</summary>
        /// <param name="r">The structure result.</param>
        /// <returns>One line.</returns>
        internal static string VignetteNote(StructureResult r)
            => T($"the corner is {CornerOverCentrePct(r):F0} % of the centre; "
                 + "that is the lens, not an error of the flat",
                 $"coltul e {CornerOverCentrePct(r):F0} % din centru; "
                 + "asa e obiectivul, nu e o eroare a flat-ului");

        /// <summary>The note under the window's illumination-centre row: the brightest point (the
        /// fitted vertex) against the sensor's centre and that the figure is only indicative, or
        /// that the point falls outside the frame.</summary>
        /// <param name="r">The structure result.</param>
        /// <returns>One line.</returns>
        internal static string CentroidNote(StructureResult r)
            => r.CentroidValid
                ? T($"the brightest point is at ({r.FitCx:F0}, {r.FitCy:F0}), the sensor's centre at ({r.SensorCx:F0}, {r.SensorCy:F0}); "
                    + "the figure is only indicative, how large the imbalance is \"tilt\" says",
                    $"punctul cel mai luminos e la ({r.FitCx:F0}, {r.FitCy:F0}), centrul senzorului la ({r.SensorCx:F0}, {r.SensorCy:F0}); "
                    + "cifra e doar orientativa, cat de mare e dezechilibrul spune \"inclinare\"")
                : T("the brightest point falls outside the frame; it cannot be measured",
                    "punctul cel mai luminos cade in afara cadrului; nu se poate masura");

        /// <summary>
        /// The warning for a compact spot, or null when there is none. It is not a verdict,
        /// and that is why it does not vote: a spot fixed to the camera is right for the session
        /// whose lights have it too, and wrong for a library flat - the answer depends on what you
        /// do with the flat, not on the flat. Returned as a string because the window shows it
        /// too, under the verdict.
        /// </summary>
        /// <param name="r">The structure result.</param>
        /// <returns>Four lines joined by '\n' when the spot is flagged, else null.</returns>
        internal static string LocalSpotWarning(StructureResult r)
            => r.LocalSpotFlagged
                ? T($"WARNING: small spot of {Math.Abs(r.LocalSpotPct):F2} % at ({r.LocalSpotX}, {r.LocalSpotY}), above {Th.LocalSpotWarn:F0} %.\n"
                    + "    It belongs to the camera: dust on the sensor or on a filter, condensation, a small light leak.\n"
                    + "    Fine for the session whose lights have the same spot; NOT for a\n"
                    + "    library flat. Look for the position above in the residual map.",
                    $"ATENTIE: pata mica de {Math.Abs(r.LocalSpotPct):F2} % la ({r.LocalSpotX}, {r.LocalSpotY}), peste {Th.LocalSpotWarn:F0} %.\n"
                    + "    Tine de aparat: praf pe senzor sau pe filtru, condens, o scurgere mica de lumina.\n"
                    + "    E in regula pentru sesiunea ale carei lights au aceeasi pata; NU pentru un\n"
                    + "    flat de biblioteca. Cauta pozitia de mai sus in harta reziduala.")
                : null;

        /// <summary>
        /// What each of the five tests measures, in one sentence.
        ///
        /// NO CALLER IN THE CONSOLE, like <see cref="RunReport.MapScaleLabel"/> and the others:
        /// the console report keeps its explanations grouped under the scale, and five notes
        /// interleaved between the rows would drown the very figures. The window puts each note
        /// under its row, where it costs nothing.
        ///
        /// They sit here all the same, and not in the ViewModel, because they are sentences of
        /// diagnosis: the rule is that no explanation for the user is written in a ViewModel,
        /// so that there are not two wordings of the same thing when another consumer appears.
        /// Two of them were in fact written inline there.
        ///
        /// The key is the name of the figure in <see cref="StructureResult"/>, not an index: a
        /// list by position would break silently if someone reorders the rows.
        /// </summary>
        /// <param name="metric">One of <see cref="TestKeys"/>.</param>
        /// <returns>The note; null for an unknown key.</returns>
        internal static string TestNote(string metric) => metric switch
        {
            nameof(StructureResult.CornerSpreadPct) => T(
                "how much the four corners differ from each other; on a correct flat they would be equally bright",
                "cat difera intre ele cele patru colturi; pe un flat corect ar fi la fel de luminoase"),

            nameof(StructureResult.AzimuthalMaxPct) => T(
                "how much the 12 slices of the same ring around the centre differ from each other, on "
              + "the most uneven ring; measured only in the circle that fits whole in the frame",
                "cat difera intre ele cele 12 felii ale aceluiasi inel din jurul centrului, pe inelul "
              + "cel mai neuniform; se masoara doar in cercul care incape intreg in cadru"),

            nameof(StructureResult.ResidualPtpPct) => T(
                "how much of the frame does NOT look like a usual vignetting, symmetric about the "
              + "centre; the figure that matters most",
                "cat din cadru NU arata ca o vignetare obisnuita, simetrica in jurul centrului; "
              + "cifra care conteaza cel mai mult"),

            nameof(StructureResult.TiltPct) => T(
                "how much brighter one edge of the frame is than the opposite one; it also catches a "
              + "lens whose axis is off-centre",
                "cu cat e o margine a cadrului mai luminoasa decat cea opusa; prinde si un obiectiv "
              + "cu axa descentrata"),

            nameof(StructureResult.MaxTilePct) => T(
                "the small square that stands out most from the others; it catches a small compact "
              + "defect, which the residual sets aside as an extreme",
                "patratelul care iese cel mai mult din randul celorlalte; prinde un defect mic si "
              + "compact, pe care reziduul il lasa deoparte ca pe o extrema"),

            _ => null,
        };

        /// <summary>
        /// The common scale of the five tests of deviation from radial symmetry: the band's
        /// name, the interval and the meaning. It does not apply to the offset of the illumination
        /// centre, which measures a position.
        ///
        /// It is returned as data, not as text already written, because two consumers read it:
        /// the report below, which lays it out in columns, and the WPF application, which puts it
        /// in one table with the depth's interval in stops beside it (<see cref="DepthScaleRows"/>)
        /// and marks the cell each verdict fell in (R-126). The thresholds stay the only ones in
        /// Th; a second list of bands would have diverged at the first recalibration.
        /// </summary>
        /// <returns>Four rows, PERFECT to REFA: the band, its interval in percent, its
        /// meaning.</returns>
        internal static IReadOnlyList<(string Band, string Range, string Meaning)> Scale()
            => new[]
            {
                ("PERFECT", T($"under {Th.ResidPerfect:F0} %", $"sub {Th.ResidPerfect:F0} %"),
                            T("perfect quality", "calitate perfecta")),
                ("BUN", $"{Th.ResidPerfect:F0} - {Th.ResidUsable:F0} %",
                        T("usable; background extraction removes what is left", "utilizabil; extragerea de fond scoate ce ramane")),
                ("ACCEPTABIL", $"{Th.ResidUsable:F0} - {Th.ResidCheck:F0} %",
                               T("usable, but confirm with the End-to-end test (--light) before a hard stretch",
                                 "utilizabil, dar confirma cu testul End-to-end (--light) inainte de un stretch puternic")),
                ("REFA", T($"over {Th.ResidCheck:F0} %", $"peste {Th.ResidCheck:F0} %"),
                         T("redo the flats", "refa flat-urile")),
            };

        /// <summary>
        /// Why the residual after the radial model is the one that matters. Taken out separately
        /// for the same reason as <see cref="Scale"/>: the window shows it too, at the foot of the
        /// five tests (R-126), where its "the most important one" points at their rows.
        /// </summary>
        /// <returns>Eight lines joined by '\n'.</returns>
        internal static string WhyResidualMatters()
            => T("The most important one is \"residual (radial model)\": it says directly how much of\n"
                 + "the frame does not look like a usual vignetting, symmetric about the centre. It is an\n"
                 + "UPPER BOUND of the flat's error, not the error itself: it holds both what belongs to the\n"
                 + "camera (the optics, divided out of the lights correctly) and what belongs to the world\n"
                 + "(the illumination, which does not repeat); one master cannot tell them apart. The corner\n"
                 + "spread, the azimuthal asymmetry and the tilt are pieces of the same deviation.\n"
                 + "\"Worst spot\" catches what the others miss: a small compact defect, a light leak,\n"
                 + "a condensation spot, which the residual sets aside as an extreme.",
                 "Cel mai important e \"reziduu dupa model rad.\": spune direct cat din cadru nu\n"
                 + "arata ca o vignetare obisnuita, simetrica in jurul centrului. E o LIMITA DE SUS a\n"
                 + "erorii flat-ului, nu eroarea insasi: cuprinde si ce tine de aparat (optica, care se\n"
                 + "imparte corect din lights) si ce tine de lume (iluminarea, care nu se repeta); un\n"
                 + "singur master nu le poate desparti. Imprastierea colturilor, asimetria azimutala si\n"
                 + "inclinarea sunt bucati ale aceleiasi abateri. \"Pata maxima\" prinde ce le scapa\n"
                 + "celorlalte: un defect mic si compact, o scurgere de lumina, o pata de condens, pe\n"
                 + "care reziduul il lasa deoparte ca pe o extrema.");

        /// <summary>Writes the residual scale in columns, and why the residual matters.</summary>
        /// <param name="w">Where to write.</param>
        static void WriteScale(TextWriter w)
        {
            w.WriteLine(T("\n  SCALE of the five deviation tests above", "\n  SCARA celor cinci teste de abatere de mai sus"));
            foreach (var (_, range, meaning) in Scale())
                w.WriteLine($"    {range,-13}{meaning}");
            Indent(WhyResidualMatters(), w);
        }

        /// <summary>
        /// What the person should do with this verdict. Returned as a string, not written
        /// directly, because the window shows it too, under the coloured verdict - and two
        /// wordings of the same advice would diverge exactly where it matters most not to diverge.
        /// </summary>
        /// <param name="verdict">A word of the residual scale; anything else gets REFA's
        /// advice.</param>
        /// <returns>The advice, lines joined by '\n'.</returns>
        internal static string Advice(string verdict) => verdict switch
        {
            "PERFECT" => T(
                "The flat looks like a clean vignetting; what departs from it is below the noise.",
                "Flat-ul arata ca o vignetare curata; ce abate de la ea e sub zgomot."),

            "BUN" => T(
                "Usable as it is. What is left is in the range that background\n"
              + "extraction removes without effort.",
                "Utilizabil asa cum e. Ce ramane e in intervalul pe care extragerea\n"
              + "de fond il scoate fara efort."),

            "ACCEPTABIL" => T(
                "Usable, but run the End-to-end test (--light) on the calibrated lights before\n"
              + "a hard stretch, to confirm that the flat really corrects.",
                "Utilizabil, dar ruleaza testul End-to-end (--light) pe lighturile calibrate\n"
              + "inainte de un stretch puternic, ca sa confirmi ca flat-ul chiar corecteaza."),

            // Says that the figure is an upper bound: a part fixed to the camera (a genuinely
            // non-radial optic, an off-centre axis) comes out here too, without being a flat
            // error. Measured: an axis off-centre by 5% of the width gives REFA on a flat that
            // corrects perfectly.
            _ => T(
                "Too large a departure from a usual vignetting: an uneven light source, a light\n"
              + "leak, the camera tilted toward the source, or light from around. It is NOT vignetting.\n"
              + "The figure is an upper bound: a lens whose axis is off-centre shows here too, and that\n"
              + "part is not a flat error. Otherwise redo the flats; the map shows the shape of the problem.",
                "Abatere prea mare fata de o vignetare obisnuita: sursa de lumina neuniforma, o\n"
              + "scurgere de lumina, aparatul inclinat fata de sursa sau lumina din jur. NU e vignetare.\n"
              + "Cifra e o limita de sus: un obiectiv cu axa descentrata apare si el aici, si partea\n"
              + "aceea nu e eroare de flat. Altfel refa flat-urile; harta arata forma problemei."),
        };

        /// <summary>Writes the advice of a verdict, indented.</summary>
        /// <param name="verdict">A word of the residual scale.</param>
        /// <param name="w">Where to write.</param>
        static void WriteAdvice(string verdict, TextWriter w) => Indent(Advice(verdict), w);

        /// <summary>Writes each line of a text indented by four spaces.</summary>
        /// <param name="text">The lines, joined by '\n'.</param>
        /// <param name="w">Where to write.</param>
        static void Indent(string text, TextWriter w)
        {
            foreach (string line in text.Split('\n')) w.WriteLine("    " + line);
        }
    }
}
