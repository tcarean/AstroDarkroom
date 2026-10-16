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

// SeriesChart is copied from LightsCheck's Charts.cs (its DitherMap is not needed here), with
// one addition, the dashed Reference series; a fix in the shared mechanics goes there first
// (ADR 0009).

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DarksCheck.Wpf;

/// <summary>A point of a chart: x, y (NaN = missing), a label, the colour it is drawn in, and an optional tooltip.</summary>
public sealed record ChartPoint(double X, double Y, string Label, Color Colour, string? Tip = null);

/// <summary>A horizontal guide line at a value, with its label.</summary>
public sealed record GuideLine(double Value, string Label, Color Colour);

/// <summary>What every chart shares: the palette, the typeface, the margins, the axis mapping.</summary>
public abstract class ChartElement : FrameworkElement
{
    protected static readonly Brush TickBrush = Frozen(Color.FromRgb(0x9A, 0x9A, 0xA3));
    protected static readonly Brush LabelBrush = Frozen(Color.FromRgb(0xE4, 0xE4, 0xE7));
    protected static readonly Brush LabelBacking = Frozen(Color.FromArgb(0xD0, 0x1E, 0x1E, 0x22));
    protected static readonly Typeface Face = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
    protected const double MarginLeft = 44, MarginRight = 12, MarginTop = 10, MarginBottom = 22;

    protected static Brush Frozen(Color c)
    {
        SolidColorBrush b = new(c);
        b.Freeze();
        return b;
    }

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

    protected static DependencyProperty Reg<T>(string name, Type owner, T? def = default) =>
        DependencyProperty.Register(name, typeof(T), owner, new FrameworkPropertyMetadata(def, FrameworkPropertyMetadataOptions.AffectsRender));

    protected FormattedText Text(string s, double size, Brush brush) =>
        new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    /// <summary>A tick label with as many decimals as the tick step needs, so two ticks never read the same.</summary>
    protected static string Num(double v, double step)
    {
        int decimals = step >= 1 ? 0 : step >= 0.1 ? 1 : step >= 0.01 ? 2 : 3;
        return v.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// Values over the frame index (or over n) as a line with dots or as bars, with guide lines at
/// thresholds and an optional dashed reference series (the expected curve of check 8).
/// Missing values (NaN) break the line.
/// </summary>
public sealed class SeriesChart : ChartElement
{
    public static readonly DependencyProperty PointsProperty = Reg<IReadOnlyList<ChartPoint>>("Points", typeof(SeriesChart));
    public static readonly DependencyProperty ReferenceProperty = Reg<IReadOnlyList<ChartPoint>>("Reference", typeof(SeriesChart));
    public static readonly DependencyProperty GuidesProperty = Reg<IReadOnlyList<GuideLine>>("Guides", typeof(SeriesChart));
    public static readonly DependencyProperty BarsProperty = Reg("Bars", typeof(SeriesChart), false);
    public static readonly DependencyProperty TitleProperty = Reg<string>("Title", typeof(SeriesChart));
    public static readonly DependencyProperty MinimumProperty = Reg("Minimum", typeof(SeriesChart), double.NaN);

    public IReadOnlyList<ChartPoint>? Points { get => (IReadOnlyList<ChartPoint>?)GetValue(PointsProperty); set => SetValue(PointsProperty, value); }

    /// <summary>A second series drawn as a dashed line without dots: what the points should follow.</summary>
    public IReadOnlyList<ChartPoint>? Reference { get => (IReadOnlyList<ChartPoint>?)GetValue(ReferenceProperty); set => SetValue(ReferenceProperty, value); }
    public IReadOnlyList<GuideLine>? Guides { get => (IReadOnlyList<GuideLine>?)GetValue(GuidesProperty); set => SetValue(GuidesProperty, value); }
    public bool Bars { get => (bool)GetValue(BarsProperty); set => SetValue(BarsProperty, value); }
    public string? Title { get => (string?)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    /// <summary>The y axis starts here when set; otherwise at the smallest value (or 0 for bars).</summary>
    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }

    // The last layout, for the hover: which point sits under the mouse. The tooltip is one
    // owned object opened and closed by hand as the hovered point changes, since a ToolTip
    // the framework opens shows once per entry into the element and then stays shut.
    private double _xMin, _xMax = 1, _plotW;
    private IReadOnlyList<ChartPoint> _drawn = [];
    private readonly ToolTip _tip = new() { Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse };
    private ChartPoint? _hover;

    public SeriesChart()
    {
        _tip.PlacementTarget = this;
    }

    /// <summary>The point whose slot holds the x position, or null outside every slot; the tooltip shows its Tip.</summary>
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

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = null;
        _tip.IsOpen = false;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        double w = ActualWidth, h = ActualHeight;
        drawingContext.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));
        if (w < MarginLeft + MarginRight + 20 || h < MarginTop + MarginBottom + 20)
        {
            return;
        }

        IReadOnlyList<ChartPoint> pts = Points ?? [];
        IReadOnlyList<ChartPoint> reference = Reference ?? [];
        IReadOnlyList<GuideLine> guides = Guides ?? [];
        IEnumerable<ChartPoint> all = pts.Concat(reference);
        double plotW = w - MarginLeft - MarginRight, plotH = h - MarginTop - MarginBottom;
        double xMin = all.Any() ? all.Min(p => p.X) : 0, xMax = all.Any() ? all.Max(p => p.X) : 1;
        if (xMax <= xMin)
        {
            xMax = xMin + 1;
        }

        IEnumerable<double> ys = all.Where(p => !double.IsNaN(p.Y)).Select(p => p.Y).Concat(guides.Select(g => g.Value));
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

        // The reference first, so the points sit over it.
        Point? previousRef = null;
        foreach (ChartPoint p in reference)
        {
            if (double.IsNaN(p.Y))
            {
                previousRef = null;
                continue;
            }

            Point here = new(X(p.X), Y(p.Y));
            if (previousRef is Point pv)
            {
                drawingContext.DrawLine(FrozenPen(p.Colour, 1.2, dashed: true), pv, here);
            }

            previousRef = here;
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
