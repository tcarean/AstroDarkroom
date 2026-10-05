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

// PreviewWindowViewModel.cs - fereastra de preview a unui rand al simularii (R-131):
// imaginea ridicata la tinta randului, cu pixelii de sub negrul hartiei si cei de sub
// pragul de separare slaba vopsiti diferit.
//
// Imaginea ridicata si clasa fiecarui pixel vin din nucleu (PrintPreview, R-045); aici doar
// se vopsesc si se lasa omul sa stinga straturile.

using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AstroPhotoAnalyzer.Wpf
{
    public sealed class PreviewWindowViewModel : ObservableObject
    {
        static readonly Color BlackColour = Color.FromArgb(140, 0xF8, 0x51, 0x49);
        static readonly Color LowColour = Color.FromArgb(100, 0xE3, 0xB3, 0x41);

        bool _showBlack = true, _showLow = true;

        public string Title { get; private init; }
        public BitmapSource Image { get; private init; }
        public BitmapSource BlackLayer { get; private init; }
        public BitmapSource LowLayer { get; private init; }
        public int Width { get; private init; }
        public int Height { get; private init; }

        /// <summary>Ce rand e: tinta, gamma, praful si cerul lui, negrul hartiei.</summary>
        public string Caption { get; private init; }
        public string BlackLegend { get; private init; }
        public string LowLegend { get; private init; }

        public bool ShowBlack { get => _showBlack; set => Set(ref _showBlack, value); }
        public bool ShowLow { get => _showLow; set => Set(ref _showLow, value); }

        PreviewWindowViewModel() { }

        internal static PreviewWindowViewModel From(AnalysisResult r, SimulationRow row)
        {
            PrintPreviewResult p = PrintPreview.Render(r.Raster, row.Gamma, r.Parameters.Dmax);
            Raster img = p.Image;
            int w = img.W, h = img.H;
            PreviewClass[] c = p.Classes;
            string target = row.AsIs ? $"asa cum e ({Fmt.Level(row.Target)})" : $"fundal {Fmt.Level(row.Target)}" + (row.Explicit ? " (dat)" : "") + (row.Recommended ? ", recomandat" : "");
            return new PreviewWindowViewModel
            {
                Title = $"Preview {target} - {System.IO.Path.GetFileName(r.Path)}",
                Image = RasterBitmaps.Region(img, 0, 0, w, h),
                Width = w,
                Height = h,
                BlackLayer = RasterBitmaps.Overlay(w, h, (x, y) => c[y * w + x] == PreviewClass.UnderPaperBlack, BlackColour),
                LowLayer = RasterBitmaps.Overlay(w, h, (x, y) => c[y * w + x] == PreviewClass.UnderLowSeparation, LowColour),
                Caption = $"{target}: gamma {Fmt.F3(row.Gamma)}; praful la L* {Fmt.F1(row.LstarDust)}, cerul la L* {Fmt.F1(row.LstarSky)}; "
                    + $"negrul hartiei L* {Fmt.F1(p.PaperBlackLstar)} la Dmax {Fmt.Dmax(r.Parameters.Dmax)}" + ReportWriter.RowMark(row).Replace("   ", "; "),
                BlackLegend = $"sub negrul hartiei (L* {Fmt.F1(p.PaperBlackLstar)}): {Fmt.Pct(p.PctUnderPaperBlack)} din pixeli - se tiparesc cu acelasi negru",
                LowLegend = $"sub L* {Fmt.F0(Thresholds.LowSeparationLstar)}, peste negru: {Fmt.Pct(p.PctUnderLowSeparation - p.PctUnderPaperBlack)} din pixeli - hartia le separa, ochiul abia",
            };
        }
    }
}
