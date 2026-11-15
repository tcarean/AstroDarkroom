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

// TonesRangeTests.cs - the "Show intervals" list of the Tonuri tab (R-047, ADR 0100).

using AstroPhotoAnalyzer.Tests;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AstroPhotoAnalyzer.Wpf.Tests;

[Collection("fereastra")]
public class TonesRangeTests
{
    readonly StaFixture _sta;

    public TonesRangeTests(StaFixture sta) => _sta = sta;

    static AnalysisResult Result(Raster r) => AnalysisRun.Execute(r, "memorie", Scenes.OnBuiltForPaper(null));

    static IEnumerable<T> Descendants<T>(DependencyObject d) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(d, i);
            if (child is T t) yield return t;
            foreach (T x in Descendants<T>(child)) yield return x;
        }
    }

    /// <summary>The table's texts: the header, the rows, the footer.</summary>
    static List<string> Texts(TonesTabViewModel vm) => vm.SimulationRows.Select(r => r.Text).ToList();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R047_TheTableIsUnchangedUntilARangeIsChosen(bool english)
    {
        using IDisposable scope = Tr.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        AnalysisResult result = Result(WcsFixtures.BandScene());
        var vm = new TonesTabViewModel();
        Assert.Equal(0, vm.SelectedRange);
        Assert.Equal(
            new[]
            {
                Tr.Pick("None", "Niciunul"),
                Tr.Pick("The middle half", "Jumatatea din mijloc"),
                Tr.Pick($"Without {Thresholds.RangeWideCut * 100:0} % at each end", $"Fara cate {Thresholds.RangeWideCut * 100:0} % la capete"),
            },
            vm.RangeChoices);

        vm.Populate(result);
        Assert.Equal(0, vm.SelectedRange);
        Assert.Null(vm.RangeNote);
        var expected = new List<string> { ReportWriter.SimulationHeader };
        expected.AddRange(result.Simulation.Rows.Select(ReportWriter.SimulationLine));
        expected.Add(ReportWriter.SimulationFooter(result.Simulation));
        Assert.Equal(expected, Texts(vm));
    }

    [Fact]
    public void R047_ChoosingARangeRewritesTheTableAndKeepsThePreviewButtons()
    {
        AnalysisResult result = Result(WcsFixtures.BandScene());
        var vm = new TonesTabViewModel();
        vm.Populate(result);
        List<string> plain = Texts(vm);
        string definition = vm.Definition, exact = vm.ExactLine;
        var changed = new List<string>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? "");

        foreach (var (index, kind) in new[] { (1, RangeKind.MiddleHalf), (2, RangeKind.Wide) })
        {
            changed.Clear();
            vm.SelectedRange = index;
            Assert.Contains(nameof(TonesTabViewModel.SimulationRows), changed);
            Assert.Contains(nameof(TonesTabViewModel.RangeNote), changed);
            var expected = new List<string> { ReportWriter.SimulationHeaderFor(kind) };
            expected.AddRange(result.Simulation.Rows.Select(row => ReportWriter.SimulationLine(row, kind)));
            expected.Add(ReportWriter.SimulationFooter(result.Simulation));
            Assert.Equal(expected, Texts(vm));
            Assert.NotEqual(plain, Texts(vm));
            Assert.Contains("(interval)", vm.SimulationRows[0].Text);

            // The header and the footer have no button; every row keeps its own, and its target.
            Assert.Null(vm.SimulationRows[0].Preview);
            Assert.Null(vm.SimulationRows[^1].Preview);
            Assert.All(vm.SimulationRows.Skip(1).Take(result.Simulation.Rows.Count), row => { Assert.NotNull(row.Preview); Assert.NotNull(row.Target); });
            // Nothing else follows the choice.
            Assert.Equal(definition, vm.Definition);
            Assert.Equal(exact, vm.ExactLine);
        }

        vm.SelectedRange = 0;
        Assert.Equal(plain, Texts(vm));
    }

    [Fact]
    public void R047_TheNoteStandsUnderTheTableOnlyWhileARangeIsShown()
    {
        AnalysisResult result = Result(WcsFixtures.BandScene());
        var vm = new TonesTabViewModel();
        vm.Populate(result);
        Assert.Null(vm.RangeNote);
        vm.SelectedRange = 1;
        Assert.Equal(ReportWriter.RangeNote(result.Simulation, RangeKind.MiddleHalf), vm.RangeNote);
        Assert.StartsWith("In paranteze: ", vm.RangeNote);
        vm.SelectedRange = 2;
        Assert.Equal(ReportWriter.RangeNote(result.Simulation, RangeKind.Wide), vm.RangeNote);
        vm.SelectedRange = 0;
        Assert.Null(vm.RangeNote);
    }

    [Fact]
    public void R047_TheChoiceSurvivesAnotherImage()
    {
        var vm = new TonesTabViewModel();
        // Chosen before any image: the first table already carries the range.
        vm.SelectedRange = 2;
        Assert.Null(vm.RangeNote);
        AnalysisResult first = Result(WcsFixtures.BandScene());
        vm.Populate(first);
        Assert.Equal(ReportWriter.SimulationHeaderFor(RangeKind.Wide), vm.SimulationRows[0].Text);
        Assert.Equal(ReportWriter.RangeNote(first.Simulation, RangeKind.Wide), vm.RangeNote);

        // Another image: the choice stays, the table and the note are the new image's.
        vm.Clear();
        Assert.Equal(2, vm.SelectedRange);
        Assert.Null(vm.RangeNote);
        AnalysisResult second = Result(Scenes.DarkSky());
        vm.Populate(second);
        Assert.Equal(2, vm.SelectedRange);
        Assert.Equal(ReportWriter.SimulationLine(second.Simulation.Rows[0], RangeKind.Wide), vm.SimulationRows[1].Text);
        Assert.Equal(ReportWriter.RangeNote(second.Simulation, RangeKind.Wide), vm.RangeNote);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R047_TheGuideExplainsTheTwoFiguresAndTheIntervals(bool english)
    {
        using IDisposable scope = Tr.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        var vm = new TonesTabViewModel();
        vm.Populate(Result(Scenes.DarkSky()));
        List<string> leads = vm.Guide.Select(g => g.Lead).ToList();
        int dustAndSky = leads.IndexOf(Tr.Pick("The dust and the sky.", "Praful si cerul."));
        Assert.True(dustAndSky >= 0);
        Assert.Equal(Tr.Pick("The sky's L* and the dust's L*.", "L* cer si L* praf."), leads[dustAndSky + 1]);
        Assert.Equal(Tr.Pick("The intervals.", "Intervalele."), leads[dustAndSky + 2]);

        // The sentence that defined the two figures left the first paragraph for the second.
        string first = vm.Guide[dustAndSky].Text, figures = vm.Guide[dustAndSky + 1].Text, intervals = vm.Guide[dustAndSky + 2].Text;
        string threshold = Tr.Pick($"the threshold under which {Thresholds.DustClassFraction * 100:0} % of the dust patches lie", $"pragul sub care stau {Thresholds.DustClassFraction * 100:0} % din dalele de praf");
        Assert.DoesNotContain(threshold, first);
        Assert.Contains(threshold, figures);
        Assert.Contains(Tr.Pick("The sky's L* is the average sky: the middle value of the sky patches", "L*-ul cerului e cerul mediu: valoarea din mijloc a dalelor de cer"), figures);
        Assert.Contains(Tr.Pick($"at least {Thresholds.DustClassMinFraction * 100:0} % of the patches", $"cel putin {Thresholds.DustClassMinFraction * 100:0} % din dale"), figures);
        Assert.Contains(Tr.Pick($"the darkest {Thresholds.SkyDarkestFraction * 100:0} % of the sky patches", $"cel mai intunecat {Thresholds.SkyDarkestFraction * 100:0} % din dalele de cer"), figures);

        // The intervals: each choice by its name and its cut, from Thresholds.
        Assert.Contains(Tr.Pick("\"Show intervals\"", "\"Arata intervalele\""), intervals);
        Assert.Contains(Tr.Pick($"\"The middle half\" leaves out the darkest {Thresholds.RangeHalfCut * 100:0} % and the lightest {Thresholds.RangeHalfCut * 100:0} % of the patches",
                                $"\"Jumatatea din mijloc\" lasa deoparte cele mai intunecate {Thresholds.RangeHalfCut * 100:0} % si cele mai deschise {Thresholds.RangeHalfCut * 100:0} % din dale"), intervals);
        Assert.Contains(Tr.Pick($"\"Without {Thresholds.RangeWideCut * 100:0} % at each end\" leaves out {Thresholds.RangeWideCut * 100:0} % at each end",
                                $"\"Fara cate {Thresholds.RangeWideCut * 100:0} % la capete\" lasa deoparte cate {Thresholds.RangeWideCut * 100:0} %"), intervals);
        Assert.Contains(Tr.Pick("the verdict reads the figure before the brackets", "verdictul citeste cifra dinaintea parantezei"), intervals);
        Assert.Contains(Tr.Pick("the dust never goes over the average sky", "praful nu trece insa niciodata peste cerul mediu"), intervals);
        Assert.Contains(Tr.Pick("it can fall outside the dust zone's interval", "ea poate cadea in afara intervalului zonei de praf"), intervals);

        // Plain words (ADR 0071): none of the method's words.
        var methodWords = new System.Text.RegularExpressions.Regex(@"\b(mediana|median|percentil\w*|quartil\w*|cuartil\w*)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Assert.False(methodWords.IsMatch(figures), figures);
        Assert.False(methodWords.IsMatch(intervals), intervals);
    }

    /// <summary>
    /// The list sits in the rendered tab, bound to the view-model, and the note under the table
    /// shows only while a range is chosen.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void R047_TheTabShowsTheListAndTheNote(bool english)
        => _sta.Invoke(() =>
        {
            using IDisposable scope = Tr.Use(english ? UiLanguage.English : UiLanguage.Romanian);
            var vm = new TonesTabViewModel();
            vm.Populate(Result(WcsFixtures.BandScene()));
            var view = new TonesView { DataContext = vm };
            WindowSmokeTests.Render(view, 1400, 2000);

            ComboBox list = Assert.Single(Descendants<ComboBox>(view));
            Assert.Equal(vm.RangeChoices, list.Items.Cast<string>());
            Assert.Equal(0, list.SelectedIndex);
            Assert.Equal(Tr.Pick("Show intervals", "Arata intervalele"), System.Windows.Automation.AutomationProperties.GetName(list));
            Assert.Contains(Descendants<TextBlock>(view), t => t.Text == Tr.Pick("Show intervals", "Arata intervalele"));
            // The note's place is there, collapsed: an unshown view has no IsVisible, so the
            // test reads Visibility, which is what the binding sets.
            string noteStart = Tr.Pick("In brackets: ", "In paranteze: ");
            Assert.DoesNotContain(Descendants<TextBlock>(view), t => t.Visibility == Visibility.Visible && t.Text.StartsWith(noteStart, StringComparison.Ordinal));

            // The list drives the view-model, and the note appears under the table.
            list.SelectedIndex = 1;
            Assert.Equal(1, vm.SelectedRange);
            WindowSmokeTests.Render(view, 1400, 2000, name: english ? "tab-tonuri-interval-en" : "tab-tonuri-interval");
            TextBlock note = Assert.Single(Descendants<TextBlock>(view), t => t.Visibility == Visibility.Visible && t.Text == vm.RangeNote);
            TextBlock header = Assert.Single(Descendants<TextBlock>(view), t => t.Text == vm.SimulationRows[0].Text);
            Assert.True(note.TransformToAncestor(view).Transform(new Point(0, 0)).Y > header.TransformToAncestor(view).Transform(new Point(0, 0)).Y);
            // The widest row fits in the card: its text is not cut off at the right.
            foreach (TextBlock row in Descendants<TextBlock>(view).Where(t => vm.SimulationRows.Any(r => r.Text == t.Text)))
            {
                row.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                Assert.True(row.ActualWidth >= row.DesiredSize.Width - 0.5, row.Text);
            }
        });
}
