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

// HistogramCanvas.cs - desenul unui canal: doua curbe, benzile verdictului, clipping-ul.
//
// DE CE E DESENAT DE MANA. Regula celei unei singure dependinte. Un grafic cu bare, cinci
// zone colorate si o linie verticala e o pagina de DrawingContext; un pachet de charting
// ar fi a doua dependinta a proiectului, pentru asta.
//
// CE DESENEAZA, in ordinea straturilor:
//   1. zonele verdictului  - prea jos / cam jos / tinta / cam sus / prea sus
//   2. dunga de clipping   - 98..100 % din white, unde analizorul numara pixelii saturati
//   3. curba canalului intreg, palida si lata (aia E vignetarea)
//   4. curba ferestrei centrale, plina - fereastra din care se ia verdictul
//   5. linia medianei centrale, cu valoarea ei
//   6. gradatiile axei
//
// NU DECIDE NIMIC. Pragurile vin gata alese din ExposureBands prin ViewModel, iar curbele
// vin deja normalizate la propriul varf. Aici nu se calculeaza nicio cifra pe care sa se
// poata sprijini un verdict - se aseaza pixeli.
//
// FIECARE PROPRIETATE TREBUIE SA SUPORTE VALOAREA IMPLICITA. Elementul exista in sablon
// inainte de a fi ales vreun fisier, deci se deseneaza si cu toate proprietatile goale:
// atunci nu apare decat axa. WindowSmokeTests randeaza chiar starea aia.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace FlatCheck.Wpf
{
    public sealed class HistogramCanvas : FrameworkElement
    {
        // Mobila graficului, nu paleta de verdict: transparente peste fundalul cardului,
        // alese sa se citeasca drept "zona", nu drept "rezultat". Pastila de verdict de
        // deasupra cardului ramane singurul loc unde culoarea inseamna o judecata.
        //
        // Foarte palide, si asta a fost masurat cu ochiul pe randari: la o transparenta
        // de doua ori mai mare, zona rosie de sub prag - care acopera tot primul sfert al
        // axei - inunda graficul si il face sa arate ca o alarma, chiar pe un fisier cu
        // verdict OK. Zonele sunt un reper de citit din coada ochiului, iar ce trebuie sa
        // se vada intai e CURBA.
        static readonly Brush TooLowZone = Frozen(Color.FromArgb(0x12, 0xF8, 0x51, 0x49));
        static readonly Brush ShoulderZone = Frozen(Color.FromArgb(0x12, 0xE3, 0xB3, 0x41));
        static readonly Brush TargetZone = Frozen(Color.FromArgb(0x20, 0x3F, 0xB9, 0x50));
        static readonly Brush ClipZone = Frozen(Color.FromArgb(0x38, 0xF8, 0x51, 0x49));
        static readonly Brush AxisBr = Frozen(Color.FromArgb(0x66, 0x6E, 0x6E, 0x77));
        static readonly Brush TickBr = Frozen(Color.FromRgb(0x6E, 0x6E, 0x77));
        static readonly Brush MarkerBr = Frozen(Color.FromRgb(0xE4, 0xE4, 0xE7));

        static readonly Typeface Face =
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        static Brush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        static DependencyProperty Reg<T>(string name, T def) =>
            DependencyProperty.Register(name, typeof(T), typeof(HistogramCanvas),
                new FrameworkPropertyMetadata(def, FrameworkPropertyMetadataOptions.AffectsRender));

        // IReadOnlyList<double>, nu double[]: o proprietate de dependinta de tip TABLOU
        // nu se poate lega dintr-un DataTemplate compilat - BAML incearca sa scrie un
        // record de "property array" si compilarea cade cu MC4102. Interfata primeste
        // acelasi double[] de la ViewModel, fara conversie.

        /// <summary>Curba canalului intreg, 0..1, deja normalizata la propriul varf.</summary>
        public static readonly DependencyProperty WholeProperty = Reg<IReadOnlyList<double>>("Whole", null);

        /// <summary>Curba ferestrei centrale, la fel.</summary>
        public static readonly DependencyProperty CentreProperty = Reg<IReadOnlyList<double>>("Centre", null);

        /// <summary>Culoarea canalului. Curba din fundal o foloseste diluata.</summary>
        public static readonly DependencyProperty FillProperty = Reg<Brush>("Fill", null);

        /// <summary>Pragurile verdictului, in procente din white. NaN = zona lipseste.</summary>
        public static readonly DependencyProperty TooLowProperty = Reg("TooLow", double.NaN);
        public static readonly DependencyProperty BandLoProperty = Reg("BandLo", double.NaN);
        public static readonly DependencyProperty BandHiProperty = Reg("BandHi", double.NaN);
        public static readonly DependencyProperty TooHighProperty = Reg("TooHigh", double.NaN);

        /// <summary>De unde in sus se numara clipping: 98 % din white, ca in analizor.</summary>
        public static readonly DependencyProperty ClipFromProperty = Reg("ClipFrom", 98.0);

        /// <summary>Mediana ferestrei centrale - singura cifra care decide.</summary>
        public static readonly DependencyProperty MarkerProperty = Reg("Marker", double.NaN);

        /// <summary>Eticheta scrisa in banda tinta.</summary>
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

        const double AxisH = 14;    // banda de jos, pentru gradatii

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 1 || h <= AxisH + 2) return;

            double plotH = h - AxisH;

            // Fundal transparent, dar desenat: fara el elementul nu primeste hit-test si,
            // mai important, zonele de mai jos ar fi singurul lucru care da inaltime.
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));

            DrawZones(dc, w, plotH);
            DrawCurve(dc, Whole, w, plotH, Diluted(), null);
            DrawCurve(dc, Centre, w, plotH, Fill, Outline());
            DrawMarker(dc, w, plotH);
            DrawAxis(dc, w, plotH);
        }

        double X(double pct, double w) => pct / 100.0 * w;

        /// <summary>Curba din fundal e aceeasi culoare, doar palida: e acelasi canal.</summary>
        Brush Diluted()
        {
            if (Fill is not SolidColorBrush s) return null;
            Color c = s.Color;
            var b = new SolidColorBrush(Color.FromArgb(0x4D, c.R, c.G, c.B));
            b.Freeze();
            return b;
        }

        Pen Outline()
        {
            if (Fill is not SolidColorBrush s) return null;
            var p = new Pen(new SolidColorBrush(s.Color), 1);
            p.Freeze();
            return p;
        }

        void DrawZones(DrawingContext dc, double w, double h)
        {
            void Zone(double from, double to, Brush b)
            {
                if (double.IsNaN(from) || double.IsNaN(to) || to <= from) return;
                dc.DrawRectangle(b, null, new Rect(X(from, w), 0, X(to, w) - X(from, w), h));
            }

            Zone(0, TooLow, TooLowZone);
            Zone(TooLow, BandLo, ShoulderZone);      // lipseste pe cer: TooLow == BandLo
            Zone(BandLo, BandHi, TargetZone);
            Zone(BandHi, TooHigh, ShoulderZone);
            Zone(TooHigh, 100, TooLowZone);

            // Dunga de clipping peste toate: acolo verdictul nu mai depinde de mediana.
            Zone(ClipFrom, 100, ClipZone);

            if (!string.IsNullOrEmpty(BandLabel) && !double.IsNaN(BandLo) && !double.IsNaN(BandHi))
            {
                var t = Text(BandLabel, 10, TickBr);
                double mid = (X(BandLo, w) + X(BandHi, w) - t.Width) / 2;
                // JOS, nu sus: sus sta eticheta marcatorului, iar marcatorul cade chiar in
                // banda tinta ori de cate ori fisierul e bine expus - adica exact cand se
                // uita omul mai atent. Suprapuse, cele doua se citeau "51.6 %0-65 %".
                if (t.Width < X(BandHi, w) - X(BandLo, w))
                    dc.DrawText(t, new Point(mid, h - t.Height - 2));
            }
        }

        /// <summary>
        /// Curba, ca poligon plin. Un cos = o coloana de latimea w/n, deci nu se pierde
        /// niciun cos si nu se inventeaza puncte intre ele: histograma e discreta.
        /// </summary>
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

        void DrawMarker(DrawingContext dc, double w, double h)
        {
            if (double.IsNaN(Marker)) return;

            double x = X(Marker, w);
            if (x < 0 || x > w) return;

            var pen = new Pen(MarkerBr, 1.5);
            pen.Freeze();
            dc.DrawLine(pen, new Point(x, 0), new Point(x, h));

            var t = Text(Marker.ToString("F1", CultureInfo.InvariantCulture) + " %", 11, MarkerBr);
            // Eticheta se muta in stanga liniei cand n-ar mai incapea in dreapta.
            double tx = x + 4 + t.Width <= w ? x + 4 : x - 4 - t.Width;
            dc.DrawText(t, new Point(Math.Max(0, tx), 1));
        }

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

            // Unitatea NU se scrie aici. Oriunde ar incapea intre gradatii se lipeste de
            // una din ele, iar sub cele patru desene sta oricum o singura data, in textul
            // cardului - unde e citita o data, nu de patru ori.
        }

        FormattedText Text(string s, double size, Brush brush) =>
            new FormattedText(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                              Face, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }
}
