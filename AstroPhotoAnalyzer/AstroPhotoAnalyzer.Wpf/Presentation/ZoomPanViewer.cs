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

// ZoomPanViewer.cs - a viewer that fits its child, zooms it with the wheel around the cursor and
// pans it with the left button (R-163): the soft proof's image in the tab and in the window.
//
// The state is the view's: the zoom relative to the fit (1 = the whole image in the viewport)
// and the point of the child at the viewport's centre, in the child's own units, so that a
// resize of the viewport, or a new image of the same size, keeps what was looked at. The zoom
// is clamped between the fit and MaxScale child units per device-independent pixel, the centre
// so that the child never leaves the viewport. The child is arranged at its natural size and
// drawn through a scale and a translation; nothing is computed about the image here.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// A viewer that fits its child, zooms it with the mouse wheel around the cursor and pans it
    /// by dragging with the left button; a double click fits it again (R-163).
    /// </summary>
    public sealed class ZoomPanViewer : Decorator
    {
        /// <summary>The largest scale: this many device-independent pixels per unit of the child.</summary>
        public const double MaxScale = 8;

        /// <summary>The zoom of one wheel notch, in or out.</summary>
        public const double WheelFactor = 1.25;

        readonly ScaleTransform _scale = new();
        readonly TranslateTransform _translate = new();

        double _zoom = 1;
        Point _centre;
        Size _childSize;
        bool _centred;
        Point _dragStart;
        Point _dragCentre;

        /// <summary>Clips to its bounds and listens to the mouse.</summary>
        public ZoomPanViewer()
        {
            ClipToBounds = true;
            Background = Brushes.Transparent;
            Focusable = false;
        }

        /// <summary>
        /// The brush behind the child; transparent by default, set so that the whole viewport
        /// takes the mouse, not only the child.
        /// </summary>
        public static readonly DependencyProperty BackgroundProperty =
            DependencyProperty.Register(nameof(Background), typeof(Brush), typeof(ZoomPanViewer), new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush Background
        {
            get => (Brush)GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }

        /// <summary>The zoom relative to the fit: 1 shows the whole child, 2 twice as large.</summary>
        public double Zoom => _zoom;

        /// <summary>The scale drawn: device-independent pixels per unit of the child.</summary>
        public double Scale => _scale.ScaleX;

        /// <summary>The point of the child, in its own units, at the viewport's centre.</summary>
        public Point Centre => _centre;

        /// <summary>The scale that fits the whole child in the viewport, 1 when either is empty.</summary>
        public double FitScale => FitScaleFor(RenderSize, _childSize);

        /// <summary>Zooms around a point of the viewport, keeping the child under it in place.</summary>
        /// <param name="at">The point, in the viewer's device-independent pixels.</param>
        /// <param name="factor">Over 1 zooms in, under 1 out; clamped to the fit and <see cref="MaxScale"/>.</param>
        public void ZoomAt(Point at, double factor)
        {
            double fit = FitScale;
            if (fit <= 0) return;
            double maxZoom = Math.Max(1, MaxScale / fit);
            double zoom = Math.Clamp(_zoom * factor, 1, maxZoom);
            if (zoom == _zoom) return;
            // The child point under the cursor before, kept under it after.
            Point under = ToChild(at);
            double scale = fit * zoom;
            _zoom = zoom;
            _centre = new Point(under.X + (RenderSize.Width / 2 - at.X) / scale, under.Y + (RenderSize.Height / 2 - at.Y) / scale);
            Apply();
        }

        /// <summary>Moves the view by a distance of the viewport.</summary>
        /// <param name="dx">The horizontal distance, in device-independent pixels; positive drags the child right.</param>
        /// <param name="dy">The vertical distance, in device-independent pixels; positive drags the child down.</param>
        public void PanBy(double dx, double dy)
        {
            double scale = Scale;
            if (scale <= 0) return;
            _centre = new Point(_centre.X - dx / scale, _centre.Y - dy / scale);
            Apply();
        }

        /// <summary>Fits the whole child in the viewport again.</summary>
        public void Reset()
        {
            _zoom = 1;
            _centre = new Point(_childSize.Width / 2, _childSize.Height / 2);
            Apply();
        }

        /// <summary>The point of the child under a point of the viewport.</summary>
        /// <param name="at">The point, in the viewer's device-independent pixels.</param>
        /// <returns>The point in the child's units.</returns>
        public Point ToChild(Point at)
        {
            double scale = Scale;
            return scale <= 0 ? at : new Point(_centre.X + (at.X - RenderSize.Width / 2) / scale, _centre.Y + (at.Y - RenderSize.Height / 2) / scale);
        }

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size constraint)
        {
            Child?.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            // The viewer takes what it is given; the child's size is its own.
            return new Size(double.IsInfinity(constraint.Width) ? Child?.DesiredSize.Width ?? 0 : constraint.Width,
                            double.IsInfinity(constraint.Height) ? Child?.DesiredSize.Height ?? 0 : constraint.Height);
        }

        /// <inheritdoc/>
        protected override Size ArrangeOverride(Size arrangeSize)
        {
            UIElement child = Child;
            if (child == null) return arrangeSize;
            Size size = child.DesiredSize;
            child.Arrange(new Rect(size));
            if (child.RenderTransform is not TransformGroup)
            {
                var group = new TransformGroup();
                group.Children.Add(_scale);
                group.Children.Add(_translate);
                child.RenderTransform = group;
            }
            // A child of another size is a new image: look at the whole of it.
            if (size != _childSize || !_centred)
            {
                _childSize = size;
                _centred = true;
                _zoom = 1;
                _centre = new Point(size.Width / 2, size.Height / 2);
            }
            Apply(arrangeSize);
            return arrangeSize;
        }

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            drawingContext.DrawRectangle(Background, null, new Rect(RenderSize));
        }

        /// <inheritdoc/>
        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            ZoomAt(e.GetPosition(this), e.Delta > 0 ? WheelFactor : 1 / WheelFactor);
            e.Handled = true;
        }

        /// <inheritdoc/>
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (e.ClickCount == 2) { Reset(); e.Handled = true; return; }
            _dragStart = e.GetPosition(this);
            _dragCentre = _centre;
            CaptureMouse();
            Cursor = Cursors.SizeAll;
            e.Handled = true;
        }

        /// <inheritdoc/>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!IsMouseCaptured) return;
            Point now = e.GetPosition(this);
            double scale = Scale;
            if (scale <= 0) return;
            _centre = new Point(_dragCentre.X - (now.X - _dragStart.X) / scale, _dragCentre.Y - (now.Y - _dragStart.Y) / scale);
            Apply();
        }

        /// <inheritdoc/>
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (!IsMouseCaptured) return;
            ReleaseMouseCapture();
            Cursor = null;
            e.Handled = true;
        }

        /// <inheritdoc/>
        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            Cursor = null;
        }

        /// <summary>Clamps the centre and sets the two transforms for the current viewport.</summary>
        void Apply() => Apply(RenderSize);

        /// <summary>Clamps the centre and sets the two transforms for a viewport of the given size.</summary>
        /// <param name="viewport">The viewport's size, in device-independent pixels.</param>
        void Apply(Size viewport)
        {
            double fit = FitScaleFor(viewport, _childSize);
            double scale = fit * _zoom;
            if (scale <= 0 || double.IsInfinity(scale)) return;
            // The child never leaves the viewport: along an axis where it is larger, its edges
            // stay outside or on the viewport's; where it is smaller, it sits in the middle.
            _centre = new Point(ClampCentre(_centre.X, _childSize.Width, viewport.Width / scale), ClampCentre(_centre.Y, _childSize.Height, viewport.Height / scale));
            _scale.ScaleX = _scale.ScaleY = scale;
            _translate.X = viewport.Width / 2 - _centre.X * scale;
            _translate.Y = viewport.Height / 2 - _centre.Y * scale;
        }

        /// <summary>The centre along one axis, clamped as <see cref="Apply(Size)"/> says.</summary>
        /// <param name="centre">The wanted centre, in child units.</param>
        /// <param name="child">The child's extent along the axis, in child units.</param>
        /// <param name="viewport">The viewport's extent along the axis, in child units.</param>
        /// <returns>The clamped centre.</returns>
        static double ClampCentre(double centre, double child, double viewport)
            => viewport >= child ? child / 2 : Math.Clamp(centre, viewport / 2, child - viewport / 2);

        /// <summary>The scale that fits a child in a viewport, both axes; 1 when either is empty.</summary>
        /// <param name="viewport">The viewport's size.</param>
        /// <param name="child">The child's size.</param>
        /// <returns>The scale, positive.</returns>
        static double FitScaleFor(Size viewport, Size child)
        {
            if (child.Width <= 0 || child.Height <= 0 || viewport.Width <= 0 || viewport.Height <= 0) return 1;
            return Math.Min(viewport.Width / child.Width, viewport.Height / child.Height);
        }
    }
}
