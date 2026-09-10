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

// ZoomableImage.xaml.cs - interactiunea cu poza: zoom, deplasare, taste.
//
// CE E AL CONTROLULUI SI CE NU. Starea (potrivit sau un factor) e a lui
// ImageZoomViewModel, legata prin IsFit / Zoom; controlul o citeste si o scrie inapoi
// cand omul roteste rotita sau da dublu click. Ce calculeaza el singur e factorul
// EFECTIV - pe "potrivit" depinde de marimea ferestrei, pe care numai el o stie - si il
// scrie in EffectiveZoom, ca eticheta de sus sa-l poata arata. Deplasarea nu are stare:
// e pozitia barelor.
//
// La factor >= 1 pixelii se deseneaza ca patrate crispe (NearestNeighbor), sub 1 se
// netezesc (HighQuality): ce se mareste e chiar ce s-a calculat, iar pe mozaic tabla
// de sah R/G/B se vede la 100 % exact ca in Siril.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlatCheck.Wpf
{
    public partial class ZoomableImage : UserControl
    {
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source), typeof(ImageSource), typeof(ZoomableImage),
            new PropertyMetadata(null, (d, _) => ((ZoomableImage)d).OnSourceChanged()));

        public static readonly DependencyProperty IsFitProperty = DependencyProperty.Register(
            nameof(IsFit), typeof(bool), typeof(ZoomableImage),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (d, _) => ((ZoomableImage)d).Apply()));

        public static readonly DependencyProperty ZoomProperty = DependencyProperty.Register(
            nameof(Zoom), typeof(double), typeof(ZoomableImage),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (d, _) => ((ZoomableImage)d).Apply()));

        public static readonly DependencyProperty EffectiveZoomProperty = DependencyProperty.Register(
            nameof(EffectiveZoom), typeof(double), typeof(ZoomableImage),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        Point _dragStart;
        double _dragOffsetX, _dragOffsetY;
        bool _dragging;

        public ZoomableImage()
        {
            InitializeComponent();
            scroller.PreviewMouseWheel += OnWheel;
            scroller.PreviewMouseLeftButtonDown += OnMouseDown;
            scroller.PreviewMouseMove += OnMouseMove;
            scroller.PreviewMouseLeftButtonUp += OnMouseUp;
            scroller.MouseDoubleClick += OnDoubleClick;
            KeyDown += OnKey;
        }

        public ImageSource Source
        {
            get => (ImageSource)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        public bool IsFit
        {
            get => (bool)GetValue(IsFitProperty);
            set => SetValue(IsFitProperty, value);
        }

        public double Zoom
        {
            get => (double)GetValue(ZoomProperty);
            set => SetValue(ZoomProperty, value);
        }

        /// <summary>Factorul chiar aplicat; scris de control, citit de eticheta.</summary>
        public double EffectiveZoom
        {
            get => (double)GetValue(EffectiveZoomProperty);
            set => SetValue(EffectiveZoomProperty, value);
        }

        void OnSourceChanged()
        {
            image.Source = Source;
            Apply();
        }

        void OnViewportSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (IsFit) Apply();
        }

        (double W, double H) PixelSize()
        {
            if (Source is BitmapSource b) return (b.PixelWidth, b.PixelHeight);
            return Source == null ? (0, 0) : (Source.Width, Source.Height);
        }

        double FitFactor()
        {
            var (w, h) = PixelSize();
            if (w <= 0 || h <= 0) return 1.0;
            // Inainte de prima asezare viewport-ul e 0; atunci marimea controlului e tot
            // ce se stie, si e de ajuns pentru un prim factor pe care SizeChanged il refac.
            double vw = scroller.ViewportWidth > 0 ? scroller.ViewportWidth : ActualWidth;
            double vh = scroller.ViewportHeight > 0 ? scroller.ViewportHeight : ActualHeight;
            if (vw <= 0 || vh <= 0) return 1.0;
            // "Potrivit" nu mareste niciodata peste 100 %: o poza mai mica decat viewport-ul
            // se arata la marimea ei, nu umflata.
            return Math.Min(1.0, Math.Min(vw / w, vh / h));
        }

        /// <summary>Pune factorul efectiv pe transformare si il anunta.</summary>
        void Apply()
        {
            double z = IsFit ? FitFactor() : Math.Clamp(Zoom, ImageZoomViewModel.Min, ImageZoomViewModel.Max);
            scale.ScaleX = z;
            scale.ScaleY = z;
            RenderOptions.SetBitmapScalingMode(image,
                z >= 1.0 ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
            SetCurrentValue(EffectiveZoomProperty, z);
        }

        /// <summary>
        /// Zoom in jurul unui punct din viewport: punctul de sub cursor ramane sub cursor.
        /// offset' = (offset + p) * z' / z - p, pe fiecare axa.
        /// </summary>
        void ZoomAbout(Point p, double newZoom)
        {
            double old = EffectiveZoom;
            newZoom = Math.Clamp(newZoom, ImageZoomViewModel.Min, ImageZoomViewModel.Max);
            if (old <= 0) old = newZoom;

            double cx = scroller.HorizontalOffset + p.X;
            double cy = scroller.VerticalOffset + p.Y;

            SetCurrentValue(ZoomProperty, newZoom);
            SetCurrentValue(IsFitProperty, false);
            scroller.UpdateLayout();   // intinderea noua trebuie sa existe inainte de ScrollTo

            scroller.ScrollToHorizontalOffset(cx * newZoom / old - p.X);
            scroller.ScrollToVerticalOffset(cy * newZoom / old - p.Y);
        }

        void OnWheel(object sender, MouseWheelEventArgs e)
        {
            // Fara Ctrl rotita ramane a paginii: tabul deruleaza in continuare peste poza.
            if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;

            double factor = e.Delta > 0 ? ImageZoomViewModel.Step : 1.0 / ImageZoomViewModel.Step;
            ZoomAbout(e.GetPosition(scroller), EffectiveZoom * factor);
            e.Handled = true;
        }

        void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            _dragStart = e.GetPosition(scroller);
            _dragOffsetX = scroller.HorizontalOffset;
            _dragOffsetY = scroller.VerticalOffset;
            _dragging = true;
            scroller.CaptureMouse();
        }

        void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            Point p = e.GetPosition(scroller);
            scroller.ScrollToHorizontalOffset(_dragOffsetX - (p.X - _dragStart.X));
            scroller.ScrollToVerticalOffset(_dragOffsetY - (p.Y - _dragStart.Y));
            scroller.Cursor = Cursors.SizeAll;
        }

        void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            scroller.ReleaseMouseCapture();
            scroller.Cursor = null;
        }

        void OnDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (IsFit) ZoomAbout(e.GetPosition(scroller), 1.0);
            else SetCurrentValue(IsFitProperty, true);
            e.Handled = true;
        }

        void OnKey(object sender, KeyEventArgs e)
        {
            Point centre = new(scroller.ViewportWidth / 2, scroller.ViewportHeight / 2);
            switch (e.Key)
            {
                case Key.OemPlus:
                case Key.Add:
                    ZoomAbout(centre, EffectiveZoom * ImageZoomViewModel.Step); break;
                case Key.OemMinus:
                case Key.Subtract:
                    ZoomAbout(centre, EffectiveZoom / ImageZoomViewModel.Step); break;
                case Key.D0:
                case Key.NumPad0:
                    SetCurrentValue(IsFitProperty, true); break;
                case Key.D1:
                case Key.NumPad1:
                    ZoomAbout(centre, 1.0); break;
                default:
                    return;
            }
            e.Handled = true;
        }
    }
}
