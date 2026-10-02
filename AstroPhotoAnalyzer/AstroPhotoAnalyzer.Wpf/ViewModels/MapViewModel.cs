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

// MapViewModel.cs - fereastra "Harta praf / cer" (R-130): imaginea intreaga, cu dalele
// care intra in statistica prafului si cele care intra in cea a cerului vopsite diferit,
// si norii catalogati incercuiti, cu numele lor.
//
// Nu calculeaza nimic: clasele dalelor, cercurile si numele vin din rezultatul nucleului
// (DustAnalyzer, PrintStretchSimulator); aici se aseaza pe o copie redusa a imaginii si se
// lasa omul sa stinga straturile.

using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AstroPhotoAnalyzer.Wpf
{
    public sealed class MapViewModel : ObservableObject
    {
        // Culorile straturilor: praful cald, cerul rece - cele doua ale temei, cu alpha.
        static readonly Color DustColour = Color.FromArgb(110, 0xE3, 0xB3, 0x41);
        static readonly Color SkyColour = Color.FromArgb(70, 0x4E, 0xA8, 0xDE);

        bool _showDust = true, _showSky = true, _showLabels;

        public string Title { get; private init; }
        public BitmapSource Image { get; private init; }
        public BitmapSource DustLayer { get; private init; }
        public BitmapSource SkyLayer { get; private init; }
        public IReadOnlyList<CloudLayer> Clouds { get; private init; }

        /// <summary>Latimea si inaltimea imaginii reduse: marimea panzei pe care se deseneaza totul.</summary>
        public int Width { get; private init; }
        public int Height { get; private init; }

        /// <summary>Fara WCS nu exista nori de etichetat: bifa e dezactivata si nebifata.</summary>
        public bool HasWcs { get; private init; }

        /// <summary>Propozitia care defineste praful si cerul, aceeasi ca sub tabelul simularii.</summary>
        public string Definition { get; private init; }

        /// <summary>Propozitia rosie fara WCS; null cu WCS.</summary>
        public string NoWcsWarning { get; private init; }

        public string DustLegend { get; private init; }
        public string SkyLegend { get; private init; }

        public bool ShowDust { get => _showDust; set => Set(ref _showDust, value); }
        public bool ShowSky { get => _showSky; set => Set(ref _showSky, value); }
        public bool ShowLabels { get => _showLabels; set => Set(ref _showLabels, value); }

        MapViewModel() { }

        internal static MapViewModel From(AnalysisResult r)
        {
            SimulationResult s = r.Simulation;
            DustMap map = s.Map;
            Raster img = r.Raster;
            int stride = RasterBitmaps.StrideFor(img, Thresholds.PreviewMaxSide);
            BitmapSource image = RasterBitmaps.Whole(img, Thresholds.PreviewMaxSide);
            int w = image.PixelWidth, h = image.PixelHeight;

            PatchClass[] classes = DustAnalyzer.Classes(map, s.AsIsDust);
            double patchPx = map.Side * map.Stride;
            PatchClass ClassAt(int x, int y)
            {
                int bx = (int)(x * stride / patchPx), by = (int)(y * stride / patchPx);
                return bx < map.Cols && by < map.Rows ? classes[by * map.Cols + bx] : PatchClass.None;
            }

            var labelled = new HashSet<int>(s.Clouds.Take(Thresholds.DustCloudReportRows).Select(c => c.MapIndex));
            var clouds = s.Clouds.Select(c =>
            {
                DustCloud cloud = map.Clouds[c.MapIndex];
                return new CloudLayer
                {
                    Name = c.Name,
                    Caption = $"{c.Name}  L* {Fmt.F1(c.Lstar)}  tinta {(c.ExactTarget.HasValue ? Fmt.Level(c.ExactTarget.Value) : "> " + Fmt.Level(Thresholds.ExactTargetHi))}  ({Fmt.Int(c.Patches)} dale)",
                    CentreX = cloud.CentreX / stride,
                    CentreY = cloud.CentreY / stride,
                    Radius = cloud.RadiusPx / stride,
                    Labelled = labelled.Contains(c.MapIndex),
                };
            }).ToList();

            bool hasWcs = img.Source?.Wcs != null;
            return new MapViewModel
            {
                Title = $"Harta praf / cer - {System.IO.Path.GetFileName(r.Path)}",
                Image = image,
                Width = w,
                Height = h,
                DustLayer = RasterBitmaps.Overlay(w, h, (x, y) => ClassAt(x, y) == PatchClass.Dust, DustColour),
                SkyLayer = RasterBitmaps.Overlay(w, h, (x, y) => ClassAt(x, y) == PatchClass.Sky, SkyColour),
                Clouds = clouds,
                HasWcs = hasWcs,
                _showLabels = hasWcs && clouds.Count > 0,
                Definition = ReportWriter.DustDefinition(s),
                NoWcsWarning = hasWcs ? null : TonesTabViewModel.NoWcsSentence,
                DustLegend = $"praf: dalele de {Thresholds.DustPatchPx} px din care se ia L*-ul prafului ({s.DustDescription})",
                SkyLegend = $"cer: dalele din care se ia L*-ul cerului ({s.SkyDescription})",
            };
        }
    }
}
