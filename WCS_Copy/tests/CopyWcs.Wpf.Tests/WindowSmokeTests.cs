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
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CopyWcs.Tests;

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
    [GeneratedRegex(@"\b(ATENTIE|Nu pot|Chei care|Chei vechi|Copiate|Eliminate|Eliminata|destinatiei|oglinda|oglindita|Intoarce|Dimensiuni|Nicio cheie|Eroare|sursa|destinatie|Verifica vizual|Alege|Scrie|Copiaza|Ajutor|Manualul|sectiunea|Despre|Refuzat|Inchide|Sursa|Destinatia|Previzualizare|fara|deja|LICENTA|Textul|licentei|Continua|metadatele|dintr-un|Versiune|licenta|rezolvat|aceeasi|pixelii|COMPONENTE|TERTE|componente|Unealta|jurnalului|leaga)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RomanianWord();

    /// <summary>Every text a rendered tree shows: the text blocks (the buttons' and the check boxes' labels become ones), and the hints as strings.</summary>
    private static IEnumerable<string> TextsOf(DependencyObject root)
    {
        foreach (DependencyObject d in Descendants(root))
        {
            // The text the block displays: TextBlock.Text misses the inline runs of a template's
            // blocks (the About window's component row, the second review of 2026-10-30).
            string shown = d is TextBlock t ? new TextRange(t.ContentStart, t.ContentEnd).Text : "";
            if (shown.Length > 0)
            {
                yield return shown;
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
    /// preview's heading, the cards and the line of the keys it would drop (R-016): no
    /// Romanian word is left anywhere (R-036); and in Romanian the buttons read as R-036 says.
    /// </summary>
    [Fact]
    public Task R036_BothWindowsRenderInEnglishWithNoRomanianWordAndInRomanian()
        => _sta.InvokeAsync(async () =>
        {
            using WindowFixture f = new(destCards: FitsTestData.OldSolveCards(200, 80));
            List<string> english;
            using (Ui.Use(UiLanguage.English))
            {
                using BindingErrors errors = new();
                MainViewModel vm = new() { Source = f.Source, Dest = f.Dest, Force = true };
                await vm.Preview();
                Assert.True(vm.HasWarnings && vm.HasCards && vm.HasNotes);
                FrameworkElement window = (FrameworkElement)new MainWindow(vm).Content;
                Render(window, name: "window-en");
                english = TextsOf(window).ToList();
                vm.Force = false;
                await vm.Preview();
                Assert.True(vm.HasError);
                Render(window);
                english.AddRange(TextsOf(window));

                // a copy held in flight: the card's in-progress line (R-053)
                using ManualResetEventSlim release = new();
                vm.RunCore = WindowCloseTests.HeldRun(release);
                Task copy = vm.Copy();
                Render(window, name: "window-running-en");
                english.AddRange(TextsOf(window));
                release.Set();
                await copy;

                AboutWindow about = new();
                Render((FrameworkElement)about.Content, 560, AboutHeight(about), "about-en");
                english.Add(about.Title);
                english.AddRange(TextsOf((FrameworkElement)about.Content));
                Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
            }

            Assert.Contains("Preview", english);
            Assert.Contains("Copy", english);
            Assert.Contains("Help", english);
            Assert.Contains("The manual, at the section about what is on screen (F1)", english);
            Assert.Contains("About", english);
            Assert.Contains("Refused", english);
            Assert.Contains("Copy in progress...", english);
            Assert.Contains("Source: 100 x 80, plate-solved", english);
            Assert.Contains(english, t => t.StartsWith("Old plate-solve keys of the destination that would be dropped (6): ", StringComparison.Ordinal));
            Assert.Contains("Continue even when the dimensions differ or the image is mirrored", english);
            List<string> romanian = english.Where(t => RomanianWord().IsMatch(t)).Distinct(StringComparer.Ordinal).ToList();
            Assert.True(romanian.Count == 0, "Romanian left in the English window:\n" + string.Join("\n", romanian));

            // the assembly's default, Romanian: the same elements, the other wording, once the
            // boxes' files are looked at (R-052)
            MainViewModel ro = new() { Source = f.Source, Dest = f.Dest };
            await ro.FactsRead;
            FrameworkElement roWindow = (FrameworkElement)new MainWindow(ro).Content;
            Render(roWindow, name: "window-ro");
            List<string> texts = TextsOf(roWindow).ToList();
            Assert.Contains("Previzualizare", texts);
            Assert.Contains("Copiaza", texts);
            Assert.Contains("Ajutor", texts);
            Assert.Contains("Despre", texts);
            Assert.Contains("Sursa: 100 x 80, plate-solved", texts);
            Assert.Contains("Continua chiar daca dimensiunile difera sau imaginea e oglindita", texts);
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
    /// What a window's <c>Width</c> and <c>MinWidth</c> count beyond its content: the resizable
    /// frame, 8 px a side at 96 dpi on Windows 10, so a window of 820 px lays out 804 px inside.
    /// </summary>
    private const double WindowFrame = 16;

    /// <summary>
    /// The band at the top (R-038, R-039): edge to edge on the panel colour with a line under it,
    /// the console's first two lines on the left, the explanation in the middle, and on the right
    /// Help (the drawn icon, then the word) and the window's one About button, in each language.
    /// Neither runs a command: F1 is the window's own key for Help, and About has its click.
    /// </summary>
    [Theory]
    [InlineData(false, "Copies the plate solve (WCS) from a FITS solved in Siril into the same image processed elsewhere, card by card, without touching its pixels.", "Help", "The manual, at the section about what is on screen (F1)", "About", "Version, licence, components")]
    [InlineData(true, "Copiaza plate solve-ul (WCS) dintr-un FITS rezolvat in Siril in aceeasi imagine procesata in alta parte, card cu card, fara sa-i atinga pixelii.", "Ajutor", "Manualul, la sectiunea despre ce e pe ecran (F1)", "Despre", "Versiune, licenta, componente")]
    public void R038_TheBandShowsTheConsolesFirstTwoLinesTheExplanationHelpAndTheOnlyAboutButton(bool romanian, string explanation, string help, string helpTip, string about, string aboutTip)
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

            // the copyright under the banner, the explanation, Help and the one About button, all in it
            TextBlock copyright = TextBlockWith(window, console[1]);
            TextBlock line = TextBlockWith(window, explanation);
            Button helpButton = w.HelpButton;
            Button button = Assert.Single(Descendants(window).OfType<Button>(), b => Equals(b.Content, about));
            Assert.Same(w.AboutButton, button);
            foreach (FrameworkElement e in new FrameworkElement[] { copyright, line, helpButton, button })
            {
                Assert.Contains(band, Ancestors(e));
            }

            // Help: the theme's drawn icon, then the word; its name for a screen reader is the word
            Image icon = Assert.Single(Descendants(helpButton).OfType<Image>());
            Assert.Same(Application.Current.FindResource("HelpIcon"), icon.Source);
            TextBlock word = Assert.Single(Descendants(helpButton).OfType<TextBlock>(), t => t.Text == help);
            Assert.True(Box(icon, window).Right <= Box(word, window).Left);
            Assert.Equal(help, AutomationProperties.GetName(helpButton));
            Assert.Equal(helpTip, helpButton.ToolTip as string);

            // the banner over the copyright on the left, the explanation in the middle, Help then
            // About on the right and inside the window: a column swapped, or a line too wide to
            // wrap, fails
            Assert.True(Box(banner, window).Top < Box(copyright, window).Top);
            Assert.True(Math.Max(Box(banner, window).Right, Box(copyright, window).Right) < Box(line, window).Left);
            Assert.True(Box(line, window).Right < Box(helpButton, window).Left);
            Assert.True(Box(helpButton, window).Right < Box(button, window).Left);
            Assert.True(Box(button, window).Right <= window.ActualWidth);
            Assert.Equal(TextWrapping.Wrap, line.TextWrapping);

            // neither button runs a command, and the window binds none for F1's gesture
            Assert.Null(button.Command);
            Assert.Null(helpButton.Command);
            Assert.Empty(w.CommandBindings);
            Assert.Equal(aboutTip, button.ToolTip as string);

            // at the width the window opens with, less its frame, the explanation takes two
            // lines: the band is as tall as on a window wide enough for one, where the banner and
            // the copyright set its height (57 px on 2026-11-16; three lines made it 70)
            double BandAt(double width)
            {
                window.Measure(new Size(width, 560));
                window.Arrange(new Rect(0, 0, width, 560));
                window.UpdateLayout();
                return band.ActualHeight;
            }

            Assert.Equal(820, w.Width);
            Assert.Equal(BandAt(2000), BandAt(w.Width - WindowFrame));
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

    /// <summary>
    /// The files from the command line fill the boxes, either one alone too (R-034); their looks
    /// end back on this thread (R-052), which the test lets them reach by awaiting.
    /// </summary>
    [Fact]
    public Task R034_StartFillsTheBoxesWithTheFilesGiven()
        => _sta.InvokeAsync(async () =>
        {
            using WindowFixture f = new();
            MainViewModel vm = new();
            MainWindow w = new(vm);

            w.Start(f.Source, f.Dest);
            await vm.FactsRead;
            Assert.Equal(f.Source, vm.Source);
            Assert.Equal(f.Dest, vm.Dest);
            Assert.True(vm.CanPreview);

            MainViewModel one = new();
            new MainWindow(one).Start(null, f.Dest);
            await one.FactsRead;
            Assert.Equal("", one.Source);
            Assert.Equal(f.Dest, one.Dest);
            Assert.False(one.CanPreview);
        });

    /// <summary>
    /// The window back in front looks at its files again (R-054; CR-010 of the review of
    /// 2026-11-16): a source plate-solved and saved in Siril while the window was behind shows
    /// as plate-solved, with no edit of its box. The activation is WPF's own, raised through
    /// <c>OnActivated</c>, since a test window is never shown.
    /// </summary>
    [Fact]
    public Task R054_TheWindowBackInFrontLooksAgain()
        => _sta.InvokeAsync(async () =>
        {
            List<string> unsolved = FitsTestData.PlateSolvedCards().Where(c => !c.StartsWith("CTYPE1", StringComparison.Ordinal)).ToList();
            using WindowFixture f = new(sourceCards: unsolved);
            MainViewModel vm = new() { Source = f.Source, Dest = f.Dest };
            MainWindow w = new(vm);
            await vm.FactsRead;
            Assert.Equal("Sursa: 100 x 80, fara plate solve", vm.SourceFacts);

            File.WriteAllBytes(f.Source, FitsTestData.MinimalFitsFile(FitsTestData.PlateSolvedCards(), FitsTestData.DeterministicData(400)));
            typeof(Window).GetMethod("OnActivated", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(w, [EventArgs.Empty]);
            await vm.FactsRead;

            Assert.Equal("Sursa: 100 x 80, plate-solved", vm.SourceFacts);
        });

    /// <summary>
    /// The window's three file dialogs put the working folder back as they close, so no Browse
    /// moves the folder a path typed relative is taken from (R-051; CR-006 of the review of
    /// 2026-11-16). Measured on 2026-11-18 with .NET 8.0.34: the Save dialog never moved it, the
    /// Open dialog with the setting did not; the Open dialog without it was not measured.
    /// </summary>
    [Fact]
    public void R051_TheFileDialogsPutTheWorkingFolderBack()
    {
        Assert.True(MainWindow.OpenDialog("source", "").RestoreDirectory);
        Assert.True(MainWindow.OpenDialog("destination", @"C:\Windows\win.ini").RestoreDirectory);
        Assert.True(MainWindow.SaveDialog(@"C:\Windows\win.ini").RestoreDirectory);
    }

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

            // The third-party card is on screen, not only in the view-model (R-035): its heading,
            // its sentence, and the row's name and role, licence, copyright line and link.
            List<string> shown = TextsOf((FrameworkElement)about.Content).ToList();
            ThirdPartyComponent nlog = Assert.Single(ThirdParty.Components);
            Assert.Contains("COMPONENTE TERTE", shown);
            Assert.Contains(((AboutViewModel)about.DataContext).ThirdPartyLead, shown);
            // (the XAML's line breaks between the runs read as spaces around the dash)
            Assert.Contains(shown, t => t.StartsWith("NLog", StringComparison.Ordinal)
                                        && t.Contains("scrierea jurnalului de diagnostic", StringComparison.Ordinal));
            Assert.Contains("BSD-3-Clause", shown);
            Assert.Contains(nlog.Copyright, shown);
            Assert.Contains("https://nlog-project.org/", shown);

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

            // a preview held in flight: the card shows its in-progress line alone (R-053)
            using ManualResetEventSlim release = new();
            vm.RunCore = WindowCloseTests.HeldRun(release);
            Task preview = vm.Preview();
            Render((FrameworkElement)w.Content, name: "window-running");
            Assert.Contains("Previzualizare in curs...", TextsOf((FrameworkElement)w.Content));
            release.Set();
            await preview;

            Assert.True(errors.Text.Length == 0, "binding errors:\n" + errors.Text);
        });

    /// <summary>
    /// The window fits into the work area of its screen when it opens (R-055; CR-012 of the
    /// review of 2026-11-16): on a screen with 600 units of height it opens 600 high, its minimum
    /// no taller, at the area's top; on a tall screen it keeps its own height and minimum and is
    /// moved up only as far as its bottom needs.
    /// </summary>
    [Fact]
    public void R055_TheWindowFitsIntoTheWorkAreaOfItsScreen()
        => _sta.Invoke(() =>
        {
            MainWindow small = new() { Top = 300 };
            small.FitInto(new Rect(0, 20, 1280, 600));
            Assert.Equal(600, small.Height);
            Assert.Equal(600, small.MinHeight);
            Assert.Equal(20, small.Top);

            MainWindow tall = new() { Top = 500 };
            double height = tall.Height;
            double minimum = tall.MinHeight;
            tall.FitInto(new Rect(0, 0, 1920, 1040));
            Assert.Equal(height, tall.Height);
            Assert.Equal(minimum, tall.MinHeight);
            Assert.Equal(1040 - height, tall.Top);
        });

    /// <summary>
    /// At its minimum height the window keeps four cards in view in the worst case the core
    /// gives (R-055): a forced copy with two warnings (different sizes, a mirrored plate solve),
    /// the drop line and the two closing lines, in both languages. Measured on 2026-11-18 at the
    /// old minimum of 620: the list had 29 px, one card.
    /// </summary>
    /// <param name="romanian">The language of the window.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task R055_AtItsMinimumHeightTheCardsListShowsFourCards(bool romanian)
        => _sta.InvokeAsync(async () =>
        {
            using IDisposable _ = Ui.Use(romanian ? UiLanguage.Romanian : UiLanguage.English);
            using WindowFixture f = new(destCards: FitsTestData.MirroredSolveCards(200, 80));
            MainViewModel vm = new() { Source = f.Source, Dest = f.Dest, Force = true };
            await vm.FactsRead;
            MainWindow w = new(vm) { Left = -20000, Top = -20000, ShowActivated = false, Width = 720 };
            w.Height = w.MinHeight;
            w.Show();
            try
            {
                await vm.Copy();
                w.UpdateLayout();
                Assert.Equal(2, vm.Warnings.Count);
                Assert.Equal(3, vm.Notes.Count);
                Render((FrameworkElement)w.Content, (int)((FrameworkElement)w.Content).ActualWidth,
                    (int)((FrameworkElement)w.Content).ActualHeight, romanian ? "window-minimum-ro" : "window-minimum-en");

                ListBox list = Descendants(w).OfType<ListBox>().Single();
                ScrollViewer scroll = Descendants(list).OfType<ScrollViewer>().First();
                Assert.True(scroll.ViewportHeight >= 4, $"the list shows {scroll.ViewportHeight} cards, {list.ActualHeight} px");
            }
            finally
            {
                w.Close();
            }
        });

    /// <summary>
    /// A refusal with a warning raised before it shows the warning above the refusal, as R-033
    /// says and the console prints them (CR-017 of the review of 2026-11-16): a source without a
    /// plate solve and the optical keys off gives the warning, then `No relevant key found`.
    /// </summary>
    [Fact]
    public Task R033_TheWarningsRaisedBeforeARefusalAreShownAboveIt()
        => _sta.InvokeAsync(async () =>
        {
            using IDisposable _ = Ui.Use(UiLanguage.English);
            // a processed header as the source: no plate solve at all, the optical keys only
            using WindowFixture f = new(sourceCards: FitsTestData.ProcessedCards());
            MainViewModel vm = new() { Source = f.Source, Dest = f.Dest, IncludeOptical = false };
            MainWindow w = new(vm);
            await vm.Preview();
            Assert.True(vm.HasError);
            Assert.True(vm.HasWarnings);
            FrameworkElement window = (FrameworkElement)w.Content;
            Render(window, name: "window-warning-and-refusal");

            TextBlock warning = TextBlockWith(window, Assert.Single(vm.Warnings));
            TextBlock refusal = TextBlockWith(window, vm.Error!);
            Assert.True(Box(warning, window).Bottom <= Box(refusal, window).Top,
                $"warning at {Box(warning, window)}, refusal at {Box(refusal, window)}");
            w.Close();
        });

    /// <summary>
    /// Where a window's top goes to stay on its screen (R-055): its own top when it fits, the
    /// area's top when it starts above it or is as tall as the area, and up until its bottom is
    /// the area's when it runs past the bottom.
    /// </summary>
    /// <param name="top">The window's top before the fit.</param>
    /// <param name="height">The window's height.</param>
    /// <param name="expected">The top after the fit, in an area from 40 to 1040.</param>
    [Theory]
    [InlineData(200, 700, 200)]
    [InlineData(-100, 700, 40)]
    [InlineData(600, 700, 340)]
    [InlineData(300, 1000, 40)]
    [InlineData(300, 1200, 40)]
    public void R055_WindowFitTopKeepsTheWindowInsideTheArea(double top, double height, double expected)
        => Assert.Equal(expected, WindowFit.Top(top, height, 40, 1000));

    /// <summary>
    /// A shown window on a screen shorter than its minimum is no taller than the screen's work
    /// area (R-055; the review of the branch, 2026-11-18): Windows holds a shown window at its
    /// minimum, so the minimum must come down before the height does, or the window stays 680
    /// high and runs past the area's bottom.
    /// </summary>
    [Fact]
    public void R055_AShownWindowOnAShortScreenIsNoTallerThanItsWorkArea()
        => _sta.Invoke(() =>
        {
            MainWindow w = new() { Left = -20000, Top = -20000, ShowActivated = false };
            w.Show();
            try
            {
                Rect area = new(-20000, -20000, 1280, 600);
                w.FitInto(area);
                w.UpdateLayout();

                Assert.Equal(600, w.ActualHeight);
                Assert.True(w.Top + w.ActualHeight <= area.Bottom, $"top {w.Top}, height {w.ActualHeight}");
            }
            finally
            {
                w.Close();
            }
        });
}
