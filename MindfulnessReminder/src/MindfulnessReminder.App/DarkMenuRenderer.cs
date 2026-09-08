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

using System.Drawing;
using System.Windows.Forms;

namespace MindfulnessReminder.App;

/// <summary>
/// Paints the tray menu in the same colours as the Settings window.
///
/// The menu is Windows Forms, not WPF, so none of Theme/DarkTheme.xaml reaches it and the
/// colours are repeated here as <see cref="Color"/> literals. That duplication is the price
/// of the hybrid design; the alternative is a shared table in Core, which would be a
/// palette -- presentation, not a rule -- living in the project that is meant to hold only
/// rules.
///
/// Setting BackColor and ForeColor on the strip is not enough on its own: the professional
/// renderer paints the drop-down background, the border, the gutter down the left where the
/// check mark goes, and the hover fill from its own colour table, and every one of those is
/// light. What is overridden below is exactly that list, flat rather than gradient, because
/// a gradient tuned for a light menu turns muddy when its two ends are dark.
/// </summary>
internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
{
    private static readonly Color Background = Color.FromArgb(0x1B, 0x21, 0x1F);

    private static readonly Color BorderColor = Color.FromArgb(0x3A, 0x42, 0x3F);

    private static readonly Color Hover = Color.FromArgb(0x2A, 0x30, 0x2E);

    private static readonly Color TextColor = Color.FromArgb(0xE6, 0xEA, 0xE8);

    private static readonly Color DisabledTextColor = Color.FromArgb(0x5E, 0x6A, 0x66);

    private static readonly Color Accent = Color.FromArgb(0x2A, 0x7A, 0x6E);

    private static readonly Color SeparatorColor = Color.FromArgb(0x33, 0x3B, 0x38);

    public DarkMenuRenderer()
        : base(new DarkColorTable())
    {
    }

    /// <summary>Applies the renderer and the two colours the renderer does not reach.</summary>
    public static void Apply(ToolStripDropDown menu)
    {
        ArgumentNullException.ThrowIfNull(menu);

        menu.BackColor = Background;
        menu.ForeColor = TextColor;
        menu.Renderer = new DarkMenuRenderer();
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        e.Graphics.Clear(Background);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        using var pen = new Pen(BorderColor);

        var bounds = e.AffectedBounds;
        e.Graphics.DrawRectangle(pen, 0, 0, bounds.Width - 1, bounds.Height - 1);
    }

    /// <summary>
    /// The strip down the left that holds the check mark. Left alone it stays the light
    /// gutter of the stock menu, which is the single most obvious tell that a dark menu was
    /// only half done.
    /// </summary>
    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        using var brush = new SolidBrush(Background);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        // Selected AND enabled: a disabled item still reports Selected when the mouse is
        // over it, and highlighting one suggests it can be clicked.
        var fill = e.Item.Selected && e.Item.Enabled ? Hover : Background;

        using var brush = new SolidBrush(fill);
        e.Graphics.FillRectangle(brush, new Rectangle(Point.Empty, e.Item.Size));
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        e.TextColor = e.Item.Enabled ? TextColor : DisabledTextColor;

        base.OnRenderItemText(e);
    }

    /// <summary>
    /// The tick beside "Run at Windows startup". The stock renderer draws it on a pale
    /// rounded plate that reads as a hole in a dark menu, so the plate becomes the accent
    /// and the glyph becomes white on top of it.
    /// </summary>
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        var bounds = e.ImageRectangle;

        using var brush = new SolidBrush(Accent);
        e.Graphics.FillRectangle(brush, bounds);

        ControlPaint.DrawMenuGlyph(e.Graphics, bounds, MenuGlyph.Checkmark, Color.White, Accent);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        var bounds = new Rectangle(Point.Empty, e.Item.Size);
        var middle = bounds.Height / 2;

        using var pen = new Pen(SeparatorColor);
        e.Graphics.DrawLine(pen, bounds.Left + 4, middle, bounds.Right - 4, middle);
    }

    /// <summary>
    /// What is left after the overrides above: the odd corner the professional renderer
    /// reaches for without going through one of them. Keeping a table underneath means a
    /// missed spot lands on a dark colour rather than on the stock light one.
    /// </summary>
    private sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Background;

        public override Color MenuBorder => BorderColor;

        public override Color MenuItemBorder => Hover;

        public override Color MenuItemSelected => Hover;

        public override Color MenuItemSelectedGradientBegin => Hover;

        public override Color MenuItemSelectedGradientEnd => Hover;

        public override Color MenuItemPressedGradientBegin => Hover;

        public override Color MenuItemPressedGradientMiddle => Hover;

        public override Color MenuItemPressedGradientEnd => Hover;

        public override Color ImageMarginGradientBegin => Background;

        public override Color ImageMarginGradientMiddle => Background;

        public override Color ImageMarginGradientEnd => Background;

        public override Color CheckBackground => Accent;

        public override Color CheckSelectedBackground => Accent;

        public override Color CheckPressedBackground => Accent;

        public override Color SeparatorDark => SeparatorColor;

        public override Color SeparatorLight => SeparatorColor;
    }
}
