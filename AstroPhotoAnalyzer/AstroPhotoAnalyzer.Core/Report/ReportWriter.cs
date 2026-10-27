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

// ReportWriter.cs - the report as text: the decisions first, then the appendices with the tables.
//
// The same text for the console and for the window's Raport.txt - that is why it writes into a
// TextWriter and does not know who reads it. The format is pinned by the golden tests: a
// rewording here shows there, and that is the intention.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    /// <summary>
    /// Writes the report, in the interface language, and the lines of it the window's tabs show
    /// as they are: the simulation's table, the size table, the crop's cut.
    /// </summary>
    internal static class ReportWriter
    {
        /// <summary>
        /// Banner + body: what the window saves and what the console writes with -o.
        /// </summary>
        /// <param name="r">The analysis, complete.</param>
        /// <param name="w">The writer the report goes to.</param>
        internal static void WriteFull(AnalysisResult r, TextWriter w)
        {
            RunReport.WriteBanner(w);
            WriteBody(r, w);
        }

        /// <summary>
        /// <see cref="WriteFull"/> into a file, whole or not at all (<see cref="AtomicFile"/>:
        /// a temporary file beside the target, moved over it once every line is written, so that
        /// a write that fails midway leaves the old file or none, never a truncated report;
        /// R-005, R-111). The console's -o and the window's "Salveaza raportul" both come here;
        /// the exception is theirs to tell. The bytes are UTF-8 without a byte-order mark, as
        /// a StreamWriter on a path writes them.
        /// </summary>
        /// <param name="r">The analysis, complete.</param>
        /// <param name="path">
        /// The file to write, replaced if it exists; its folder must exist and allow a file to
        /// be created. A file another program holds open, a folder that refuses or a path the
        /// system refuses throws (IOException, UnauthorizedAccessException, or an
        /// ArgumentException on a malformed path), with the target's name in the message.
        /// </param>
        internal static void SaveFull(AnalysisResult r, string path)
            => AtomicFile.Write(path, s =>
            {
                using var w = new StreamWriter(s);
                WriteFull(r, w);
            });

        /// <summary>
        /// The report without the banner: the file's header and the verdict, the six sections of
        /// recommendations, then the appendices A to F. It is what
        /// <see cref="AnalysisResult.ReportBody"/> holds and what the console prints after its own
        /// banner.
        /// </summary>
        /// <param name="r">The analysis, its recommendations included.</param>
        /// <param name="w">The writer the report goes to.</param>
        internal static void WriteBody(AnalysisResult r, TextWriter w)
        {
            Header(r, w);
            Decisions(r, w);
            AppendixTones(r, w);
            AppendixSimulation(r, w);
            AppendixExport(r, w);
            AppendixResolution(r, w);
            AppendixBackground(r, w);
            AppendixParameters(r, w);
        }

        /// <summary>
        /// The report's head: the path as it was given, the file's facts, the medium with its
        /// Dmax and paper black, the viewing distance, and the overall verdict.
        /// </summary>
        /// <param name="r">The analysis.</param>
        /// <param name="w">The writer.</param>
        static void Header(AnalysisResult r, TextWriter w)
        {
            FileFactsResult f = r.File;
            w.WriteLine();
            w.WriteLine(Tr.Pick("File: ", "Fisier: ") + r.Path);
            string bits = f.IsFloat ? "float" : f.BitDepth + "-bit";
            string icc = f.HasIcc
                ? Tr.Pick("ICC profile: ", "profil ICC: ") + (f.IccDescription ?? Tr.Pick("(no name)", "(fara nume)"))
                : Tr.Pick("no ICC profile", "fara profil ICC");
            w.WriteLine($"  {f.Format} {bits} {(f.IsColor ? "RGB" : "mono")}, {Fmt.Dim(f.W, f.H)} ({Fmt.F1(f.Megapixels)} MP), {f.Compression ?? "-"}, {Fmt.Mb(f.FileMegabytes)}, {icc}");
            Parameters p = r.Parameters;
            string dist = p.ViewingDistanceM.HasValue ? Fmt.F1(p.ViewingDistanceM.Value) + " m" : Tr.Pick("1.5 x the diagonal", "1.5 x diagonala");
            w.WriteLine(Tr.Pick($"  Medium: {Parameters.NameOf(p.Medium)} (Dmax {Fmt.Dmax(p.Dmax)}, paper black L* {Fmt.F1(r.Tonal.PaperBlackLstar)}). Viewing distance: {dist}.",
                                $"  Mediu: {Parameters.NameOf(p.Medium)} (Dmax {Fmt.Dmax(p.Dmax)}, negrul hartiei L* {Fmt.F1(r.Tonal.PaperBlackLstar)}). Distanta de privire: {dist}."));
            w.WriteLine();
            w.WriteLine("VERDICT: " + VerdictText.Label(r.Overall));
        }

        /// <summary>
        /// The six sections in decision order, each numbered with its title and its verdict (the
        /// maximum of its recommendations), then each recommendation with its mark and its
        /// "because" ("fiindca"); "(nothing to report)" ("(nimic de semnalat)") for a section
        /// without one.
        /// </summary>
        /// <param name="r">The analysis, its recommendations included.</param>
        /// <param name="w">The writer.</param>
        static void Decisions(AnalysisResult r, TextWriter w)
        {
            foreach (Topic t in Enum.GetValues(typeof(Topic)))
            {
                var recs = r.Recommendations.Where(x => x.Topic == t).ToList();
                Verdict v = recs.Count == 0 ? Verdict.Ok : recs.Max(x => x.Verdict);
                w.WriteLine();
                w.WriteLine($"{(int)t}. {VerdictText.Title(t)}  [{VerdictText.Label(v)}]");
                if (recs.Count == 0) { w.WriteLine(Tr.Pick("   (nothing to report)", "   (nimic de semnalat)")); continue; }
                foreach (Recommendation rec in recs)
                {
                    w.WriteLine($"   {Mark(rec.Verdict)} {rec.Text}");
                    w.WriteLine(Tr.Pick("     because: ", "     fiindca: ") + Fmt.Plain(rec.Because));
                }
            }
        }

        /// <summary>The mark before a recommendation's text.</summary>
        /// <param name="v">The recommendation's verdict.</param>
        /// <returns>"-" for Ok, "!" for Attention, "!!" for Problem.</returns>
        static string Mark(Verdict v) => v == Verdict.Ok ? "-" : v == Verdict.Attention ? "!" : "!!";

        /// <summary>
        /// Appendix A: the channel means and the reference percentiles (0..255), the shares under
        /// the two dark levels, the median L* and the shares under paper black, under the
        /// visibility threshold and over the highlight threshold.
        /// </summary>
        /// <param name="r">The analysis.</param>
        /// <param name="w">The writer.</param>
        static void AppendixTones(AnalysisResult r, TextWriter w)
        {
            TonalResult t = r.Tonal;
            w.WriteLine();
            w.WriteLine(Tr.Pick("Appendix A. Tones", "Anexa A. Tonuri"));
            w.WriteLine(Tr.Pick("  channel means (0..255): ", "  medii pe canal (0..255): ") + string.Join(" / ", t.MeanLevels.Select(Fmt.F1)));
            w.WriteLine(Tr.Pick($"  under level {Thresholds.DarkLevelA}: {Fmt.Pct(t.PctBelowDarkA)}   under {Thresholds.DarkLevelB}: {Fmt.Pct(t.PctBelowDarkB)}",
                                $"  sub nivelul {Thresholds.DarkLevelA}: {Fmt.Pct(t.PctBelowDarkA)}   sub {Thresholds.DarkLevelB}: {Fmt.Pct(t.PctBelowDarkB)}"));
            w.WriteLine(Tr.Pick("  reference channel percentiles (0..255): ", "  percentile canal referinta (0..255): ")
                + string.Join("  ", TonalResult.PercentileRanks.Zip(t.ReferencePercentiles, (p, v) => $"p{Fmt.F1(p)}={Fmt.F1(v)}")));
            w.WriteLine(Tr.Pick($"  median L* {Fmt.F1(t.LstarMedian)}; under paper black (L* {Fmt.F1(t.PaperBlackLstar)}): {Fmt.Pct(t.PctBelowPaperBlack)}; under L* {Fmt.F0(Thresholds.LowSeparationLstar)}: {Fmt.Pct(t.PctBelowLowSeparation)}; over L* {Fmt.F0(Thresholds.HighlightLstar)}: {Fmt.Pct(t.PctAboveHighlight)}",
                                $"  L* median {Fmt.F1(t.LstarMedian)}; sub negrul hartiei (L* {Fmt.F1(t.PaperBlackLstar)}): {Fmt.Pct(t.PctBelowPaperBlack)}; sub L* {Fmt.F0(Thresholds.LowSeparationLstar)}: {Fmt.Pct(t.PctBelowLowSeparation)}; peste L* {Fmt.F0(Thresholds.HighlightLstar)}: {Fmt.Pct(t.PctAboveHighlight)}"));
        }

        /// <summary>
        /// Where the Dmax comes from, in Appendix F: nothing when it is the medium's, the profile
        /// when it was read from one, otherwise "(explicit)".
        /// </summary>
        /// <param name="p">The parameters the analysis ran with.</param>
        /// <returns>
        /// " (from the ICC profile "name")" (Romanian " (din profilul ICC "name")"), " (explicit)"
        /// for a given Dmax that differs from the medium's, or "" (each with its leading space, to
        /// follow the Dmax).
        /// </returns>
        internal static string DmaxOrigin(Parameters p)
            => p.DmaxProfile != null && p.DmaxOverride.HasValue ? Tr.Pick($" (from the ICC profile \"{p.DmaxProfile}\")", $" (din profilul ICC \"{p.DmaxProfile}\")")
             : p.DmaxIsExplicit ? " (explicit)"
             : "";

        /// <summary>
        /// Appendix B: the simulation's table from the measured level, its footer, the definition
        /// of the dust and the sky, the class shares, the exact-target line, and the darkest
        /// <see cref="Thresholds.DustCloudReportRows"/> cloud rows with a count of the rest.
        /// </summary>
        /// <param name="r">The analysis.</param>
        /// <param name="w">The writer.</param>
        static void AppendixSimulation(AnalysisResult r, TextWriter w)
        {
            SimulationResult s = r.Simulation;
            w.WriteLine();
            w.WriteLine(Tr.Pick($"Appendix B. Background simulation (from level {Fmt.Level(s.BackgroundMeasured)}; a gamma curve on every channel)",
                                $"Anexa B. Simularea fundalului (de la nivelul {Fmt.Level(s.BackgroundMeasured)}; curba gamma pe toate canalele)"));
            w.WriteLine("  " + SimulationHeader);
            foreach (SimulationRow row in s.Rows)
                w.WriteLine("  " + SimulationLine(row));
            w.WriteLine("  " + SimulationFooter(s));
            w.WriteLine("  " + DustDefinition(s));
            w.WriteLine(Tr.Pick("  classes: ", "  clase: ") + s.ClassFractions);
            w.WriteLine("  " + ExactLine(s));
            if (s.Clouds.Count > 0)
            {
                w.WriteLine(Tr.Pick($"  the catalogued clouds with at least {Thresholds.DustCloudMinPatches} patches ({Fmt.Int(s.Clouds.Count)}), darkest first; L* = {Fmt.F0(Thresholds.DustCloudFraction * 100)} % of the cloud's patches; target = where it reaches paper black:",
                                    $"  norii catalogati cu cel putin {Thresholds.DustCloudMinPatches} dale ({Fmt.Int(s.Clouds.Count)}), de la cel mai intunecat; L* = {Fmt.F0(Thresholds.DustCloudFraction * 100)} % din dalele norului; tinta = unde ajunge pe negrul hartiei:"));
                w.WriteLine("  " + CloudHeader);
                foreach (CloudRow c in s.Clouds.Take(Thresholds.DustCloudReportRows))
                    w.WriteLine("  " + CloudLine(c));
                if (s.Clouds.Count > Thresholds.DustCloudReportRows)
                    w.WriteLine(Tr.Pick($"  ... and {Fmt.Int(s.Clouds.Count - Thresholds.DustCloudReportRows)} more (the window shows them all)",
                                        $"  ... si inca {Fmt.Int(s.Clouds.Count - Thresholds.DustCloudReportRows)} (fereastra ii arata pe toti)"));
            }
        }

        // The simulation's table, the same text in the report and in the Tonuri tab: the header,
        // the rows, the note under it, the definition of the dust and the sky, the exact target and
        // the clouds. The headers are properties, picked when they are shown (ADR 0048); each
        // English column title ends where the Romanian one does, over the right-aligned figures.

        internal static string SimulationHeader => Tr.Pick(
            "target background    gamma   L* dust   L* sky  pure black    L* < 15      L* > 50",
            "fundal-tinta         gamma   L* praf   L* cer   negru pur   sub L*15   peste L*50");

        /// <summary>
        /// A row of the simulation's table, under <see cref="SimulationHeader"/>, without the
        /// indent.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <returns>
        /// The target (with "as is" or "(given)", Romanian "asa cum e" or "(dat)", and " *" on the
        /// recommended row), the gamma,
        /// the dust's and the sky's L*, the three shares, and the row's mark
        /// (<see cref="RowMark"/>).
        /// </returns>
        internal static string SimulationLine(SimulationRow row)
        {
            string target = Fmt.Level(row.Target) + (row.AsIs ? Tr.Pick(" as is", " asa cum e") : row.Explicit ? Tr.Pick(" (given)", " (dat)") : "") + (row.Recommended ? " *" : "");
            return $"{target,-18}   {Fmt.F3(row.Gamma),5}   {Fmt.F1(row.LstarDust),7}   {Fmt.F1(row.LstarSky),6}   {Fmt.Pct(row.PctPatchesBelowPaperBlack),9}   {Fmt.Pct(row.PctBelowLowSeparation),8}   {Fmt.Pct(row.PctAboveHighlight),10}{RowMark(row)}";
        }

        /// <summary>
        /// A row's mark: "print-safe" when the dust is on paper black and the sky at the
        /// threshold or above, "sky under L* 15" ("cer sub L* 15") when only the dust passes,
        /// nothing when the dust is under black (R-043).
        /// </summary>
        /// <param name="row">The row.</param>
        /// <returns>The mark with its three leading spaces, or "".</returns>
        internal static string RowMark(SimulationRow row)
            => row.PrintSafe ? "   print-safe"
             : row.DustOnPaperBlack ? Tr.Pick($"   sky under L* {Fmt.F0(Thresholds.LowSeparationLstar)}", $"   cer sub L* {Fmt.F0(Thresholds.LowSeparationLstar)}")
             : "";

        /// <summary>The note under the simulation's table.</summary>
        /// <param name="s">The simulation.</param>
        /// <returns>
        /// What "*" means when a row is recommended; otherwise why none is: the sky reaches the
        /// visibility threshold on no row (with its target), or the dust reaches paper black on
        /// no row (with its exact target, or "not even 0.60" when the ladder has none).
        /// </returns>
        internal static string SimulationFooter(SimulationResult s)
            => s.Recommended != null
                ? Tr.Pick($"* = recommended: the lowest print-safe target - the dust on paper black and the sky at L* {Fmt.F0(Thresholds.LowSeparationLstar)} or above",
                          $"* = recomandat: cea mai mica tinta print-safe - praful pe negrul hartiei si cerul la L* {Fmt.F0(Thresholds.LowSeparationLstar)} sau peste")
                : s.Rows.Any(x => x.DustOnPaperBlack)
                    ? Tr.Pick($"no target of the grid brings the sky to L* {Fmt.F0(Thresholds.LowSeparationLstar)} (it would take {TargetText(s.VisibilityTarget)})",
                              $"nicio tinta din grila nu aduce cerul la L* {Fmt.F0(Thresholds.LowSeparationLstar)} (ar trebui {TargetText(s.VisibilityTarget)})")
                : s.ExactTarget.HasValue
                    ? Tr.Pick($"no target of the grid brings the dust onto paper black (it would take ~{Fmt.Level(s.ExactTarget.Value)})",
                              $"nicio tinta din grila nu aduce praful pe negrul hartiei (ar trebui ~{Fmt.Level(s.ExactTarget.Value)})")
                    : Tr.Pick($"no target of the grid brings the dust onto paper black, not even {Fmt.Level(Thresholds.ExactTargetHi)}",
                              $"nicio tinta din grila nu aduce praful pe negrul hartiei, nici {Fmt.Level(Thresholds.ExactTargetHi)}");

        /// <summary>
        /// "dust = the 25 % percentile of the dust patches (0.5 L* below the modelled sky (4 sigma
        /// of the sky); 5.5 % of the frame) (no WCS); sky = the median of the sky patches (classes
        /// relative to the modelled sky); pure black = 32 px patches under paper black (L* 9.0 at
        /// Dmax 2.0)"; in Romanian "praful = percentila 25 % a dalelor de praf ...".
        /// </summary>
        /// <param name="s">The simulation.</param>
        /// <returns>
        /// What the dust is (with why the band classes were not made, on the sky path), what the
        /// sky is, and what "pure black" ("negru pur") counts, with the paper black and the Dmax.
        /// </returns>
        internal static string DustDefinition(SimulationResult s)
            => Tr.Pick($"dust = {s.DustDescription}{(s.MethodNote == null ? "" : " (" + s.MethodNote + ")")}; sky = {s.SkyDescription}; "
                       + $"pure black = {Thresholds.DustPatchPx} px patches under paper black (L* {Fmt.F1(s.PaperBlackLstar)} at Dmax {Fmt.Dmax(s.Dmax)})",
                       $"praful = {s.DustDescription}{(s.MethodNote == null ? "" : " (" + s.MethodNote + ")")}; cerul = {s.SkyDescription}; "
                       + $"negru pur = dale de {Thresholds.DustPatchPx} px sub negrul hartiei (L* {Fmt.F1(s.PaperBlackLstar)} la Dmax {Fmt.Dmax(s.Dmax)})");

        /// <summary>
        /// An exact target from the ladder (R-043): its value, or a bound at the ladder's ends -
        /// "&lt;= 0.05" when even the first step already puts the dust (or the cloud) on black, so
        /// the true target is somewhere lower, where the ladder does not reach; "> 0.60" when not
        /// even the last puts it there. Written as a plain "0.05", the first case looked like a
        /// measured target.
        /// </summary>
        /// <param name="t">
        /// The target, 0..1, read on the ladder (the dust's, a cloud's, or the sky's at the
        /// visibility threshold); null when no step of the ladder reaches it, or with a background
        /// at 0 or 1.
        /// </param>
        /// <returns>The level (<see cref="Fmt.Level"/>), "&lt;= 0.05" or "> 0.60".</returns>
        internal static string TargetText(double? t)
            => !t.HasValue ? "> " + Fmt.Level(Thresholds.ExactTargetHi)
             : t.Value <= Thresholds.ExactTargetLo + 1e-9 ? "<= " + Fmt.Level(Thresholds.ExactTargetLo)
             : Fmt.Level(t.Value);

        /// <summary>
        /// The Dmax from which the dust as it is reaches paper black, as a sentence (R-043):
        /// "the dust reaches paper black from Dmax 2.28", or "the dust reaches paper black at no
        /// Dmax (it is at L* 0)" (Romanian "praful ajunge pe negrul hartiei de la Dmax 2.28"). When the sky as it is is under the visibility
        /// threshold it adds that, as it is, no paper makes the image print-safe, since the sky is
        /// under L* 15, and that the remedy is lifting the background at the stretch: the figure
        /// is the dust's alone, and until the review of 2026-10-23 it said "trece asa cum e" even
        /// beside a sky at L* 7 (ADR 0043). The same sentence in the "fiindca" (Recommendations)
        /// and in Appendix B.
        /// </summary>
        /// <param name="s">The simulation.</param>
        /// <returns>The sentence, without a final full stop.</returns>
        internal static string DustDmaxText(SimulationResult s)
        {
            string dust = double.IsNaN(s.DmaxDustOnPaperBlackAsIs)
                ? Tr.Pick("the dust reaches paper black at no Dmax (it is at L* 0)", "praful nu ajunge pe negrul hartiei la niciun Dmax (e la L* 0)")
                : Tr.Pick($"the dust reaches paper black from Dmax {Fmt.Dmax(s.DmaxDustOnPaperBlackAsIs)}",
                          $"praful ajunge pe negrul hartiei de la Dmax {Fmt.Dmax(s.DmaxDustOnPaperBlackAsIs)}");
            return s.SkyBelowVisibility
                ? dust + Tr.Pick($"; as it is, no paper makes the image print-safe: the sky is under L* {Fmt.F0(Thresholds.LowSeparationLstar)}, and the remedy is lifting the background at the stretch",
                                 $"; asa cum e, nicio hartie nu face imaginea print-safe: cerul e sub L* {Fmt.F0(Thresholds.LowSeparationLstar)}, iar remediul e ridicarea fundalului la stretch")
                : dust;
        }

        /// <summary>
        /// The exact target, the two at the neighbouring Dmax, and the Dmax from which the dust
        /// reaches black (<see cref="DustDmaxText"/>).
        /// </summary>
        /// <param name="s">The simulation.</param>
        /// <returns>
        /// The line, which ends with the target at which the sky reaches the visibility threshold;
        /// every target through <see cref="TargetText"/>.
        /// </returns>
        internal static string ExactLine(SimulationResult s)
        {
            double lo = s.Dmax - Thresholds.DmaxSensitivityStep, hi = s.Dmax + Thresholds.DmaxSensitivityStep;
            return Tr.Pick($"the exact target of the dust on black: {TargetText(s.ExactTarget)} at Dmax {Fmt.Dmax(s.Dmax)} ({TargetText(s.ExactTargetDmaxLower)} at Dmax {Fmt.Dmax(lo)}, {TargetText(s.ExactTargetDmaxHigher)} at Dmax {Fmt.Dmax(hi)}); {DustDmaxText(s)}; "
                           + $"the sky reaches the visibility threshold L* {Fmt.F0(Thresholds.LowSeparationLstar)} at {TargetText(s.VisibilityTarget)}",
                           $"tinta exacta a prafului pe negru: {TargetText(s.ExactTarget)} la Dmax {Fmt.Dmax(s.Dmax)} ({TargetText(s.ExactTargetDmaxLower)} la Dmax {Fmt.Dmax(lo)}, {TargetText(s.ExactTargetDmaxHigher)} la Dmax {Fmt.Dmax(hi)}); {DustDmaxText(s)}; "
                           + $"cerul ajunge la pragul de vizibilitate L* {Fmt.F0(Thresholds.LowSeparationLstar)} la {TargetText(s.VisibilityTarget)}");
        }

        /// <summary>
        /// What the crop cuts, as text (R-032): "192 px on each side of the width" when the two
        /// sides are equal, otherwise "0 px on the left and 1 px on the right of the width" or
        /// "... at the top and ... at the bottom of the height" (Romanian "192 px pe fiecare parte
        /// din latime"). The same text in the recommendation and in the Preview tile.
        /// </summary>
        /// <param name="c">The crop, one that is needed (its axis is not null).</param>
        /// <returns>The pixels cut on each side and the axis, without a final full stop.</returns>
        internal static string CropCutText(CropResult c)
        {
            if (c.CutStartPx == c.CutEndPx)
                return Tr.Pick($"{Fmt.Int(c.CutStartPx)} px on each side of {c.Axis}", $"{Fmt.Int(c.CutStartPx)} px pe fiecare parte din {c.Axis}");
            bool width = c.CutsWidth;
            return Tr.Pick($"{Fmt.Int(c.CutStartPx)} px {(width ? "on the left" : "at the top")} and {Fmt.Int(c.CutEndPx)} px {(width ? "on the right" : "at the bottom")} of {c.Axis}",
                           $"{Fmt.Int(c.CutStartPx)} px {(width ? "la stanga" : "sus")} si {Fmt.Int(c.CutEndPx)} px {(width ? "la dreapta" : "jos")} din {c.Axis}");
        }

        internal static string CloudHeader => Tr.Pick(
            "cloud                     patches   L*     target",
            "nor                          dale   L*     tinta");

        /// <summary>
        /// A row of the cloud table, under <see cref="CloudHeader"/>, without the indent.
        /// </summary>
        /// <param name="c">The cloud's row.</param>
        /// <returns>
        /// The name, the patch count, the L* and the exact target (<see cref="TargetText"/>).
        /// </returns>
        internal static string CloudLine(CloudRow c)
            => $"{c.Name,-28} {Fmt.Int(c.Patches),4}   {Fmt.F1(c.Lstar),4}   {TargetText(c.ExactTarget)}";

        /// <summary>
        /// Appendix C: the 8-bit LZW TIFF's size, the JPEG table, and when
        /// <see cref="QuantizationResult.Applicable"/> the 16 -> 8 rounding and the shadow-lift
        /// stress; then the plateau, or that no clean sky tile was found.
        /// </summary>
        /// <param name="r">The analysis.</param>
        /// <param name="w">The writer.</param>
        static void AppendixExport(AnalysisResult r, TextWriter w)
        {
            QuantizationResult q = r.Quantization;
            w.WriteLine();
            w.WriteLine(Tr.Pick("Appendix C. Export", "Anexa C. Export"));
            w.WriteLine($"  TIFF 8-bit LZW: {Fmt.Mb(r.Jpeg.Tiff8LzwMegabytes)}");
            w.WriteLine(Tr.Pick("  JPEG 4:4:4     size      mean err   max err   p99    p99.9   > 2 lev    mean level",
                                "  JPEG 4:4:4   marime     err medie   err max   p99    p99.9   > 2 niv   nivel mediu"));
            foreach (JpegRow j in r.Jpeg.Rows)
                w.WriteLine($"  q{j.Quality,-3}         {Fmt.Mb(j.Megabytes),8}   {Fmt.F3(j.MeanAbsError),9}   {Fmt.F1(j.MaxError),7}   {Fmt.F2(j.P99),4}   {Fmt.F2(j.P999),5}   {Fmt.Pct(j.PctOverThreshold),7}   {Fmt.F0(j.MeanLevelOverThreshold),11}");
            if (q.Applicable)
            {
                w.WriteLine(Tr.Pick($"  16 -> 8 rounding: mean {Fmt.F3(q.RoundingMeanAbs)}, max {Fmt.F3(q.RoundingMax)}, p99.9 {Fmt.F3(q.RoundingP999)} levels",
                                    $"  rotunjire 16 -> 8: medie {Fmt.F3(q.RoundingMeanAbs)}, maxim {Fmt.F3(q.RoundingMax)}, p99.9 {Fmt.F3(q.RoundingP999)} niveluri"));
                w.WriteLine(Tr.Pick($"  shadow-lift stress (gamma {Fmt.F1(Thresholds.ShadowLiftGamma)}), levels {Thresholds.ShadowLiftLevelLo}..{Thresholds.ShadowLiftLevelHi}: from 16 bits {q.LevelsUsedFrom16} used / {q.MissingFrom16} missing; from 8 bits {q.LevelsUsedFrom8} used / {q.MissingFrom8} missing",
                                    $"  stres shadow-lift (gamma {Fmt.F1(Thresholds.ShadowLiftGamma)}), niveluri {Thresholds.ShadowLiftLevelLo}..{Thresholds.ShadowLiftLevelHi}: din 16 biti {q.LevelsUsedFrom16} folosite / {q.MissingFrom16} lipsa; din 8 biti {q.LevelsUsedFrom8} folosite / {q.MissingFrom8} lipsa"));
            }
            SizeRow reference = r.Size.ReferenceRow;
            string steps = !q.PlateauKnown || reference == null ? "" : Tr.Pick(
                $"; flat steps over {Fmt.F1(Thresholds.PlateauVisibleMm)} mm at {Fmt.F0(reference.Dpi)} dpi on {Fmt.F2(q.PlateauVisibleFramePctAt(reference.Dpi))} % of the frame",
                $"; trepte plate de peste {Fmt.F1(Thresholds.PlateauVisibleMm)} mm la {Fmt.F0(reference.Dpi)} dpi pe {Fmt.F2(q.PlateauVisibleFramePctAt(reference.Dpi))} % din cadru");
            w.WriteLine(q.PlateauKnown
                ? Tr.Pick($"  plateau on the quantized sky: {Fmt.F1(q.PlateauPx)} px over {q.PlateauTiles} tiles of {q.PlateauPositions} positions ({Fmt.Pct(q.PlateauFramePct)} of the frame, noise left sigma {Fmt.F2(q.PlateauSigmaLsb)} LSB){steps}", $"  palier pe cerul cuantizat: {Fmt.F1(q.PlateauPx)} px pe {q.PlateauTiles} dale din {q.PlateauPositions} pozitii ({Fmt.Pct(q.PlateauFramePct)} din cadru, zgomot ramas sigma {Fmt.F2(q.PlateauSigmaLsb)} LSB){steps}")
                : Tr.Pick($"  plateau: no clean sky tiles ({q.PlateauTiles} of {q.PlateauPositions} positions)", $"  palier: fara dale de cer curat ({q.PlateauTiles} din {q.PlateauPositions} pozitii)"));
        }

        /// <summary>
        /// Appendix D: the stars and their FWHM (or that too few were measured), the regions with
        /// the corner / centre ratio, and the size table with its footer.
        /// </summary>
        /// <param name="r">The analysis.</param>
        /// <param name="w">The writer.</param>
        static void AppendixResolution(AnalysisResult r, TextWriter w)
        {
            StarResult s = r.Stars;
            PrintSizeResult ps = r.Size;
            w.WriteLine();
            w.WriteLine(Tr.Pick("Appendix D. Resolution and size", "Anexa D. Rezolutie si marime"));
            w.WriteLine(s.Insufficient
                ? Tr.Pick($"  stars: {s.Count} measured - too few for a FWHM", $"  stele: {s.Count} masurate - prea putine pentru FWHM")
                : Tr.Pick($"  stars: {s.Count} measured; median FWHM {Fmt.F2(s.FwhmMedian)} px, Q1 {Fmt.F2(s.FwhmQ1)}, Q3 {Fmt.F2(s.FwhmQ3)}; real detail ~{Fmt.F0(ps.EffectiveDetailPx)} px across the width",
                          $"  stele: {s.Count} masurate; FWHM median {Fmt.F2(s.FwhmMedian)} px, Q1 {Fmt.F2(s.FwhmQ1)}, Q3 {Fmt.F2(s.FwhmQ3)}; detaliu real ~{Fmt.F0(ps.EffectiveDetailPx)} px pe latime"));
            w.WriteLine(Tr.Pick("  regions: ", "  regiuni: ") + string.Join("; ", s.Regions.Select(x => $"{x.Name} {(x.Known ? Fmt.F2(x.FwhmMedian) + " px" : "n/a")} ({x.Count})"))
                + (double.IsNaN(s.CornerToCenterRatio) ? "" : Tr.Pick("; corner/centre ", "; colt/centru ") + Fmt.F2(s.CornerToCenterRatio)));
            w.WriteLine(Tr.Pick($"  sizes for {Fmt.Dim(ps.PixelW, ps.PixelH)} px{(r.Crop.Needed ? " (after the crop)" : "")}:",
                                $"  marimi pentru {Fmt.Dim(ps.PixelW, ps.PixelH)} px{(r.Crop.Needed ? " (dupa decupaj)" : "")}:"));
            w.WriteLine(Tr.Pick("   width cm     height   dpi   star mm    point from       dist. used   outer frame cm",
                                "  latime cm   inaltime   dpi   stea mm   punct de la   dist. folosita   rama ext. cm"));
            foreach (SizeRow row in ps.Rows)
                w.WriteLine("  " + SizeTableRow(row));
            foreach (string line in SizeTableFooter(ps))
                w.WriteLine("  " + line);
        }

        /// <summary>
        /// A row of the size table, without the indent - the same in the report and in the
        /// Rezolutie tab.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <returns>
        /// The width (" *" on the recommended row), the height, the dpi, the star in mm, the
        /// distance from which it is a point, the distance used and the outer size, with
        /// "(over the limit)" ("(peste limita)") over <see cref="Thresholds.OversizeCm"/>.
        /// </returns>
        internal static string SizeTableRow(SizeRow row) =>
            $"{Fmt.F0(row.WidthCm),6}{(row.Recommended ? " *" : "  ")}    {Fmt.F1(row.HeightCm),8}   {Fmt.F0(row.Dpi),3}   {Fmt.F2(row.StarMm),7}   {Fmt.F2(row.MinDistanceM) + " m",11}   {Fmt.F2(row.ViewingDistanceM) + " m",14}   {Fmt.F0(row.OuterWidthCm)} x {Fmt.F0(row.OuterHeightCm)}{(row.Oversize ? Tr.Pick("  (over the limit)", "  (peste limita)") : "")}";

        /// <summary>
        /// The table's legend: the asterisk when there is a recommended row; otherwise why there
        /// is none - without stars only the pixel grid is judged, at the natural distance the
        /// point / distance ratio is the same on every row.
        /// </summary>
        /// <param name="ps">The size table's result.</param>
        /// <returns>One line, or two for the natural distance, without the indent.</returns>
        internal static IEnumerable<string> SizeTableFooter(PrintSizeResult ps)
        {
            if (ps.CanRecommend)
            {
                yield return Tr.Pick("* = recommended", "* = recomandat");
                yield break;
            }
            if (!ps.FwhmKnown)
            {
                yield return Tr.Pick($"no recommended row: without stars the detail is not measured, only the pixel grid (threshold {Fmt.F0(ps.FallbackDpi)} dpi) - a limit of the file, not of the image",
                                     $"fara rand recomandat: fara stele nu se masoara detaliul, ci doar grila de pixeli (prag {Fmt.F0(ps.FallbackDpi)} dpi) - o limita a fisierului, nu a imaginii");
                yield break;
            }
            yield return Tr.Pick($"no recommended row: \"point from\" and \"dist. used\" both grow with the width, so their ratio ({Fmt.F2(ps.NaturalRatio)}) is the same on every row",
                                 $"fara rand recomandat: \"punct de la\" si \"dist. folosita\" cresc amandoua cu latimea, deci raportul lor ({Fmt.F2(ps.NaturalRatio)}) e acelasi pe fiecare rand");
            yield return Tr.Pick("  and the test does not tell the sizes apart - the wall chooses; give a viewing distance to tell them apart",
                                 "  si testul nu desparte marimile - o alege peretele; da o distanta de privire ca sa le desparta");
        }

        /// <summary>
        /// Appendix E: the background's colour (R / G / B, or its one channel, 0..255) with the
        /// two cast metrics on colour, then each edge region with its levels, its deltas and "!"
        /// when it is flagged.
        /// </summary>
        /// <param name="r">The analysis.</param>
        /// <param name="w">The writer.</param>
        static void AppendixBackground(AnalysisResult r, TextWriter w)
        {
            BackgroundResult b = r.Background;
            w.WriteLine();
            w.WriteLine(Tr.Pick("Appendix E. Background colour", "Anexa E. Culoarea fundalului"));
            string levels = b.ColorApplicable
                ? Tr.Pick("background colour, R / G / B (0..255)", "culoarea fundalului, R / G / B (0..255)")
                : Tr.Pick("the background, one channel (0..255)", "fundalul, un singur canal (0..255)");
            w.WriteLine($"  {levels}: {string.Join(" / ", b.Levels.Select(Fmt.F1))}"
                + (b.ColorApplicable ? $"; G - (R+B)/2 = {Fmt.S1(b.GreenExcess)}; R - B = {Fmt.S1(b.RedMinusBlue)}; L* {Fmt.F1(b.Lab.L)}, a* {Fmt.S1(b.Lab.A)}, b* {Fmt.S1(b.Lab.B)}, {Tr.Pick("chroma", "croma")} {Fmt.F1(b.Chroma)}" : "")
                + $"; {Tr.Pick("from", "de pe")} {b.SourceText}");
            foreach (EdgeRegion e in b.Regions)
                w.WriteLine($"  {e.Name,-18} {string.Join(" / ", e.Levels.Select(Fmt.F1)),-22} delta {string.Join(" / ", e.Deltas.Select(Fmt.S1))}{e.PixelsNote}{(e.Flagged ? "  !" : "")}");
        }

        /// <summary>
        /// Appendix F: every parameter as the analysis used it - the medium and the Dmax with its
        /// origin, the widths, the passe-partout and the frame, the profile's note, the distance,
        /// the target aspect, the target background with the grid's range, the WCS and the seed.
        /// </summary>
        /// <param name="r">The analysis.</param>
        /// <param name="w">The writer.</param>
        static void AppendixParameters(AnalysisResult r, TextWriter w)
        {
            Parameters p = r.Parameters;
            w.WriteLine();
            w.WriteLine(Tr.Pick("Appendix F. Parameters", "Anexa F. Parametri"));
            string widths = string.Join(", ", p.CandidateWidthsCm.Select(Fmt.F0));
            w.WriteLine(Tr.Pick($"  medium {Parameters.NameOf(p.Medium)}, Dmax {Fmt.Dmax(p.Dmax)}{DmaxOrigin(p)}; widths {widths} cm; passepartout {Fmt.F0(p.PassepartoutCm)} cm, frame {Fmt.F0(p.FrameCm)} cm",
                                $"  mediu {Parameters.NameOf(p.Medium)}, Dmax {Fmt.Dmax(p.Dmax)}{DmaxOrigin(p)}; latimi {widths} cm; passepartout {Fmt.F0(p.PassepartoutCm)} cm, rama {Fmt.F0(p.FrameCm)} cm"));
            if (p.DmaxProfile != null && p.DmaxOverride.HasValue && p.DmaxProfileNote != null)
                w.WriteLine(Tr.Pick("  the profile's note: ", "  nota profilului: ") + p.DmaxProfileNote);
            string distance = p.ViewingDistanceM.HasValue ? Fmt.F1(p.ViewingDistanceM.Value) + " m" : Tr.Pick("natural", "naturala");
            string aspect = p.TargetAspect.HasValue ? Fmt.F3(p.TargetAspect.Value) : Tr.Pick("native", "nativ");
            string target = p.TargetBackground.HasValue ? Fmt.Level(p.TargetBackground.Value) : Tr.Pick("none", "niciunul");
            string grid = $"{Fmt.Level(p.SimulationTargets.Min())}..{Fmt.Level(p.SimulationTargets.Max())}";
            string wcs = p.WcsPath ?? (r.Raster.Source?.Wcs != null ? Tr.Pick("from the file", "din fisier") : Tr.Pick("none", "fara"));
            w.WriteLine(Tr.Pick($"  distance {distance}; target ratio {aspect}; target background {target} (grid {grid}); WCS {wcs}; seed {p.Seed}",
                                $"  distanta {distance}; raport tinta {aspect}; fundal-tinta {target} (grila {grid}); WCS {wcs}; samanta {p.Seed}"));
        }
    }
}
