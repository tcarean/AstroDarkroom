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
    [GeneratedRegex(@"\b(cadre|cadru|cadrele|fata de|dark-uri|dark-urile|sesiunii|sesiune|NEMASURAT|ATENTIE|REFA|EROARE|Rezumat|Raport|Alege|pixeli|fierbinti|lumina|verificarea|biblioteca|partener|partenerul|eticheta|zgomot|masurat|niciun|niciunul|amandoua|scadere)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
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

                AboutWindow about = new(new Rect(0, 0, 1920, 1040));
                Render((FrameworkElement)about.Content, 600, 1100);
                texts.Add(about.Title);
                texts.AddRange(TextsOf((FrameworkElement)about.Content));

                List<string> romanian = texts.Where(t => RomanianWord().IsMatch(t)).Distinct(StringComparer.Ordinal).ToList();
                Assert.True(romanian.Count == 0, "Romanian left in the English window:\n" + string.Join("\n", romanian));
            }
        });
    }

    /// <summary>
    /// The Cadre tab in plain words (R-083, ADR 0031): its column names and hints, the table's note,
    /// the reasons of the frames to discard, the chart cards and the guide lines name no part of the
    /// method, in either language.
    /// </summary>
    /// <param name="english">True for the English pass, false for the Romanian one.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task R083_TheFramesTabIsInPlainWords(bool english)
    {
        using TestSettings.Own _ = new();
        (int X, int Y, double Adu)[] hot = Enumerable.Range(0, 10).Select(k => (60 + 7 * k, 100 + 5 * k, 300.0)).ToArray();
        using SessionFixture f = new SessionFixture().WithDarkSeries(12, i => new SyntheticDark { Seed = 100 + i, Hot = hot, HotFactor = 1.2 - 0.4 * Math.Exp(-i / 3.0) }, biases: 3);

        await _sta.InvokeAsync(async () =>
        {
            using (Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian))
            {
                MainViewModel vm = new();
                await vm.OpenAsync(f.Root);
                Assert.True(vm.HasResult);
                FramesView view = new() { DataContext = vm };
                Render(view, 1360, 1800);
                List<string> texts = [.. TextsOf(view), vm.WarmingNote, vm.FrameTableNote, vm.PlateauHeaderTip, vm.HotHeaderTip];
                texts.AddRange(vm.WarmingGuides.Concat(vm.ResidualGuides).Concat(vm.BandingGuides).Select(g => g.Label));

                // the scenes reached their variants, so the words below were looked at
                string all = string.Join("\n", texts);
                Assert.Contains(english ? "HOW MUCH EACH FRAME DIFFERS FROM THE MASTER (ADU)" : "CAT DIFERA FIECARE CADRU DE MASTER (ADU)", all, StringComparison.Ordinal);
                Assert.Contains(english ? "to discard: the hot pixels are at " : "de aruncat: pixelii fierbinti sunt la ", all, StringComparison.Ordinal);
                Assert.Contains(english ? "1.5 times the average noise" : "de 1.5 ori zgomotul mediu", all, StringComparison.Ordinal);
                Assert.Contains(english ? "no banding" : "fara benzi", all, StringComparison.Ordinal);
                Assert.Contains(english ? "the noise of the frame, in ADU, on the green pixels G1" : "zgomotul cadrului, in ADU, pe pixelii verzi G1", all, StringComparison.Ordinal);
                Assert.Contains(english ? "how many pixels of the frame go over 20 ADU-eq, on the green pixels G1" : "cati pixeli din cadru trec de 20 ADU-eq, pe pixelii verzi G1", all, StringComparison.Ordinal);
                Assert.Contains(english ? "RANDOM BANDING (1 = NO BANDING)" : "BENZI ALEATOARE (1 = FARA BENZI)", all, StringComparison.Ordinal);
                Assert.Contains(english ? "How many times more the rows of the frame differ from one another" : "De cate ori difera randurile cadrului intre ele", all, StringComparison.Ordinal);
                Regex method = english ? PlainWords.EnglishTabWords() : PlainWords.RomanianTabWords();
                List<string> left = texts.Where(t => method.IsMatch(t)).Distinct(StringComparer.Ordinal).ToList();
                Assert.True(left.Count == 0, "the method's words are left:\n" + string.Join("\n", left));
            }
        });
    }

    /// <summary>
    /// The Harti tab in plain words (R-084, ADR 0032): its lead, its map titles and the message of a
    /// missing map name no part of the method, in either language; a session without biases shows
    /// the bias maps' message, one of tiny frames the dark master's.
    /// </summary>
    /// <param name="english">True for the English pass, false for the Romanian one.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task R084_TheMapsTabIsInPlainWords(bool english)
    {
        using TestSettings.Own _ = new();
        using SessionFixture f = new SessionFixture().WithDarkSeries(4, i => new SyntheticDark { Seed = 100 + i });
        using SessionFixture tiny = new SessionFixture().WithDarkSeries(3, i => new SyntheticDark { Seed = 100 + i, Width = 24, Height = 16 });

        await _sta.InvokeAsync(async () =>
        {
            using (Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian))
            {
                List<string> texts = [];
                foreach (SessionFixture s in new[] { f, tiny })
                {
                    MainViewModel vm = new();
                    await vm.OpenAsync(s.Root);
                    Assert.True(vm.HasResult);
                    MapsView view = new() { DataContext = vm };
                    Render(view, 1360, 1800);
                    texts.AddRange(TextsOf(view));
                }

                // the scenes reached their variants, so the words below were looked at
                string all = string.Join("\n", texts);
                Assert.Contains("DARK MINUS BIAS", all, StringComparison.Ordinal);
                Assert.Contains(english ? "Each map has its own contrast" : "Fiecare harta are contrastul ei", all, StringComparison.Ordinal);
                Assert.Contains(english ? "No map: the session has no biases that can be used, or the frames are smaller than 32 px." : "Nu e harta: sesiunea nu are bias-uri care sa se poata folosi, sau cadrele sunt mai mici de 32 px.", all, StringComparison.Ordinal);
                Assert.Contains(english ? "No map: the frames are smaller than 32 px." : "Nu e harta: cadrele sunt mai mici de 32 px.", all, StringComparison.Ordinal);
                Regex method = english ? PlainWords.EnglishTabWords() : PlainWords.RomanianTabWords();
                List<string> left = texts.Where(t => method.IsMatch(t)).Distinct(StringComparer.Ordinal).ToList();
                Assert.True(left.Count == 0, "the method's words are left:\n" + string.Join("\n", left));
            }
        });
    }

    /// <summary>
    /// The Pixeli fierbinti tab in plain words (R-085, ADR 0033): the text under check 5's rows, the
    /// noise chart's title and sentence and the hints of its points name no part of the method, in
    /// either language; each hint names its frames as the Rezumat's check 8 does.
    /// </summary>
    /// <param name="english">True for the English pass, false for the Romanian one.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task R085_TheHotPixelsTabIsInPlainWords(bool english)
    {
        using TestSettings.Own _ = new();
        using SessionFixture f = WindowFixture.Session();

        await _sta.InvokeAsync(async () =>
        {
            using (Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian))
            {
                MainViewModel vm = new();
                await vm.OpenAsync(f.Root);
                Assert.True(vm.HasResult);
                HotPixelsView view = new() { DataContext = vm };
                Render(view, 1360, 1800);
                List<string> texts = [.. TextsOf(view), .. vm.NoisePoints.Select(p => p.Tip ?? "")];

                // the scenes reached their variants, so the words below were looked at
                string all = string.Join("\n", texts);
                Assert.Contains(english ? "8. HOW THE NOISE FALLS WITH MORE FRAMES (ADU)" : "8. CAT SCADE ZGOMOTUL CU MAI MULTE CADRE (ADU)", all, StringComparison.Ordinal);
                Assert.Contains(english ? "(4 times the frames, half the noise)" : "(de 4 ori mai multe cadre, zgomot de 2 ori mai mic)", all, StringComparison.Ordinal);
                Assert.Contains(english ? "how many jump between two levels from one frame to the next (RTS). For information only: this check never warns." : "cati sar intre doua niveluri de la un cadru la altul (RTS). Doar de informare: verificarea asta nu da niciodata avertizare.", all, StringComparison.Ordinal);

                // six darks give the points of one and two frames: the singular and the plural
                Assert.True(vm.NoisePoints.Count >= 2);
                string[] frames = english ? ["1 frame", "2 frames"] : ["1 cadru", "2 cadre"];
                for (int i = 0; i < 2; i++)
                {
                    string measured = RunReport.F(vm.NoisePoints[i].Y, 3), expected = RunReport.F(vm.NoiseExpected[i].Y, 3);
                    Assert.Equal(english ? $"{frames[i]}: measured {measured}, expected {expected}" : $"{frames[i]}: masurat {measured}, se astepta {expected}", vm.NoisePoints[i].Tip);
                }

                Regex method = english ? PlainWords.EnglishTabWords() : PlainWords.RomanianTabWords();
                List<string> left = texts.Where(t => method.IsMatch(t)).Distinct(StringComparer.Ordinal).ToList();
                Assert.True(left.Count == 0, "the method's words are left:\n" + string.Join("\n", left));
            }
        });
    }

    /// <summary>
    /// The Lights tab in plain words (R-086, ADR 0034): the chart's title, sentence and guide lines,
    /// the table's column names, their hints and its note name no part of the method, in either
    /// language.
    /// </summary>
    /// <param name="english">True for the English pass, false for the Romanian one.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task R086_TheLightsTabIsInPlainWords(bool english)
    {
        using TestSettings.Own _ = new();
        using SessionFixture f = WindowFixture.Session();

        await _sta.InvokeAsync(async () =>
        {
            using (Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian))
            {
                MainViewModel vm = new();
                await vm.OpenAsync(f.Root);
                Assert.True(vm.HasLights);
                LightsView view = new() { DataContext = vm };
                Render(view, 1360, 1800);
                List<string> texts = [.. TextsOf(view), .. vm.LightGuides.Select(g => g.Label)];

                // the scenes reached their variants, so the words below were looked at
                string all = string.Join("\n", texts);
                Assert.Contains(english ? "EACH LIGHT AGAINST THE DARK" : "FIECARE LIGHT FATA DE DARK", all, StringComparison.Ordinal);
                // the card of the lights from another folder (R-122)
                Assert.Contains(english ? "LIGHTS FROM ANOTHER FOLDER" : "LIGHTS-URI DIN ALT FOLDER", all, StringComparison.Ordinal);
                Assert.Contains(english ? "(none: the open folder's lights)" : "(niciunul: lights-urile folderului deschis)", all, StringComparison.Ordinal);
                Assert.Contains(english ? "The choice holds for this folder only and is not kept." : "Alegerea tine doar pentru folderul asta si nu se pastreaza.", all, StringComparison.Ordinal);
                Assert.Contains(english ? "Choose lights..." : "Alege lights-urile...", all, StringComparison.Ordinal);
                Assert.Contains(english ? "The first lights of the night usually sit lower, because the camera is still cold." : "Primele lights ale noptii ies de obicei mai jos, pentru ca aparatul e inca rece.", all, StringComparison.Ordinal);
                Assert.Contains(english ? "0.4 cosmetic only" : "0.4 doar cosmetic", all, StringComparison.Ordinal);
                Assert.Contains(english ? "how bright the sky is in the light: the average value of the pixels, above the black, in ADU" : "cat de luminos e cerul in light: valoarea medie a pixelilor, peste negru, in ADU", all, StringComparison.Ordinal);
                Assert.Contains(english ? "stars and hot pixels" : "stele si pixeli fierbinti", all, StringComparison.Ordinal);
                Assert.Contains(english ? "half of the hot pixels of the master are, in this light, between low and high" : "jumatate din pixelii fierbinti ai masterului sunt, in acest light, intre jos si sus", all, StringComparison.Ordinal);
                string[] names = english ? ["noise", "bright", "single", "hot", "low", "high"] : ["zgomot", "luminosi", "singuri", "fierb.", "jos", "sus"];
                foreach (string name in names)
                {
                    Assert.Contains(name, texts);
                }

                Regex method = english ? PlainWords.EnglishTabWords() : PlainWords.RomanianTabWords();
                List<string> left = texts.Where(t => method.IsMatch(t)).Distinct(StringComparer.Ordinal).ToList();
                Assert.True(left.Count == 0, "the method's words are left:\n" + string.Join("\n", left));
            }
        });
    }

    /// <summary>
    /// The Index tab in plain words (R-087, ADR 0036): the text under the label, the partner's
    /// explanation and the hint of Analyse again name no part of the method, in either language;
    /// the run has a partner, so its rows are on the tab too.
    /// </summary>
    /// <param name="english">True for the English pass, false for the Romanian one.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task R087_TheIndexTabIsInPlainWords(bool english)
    {
        using TestSettings.Own _ = new();
        using SessionFixture f = WindowFixture.Session();
        using SessionFixture p = WindowFixture.Partner();

        await _sta.InvokeAsync(async () =>
        {
            using (Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian))
            {
                // a run with a partner and one without, so the partner's rows and the empty partner's text are both read
                List<string> texts = [];
                foreach (string? partner in new[] { p.Root, null })
                {
                    MainViewModel vm = new() { Partner = partner };
                    await vm.OpenAsync(f.Root);
                    Assert.True(vm.HasResult);
                    IndexView view = new() { DataContext = vm };
                    Render(view, 1360, 1800);
                    texts.AddRange(TextsOf(view));
                }

                // the scenes reached their variants, so the words below were looked at
                string all = string.Join("\n", texts);
                string places = $"{RunReport.F(Th.IndexRankLowPerMpx, 0)}-{RunReport.F(Th.IndexRankHighPerMpx, 0)}";
                Assert.Contains(english ? "(none: choose a library or another session to compare the darks with)" : "(niciunul: alege o biblioteca sau alta sesiune ca sa compari dark-urile)", all, StringComparison.Ordinal);
                Assert.Contains(english ? $"the average value of the master's hot pixels at places {places} per megapixel, counting from the hottest" : $"valoarea medie a pixelilor fierbinti din master de pe locurile {places} pe fiecare megapixel, numarand de la cel mai fierbinte", all, StringComparison.Ordinal);
                Assert.Contains(english ? "The warmer the sensor, the higher the index." : "Cu cat senzorul a fost mai cald, cu atat indexul e mai mare.", all, StringComparison.Ordinal);
                Assert.Contains(english ? "if the session has no darks, they are compared directly with the darks of the partner." : "daca sesiunea nu are dark-uri, se compara direct cu dark-urile partenerului.", all, StringComparison.Ordinal);
                Assert.Contains(english ? "(idx- when the index cannot be measured)" : "(idx- cand indexul nu se poate masura)", all, StringComparison.Ordinal);
                Assert.Contains(english ? "Analyses the open folder again, with the current partner and settings" : "Analizeaza din nou folderul deschis, cu partenerul si setarile de acum", all, StringComparison.Ordinal);
                Regex method = english ? PlainWords.EnglishTabWords() : PlainWords.RomanianTabWords();
                List<string> left = texts.Where(t => method.IsMatch(t)).Distinct(StringComparer.Ordinal).ToList();
                Assert.True(left.Count == 0, "the method's words are left:\n" + string.Join("\n", left));
            }
        });
    }

    /// <summary>
    /// The texts around the report in plain words (R-088, ADR 0037): the start screen of a window
    /// with no folder and the hints of the top bar's four settings name no part of the method, in
    /// either language.
    /// </summary>
    /// <param name="english">True for the English pass, false for the Romanian one.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task R088_TheStartScreenAndTheTopBarAreInPlainWords(bool english)
    {
        using TestSettings.Own _ = new();

        await _sta.InvokeAsync(() =>
        {
            using (Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian))
            {
                MainViewModel vm = new();
                FrameworkElement window = (FrameworkElement)new MainWindow(vm).Content;
                Render(window);
                List<string> texts = [.. TextsOf(window), vm.MaxFramesTip, vm.IsoFactorTip, vm.DoublingTip];

                // the scenes reached their variants, so the words below were looked at
                string all = string.Join("\n", texts);
                Assert.Contains(english ? "the program reads and measures the biases, the darks and the lights, then works out the index and the label." : "programul citeste si masoara bias-urile, dark-urile si lights-urile, apoi socoteste indexul si eticheta.", all, StringComparison.Ordinal);
                Assert.Contains(english ? "How many frames are read at the same time; blank = half the cores of the processor" : "Cate cadre se citesc in acelasi timp; gol = jumatate din nucleele procesorului", all, StringComparison.Ordinal);
                Assert.Contains(english ? "SIGNAL / ISO STOP" : "SEMNAL / TREAPTA ISO", all, StringComparison.Ordinal);
                Assert.Contains(english ? "only some are read, evenly spaced (one in two, for example)" : "se citesc doar unele, la distante egale (de exemplu unul din doua)", all, StringComparison.Ordinal);
                Assert.Contains(english ? "2 = the signal doubles at every stop (D3300)" : "2 = semnalul se dubleaza la fiecare treapta (D3300)", all, StringComparison.Ordinal);
                Assert.Contains(english ? "(what the sensor gathers from its own heat)" : "(ce aduna senzorul din caldura lui)", all, StringComparison.Ordinal);
                Regex method = english ? PlainWords.EnglishTabWords() : PlainWords.RomanianTabWords();
                List<string> left = texts.Where(t => method.IsMatch(t)).Distinct(StringComparer.Ordinal).ToList();
                Assert.True(left.Count == 0, "the method's words are left:\n" + string.Join("\n", left));
            }

            return Task.CompletedTask;
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
