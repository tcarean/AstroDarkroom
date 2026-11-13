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

// BeforeAfterViewer.xaml.cs - two images of the same size compared across a vertical line
// (R-179, ADR 0090): the paper fit beside the original in the Culori pentru hartie tab.
//
// One ZoomPanViewer holds both images, so they share one scale and one translation and cannot
// drift apart. The line belongs to the viewport (SplitDivider keeps it as a fraction of the
// width), while the cut of the Before layer has to be said in the child's units, because the
// viewer transforms its child: the cut is set again whenever the view or the line moves
// (ZoomPanViewer.ViewChanged, ToChildX). The line wins over the image: a press SplitDivider
// calls the line's is taken here, on the way down, and never reaches the viewer, so it cannot
// pan or reset the zoom; every other press is the viewer's, and so is the wheel, which this
// control hands to the viewer itself while a drag of the line holds the mouse. All of it is
// view state: nothing is computed about the images, and no view-model holds the line's place.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// Shows two images of the same size as one, cut by a vertical line the user drags: Before
    /// left of it, After right of it, zoomed and panned together (R-179).
    /// </summary>
    public partial class BeforeAfterViewer : UserControl
    {
        readonly SplitDivider _divider = new();
        readonly TranslateTransform _lineAt = new(), _handleAt = new(), _beforeLabelAt = new(), _afterLabelAt = new();
        bool _dragging;

        // Where the line was grabbed: the pointer's x less the line's, kept for the whole drag,
        // so that the line does not jump under the pointer at the first move.
        double _grabOffset;

        /// <summary>The image left of the line; null shows nothing there.</summary>
        public static readonly DependencyProperty BeforeProperty = Register<ImageSource>(nameof(Before), null);

        /// <summary>A layer painted over <see cref="Before"/>, cut with it; null for none.</summary>
        public static readonly DependencyProperty BeforeOverlayProperty = Register<ImageSource>(nameof(BeforeOverlay), null);

        /// <summary>The image right of the line; null means there is nothing to compare, and the line is hidden.</summary>
        public static readonly DependencyProperty AfterProperty = Register<ImageSource>(nameof(After), null, (v, _) => v.OnAfterChanged());

        /// <summary>A layer painted over <see cref="After"/>; null for none.</summary>
        public static readonly DependencyProperty AfterOverlayProperty = Register<ImageSource>(nameof(AfterOverlay), null);

        /// <summary>The width the two images share, in their own pixels.</summary>
        public static readonly DependencyProperty ContentWidthProperty = Register(nameof(ContentWidth), 0.0, (v, _) => v.Place());

        /// <summary>The height the two images share, in their own pixels.</summary>
        public static readonly DependencyProperty ContentHeightProperty = Register(nameof(ContentHeight), 0.0, (v, _) => v.Place());

        /// <summary>The label of the left side, as the user reads it.</summary>
        public static readonly DependencyProperty BeforeTextProperty = Register<string>(nameof(BeforeText), null);

        /// <summary>The label of the right side, as the user reads it.</summary>
        public static readonly DependencyProperty AfterTextProperty = Register<string>(nameof(AfterText), null);

        /// <summary>Passed to the viewer: whether images smaller than the area are enlarged to it (<see cref="ZoomPanViewer.FitEnlarges"/>).</summary>
        public static readonly DependencyProperty FitEnlargesProperty = Register(nameof(FitEnlarges), true);

        /// <summary>
        /// Loads the XAML, sizes the line and the handle, gives each part of the comparison the
        /// translation that places it, and follows the viewer's view.
        /// </summary>
        public BeforeAfterViewer()
        {
            InitializeComponent();
            // The inner bindings read this control's properties; the control's own bindings,
            // set where it is used, keep the data context it inherits.
            LayoutRoot.DataContext = this;
            Line.Width = SplitDivider.LineWidth;
            Handle.Width = SplitDivider.HandleWidth;
            Handle.Height = SplitDivider.HandleHeight;
            Line.RenderTransform = _lineAt;
            Handle.RenderTransform = _handleAt;
            BeforeLabel.RenderTransform = _beforeLabelAt;
            AfterLabel.RenderTransform = _afterLabelAt;
            _beforeLabelAt.Y = _afterLabelAt.Y = SplitDivider.LabelGap;
            Viewer.ViewChanged += (_, _) => Place();
            BeforeLabel.SizeChanged += (_, _) => Place();
            AfterLabel.SizeChanged += (_, _) => Place();
        }

        public ImageSource Before { get => (ImageSource)GetValue(BeforeProperty); set => SetValue(BeforeProperty, value); }
        public ImageSource BeforeOverlay { get => (ImageSource)GetValue(BeforeOverlayProperty); set => SetValue(BeforeOverlayProperty, value); }
        public ImageSource After { get => (ImageSource)GetValue(AfterProperty); set => SetValue(AfterProperty, value); }
        public ImageSource AfterOverlay { get => (ImageSource)GetValue(AfterOverlayProperty); set => SetValue(AfterOverlayProperty, value); }
        public double ContentWidth { get => (double)GetValue(ContentWidthProperty); set => SetValue(ContentWidthProperty, value); }
        public double ContentHeight { get => (double)GetValue(ContentHeightProperty); set => SetValue(ContentHeightProperty, value); }
        public string BeforeText { get => (string)GetValue(BeforeTextProperty); set => SetValue(BeforeTextProperty, value); }
        public string AfterText { get => (string)GetValue(AfterTextProperty); set => SetValue(AfterTextProperty, value); }
        public bool FitEnlarges { get => (bool)GetValue(FitEnlargesProperty); set => SetValue(FitEnlargesProperty, value); }

        /// <summary>The line's place as a fraction of the area's width: 0.5 until it is dragged.</summary>
        public double DividerPosition => _divider.Position;

        /// <summary>Registers one of the control's dependency properties.</summary>
        /// <typeparam name="T">The property's type.</typeparam>
        /// <param name="name">The property's name.</param>
        /// <param name="initial">Its default value.</param>
        /// <param name="changed">What to do on the control when the value changes; null for nothing.</param>
        /// <returns>The registered property.</returns>
        static DependencyProperty Register<T>(string name, T initial, Action<BeforeAfterViewer, DependencyPropertyChangedEventArgs> changed = null)
            => DependencyProperty.Register(name, typeof(T), typeof(BeforeAfterViewer),
                new PropertyMetadata(initial, changed == null ? null : (d, e) => changed((BeforeAfterViewer)d, e)));

        /// <summary>
        /// Moves the line under an x of the area, as a drag does, and cuts the Before layer
        /// there; the view does not move.
        /// </summary>
        /// <param name="x">The x, in device-independent pixels from the area's left edge; past an edge the line stops on it.</param>
        internal void MoveDividerTo(double x)
        {
            _divider.MoveTo(x, Viewer.Viewport.Width);
            Place();
        }

        /// <summary>
        /// Starts a drag of the line when a press is the line's (<see cref="SplitDivider.HitTest"/>),
        /// remembering how far from the line it landed.
        /// </summary>
        /// <param name="at">The press, in the viewer's device-independent pixels.</param>
        /// <returns>True when the line is now dragged; false when the press is the image's.</returns>
        internal bool BeginDividerDrag(Point at)
        {
            if (!OnDivider(at)) return false;
            _dragging = true;
            _grabOffset = at.X - _divider.LineX(Viewer.Viewport.Width);
            return true;
        }

        /// <summary>Moves a dragged line with the pointer, keeping the distance it was grabbed at; nothing without a drag.</summary>
        /// <param name="x">The pointer's x, in the viewer's device-independent pixels.</param>
        internal void DragDividerTo(double x)
        {
            if (_dragging) MoveDividerTo(x - _grabOffset);
        }

        /// <summary>Ends a drag of the line; the line stays where it is.</summary>
        internal void EndDividerDrag() => _dragging = false;

        /// <summary>
        /// A new After image: the line shows only while there is something to compare. When
        /// there is nothing, the line goes back to the middle and the view to the fit, so that
        /// the next image starts whole whatever its size (the viewer by itself resets only for a
        /// child of another size); another variant of the same image replaces After without
        /// passing through null, and keeps both. A drag in progress ends with the comparison:
        /// the pointer has nothing left to move.
        /// </summary>
        void OnAfterChanged()
        {
            bool compares = After != null;
            Overlay.Visibility = compares ? Visibility.Visible : Visibility.Collapsed;
            if (!compares)
            {
                EndDividerDrag();
                if (IsMouseCaptured) ReleaseMouseCapture();
                _divider.Reset();
                Viewer.Reset();
            }
            Place();
        }

        /// <summary>
        /// Puts the line, the handle and the two labels where <see cref="SplitDivider"/> says for
        /// the viewer's viewport, and cuts the Before layer at the line, in the child's units.
        /// It sets translations, a label's being seen and a clip, never a size or a position of
        /// the layout: it runs inside the viewer's arrange pass too, where one set from here
        /// would be applied to a slot the layout had already given.
        /// </summary>
        void Place()
        {
            Size area = Viewer.Viewport;
            if (area.Width <= 0 || area.Height <= 0) return;
            double x = _divider.LineX(area.Width);

            _lineAt.X = x - SplitDivider.LineWidth / 2;
            Rect handle = _divider.Handle(area.Width, area.Height);
            _handleAt.X = handle.X;
            _handleAt.Y = handle.Y;

            // Hidden, not Collapsed: a hidden label keeps its measured width for the next move.
            double before = BeforeLabel.DesiredSize.Width, after = AfterLabel.DesiredSize.Width;
            _beforeLabelAt.X = x - SplitDivider.LabelGap - before;
            _afterLabelAt.X = x + SplitDivider.LabelGap;
            BeforeLabel.Visibility = SplitDivider.LabelFits(x, before) ? Visibility.Visible : Visibility.Hidden;
            AfterLabel.Visibility = SplitDivider.LabelFits(area.Width - x, after) ? Visibility.Visible : Visibility.Hidden;

            // Everything of the child left of the line, whatever lies above and below the view.
            // A new clip asks the layer for a layout pass, so an unchanged cut sets nothing.
            var cut = new Rect(0, 0, Math.Max(0, Viewer.ToChildX(x)), Math.Max(0, ContentHeight));
            if (BeforeLayer.Clip is not RectangleGeometry now || now.Rect != cut) BeforeLayer.Clip = new RectangleGeometry(cut);
        }

        /// <summary>Whether a point of the viewer is the line's to grab, with something to compare.</summary>
        /// <param name="at">The point, in the viewer's device-independent pixels.</param>
        /// <returns>True when a press there drags the line.</returns>
        bool OnDivider(Point at)
        {
            Size area = Viewer.Viewport;
            return After != null && _divider.HitTest(at.X, at.Y, area.Width, area.Height);
        }

        /// <inheritdoc/>
        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);
            if (!BeginDividerDrag(e.GetPosition(Viewer))) return;
            // The line's press: taken before the viewer sees it, so it neither pans nor, on a
            // second click, fits the image again. Without the capture no release would be heard
            // outside the control, so a drag that cannot hold the mouse is not started.
            if (!CaptureMouse()) EndDividerDrag();
            e.Handled = true;
        }

        /// <inheritdoc/>
        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            base.OnPreviewMouseMove(e);
            Point at = e.GetPosition(Viewer);
            if (_dragging)
            {
                DragDividerTo(at.X);
                e.Handled = true;
                return;
            }
            // While the viewer pans, the cursor is its own; otherwise the line says it can be dragged.
            Cursor = !Viewer.IsMouseCaptured && OnDivider(at) ? Cursors.SizeWE : null;
        }

        /// <inheritdoc/>
        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonUp(e);
            if (!_dragging) return;
            EndDividerDrag();
            ReleaseMouseCapture();
            e.Handled = true;
        }

        /// <inheritdoc/>
        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            base.OnPreviewMouseWheel(e);
            // While the line is dragged this control holds the mouse, so the wheel would not
            // reach the viewer: it is handed over, and the zoom works with the line in hand.
            if (!_dragging) return;
            Viewer.ZoomAt(e.GetPosition(Viewer), e.Delta > 0 ? ZoomPanViewer.WheelFactor : 1 / ZoomPanViewer.WheelFactor);
            e.Handled = true;
        }

        /// <inheritdoc/>
        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);
            EndDividerDrag();
        }
    }
}
