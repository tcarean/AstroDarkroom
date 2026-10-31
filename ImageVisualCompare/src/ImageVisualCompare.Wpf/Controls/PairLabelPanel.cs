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

using System.Windows;
using System.Windows.Controls;
using ImageVisualCompare.Core;

namespace ImageVisualCompare.Wpf.Controls;

/// <summary>
/// Lays the toolbar's pair label out in a row, left to right (R-065): the two children marked
/// <see cref="IsNameProperty"/> are the file names, given the widths <see cref="PairLabelLayout"/>
/// works out from the room the panel has, so they are trimmed before a button is lost; every
/// other child keeps its own width. A <c>StackPanel</c> measures its children without a limit
/// and would let a long name push the last button out of the panel's clip. With other than two
/// children marked as names nothing is trimmed: the row is then a plain stack.
/// </summary>
public sealed class PairLabelPanel : Panel
{
    /// <summary>Marks a child as one of the two file names, the ones that give up width.</summary>
    public static readonly DependencyProperty IsNameProperty = DependencyProperty.RegisterAttached(
        "IsName", typeof(bool), typeof(PairLabelPanel), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsParentMeasure));

    /// <summary>Marks or unmarks a child as a file name.</summary>
    /// <param name="element">The child.</param>
    /// <param name="value">True for a file name.</param>
    public static void SetIsName(UIElement element, bool value) => element.SetValue(IsNameProperty, value);

    /// <summary>Says whether a child is a file name.</summary>
    /// <param name="element">The child.</param>
    /// <returns>True for a file name.</returns>
    public static bool GetIsName(UIElement element) => (bool)element.GetValue(IsNameProperty);

    /// <summary>
    /// Measures every child at its own width, the names within the panel's whole width, then
    /// a name again at the width <see cref="PairLabelLayout.For"/> gives it when that is less,
    /// so it is trimmed. A name that fits is left measured at the panel's width on purpose:
    /// WPF measures a child whose text changed with the last constraint it was given, and asks
    /// the panel again only when the child's desired size changed; a name last measured at its
    /// own width could never grow, and the names, empty when the toolbar is first laid out,
    /// stayed invisible once the pair was shown (seen on 2026-10-30).
    /// </summary>
    /// <param name="availableSize">The room the panel has; its width may be infinite.</param>
    /// <returns>The row's size: the children's widths summed, capped at the room, and the tallest child.</returns>
    protected override Size MeasureOverride(Size availableSize)
    {
        var names = new List<UIElement>(2);
        double fixedWidth = 0;
        foreach (UIElement child in InternalChildren)
        {
            if (GetIsName(child))
            {
                child.Measure(new Size(availableSize.Width, availableSize.Height));
                names.Add(child);
            }
            else
            {
                child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
                fixedWidth += child.DesiredSize.Width;
            }
        }

        if (names.Count == 2)
        {
            PairLabelLayout layout = PairLabelLayout.For(
                availableSize.Width, names[0].DesiredSize.Width, names[1].DesiredSize.Width, fixedWidth);
            Trim(names[0], layout.NameAWidth, availableSize.Height);
            Trim(names[1], layout.NameBWidth, availableSize.Height);
        }

        double width = 0;
        double height = 0;
        foreach (UIElement child in InternalChildren)
        {
            width += child.DesiredSize.Width;
            height = Math.Max(height, child.DesiredSize.Height);
        }

        return new Size(Math.Min(width, availableSize.Width), height);
    }

    /// <summary>Measures a name again at the width the layout gives it, only when that is less than it took.</summary>
    /// <param name="name">The name.</param>
    /// <param name="width">The width the layout gives it.</param>
    /// <param name="height">The room's height.</param>
    private static void Trim(UIElement name, double width, double height)
    {
        if (width < name.DesiredSize.Width)
        {
            name.Measure(new Size(width, height));
        }
    }

    /// <summary>Places the children in a row, each at the width it was measured to, the row's full height.</summary>
    /// <param name="finalSize">The size the panel was given.</param>
    /// <returns>That size.</returns>
    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0;
        foreach (UIElement child in InternalChildren)
        {
            double width = child.DesiredSize.Width;
            child.Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width;
        }

        return finalSize;
    }
}
