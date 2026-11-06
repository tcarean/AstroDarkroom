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

// PreviewWindowViewModel.cs - the preview window of one row of the simulation (R-131): the
// image lifted to the row's target, with the 32 px patches under paper black and those under
// the weak-separation threshold painted differently (ADR 0082).
//
// The lifted image, the class of each pixel (its patch's) and the two shares of the patches
// come from the core (PrintPreview, R-045); here they are painted, the amber legend's share is
// taken as the difference of the two, and the user is let to switch the layers on: both start
// off, so the window opens on the lifted image alone (ADR 0064). The note under the boxes is
// written here, its patch side from Thresholds.

using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// The preview window's state (R-131): the lifted image, its two overlays with their
    /// legends, the caption, and the two visibility switches.
    /// </summary>
    public sealed class PreviewWindowViewModel : ObservableObject
    {
        static readonly Color BlackColour = Color.FromArgb(140, 0xF8, 0x51, 0x49);
        static readonly Color LowColour = Color.FromArgb(100, 0xE3, 0xB3, 0x41);

        bool _showBlack, _showLow;

        public string Title { get; private init; }
        public BitmapSource Image { get; private init; }
        public BitmapSource BlackLayer { get; private init; }
        public BitmapSource LowLayer { get; private init; }
        public int Width { get; private init; }
        public int Height { get; private init; }

        /// <summary>
        /// Which row it is: the target, the gamma, its dust and sky, paper black.
        /// </summary>
        public string Caption { get; private init; }
        public string BlackLegend { get; private init; }
        public string LowLegend { get; private init; }

        /// <summary>
        /// The note under the boxes: a reduced copy lifted by a simple curve, and the colours
        /// covering the patches the table counts, not pixels.
        /// </summary>
        public string Note { get; private init; }

        public bool ShowBlack { get => _showBlack; set => Set(ref _showBlack, value); }
        public bool ShowLow { get => _showLow; set => Set(ref _showLow, value); }

        /// <summary>Only <see cref="From"/> builds one.</summary>
        PreviewWindowViewModel() { }

        /// <summary>
        /// Renders a simulation row through the core and paints its two classes: red where a
        /// pixel's patch is under paper black, amber where it is under the weak-separation L*
        /// but over black.
        /// </summary>
        /// <param name="r">The analysis the row belongs to; its raster and Dmax are read.</param>
        /// <param name="row">
        /// The row: its gamma lifts the image, its figures fill the caption.
        /// </param>
        /// <returns>The view-model, with both layers hidden until their boxes are ticked.</returns>
        internal static PreviewWindowViewModel From(AnalysisResult r, SimulationRow row)
        {
            PrintPreviewResult p = PrintPreview.Render(r.Raster, row.Gamma, r.Parameters.Dmax, row.AsIs);
            Raster img = p.Image;
            int w = img.W, h = img.H;
            PreviewClass[] c = p.Classes;
            string target = row.AsIs
                ? Tr.Pick($"as is ({Fmt.Level(row.Target)})", $"asa cum e ({Fmt.Level(row.Target)})")
                : Tr.Pick($"background {Fmt.Level(row.Target)}", $"fundal {Fmt.Level(row.Target)}")
                  + (row.Explicit ? Tr.Pick(" (given)", " (dat)") : "") + (row.Recommended ? Tr.Pick(", recommended", ", recomandat") : "");
            return new PreviewWindowViewModel
            {
                Title = $"Preview {target} - {System.IO.Path.GetFileName(r.Path)}",
                Image = RasterBitmaps.Region(img, 0, 0, w, h),
                Width = w,
                Height = h,
                BlackLayer = RasterBitmaps.Overlay(w, h, (x, y) => c[y * w + x] == PreviewClass.UnderPaperBlack, BlackColour),
                LowLayer = RasterBitmaps.Overlay(w, h, (x, y) => c[y * w + x] == PreviewClass.UnderLowSeparation, LowColour),
                Caption = Tr.Pick($"{target}: the tones lifted with a curve (gamma {Fmt.F3(row.Gamma)}); the dust at L* {Fmt.F1(row.LstarDust)}, the sky at L* {Fmt.F1(row.LstarSky)}; "
                        + $"paper black is L* {Fmt.F1(p.PaperBlackLstar)} at Dmax {Fmt.Dmax(r.Parameters.Dmax)}",
                        $"{target}: tonurile ridicate cu o curba (gamma {Fmt.F3(row.Gamma)}); praful la L* {Fmt.F1(row.LstarDust)}, cerul la L* {Fmt.F1(row.LstarSky)}; "
                        + $"negrul hartiei e L* {Fmt.F1(p.PaperBlackLstar)} la Dmax {Fmt.Dmax(r.Parameters.Dmax)}")
                    + ReportWriter.RowMark(row).Replace("   ", "; "),
                BlackLegend = Tr.Pick($"under paper black (L* {Fmt.F1(p.PaperBlackLstar)}): {Fmt.Pct(p.PctUnderPaperBlack)} of the {Thresholds.DustPatchPx} px patches - they print as the same black", $"sub negrul hartiei (L* {Fmt.F1(p.PaperBlackLstar)}): {Fmt.Pct(p.PctUnderPaperBlack)} din dalele de {Thresholds.DustPatchPx} px - se tiparesc cu acelasi negru"),
                LowLegend = Tr.Pick($"between paper black and L* {Fmt.F0(Thresholds.LowSeparationLstar)}: {Fmt.Pct(p.PctUnderLowSeparation - p.PctUnderPaperBlack)} of the {Thresholds.DustPatchPx} px patches - the paper tells them apart, but the eye barely sees the difference", $"intre negrul hartiei si L* {Fmt.F0(Thresholds.LowSeparationLstar)}: {Fmt.Pct(p.PctUnderLowSeparation - p.PctUnderPaperBlack)} din dalele de {Thresholds.DustPatchPx} px - pe hartie se deosebesc, dar ochiul abia le vede diferenta"),
                Note = Tr.Pick("A reduced copy, with the tones lifted by a simple curve (gamma), not by the stretch in Siril. On screen it looks washed out, but on paper it comes back. The program assumes the pixels are sRGB. "
                        + $"The colours cover {Thresholds.DustPatchPx} px patches, the ones the table counts, not pixels: a stray darker pixel does not show on paper.",
                        "E o copie micsorata, cu tonurile ridicate printr-o curba simpla (gamma), nu prin stretch-ul din Siril. Pe ecran pare stearsa, dar pe hartie revine. Programul presupune ca pixelii sunt sRGB. "
                        + $"Culorile acopera dale de {Thresholds.DustPatchPx} px, cele pe care le numara tabelul, nu pixeli: un pixel razlet mai intunecat nu se vede pe hartie."),
            };
        }
    }
}
