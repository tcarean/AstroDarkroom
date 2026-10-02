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

// MapViewModel.cs - fereastra "Harta praf / cer" (R-130): imaginea intreaga, cu cele cinci
// clase ale dalelor (praf, cer, banda, nori luminosi, varfuri; ADR 0026) vopsite diferit,
// fiecare cu bifa ei, si norii catalogati incercuiti, cu numele lor.
//
// Nu calculeaza nimic: clasele dalelor, cercurile si numele vin din rezultatul nucleului
// (DustAnalyzer, PrintStretchSimulator); aici se aseaza pe o copie redusa a imaginii si se
// lasa omul sa stinga straturile, norii toti sau unul cate unul.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>Un strat al hartii: o clasa de dale, cu culoarea, legenda si bifa ei.</summary>
    public sealed class ClassLayer : ObservableObject
    {
        bool _visible = true;

        public string Name { get; init; }
        public string Legend { get; init; }
        public BitmapSource Layer { get; init; }
        public Brush Swatch { get; init; }
        public bool IsVisible { get => _visible; set => Set(ref _visible, value); }
    }

    public sealed class MapViewModel : ObservableObject
    {
        // Culorile claselor, cu alpha: praful cald, cerul rece, banda verde, norii luminosi
        // roz, varfurile albe.
        static readonly (PatchClass Class, string Name, Color Colour)[] Palette =
        {
            (PatchClass.Dust, "praf", Color.FromArgb(130, 0xE3, 0xB3, 0x41)),
            (PatchClass.Sky, "cer", Color.FromArgb(80, 0x4E, 0xA8, 0xDE)),
            (PatchClass.Band, "banda", Color.FromArgb(70, 0x78, 0xDC, 0x78)),
            (PatchClass.BrightCloud, "nori luminosi", Color.FromArgb(100, 0xFF, 0x78, 0xFF)),
            (PatchClass.Peak, "varfuri", Color.FromArgb(160, 0xFF, 0xFF, 0xFF)),
        };

        bool _showClouds;

        public string Title { get; private init; }
        public BitmapSource Image { get; private init; }

        /// <summary>Cele cinci clase, in ordinea paletei; fara clase (fara WCS sau banda), doar praf si cer.</summary>
        public IReadOnlyList<ClassLayer> Layers { get; private init; }
        public IReadOnlyList<CloudLayer> Clouds { get; private init; }

        /// <summary>Latimea si inaltimea imaginii reduse: marimea panzei pe care se deseneaza totul.</summary>
        public int Width { get; private init; }
        public int Height { get; private init; }

        /// <summary>Fara WCS nu exista nori de aratat: bifa norilor e dezactivata si nebifata.</summary>
        public bool HasWcs { get; private init; }

        /// <summary>Propozitia care defineste praful si cerul, aceeasi ca sub tabelul simularii.</summary>
        public string Definition { get; private init; }

        /// <summary>Propozitia rosie fara WCS; null cu WCS.</summary>
        public string NoWcsWarning { get; private init; }

        /// <summary>Norii, cercuri si etichete deopotriva; nebifat, lista lor e dezactivata.</summary>
        public bool ShowClouds { get => _showClouds; set => Set(ref _showClouds, value); }

        /// <summary>Toti norii vizibili, respectiv niciunul: bifele din lista, nu stratul.</summary>
        public ICommand SelectAllClouds { get; }
        public ICommand DeselectAllClouds { get; }

        MapViewModel()
        {
            SelectAllClouds = new RelayCommand(() => { foreach (CloudLayer c in Clouds) c.IsVisible = true; });
            DeselectAllClouds = new RelayCommand(() => { foreach (CloudLayer c in Clouds) c.IsVisible = false; });
        }

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

            var layers = new List<ClassLayer>();
            foreach (var (cls, name, colour) in Palette)
            {
                if (!map.UsesClasses && cls != PatchClass.Dust && cls != PatchClass.Sky) continue;
                double pct = 100.0 * classes.Count(c => c == cls) / classes.Length;
                layers.Add(new ClassLayer
                {
                    Name = name,
                    Legend = $"{name}: {Fmt.Pct(pct)} din dale" + LegendOf(cls, s),
                    Layer = RasterBitmaps.Overlay(w, h, (x, y) => ClassAt(x, y) == cls, colour),
                    Swatch = new SolidColorBrush(Color.FromRgb(colour.R, colour.G, colour.B)),
                });
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
                Layers = layers,
                Clouds = clouds,
                HasWcs = hasWcs,
                _showClouds = hasWcs && clouds.Count > 0,
                Definition = ReportWriter.DustDefinition(s),
                NoWcsWarning = hasWcs ? null : TonesTabViewModel.NoWcsSentence,
            };
        }

        // Ce inseamna fiecare clasa, cu cifrele din Thresholds si din rezultat; explicatie, nu diagnostic.
        static string LegendOf(PatchClass cls, SimulationResult s)
        {
            SimulationRow asIs = s.AsIs;
            if (!s.Map.UsesClasses)
                return cls == PatchClass.Dust
                    ? $" - la sau sub L*-ul prafului ({Fmt.F1(asIs.LstarDust)}, cele mai intunecate {Fmt.F0(Thresholds.DustAreaFraction * 100)} %)"
                    : $" - la sau sub mediana ({Fmt.F1(asIs.LstarSky)})";
            return cls switch
            {
                PatchClass.Dust => $" - cu {Fmt.F0(Thresholds.DustDepthLstar)} L* sub nivelul local al benzii sau sub cerul modelat; L*-ul prafului = percentila {Fmt.F0(Thresholds.DustClassFraction * 100)} % a lor, {Fmt.F1(asIs.LstarDust)}",
                PatchClass.Sky => $" - in afara benzii (|b| peste {Fmt.F0(Thresholds.BandGalacticLatitudeDeg)} grade), fara praf; L*-ul cerului = mediana lor, {Fmt.F1(asIs.LstarSky)}",
                PatchClass.Band => $" - |b| sub {Fmt.F0(Thresholds.BandGalacticLatitudeDeg)} grade, nici praf, nici nor luminos; sau peste cerul modelat cu {Fmt.F0(Thresholds.BrightCloudLstar)} L* in afara ei",
                PatchClass.BrightCloud => $" - in banda, cu {Fmt.F0(Thresholds.BrightCloudLstar)} L* peste nivelul ei local",
                PatchClass.Peak => $" - cele mai luminoase {Fmt.F1(Thresholds.PeakFraction * 100)} % din dale",
                _ => "",
            };
        }
    }
}
