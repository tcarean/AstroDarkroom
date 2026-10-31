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
using System.Windows.Media;
using ImageVisualCompare.Core;
using ImageVisualCompare.Wpf.Controls;

namespace ImageVisualCompare.Tests;

/// <summary>
/// The toolbar's pair label on the WPF test thread (ADR 0032): the panel is measured at a few
/// widths, as the toolbar's column would measure it, and the width each child was given is read
/// back. The widths themselves are <see cref="PairLabelLayout"/>'s, tested on their own; here the
/// panel is checked to hand them over (a first version gave the names almost nothing, found on a
/// screenshot on 2026-10-30).
/// </summary>
public class PairLabelPanelTests
{
    [Fact]
    public async Task R065_WithRoomToSpare_EveryChildKeepsItsOwnWidth()
    {
        await WpfThread.RunAsync(() =>
        {
            (PairLabelPanel panel, TextBlock nameA, TextBlock nameB, double fixedWidth) = Label();
            double naturalA = Natural(nameA);
            double naturalB = Natural(nameB);

            panel.Measure(new Size(1270, 26));
            panel.Arrange(new Rect(0, 0, panel.DesiredSize.Width, 26));

            Assert.Equal(naturalA, nameA.DesiredSize.Width, 0.5);
            Assert.Equal(naturalB, nameB.DesiredSize.Width, 0.5);
            Assert.Equal(naturalA + naturalB + fixedWidth, panel.DesiredSize.Width, 0.5);
            Assert.True(naturalA > 50, $"a name of 14 characters measures {naturalA}");
        });
    }

    [Fact]
    public async Task R065_WithoutRoom_TheNamesAreCutToTheLayoutsWidths_AndTheButtonsKeepTheirs()
    {
        await WpfThread.RunAsync(() =>
        {
            (PairLabelPanel panel, TextBlock nameA, TextBlock nameB, double fixedWidth) = Label();
            double naturalA = Natural(nameA);
            double naturalB = Natural(nameB);
            double available = fixedWidth + naturalA + naturalB - 60;
            PairLabelLayout expected = PairLabelLayout.For(available, naturalA, naturalB, fixedWidth);

            panel.Measure(new Size(available, 26));
            panel.Arrange(new Rect(0, 0, panel.DesiredSize.Width, 26));

            Assert.Equal(expected.NameAWidth, nameA.DesiredSize.Width, 0.5);
            Assert.Equal(expected.NameBWidth, nameB.DesiredSize.Width, 0.5);
            Assert.Equal(available, panel.DesiredSize.Width, 0.5);
            foreach (UIElement child in panel.Children)
            {
                if (child is Button button)
                {
                    Assert.Equal(Natural(button), button.DesiredSize.Width, 0.5);
                }
            }

            // The row is laid out left to right and the last button ends inside the room.
            double x = -1;
            foreach (UIElement child in panel.Children)
            {
                double left = VisualTreeHelper.GetOffset(child).X;
                Assert.True(left > x, $"a child at {left} after one at {x}");
                x = left;
            }

            Assert.True(RightEdge(panel) <= available + 0.5, "the last button ends past the room");
        });
    }

    [Fact]
    public async Task R065_AtTheFloor_TheNamesStopAt48_AndTheLabelIsClipped()
    {
        await WpfThread.RunAsync(() =>
        {
            (PairLabelPanel panel, TextBlock nameA, TextBlock nameB, double fixedWidth) = Label();
            double available = fixedWidth + 40;

            panel.Measure(new Size(available, 26));
            panel.Arrange(new Rect(0, 0, panel.DesiredSize.Width, 26));

            // A trimmed name ends at a character boundary, so it can measure a little under the
            // 48 it was given, never over it.
            Assert.InRange(nameA.DesiredSize.Width, PairLabelLayout.MinName - 10, PairLabelLayout.MinName + 0.5);
            Assert.InRange(nameB.DesiredSize.Width, PairLabelLayout.MinName - 10, PairLabelLayout.MinName + 0.5);
            Assert.Equal(available, panel.DesiredSize.Width, 0.5);
            Assert.True(RightEdge(panel) > available, "the last button is not past the clip");
        });
    }

    /// <summary>Where the last child's border ends, in the panel: its offset, its width and its right margin.</summary>
    private static double RightEdge(PairLabelPanel panel)
    {
        var last = (FrameworkElement)panel.Children[panel.Children.Count - 1];
        return VisualTreeHelper.GetOffset(last).X + last.ActualWidth + last.Margin.Right;
    }

    /// <summary>
    /// The toolbar is laid out before any pair is shown, with the names empty; the names then
    /// get their text. WPF measures a changed child with the last constraint it was given, and
    /// the panel's first version had left each name a constraint of its own, empty, width, so
    /// the names never grew (the screenshot of 2026-10-30).
    /// </summary>
    [Fact]
    public async Task R065_NamesSetAfterTheFirstLayout_TakeTheirWidth()
    {
        await WpfThread.RunAsync(() =>
        {
            (PairLabelPanel panel, TextBlock nameA, TextBlock nameB, double fixedWidth) = Label();
            nameA.Text = string.Empty;
            nameB.Text = string.Empty;
            panel.Measure(new Size(1270, 26));
            panel.Arrange(new Rect(0, 0, panel.DesiredSize.Width, 26));

            nameA.Text = "de_printat.png";
            nameB.Text = "de_printat_PRINT_preview_16bit.tif";
            panel.UpdateLayout();

            double naturalA = Natural(Name("de_printat.png"));
            double naturalB = Natural(Name("de_printat_PRINT_preview_16bit.tif"));
            Assert.Equal(naturalA, nameA.DesiredSize.Width, 0.5);
            Assert.Equal(naturalB, nameB.DesiredSize.Width, 0.5);
            Assert.Equal(naturalA + naturalB + fixedWidth, panel.DesiredSize.Width, 0.5);
        });
    }

    /// <summary>The label as the toolbar builds it: name, button, "vs", name, button.</summary>
    private static (PairLabelPanel Panel, TextBlock NameA, TextBlock NameB, double FixedWidth) Label()
    {
        var panel = new PairLabelPanel();
        TextBlock nameA = Name("de_printat.png");
        TextBlock nameB = Name("de_printat_PRINT_preview_16bit.tif");
        TextBlock vs = Name("vs");
        PairLabelPanel.SetIsName(vs, false);
        Button buttonA = ChangeButton("Change A…");
        Button buttonB = ChangeButton("Change B…");
        panel.Children.Add(nameA);
        panel.Children.Add(buttonA);
        panel.Children.Add(vs);
        panel.Children.Add(nameB);
        panel.Children.Add(buttonB);
        return (panel, nameA, nameB, Natural(buttonA) + Natural(vs) + Natural(buttonB));
    }

    private static TextBlock Name(string text)
    {
        var name = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 12,
            Margin = new Thickness(0, 0, 3, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        PairLabelPanel.SetIsName(name, true);
        return name;
    }

    private static Button ChangeButton(string text) => new()
    {
        Content = text,
        FontSize = 12,
        Height = 26,
        Margin = new Thickness(3, 0, 3, 0),
        Padding = new Thickness(10, 0, 10, 0),
    };

    /// <summary>The width an element takes with nothing in its way, margin included.</summary>
    private static double Natural(UIElement element)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return element.DesiredSize.Width;
    }
}
