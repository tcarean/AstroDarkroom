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

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LightsCheck.Wpf;

/// <summary>
/// A point of a chart: x, y (NaN = missing), a label, the colour it is drawn in, and whether the
/// map draws it as a ring (a frame of a run at 10 px, R-035).
/// </summary>
/// <param name="X">
/// The frame index on the series charts, the position in pixels on the map; NaN = missing.
/// </param>
/// <param name="Y">
/// The value on the series charts, the position in pixels on the map; NaN = missing.
/// </param>
/// <param name="Label">The frame's number, drawn beside the point on the map.</param>
/// <param name="Colour">The colour the point (and the line or bar to it) is drawn in.</param>
/// <param name="Ring">True for a hollow ring on the map instead of a filled dot.</param>
/// <param name="Tip">
/// The tooltip the series chart shows when the mouse is over the point; null = none.
/// </param>
public sealed record ChartPoint(double X, double Y, string Label, Color Colour, bool Ring = false, string? Tip = null);

/// <summary>A horizontal guide line at a value, with its label.</summary>
/// <param name="Value">The value on the y axis the line is drawn at, in the chart's unit.</param>
/// <param name="Label">The text drawn above the line's right end.</param>
/// <param name="Colour">The colour of the dashed line and of its label.</param>
public sealed record GuideLine(double Value, string Label, Color Colour);

/// <summary>
/// What every chart shares: the palette, the typeface, the margins, the axis mapping.
/// </summary>
public abstract class ChartElement : FrameworkElement
{
    protected static readonly Brush AxisBrush = Frozen(Color.FromArgb(0x66, 0x6E, 0x6E, 0x77));
    protected static readonly Brush TickBrush = Frozen(Color.FromRgb(0x9A, 0x9A, 0xA3));
    protected static readonly Brush LabelBrush = Frozen(Color.FromRgb(0xE4, 0xE4, 0xE7));
    protected static readonly Brush LabelBacking = Frozen(Color.FromArgb(0xD0, 0x1E, 0x1E, 0x22));
    protected static readonly Typeface Face = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    protected const double MarginLeft = 44, MarginRight = 12, MarginTop = 10, MarginBottom = 22;

    /// <summary>
    /// A solid brush of one colour, frozen so rendering never tracks it for changes.
    /// </summary>
    /// <param name="c">The colour.</param>
    /// <returns>The frozen brush.</returns>
    protected static Brush Frozen(Color c)
    {
        SolidColorBrush b = new(c);
        b.Freeze();
        return b;
    }

    /// <summary>A frozen pen of one colour.</summary>
    /// <param name="c">The colour.</param>
    /// <param name="thickness">The line width, in device-independent pixels.</param>
    /// <param name="dashed">True for a dashed line, false for a solid one.</param>
    /// <returns>The frozen pen.</returns>
    protected static Pen FrozenPen(Color c, double thickness, bool dashed = false)
    {
        Pen p = new(Frozen(c), thickness);
        if (dashed)
        {
            p.DashStyle = DashStyles.Dash;
        }

        p.Freeze();
        return p;
    }

    /// <summary>Registers a dependency property whose change redraws the chart.</summary>
    /// <typeparam name="T">The property's type.</typeparam>
    /// <param name="name">The property's name.</param>
    /// <param name="owner">The chart type that owns it.</param>
    /// <param name="def">The default value; the type's default when left out.</param>
    /// <returns>The registered property, with metadata that affects the render.</returns>
    protected static DependencyProperty Reg<T>(string name, Type owner, T? def = default) =>
        DependencyProperty.Register(name, typeof(T), owner, new FrameworkPropertyMetadata(def, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// A text laid out in the charts' typeface at this element's DPI, culture-invariant.
    /// </summary>
    /// <param name="s">The text.</param>
    /// <param name="size">The font size, in device-independent pixels.</param>
    /// <param name="brush">The brush it is drawn with.</param>
    /// <returns>The formatted text, ready to measure and draw.</returns>
    protected FormattedText Text(string s, double size, Brush brush) =>
        new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    /// <summary>
    /// A tick label with as many decimals as the tick step needs, so two ticks never read the same.
    /// </summary>
    /// <param name="v">The tick's value.</param>
    /// <param name="step">The distance between two ticks, in the same unit.</param>
    /// <returns>
    /// The value with 0 decimals for a step of 1 or more, 1 from 0.1, 2 from 0.01, else 3;
    /// invariant culture.
    /// </returns>
    protected static string Num(double v, double step)
    {
        int decimals = step >= 1 ? 0 : step >= 0.1 ? 1 : step >= 0.01 ? 2 : 3;
        return v.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Values over the frame index as a line with dots (the trend charts) or as bars (the step
/// chart), with guide lines at thresholds. Missing values (NaN) break the line.
/// </summary>
public sealed class SeriesChart : ChartElement
{
    public static readonly DependencyProperty PointsProperty = Reg<IReadOnlyList<ChartPoint>>("Points", typeof(SeriesChart));
    public static readonly DependencyProperty GuidesProperty = Reg<IReadOnlyList<GuideLine>>("Guides", typeof(SeriesChart));
    public static readonly DependencyProperty BarsProperty = Reg("Bars", typeof(SeriesChart), false);
    public static readonly DependencyProperty TitleProperty = Reg<string>("Title", typeof(SeriesChart));
    public static readonly DependencyProperty MinimumProperty = Reg("Minimum", typeof(SeriesChart), double.NaN);

    public IReadOnlyList<ChartPoint>? Points { get => (IReadOnlyList<ChartPoint>?)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public IReadOnlyList<GuideLine>? Guides { get => (IReadOnlyList<GuideLine>?)GetValue(GuidesProperty); set => SetValue(GuidesProperty, value); }
    public bool Bars { get => (bool)GetValue(BarsProperty); set => SetValue(BarsProperty, value); }
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>
    /// The y axis starts here when set; otherwise 8 % of the range under the smallest value or
    /// guide (or at 0 for bars).
    /// </summary>
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    // The last layout, for the hover: which point sits under the mouse (R-039). The tooltip
    // is one owned object opened and closed by hand as the hovered point changes, since a
    // ToolTip the framework opens shows once per entry into the element and then stays shut.
    private double _xMin, _xMax = 1, _plotW;
    private IReadOnlyList<ChartPoint> _drawn = [];
    private readonly ToolTip _tip = new() { Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse };
    private ChartPoint? _hover;

    /// <summary>An empty chart whose one tooltip is placed at the mouse over it.</summary>
    public SeriesChart()
    {
        _tip.PlacementTarget = this;
    }

    /// <summary>
    /// The point whose slot holds the x position, or null outside every slot; the tooltip shows its
    /// Tip.
    /// </summary>
    /// <param name="x">
    /// The horizontal position in the element, in device-independent pixels.
    /// </param>
    /// <returns>
    /// The point of the last render nearest to <paramref name="x"/> within half a slot (one frame's
    /// width), points without an x skipped; null before a render or outside every slot.
    /// </returns>
    internal ChartPoint? PointAt(double x)
    {
        if (_drawn.Count == 0 || _plotW <= 0)
        {
            return null;
        }

        double slot = _plotW / Math.Max(1, _xMax - _xMin);
        ChartPoint? hit = null;
        double best = double.MaxValue;
        foreach (ChartPoint p in _drawn)
        {
            if (double.IsNaN(p.X))
            {
                continue;
            }

            double dx = Math.Abs(MarginLeft + (p.X - _xMin) / (_xMax - _xMin) * _plotW - x);
            if (dx < best && dx <= slot / 2)
            {
                best = dx;
                hit = p;
            }
        }

        return hit;
    }

    /// <summary>
    /// Follows the mouse: when the point under it changes, closes the tooltip and opens it again
    /// with the new point's Tip, if it has one.
    /// </summary>
    /// <param name="e">The mouse's arguments; its position over this element is read.</param>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        ArgumentNullException.ThrowIfNull(e);
        ChartPoint? hit = PointAt(e.GetPosition(this).X);
        if (ReferenceEquals(hit, _hover))
        {
            return;
        }

        _hover = hit;
        _tip.IsOpen = false;
        if (hit?.Tip is string tip)
        {
            _tip.Content = tip;
            _tip.IsOpen = true;
        }
    }

    /// <summary>
    /// Closes the tooltip and forgets the hovered point when the mouse leaves the chart.
    /// </summary>
    /// <param name="e">The mouse's arguments, not used.</param>
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = null;
        _tip.IsOpen = false;
    }

    /// <summary>
    /// Draws the axes and ticks, the points as bars or as a line broken at each NaN, the guide
    /// lines with their labels over the data, and the title; nothing but the hit area under a
    /// minimum size. Keeps the layout for <see cref="PointAt"/>.
    /// </summary>
    /// <param name="drawingContext">The context the chart is drawn into.</param>
    protected override void OnRender(DrawingContext drawingContext)
    {
        double w = ActualWidth, h = ActualHeight;
        drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));
        if (w < MarginLeft + MarginRight + 20 || h < MarginTop + MarginBottom + 20)
        {
            return;
        }

        IReadOnlyList<ChartPoint> pts = Points ?? [];
        IReadOnlyList<GuideLine> guides = Guides ?? [];
        double plotW = w - MarginLeft - MarginRight, plotH = h - MarginTop - MarginBottom;
        double xMin = pts.Count > 0 ? pts.Min(p => p.X) : 0, xMax = pts.Count > 0 ? pts.Max(p => p.X) : 1;
        if (xMax <= xMin)
        {
            xMax = xMin + 1;
        }

        IEnumerable<double> ys = pts.Where(p => !double.IsNaN(p.Y)).Select(p => p.Y).Concat(guides.Select(g => g.Value));
        double yMin = double.IsNaN(Minimum) ? (Bars ? 0 : (ys.Any() ? ys.Min() : 0)) : Minimum;
        double yMax = ys.Any() ? ys.Max() : 1;
        if (yMax <= yMin)
        {
            yMax = yMin + 1;
        }

        double pad = (yMax - yMin) * 0.08;
        yMax += pad;
        if (double.IsNaN(Minimum) && !Bars)
        {
            yMin -= pad;
        }

        double X(double x) => MarginLeft + (x - xMin) / (xMax - xMin) * plotW;
        double Y(double y) => MarginTop + (yMax - y) / (yMax - yMin) * plotH;
        (_xMin, _xMax, _plotW, _drawn) = (xMin, xMax, plotW, pts);

        // Axes and ticks.
        Pen axis = FrozenPen(Color.FromArgb(0x66, 0x6E, 0x6E, 0x77), 1);
        drawingContext.DrawLine(axis, new Point(MarginLeft, MarginTop), new Point(MarginLeft, MarginTop + plotH));
        drawingContext.DrawLine(axis, new Point(MarginLeft, MarginTop + plotH), new Point(MarginLeft + plotW, MarginTop + plotH));
        for (int i = 0; i <= 4; i++)
        {
            double yv = yMin + (yMax - yMin) * i / 4;
            FormattedText t = Text(Num(yv, (yMax - yMin) / 4), 10, TickBrush);
            drawingContext.DrawText(t, new Point(MarginLeft - 6 - t.Width, Y(yv) - t.Height / 2));
            drawingContext.DrawLine(FrozenPen(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF), 1), new Point(MarginLeft, Y(yv)), new Point(MarginLeft + plotW, Y(yv)));
        }

        int step = Math.Max(1, (int)Math.Ceiling((xMax - xMin) / Math.Max(1, plotW / 40)));
        for (double xv = Math.Ceiling(xMin); xv <= xMax; xv += step)
        {
            FormattedText t = Text(xv.ToString("F0", CultureInfo.InvariantCulture), 10, TickBrush);
            drawingContext.DrawText(t, new Point(X(xv) - t.Width / 2, MarginTop + plotH + 4));
        }

        if (Bars)
        {
            double bw = Math.Max(2, plotW / (xMax - xMin + 1) * 0.7);
            foreach (ChartPoint p in pts)
            {
                if (double.IsNaN(p.Y))
                {
                    continue;
                }

                double top = Y(Math.Max(p.Y, yMin)), bottom = Y(yMin);
                drawingContext.DrawRectangle(Frozen(p.Colour), null, new Rect(X(p.X) - bw / 2, Math.Min(top, bottom), bw, Math.Abs(bottom - top)));
            }
        }
        else
        {
            Point? previous = null;
            foreach (ChartPoint p in pts)
            {
                if (double.IsNaN(p.Y))
                {
                    previous = null;
                    continue;
                }

                Point here = new(X(p.X), Y(p.Y));
                if (previous is Point pv)
                {
                    drawingContext.DrawLine(FrozenPen(p.Colour, 1.5), pv, here);
                }

                drawingContext.DrawEllipse(Frozen(p.Colour), null, here, 2.5, 2.5);
                previous = here;
            }
        }

        // Guides over the data, each label on a dark backing: a bar of the guide's own colour
        // under the label would otherwise swallow it.
        foreach (GuideLine g in guides)
        {
            drawingContext.DrawLine(FrozenPen(g.Colour, 1, dashed: true), new Point(MarginLeft, Y(g.Value)), new Point(MarginLeft + plotW, Y(g.Value)));
            FormattedText t = Text(g.Label, 10, Frozen(g.Colour));
            Point at = new(MarginLeft + plotW - t.Width - 4, Y(g.Value) - t.Height - 1);
            drawingContext.DrawRectangle(LabelBacking, null, new Rect(at.X - 2, at.Y, t.Width + 4, t.Height));
            drawingContext.DrawText(t, at);
        }

        if (!string.IsNullOrEmpty(Title))
        {
            FormattedText t = Text(Title, 11, LabelBrush);
            drawingContext.DrawText(t, new Point(MarginLeft + 6, MarginTop - 2));
        }
    }
}

/// <summary>
/// The dither map: every frame's position relative to the first, joined in order, numbered,
/// coloured by the frame's class; equal scales on both axes, y downward like the image.
/// </summary>
public sealed class DitherMap : ChartElement
{
    public static readonly DependencyProperty PointsProperty = Reg<IReadOnlyList<ChartPoint>>("Points", typeof(DitherMap));
    public static readonly DependencyProperty XLabelProperty = Reg<string>("XLabel", typeof(DitherMap));
    public static readonly DependencyProperty YLabelProperty = Reg<string>("YLabel", typeof(DitherMap));

    public IReadOnlyList<ChartPoint>? Points { get => (IReadOnlyList<ChartPoint>?)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public string? XLabel { get => (string?)GetValue(XLabelProperty); set => SetValue(XLabelProperty, value); }
    public string? YLabel { get => (string?)GetValue(YLabelProperty); set => SetValue(YLabelProperty, value); }

    /// <summary>
    /// Draws the grid, the path joining the frames in order, each point as a dot or a ring with
    /// its number, and the two axis labels; the points without both coordinates are left out, and
    /// nothing is drawn under 80 x 80 or without a point.
    /// </summary>
    /// <param name="drawingContext">The context the map is drawn into.</param>
    protected override void OnRender(DrawingContext drawingContext)
    {
        double w = ActualWidth, h = ActualHeight;
        drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));
        List<ChartPoint> pts = (Points ?? []).Where(p => !double.IsNaN(p.X) && !double.IsNaN(p.Y)).ToList();
        if (w < 80 || h < 80 || pts.Count == 0)
        {
            return;
        }

        double plotW = w - MarginLeft - MarginRight, plotH = h - MarginTop - MarginBottom;
        double xMin = pts.Min(p => p.X), xMax = pts.Max(p => p.X), yMin = pts.Min(p => p.Y), yMax = pts.Max(p => p.Y);
        double span = Math.Max(Math.Max(xMax - xMin, yMax - yMin), 10) * 1.15;
        double cx = (xMin + xMax) / 2, cy = (yMin + yMax) / 2;
        double scale = Math.Min(plotW, plotH) / span;
        double X(double x) => MarginLeft + plotW / 2 + (x - cx) * scale;
        double Y(double y) => MarginTop + plotH / 2 + (y - cy) * scale;

        // A grid every 10 px of the image: the walking-noise distance (every 50 px once the
        // span passes 400 px).
        Pen grid = FrozenPen(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF), 1);
        double gridStep = span > 400 ? 50 : 10;
        for (double gx = Math.Floor((cx - span / 2) / gridStep) * gridStep; gx <= cx + span / 2; gx += gridStep)
        {
            if (X(gx) >= MarginLeft && X(gx) <= MarginLeft + plotW)
            {
                drawingContext.DrawLine(grid, new Point(X(gx), MarginTop), new Point(X(gx), MarginTop + plotH));
            }
        }

        for (double gy = Math.Floor((cy - span / 2) / gridStep) * gridStep; gy <= cy + span / 2; gy += gridStep)
        {
            if (Y(gy) >= MarginTop && Y(gy) <= MarginTop + plotH)
            {
                drawingContext.DrawLine(grid, new Point(MarginLeft, Y(gy)), new Point(MarginLeft + plotW, Y(gy)));
            }
        }

        Pen path = FrozenPen(Color.FromArgb(0x80, 0x9A, 0x9A, 0xA3), 1);
        for (int i = 1; i < pts.Count; i++)
        {
            drawingContext.DrawLine(path, new Point(X(pts[i - 1].X), Y(pts[i - 1].Y)), new Point(X(pts[i].X), Y(pts[i].Y)));
        }

        foreach (ChartPoint p in pts)
        {
            Point here = new(X(p.X), Y(p.Y));
            if (p.Ring)
            {
                drawingContext.DrawEllipse(null, FrozenPen(p.Colour, 1.5), here, 4, 4);
            }
            else
            {
                drawingContext.DrawEllipse(Frozen(p.Colour), null, here, 4, 4);
            }

            FormattedText t = Text(p.Label, 9, TickBrush);
            drawingContext.DrawText(t, new Point(here.X + 5, here.Y - t.Height / 2));
        }

        FormattedText xl = Text($"{XLabel} (px); grila la {gridStep.ToString("F0", CultureInfo.InvariantCulture)} px", 10, TickBrush);
        drawingContext.DrawText(xl, new Point(MarginLeft + plotW - xl.Width, MarginTop + plotH + 4));
        FormattedText yl = Text($"{YLabel} (px)", 10, TickBrush);
        drawingContext.DrawText(yl, new Point(4, MarginTop));
    }
}
