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

// DescribedRowsTests.cs - where the texts of the described panels stop (R-174).

using AstroPhotoAnalyzer.Tests;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AstroPhotoAnalyzer.Wpf.Tests;

[Collection("fereastra")]
public class DescribedRowsTests
{
    readonly StaFixture _sta;

    public DescribedRowsTests(StaFixture sta) => _sta = sta;

    /// <summary>The gap between a table's texts and its widest value, in px (R-174).</summary>
    const double Gap = 24;

    static IEnumerable<T> Descendants<T>(DependencyObject d) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(d, i);
            if (child is T t) yield return t;
            foreach (T x in Descendants<T>(child)) yield return x;
        }
    }

    static double Left(FrameworkElement el, Visual table) => el.TransformToAncestor(table).Transform(new Point(0, 0)).X;

    /// <summary>
    /// On each of the four tabs, every text of a described row (name, description, note) ends
    /// exactly the gap before the left edge of the table's widest value: never under or between
    /// the values, and no farther from them than the gap.
    /// </summary>
    [Fact]
    public void R174_EveryTextStopsTheGapBeforeTheWidestValueOfItsTable()
        => _sta.Invoke(() =>
        {
            AnalysisResult result = AnalysisRun.Execute(Scenes.DarkSky(), "memorie", Scenes.OnBuiltForPaper(null));
            var tones = new TonesTabViewModel();
            var export = new ExportTabViewModel();
            var resolution = new ResolutionTabViewModel();
            var background = new BackgroundTabViewModel();
            tones.Populate(result);
            export.Populate(result);
            resolution.Populate(result);
            background.Populate(result);

            var template = (DataTemplate)Application.Current.FindResource("DescribedMetricRowTemplate");
            int tables = 0;
            foreach (FrameworkElement view in new FrameworkElement[]
            {
                new TonesView { DataContext = tones },
                new ExportView { DataContext = export },
                new ResolutionView { DataContext = resolution },
                new BackgroundView { DataContext = background },
            })
            {
                WindowSmokeTests.Render(view, 1400, 3000);
                foreach (ItemsControl table in Descendants<ItemsControl>(view).Where(x => x.ItemTemplate == template))
                {
                    tables++;
                    var rows = Enumerable.Range(0, table.Items.Count)
                        .Select(i => (Grid)VisualTreeHelper.GetChild(table.ItemContainerGenerator.ContainerFromIndex(i), 0))
                        .ToList();
                    Assert.True(rows.Count > 1, view.GetType().Name);
                    List<TextBlock> values = rows.Select(g => g.Children.OfType<TextBlock>().Single(t => Grid.GetColumn(t) == 1)).ToList();
                    double widest = values.Max(v => v.ActualWidth);
                    double valuesLeft = values.Min(v => Left(v, table));
                    foreach (TextBlock v in values)
                        Assert.Equal(valuesLeft + widest, Left(v, table) + v.ActualWidth, 1);

                    // Visibility, not IsVisible: a view rendered outside a window is never "visible".
                    List<TextBlock> texts = rows.SelectMany(g => Descendants<TextBlock>(g))
                        .Where(t => !values.Contains(t) && t.Visibility == Visibility.Visible).ToList();
                    Assert.True(texts.Count >= 2 * rows.Count, view.GetType().Name);
                    foreach (TextBlock text in texts)
                        Assert.True(Math.Abs(Left(text, table) + text.ActualWidth - (valuesLeft - Gap)) <= 1,
                            $"{view.GetType().Name}: '{text.Text}' ends at {Left(text, table) + text.ActualWidth:F0} px, the widest value starts at {valuesLeft:F0} px");
                }
            }
            Assert.Equal(4, tables);
        });
}
