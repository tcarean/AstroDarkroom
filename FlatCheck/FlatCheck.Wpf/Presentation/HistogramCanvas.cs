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

// HistogramCanvas.cs - the drawing of one channel: two curves, the verdict's bands, the
// clipping.
//
// WHY IT IS DRAWN BY HAND. The dependency rule (docs/design/dependencies.md). A bar chart, five
// coloured zones and a vertical line are a page of DrawingContext; a charting package would be
// the project's third dependency, after libraw and ImageSharp, for this.
//
// WHAT IT DRAWS, in the order of the layers:
//   1. the verdict's zones - too low / somewhat low / target / somewhat high / too high
//   2. the clipping stripe - 98..100 % of white, where the analyzer counts the saturated pixels
//   3. the whole channel's curve, pale and wide (that IS the vignetting)
//   4. the central window's curve, solid - the window the verdict is taken from
//   5. the central median's line, with its value
//   6. the axis ticks
//
// IT DECIDES NOTHING. The thresholds come already chosen from ExposureBands through the
// view-model, and the curves come already normalised to their own peak. No figure a verdict
// could rest on is computed here - pixels are laid out.
//
// EVERY PROPERTY MUST SUPPORT ITS DEFAULT VALUE. The element exists in the template before any
// file has been chosen, so it is drawn with every property empty too: then only the axis
// shows. WindowSmokeTests renders exactly that state.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace FlatCheck.Wpf
{
    /// <summary>
    /// Draws one CFA channel's histogram on the Exposure tab: the verdict's zones, the
    /// clipping stripe, the whole and central curves, the median marker and the axis, from
    /// values the view-model hands in; computes nothing.
    /// </summary>
    public sealed class HistogramCanvas : FrameworkElement
    {
        // The chart's furniture, not the verdict palette: transparent over the card's
        // background, chosen to read as "zone", not as "result". The verdict pill above the
        // card stays the only place where colour means a judgement.
        //
        // Very pale, and that was measured by eye on renders: at twice the opacity, the red
        // zone under the threshold - which covers the whole first quarter of the axis -
        // floods the chart and makes it look like an alarm, even on a file with an OK
        // verdict. The zones are a landmark to read from the corner of the eye, and what must
        // be seen first is the CURVE.
        static readonly Brush TooLowZone = Frozen(Color.FromArgb(0x12, 0xF8, 0x51, 0x49));
        static readonly Brush ShoulderZone = Frozen(Color.FromArgb(0x12, 0xE3, 0xB3, 0x41));
        static readonly Brush TargetZone = Frozen(Color.FromArgb(0x20, 0x3F, 0xB9, 0x50));
        static readonly Brush ClipZone = Frozen(Color.FromArgb(0x38, 0xF8, 0x51, 0x49));
        static readonly Brush AxisBr = Frozen(Color.FromArgb(0x66, 0x6E, 0x6E, 0x77));
        static readonly Brush TickBr = Frozen(Color.FromRgb(0x6E, 0x6E, 0x77));
        static readonly Brush MarkerBr = Frozen(Color.FromRgb(0xE4, 0xE4, 0xE7));

        static readonly Typeface Face =
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        /// <summary>A frozen brush of a colour.</summary>
        /// <param name="c">The colour, alpha included.</param>
        /// <returns>A new frozen <see cref="SolidColorBrush"/>.</returns>
        static Brush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        /// <summary>Registers a dependency property of this element that redraws it when it
        /// changes.</summary>
        /// <typeparam name="T">The property's type.</typeparam>
        /// <param name="name">The CLR property's name.</param>
        /// <param name="def">The default value, which the drawing must support.</param>
        /// <returns>The registered property.</returns>
        static DependencyProperty Reg<T>(string name, T def) =>
            DependencyProperty.Register(name, typeof(T), typeof(HistogramCanvas),
                new FrameworkPropertyMetadata(def, FrameworkPropertyMetadataOptions.AffectsRender));

        // IReadOnlyList<double>, not double[]: a dependency property of ARRAY type cannot be
        // bound from a compiled DataTemplate - BAML tries to write a "property array" record
        // and the compilation fails with MC4102. The interface receives the same double[] from
        // the view-model, without conversion.

        /// <summary>The whole channel's curve, 0..1, already normalised to its own
        /// peak.</summary>
        public static readonly DependencyProperty WholeProperty = Reg<IReadOnlyList<double>>("Whole", null);

        /// <summary>The central window's curve, likewise.</summary>
        public static readonly DependencyProperty CentreProperty = Reg<IReadOnlyList<double>>("Centre", null);

        /// <summary>The channel's colour. The background curve uses it diluted.</summary>
        public static readonly DependencyProperty FillProperty = Reg<Brush>("Fill", null);

        /// <summary>The verdict's thresholds, in percent of white. NaN = the zone is
        /// missing.</summary>
        public static readonly DependencyProperty TooLowProperty = Reg("TooLow", double.NaN);
        public static readonly DependencyProperty BandLoProperty = Reg("BandLo", double.NaN);
        public static readonly DependencyProperty BandHiProperty = Reg("BandHi", double.NaN);
        public static readonly DependencyProperty TooHighProperty = Reg("TooHigh", double.NaN);

        /// <summary>From where upwards clipping is counted: 98 % of white, as in the
        /// analyzer.</summary>
        public static readonly DependencyProperty ClipFromProperty = Reg("ClipFrom", 98.0);

        /// <summary>The central window's median - the only figure that decides.</summary>
        public static readonly DependencyProperty MarkerProperty = Reg("Marker", double.NaN);

        /// <summary>The label written in the target band.</summary>
        public static readonly DependencyProperty BandLabelProperty = Reg<string>("BandLabel", null);

        public IReadOnlyList<double> Whole { get => (IReadOnlyList<double>)GetValue(WholeProperty); set => SetValue(WholeProperty, value); }
        public IReadOnlyList<double> Centre { get => (IReadOnlyList<double>)GetValue(CentreProperty); set => SetValue(CentreProperty, value); }
        public Brush Fill { get => (Brush)GetValue(FillProperty); set => SetValue(FillProperty, value); }
        public double TooLow { get => (double)GetValue(TooLowProperty); set => SetValue(TooLowProperty, value); }
        public double BandLo { get => (double)GetValue(BandLoProperty); set => SetValue(BandLoProperty, value); }
        public double BandHi { get => (double)GetValue(BandHiProperty); set => SetValue(BandHiProperty, value); }
        public double TooHigh { get => (double)GetValue(TooHighProperty); set => SetValue(TooHighProperty, value); }
        public double ClipFrom { get => (double)GetValue(ClipFromProperty); set => SetValue(ClipFromProperty, value); }
        public double Marker { get => (double)GetValue(MarkerProperty); set => SetValue(MarkerProperty, value); }
        public string BandLabel { get => (string)GetValue(BandLabelProperty); set => SetValue(BandLabelProperty, value); }

        const double AxisH = 14;    // the bottom band, for the ticks

        /// <summary>Draws the layers in order; draws nothing when the element is too small
        /// for the axis.</summary>
        /// <param name="dc">The drawing context WPF hands in.</param>
        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 1 || h <= AxisH + 2) return;

            double plotH = h - AxisH;

            // A transparent background, but drawn: without it the element gets no hit test
            // and, more importantly, the zones below would be the only thing that gives
            // height.
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));

            DrawZones(dc, w, plotH);
            DrawCurve(dc, Whole, w, plotH, Diluted(), null);
            DrawCurve(dc, Centre, w, plotH, Fill, Outline());
            DrawMarker(dc, w, plotH);
            DrawAxis(dc, w, plotH);
        }

        /// <summary>The horizontal position of a level.</summary>
        /// <param name="pct">The level, in percent of white (0..100).</param>
        /// <param name="w">The element's width, device-independent pixels.</param>
        /// <returns>The x coordinate.</returns>
        double X(double pct, double w) => pct / 100.0 * w;

        /// <summary>The background curve is the same colour, only pale: it is the same
        /// channel.</summary>
        /// <returns>A frozen brush at about 30 % opacity, or null when the fill is not a
        /// solid colour.</returns>
        Brush Diluted()
        {
            if (Fill is not SolidColorBrush s) return null;
            Color c = s.Color;
            var b = new SolidColorBrush(Color.FromArgb(0x4D, c.R, c.G, c.B));
            b.Freeze();
            return b;
        }

        /// <summary>The central curve's outline, in the fill's colour.</summary>
        /// <returns>A frozen 1-pixel pen, or null when the fill is not a solid
        /// colour.</returns>
        Pen Outline()
        {
            if (Fill is not SolidColorBrush s) return null;
            var p = new Pen(new SolidColorBrush(s.Color), 1);
            p.Freeze();
            return p;
        }

        /// <summary>Draws the five verdict zones, the clipping stripe over them and the target
        /// band's label; a zone with a NaN edge is left out.</summary>
        /// <param name="dc">The drawing context.</param>
        /// <param name="w">The plot's width.</param>
        /// <param name="h">The plot's height, without the axis band.</param>
        void DrawZones(DrawingContext dc, double w, double h)
        {
            void Zone(double from, double to, Brush b)
            {
                if (double.IsNaN(from) || double.IsNaN(to) || to <= from) return;
                dc.DrawRectangle(b, null, new Rect(X(from, w), 0, X(to, w) - X(from, w), h));
            }

            Zone(0, TooLow, TooLowZone);
            Zone(TooLow, BandLo, ShoulderZone);      // missing on the sky: TooLow == BandLo
            Zone(BandLo, BandHi, TargetZone);
            Zone(BandHi, TooHigh, ShoulderZone);
            Zone(TooHigh, 100, TooLowZone);

            // The clipping stripe over everything: there the verdict no longer depends on the
            // median.
            Zone(ClipFrom, 100, ClipZone);

            if (!string.IsNullOrEmpty(BandLabel) && !double.IsNaN(BandLo) && !double.IsNaN(BandHi))
            {
                var t = Text(BandLabel, 10, TickBr);
                double mid = (X(BandLo, w) + X(BandHi, w) - t.Width) / 2;
                // AT THE BOTTOM, not the top: the marker's label sits at the top, and the marker
                // falls right in the target band whenever the file is well exposed - that is,
                // exactly when the user looks most carefully. Overlapping, the two read
                // "51.6 %0-65 %".
                if (t.Width < X(BandHi, w) - X(BandLo, w))
                    dc.DrawText(t, new Point(mid, h - t.Height - 2));
            }
        }

        /// <summary>
        /// The curve, as a filled polygon. One bin = one column of width w/n, so no bin is lost
        /// and no points are invented between them: the histogram is discrete.
        /// </summary>
        /// <param name="dc">The drawing context.</param>
        /// <param name="bins">The bins, 0..1 (clamped; NaN as 0); null or empty draws
        /// nothing.</param>
        /// <param name="w">The plot's width.</param>
        /// <param name="h">The plot's height.</param>
        /// <param name="fill">The fill, or null.</param>
        /// <param name="pen">The outline, or null; with both null nothing is drawn.</param>
        static void DrawCurve(DrawingContext dc, IReadOnlyList<double> bins, double w, double h,
                              Brush fill, Pen pen)
        {
            if (bins == null || bins.Count == 0 || (fill == null && pen == null)) return;

            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                double step = w / bins.Count;
                g.BeginFigure(new Point(0, h), true, true);
                for (int i = 0; i < bins.Count; i++)
                {
                    double v = bins[i];
                    if (double.IsNaN(v) || v < 0) v = 0;
                    if (v > 1) v = 1;
                    double y = h - v * h;
                    g.LineTo(new Point(i * step, y), true, false);
                    g.LineTo(new Point((i + 1) * step, y), true, false);
                }
                g.LineTo(new Point(w, h), true, false);
            }
            geo.Freeze();
            dc.DrawGeometry(fill, pen, geo);
        }

        /// <summary>Draws the central median's line with its value; nothing for NaN or a level
        /// outside the axis.</summary>
        /// <param name="dc">The drawing context.</param>
        /// <param name="w">The plot's width.</param>
        /// <param name="h">The plot's height.</param>
        void DrawMarker(DrawingContext dc, double w, double h)
        {
            if (double.IsNaN(Marker)) return;

            double x = X(Marker, w);
            if (x < 0 || x > w) return;

            var pen = new Pen(MarkerBr, 1.5);
            pen.Freeze();
            dc.DrawLine(pen, new Point(x, 0), new Point(x, h));

            var t = Text(Marker.ToString("F1", CultureInfo.InvariantCulture) + " %", 11, MarkerBr);
            // The label moves to the left of the line when it would no longer fit on the right.
            double tx = x + 4 + t.Width <= w ? x + 4 : x - 4 - t.Width;
            dc.DrawText(t, new Point(Math.Max(0, tx), 1));
        }

        /// <summary>Draws the axis line and its ticks at 0, 25, 50, 75 and 100 %.</summary>
        /// <param name="dc">The drawing context.</param>
        /// <param name="w">The plot's width.</param>
        /// <param name="h">The plot's height; the axis is drawn at it.</param>
        void DrawAxis(DrawingContext dc, double w, double h)
        {
            var pen = new Pen(AxisBr, 1);
            pen.Freeze();
            dc.DrawLine(pen, new Point(0, h), new Point(w, h));

            foreach (double pct in new[] { 0.0, 25.0, 50.0, 75.0, 100.0 })
            {
                double x = X(pct, w);
                dc.DrawLine(pen, new Point(x, h), new Point(x, h + 3));

                var t = Text(pct.ToString("F0", CultureInfo.InvariantCulture), 10, TickBr);
                double tx = pct >= 100 ? x - t.Width : (pct <= 0 ? x : x - t.Width / 2);
                dc.DrawText(t, new Point(tx, h + 2));
            }

            // The unit is NOT written here. Wherever it would fit between the ticks it sticks to
            // one of them, and under the four drawings it stands once anyway, in the card's
            // text - where it is read once, not four times.
        }

        /// <summary>Lays out a short text in the element's font and DPI.</summary>
        /// <param name="s">The text.</param>
        /// <param name="size">The font size, device-independent pixels.</param>
        /// <param name="brush">The text's brush.</param>
        /// <returns>The formatted text, ready to draw.</returns>
        FormattedText Text(string s, double size, Brush brush) =>
            new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                              Face, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }
}
