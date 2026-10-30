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

using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CopyWcs.Wpf.Tests;

/// <summary>
/// The window really loads and really draws. The view-model tests check C#; XAML is checked
/// only at run time: a misnamed resource, a missing style or a binding to a property that
/// does not exist pass the compiler and show when the window loads. Each test builds the
/// element, lays it out, renders it to a bitmap and asserts that no binding error was traced.
/// With COPYWCS_DUMP_SCREENS=&lt;folder&gt; each render is also saved as a PNG, to look at
/// the window without starting it; it is not a comparison against a reference. After
/// DarksCheck's (ADR 0007).
/// </summary>
[Collection("window")]
public partial class WindowSmokeTests
{
    private readonly StaFixture _sta;

    public WindowSmokeTests(StaFixture sta) => _sta = sta;

    // Words that only the Romanian texts hold, the console's (LanguageTests) and the window's
    // own (R-036); the paths of the fixture are temp folders named CopyWcsTests_ plus random
    // letters, so none of these can come from them.
    [GeneratedRegex(@"\b(ATENTIE|Nu pot|Chei care|Copiate|Dimensiuni|Nicio cheie|Eroare|sursa|destinatie|Verifica vizual|Alege|Scrie|Copiaza|Despre|Refuzat|Inchide|Sursa|Destinatia|Previzualizare|fara|deja|LICENTA|Textul|licentei|Continua|metadatele|dintr-un|Versiune|licenta|rezolvat|aceeasi|pixelii|COMPONENTE|TERTE|componente|Unealta|jurnalului|leaga)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RomanianWord();

    /// <summary>Every text a rendered tree shows: the text blocks (the buttons' and the check boxes' labels become ones), and the hints as strings.</summary>
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

    /// <summary>The visual parents of an element, the nearest first.</summary>
    private static IEnumerable<DependencyObject> Ancestors(DependencyObject d)
    {
        for (DependencyObject? p = VisualTreeHelper.GetParent(d); p is not null; p = VisualTreeHelper.GetParent(p))
        {
            yield return p;
        }
    }

    /// <summary>The one text block of a rendered tree that shows exactly this text.</summary>
    private static TextBlock TextBlockWith(DependencyObject root, string text)
        => Assert.Single(Descendants(root).OfType<TextBlock>(), t => t.Text == text);

    /// <summary>Where an element was laid out, in the coordinates of the rendered root.</summary>
    private static Rect Box(FrameworkElement element, Visual root)
        => element.TransformToAncestor(root).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));

    /// <summary>
    /// Both windows in English, with an outcome that has a refusal heading, a warning, the
    /// preview's heading and the cards: no Romanian word is left anywhere (R-036); and in
    /// Romanian the buttons read as R-036 says.
    /// </summary>
    [Fact]
    public Task R036_BothWindowsRenderInEnglishWithNoRomanianWordAndInRomanian()
        => _sta.InvokeAsync(async () =>
        {
            using WindowFixture f = new(destWidth: 200);
            List<string> english;
            using (Ui.Use(UiLanguage.English))
            {
                using BindingErrors errors = new();
                MainViewModel vm = new() { Source = f.Source, Dest = f.Dest, Force = true };
                await vm.Preview();
                Assert.True(vm.HasWarnings && vm.HasCards);
                FrameworkElement window = (FrameworkElement)new MainWindow(vm).Content;
                Render(window, name: "window-en");
                english = TextsOf(window).ToList();
                vm.Force = false;
                await vm.Preview();
                Assert.True(vm.HasError);
                Render(window);
                english.AddRange(TextsOf(window));

                AboutWindow about = new();
                Render((FrameworkElement)about.Content, 560, AboutHeight(about), "about-en");
                english.Add(about.Title);
                english.AddRange(TextsOf((FrameworkElement)about.Content));
                Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
            }

            Assert.Contains("Preview", english);
            Assert.Contains("Copy", english);
            Assert.Contains("About", english);
            Assert.Contains("Refused", english);
            Assert.Contains("Source: 100 x 80, plate-solved", english);
            List<string> romanian = english.Where(t => RomanianWord().IsMatch(t)).Distinct(StringComparer.Ordinal).ToList();
            Assert.True(romanian.Count == 0, "Romanian left in the English window:\n" + string.Join("\n", romanian));

            // the assembly's default, Romanian: the same elements, the other wording
            MainViewModel ro = new() { Source = f.Source, Dest = f.Dest };
            FrameworkElement roWindow = (FrameworkElement)new MainWindow(ro).Content;
            Render(roWindow, name: "window-ro");
            List<string> texts = TextsOf(roWindow).ToList();
            Assert.Contains("Previzualizare", texts);
            Assert.Contains("Copiaza", texts);
            Assert.Contains("Despre", texts);
            Assert.Contains("Sursa: 100 x 80, plate-solved", texts);
            Assert.DoesNotContain("Preview", texts);
        });

    /// <summary>Catches the binding errors WPF otherwise writes only to the debugger's output.</summary>
    internal sealed class BindingErrors : IDisposable
    {
        private readonly StringBuilder _sb = new();
        private readonly TraceListener _listener;
        private readonly SourceLevels _before;

        internal BindingErrors()
        {
            PresentationTraceSources.Refresh();
            _before = PresentationTraceSources.DataBindingSource.Switch.Level;
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
            _listener = new TextWriterTraceListener(new StringWriter(_sb));
            PresentationTraceSources.DataBindingSource.Listeners.Add(_listener);
        }

        internal string Text
        {
            get
            {
                _listener.Flush();
                return _sb.ToString();
            }
        }

        public void Dispose()
        {
            PresentationTraceSources.DataBindingSource.Listeners.Remove(_listener);
            PresentationTraceSources.DataBindingSource.Switch.Level = _before;
            _listener.Dispose();
        }
    }

    /// <summary>
    /// The About window's content height at its 560 px width: the window sizes to its content
    /// (<c>SizeToContent="Height"</c>), so a render at that height cuts nothing off, the
    /// third-party card included. Measured twice: the card's items exist only after a first
    /// layout pass has bound the list.
    /// </summary>
    private static int AboutHeight(AboutWindow about)
    {
        var content = (FrameworkElement)about.Content;
        content.Measure(new Size(560, double.PositiveInfinity));
        content.Arrange(new Rect(content.DesiredSize));
        content.UpdateLayout();
        content.Measure(new Size(560, double.PositiveInfinity));
        return (int)Math.Ceiling(content.DesiredSize.Height);
    }

    internal static void Render(FrameworkElement el, int w = 720, int h = 560, string? name = null)
    {
        el.Measure(new Size(w, h));
        el.Arrange(new Rect(0, 0, w, h));
        el.UpdateLayout();

        // The window's background first: the content alone renders on transparency, where the
        // anti-aliased labels come out dim in the dumped PNG.
        System.Windows.Shapes.Rectangle background = new() { Width = w, Height = h, Fill = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1D)) };
        background.Measure(new Size(w, h));
        background.Arrange(new Rect(0, 0, w, h));
        RenderTargetBitmap bmp = new(w, h, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(background);
        bmp.Render(el);

        Assert.Equal(w, bmp.PixelWidth);
        Dump(bmp, name);
    }

    private static void Dump(RenderTargetBitmap bmp, string? name)
    {
        string? dir = Environment.GetEnvironmentVariable("COPYWCS_DUMP_SCREENS");
        if (string.IsNullOrEmpty(dir) || name is null)
        {
            return;
        }

        Directory.CreateDirectory(dir);
        PngBitmapEncoder enc = new();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using FileStream fs = File.Create(Path.Combine(dir, name + ".png"));
        enc.Save(fs);
    }

    /// <summary>
    /// The band at the top (R-038): edge to edge on the panel colour with a line under it, the
    /// console's first two lines on the left, the explanation in the middle, and on the right
    /// the window's one About button, which runs Help as F1 does, in each language.
    /// </summary>
    [Theory]
    [InlineData(false, "Copies the plate solve (WCS) from a FITS solved in Siril into the same image processed elsewhere, card by card, without touching its pixels.", "About", "Version, licence, components (F1)")]
    [InlineData(true, "Copiaza plate solve-ul (WCS) dintr-un FITS rezolvat in Siril in aceeasi imagine procesata in alta parte, card cu card, fara sa-i atinga pixelii.", "Despre", "Versiune, licenta, componente (F1)")]
    public void R038_TheBandShowsTheConsolesFirstTwoLinesTheExplanationAndTheOnlyAboutButton(bool romanian, string explanation, string about, string tip)
        => _sta.Invoke(() =>
        {
            using IDisposable _ = Ui.Use(romanian ? UiLanguage.Romanian : UiLanguage.English);
            using StringWriter stdout = new();
            Assert.Equal(0, CopyWcsCli.Run(["--help"], stdout, new StringWriter()));
            string[] console = stdout.ToString().ReplaceLineEndings("\n").Split('\n');

            using BindingErrors errors = new();
            MainWindow w = new();
            FrameworkElement window = (FrameworkElement)w.Content;
            Render(window, name: romanian ? "band-ro" : "band-en");
            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);

            // the band: the nearest border on the panel colour around the banner, at the very
            // top, as wide as the window, with a line under it
            TextBlock banner = TextBlockWith(window, console[0]);
            Border band = Ancestors(banner).OfType<Border>()
                .First(b => b.Background is SolidColorBrush brush && brush.Color == Color.FromRgb(0x23, 0x23, 0x26));
            Assert.Equal(0.0, Box(band, window).Top);
            Assert.Equal(window.ActualWidth, band.ActualWidth);
            Assert.Equal(new Thickness(0, 0, 0, 1), band.BorderThickness);
            Assert.Equal(Color.FromRgb(0x3A, 0x3A, 0x40), Assert.IsType<SolidColorBrush>(band.BorderBrush).Color);
            Assert.Equal(FontWeights.SemiBold, banner.FontWeight);

            // the copyright under the banner, the explanation, and the one About button, all in it
            TextBlock copyright = TextBlockWith(window, console[1]);
            TextBlock line = TextBlockWith(window, explanation);
            Button button = Assert.Single(Descendants(window).OfType<Button>(), b => Equals(b.Content, about));
            foreach (FrameworkElement e in new FrameworkElement[] { copyright, line, button })
            {
                Assert.Contains(band, Ancestors(e));
            }

            // the banner over the copyright on the left, the explanation in the middle, About on
            // the right and inside the window: a column swapped, or a line too wide to wrap, fails
            Assert.True(Box(banner, window).Top < Box(copyright, window).Top);
            Assert.True(Math.Max(Box(banner, window).Right, Box(copyright, window).Right) < Box(line, window).Left);
            Assert.True(Box(line, window).Right < Box(button, window).Left);
            Assert.True(Box(button, window).Right <= window.ActualWidth);
            Assert.Equal(TextWrapping.Wrap, line.TextWrapping);

            // About runs the Help command, as F1 does, and the window's binding lets it run
            Assert.Same(ApplicationCommands.Help, button.Command);
            Assert.Contains(w.CommandBindings.Cast<CommandBinding>(), b => b.Command == ApplicationCommands.Help);
            Assert.True(ApplicationCommands.Help.CanExecute(null, button));
            Assert.Equal(tip, button.ToolTip as string);
        });

    /// <summary>The window before any file: the empty state, the dark palette as its background (R-037).</summary>
    [Fact]
    public void R037_TheMainWindowLoadsAndRendersEmpty()
        => _sta.Invoke(() =>
        {
            using BindingErrors errors = new();
            MainWindow w = new();
            Render((FrameworkElement)w.Content, name: "window-empty");
            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
            Assert.Equal(Color.FromRgb(0x1B, 0x1B, 0x1D), Assert.IsType<SolidColorBrush>(w.Background).Color);
        });

    /// <summary>The files from the command line fill the boxes, either one alone too (R-034).</summary>
    [Fact]
    public void R034_StartFillsTheBoxesWithTheFilesGiven()
        => _sta.Invoke(() =>
        {
            using WindowFixture f = new();
            MainViewModel vm = new();
            MainWindow w = new(vm);

            w.Start(f.Source, f.Dest);
            Assert.Equal(f.Source, vm.Source);
            Assert.Equal(f.Dest, vm.Dest);
            Assert.True(vm.CanPreview);

            MainViewModel one = new();
            new MainWindow(one).Start(null, f.Dest);
            Assert.Equal("", one.Source);
            Assert.Equal(f.Dest, one.Dest);
            Assert.False(one.CanPreview);
        });

    /// <summary>
    /// The About window: its own XAML, the icon through a decoder and a link bound to a
    /// property, all of which compile with a wrong name (R-035, R-037).
    /// </summary>
    [Fact]
    public void R035_TheAboutWindowLoadsAndRenders()
        => _sta.Invoke(() =>
        {
            using BindingErrors errors = new();
            AboutWindow about = new();

            // The content, not the window: an unshown Window renders transparent, while the
            // root panel inherits the DataContext and goes through the same bindings.
            Render((FrameworkElement)about.Content, 560, AboutHeight(about), "about");
            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);

            BitmapSource logo = Assert.IsAssignableFrom<BitmapSource>(about.Logo.Source);
            Assert.Equal(256, logo.PixelWidth);
            Assert.Equal("Despre CopyWcs", about.Title);
        });

    /// <summary>The window with an outcome of every kind drawn: the refusal, the warning, the cards and the closing lines (R-037).</summary>
    [Fact]
    public Task R037_TheOutcomeCardRendersEveryKindWithNoBindingError()
        => _sta.InvokeAsync(async () =>
        {
            using WindowFixture f = new(destWidth: 200);
            using BindingErrors errors = new();
            MainViewModel vm = new() { Source = f.Source, Dest = f.Dest };
            MainWindow w = new(vm);

            await vm.Preview();
            Assert.True(vm.HasError);
            Render((FrameworkElement)w.Content, name: "window-refused");

            vm.Force = true;
            await vm.Preview();
            Assert.True(vm.HasWarnings);
            Assert.True(vm.HasCards);
            Render((FrameworkElement)w.Content, name: "window-preview");

            await vm.Copy();
            Assert.True(vm.HasNotes);
            Render((FrameworkElement)w.Content, name: "window-copied");

            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });
}
