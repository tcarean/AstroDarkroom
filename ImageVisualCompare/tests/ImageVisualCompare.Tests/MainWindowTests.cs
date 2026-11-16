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
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageVisualCompare.Core;
using ImageVisualCompare.Wpf;
using ImageVisualCompare.Wpf.Imaging;
using ImageVisualCompare.Wpf.Services;

namespace ImageVisualCompare.Tests;

/// <summary>
/// The main window's open flow and toolbar on the WPF test thread (ADR 0032), with dialogs and
/// a loader of the test's own: the window is built, never shown, and driven through its buttons.
/// </summary>
public class MainWindowTests
{
    // About is left out: its click opens a modal dialog, which a never-shown window cannot. Help's
    // click goes to an address the test keeps, never to a browser.
    [Theory]
    [InlineData("ZoomOutButton")]
    [InlineData("ZoomInButton")]
    [InlineData("FitButton")]
    [InlineData("ActualSizeButton")]
    [InlineData("HistogramButton")]
    [InlineData("FalseColoursButton")]
    [InlineData("OpenButton")]
    [InlineData("HelpButton")]
    [InlineData("ChangeBeforeButton")]
    [InlineData("ChangeAfterButton")]
    public async Task R063_AToolbarClick_HandsTheFocusBackToTheComparisonSurface(string buttonName)
    {
        object? surface = null;
        object? focused = null;

        await WpfThread.RunAsync(() =>
        {
            var window = new MainWindow(new CountingDialogs(), new ImagePairLoader(new StalledLoader())) { OpenAddress = _ => { } };
            var button = (ButtonBase)window.FindName(buttonName)!;

            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            // The window has no handle, so the keyboard focus cannot move (ADR 0032); what
            // Focus() leaves behind here is the logical focus of the window's scope, which the
            // keyboard focus follows in the running application.
            surface = window.Compare;
            focused = FocusManager.GetFocusedElement(window);
        });

        Assert.Same(surface, focused);
    }

    // CR-002 of the review of 2026-11-13: the status bar's height is not a whole number of
    // pixels, so the surface above it ended on a fraction of one; and a toolbar whose height is
    // not whole, as it is on a display scaled 150 %, would make the surface start on one.
    [Theory]
    [InlineData(0)]
    [InlineData(0.5)]
    public async Task R022_TheSurfaceStartsAndEndsOnWholePhysicalPixels(double addedToTheToolbar)
    {
        double top = 0;
        double height = 0;
        (double Top, double Height) dips = default;

        await WpfThread.RunAsync(() =>
        {
            var window = new MainWindow(new CountingDialogs(), new ImagePairLoader(new StalledLoader()));
            var root = (Grid)window.Content;
            double scale = VisualTreeHelper.GetDpi(root).DpiScaleY;

            Thickness padding = window.TopBar.Padding;
            window.TopBar.Padding = new Thickness(padding.Left, padding.Top, padding.Right, padding.Bottom + (addedToTheToolbar / scale));

            // The window's client area: 768 by 528 device-independent pixels, a whole number of
            // physical pixels, as a window's is, at 100, 125, 150, 175 and 200 % alike.
            root.Measure(new Size(768, 528));
            root.Arrange(new Rect(0, 0, 768, 528));
            root.UpdateLayout();

            // In physical pixels of the system's scale, which the tests' windows are laid out at.
            top = window.Compare.TranslatePoint(new Point(0, 0), root).Y * scale;
            height = window.Compare.ActualHeight * scale;
            dips = (top / scale, height / scale);
        });

        Assert.InRange(dips.Top, 30, 60);
        Assert.InRange(dips.Height, 400, 480);
        Assert.Equal(Math.Round(top), top, 9);
        Assert.Equal(Math.Round(height), height, 9);
    }

    [Fact]
    public async Task R022_AChangeOfDisplayScale_LaysTheTwoBarsOutAgain()
    {
        // The bars' heights are rounded to the pixels of the display the window is on; moved to
        // a display scaled differently, they have to be measured again even if nothing else of
        // theirs changed.
        (bool Top, bool Bottom) before = default;
        (bool Top, bool Bottom) after = default;

        await WpfThread.RunAsync(() =>
        {
            var window = new MainWindow(new CountingDialogs(), new ImagePairLoader(new StalledLoader()));
            var root = (Grid)window.Content;
            root.Measure(new Size(768, 528));
            root.Arrange(new Rect(0, 0, 768, 528));
            root.UpdateLayout();
            before = (window.TopBar.IsMeasureValid, window.BottomBar.IsMeasureValid);

            // What WPF calls when the window comes onto a display scaled differently.
            typeof(MainWindow)
                .GetMethod("OnDpiChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(window, [new DpiScale(1, 1), new DpiScale(1.5, 1.5)]);
            after = (window.TopBar.IsMeasureValid, window.BottomBar.IsMeasureValid);
        });

        Assert.Equal((true, true), before);
        Assert.Equal((false, false), after);
    }

    [Fact]
    public async Task R006_ASecondOpenWhileAPairLoads_AsksForNothing()
    {
        var dialogs = new CountingDialogs();

        await WpfThread.RunAsync(() =>
        {
            var window = new MainWindow(dialogs, new ImagePairLoader(new StalledLoader()));

            // The body holds the dispatcher, so the first open is still loading, suspended at
            // its first await, when the second click comes.
            window.OpenButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            window.OpenButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });

        Assert.Equal(1, dialogs.Asked);
    }

    [Fact]
    public async Task R065_ChangingImageB_ReplacesItAlone_AndKeepsTheView()
    {
        var loader = new ModeLoader(shownAsStored: true);
        var dialogs = new CountingDialogs();
        MainWindow window = null!;
        await WpfThread.RunAsync(() =>
        {
            window = new MainWindow(dialogs, new ImagePairLoader(loader));
            window.OpenButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });
        await WaitForPairAsync(window);

        object? imageA = null;
        Task changed = null!;
        Task switched = null!;
        await WpfThread.RunAsync(() =>
        {
            Assert.True(window.ChangeAfterButton.IsEnabled);
            imageA = window.Compare.BeforeImage.Source;
            window.Compare.Divider.Position = 0.3;
            changed = window.ChangeImageAsync(ImageSide.B, "the test");
        });
        await changed;

        await WpfThread.RunAsync(() =>
        {
            Assert.Equal(("a.png", "c.png"), (window.BeforeName.Text, window.AfterName.Text));
            Assert.StartsWith("a.png  vs  c.png  —  ", window.Title);
            Assert.Same(imageA, window.Compare.BeforeImage.Source);
            Assert.Equal(0.3, window.Compare.Divider.Position);
            Assert.True(window.HistogramButton.IsEnabled);
        });
        Assert.Equal((1, 1), (dialogs.Asked, dialogs.AskedForOne));
        Assert.Equal(ImageSide.B, dialogs.SideAsked);
        Assert.Equal(@"C:\pair", dialogs.DirectoryAsked);
        Assert.Equal([@"C:\pair\a.png", @"C:\pair\b.png", @"C:\pair\c.png"], loader.Loaded);

        // A switch of mode after the change decodes the pair now on screen, not the old file.
        await WpfThread.RunAsync(() => switched = window.ToggleModeAsync(DisplayMode.Histogram));
        await switched;
        Assert.Equal([@"C:\pair\a.png", @"C:\pair\c.png"], loader.Loaded.Skip(3));
    }

    [Fact]
    public async Task R065_ChangingImageA_ReplacesItAlone()
    {
        var loader = new ModeLoader(shownAsStored: true);
        var dialogs = new CountingDialogs();
        MainWindow window = null!;
        await WpfThread.RunAsync(() =>
        {
            window = new MainWindow(dialogs, new ImagePairLoader(loader));
            window.OpenButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });
        await WaitForPairAsync(window);

        object? imageB = null;
        Task changed = null!;
        await WpfThread.RunAsync(() =>
        {
            imageB = window.Compare.AfterImage.Source;
            changed = window.ChangeImageAsync(ImageSide.A, "the test");
        });
        await changed;

        await WpfThread.RunAsync(() =>
        {
            Assert.Equal(("c.png", "b.png"), (window.BeforeName.Text, window.AfterName.Text));
            Assert.Same(imageB, window.Compare.AfterImage.Source);
        });
        Assert.Equal(ImageSide.A, dialogs.SideAsked);
        Assert.Equal([@"C:\pair\a.png", @"C:\pair\b.png", @"C:\other\c.png"], loader.Loaded);

        // Open… then starts in the folder of the image A chosen last, the change's (R-001); the
        // open it starts is waited for, so nothing of it runs on into the next test.
        await WpfThread.RunAsync(() => window.OpenButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));
        Assert.Equal(@"C:\other", dialogs.PairDirectoryAsked);
        await WaitUntilAsync(window, w => w.BeforeName.Text == "a.png", "the pair opened again");
    }

    [Fact]
    public async Task R065_CancellingTheDialog_LeavesThePairAsItWas()
    {
        var loader = new ModeLoader(shownAsStored: true);
        var dialogs = new CountingDialogs { CancelsTheChange = true };
        MainWindow window = null!;
        await WpfThread.RunAsync(() =>
        {
            window = new MainWindow(dialogs, new ImagePairLoader(loader));
            window.OpenButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });
        await WaitForPairAsync(window);

        Task changed = null!;
        await WpfThread.RunAsync(() => changed = window.ChangeImageAsync(ImageSide.B, "the test"));
        await changed;

        await WpfThread.RunAsync(() => Assert.Equal(("a.png", "b.png"), (window.BeforeName.Text, window.AfterName.Text)));
        Assert.Equal(1, dialogs.AskedForOne);
        Assert.Equal([@"C:\pair\a.png", @"C:\pair\b.png"], loader.Loaded);
    }

    [Fact]
    public async Task R065_AChangeWhileAPairLoads_OrWithNoPair_AsksForNothing()
    {
        var dialogs = new CountingDialogs();

        await WpfThread.RunAsync(() =>
        {
            var window = new MainWindow(dialogs, new ImagePairLoader(new StalledLoader()));

            Assert.False(window.ChangeBeforeButton.IsEnabled);
            Assert.False(window.ChangeAfterButton.IsEnabled);
            window.ChangeAfterButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            // The open is still loading, suspended at its first await, when the change comes.
            window.OpenButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            window.ChangeBeforeButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });

        Assert.Equal((1, 0), (dialogs.Asked, dialogs.AskedForOne));
    }

    /// <summary>
    /// Picks the same pair whenever it is asked, and for one image <c>c.png</c>, in another
    /// folder for A; counts the asking and keeps the folders asked for; with
    /// <see cref="CancelsTheChange"/> the dialog for one image is cancelled.
    /// </summary>
    private sealed class CountingDialogs : IFileDialogService
    {
        public int Asked { get; private set; }

        public int AskedForOne { get; private set; }

        public ImageSide? SideAsked { get; private set; }

        public string? DirectoryAsked { get; private set; }

        public string? PairDirectoryAsked { get; private set; }

        public bool CancelsTheChange { get; init; }

        public ImagePairSelection? PickImagePair(string? initialDirectory = null)
        {
            Asked++;
            PairDirectoryAsked = initialDirectory;
            return new ImagePairSelection(@"C:\pair\a.png", @"C:\pair\b.png");
        }

        public string? PickImage(ImageSide side, string? initialDirectory = null)
        {
            AskedForOne++;
            SideAsked = side;
            DirectoryAsked = initialDirectory;
            return CancelsTheChange ? null : (side == ImageSide.A ? @"C:\other\c.png" : @"C:\pair\c.png");
        }
    }

    /// <summary>Probes every file as the same small image and never finishes a decode.</summary>
    private sealed class StalledLoader : IImageLoader
    {
        public ImageInfo Probe(string path) => new(path, 4, 4, 8, ColorChannels.Rgb);

        public Task<LoadedImage> LoadAsync(
            string path, DisplayMode mode = DisplayMode.Linear, CancellationToken cancellationToken = default) =>
            new TaskCompletionSource<LoadedImage>().Task;
    }

    [Fact]
    public async Task R113_ASwitchOfMode_DecodesThePairAgain_AndKeepsTheDivider()
    {
        var loader = new ModeLoader(shownAsStored: true);
        MainWindow window = null!;
        await WpfThread.RunAsync(() =>
        {
            window = new MainWindow(new CountingDialogs(), new ImagePairLoader(loader));
            window.OpenButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });
        await WaitForPairAsync(window);

        Task switched = null!;
        await WpfThread.RunAsync(() =>
        {
            window.Compare.Divider.Position = 0.3;
            switched = window.ToggleModeAsync(DisplayMode.Histogram);
        });
        await switched;

        await WpfThread.RunAsync(() =>
        {
            Assert.Equal(0.3, window.Compare.Divider.Position);
            Assert.True(window.HistogramButton.IsChecked);
            Assert.False(window.FalseColoursButton.IsChecked);
            Assert.EndsWith("  ·  Histogram", window.StatusPair.Text);
        });
        Assert.Equal(
            [DisplayMode.Linear, DisplayMode.Linear, DisplayMode.Histogram, DisplayMode.Histogram],
            loader.Modes);
    }

    /// <summary>
    /// A held H switches once: its repeats, which come about thirty times a second, would each
    /// decode the pair again once the switch before them has ended (R-114, ADR 0044). The test
    /// calls what the key handler hands the repeat to; the F1 test presses a real repeat.
    /// </summary>
    [Fact]
    public async Task R114_AHeldModeKeySwitchesOnce_ItsRepeatsDoNothing()
    {
        var loader = new ModeLoader(shownAsStored: true);
        MainWindow window = null!;
        await WpfThread.RunAsync(() =>
        {
            window = new MainWindow(new CountingDialogs(), new ImagePairLoader(loader));
            window.OpenButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });
        await WaitForPairAsync(window);

        Task switched = null!;
        await WpfThread.RunAsync(() => switched = window.OnModeKeyAsync(DisplayMode.Histogram, isRepeat: false));
        await switched;
        await WpfThread.RunAsync(() => switched = window.OnModeKeyAsync(DisplayMode.Histogram, isRepeat: true));
        await switched;
        await WpfThread.RunAsync(() => switched = window.OnModeKeyAsync(DisplayMode.Histogram, isRepeat: true));
        await switched;

        await WpfThread.RunAsync(() => Assert.True(window.HistogramButton.IsChecked));
        Assert.Equal([DisplayMode.Linear, DisplayMode.Linear, DisplayMode.Histogram, DisplayMode.Histogram], loader.Modes);
    }

    [Fact]
    public async Task R114_TheModeAlreadyShown_SwitchesBackToLinear()
    {
        var loader = new ModeLoader(shownAsStored: true);
        MainWindow window = null!;
        await WpfThread.RunAsync(() =>
        {
            window = new MainWindow(new CountingDialogs(), new ImagePairLoader(loader));
            window.OpenButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });
        await WaitForPairAsync(window);

        Task switched = null!;
        await WpfThread.RunAsync(() => switched = window.ToggleModeAsync(DisplayMode.FalseColours));
        await switched;
        await WpfThread.RunAsync(() => switched = window.ToggleModeAsync(DisplayMode.FalseColours));
        await switched;

        await WpfThread.RunAsync(() =>
        {
            Assert.False(window.HistogramButton.IsChecked);
            Assert.False(window.FalseColoursButton.IsChecked);
            Assert.True(window.HistogramButton.IsEnabled);
            Assert.DoesNotContain("  ·  False colours", window.StatusPair.Text);
        });
        Assert.Equal(
            [DisplayMode.Linear, DisplayMode.Linear, DisplayMode.FalseColours, DisplayMode.FalseColours, DisplayMode.Linear, DisplayMode.Linear],
            loader.Modes);
    }

    [Fact]
    public async Task R110_APairNotShownAsStored_DisablesTheButtons_AndIgnoresTheKeys()
    {
        var loader = new ModeLoader(shownAsStored: false);
        MainWindow window = null!;
        await WpfThread.RunAsync(() =>
        {
            window = new MainWindow(new CountingDialogs(), new ImagePairLoader(loader));
            window.OpenButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });
        await WaitForPairAsync(window);

        Task switched = null!;
        await WpfThread.RunAsync(() => switched = window.ToggleModeAsync(DisplayMode.Histogram));
        await switched;

        await WpfThread.RunAsync(() =>
        {
            Assert.False(window.HistogramButton.IsEnabled);
            Assert.False(window.FalseColoursButton.IsEnabled);
            Assert.False(window.HistogramButton.IsChecked);
        });
        Assert.Equal([DisplayMode.Linear, DisplayMode.Linear], loader.Modes);
    }

    /// <summary>
    /// Waits for the open the test started to put its pair on screen: the open runs on through
    /// the dispatcher once the test's body returns, so the window is looked at between pumps.
    /// </summary>
    private static Task WaitForPairAsync(MainWindow window) =>
        WaitUntilAsync(window, w => w.StatusPair.Text.Length > 0, "the pair was shown");

    /// <summary>Waits up to 5 s, between pumps of the dispatcher, for the window to satisfy a condition.</summary>
    private static async Task WaitUntilAsync(MainWindow window, Func<MainWindow, bool> condition, string what)
    {
        for (int i = 0; i < 100; i++)
        {
            bool satisfied = false;
            await WpfThread.RunAsync(() => satisfied = condition(window));
            if (satisfied)
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail($"not within 5 s: {what}");
    }

    /// <summary>
    /// Probes every file as one small image, shown as stored or not, decodes it as a 2 x 2
    /// bitmap at once, and records the path and the mode of every load.
    /// </summary>
    private sealed class ModeLoader(bool shownAsStored) : IImageLoader
    {
        public List<DisplayMode> Modes { get; } = [];

        public List<string> Loaded { get; } = [];

        public ImageInfo Probe(string path) =>
            new(path, 2, 2, 32, ColorChannels.Rgb, IsFloat: shownAsStored, ShownAsStored: shownAsStored);

        public Task<LoadedImage> LoadAsync(
            string path, DisplayMode mode = DisplayMode.Linear, CancellationToken cancellationToken = default)
        {
            lock (Modes)
            {
                Modes.Add(mode);
                Loaded.Add(path);
            }

            var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[16], 8);
            bitmap.Freeze();
            return Task.FromResult(new LoadedImage(Probe(path), bitmap, mode));
        }
    }

    /// <summary>
    /// CR-006 of the review of 2026-11-13. The read-out was written only when the pointer moved
    /// onto another image pixel, so under a pointer at rest a switch of display mode, or a
    /// changed image, left the values of the bitmaps that were no longer on screen.
    /// </summary>
    [Theory]
    [InlineData("a switch to Histogram", "A  90  90  90", "B  91  91  91")]
    [InlineData("image B changed", "A  35  35  35", "B  40  40  40")]
    public async Task R051_TheReadOut_FollowsWhatIsUnderARestingPointer(string change, string expectedA, string expectedB)
    {
        MainWindow window = null!;
        await WpfThread.RunAsync(() =>
        {
            window = new MainWindow(new CountingDialogs(), new ImagePairLoader(new LevelLoader()));

            // A size for the surface, so that a position on it is a pixel of the image.
            var root = (Grid)window.Content;
            root.Measure(new Size(768, 528));
            root.Arrange(new Rect(0, 0, 768, 528));
            root.UpdateLayout();
            window.OpenButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });
        await WaitForPairAsync(window);

        (string Cursor, string A, string B) before = default;
        Task changed = null!;
        await WpfThread.RunAsync(() =>
        {
            // The pointer comes to rest on the image, once; nothing reports it again.
            window.Compare.ReportPointer(new Point(window.Compare.ActualWidth / 2, window.Compare.ActualHeight / 2));
            before = (window.StatusCursor.Text, window.StatusBefore.Text, window.StatusAfter.Text);
            changed = change == "image B changed"
                ? window.ChangeImageAsync(ImageSide.B, "the test")
                : window.ToggleModeAsync(DisplayMode.Histogram);
        });
        await changed;

        (string Cursor, string A, string B) after = default;
        await WpfThread.RunAsync(() => after = (window.StatusCursor.Text, window.StatusBefore.Text, window.StatusAfter.Text));

        Assert.Equal(("A  35  35  35", "B  36  36  36"), (before.A, before.B));
        Assert.NotEqual(string.Empty, before.Cursor);
        Assert.Equal((before.Cursor, expectedA, expectedB), after);
    }

    /// <summary>
    /// Decodes every file as an 8 x 8 image of one level, which says what was decoded: 35 for
    /// <c>a.png</c>, 36 for <c>b.png</c>, 40 for <c>c.png</c>, and 55 more in any mode but linear.
    /// </summary>
    private sealed class LevelLoader : IImageLoader
    {
        public ImageInfo Probe(string path) =>
            new(path, 8, 8, 32, ColorChannels.Rgb, IsFloat: true, ShownAsStored: true);

        public Task<LoadedImage> LoadAsync(
            string path, DisplayMode mode = DisplayMode.Linear, CancellationToken cancellationToken = default)
        {
            int level = (System.IO.Path.GetFileName(path) switch { "a.png" => 35, "b.png" => 36, _ => 40 })
                + (mode == DisplayMode.Linear ? 0 : 55);
            byte[] pixels = new byte[8 * 8 * 4];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = i % 4 == 3 ? (byte)255 : (byte)level;
            }

            var bitmap = BitmapSource.Create(8, 8, 96, 96, PixelFormats.Bgra32, null, pixels, 32);
            bitmap.Freeze();
            return Task.FromResult(new LoadedImage(Probe(path), bitmap, mode));
        }
    }

    private const string ManualPage = "https://github.com/tcarean/AstroDarkroom/wiki/ImageVisualCompare";

    /// <summary>
    /// F1, pressed wherever the focus is, and the Help button open the manual at the section of
    /// what is on screen (R-066, ADR 0056): "Opening a pair" while no pair is shown, then the
    /// section of the display mode the pair is shown in. The key goes through the window's own
    /// key handler, as the keyboard's would. The browser never starts: the address is kept.
    /// </summary>
    [Fact]
    public async Task R066_F1AndHelp_OpenTheManualAtTheSectionOfTheViewOnScreen()
    {
        var opened = new List<string>();
        MainWindow window = null!;
        await WpfThread.RunAsync(() =>
        {
            window = new MainWindow(new CountingDialogs(), new ImagePairLoader(new ModeLoader(shownAsStored: true)))
            {
                OpenAddress = opened.Add,
            };
            Assert.True(PressF1(window.Compare));
            window.OpenButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });
        await WaitForPairAsync(window);
        await WpfThread.RunAsync(() => Assert.True(PressF1(window.Compare)));

        Task switched = null!;
        await WpfThread.RunAsync(() => switched = window.ToggleModeAsync(DisplayMode.Histogram));
        await switched;
        await WpfThread.RunAsync(() => Assert.True(PressF1(window.OpenButton)));

        await WpfThread.RunAsync(() => switched = window.ToggleModeAsync(DisplayMode.FalseColours));
        await switched;
        await WpfThread.RunAsync(() => window.HelpButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));

        Assert.Equal(
            [ManualPage + "#opening-a-pair", ManualPage + "#the-window", ManualPage + "#histogram", ManualPage + "#false-colours"],
            opened);
    }

    /// <summary>
    /// While the loading veil is on screen, here over a pair in Linear whose switch to Histogram
    /// is still decoding, F1 opens the section that explains the veil, not the mode's (R-066).
    /// </summary>
    [Fact]
    public async Task R066_F1UnderTheLoadingVeil_OpensOpeningAPair()
    {
        var opened = new List<string>();
        MainWindow window = null!;
        await WpfThread.RunAsync(() =>
        {
            window = new MainWindow(new CountingDialogs(), new ImagePairLoader(new FirstPairOnlyLoader())) { OpenAddress = opened.Add };
            window.OpenButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });
        await WaitForPairAsync(window);

        await WpfThread.RunAsync(() =>
        {
            Assert.True(PressF1(window.Compare));

            // Never finishes: the second pair's decode stalls.
            _ = window.ToggleModeAsync(DisplayMode.Histogram);
        });
        await WpfThread.RunAsync(() => Assert.True(PressF1(window.Compare)));

        Assert.Equal([ManualPage + "#the-window", ManualPage + "#opening-a-pair"], opened);
    }

    /// <summary>
    /// A held F1 opens the manual once: its repeats, some thirty a second, would each open the
    /// browser again. The repeats go through the window's key handler, as the keyboard's would,
    /// so a handler that dropped the key's repeat flag would fail here.
    /// </summary>
    [Fact]
    public async Task R066_AHeldF1_OpensTheManualOnce()
    {
        var opened = new List<string>();
        await WpfThread.RunAsync(() =>
        {
            var window = new MainWindow(new CountingDialogs(), new ImagePairLoader(new StalledLoader())) { OpenAddress = opened.Add };
            Assert.True(PressF1(window.Compare));
            Assert.True(PressF1(window.Compare, isRepeat: true));
            Assert.True(PressF1(window.Compare, isRepeat: true));
        });

        Assert.Equal([ManualPage + "#opening-a-pair"], opened);
    }

    /// <summary>
    /// The Help button is the drawn icon, a disc of the accent colour, then the word, with its
    /// tooltip and its automation name, between Open… and About; the tooltip is dark, the
    /// theme's own style, not WPF's light one (ADR 0018).
    /// </summary>
    [Fact]
    public async Task R066_TheHelpButton_ShowsTheDrawnIconAndTheWord_BetweenOpenAndAbout()
    {
        await WpfThread.RunAsync(() =>
        {
            var window = new MainWindow(new CountingDialogs(), new ImagePairLoader(new StalledLoader()));

            var content = Assert.IsType<StackPanel>(window.HelpButton.Content);
            Image icon = Assert.IsType<Image>(content.Children[0]);
            Assert.Same(window.HelpText, content.Children[1]);
            Assert.Equal("Help", window.HelpText.Text);
            Assert.Equal((16.0, 16.0), (icon.Width, icon.Height));
            var drawing = Assert.IsType<DrawingGroup>(Assert.IsType<DrawingImage>(icon.Source).Drawing);
            var disc = Assert.IsType<GeometryDrawing>(drawing.Children[0]);
            Assert.IsType<EllipseGeometry>(disc.Geometry);
            Assert.Equal(Color.FromRgb(0x3F, 0xE0, 0xC8), Assert.IsType<SolidColorBrush>(disc.Brush).Color);

            Assert.Equal("The manual, at the section about what is on screen (F1)", window.HelpButton.ToolTip);
            Assert.Equal("Help", System.Windows.Automation.AutomationProperties.GetName(window.HelpButton));
            int at = window.Toolbar.Children.IndexOf(window.HelpButton);
            Assert.Same(window.OpenButton, window.Toolbar.Children[at - 1]);
            Assert.Same(window.AboutButton, window.Toolbar.Children[at + 1]);

            var tip = Assert.IsType<Style>(window.TryFindResource(typeof(ToolTip)));
            Setter background = tip.Setters.OfType<Setter>().Single(s => s.Property == Control.BackgroundProperty);
            Assert.Equal(Color.FromRgb(0x23, 0x27, 0x2E), Assert.IsType<SolidColorBrush>(background.Value).Color);
        });
    }

    /// <summary>Raises a key-down of F1, without a modifier, on <paramref name="target"/>, as the
    /// keyboard would with the focus there, a repeat of a held key when asked; returns whether
    /// something handled it.</summary>
    private static bool PressF1(UIElement target, bool isRepeat = false)
    {
        var e = new KeyEventArgs(Keyboard.PrimaryDevice, new KeySource(), 0, Key.F1) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        if (isRepeat)
        {
            // WPF marks a repeat from the keyboard's state, through a member of its own; the test
            // marks it the same way.
            typeof(KeyEventArgs)
                .GetMethod("SetRepeat", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(e, [true]);
        }

        target.RaiseEvent(e);
        return e.Handled;
    }

    /// <summary>The presentation source a key event names: a window never shown has none, and
    /// nothing is drawn through this one.</summary>
    private sealed class KeySource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = null!;

        public override bool IsDisposed => false;

        protected override CompositionTarget GetCompositionTargetCore() => null!;
    }

    /// <summary>
    /// Decodes the first pair at once, as <see cref="ModeLoader"/> does, and never finishes a
    /// later decode, so the load after the first stays behind the loading veil.
    /// </summary>
    private sealed class FirstPairOnlyLoader : IImageLoader
    {
        private readonly ModeLoader _first = new(shownAsStored: true);
        private int _loads;

        public ImageInfo Probe(string path) => _first.Probe(path);

        public Task<LoadedImage> LoadAsync(
            string path, DisplayMode mode = DisplayMode.Linear, CancellationToken cancellationToken = default) =>
            Interlocked.Increment(ref _loads) <= 2
                ? _first.LoadAsync(path, mode, cancellationToken)
                : new TaskCompletionSource<LoadedImage>().Task;
    }

    [Fact]
    public async Task R114_AToggleFlippedByItsOwnClick_IsPutBackWhenTheSwitchIsRefused()
    {
        // A ToggleButton flips IsChecked before it raises Click; on a pair that allows no mode
        // the switch is refused, and the toggle must not stay lit for a mode not shown.
        var loader = new ModeLoader(shownAsStored: false);
        MainWindow window = null!;
        await WpfThread.RunAsync(() =>
        {
            window = new MainWindow(new CountingDialogs(), new ImagePairLoader(loader));
            window.OpenButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });
        await WaitForPairAsync(window);

        Task switched = null!;
        await WpfThread.RunAsync(() =>
        {
            window.HistogramButton.IsChecked = true;
            switched = window.ToggleModeAsync(DisplayMode.Histogram);
        });
        await switched;

        await WpfThread.RunAsync(() => Assert.False(window.HistogramButton.IsChecked));
        Assert.Equal([DisplayMode.Linear, DisplayMode.Linear], loader.Modes);
    }
}
