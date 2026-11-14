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

// CropViewer.cs - the image of the Crop with aspect ratio tab (R-183): the image fitted to the
// whole area, the crop rectangle over it with the part outside darkened and a handle at each
// corner, and the mouse turned into moves of that rectangle.
//
// It decides nothing about the rectangle: it draws the one it is given, in pixels of the full
// image, and says where the pointer drags it, in those same pixels; the core answers with the
// next rectangle (FrameCrop), which comes back through the bindings. What is the view's own is
// the placing of the image in the area and what a press grabs: a corner within HandleGrab of
// the pointer, otherwise the rectangle when the pointer is inside it. The three drag methods
// are internal and hold the whole rule, so the tests drive a drag without a mouse.

using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// Shows an image fitted to its area with a crop rectangle over it, and reports the drags
    /// that move the rectangle or resize it from a corner (R-183).
    /// </summary>
    public sealed class CropViewer : FrameworkElement
    {
        /// <summary>The side of a corner handle, in device-independent pixels.</summary>
        public const double HandleSize = 10;

        /// <summary>
        /// How far from a corner, on each axis, a press still grabs it, in device-independent
        /// pixels: more than the handle drawn, so that it is easy to hit.
        /// </summary>
        public const double HandleGrab = 12;

        /// <summary>What a press grabs.</summary>
        internal enum Grip
        {
            None,
            Move,
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight,
        }

        static readonly Brush Veil = Frozen(new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)));
        static readonly Pen Outline = Frozen(new Pen(Frozen(new SolidColorBrush(Color.FromArgb(200, 0, 0, 0))), 3));
        static readonly Pen Line = Frozen(new Pen(Brushes.White, 1));
        static readonly Pen HandleEdge = Frozen(new Pen(Brushes.Black, 1));

        Grip _grip;
        Point _dragStart;
        Vector _grabOffset;

        /// <summary>Draws the image smoothed: it is a reduced copy fitted to the area.</summary>
        public CropViewer()
        {
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
            Focusable = false;
        }

        /// <summary>Freezes a drawing resource, so that it is shared without a cost.</summary>
        /// <typeparam name="T">The resource's type.</typeparam>
        /// <param name="f">The resource.</param>
        /// <returns>The same resource, frozen.</returns>
        static T Frozen<T>(T f) where T : Freezable
        {
            f.Freeze();
            return f;
        }

        /// <summary>Registers a property whose change redraws the element.</summary>
        /// <typeparam name="T">The property's type.</typeparam>
        /// <param name="name">The property's name.</param>
        /// <returns>The dependency property.</returns>
        static DependencyProperty Drawn<T>(string name)
            => DependencyProperty.Register(name, typeof(T), typeof(CropViewer), new FrameworkPropertyMetadata(default(T), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty SourceProperty = Drawn<ImageSource>(nameof(Source));
        public static readonly DependencyProperty ImageWidthProperty = Drawn<int>(nameof(ImageWidth));
        public static readonly DependencyProperty ImageHeightProperty = Drawn<int>(nameof(ImageHeight));
        public static readonly DependencyProperty HasCropProperty = Drawn<bool>(nameof(HasCrop));
        public static readonly DependencyProperty CropXProperty = Drawn<int>(nameof(CropX));
        public static readonly DependencyProperty CropYProperty = Drawn<int>(nameof(CropY));
        public static readonly DependencyProperty CropWidthProperty = Drawn<int>(nameof(CropWidth));
        public static readonly DependencyProperty CropHeightProperty = Drawn<int>(nameof(CropHeight));

        /// <summary>The image shown, of any size: it is stretched over the full image's place.</summary>
        public ImageSource Source { get => (ImageSource)GetValue(SourceProperty); set => SetValue(SourceProperty, value); }

        /// <summary>The full image's width, in pixels: the unit of the rectangle and of the drags.</summary>
        public int ImageWidth { get => (int)GetValue(ImageWidthProperty); set => SetValue(ImageWidthProperty, value); }

        /// <summary>The full image's height, in pixels.</summary>
        public int ImageHeight { get => (int)GetValue(ImageHeightProperty); set => SetValue(ImageHeightProperty, value); }

        /// <summary>Whether there is a rectangle to draw and to drag.</summary>
        public bool HasCrop { get => (bool)GetValue(HasCropProperty); set => SetValue(HasCropProperty, value); }

        /// <summary>The rectangle's left column, in pixels of the full image.</summary>
        public int CropX { get => (int)GetValue(CropXProperty); set => SetValue(CropXProperty, value); }

        /// <summary>The rectangle's top row, in pixels of the full image.</summary>
        public int CropY { get => (int)GetValue(CropYProperty); set => SetValue(CropYProperty, value); }

        /// <summary>The rectangle's width, in pixels of the full image.</summary>
        public int CropWidth { get => (int)GetValue(CropWidthProperty); set => SetValue(CropWidthProperty, value); }

        /// <summary>The rectangle's height, in pixels of the full image.</summary>
        public int CropHeight { get => (int)GetValue(CropHeightProperty); set => SetValue(CropHeightProperty, value); }

        /// <summary>A press grabbed the rectangle or a corner: the drag that follows starts from the rectangle as it is now.</summary>
        internal event Action DragStarted;

        /// <summary>The rectangle is dragged: the distance from where the drag started, in pixels of the full image.</summary>
        internal event Action<int, int> MoveDragged;

        /// <summary>A corner is dragged: the corner, and where it is pulled to, in pixels of the full image from its top left.</summary>
        internal event Action<CropCorner, double, double> CornerDragged;

        /// <summary>The drag ended.</summary>
        internal event Action DragEnded;

        /// <summary>
        /// Where the image is drawn: fitted to the element, both axes, centred; empty without an
        /// image size or an area.
        /// </summary>
        internal Rect ImageArea
        {
            get
            {
                Size area = RenderSize;
                if (ImageWidth <= 0 || ImageHeight <= 0 || area.Width <= 0 || area.Height <= 0) return Rect.Empty;
                double scale = Math.Min(area.Width / ImageWidth, area.Height / ImageHeight);
                double w = ImageWidth * scale, h = ImageHeight * scale;
                return new Rect((area.Width - w) / 2, (area.Height - h) / 2, w, h);
            }
        }

        /// <summary>The device-independent pixels one pixel of the full image takes; 0 without an image.</summary>
        internal double PixelSize => ImageArea.IsEmpty ? 0 : ImageArea.Width / ImageWidth;

        /// <summary>Where the rectangle is drawn; empty without one.</summary>
        internal Rect CropArea
        {
            get
            {
                Rect image = ImageArea;
                if (!HasCrop || image.IsEmpty) return Rect.Empty;
                double s = PixelSize;
                return new Rect(image.X + CropX * s, image.Y + CropY * s, CropWidth * s, CropHeight * s);
            }
        }

        /// <inheritdoc/>
        protected override Size MeasureOverride(Size availableSize)
            // It takes the area it is given; with no limit on an axis, the image's own size.
            => new Size(double.IsInfinity(availableSize.Width) ? ImageWidth : availableSize.Width,
                        double.IsInfinity(availableSize.Height) ? ImageHeight : availableSize.Height);

        /// <inheritdoc/>
        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            // The whole area takes the mouse, not only what is drawn.
            drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
            Rect image = ImageArea;
            if (Source == null || image.IsEmpty) return;
            drawingContext.DrawImage(Source, image);
            Rect crop = CropArea;
            if (crop.IsEmpty) return;

            // What is left out is darkened; the rectangle has a white line on a dark one, so
            // that it shows on a bright nebula and on a dark sky alike.
            drawingContext.DrawGeometry(Veil, null, new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(image), new RectangleGeometry(crop)));
            drawingContext.DrawRectangle(null, Outline, crop);
            drawingContext.DrawRectangle(null, Line, crop);
            foreach (Point c in new[] { crop.TopLeft, crop.TopRight, crop.BottomLeft, crop.BottomRight })
                drawingContext.DrawRectangle(Brushes.White, HandleEdge, new Rect(c.X - HandleSize / 2, c.Y - HandleSize / 2, HandleSize, HandleSize));
        }

        /// <summary>
        /// What a press at a point grabs: the nearest corner within <see cref="HandleGrab"/> on
        /// both axes, otherwise the rectangle when the point is inside it, otherwise nothing.
        /// </summary>
        /// <param name="at">The point, in the element's device-independent pixels.</param>
        /// <returns>The grip.</returns>
        internal Grip HitTestGrip(Point at)
        {
            Rect crop = CropArea;
            if (crop.IsEmpty) return Grip.None;
            Grip best = Grip.None;
            double nearest = double.MaxValue;
            foreach ((Grip grip, Point corner) in new[] { (Grip.TopLeft, crop.TopLeft), (Grip.TopRight, crop.TopRight), (Grip.BottomLeft, crop.BottomLeft), (Grip.BottomRight, crop.BottomRight) })
            {
                double dx = Math.Abs(at.X - corner.X), dy = Math.Abs(at.Y - corner.Y);
                if (dx > HandleGrab || dy > HandleGrab || dx + dy >= nearest) continue;
                nearest = dx + dy;
                best = grip;
            }
            if (best != Grip.None) return best;
            return crop.Contains(at) ? Grip.Move : Grip.None;
        }

        /// <summary>Starts a drag at a point, when a press there grabs something.</summary>
        /// <param name="at">The point, in the element's device-independent pixels.</param>
        /// <returns>True when a drag started; false when the press grabs nothing.</returns>
        internal bool BeginDrag(Point at)
        {
            Grip grip = HitTestGrip(at);
            if (grip == Grip.None) return false;
            _grip = grip;
            _dragStart = at;
            // A corner keeps the distance from the pointer it was grabbed at, so that it does
            // not jump under the pointer.
            _grabOffset = grip == Grip.Move ? default : at - CornerPoint(grip, CropArea);
            DragStarted?.Invoke();
            return true;
        }

        /// <summary>Carries the drag in progress to a point; nothing without one.</summary>
        /// <param name="at">The point, in the element's device-independent pixels; it may be outside the image.</param>
        internal void DragTo(Point at)
        {
            double s = PixelSize;
            if (_grip == Grip.None || s <= 0) return;
            if (_grip == Grip.Move)
            {
                MoveDragged?.Invoke((int)Math.Round((at.X - _dragStart.X) / s), (int)Math.Round((at.Y - _dragStart.Y) / s));
                return;
            }
            Point corner = at - _grabOffset;
            Rect image = ImageArea;
            CornerDragged?.Invoke(CornerOf(_grip), (corner.X - image.X) / s, (corner.Y - image.Y) / s);
        }

        /// <summary>Ends the drag in progress; nothing without one.</summary>
        internal void EndDrag()
        {
            if (_grip == Grip.None) return;
            _grip = Grip.None;
            DragEnded?.Invoke();
        }

        /// <summary>The point of a corner of a rectangle.</summary>
        /// <param name="grip">A corner grip.</param>
        /// <param name="r">The rectangle.</param>
        /// <returns>The corner's point.</returns>
        static Point CornerPoint(Grip grip, Rect r) => grip switch
        {
            Grip.TopLeft => r.TopLeft,
            Grip.TopRight => r.TopRight,
            Grip.BottomLeft => r.BottomLeft,
            _ => r.BottomRight,
        };

        /// <summary>The core's name of a corner grip.</summary>
        /// <param name="grip">A corner grip.</param>
        /// <returns>The corner.</returns>
        static CropCorner CornerOf(Grip grip) => grip switch
        {
            Grip.TopLeft => CropCorner.TopLeft,
            Grip.TopRight => CropCorner.TopRight,
            Grip.BottomLeft => CropCorner.BottomLeft,
            _ => CropCorner.BottomRight,
        };

        /// <summary>The cursor that says what a press would grab.</summary>
        /// <param name="grip">The grip under the pointer.</param>
        /// <returns>The cursor; null for the default one.</returns>
        static Cursor CursorFor(Grip grip) => grip switch
        {
            Grip.Move => Cursors.SizeAll,
            Grip.TopLeft or Grip.BottomRight => Cursors.SizeNWSE,
            Grip.TopRight or Grip.BottomLeft => Cursors.SizeNESW,
            _ => null,
        };

        /// <inheritdoc/>
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (!BeginDrag(e.GetPosition(this))) return;
            CaptureMouse();
            e.Handled = true;
        }

        /// <inheritdoc/>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point at = e.GetPosition(this);
            if (IsMouseCaptured) DragTo(at);
            else Cursor = CursorFor(HitTestGrip(at));
        }

        /// <inheritdoc/>
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (!IsMouseCaptured) return;
            ReleaseMouseCapture();
            e.Handled = true;
        }

        /// <inheritdoc/>
        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            // The button released, or the capture taken away: either way the drag is over.
            EndDrag();
        }
    }
}
