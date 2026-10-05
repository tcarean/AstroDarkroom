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

using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ImageVisualCompare.Core;
using ImageVisualCompare.Wpf.Imaging;
using ImageVisualCompare.Wpf.Interop;
using ImageVisualCompare.Wpf.Services;

namespace ImageVisualCompare.Wpf;

public partial class MainWindow : Window
{
    private readonly IFileDialogService _dialogs = new FileDialogService();
    private readonly ImagePairLoader _loader = new(new RoutingImageLoader());
    private readonly DispatcherTimer _memoryTimer = new() { Interval = TimeSpan.FromSeconds(2) };

    private ImagePair? _pair;
    private string? _lastDirectory;

    public MainWindow()
    {
        InitializeComponent();

        DarkTitleBar.Apply(this);

        Compare.ViewChanged += (_, _) => UpdateZoomReadout();
        Compare.CursorContentPointChanged += (_, point) => UpdatePixelReadout(point);

        ZoomInButton.Click += (_, _) => Compare.ZoomIn();
        ZoomOutButton.Click += (_, _) => Compare.ZoomOut();
        FitButton.Click += (_, _) => Compare.ZoomToFit();
        ActualSizeButton.Click += (_, _) => Compare.ZoomToActualSize();
        OpenButton.Click += async (_, _) => await OpenPairAsync();
        AboutButton.Click += (_, _) =>
            MessageDialog.Show(this, "About " + VersionInfo.Banner, VersionInfo.About);

        Title = VersionInfo.Banner;
        StatusLegal.Text = $"{VersionInfo.Banner} · {VersionInfo.Copyright}";

        // Sampled on a timer rather than on every view change: the render thread does its work
        // after the UI thread has finished, so a reading taken during a zoom is always stale
        // and reports less than the process is really using.
        _memoryTimer.Tick += (_, _) => StatusMemory.Text = MemoryHousekeeping.Snapshot().Describe();

        Loaded += async (_, _) => await OpenPairAsync();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.O && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;
            await OpenPairAsync();
        }
    }

    private async Task OpenPairAsync()
    {
        // A pair given on the command line is used once, then the dialogs take over as normal.
        ImagePairSelection? fromCommandLine = App.TakeStartupSelection();

        while (true)
        {
            ImagePairSelection? selection = fromCommandLine ?? _dialogs.PickImagePair(_lastDirectory);
            fromCommandLine = null;

            if (selection is null)
            {
                // Cancelling the very first prompt means the user did not want to be here.
                // Cancelling later just means they changed their mind about swapping pairs.
                if (_pair is null)
                {
                    Close();
                }

                return;
            }

            _lastDirectory = Path.GetDirectoryName(selection.BeforePath);

            ImagePairLoadResult result = await LoadWithOverlayAsync(selection);

            if (result.IsSuccess)
            {
                ShowPair(result.Pair!);
                return;
            }

            // Declining the padding is an answer, not an error. Showing a second dialog saying
            // so would only make the user dismiss their own decision.
            if (!result.WasDeclined)
            {
                MessageDialog.Show(this, "Cannot compare these files", result.Error!);
            }
        }
    }

    private async Task<ImagePairLoadResult> LoadWithOverlayAsync(ImagePairSelection selection)
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        LoadingBar.Value = 0;
        LoadingText.Text = "Opening…";

        var progress = new Progress<ImageLoadProgress>(p =>
        {
            LoadingBar.Value = p.Fraction;
            LoadingText.Text = p.FileName is null ? "Almost there…" : $"Decoding {p.FileName}…";
        });

        try
        {
            return await _loader.LoadAsync(
                selection.BeforePath, selection.AfterPath, progress, ConfirmPaddingAsync);
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Asks before padding a mismatched pair. Called from a thread pool thread, and reached
    /// before either file has been decoded, so this is the last cheap moment to back out.
    /// </summary>
    private Task<bool> ConfirmPaddingAsync(PadRequest request) => Dispatcher.InvokeAsync(() =>
    {
        // The overlay is up at this point and a dialog sitting on top of a progress bar that
        // is not moving reads as a hang.
        Visibility overlay = LoadingOverlay.Visibility;
        LoadingOverlay.Visibility = Visibility.Collapsed;

        try
        {
            return MessageDialog.Confirm(this, "Images have different sizes", request.Message);
        }
        finally
        {
            LoadingOverlay.Visibility = overlay;
        }
    }).Task;

    private void ShowPair(ImagePair pair)
    {
        if (_pair is not null)
        {
            // The incoming pair was decoded while the outgoing one stayed on screen, so both are
            // resident up to here. Drop the outgoing one before the incoming one is shown, so
            // that from here on only one is.
            _pair = null;
            Compare.Clear();
            MemoryHousekeeping.ReleaseImageBuffers();
        }

        _pair = pair;
        Compare.SetPair(pair);
        Compare.Focus();

        Title = $"{pair.Before.Info.FileName}  vs  {pair.After.Info.FileName}  —  {VersionInfo.Banner}";
        ToolbarTitle.Text = $"{pair.Before.Info.FileName}   vs   {pair.After.Info.FileName}";
        // When the pair had to be padded, the canvas size alone would be a half-truth: neither
        // image is that size. Both originals are named instead.
        string dimensions = pair.Pad.RequiresPadding
            ? $"{pair.Pad.Dimensions} padded  ·  A {pair.Before.Info.Dimensions}  ·  B {pair.After.Info.Dimensions}"
            : pair.Before.Info.Dimensions;

        StatusPair.Text =
            $"{dimensions}  ·  A {pair.Before.Info.FormatDescription}  ·  B {pair.After.Info.FormatDescription}";

        UpdateZoomReadout();
        StatusMemory.Text = MemoryHousekeeping.Snapshot().Describe();
        _memoryTimer.Start();
    }

    private void UpdateZoomReadout() => ZoomLabel.Text = $"{Compare.Viewport.Scale * 100:F0}%";

    /// <summary>
    /// Shows the pixel under the cursor from both images at once. On a pair of finished astro
    /// frames the interesting differences are often below what the eye picks out of a dark
    /// field, and the numbers settle it.
    /// </summary>
    private void UpdatePixelReadout(PointD? point)
    {
        if (_pair is null || point is not { } content)
        {
            StatusCursor.Text = string.Empty;
            StatusBefore.Text = string.Empty;
            StatusAfter.Text = string.Empty;
            return;
        }

        int x = (int)content.X;
        int y = (int)content.Y;

        StatusCursor.Text = $"{x}, {y}";
        StatusBefore.Text = $"A {Describe(_pair.Before.Bitmap, x, y)}";
        StatusAfter.Text = $"B {Describe(_pair.After.Bitmap, x, y)}";
    }

    private static string Describe(BitmapSource bitmap, int x, int y)
    {
        if (x < 0 || y < 0 || x >= bitmap.PixelWidth || y >= bitmap.PixelHeight)
        {
            return "—";
        }

        // Everything is Bgra32 by the time it gets here, so one pixel is four bytes in BGRA
        // order regardless of what the file on disk was.
        byte[] pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return $"{pixel[2],3} {pixel[1],3} {pixel[0],3}";
    }
}
