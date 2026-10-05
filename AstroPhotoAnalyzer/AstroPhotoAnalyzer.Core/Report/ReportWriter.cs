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

// ReportWriter.cs - raportul ca text: intai deciziile, apoi anexele cu tabelele.
//
// Acelasi text pentru consola si pentru Raport.txt din fereastra - de asta scrie intr-un
// TextWriter si nu stie cine il citeste. Formatul e fixat de testele golden: o
// reformulare aici se vede acolo, si asta e intentia.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AstroPhotoAnalyzer
{
    internal static class ReportWriter
    {
        /// <summary>Banner + corp: ce salveaza fereastra si ce scrie consola cu -o.</summary>
        internal static void WriteFull(AnalysisResult r, TextWriter w)
        {
            RunReport.WriteBanner(w);
            WriteBody(r, w);
        }

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

        static string Mark(Verdict v) => v == Verdict.Ok ? "-" : v == Verdict.Attention ? "!" : "!!";

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

        /// <summary>De unde vine Dmax-ul, in Anexa F: nimic cand e al mediului, profilul cand a fost citit din el, altfel "(explicit)".</summary>
        internal static string DmaxOrigin(Parameters p)
            => p.DmaxProfile != null && p.DmaxOverride.HasValue ? $" (din profilul ICC \"{p.DmaxProfile}\")"
             : p.DmaxIsExplicit ? " (explicit)"
             : "";

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
            if (s.ClassFractions != null) w.WriteLine("  clase: " + s.ClassFractions);
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

        // Tabelul simularii, acelasi text in raport si in tabul Tonuri: antetul, randurile, nota
        // de sub el, definitia prafului si a cerului, tinta exacta si norii.

        internal const string SimulationHeader = "fundal-tinta         gamma   L* praf   L* cer   negru pur   sub L*15   peste L*50";

        internal static string SimulationLine(SimulationRow row)
        {
            string target = Fmt.Level(row.Target) + (row.AsIs ? " asa cum e" : row.Explicit ? " (dat)" : "") + (row.Recommended ? " *" : "");
            return $"{target,-18}   {Fmt.F3(row.Gamma),5}   {Fmt.F1(row.LstarDust),7}   {Fmt.F1(row.LstarSky),6}   {Fmt.Pct(row.PctPatchesBelowPaperBlack),9}   {Fmt.Pct(row.PctBelowLowSeparation),8}   {Fmt.Pct(row.PctAboveHighlight),10}{RowMark(row)}";
        }

        /// <summary>
        /// Marca unui rand: "print-safe" cand praful e pe negrul hartiei si cerul la prag sau
        /// peste, "cer sub L* 15" cand doar praful trece, nimic cand praful e sub negru (R-043).
        /// </summary>
        internal static string RowMark(SimulationRow row)
            => row.PrintSafe ? "   print-safe"
             : row.DustOnPaperBlack ? $"   cer sub L* {Fmt.F0(Thresholds.LowSeparationLstar)}"
             : "";

        internal static string SimulationFooter(SimulationResult s)
            => s.Recommended != null
                ? $"* = recomandat: cea mai mica tinta print-safe - praful pe negrul hartiei si cerul la L* {Fmt.F0(Thresholds.LowSeparationLstar)} sau peste"
                : s.Rows.Any(x => x.DustOnPaperBlack)
                    ? $"nicio tinta din grila nu aduce cerul la L* {Fmt.F0(Thresholds.LowSeparationLstar)} (ar trebui {TargetText(s.VisibilityTarget)})"
                : s.ExactTarget.HasValue
                    ? $"nicio tinta din grila nu aduce praful pe negrul hartiei (ar trebui ~{Fmt.Level(s.ExactTarget.Value)})"
                    : $"nicio tinta din grila nu aduce praful pe negrul hartiei, nici {Fmt.Level(Thresholds.ExactTargetHi)}";

        /// <summary>"praful = 10 % din dalele norilor catalogati (406 nori, 4078 dale); cerul = mediana dalelor din afara norilor; negru pur = dale de 32 px sub negrul hartiei (L* 3.6 la Dmax 2.4)".</summary>
        internal static string DustDefinition(SimulationResult s)
            => $"praful = {s.DustDescription}{(s.MethodNote == null ? "" : " (" + s.MethodNote + ")")}; cerul = {s.SkyDescription}; "
             + $"negru pur = dale de {Thresholds.DustPatchPx} px sub negrul hartiei (L* {Fmt.F1(s.PaperBlackLstar)} la Dmax {Fmt.Dmax(s.Dmax)})";

        /// <summary>
        /// O tinta exacta de pe scara (R-043): valoarea ei, sau o margine la capetele scarii -
        /// "<= 0.05" cand chiar prima treapta pune deja praful (sau norul) pe negru, deci tinta
        /// adevarata e undeva mai jos, unde scara nu ajunge; "> 0.60" cand nici ultima nu-l
        /// pune. Scris "0.05" simplu, primul caz parea o tinta masurata.
        /// </summary>
        internal static string TargetText(double? t)
            => !t.HasValue ? "> " + Fmt.Level(Thresholds.ExactTargetHi)
             : t.Value <= Thresholds.ExactTargetLo + 1e-9 ? "<= " + Fmt.Level(Thresholds.ExactTargetLo)
             : Fmt.Level(t.Value);

        /// <summary>Tinta exacta, cele doua la Dmax vecin, si Dmax-ul de la care trece asa cum e.</summary>
        internal static string ExactLine(SimulationResult s)
        {
            string pass = double.IsNaN(s.DmaxToPassAsIs) ? "nu trece asa cum e la niciun Dmax" : $"trece asa cum e de la Dmax {Fmt.Dmax(s.DmaxToPassAsIs)}";
            double lo = s.Dmax - Thresholds.DmaxSensitivityStep, hi = s.Dmax + Thresholds.DmaxSensitivityStep;
            return $"tinta exacta a prafului pe negru: {TargetText(s.ExactTarget)} la Dmax {Fmt.Dmax(s.Dmax)} ({TargetText(s.ExactTargetDmaxLower)} la Dmax {Fmt.Dmax(lo)}, {TargetText(s.ExactTargetDmaxHigher)} la Dmax {Fmt.Dmax(hi)}); {pass}; "
                + $"cerul ajunge la pragul de vizibilitate L* {Fmt.F0(Thresholds.LowSeparationLstar)} la {TargetText(s.VisibilityTarget)}";
        }

        internal const string CloudHeader = "nor                          dale   L*     tinta";

        internal static string CloudLine(CloudRow c)
            => $"{c.Name,-28} {Fmt.Int(c.Patches),4}   {Fmt.F1(c.Lstar),4}   {TargetText(c.ExactTarget)}";

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

        /// <summary>Un rand al tabelului de marimi, fara indentare - acelasi in raport si in tabul Rezolutie.</summary>
        internal static string SizeTableRow(SizeRow row) =>
            $"{Fmt.F0(row.WidthCm),6}{(row.Recommended ? " *" : "  ")}    {Fmt.F1(row.HeightCm),8}   {Fmt.F0(row.Dpi),3}   {Fmt.F2(row.StarMm),7}   {Fmt.F2(row.MinDistanceM) + " m",11}   {Fmt.F2(row.ViewingDistanceM) + " m",14}   {Fmt.F0(row.OuterWidthCm)} x {Fmt.F0(row.OuterHeightCm)}{(row.Oversize ? "  (peste limita)" : "")}";

        /// <summary>
        /// Legenda tabelului: steluta cand exista un rand recomandat; altfel de ce nu exista -
        /// fara stele se judeca doar grila de pixeli, la distanta naturala raportul
        /// punct/distanta e acelasi pe fiecare rand.
        /// </summary>
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
