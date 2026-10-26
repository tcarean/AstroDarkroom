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
    [InlineData("OpenButton")]
    public async Task R063_AToolbarClick_HandsTheFocusBackToTheComparisonSurface(string buttonName)
    {
        object? surface = null;
        object? focused = null;

        await WpfThread.RunAsync(() =>
        {
            var window = new MainWindow(new CountingDialogs(), new ImagePairLoader(new StalledLoader()));
            var button = (Button)window.FindName(buttonName)!;

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

        public Task<LoadedImage> LoadAsync(string path, CancellationToken cancellationToken = default) =>
            new TaskCompletionSource<LoadedImage>().Task;
    }
}
