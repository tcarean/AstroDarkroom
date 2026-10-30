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
    // About is left out: its click opens a modal dialog, which a never-shown window cannot.
    [Theory]
    [InlineData("ZoomOutButton")]
    [InlineData("ZoomInButton")]
    [InlineData("FitButton")]
    [InlineData("ActualSizeButton")]
    [InlineData("HistogramButton")]
    [InlineData("FalseColoursButton")]
    [InlineData("OpenButton")]
    public async Task R063_AToolbarClick_HandsTheFocusBackToTheComparisonSurface(string buttonName)
    {
        object? surface = null;
        object? focused = null;

        await WpfThread.RunAsync(() =>
        {
            var window = new MainWindow(new CountingDialogs(), new ImagePairLoader(new StalledLoader()));
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

    /// <summary>Picks the same pair whenever it is asked, and counts the asking.</summary>
    private sealed class CountingDialogs : IFileDialogService
    {
        public int Asked { get; private set; }

        public ImagePairSelection? PickImagePair(string? initialDirectory = null)
        {
            Asked++;
            return new ImagePairSelection(@"C:\pair\a.png", @"C:\pair\b.png");
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
    /// decode the pair again once the switch before them has ended (R-114, ADR 0044). A test cannot
    /// make a <c>KeyEventArgs</c> a repeat, so it calls what the key handler hands the repeat to.
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
    private static async Task WaitForPairAsync(MainWindow window)
    {
        for (int i = 0; i < 100; i++)
        {
            bool shown = false;
            await WpfThread.RunAsync(() => shown = window.StatusPair.Text.Length > 0);
            if (shown)
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail("the pair was not shown within 5 s");
    }

    /// <summary>
    /// Probes every file as one small image, shown as stored or not, decodes it as a 2 x 2
    /// bitmap at once, and records the mode of every load.
    /// </summary>
    private sealed class ModeLoader(bool shownAsStored) : IImageLoader
    {
        public List<DisplayMode> Modes { get; } = [];

        public ImageInfo Probe(string path) =>
            new(path, 2, 2, 32, ColorChannels.Rgb, IsFloat: shownAsStored, ShownAsStored: shownAsStored);

        public Task<LoadedImage> LoadAsync(
            string path, DisplayMode mode = DisplayMode.Linear, CancellationToken cancellationToken = default)
        {
            lock (Modes)
            {
                Modes.Add(mode);
            }

            var bitmap = BitmapSource.Create(2, 2, 96, 96, PixelFormats.Bgra32, null, new byte[16], 8);
            bitmap.Freeze();
            return Task.FromResult(new LoadedImage(Probe(path), bitmap, mode));
        }
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
