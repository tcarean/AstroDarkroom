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

using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DarksCheck.Core;
using DarksCheck.Tests;

namespace DarksCheck.Wpf.Tests;

/// <summary>
/// The window's language (R-094, ADR 0021): <c>--lang</c> read by <see cref="App"/>, the folder
/// argument found without it, and every tab rendered in English with no Romanian word left. The
/// assembly runs in Romanian (<see cref="RomanianByDefault"/>); the English pass opens its own
/// scope on the UI thread, which the analysis' tasks inherit.
/// </summary>
[Collection("window")]
public partial class LanguageWindowTests
{
    private readonly StaFixture _sta;

    public LanguageWindowTests(StaFixture sta) => _sta = sta;

    // Words that only Romanian text holds; the paths of the synthetic sessions are temp folders and
    // the file names DSC_nnnn, so none of these can come from them.
    [GeneratedRegex(@"\b(cadre|cadru|cadrele|fata de|dark-uri|dark-urile|sesiunii|sesiune|NEMASURAT|ATENTIE|EROARE|Rezumat|Raport|Alege|pixeli|fierbinti|lumina|verificarea|biblioteca|partener|partenerul|eticheta|zgomot|masurat|niciun|niciunul|amandoua|scadere)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RomanianWord();

    [Theory]
    [InlineData(new[] { "--lang", "ro" }, false, null)]
    [InlineData(new[] { "--LANG=RO", "C:\\s" }, false, null)]
    [InlineData(new[] { "--lang=en" }, true, null)]
    [InlineData(new[] { "--lang=ro", "--lang", "en" }, true, null)]
    [InlineData(new[] { "C:\\s" }, true, null)]
    [InlineData(new string[0], true, null)]
    [InlineData(new[] { "--lang", "fr" }, true, "fr")]
    [InlineData(new[] { "C:\\s", "--lang" }, true, "")]
    public void R094_TheWindowTakesItsLanguageFromLang(string[] args, bool english, string? bad)
    {
        UiLanguage lang = App.LanguageFromArgs(args, out string? badValue);

        Assert.Equal(english ? UiLanguage.English : UiLanguage.Romanian, lang);
        Assert.Equal(bad, badValue);
    }

    [Fact]
    public void R094_WithoutArgumentsTheWindowIsEnglish()
    {
        Assert.Equal(UiLanguage.English, App.LanguageFromArgs(null, out string? badValue));
        Assert.Null(badValue);
    }

    [Fact]
    public void R094_TheFolderArgumentIsFoundWithoutLangAndItsValue()
    {
        using TempWorkspace ws = new();
        string a = Directory.CreateDirectory(Path.Combine(ws.Dir, "a")).FullName;
        string b = Directory.CreateDirectory(Path.Combine(ws.Dir, "b")).FullName;

        // the spaced form takes the next argument as its value, even an existing folder
        Assert.Null(App.FolderFromArgs(["--lang", a]));
        Assert.Equal(b, App.FolderFromArgs(["--lang", a, "--lang=ro", b]));
        Assert.Equal(a, App.FolderFromArgs([a, "--lang", "ro"]));
    }

    /// <summary>
    /// Every tab with a real result, in English: the tab headers, the cards, the tables, the hints and
    /// the report hold no Romanian word, and the first tab is Summary.
    /// </summary>
    [Fact]
    public async Task R094_EveryTabRendersInEnglish()
    {
        using TestSettings.Own _ = new();
        using SessionFixture f = WindowFixture.Session();
        using SessionFixture p = WindowFixture.Partner();

        await _sta.InvokeAsync(async () =>
        {
            using (Ui.Use(UiLanguage.English))
            {
                MainViewModel vm = new() { Partner = p.Root };
                await vm.OpenAsync(f.Root);
                Assert.True(vm.HasResult);

                List<string> texts = [vm.Report, vm.Status ?? "", vm.MaxFramesTip, vm.IsoFactorTip, vm.DoublingTip];
                FrameworkElement window = (FrameworkElement)new MainWindow(vm).Content;
                Render(window);
                TabControl tabs = Descendants(window).OfType<TabControl>().Single();
                Assert.Equal("Summary", ((TabItem)tabs.Items[0]).Header);
                texts.AddRange(tabs.Items.OfType<TabItem>().Select(t => t.Header as string ?? ""));
                texts.AddRange(TextsOf(window));

                foreach (FrameworkElement view in new FrameworkElement[]
                {
                    new SummaryView { DataContext = vm }, new FramesView { DataContext = vm }, new MapsView { DataContext = vm },
                    new HotPixelsView { DataContext = vm }, new LightsView { DataContext = vm }, new IndexView { DataContext = vm },
                    new ScaleView { DataContext = vm },
                })
                {
                    Render(view, 1360, 1800);
                    texts.AddRange(TextsOf(view));
                }

                AboutWindow about = new();
                Render((FrameworkElement)about.Content, 600, 760);
                texts.Add(about.Title);
                texts.AddRange(TextsOf((FrameworkElement)about.Content));

                List<string> romanian = texts.Where(t => RomanianWord().IsMatch(t)).Distinct(StringComparer.Ordinal).ToList();
                Assert.True(romanian.Count == 0, "Romanian left in the English window:\n" + string.Join("\n", romanian));
            }
        });
    }

    private static void Render(FrameworkElement el, int w = 1360, int h = 820)
    {
        el.Measure(new Size(w, h));
        el.Arrange(new Rect(0, 0, w, h));
        el.UpdateLayout();
    }

    /// <summary>Every text a rendered tree shows: the text blocks, and the hints as strings.</summary>
    private static IEnumerable<string> TextsOf(DependencyObject root)
    {
        foreach (DependencyObject d in Descendants(root))
        {
            if (d is TextBlock t && !string.IsNullOrEmpty(t.Text))
            {
                yield return t.Text;
            }

            if (d is FrameworkElement fe && fe.ToolTip is string tip)
            {
                yield return tip;
            }
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject d)
    {
        int n = VisualTreeHelper.GetChildrenCount(d);
        for (int i = 0; i < n; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(d, i);
            yield return child;
            foreach (DependencyObject grandchild in Descendants(child))
            {
                yield return grandchild;
            }
        }
    }
}
