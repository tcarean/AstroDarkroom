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

namespace FlatCheck.Wpf.Tests;

/// <summary>
/// The End to end tab's explanations in place (R-105): a line under each series figure, a
/// tooltip on each column header, a paragraph under the map. The texts are documentation, not
/// results: they are there in both languages, quote the thresholds of Th rather than retyping
/// them, and do not depend on what the run produced.
/// </summary>
[Collection("window")]
public class LightExplanationsTests
{
    readonly StaFixture _sta;

    public LightExplanationsTests(StaFixture sta) => _sta = sta;

    static string[] Notes(LightTabViewModel vm)
        => new[] { vm.MeanRadialNote, vm.SpreadNote, vm.SemNote, vm.CorrelationNote };

    static string[] Tips(LightTabViewModel vm)
        => new[] { vm.FrameTip, vm.RadialTip, vm.SkyTip, vm.TilesTip };

    static string[] All(LightTabViewModel vm)
        => Notes(vm).Concat(Tips(vm)).Append(vm.MapNote).ToArray();

    /// <summary>
    /// Nine texts, each written twice. Without diacritics, like every other text the window
    /// shows (the console report and the saved file have none).
    /// </summary>
    [Fact]
    public void R105_EveryExplanationIsWrittenInBothLanguagesWithoutDiacritics()
    {
        string[] english, romanian;
        using (Ui.Use(UiLanguage.English)) english = All(new LightTabViewModel());
        using (Ui.Use(UiLanguage.Romanian)) romanian = All(new LightTabViewModel());

        Assert.Equal(9, english.Length);
        for (int i = 0; i < english.Length; i++)
        {
            Assert.False(string.IsNullOrWhiteSpace(english[i]), $"English text {i} is empty");
            Assert.False(string.IsNullOrWhiteSpace(romanian[i]), $"Romanian text {i} is empty");
            Assert.NotEqual(english[i], romanian[i]);
            Assert.All(english[i] + romanian[i], c => Assert.True(c < 128, $"non-ASCII character: '{c}'"));
        }
    }

    /// <summary>
    /// The figures in the texts are Th's, formatted from Th: a recalibration cannot leave a
    /// stale number on screen.
    /// </summary>
    [Fact]
    public void R105_TheThresholdsInTheTextsAreReadFromTh()
    {
        using var _ = Ui.Use(UiLanguage.English);
        var vm = new LightTabViewModel();

        Assert.Contains($"at least {Th.LightSeriesMinFrames} frames", vm.MeanRadialNote);
        Assert.Contains($"Under {Th.LightRadOk:F0} %", vm.MeanRadialNote);
        Assert.Contains($"above {Th.LightRadBad:F0} %", vm.MeanRadialNote);
        Assert.Contains($"Above {Th.LightSeriesSemMax:F2} points", vm.SemNote);
        Assert.Contains($"above {Th.LightSeriesSkyCorr:F2} in absolute value", vm.CorrelationNote);
        Assert.Contains($"at least {Th.LightSeriesSkySpanMin:F0} points", vm.CorrelationNote);
        Assert.Contains($"above {Th.LightPlaneWarn:F0} %", vm.SkyTip);
        Assert.Contains(SeriesFinder.DefaultPattern, vm.FrameTip);
        Assert.Contains("r_pp_light_*", vm.FrameTip);
    }

    /// <summary>
    /// The sign convention of the map is the one the figures use: blue corners are a background
    /// that falls toward the edges, which is under-correction, the positive sign.
    /// </summary>
    [Fact]
    public void R105_TheMapNoteReadsBlueCornersAsUnderCorrection()
    {
        using var _ = Ui.Use(UiLanguage.English);
        var vm = new LightTabViewModel();

        Assert.Contains("blue corners", vm.MapNote);
        Assert.Contains("under-correction", vm.MapNote);
        Assert.Contains("red corners", vm.MapNote);
        Assert.Contains("over-correction", vm.MapNote);
        Assert.True(vm.MapNote.IndexOf("blue corners") < vm.MapNote.IndexOf("under-correction"));
        Assert.True(vm.MapNote.IndexOf("red corners") < vm.MapNote.IndexOf("over-correction"));
    }

    /// <summary>
    /// The explanations do not depend on the run: the same before a run, on a refusal (where
    /// only the tooltips are on screen, the figures being hidden) and after a reset.
    /// </summary>
    [Fact]
    public async Task R105_TheExplanationsAreTheSameBeforeARunOnARefusalAndAfterAReset()
    {
        using var f = new SessionFixture().WithFlatMaster()
                                          .WithLightSeries(6, SessionFixture.WideFieldCards);
        var vm = new LightTabViewModel();
        string[] before = All(vm);

        await vm.RunAsync(f.Session, CancellationToken.None);
        Assert.True(vm.Refused);
        Assert.Equal(before, All(vm));

        vm.Reset();
        Assert.Equal(before, All(vm));
    }

    /// <summary>
    /// Where the texts sit: the four tooltips on the four headers, in column order; each note in
    /// the label column of its figure's grid, one column wide, so it never runs under the value;
    /// the map's paragraph in the same grid as the legend, in a row below it.
    /// </summary>
    [Fact]
    public async Task R105_TheHeadersCarryTheTipsAndTheNotesStayInTheLabelColumn()
    {
        using var f = new SessionFixture().WithFlatMaster().WithLightSeries(6);
        var vm = new LightTabViewModel();

        await _sta.InvokeAsync(async () =>
        {
            await vm.RunAsync(f.Session, CancellationToken.None);
            Assert.True(vm.HasResult && !vm.Refused);

            var view = new LightView { DataContext = vm };
            view.Measure(new Size(1360, 2400));
            view.Arrange(new Rect(0, 0, 1360, 2400));
            view.UpdateLayout();
            var blocks = Descendants(view).OfType<TextBlock>().ToList();

            var tips = blocks.Where(b => b.ToolTip is string).Select(b => (string)b.ToolTip).ToList();
            Assert.Equal(Tips(vm), tips);

            foreach (string note in Notes(vm))
            {
                TextBlock b = Assert.Single(blocks, x => x.Text == note);
                Assert.Equal(0, Grid.GetColumn(b));
                Assert.Equal(1, Grid.GetColumnSpan(b));
                Assert.Equal(1, Grid.GetRow(b));
            }

            TextBlock legend = Assert.Single(blocks, x => x.Text == vm.MapLegend);
            TextBlock map = Assert.Single(blocks, x => x.Text == vm.MapNote);
            Assert.Same(VisualTreeHelper.GetParent(legend), VisualTreeHelper.GetParent(map));
            Assert.True(Grid.GetRow(map) > Grid.GetRow(legend));
        });
    }

    static IEnumerable<DependencyObject> Descendants(DependencyObject node)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(node, i);
            yield return child;
            foreach (var d in Descendants(child)) yield return d;
        }
    }
}
