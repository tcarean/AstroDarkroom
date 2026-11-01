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
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LightsCheck.Core;
using LightsCheck.Tests;

namespace LightsCheck.Wpf.Tests;

/// <summary>
/// The window in English (R-063): the start-up reads <c>--lang</c>, every screen built with real
/// results shows no Romanian word, and a verdict colours its chip the same in both languages.
/// The assembly runs its tests one at a time, so a test may set <see cref="Ui.Default"/>; each
/// puts back the assembly's Romanian when it ends.
/// </summary>
[Collection("window")]
public class WindowLanguageTests
{
    private readonly StaFixture _sta;

    public WindowLanguageTests(StaFixture sta) => _sta = sta;

    [Fact]
    public void R063_TheStartUpReadsLangAndNeverTakesItsValueForTheFolder()
    {
        using TempWorkspace ws = new();
        try
        {
            (string[] args, string? bad) = App.ApplyCommandLine(["--lang", "en", ws.Dir]);
            Assert.Equal(UiLanguage.English, Ui.Default);
            Assert.Null(bad);
            Assert.Equal(ws.Dir, App.FolderFromArgs(args));

            App.ApplyCommandLine([ws.Dir, "--lang=RO"]);
            Assert.Equal(UiLanguage.Romanian, Ui.Default);

            // The spaced form takes the next argument whatever it is: an existing folder there is
            // a language not understood, said in a message, and the window goes on in English.
            (args, bad) = App.ApplyCommandLine(["--lang", ws.Dir]);
            Assert.Equal(ws.Dir, bad);
            Assert.Null(App.FolderFromArgs(args));
            Assert.Equal(UiLanguage.English, Ui.Default);

            App.ApplyCommandLine([ws.Dir]);
            Assert.Equal(Ui.StartupDefault, Ui.Default);
        }
        finally
        {
            Ui.Default = UiLanguage.Romanian;
        }
    }

    [Fact]
    public async Task R063_EveryScreenIsInEnglishWithLangEn()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.ClassesPattern, biases: 2);
        File.WriteAllBytes(Path.Combine(f.LightsDir, "DSC_0009.fit"), new byte[100]);
        using TempWorkspace ws = new();
        Ui.Default = UiLanguage.English;
        try
        {
            await _sta.InvokeAsync(async () =>
            {
                MainViewModel vm = new();
                await vm.OpenAsync(f.Root);
                Assert.True(vm.HasResult);

                List<string> texts = [];
                texts.AddRange(Texts(Render((FrameworkElement)new MainWindow(vm).Content, "en-window")));
                texts.AddRange(Texts(Render(new SummaryView { DataContext = vm }, "en-tab-summary", h: 1400)));
                texts.AddRange(Texts(Render(new DitherView { DataContext = vm }, "en-tab-dither", h: 1200)));
                texts.AddRange(Texts(Render(new QualityView { DataContext = vm }, "en-tab-quality", h: 1200)));
                texts.AddRange(Texts(Render(new RejectedView { DataContext = vm }, "en-tab-rejected")));
                vm.SelectedFrame = vm.Frames[2];
                await vm.CropsPending;
                Assert.NotEmpty(vm.Crops);
                texts.AddRange(Texts(Render(new FramesView { DataContext = vm }, "en-tab-frames")));
                texts.AddRange(Texts(Render((FrameworkElement)new AboutWindow(SystemParameters.WorkArea).Content, "en-about", 600, 1000)));

                // What the charts draw themselves, outside the visual tree.
                texts.AddRange(vm.StepGuides.Concat(vm.ElongationGuides).Select(g => g.Label));
                texts.AddRange([vm.MapXLabel, vm.MapYLabel, vm.Report, WindowTexts.GridNote("10"), WindowTexts.Busy(0, 8), WindowTexts.Busy(3, 8), WindowTexts.BusyRegistering, WindowTexts.Cancelled, WindowTexts.ChooseTitle, WindowTexts.ExportTitle, WindowTexts.CropsBusy, WindowTexts.NoCrops]);
                texts.AddRange(vm.StepPoints.Select(p => p.Tip ?? ""));

                vm.Export(ws.Dir);
                texts.Add(vm.Status ?? "");

                MainViewModel problem = new();
                await problem.OpenAsync(Path.Combine(ws.Dir, "missing"));
                Assert.True(problem.HasProblem);
                texts.AddRange(Texts(Render((FrameworkElement)new MainWindow(problem).Content, "en-window-problem")));

                MainViewModel empty = new();
                texts.AddRange(Texts(Render((FrameworkElement)new MainWindow(empty).Content, "en-window-empty")));

                AssertNoRomanian(string.Join("\n", texts).Replace(f.Root, "", StringComparison.Ordinal).Replace(ws.Dir, "", StringComparison.Ordinal));
                Assert.Contains(texts, t => t == "Summary");
            });
        }
        finally
        {
            Ui.Default = UiLanguage.Romanian;
        }
    }

    [Fact]
    public async Task R063_TheSameWindowInRomanianStillShowsRezumat()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.Pattern, biases: 2);
        await _sta.InvokeAsync(async () =>
        {
            MainViewModel vm = new();
            await vm.OpenAsync(f.Root);
            List<string> texts = Texts(Render((FrameworkElement)new MainWindow(vm).Content, null)).ToList();
            Assert.Contains("Rezumat", texts);
            Assert.DoesNotContain("Summary", texts);
        });
    }

    [Fact]
    public void R063_AVerdictColoursTheSameInBothLanguages()
    {
        using SessionFixture f = new SessionFixture().WithStarSeries(SessionFixture.ClassesPattern, biases: 2);
        SeriesResult r = SeriesRun.Run(f.Root, new SeriesOptions(Threads: 2));

        static List<string?> Words(SeriesResult r, UiLanguage lang)
        {
            using IDisposable _ = Ui.Use(lang);
            return [.. SeriesReport.SummaryRows(r).Concat(SeriesReport.FocusRows(r)).Concat(SeriesReport.DewRows(r)).Concat(SeriesReport.DitherRows(r)).Select(row => row.Verdict),
                    .. SeriesReport.FrameRows(r).Select(row => row.Class)];
        }

        List<string?> en = Words(r, UiLanguage.English), ro = Words(r, UiLanguage.Romanian);
        Assert.Equal(ro.Count, en.Count);
        for (int i = 0; i < en.Count; i++)
        {
            Assert.True(VerdictPalette.Severity(en[i]) == VerdictPalette.Severity(ro[i]), $"'{en[i]}' and '{ro[i]}' colour differently");
        }

        // The words the fixture does not reach, written as the core writes them.
        (string En, string Ro)[] pairs =
        [
            ("GOOD", "BUN"), ("GOOD", "BUNA"), ("NO", "NU"), ("POSSIBLE", "POSIBIL"), ("MARGINAL", "LA LIMITA"),
            ("WARNING", "ATENTIE"), ("UNDETERMINED", "NEDETERMINAT"), ("NOT MEASURED", "NEMASURAT"), ("unread", "necitit"),
            ("BAD", "SLAB"), ("ERROR: x", "EROARE: x"), ("NO walking noise, WARNING on hot pixels", "FARA walking noise, ATENTIE la pixeli calzi"),
            ("INSUFFICIENT for walking noise, OK for hot pixels", "INSUFICIENT pentru walking noise, OK pentru pixeli calzi"),
            ("WARNING on hot pixels, OK for walking noise", "ATENTIE la pixeli calzi, OK pentru walking noise"),
        ];
        foreach ((string e, string o) in pairs)
        {
            Assert.True(VerdictPalette.Severity(e) == VerdictPalette.Severity(o), $"'{e}' and '{o}' colour differently");
        }
    }

    /// <summary>Lays an element out and renders it, saving a PNG when asked to (as the smoke tests).</summary>
    private static FrameworkElement Render(FrameworkElement el, string? name, int w = 1360, int h = 820)
    {
        el.Measure(new Size(w, h));
        el.Arrange(new Rect(0, 0, w, h));
        el.UpdateLayout();
        RenderTargetBitmap bmp = new(w, h, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(el);

        string? dir = Environment.GetEnvironmentVariable("LIGHTSCHECK_DUMP_SCREENS");
        if (!string.IsNullOrEmpty(dir) && name is not null)
        {
            Directory.CreateDirectory(dir);
            PngBitmapEncoder enc = new();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using FileStream fs = File.Create(Path.Combine(dir, name + ".png"));
            enc.Save(fs);
        }

        return el;
    }

    /// <summary>Every text of the visual tree under an element: text blocks with their runs, text boxes, string contents and headers, tooltips.</summary>
    private static IEnumerable<string> Texts(DependencyObject root)
    {
        Stack<DependencyObject> todo = new([root]);
        while (todo.Count > 0)
        {
            DependencyObject d = todo.Pop();
            switch (d)
            {
                case TextBlock tb:
                    yield return tb.Text;
                    foreach (Run run in tb.Inlines.OfType<Hyperlink>().SelectMany(h => h.Inlines.OfType<Run>()))
                    {
                        yield return run.Text;
                    }

                    break;
                case TextBox box:
                    yield return box.Text;
                    break;
                case HeaderedContentControl { Header: string header }:
                    yield return header;
                    break;
                case ContentControl { Content: string content }:
                    yield return content;
                    break;
            }

            if (d is FrameworkElement { ToolTip: string tip })
            {
                yield return tip;
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++)
            {
                todo.Push(VisualTreeHelper.GetChild(d, i));
            }
        }
    }

    // Romanian words and stems that English never holds.
    private static readonly string[] RomanianWords =
    [
        " si ", " sau ", " nu ", " din ", " cu ", " pe ", " de ", " la ", " fara ", " sub ", " mai ",
        "cadru", "cadre", "Cadre", "fisier", "eroare", "EROARE", "ATENTIE", "alungire", "ALUNGIRE", "dara", "roua", "ROUA",
        "miscar", "stele", "STELE", "fond", "FOND", "zgomot", "ZGOMOT", "necunoscut", "niciun", "SESIUNE", "REZUMAT", "Rezumat",
        "RESPINSE", "Respinse", "nedeterminat", "NEDETERMINAT", "pauz", "expunere", "EXPUNERE", "obiectiv", "aparat", "grade",
        "Alege", "Opreste", "Despre", "Scrie", "Calitate", "Raport", "FIRE", "NORD", "HARTA", "PASUL", "DISTANTA", "DECUPAJE",
        "Inchide", "LICENTA", "TERTE", "nimic", "decupaj", "pozitii", "acoperire", "intindere", "aliniere", "perechi", "scara",
        "verde", "BUN", "LA LIMITA", "SLAB", "necitit", "inregistr", "Acest program", "grila", "Scris", "Unealta", "Verificare",
        "Folderul", "Analiza", "sesiun", "centru", "stanga", "dreapta",
    ];

    /// <summary>The English texts hold none of the Romanian words.</summary>
    private static void AssertNoRomanian(string english)
    {
        foreach (string word in RomanianWords)
        {
            int at = english.IndexOf(word, StringComparison.Ordinal);
            Assert.True(at < 0, $"Romanian '{word.Trim()}' on an English screen: ...{english[Math.Max(0, at - 60)..Math.Min(english.Length, at + 60)]}...");
        }
    }
}
