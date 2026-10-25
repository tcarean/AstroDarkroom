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

// ZoomableImage.xaml.cs - the interaction with the picture: zoom, panning, keys.
//
// WHAT BELONGS TO THE CONTROL AND WHAT DOES NOT. The state (fit or a factor) is
// ImageZoomViewModel's, bound through IsFit / Zoom; the control reads it and writes it back
// when the user turns the wheel or double-clicks. What it computes by itself is the
// EFFECTIVE factor - on "fit" it depends on the window's size, which only the control
// knows - and it writes it in EffectiveZoom, so that the label above can show it. Panning
// has no state: it is the position of the bars.
//
// At a factor >= 1 the pixels are drawn as crisp squares (NearestNeighbor), below 1 they are
// smoothed (HighQuality): what is magnified is exactly what was computed, and on a mosaic
// the R/G/B chessboard shows at 100 % exactly as in Siril.
//
// THE WHEEL ZOOMS, without a modifier, as in Siril's viewer. The first version asked for
// Ctrl so that the page could scroll over the picture too; in use the user wanted the
// picture to behave like a viewer. The page scrolls with the wheel anywhere outside the
// picture and with its bar over the picture. The cursor says when there is something to
// drag: a hand when the picture overflows the viewport, the arrow at "fit", when there is
// nothing to pan.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FlatCheck.Wpf
{
    /// <summary>
    /// A picture in a viewport with zoom about the cursor, panning by drag and keys (+, -, 0
    /// for fit, 1 for 100 %); the fit-or-factor state is bound to an
    /// <see cref="ImageZoomViewModel"/>, the factor applied is written back in
    /// <see cref="EffectiveZoom"/>.
    /// </summary>
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

        /// <summary>Loads the XAML and wires the mouse and keyboard to the scroll
        /// viewer.</summary>
        public ZoomableImage()
        {
            InitializeComponent();
            scroller.PreviewMouseWheel += OnWheel;
            scroller.PreviewMouseLeftButtonDown += OnMouseDown;
            scroller.PreviewMouseMove += OnMouseMove;
            scroller.PreviewMouseLeftButtonUp += OnMouseUp;
            scroller.MouseDoubleClick += OnDoubleClick;
            scroller.ScrollChanged += (_, _) => UpdateCursor();
            KeyDown += OnKey;
        }

        bool CanPan => scroller.ScrollableWidth > 0 || scroller.ScrollableHeight > 0;

        /// <summary>A hand when the picture can be panned, the default arrow otherwise;
        /// left alone during a drag.</summary>
        void UpdateCursor()
        {
            if (_dragging) return;
            scroller.Cursor = CanPan ? Cursors.Hand : null;
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

        /// <summary>
        /// The factor actually applied; written by the control, read by the label.
        /// </summary>
        public double EffectiveZoom
        {
            get => (double)GetValue(EffectiveZoomProperty);
            set => SetValue(EffectiveZoomProperty, value);
        }

        /// <summary>Shows the new picture and applies the current zoom to it.</summary>
        void OnSourceChanged()
        {
            image.Source = Source;
            Apply();
        }

        /// <summary>Recomputes the fit factor when the viewport changes size; a fixed factor
        /// stays.</summary>
        /// <param name="sender">The scroll viewer.</param>
        /// <param name="e">The size change.</param>
        void OnViewportSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (IsFit) Apply();
        }

        /// <summary>The picture's size in bitmap pixels.</summary>
        /// <returns>The width and height in pixels; device-independent units for a source
        /// that is not a bitmap, (0, 0) without a source.</returns>
        (double W, double H) PixelSize()
        {
            if (Source is BitmapSource b) return (b.PixelWidth, b.PixelHeight);
            return Source == null ? (0, 0) : (Source.Width, Source.Height);
        }

        /// <summary>The factor at which the whole picture fits the viewport.</summary>
        /// <returns>The factor, at most 1; 1 when the picture or the viewport has no size
        /// yet.</returns>
        double FitFactor()
        {
            var (w, h) = PixelSize();
            if (w <= 0 || h <= 0) return 1.0;
            // Before the first layout the viewport is 0; then the control's size is all that
            // is known, and it is enough for a first factor that SizeChanged redoes.
            double vw = scroller.ViewportWidth > 0 ? scroller.ViewportWidth : ActualWidth;
            double vh = scroller.ViewportHeight > 0 ? scroller.ViewportHeight : ActualHeight;
            if (vw <= 0 || vh <= 0) return 1.0;
            // "Fit" never magnifies past 100 %: a picture smaller than the viewport is shown at
            // its own size, not blown up.
            return Math.Min(1.0, Math.Min(vw / w, vh / h));
        }

        /// <summary>Puts the effective factor on the transform and announces it.</summary>
        void Apply()
        {
            double z = IsFit ? FitFactor() : Math.Clamp(Zoom, ImageZoomViewModel.Min, ImageZoomViewModel.Max);
            scale.ScaleX = z;
            scale.ScaleY = z;
            RenderOptions.SetBitmapScalingMode(image,
                z >= 1.0 ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
            SetCurrentValue(EffectiveZoomProperty, z);
            UpdateCursor();
        }

        /// <summary>
        /// Zoom about a point of the viewport: the point under the cursor stays under the
        /// cursor. offset' = (offset + p) * z' / z - p, on each axis. Leaves "fit".
        /// </summary>
        /// <param name="p">The point, in viewport coordinates.</param>
        /// <param name="newZoom">The new factor, clamped to the view-model's range.</param>
        void ZoomAbout(Point p, double newZoom)
        {
            double old = EffectiveZoom;
            newZoom = Math.Clamp(newZoom, ImageZoomViewModel.Min, ImageZoomViewModel.Max);
            if (old <= 0) old = newZoom;

            double cx = scroller.HorizontalOffset + p.X;
            double cy = scroller.VerticalOffset + p.Y;

            SetCurrentValue(ZoomProperty, newZoom);
            SetCurrentValue(IsFitProperty, false);
            scroller.UpdateLayout();   // the new extent must exist before ScrollTo

            scroller.ScrollToHorizontalOffset(cx * newZoom / old - p.X);
            scroller.ScrollToVerticalOffset(cy * newZoom / old - p.Y);
        }

        /// <summary>The wheel zooms by one step about the cursor.</summary>
        /// <param name="sender">The scroll viewer.</param>
        /// <param name="e">The wheel; marked handled.</param>
        void OnWheel(object sender, MouseWheelEventArgs e)
        {
            // Handled: neither this ScrollViewer nor the page's sees the wheel any more.
            double factor = e.Delta > 0 ? ImageZoomViewModel.Step : 1.0 / ImageZoomViewModel.Step;
            ZoomAbout(e.GetPosition(scroller), EffectiveZoom * factor);
            e.Handled = true;
        }

        /// <summary>Takes the focus for the keys and starts a drag when the picture can be
        /// panned.</summary>
        /// <param name="sender">The scroll viewer.</param>
        /// <param name="e">The button press.</param>
        void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            if (!CanPan) return;   // at "fit" there is nothing to drag; the double click stays
            _dragStart = e.GetPosition(scroller);
            _dragOffsetX = scroller.HorizontalOffset;
            _dragOffsetY = scroller.VerticalOffset;
            _dragging = true;
            scroller.Cursor = Cursors.SizeAll;
            scroller.CaptureMouse();
        }

        /// <summary>During a drag, moves the bars by the mouse's travel since the press.</summary>
        /// <param name="sender">The scroll viewer.</param>
        /// <param name="e">The move.</param>
        void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging) return;
            Point p = e.GetPosition(scroller);
            scroller.ScrollToHorizontalOffset(_dragOffsetX - (p.X - _dragStart.X));
            scroller.ScrollToVerticalOffset(_dragOffsetY - (p.Y - _dragStart.Y));
        }

        /// <summary>Ends a drag and releases the mouse.</summary>
        /// <param name="sender">The scroll viewer.</param>
        /// <param name="e">The button release.</param>
        void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            scroller.ReleaseMouseCapture();
            UpdateCursor();
        }

        /// <summary>Toggles between "fit" and 100 % about the clicked point.</summary>
        /// <param name="sender">The scroll viewer.</param>
        /// <param name="e">The double click; marked handled.</param>
        void OnDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (IsFit) ZoomAbout(e.GetPosition(scroller), 1.0);
            else SetCurrentValue(IsFitProperty, true);
            e.Handled = true;
        }

        /// <summary>+ and - zoom one step about the centre, 0 returns to "fit", 1 shows
        /// 100 %; other keys pass through.</summary>
        /// <param name="sender">The control.</param>
        /// <param name="e">The key; marked handled when it is one of these.</param>
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
