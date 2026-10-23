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
    /// Writes the report, Romanian, and the lines of it the window's tabs show as they are: the
    /// simulation's table, the size table, the crop's cut.
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
            w.WriteLine("Fisier: " + r.Path);
            string bits = f.IsFloat ? "float" : f.BitDepth + "-bit";
            string icc = f.HasIcc ? "profil ICC: " + (f.IccDescription ?? "(fara nume)") : "fara profil ICC";
            w.WriteLine($"  {f.Format} {bits} {(f.IsColor ? "RGB" : "mono")}, {Fmt.Dim(f.W, f.H)} ({Fmt.F1(f.Megapixels)} MP), {f.Compression ?? "-"}, {Fmt.Mb(f.FileMegabytes)}, {icc}");
            Parameters p = r.Parameters;
            string dist = p.ViewingDistanceM.HasValue ? Fmt.F1(p.ViewingDistanceM.Value) + " m" : "1.5 x diagonala";
            w.WriteLine($"  Mediu: {Parameters.NameOf(p.Medium)} (Dmax {Fmt.Dmax(p.Dmax)}, negrul hartiei L* {Fmt.F1(r.Tonal.PaperBlackLstar)}). Distanta de privire: {dist}.");
            w.WriteLine();
            w.WriteLine("VERDICT: " + VerdictText.Label(r.Overall));
        }

        /// <summary>
        /// The six sections in decision order, each numbered with its title and its verdict (the
        /// maximum of its recommendations), then each recommendation with its mark and its
        /// "fiindca"; "(nimic de semnalat)" for a section without one.
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
                if (recs.Count == 0) { w.WriteLine("   (nimic de semnalat)"); continue; }
                foreach (Recommendation rec in recs)
                {
                    w.WriteLine($"   {Mark(rec.Verdict)} {rec.Text}");
                    w.WriteLine($"     fiindca: {rec.Because}");
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
            w.WriteLine("Anexa A. Tonuri");
            w.WriteLine($"  medii pe canal (0..255): {string.Join(" / ", t.MeanLevels.Select(Fmt.F1))}");
            w.WriteLine($"  sub nivelul {Thresholds.DarkLevelA}: {Fmt.Pct(t.PctBelowDarkA)}   sub {Thresholds.DarkLevelB}: {Fmt.Pct(t.PctBelowDarkB)}");
            w.WriteLine("  percentile canal referinta (0..255): " + string.Join("  ", TonalResult.PercentileRanks.Zip(t.ReferencePercentiles, (p, v) => $"p{Fmt.F1(p)}={Fmt.F1(v)}")));
            w.WriteLine($"  L* median {Fmt.F1(t.LstarMedian)}; sub negrul hartiei (L* {Fmt.F1(t.PaperBlackLstar)}): {Fmt.Pct(t.PctBelowPaperBlack)}; sub L* {Fmt.F0(Thresholds.LowSeparationLstar)}: {Fmt.Pct(t.PctBelowLowSeparation)}; peste L* {Fmt.F0(Thresholds.HighlightLstar)}: {Fmt.Pct(t.PctAboveHighlight)}");
        }

        /// <summary>
        /// Where the Dmax comes from, in Appendix F: nothing when it is the medium's, the profile
        /// when it was read from one, otherwise "(explicit)".
        /// </summary>
        /// <param name="p">The parameters the analysis ran with.</param>
        /// <returns>
        /// " (din profilul ICC "name")", " (explicit)" for a given Dmax that differs from the
        /// medium's, or "" (each with its leading space, to follow the Dmax).
        /// </returns>
        internal static string DmaxOrigin(Parameters p)
            => p.DmaxProfile != null && p.DmaxOverride.HasValue ? $" (din profilul ICC \"{p.DmaxProfile}\")"
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
            w.WriteLine($"Anexa B. Simularea fundalului (de la nivelul {Fmt.Level(s.BackgroundMeasured)}; curba gamma pe toate canalele)");
            w.WriteLine("  " + SimulationHeader);
            foreach (SimulationRow row in s.Rows)
                w.WriteLine("  " + SimulationLine(row));
            w.WriteLine("  " + SimulationFooter(s));
            w.WriteLine("  " + DustDefinition(s));
            w.WriteLine("  clase: " + s.ClassFractions);
            w.WriteLine("  " + ExactLine(s));
            if (s.Clouds.Count > 0)
            {
                w.WriteLine($"  norii catalogati cu cel putin {Thresholds.DustCloudMinPatches} dale ({Fmt.Int(s.Clouds.Count)}), de la cel mai intunecat; L* = {Fmt.F0(Thresholds.DustCloudFraction * 100)} % din dalele norului; tinta = unde ajunge pe negrul hartiei:");
                w.WriteLine("  " + CloudHeader);
                foreach (CloudRow c in s.Clouds.Take(Thresholds.DustCloudReportRows))
                    w.WriteLine("  " + CloudLine(c));
                if (s.Clouds.Count > Thresholds.DustCloudReportRows)
                    w.WriteLine($"  ... si inca {Fmt.Int(s.Clouds.Count - Thresholds.DustCloudReportRows)} (fereastra ii arata pe toti)");
            }
        }

        // The simulation's table, the same text in the report and in the Tonuri tab: the header,
        // the rows, the note under it, the definition of the dust and the sky, the exact target and
        // the clouds.

        internal const string SimulationHeader = "fundal-tinta         gamma   L* praf   L* cer   negru pur   sub L*15   peste L*50";

        /// <summary>
        /// A row of the simulation's table, under <see cref="SimulationHeader"/>, without the
        /// indent.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <returns>
        /// The target (with "asa cum e" or "(dat)", and " *" on the recommended row), the gamma,
        /// the dust's and the sky's L*, the three shares, and the row's mark
        /// (<see cref="RowMark"/>).
        /// </returns>
        internal static string SimulationLine(SimulationRow row)
        {
            string target = Fmt.Level(row.Target) + (row.AsIs ? " asa cum e" : row.Explicit ? " (dat)" : "") + (row.Recommended ? " *" : "");
            return $"{target,-18}   {Fmt.F3(row.Gamma),5}   {Fmt.F1(row.LstarDust),7}   {Fmt.F1(row.LstarSky),6}   {Fmt.Pct(row.PctPatchesBelowPaperBlack),9}   {Fmt.Pct(row.PctBelowLowSeparation),8}   {Fmt.Pct(row.PctAboveHighlight),10}{RowMark(row)}";
        }

        /// <summary>
        /// A row's mark: "print-safe" when the dust is on paper black and the sky at the
        /// threshold or above, "cer sub L* 15" when only the dust passes, nothing when the dust is
        /// under black (R-043).
        /// </summary>
        /// <param name="row">The row.</param>
        /// <returns>The mark with its three leading spaces, or "".</returns>
        internal static string RowMark(SimulationRow row)
            => row.PrintSafe ? "   print-safe"
             : row.DustOnPaperBlack ? $"   cer sub L* {Fmt.F0(Thresholds.LowSeparationLstar)}"
             : "";

        /// <summary>The note under the simulation's table.</summary>
        /// <param name="s">The simulation.</param>
        /// <returns>
        /// What "*" means when a row is recommended; otherwise why none is: the sky reaches the
        /// visibility threshold on no row (with its target), or the dust reaches paper black on
        /// no row (with its exact target, or "nici 0.60" when the ladder has none).
        /// </returns>
        internal static string SimulationFooter(SimulationResult s)
            => s.Recommended != null
                ? $"* = recomandat: cea mai mica tinta print-safe - praful pe negrul hartiei si cerul la L* {Fmt.F0(Thresholds.LowSeparationLstar)} sau peste"
                : s.Rows.Any(x => x.DustOnPaperBlack)
                    ? $"nicio tinta din grila nu aduce cerul la L* {Fmt.F0(Thresholds.LowSeparationLstar)} (ar trebui {TargetText(s.VisibilityTarget)})"
                : s.ExactTarget.HasValue
                    ? $"nicio tinta din grila nu aduce praful pe negrul hartiei (ar trebui ~{Fmt.Level(s.ExactTarget.Value)})"
                    : $"nicio tinta din grila nu aduce praful pe negrul hartiei, nici {Fmt.Level(Thresholds.ExactTargetHi)}";

        /// <summary>
        /// "praful = percentila 25 % a dalelor de praf (cu 0.5 L* sub cerul modelat (4 sigma ale
        /// cerului); 5.5 % din cadru) (fara WCS); cerul = mediana dalelor de cer (clase relative
        /// la cerul modelat); negru pur = dale de 32 px sub negrul hartiei (L* 9.0 la Dmax 2.0)".
        /// </summary>
        /// <param name="s">The simulation.</param>
        /// <returns>
        /// What the dust is (with why the band classes were not made, on the sky path), what the
        /// sky is, and what "negru pur" counts, with the paper black and the Dmax.
        /// </returns>
        internal static string DustDefinition(SimulationResult s)
            => $"praful = {s.DustDescription}{(s.MethodNote == null ? "" : " (" + s.MethodNote + ")")}; cerul = {s.SkyDescription}; "
             + $"negru pur = dale de {Thresholds.DustPatchPx} px sub negrul hartiei (L* {Fmt.F1(s.PaperBlackLstar)} la Dmax {Fmt.Dmax(s.Dmax)})";

        /// <summary>
        /// An exact target from the ladder (R-043): its value, or a bound at the ladder's ends -
        /// "&lt;= 0.05" when even the first step already puts the dust (or the cloud) on black, so
        /// the true target is somewhere lower, where the ladder does not reach; "> 0.60" when not
        /// even the last puts it there. Written as a plain "0.05", the first case looked like a
        /// measured target.
        /// </summary>
        /// <param name="t">
        /// The target, 0..1, read on the ladder (the dust's, a cloud's, or the sky's at the
        /// visibility threshold); null when no step of the ladder reaches it.
        /// </param>
        /// <returns>The level (<see cref="Fmt.Level"/>), "&lt;= 0.05" or "> 0.60".</returns>
        internal static string TargetText(double? t)
            => !t.HasValue ? "> " + Fmt.Level(Thresholds.ExactTargetHi)
             : t.Value <= Thresholds.ExactTargetLo + 1e-9 ? "<= " + Fmt.Level(Thresholds.ExactTargetLo)
             : Fmt.Level(t.Value);

        /// <summary>
        /// The Dmax from which the dust as it is reaches paper black, as a sentence (R-043):
        /// "praful ajunge pe negrul hartiei de la Dmax 2.28", or "praful nu ajunge pe negrul
        /// hartiei la niciun Dmax (e la L* 0)". When the sky as it is is under the visibility
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
                ? "praful nu ajunge pe negrul hartiei la niciun Dmax (e la L* 0)"
                : $"praful ajunge pe negrul hartiei de la Dmax {Fmt.Dmax(s.DmaxDustOnPaperBlackAsIs)}";
            return s.SkyBelowVisibility
                ? dust + $"; asa cum e, nicio hartie nu face imaginea print-safe: cerul e sub L* {Fmt.F0(Thresholds.LowSeparationLstar)}, iar remediul e ridicarea fundalului la stretch"
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
            return $"tinta exacta a prafului pe negru: {TargetText(s.ExactTarget)} la Dmax {Fmt.Dmax(s.Dmax)} ({TargetText(s.ExactTargetDmaxLower)} la Dmax {Fmt.Dmax(lo)}, {TargetText(s.ExactTargetDmaxHigher)} la Dmax {Fmt.Dmax(hi)}); {DustDmaxText(s)}; "
                + $"cerul ajunge la pragul de vizibilitate L* {Fmt.F0(Thresholds.LowSeparationLstar)} la {TargetText(s.VisibilityTarget)}";
        }

        /// <summary>
        /// What the crop cuts, as text (R-032): "192 px pe fiecare parte din latime" when the two
        /// sides are equal, otherwise "0 px la stanga si 1 px la dreapta din latime" or
        /// "... sus si ... jos din inaltime". The same text in the recommendation and in the
        /// Preview tile.
        /// </summary>
        /// <param name="c">The crop, one that is needed (its axis is not null).</param>
        /// <returns>The pixels cut on each side and the axis, without a final full stop.</returns>
        internal static string CropCutText(CropResult c)
        {
            if (c.CutStartPx == c.CutEndPx) return $"{Fmt.Int(c.CutStartPx)} px pe fiecare parte din {c.Axis}";
            bool width = c.Axis == "latime";
            return $"{Fmt.Int(c.CutStartPx)} px {(width ? "la stanga" : "sus")} si {Fmt.Int(c.CutEndPx)} px {(width ? "la dreapta" : "jos")} din {c.Axis}";
        }

        internal const string CloudHeader = "nor                          dale   L*     tinta";

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
            w.WriteLine("Anexa C. Export");
            w.WriteLine($"  TIFF 8-bit LZW: {Fmt.Mb(r.Jpeg.Tiff8LzwMegabytes)}");
            w.WriteLine("  JPEG 4:4:4   marime     err medie   err max   p99    p99.9   > 2 niv   nivel mediu");
            foreach (JpegRow j in r.Jpeg.Rows)
                w.WriteLine($"  q{j.Quality,-3}         {Fmt.Mb(j.Megabytes),8}   {Fmt.F3(j.MeanAbsError),9}   {Fmt.F1(j.MaxError),7}   {Fmt.F2(j.P99),4}   {Fmt.F2(j.P999),5}   {Fmt.Pct(j.PctOverThreshold),7}   {Fmt.F0(j.MeanLevelOverThreshold),11}");
            if (q.Applicable)
            {
                w.WriteLine($"  rotunjire 16 -> 8: medie {Fmt.F3(q.RoundingMeanAbs)}, maxim {Fmt.F3(q.RoundingMax)}, p99.9 {Fmt.F3(q.RoundingP999)} niveluri");
                w.WriteLine($"  stres shadow-lift (gamma {Fmt.F1(Thresholds.ShadowLiftGamma)}), niveluri {Thresholds.ShadowLiftLevelLo}..{Thresholds.ShadowLiftLevelHi}: din 16 biti {q.LevelsUsedFrom16} folosite / {q.MissingFrom16} lipsa; din 8 biti {q.LevelsUsedFrom8} folosite / {q.MissingFrom8} lipsa");
            }
            w.WriteLine(q.PlateauKnown
                ? $"  palier pe cerul cuantizat: {Fmt.F1(q.PlateauPx)} px pe {q.PlateauTiles} dale"
                : $"  palier: fara dale de cer curat ({q.PlateauTiles})");
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
            w.WriteLine("Anexa D. Rezolutie si marime");
            w.WriteLine(s.Insufficient
                ? $"  stele: {s.Count} masurate - prea putine pentru FWHM"
                : $"  stele: {s.Count} masurate; FWHM median {Fmt.F2(s.FwhmMedian)} px, Q1 {Fmt.F2(s.FwhmQ1)}, Q3 {Fmt.F2(s.FwhmQ3)}; detaliu real ~{Fmt.F0(ps.EffectiveDetailPx)} px pe latime");
            w.WriteLine("  regiuni: " + string.Join("; ", s.Regions.Select(x => $"{x.Name} {(x.Known ? Fmt.F2(x.FwhmMedian) + " px" : "n/a")} ({x.Count})"))
                + (double.IsNaN(s.CornerToCenterRatio) ? "" : $"; colt/centru {Fmt.F2(s.CornerToCenterRatio)}"));
            w.WriteLine($"  marimi pentru {Fmt.Dim(ps.PixelW, ps.PixelH)} px{(r.Crop.Needed ? " (dupa decupaj)" : "")}:");
            w.WriteLine("  latime cm   inaltime   dpi   stea mm   punct de la   dist. folosita   rama ext. cm");
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
        /// "(peste limita)" over <see cref="Thresholds.OversizeCm"/>.
        /// </returns>
        internal static string SizeTableRow(SizeRow row) =>
            $"{Fmt.F0(row.WidthCm),6}{(row.Recommended ? " *" : "  ")}    {Fmt.F1(row.HeightCm),8}   {Fmt.F0(row.Dpi),3}   {Fmt.F2(row.StarMm),7}   {Fmt.F2(row.MinDistanceM) + " m",11}   {Fmt.F2(row.ViewingDistanceM) + " m",14}   {Fmt.F0(row.OuterWidthCm)} x {Fmt.F0(row.OuterHeightCm)}{(row.Oversize ? "  (peste limita)" : "")}";

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
                yield return "* = recomandat";
                yield break;
            }
            if (!ps.FwhmKnown)
            {
                yield return $"fara rand recomandat: fara stele nu se masoara detaliul, ci doar grila de pixeli (prag {Fmt.F0(ps.FallbackDpi)} dpi) - o limita a fisierului, nu a imaginii";
                yield break;
            }
            yield return $"fara rand recomandat: \"punct de la\" si \"dist. folosita\" cresc amandoua cu latimea, deci raportul lor ({Fmt.F2(ps.NaturalRatio)}) e acelasi pe fiecare rand";
            yield return "  si testul nu desparte marimile - o alege peretele; da o distanta de privire ca sa le desparta";
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
            w.WriteLine("Anexa E. Culoarea fundalului");
            w.WriteLine($"  {(b.ColorApplicable ? "culoarea fundalului, R / G / B (0..255)" : "fundalul, un singur canal (0..255)")}: {string.Join(" / ", b.Levels.Select(Fmt.F1))}"
                + (b.ColorApplicable ? $"; G - (R+B)/2 = {Fmt.S1(b.GreenExcess)}; R - B = {Fmt.S1(b.RedMinusBlue)}" : ""));
            foreach (EdgeRegion e in b.Regions)
                w.WriteLine($"  {e.Name,-18} {string.Join(" / ", e.Levels.Select(Fmt.F1)),-22} delta {string.Join(" / ", e.Deltas.Select(Fmt.S1))}{(e.Flagged ? "  !" : "")}");
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
            w.WriteLine("Anexa F. Parametri");
            w.WriteLine($"  mediu {Parameters.NameOf(p.Medium)}, Dmax {Fmt.Dmax(p.Dmax)}{DmaxOrigin(p)}; latimi {string.Join(", ", p.CandidateWidthsCm.Select(Fmt.F0))} cm; passepartout {Fmt.F0(p.PassepartoutCm)} cm, rama {Fmt.F0(p.FrameCm)} cm");
            if (p.DmaxProfile != null && p.DmaxOverride.HasValue && p.DmaxProfileNote != null) w.WriteLine("  nota profilului: " + p.DmaxProfileNote);
            w.WriteLine($"  distanta {(p.ViewingDistanceM.HasValue ? Fmt.F1(p.ViewingDistanceM.Value) + " m" : "naturala")}; raport tinta {(p.TargetAspect.HasValue ? Fmt.F3(p.TargetAspect.Value) : "nativ")}; fundal-tinta {(p.TargetBackground.HasValue ? Fmt.Level(p.TargetBackground.Value) : "niciunul")} (grila {Fmt.Level(p.SimulationTargets.Min())}..{Fmt.Level(p.SimulationTargets.Max())}); WCS {(p.WcsPath ?? (r.Raster.Source?.Wcs != null ? "din fisier" : "fara"))}; samanta {p.Seed}");
        }
    }
}
