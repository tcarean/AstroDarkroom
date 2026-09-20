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

// ResultTabs.cs - cele sase taburi cu rezultate. Fiecare primeste AnalysisResult prin
// Populate si il transforma in randuri; niciunul nu calculeaza o cifra si nu formuleaza o
// propozitie - textele vin din Recommendations si din ReportWriter, cifrele din Fmt.
//
// Toate intr-un fisier fiindca sunt toate acelasi lucru: o lista de randuri si un bool
// HasResult. Cand unul va creste, isi ia fisierul lui.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;

namespace AstroPhotoAnalyzer.Wpf
{
    public abstract class ResultTabViewModel : ObservableObject
    {
        bool _has;
        public bool HasResult { get => _has; protected set => Set(ref _has, value); }

        internal abstract void Populate(AnalysisResult r);

        internal virtual void Clear()
        {
            HasResult = false;
        }

        protected static MetricRow Row(string label, string value, string note = null)
            => new MetricRow { Label = label, Value = value, Note = note };
    }

    // ------------------------------------------------------------ Raport

    public sealed class ReportTabViewModel : ResultTabViewModel
    {
        IReadOnlyList<SectionRow> _sections = Array.Empty<SectionRow>();
        string _verdict = "";
        string _text = "";
        bool _showText;

        public IReadOnlyList<SectionRow> Sections { get => _sections; private set => Set(ref _sections, value); }
        public string VerdictLabel { get => _verdict; private set => Set(ref _verdict, value); }

        /// <summary>Raportul complet, cu banner - exact ce se salveaza in Raport.txt.</summary>
        public string Text { get => _text; private set => Set(ref _text, value); }
        public bool ShowText { get => _showText; set => Set(ref _showText, value); }

        internal override void Populate(AnalysisResult r)
        {
            var sections = new List<SectionRow>();
            foreach (Topic t in Enum.GetValues(typeof(Topic)))
            {
                var recs = r.Recommendations.Where(x => x.Topic == t).ToList();
                Verdict v = recs.Count == 0 ? Verdict.Ok : recs.Max(x => x.Verdict);
                sections.Add(new SectionRow
                {
                    Number = (int)t,
                    Title = VerdictText.Title(t),
                    VerdictLabel = VerdictText.Label(v),
                    Recommendations = recs.Select(x => new RecommendationRow
                    {
                        Mark = x.Verdict == Verdict.Ok ? "-" : x.Verdict == Verdict.Attention ? "!" : "!!",
                        Text = x.Text,
                        Because = x.Because,
                        VerdictLabel = VerdictText.Label(x.Verdict),
                    }).ToList(),
                });
            }
            Sections = sections;
            VerdictLabel = VerdictText.Label(r.Overall);
            var sw = new System.IO.StringWriter();
            ReportWriter.WriteFull(r, sw);
            Text = sw.ToString();
            HasResult = true;
        }

        internal override void Clear()
        {
            base.Clear();
            Sections = Array.Empty<SectionRow>();
            VerdictLabel = "";
            Text = "";
        }
    }

    // ------------------------------------------------------------ Tonuri

    public sealed class TonesTabViewModel : ResultTabViewModel
    {
        IReadOnlyList<MetricRow> _rows = Array.Empty<MetricRow>();
        IReadOnlyList<double> _hist = Array.Empty<double>();
        IReadOnlyList<string> _simulation = Array.Empty<string>();
        double _paperBlack = double.NaN;

        public IReadOnlyList<MetricRow> Rows { get => _rows; private set => Set(ref _rows, value); }
        public IReadOnlyList<double> Histogram { get => _hist; private set => Set(ref _hist, value); }

        /// <summary>Negrul hartiei, in L*, ca pozitie in histograma (cosurile sunt 0..100).</summary>
        public double PaperBlack { get => _paperBlack; private set => Set(ref _paperBlack, value); }
        public IReadOnlyList<string> SimulationRows { get => _simulation; private set => Set(ref _simulation, value); }

        internal override void Populate(AnalysisResult r)
        {
            TonalResult t = r.Tonal;
            SimulationResult s = r.Simulation;
            Rows = new[]
            {
                Row("Medii pe canal (0..255)", string.Join(" / ", t.MeanLevels.Select(Fmt.F1))),
                Row($"Sub nivelul {Thresholds.DarkLevelA} / {Thresholds.DarkLevelB}", $"{Fmt.Pct(t.PctBelowDarkA)} / {Fmt.Pct(t.PctBelowDarkB)}"),
                Row("Fundal masurat (mediana canalului de referinta)", Fmt.F3(t.BackgroundMedian)),
                Row("L* median", Fmt.F1(t.LstarMedian)),
                Row($"Sub negrul hartiei (L* {Fmt.F1(t.PaperBlackLstar)}, Dmax {Fmt.F1(t.Dmax)})", Fmt.Pct(t.PctBelowPaperBlack),
                    t.PrintSafe ? "print-safe" : t.BackgroundTooLowForPaper ? "prea jos pentru hartie" : "la limita"),
                Row($"Sub L* {Fmt.F0(Thresholds.LowSeparationLstar)} (separare slaba)", Fmt.Pct(t.PctBelowLowSeparation)),
                Row($"Peste L* {Fmt.F0(Thresholds.HighlightLstar)}", Fmt.Pct(t.PctAboveHighlight)),
                Row("Percentile canal referinta", string.Join("  ", TonalResult.PercentileRanks.Zip(t.ReferencePercentiles, (p, v) => $"p{Fmt.F1(p)}={Fmt.F1(v)}"))),
            };
            Histogram = t.LstarHistogramPct;
            PaperBlack = t.PaperBlackLstar;
            SimulationRows = new[] { "fundal-tinta   gamma   L* median   sub negru   sub L*15   peste L*50" }
                .Concat(s.Rows.Select(row =>
                    $"{(row.AsIs ? "asa cum e" : Fmt.F2(row.Target)),-12}   {Fmt.F3(row.Gamma),5}   {Fmt.F1(row.LstarMedian),9}   {Fmt.Pct(row.PctBelowPaperBlack),9}   {Fmt.Pct(row.PctBelowLowSeparation),8}   {Fmt.Pct(row.PctAboveHighlight),10}{(row.PrintSafe ? "   print-safe" : "")}"))
                .ToList();
            HasResult = true;
        }
    }

    // ------------------------------------------------------------ Export

    public sealed class ExportTabViewModel : ResultTabViewModel
    {
        IReadOnlyList<string> _jpeg = Array.Empty<string>();
        IReadOnlyList<MetricRow> _rows = Array.Empty<MetricRow>();

        public IReadOnlyList<string> JpegRows { get => _jpeg; private set => Set(ref _jpeg, value); }
        public IReadOnlyList<MetricRow> Rows { get => _rows; private set => Set(ref _rows, value); }

        internal override void Populate(AnalysisResult r)
        {
            QuantizationResult q = r.Quantization;
            JpegRows = new[] { "JPEG 4:4:4   marime     err medie   err max   p99    p99.9   > 2 niv   nivel mediu" }
                .Concat(r.Jpeg.Rows.Select(j =>
                    $"q{j.Quality,-3}         {Fmt.Mb(j.Megabytes),8}   {Fmt.F3(j.MeanAbsError),9}   {Fmt.F1(j.MaxError),7}   {Fmt.F2(j.P99),4}   {Fmt.F2(j.P999),5}   {Fmt.Pct(j.PctOverThreshold),7}   {Fmt.F0(j.MeanLevelOverThreshold),11}"))
                .ToList();
            var rows = new List<MetricRow> { Row("TIFF 8-bit LZW", Fmt.Mb(r.Jpeg.Tiff8LzwMegabytes)) };
            if (q.Applicable)
            {
                rows.Add(Row("Rotunjire 16 -> 8 biti: medie / maxim / p99.9", $"{Fmt.F3(q.RoundingMeanAbs)} / {Fmt.F3(q.RoundingMax)} / {Fmt.F3(q.RoundingP999)}", "in niveluri din 255; jumatate de nivel e maximul posibil"));
                rows.Add(Row($"Stres shadow-lift, niveluri {Thresholds.ShadowLiftLevelLo}..{Thresholds.ShadowLiftLevelHi}: din 16 biti", $"{q.LevelsUsedFrom16} folosite / {q.MissingFrom16} lipsa"));
                rows.Add(Row("... din 8 biti", $"{q.LevelsUsedFrom8} folosite / {q.MissingFrom8} lipsa", q.Keep16BitMaster ? "pastreaza master-ul de 16 biti" : null));
            }
            rows.Add(q.PlateauKnown
                ? Row("Palier pe cerul cuantizat", $"{Fmt.F1(q.PlateauPx)} px", $"pe {q.PlateauTiles} dale" + (r.Size.RecommendedRow != null ? $"; {Fmt.F2(q.PlateauMmAt(r.Size.RecommendedRow.Dpi))} mm la {Fmt.F0(r.Size.RecommendedRow.Dpi)} dpi" : ""))
                : Row("Palier pe cerul cuantizat", "n/a", "fara dale de cer curat"));
            Rows = rows;
            HasResult = true;
        }
    }

    // ------------------------------------------------------------ Rezolutie

    public sealed class ResolutionTabViewModel : ResultTabViewModel
    {
        IReadOnlyList<MetricRow> _rows = Array.Empty<MetricRow>();
        IReadOnlyList<string> _sizes = Array.Empty<string>();

        public IReadOnlyList<MetricRow> Rows { get => _rows; private set => Set(ref _rows, value); }
        public IReadOnlyList<string> SizeRows { get => _sizes; private set => Set(ref _sizes, value); }

        internal override void Populate(AnalysisResult r)
        {
            StarResult s = r.Stars;
            PrintSizeResult ps = r.Size;
            var rows = new List<MetricRow>
            {
                s.Insufficient
                    ? Row("Stele masurate", Fmt.Int(s.Count), $"prea putine pentru FWHM (minim {Thresholds.StarMinCount})")
                    : Row("Stele masurate", Fmt.Int(s.Count)),
                Row("FWHM median (Q1 / Q3)", s.Insufficient ? "n/a" : $"{Fmt.F2(s.FwhmMedian)} px ({Fmt.F2(s.FwhmQ1)} / {Fmt.F2(s.FwhmQ3)})"),
                Row("Detaliu real pe latime", ps.FwhmKnown ? $"~{Fmt.F0(ps.EffectiveDetailPx)} px" : "n/a", "latimea in pixeli impartita la FWHM"),
            };
            foreach (StarRegion reg in s.Regions)
                rows.Add(Row("Regiune: " + reg.Name, reg.Known ? Fmt.F2(reg.FwhmMedian) + " px" : "n/a", $"{reg.Count} stele"));
            if (!double.IsNaN(s.CornerToCenterRatio))
                rows.Add(Row("Colt / centru", Fmt.F2(s.CornerToCenterRatio), s.SoftCorners ? "colturi moi" : null));
            Rows = rows;

            SizeRows = new[] { $"marimi pentru {Fmt.Dim(ps.PixelW, ps.PixelH)} px{(r.Crop.Needed ? " (dupa decupaj)" : "")}:", "latime cm   inaltime   dpi   stea mm   punct de la   dist. folosita   rama ext. cm" }
                .Concat(ps.Rows.Select(row =>
                    $"{Fmt.F0(row.WidthCm),6}{(row.Recommended ? " *" : "  ")}    {Fmt.F1(row.HeightCm),8}   {Fmt.F0(row.Dpi),3}   {Fmt.F2(row.StarMm),7}   {Fmt.F2(row.MinDistanceM) + " m",11}   {Fmt.F2(row.ViewingDistanceM) + " m",14}   {Fmt.F0(row.OuterWidthCm)} x {Fmt.F0(row.OuterHeightCm)}{(row.Oversize ? "  (peste limita)" : "")}"))
                .Append("* = recomandat")
                .ToList();
            HasResult = true;
        }
    }

    // ------------------------------------------------------------ Fundal

    public sealed class BackgroundTabViewModel : ResultTabViewModel
    {
        IReadOnlyList<MetricRow> _rows = Array.Empty<MetricRow>();
        IReadOnlyList<string> _regions = Array.Empty<string>();

        public IReadOnlyList<MetricRow> Rows { get => _rows; private set => Set(ref _rows, value); }
        public IReadOnlyList<string> RegionRows { get => _regions; private set => Set(ref _regions, value); }

        internal override void Populate(AnalysisResult r)
        {
            BackgroundResult b = r.Background;
            var rows = new List<MetricRow> { Row("Fundal (0..255)", string.Join(" / ", b.Levels.Select(Fmt.F1)), "cel mai intunecat sfert, fara banda de jos") };
            if (b.ColorApplicable)
            {
                rows.Add(Row("G - (R+B)/2", Fmt.S1(b.GreenExcess) + " niveluri", b.GreenExcess > Thresholds.CastWarnLevels ? "tenta verde" : b.GreenExcess < -Thresholds.CastWarnLevels ? "tenta magenta" : null));
                rows.Add(Row("R - B", Fmt.S1(b.RedMinusBlue) + " niveluri", b.RedMinusBlue > Thresholds.CastWarnLevels ? "tenta calda" : b.RedMinusBlue < -Thresholds.CastWarnLevels ? "tenta rece" : null));
            }
            Rows = rows;
            RegionRows = b.Regions.Select(e =>
                $"{e.Name,-18} {string.Join(" / ", e.Levels.Select(Fmt.F1)),-22} delta {string.Join(" / ", e.Deltas.Select(Fmt.S1))}{(e.Flagged ? "  !" : "")}").ToList();
            HasResult = true;
        }
    }

    // ------------------------------------------------------------ Preview

    public sealed class PreviewTabViewModel : ResultTabViewModel
    {
        IReadOnlyList<PreviewTile> _tiles = Array.Empty<PreviewTile>();
        public IReadOnlyList<PreviewTile> Tiles { get => _tiles; private set => Set(ref _tiles, value); }

        internal override void Populate(AnalysisResult r)
        {
            Raster img = r.Raster;
            var tiles = new List<PreviewTile>();
            int cw = Math.Min(480, img.W), ch = Math.Min(320, img.H);
            int cx = (img.W - cw) / 2, cy = (img.H - ch) / 2;

            tiles.Add(new PreviewTile { Title = "Centru, 100 %", Image = RasterBitmaps.Region(img, cx, cy, cw, ch) });
            tiles.Add(new PreviewTile { Title = "Centru, 200 %", Image = RasterBitmaps.Region(img, cx + cw / 4, cy + ch / 4, cw / 2, ch / 2, zoom: 2), Note = "pixelii din fisier, mariti de doua ori" });
            int kw = Math.Min(320, img.W), kh = Math.Min(220, img.H);
            tiles.Add(new PreviewTile { Title = "Colt stanga-sus, 100 %", Image = RasterBitmaps.Region(img, 0, 0, kw, kh) });
            tiles.Add(new PreviewTile { Title = "Colt dreapta-sus, 100 %", Image = RasterBitmaps.Region(img, img.W - kw, 0, kw, kh) });
            tiles.Add(new PreviewTile { Title = "Colt stanga-jos, 100 %", Image = RasterBitmaps.Region(img, 0, img.H - kh, kw, kh) });
            tiles.Add(new PreviewTile { Title = "Colt dreapta-jos, 100 %", Image = RasterBitmaps.Region(img, img.W - kw, img.H - kh, kw, kh) });

            const int side = 520;
            tiles.Add(new PreviewTile { Title = "Cadru intreg, asa cum e", Image = RasterBitmaps.Whole(img, side) });
            SimulationRow primary = r.Simulation.Primary;
            if (!primary.AsIs)
            {
                Raster small = RasterOps.Subsample(img, Math.Max(1, (int)Math.Ceiling(Math.Max(img.W, img.H) / (double)side)));
                tiles.Add(new PreviewTile
                {
                    Title = $"Simulare print, fundal {Fmt.F2(primary.Target)}",
                    Image = RasterBitmaps.Whole(RasterOps.Gamma(small, primary.Gamma), side),
                    Note = "curba gamma pe toate canalele; pe ecran pare plata - hartia o aduce inapoi",
                });
            }
            if (r.Crop.Needed)
                tiles.Add(new PreviewTile
                {
                    Title = "Decupaj la raportul tinta",
                    Image = RasterBitmaps.Whole(RasterOps.Crop(img, r.Crop.X0, r.Crop.Y0, r.Crop.W, r.Crop.H), side),
                    Note = $"{Fmt.Dim(r.Crop.W, r.Crop.H)} px, centrat; {Fmt.Int(r.Crop.CutPerSidePx)} px taiati pe fiecare parte din {r.Crop.Axis}",
                });
            Tiles = tiles;
            HasResult = true;
        }

        internal override void Clear()
        {
            base.Clear();
            Tiles = Array.Empty<PreviewTile>();
        }
    }
}
