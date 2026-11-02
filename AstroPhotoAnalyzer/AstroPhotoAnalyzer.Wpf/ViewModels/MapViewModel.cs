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

// MapViewModel.cs - the "Harta praf / cer" window (R-130): the whole image, with the five
// classes of the patches (dust, sky, band or nebula, bright clouds or bright nebula, peaks;
// ADR 0026, ADR 0033) painted differently, each with its tick, and the catalogued clouds
// circled, with their names.
//
// It computes nothing: the classes of the patches, each class's share of them
// (DustMap.FractionOf, the figure the report's class fractions use), the circles and the names
// come from the core's result (DustAnalyzer, PrintStretchSimulator); here they are laid on a
// reduced copy of the image, and the user is let to switch the layers off, the clouds all
// together or one by one.

using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// A layer of the map: one class of patches, with its colour, its legend and its tick.
    /// </summary>
    public sealed class ClassLayer : ObservableObject
    {
        bool _visible = true;

        public string Name { get; init; }
        public string Legend { get; init; }
        public BitmapSource Layer { get; init; }
        public Brush Swatch { get; init; }
        public bool IsVisible { get => _visible; set => Set(ref _visible, value); }
    }

    /// <summary>
    /// The map window's state (R-130): the reduced image, the five class layers, the catalogued
    /// clouds with their switches, and the sentences under the map.
    /// </summary>
    public sealed class MapViewModel : ObservableObject
    {
        // The classes' colours, with alpha, in the order of the core's names (DustMap.ClassNames):
        // the dust warm, the sky cold, the band / nebula green, the bright clouds / bright nebula
        // pink, the peaks white.
        static readonly (PatchClass Class, Color Colour)[] Palette =
        {
            (PatchClass.Dust, Color.FromArgb(130, 0xE3, 0xB3, 0x41)),
            (PatchClass.Sky, Color.FromArgb(80, 0x4E, 0xA8, 0xDE)),
            (PatchClass.Band, Color.FromArgb(70, 0x78, 0xDC, 0x78)),
            (PatchClass.BrightCloud, Color.FromArgb(100, 0xFF, 0x78, 0xFF)),
            (PatchClass.Peak, Color.FromArgb(160, 0xFF, 0xFF, 0xFF)),
        };

        bool _showClouds;

        public string Title { get; private init; }
        public BitmapSource Image { get; private init; }

        /// <summary>
        /// The five classes, in the palette's order, named for their path (the band or the modelled
        /// sky).
        /// </summary>
        public IReadOnlyList<ClassLayer> Layers { get; private init; }
        public IReadOnlyList<CloudLayer> Clouds { get; private init; }

        /// <summary>
        /// The width and the height of the reduced image: the size of the canvas everything is
        /// drawn on.
        /// </summary>
        public int Width { get; private init; }
        public int Height { get; private init; }

        /// <summary>
        /// Without a WCS there are no clouds to show: the clouds' tick is disabled and off.
        /// </summary>
        public bool HasWcs { get; private init; }

        /// <summary>
        /// The sentence that defines the dust and the sky, the same as under the simulation table.
        /// </summary>
        public string Definition { get; private init; }

        /// <summary>The red sentence without a WCS; null with one.</summary>
        public string NoWcsWarning { get; private init; }

        /// <summary>The clouds, circles and labels alike; off, their list is disabled.</summary>
        public bool ShowClouds { get => _showClouds; set => Set(ref _showClouds, value); }

        /// <summary>
        /// Every cloud visible, respectively none: the ticks in the list, not the layer.
        /// </summary>
        public ICommand SelectAllClouds { get; }
        public ICommand DeselectAllClouds { get; }

        /// <summary>
        /// Makes the two cloud commands; only <see cref="From"/> builds a complete one.
        /// </summary>
        MapViewModel()
        {
            SelectAllClouds = new RelayCommand(() => { foreach (CloudLayer c in Clouds) c.IsVisible = true; });
            DeselectAllClouds = new RelayCommand(() => { foreach (CloudLayer c in Clouds) c.IsVisible = false; });
        }

        /// <summary>
        /// Builds the map of an analysis: the image reduced by the preview's step, one overlay
        /// per class from the patch under each reduced pixel, one <see cref="CloudLayer"/> per
        /// cloud row, in reduced pixels.
        /// </summary>
        /// <param name="r">The analysis; its raster, simulation and path are read.</param>
        /// <returns>
        /// The view-model, every layer visible, the clouds shown when there is a WCS and a row.
        /// </returns>
        internal static MapViewModel From(AnalysisResult r)
        {
            SimulationResult s = r.Simulation;
            DustMap map = s.Map;
            Raster img = r.Raster;
            // The same step as PrintPreview, so that the patches fall on the reduced image's
            // pixels.
            int stride = PrintPreview.ReductionStride(img);
            BitmapSource image = RasterBitmaps.Region(img, 0, 0, img.W, img.H, stride);
            int w = image.PixelWidth, h = image.PixelHeight;

            PatchClass[] classes = map.Classes;
            IReadOnlyList<string> names = map.ClassNames;
            double patchPx = map.Side * map.Stride;
            PatchClass ClassAt(int x, int y)
            {
                int bx = (int)(x * stride / patchPx), by = (int)(y * stride / patchPx);
                return bx < map.Cols && by < map.Rows ? classes[by * map.Cols + bx] : PatchClass.None;
            }

            var layers = new List<ClassLayer>();
            for (int k = 0; k < Palette.Length; k++)
            {
                var (cls, colour) = Palette[k];
                string name = names[k];
                double pct = 100 * map.FractionOf(cls);
                layers.Add(new ClassLayer
                {
                    Name = name,
                    Legend = Tr.Pick($"{name}: {Fmt.Pct(pct)} of the patches", $"{name}: {Fmt.Pct(pct)} din dale") + LegendOf(cls, s),
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
                    Caption = Tr.Pick($"{c.Name}  L* {Fmt.F1(c.Lstar)}  target {ReportWriter.TargetText(c.ExactTarget)}  ({Fmt.Int(c.Patches)} patches)", $"{c.Name}  L* {Fmt.F1(c.Lstar)}  tinta {ReportWriter.TargetText(c.ExactTarget)}  ({Fmt.Int(c.Patches)} dale)"),
                    CentreX = cloud.CentreX / stride,
                    CentreY = cloud.CentreY / stride,
                    Radius = cloud.RadiusPx / stride,
                    Labelled = labelled.Contains(c.MapIndex),
                };
            }).ToList();

            bool hasWcs = img.Source?.Wcs != null;
            return new MapViewModel
            {
                Title = Tr.Pick($"Dust / sky map - {System.IO.Path.GetFileName(r.Path)}", $"Harta praf / cer - {System.IO.Path.GetFileName(r.Path)}"),
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

        /// <summary>
        /// What each class means, with the figures from Thresholds, from the map and from the
        /// result; explanation, not diagnosis. The dust and the sky also say what gave the
        /// verdict (ADR 0033).
        /// </summary>
        /// <param name="cls">The class; None gives an empty string.</param>
        /// <param name="s">The simulation, for the map and the as-is row.</param>
        /// <returns>The legend's tail, starting with " - ", to append after the share.</returns>
        static string LegendOf(PatchClass cls, SimulationResult s)
        {
            SimulationRow asIs = s.AsIs;
            DustMap map = s.Map;
            bool band = map.Method == DustMethod.Band;
            string dustVerdict = map.DustSource == DustSource.DustClass
                ? Tr.Pick($"; the dust's L*: {Fmt.F1(asIs.LstarDust)}, the threshold under which {Fmt.F0(Thresholds.DustClassFraction * 100)} % of them lie", $"; L*-ul prafului: {Fmt.F1(asIs.LstarDust)}, pragul sub care stau {Fmt.F0(Thresholds.DustClassFraction * 100)} % din ele")
                : Tr.Pick($"; the dust's L*: {Fmt.F1(asIs.LstarDust)}, the darkest {Fmt.F0(Thresholds.SkyDarkestFraction * 100)} % of the sky", $"; L*-ul prafului: {Fmt.F1(asIs.LstarDust)}, cel mai intunecat {Fmt.F0(Thresholds.SkyDarkestFraction * 100)} % din cer")
                  + (map.FractionOf(PatchClass.Dust) == 0 ? Tr.Pick(" (no dust patch)", " (nicio dala de praf)") : Tr.Pick($" (the dust patches are used only when they are at least {Fmt.F0(Thresholds.DustClassMinFraction * 100)} % of all and give a darker dust)", $" (dalele de praf se folosesc doar cand sunt cel putin {Fmt.F0(Thresholds.DustClassMinFraction * 100)} % din toate si dau un praf mai intunecat)"));
            return cls switch
            {
                PatchClass.Dust => " - " + s.DustClassDefinition + dustVerdict,
                PatchClass.Sky => (band
                    ? Tr.Pick($" - the sky outside the Milky Way band, over {Fmt.F0(Thresholds.BandGalacticLatitudeDeg)} degrees from it, without dust", $" - cerul din afara benzii Caii Lactee, la peste {Fmt.F0(Thresholds.BandGalacticLatitudeDeg)} grade de ea, fara praf")
                    : Tr.Pick($" - the patches that differ from the sky by at most {Fmt.F1(map.NoiseThreshold)} L*, lighter or darker", $" - dalele care difera de cer cu cel mult {Fmt.F1(map.NoiseThreshold)} L*, mai luminoase sau mai intunecate"))
                    + (map.SkyClassEmpty ? Tr.Pick($"; there is no sky patch: the sky's L* is the middle value of all the patches, {Fmt.F1(asIs.LstarSky)}", $"; nu e nicio dala de cer: L*-ul cerului e valoarea din mijloc a tuturor dalelor, {Fmt.F1(asIs.LstarSky)}") : Tr.Pick($"; the sky's L*: {Fmt.F1(asIs.LstarSky)}, the middle value of these patches", $"; L*-ul cerului: {Fmt.F1(asIs.LstarSky)}, valoarea din mijloc a acestor dale")),
                PatchClass.Band => band
                    ? Tr.Pick($" - in the Milky Way band (less than {Fmt.F0(Thresholds.BandGalacticLatitudeDeg)} degrees from it), without dust or bright clouds; or, outside it, {Fmt.F0(Thresholds.BrightCloudLstar)} L* lighter than the sky", $" - in banda Caii Lactee (la mai putin de {Fmt.F0(Thresholds.BandGalacticLatitudeDeg)} grade de ea), fara praf si fara nori luminosi; sau, in afara ei, cu {Fmt.F0(Thresholds.BrightCloudLstar)} L* mai luminoase decat cerul")
                    : Tr.Pick($" - {Fmt.F1(map.NoiseThreshold)} L* lighter than the sky ({Fmt.F0(Thresholds.SkyNoiseK)} times its noise, which is {Fmt.F2(map.SkySigma)} L*)", $" - cu {Fmt.F1(map.NoiseThreshold)} L* mai luminoase decat cerul (de {Fmt.F0(Thresholds.SkyNoiseK)} ori zgomotul lui, care e de {Fmt.F2(map.SkySigma)} L*)"),
                PatchClass.BrightCloud => band
                    ? Tr.Pick($" - in the band, {Fmt.F0(Thresholds.BrightCloudLstar)} L* lighter than the band around them", $" - in banda, cu {Fmt.F0(Thresholds.BrightCloudLstar)} L* mai luminoase decat banda din jur")
                    : double.IsPositiveInfinity(map.BrightThreshold)
                        ? Tr.Pick(" - no patch: the nebula does not split clearly into faint and bright areas", " - nicio dala: nebuloasa nu se desparte clar in zone slabe si zone stralucitoare")
                        : Tr.Pick($" - {Fmt.F1(map.BrightThreshold)} L* lighter than the sky, the threshold at which the nebula splits into faint and bright areas", $" - cu {Fmt.F1(map.BrightThreshold)} L* mai luminoase decat cerul, pragul la care nebuloasa se desparte in zone slabe si stralucitoare"),
                PatchClass.Peak => Tr.Pick($" - the brightest {Fmt.F1(Thresholds.PeakFraction * 100)} % of the patches", $" - cele mai luminoase {Fmt.F1(Thresholds.PeakFraction * 100)} % din dale"),
                _ => "",
            };
        }
    }
}
