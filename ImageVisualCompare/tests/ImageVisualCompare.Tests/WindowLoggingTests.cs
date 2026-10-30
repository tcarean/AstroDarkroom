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
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageVisualCompare.Core;
using ImageVisualCompare.Wpf;
using ImageVisualCompare.Wpf.Imaging;
using ImageVisualCompare.Wpf.Logging;
using ImageVisualCompare.Wpf.Services;

namespace ImageVisualCompare.Tests;

/// <summary>
/// What the main window and the comparison surface write in the diagnostic log (R-121), on the
/// WPF test thread (ADR 0032), with dialogs and a loader of the test's own: the window is built,
/// never shown, and driven through its buttons. The capture sees the window's lines because a
/// dispatcher operation runs in the execution context of the call that queued it.
/// </summary>
public class WindowLoggingTests
{
    /// <summary>
    /// Run on the WPF test thread before a capture opens, so that the thread exists by then: it
    /// is created in the execution context of the test that first uses it, and what WPF starts
    /// on it then (the App's own start, which builds a window of its own) would otherwise write
    /// into that test's capture.
    /// </summary>
    private static readonly Action StartOutsideTheCapture = () => { };

    [Fact]
    public async Task R121_AnOpenLogsWhereItCameFromWhatTheDialogsChoseAndThePairShown()
    {
        var dialogs = new ScriptedDialogs(new ImagePairSelection(@"C:\pair\a.png", @"C:\pair\b.png"), null);
        MainWindow window = null!;
        await WpfThread.RunAsync(StartOutsideTheCapture);
        using LogCapture capture = LogCapture.Start();

        await WpfThread.RunAsync(() =>
        {
            window = new MainWindow(dialogs, new ImagePairLoader(new SizedLoader(2, 2, shownAsStored: false)));
            window.OpenButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });
        await WaitForPairAsync(window);
        await WpfThread.RunAsync(() => window.OpenButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));

        IReadOnlyList<string> lines = capture.MessagesOf(Loggers.Window);
        Assert.Equal(
            [
                "open asked by the Open button",
                @"the dialogs chose A=C:\pair\a.png B=C:\pair\b.png",
                "shown A=a.png B=b.png mode=Linear view=fit",
                "open asked by the Open button",
                "the dialogs were cancelled; the pair on screen stays",
            ],
            WithoutStateLines(lines));
        Assert.Matches(@"^render tier [0-2] of 2, process render mode \w+$", lines[0]);
        Assert.Single(lines, l => l.StartsWith("memory: managed ", StringComparison.Ordinal) && l.Contains(" MiB, process ", StringComparison.Ordinal));
        Assert.Contains(capture.MessagesOf(Loggers.Load), l => l.StartsWith(@"load start A=C:\pair\a.png B=C:\pair\b.png mode=Linear", StringComparison.Ordinal));
        Assert.Single(capture.MessagesOf(Loggers.Surface), l => l.StartsWith("pair set: canvas 2x2, view ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task R121_ASecondOpenWhileAPairLoadsIsLoggedAsIgnored()
    {
        var dialogs = new ScriptedDialogs(new ImagePairSelection(@"C:\pair\a.png", @"C:\pair\b.png"));
        await WpfThread.RunAsync(StartOutsideTheCapture);
        using LogCapture capture = LogCapture.Start();

        await WpfThread.RunAsync(() =>
        {
            var window = new MainWindow(dialogs, new ImagePairLoader(new StalledLoader()));

            // The body holds the dispatcher, so the first open is still loading, suspended at its
            // first await, when the second click comes.
            window.OpenButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            window.OpenButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });

        Assert.Equal(
            [
                "open asked by the Open button",
                @"the dialogs chose A=C:\pair\a.png B=C:\pair\b.png",
                "open asked by the Open button while a pair is being opened: ignored",
            ],
            WithoutStateLines(capture.MessagesOf(Loggers.Window)));
    }

    [Fact]
    public async Task R121_AModeSwitchIsLoggedFromAndTo_AndARefusedOneWithWhy()
    {
        var dialogs = new ScriptedDialogs(new ImagePairSelection(@"C:\pair\a.fit", @"C:\pair\b.fit"));
        MainWindow window = null!;
        Task switched = null!;
        await WpfThread.RunAsync(StartOutsideTheCapture);
        using LogCapture capture = LogCapture.Start();

        await WpfThread.RunAsync(() =>
        {
            window = new MainWindow(dialogs, new ImagePairLoader(new SizedLoader(2, 2, shownAsStored: true)));
            switched = window.ToggleModeAsync(DisplayMode.Histogram);
        });
        await switched;
        await WpfThread.RunAsync(() => window.OpenButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)));
        await WaitForPairAsync(window);
        await WpfThread.RunAsync(() => switched = window.ToggleModeAsync(DisplayMode.Histogram));
        await switched;

        IReadOnlyList<string> lines = capture.MessagesOf(Loggers.Window);
        Assert.Equal("mode switch to Histogram refused: no pair is shown", WithoutStateLines(lines)[0]);
        Assert.Contains("mode switch Linear -> Histogram", lines);
        Assert.Equal("shown A=a.fit B=b.fit mode=Histogram view=kept", lines.Last(l => l.StartsWith("shown ", StringComparison.Ordinal)));
        Assert.Contains(lines, l => l.StartsWith("memory: ", StringComparison.Ordinal) && l.Contains(" before the outgoing pair was released", StringComparison.Ordinal));
        Assert.Single(capture.MessagesOf(Loggers.Surface), l => l.StartsWith("pair replaced, the view kept: canvas 2x2, ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task R121_TheZoomCommandsLogTheZoomTheyLeave()
    {
        var dialogs = new ScriptedDialogs(new ImagePairSelection(@"C:\pair\a.png", @"C:\pair\b.png"));
        MainWindow window = null!;
        await WpfThread.RunAsync(StartOutsideTheCapture);
        using LogCapture capture = LogCapture.Start();

        await WpfThread.RunAsync(() =>
        {
            window = new MainWindow(dialogs, new ImagePairLoader(new SizedLoader(1600, 1200, shownAsStored: false)));
            window.OpenButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });
        await WaitForPairAsync(window);
        await WpfThread.RunAsync(() =>
        {
            // A never-shown window lays nothing out; the surface is given a size so that the
            // viewport has one, as the running window's layout would.
            window.Compare.Measure(new Size(800, 600));
            window.Compare.Arrange(new Rect(0, 0, 800, 600));
            window.Compare.UpdateLayout();
            window.FitButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            window.ActualSizeButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            window.ZoomOutButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        });

        IReadOnlyList<string> zooms = [.. capture.MessagesOf(Loggers.Surface).Where(l => l.StartsWith("zoom ", StringComparison.Ordinal))];
        Assert.Equal(3, zooms.Count);
        Assert.Equal("zoom to fit -> 50%", zooms[0]);
        Assert.Equal("zoom to 100% -> 100%", zooms[1]);
        Assert.StartsWith("zoom out -> ", zooms[2]);
    }

    /// <summary>
    /// The window's lines without those that report a state, not an action: how WPF renders,
    /// written when a window is built, and the memory, written when a pair is shown.
    /// </summary>
    private static IReadOnlyList<string> WithoutStateLines(IEnumerable<string> lines) =>
        [.. lines.Where(l => !l.StartsWith("render tier ", StringComparison.Ordinal) && !l.StartsWith("memory: ", StringComparison.Ordinal))];

    /// <summary>
    /// Waits for the open the test started to put its pair on screen: the open runs on through the
    /// dispatcher once the test's body returns, so the window is looked at between pumps.
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

    /// <summary>Answers the dialogs with the selections given, one per open, then cancels.</summary>
    private sealed class ScriptedDialogs(params ImagePairSelection?[] answers) : IFileDialogService
    {
        private int _asked;

        public ImagePairSelection? PickImagePair(string? initialDirectory = null) =>
            _asked < answers.Length ? answers[_asked++] : null;
    }

    /// <summary>Probes every file as one image of this size and decodes it as such a bitmap at once.</summary>
    private sealed class SizedLoader(int width, int height, bool shownAsStored) : IImageLoader
    {
        public ImageInfo Probe(string path) =>
            new(path, width, height, 32, ColorChannels.Rgb, IsFloat: shownAsStored, ShownAsStored: shownAsStored);

        public Task<LoadedImage> LoadAsync(string path, DisplayMode mode = DisplayMode.Linear, CancellationToken cancellationToken = default)
        {
            var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, new byte[width * height * 4], width * 4);
            bitmap.Freeze();
            return Task.FromResult(new LoadedImage(Probe(path), bitmap, mode));
        }
    }

    /// <summary>Probes every file as the same small image and never finishes a decode.</summary>
    private sealed class StalledLoader : IImageLoader
    {
        public ImageInfo Probe(string path) => new(path, 4, 4, 8, ColorChannels.Rgb);

        public Task<LoadedImage> LoadAsync(string path, DisplayMode mode = DisplayMode.Linear, CancellationToken cancellationToken = default) =>
            new TaskCompletionSource<LoadedImage>().Task;
    }
}
