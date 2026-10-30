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
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ImageVisualCompare.Core;
using ImageVisualCompare.Wpf.Imaging;
using ImageVisualCompare.Wpf.Interop;
using ImageVisualCompare.Wpf.Logging;
using ImageVisualCompare.Wpf.Services;
using NLog;

namespace ImageVisualCompare.Wpf;

/// <summary>
/// The shell: the toolbar, the comparison surface, the status bar with the pixel readout from
/// both images, and the open, error and retry flow around a load.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>The user's actions and the window's answers, in the diagnostic log (R-121).</summary>
    private static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

    private readonly IFileDialogService _dialogs;
    private readonly ImagePairLoader _loader;
    private readonly DispatcherTimer _memoryTimer = new() { Interval = TimeSpan.FromSeconds(2) };

    private ImagePair? _pair;
    private ImagePairSelection? _selection;
    private DisplayMode _mode;
    private string? _lastDirectory;
    private bool _opening;

    /// <summary>
    /// Builds the main window, with its dark caption; it asks for a pair once it has loaded.
    /// </summary>
    public MainWindow()
        : this(new FileDialogService(), new ImagePairLoader(new RoutingImageLoader()))
    {
    }

    /// <summary>
    /// Builds the main window over the given dialogs and loader, which a test replaces with its
    /// own (ADR 0032).
    /// </summary>
    /// <param name="dialogs">Asks the user for the two files.</param>
    /// <param name="loader">Loads the pair the user picked.</param>
    internal MainWindow(IFileDialogService dialogs, ImagePairLoader loader)
    {
        _dialogs = dialogs;
        _loader = loader;

        InitializeComponent();

        DarkTitleBar.Apply(this);

        // How WPF draws, for a report of slowness: tier 0 is software rendering, as under Remote
        // Desktop; WPF gives the tier in the high word (R-121).
        Log.Debug("render tier " + LogText.Int(RenderCapability.Tier >> 16) + " of 2, process render mode "
            + RenderOptions.ProcessRenderMode);

        // The words are the table's, in the language chosen at start-up (ADR 0038); the XAML
        // holds none of its own. The symbols, "−", "+" and "100%", are the same in both.
        UiText text = UiText.Current;
        NavigationLabel.Text = text.Navigation;
        FitButton.Content = text.Fit;
        HistogramButton.Content = text.Histogram;
        FalseColoursButton.Content = text.FalseColours;
        OpenButton.Content = text.Open;
        AboutButton.Content = text.About;
        ChangeBeforeButton.Content = text.ChangeA;
        ChangeAfterButton.Content = text.ChangeB;
        HintText.Text = text.Hint;

        Compare.ViewChanged += (_, _) => UpdateZoomReadout();
        Compare.CursorContentPointChanged += (_, point) => UpdatePixelReadout(point);

        ZoomInButton.Click += (_, _) => Compare.ZoomIn();
        ZoomOutButton.Click += (_, _) => Compare.ZoomOut();
        FitButton.Click += (_, _) => Compare.ZoomToFit();
        ActualSizeButton.Click += (_, _) => Compare.ZoomToActualSize();
        HistogramButton.Click += async (_, _) => await ToggleModeAsync(DisplayMode.Histogram);
        FalseColoursButton.Click += async (_, _) => await ToggleModeAsync(DisplayMode.FalseColours);
        OpenButton.Click += async (_, _) => await OpenPairAsync("the Open button");
        ChangeBeforeButton.Click += async (_, _) => await ChangeImageAsync(ImageSide.A, "the Change A button");
        ChangeAfterButton.Click += async (_, _) => await ChangeImageAsync(ImageSide.B, "the Change B button");
        AboutButton.Click += (_, _) =>
        {
            Log.Info("About opened");
            new AboutWindow { Owner = this }.ShowDialog();
        };

        // Every button is focusable, so a click leaves the keyboard focus on it and the
        // surface's shortcuts would go nowhere until the image is clicked again. One handler on
        // each panel, reached after the clicked button's own, hands the focus back (R-063, ADR
        // 0042).
        var focusBack = new RoutedEventHandler((_, _) => Compare.Focus());
        Toolbar.AddHandler(ButtonBase.ClickEvent, focusBack);
        PairPanel.AddHandler(ButtonBase.ClickEvent, focusBack);

        Title = VersionInfo.Banner;
        StatusLegal.Text = $"{VersionInfo.Banner} · {VersionInfo.Copyright}";

        // Sampled on a timer rather than on every view change: the render thread does its work
        // after the UI thread has finished, so a reading taken during a zoom is always stale
        // and reports less than the process is really using.
        _memoryTimer.Tick += (_, _) => StatusMemory.Text = MemoryHousekeeping.Snapshot().Describe();

        Loaded += async (_, _) =>
        {
            ReportUnknownLanguage();
            await OpenPairAsync("the start");
        };
        PreviewKeyDown += OnPreviewKeyDown;
    }

    /// <summary>
    /// Says, before anything is asked, that the <c>--lang</c> of the command line named no
    /// language this program has, and that the interface is English (R-102); does nothing when
    /// there is nothing to say.
    /// </summary>
    private void ReportUnknownLanguage()
    {
        if (App.TakeUnknownLanguage() is { } value)
        {
            MessageDialog.Show(this, UiText.UnknownLanguageTitle, UiText.UnknownLanguage(value));
        }
    }

    /// <summary>
    /// Opens another pair on Ctrl+O, and switches the display mode on H and F (R-114), wherever
    /// the focus is; a held H or F switches once (<see cref="OnModeKeyAsync"/>), and while a pair
    /// is being opened or switched the three do nothing (<see cref="OpenPairAsync"/>,
    /// <see cref="ToggleModeAsync"/>).
    /// </summary>
    /// <param name="sender">The window itself; not read.</param>
    /// <param name="e">The key; marked handled when it is one of the three.</param>
    private async void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.O && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            e.Handled = true;

            // A held key repeats some thirty times a second. While a pair is being opened a repeat
            // of Ctrl+O would be ignored anyway; it is not passed on, so the log gets no line for each.
            if (!(e.IsRepeat && _opening))
            {
                await OpenPairAsync("Ctrl+O");
            }
        }
        else if (e.Key is Key.H or Key.F && Keyboard.Modifiers == ModifierKeys.None)
        {
            e.Handled = true;
            await OnModeKeyAsync(e.Key == Key.H ? DisplayMode.Histogram : DisplayMode.FalseColours, e.IsRepeat);
        }
    }

    /// <summary>
    /// Takes a press of H or F (R-114): the first switches the mode, as
    /// <see cref="ToggleModeAsync"/> does; a repeat of the key held down does nothing and writes
    /// nothing, since each switch decodes the pair again and a held key repeats some thirty times a
    /// second (ADR 0044). Separate from the key handler so a test can give it a repeat.
    /// </summary>
    /// <param name="mode">The mode of the key: <see cref="DisplayMode.Histogram"/> for H, <see cref="DisplayMode.FalseColours"/> for F.</param>
    /// <param name="isRepeat">True for a repeat of a key held down.</param>
    /// <returns>The switch, once it has ended; a completed task for a repeat.</returns>
    internal Task OnModeKeyAsync(DisplayMode mode, bool isRepeat) =>
        isRepeat ? Task.CompletedTask : ToggleModeAsync(mode);

    /// <summary>
    /// Switches the pair on screen into a display mode, or back to linear when that mode is
    /// already shown (R-114): the same two files are decoded again in the new mode behind the
    /// overlay, and take the place of the old bitmaps under the unchanged zoom, pan and divider
    /// (R-113). Nothing happens while a pair is being opened or switched, with no pair shown,
    /// or on a pair that allows no other mode than linear (R-110).
    /// </summary>
    /// <param name="mode">The mode asked for, <see cref="DisplayMode.Histogram"/> or <see cref="DisplayMode.FalseColours"/>.</param>
    internal async Task ToggleModeAsync(DisplayMode mode)
    {
        // A ToggleButton flips its own IsChecked before it raises Click, so whatever way this
        // returns, a refusal or a failed switch included, the two toggles are put back in step
        // with the mode that is on screen (the code review of 2026-10-26).
        try
        {
            if (_opening || _pair is null || _selection is null || !_pair.AllowsDisplayModes)
            {
                Log.Debug("mode switch to " + mode + " refused: " + (_opening ? "a pair is being opened or switched"
                    : _pair is null || _selection is null ? "no pair is shown"
                    : "the pair is not shown as stored, so it is Linear only"));
                return;
            }

            DisplayMode target = _mode == mode ? DisplayMode.Linear : mode;

            // Only the canvas's size, never the pair: a local holding it would be hoisted into
            // this method's state machine and keep the outgoing pair alive through ShowPair's
            // release, which then freed nothing (measured on 2026-10-30: 667.6 MiB before the
            // release, 665.9 after, 176 MiB outgoing; ADR 0046).
            (int width, int height) canvas = (_pair.PixelWidth, _pair.PixelHeight);
            Log.Info("mode switch " + _mode + " -> " + target);

            _opening = true;
            try
            {
                // The padding was agreed when the pair was opened; the same two files need no
                // second question (R-113).
                ImagePairSelection selection = _selection;
                ImagePairLoadResult result = await LoadWithOverlayAsync(progress =>
                    _loader.LoadAsync(selection.BeforePath, selection.AfterPath, progress, _ => Task.FromResult(true), target));

                if (result.IsSuccess)
                {
                    // The same files have the same canvas; should one have changed on disk in
                    // between, the view is not worth keeping over a wrong canvas.
                    ImagePair pair = result.Pair!;
                    ShowPair(pair, keepView: pair.PixelWidth == canvas.width && pair.PixelHeight == canvas.height);
                }
                else if (!result.WasDeclined)
                {
                    // The load logged the failure with its exception and the message.
                    Log.Info("the mode switch failed: the message is shown, the pair on screen stays");
                    MessageDialog.Show(this, UiText.Current.CannotCompareTitle, result.Error!);
                }
            }
            finally
            {
                _opening = false;
            }
        }
        finally
        {
            SyncModeButtons();
        }
    }

    /// <summary>
    /// Puts the two display-mode toggles in step with the pair on screen: enabled when it allows
    /// the modes, and lit for the mode it is shown in (R-114).
    /// </summary>
    private void SyncModeButtons()
    {
        bool allowed = _pair?.AllowsDisplayModes == true;
        HistogramButton.IsEnabled = allowed;
        FalseColoursButton.IsEnabled = allowed;
        HistogramButton.IsChecked = _mode == DisplayMode.Histogram;
        FalseColoursButton.IsChecked = _mode == DisplayMode.FalseColours;
    }

    /// <summary>
    /// Asks for a pair until one loads, the command line's first if it named one, and shows it.
    /// A failure is reported and a declined padding is not, and both ask again. Cancelling while
    /// no pair is shown closes the window; with a pair shown, that pair stays as it was. A call
    /// made while another is still opening a pair returns at once and asks for nothing.
    /// </summary>
    /// <param name="from">What asked for the open, in English, for the diagnostic log: <c>the start</c>, <c>Ctrl+O</c>.</param>
    private async Task OpenPairAsync(string from)
    {
        // The loading overlay stops only the mouse: Ctrl+O, or Space on the focused Open
        // button, would otherwise start a second open on top of this one, and two pairs would
        // decode at once (R-006).
        if (_opening)
        {
            Log.Debug("open asked by " + from + " while a pair is being opened: ignored");
            return;
        }

        Log.Info("open asked by " + from);
        _opening = true;
        try
        {
            // A pair given on the command line is used once, then the dialogs take over as normal.
            ImagePairSelection? fromCommandLine = App.TakeStartupSelection();

            while (true)
            {
                ImagePairSelection? selection = fromCommandLine ?? _dialogs.PickImagePair(_lastDirectory);
                Log.Info(fromCommandLine is not null ? "the pair of the command line is opened"
                    : selection is not null ? "the dialogs chose A=" + selection.BeforePath + " B=" + selection.AfterPath
                    : "the dialogs were cancelled; " + (_pair is null ? "no pair is shown, so the window closes" : "the pair on screen stays"));
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

                // The mode in use holds for the next pair when that pair allows it (R-113); the
                // loader falls back to linear when it does not.
                ImagePairLoadResult result = await LoadWithOverlayAsync(progress =>
                    _loader.LoadAsync(selection.BeforePath, selection.AfterPath, progress, ConfirmPaddingAsync, _mode));

                if (result.IsSuccess)
                {
                    _selection = selection;
                    ShowPair(result.Pair!);
                    return;
                }

                // Declining the padding is an answer, not an error. Showing a second dialog saying
                // so would only make the user dismiss their own decision.
                if (!result.WasDeclined)
                {
                    // The load logged the failure with its exception and the message.
                    Log.Info("the open failed: the message is shown, then the dialogs ask again");
                    MessageDialog.Show(this, UiText.Current.CannotCompareTitle, result.Error!);
                }
                else
                {
                    Log.Info("the padding was declined: the dialogs ask again");
                }
            }
        }
        finally
        {
            _opening = false;
        }
    }

    /// <summary>
    /// Changes one image of the pair on screen and keeps the other (R-065): one dialog for the
    /// new file, in the folder of the file it replaces, then the load behind the overlay and the
    /// new pair in place of the old. Cancelling the dialog, declining the padding or a file that
    /// cannot be read (its message shown, the dialog not opened again) leave the pair as it was.
    /// Nothing happens while a pair is being opened, changed or switched, or with no pair shown.
    /// </summary>
    /// <param name="side">The image to change.</param>
    /// <param name="from">What asked for the change, in English, for the diagnostic log: <c>the Change A button</c>.</param>
    internal async Task ChangeImageAsync(ImageSide side, string from)
    {
        if (_opening || _pair is null || _selection is null)
        {
            Log.Debug("change of " + side + " asked by " + from + (_opening ? " while a pair is being opened" : " with no pair shown") + ": ignored");
            return;
        }

        Log.Info("change of " + side + " asked by " + from);
        _opening = true;
        try
        {
            // The outgoing pair must be unreferenced when ShowPair releases it: a local holding
            // it here would be hoisted into this method's state machine, live across the await
            // and through ShowPair, and the collection would free nothing (measured on
            // 2026-10-30: 552.9 MiB before the release, 552.6 after, with a 92 MiB bitmap
            // outgoing). So only the canvas's size is kept, and the pair is read when the load
            // starts (ReplaceShownAsync).
            (int width, int height) canvas = (_pair.PixelWidth, _pair.PixelHeight);
            ImagePairSelection selection = _selection;
            string replaced = side == ImageSide.A ? selection.BeforePath : selection.AfterPath;
            string? path = _dialogs.PickImage(side, Path.GetDirectoryName(replaced));
            if (path is null)
            {
                Log.Info("the dialog was cancelled; the pair on screen stays");
                return;
            }

            Log.Info("the dialog chose " + side + "=" + path);

            // Open…'s first dialog follows the image A chosen last, a change of A included (R-001).
            if (side == ImageSide.A)
            {
                _lastDirectory = Path.GetDirectoryName(path);
            }

            ImagePairLoadResult result = await LoadWithOverlayAsync(progress => ReplaceShownAsync(side, path, progress));

            if (result.IsSuccess)
            {
                ImagePair pair = result.Pair!;
                _selection = side == ImageSide.A
                    ? selection with { BeforePath = path }
                    : selection with { AfterPath = path };

                // The place being looked at is kept when the canvas is the same; offsets into a
                // canvas of another size would mean nothing.
                ShowPair(pair, keepView: pair.PixelWidth == canvas.width && pair.PixelHeight == canvas.height);
            }
            else if (!result.WasDeclined)
            {
                // The load logged the failure with its exception and the message.
                Log.Info("the change failed: the message is shown, the pair on screen stays");
                MessageDialog.Show(this, UiText.Current.CannotCompareTitle, result.Error!);
            }
            else
            {
                Log.Info("the padding was declined: the pair on screen stays");
            }
        }
        finally
        {
            _opening = false;
        }
    }

    /// <summary>
    /// Starts the change of one image of the pair on screen (<see cref="ImagePairLoader.ReplaceAsync"/>),
    /// reading the pair here, in a method of its own, so that no frame of
    /// <see cref="ChangeImageAsync"/> holds the outgoing pair when it is released.
    /// </summary>
    /// <param name="side">The image to change.</param>
    /// <param name="path">The new file.</param>
    /// <param name="progress">The overlay's progress.</param>
    /// <returns>The loader's result.</returns>
    private Task<ImagePairLoadResult> ReplaceShownAsync(ImageSide side, string path, IProgress<ImageLoadProgress> progress)
    {
        ImagePair shown = _pair ?? throw new InvalidOperationException("No pair is shown.");
        return _loader.ReplaceAsync(shown, side, path, progress, ConfirmPaddingAsync, _mode);
    }

    /// <summary>
    /// Runs a load behind the progress overlay, which shows the file being decoded and comes
    /// down again whatever happens.
    /// </summary>
    /// <param name="load">
    /// The load, given the overlay's progress: a pair (<see cref="ImagePairLoader.LoadAsync"/>)
    /// or one image of it (<see cref="ImagePairLoader.ReplaceAsync"/>).
    /// </param>
    /// <returns>The loader's result: the pair, a failure with its message, or declined.</returns>
    private async Task<ImagePairLoadResult> LoadWithOverlayAsync(
        Func<IProgress<ImageLoadProgress>, Task<ImagePairLoadResult>> load)
    {
        LoadingOverlay.Visibility = Visibility.Visible;
        LoadingBar.Value = 0;
        LoadingText.Text = UiText.Current.Opening;

        var progress = new Progress<ImageLoadProgress>(p =>
        {
            LoadingBar.Value = p.Fraction;
            LoadingText.Text = p.FileName is null ? UiText.Current.AlmostThere : UiText.Current.Decoding(p.FileName);
        });

        try
        {
            return await load(progress);
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
    /// <param name="request">
    /// Both files with their sizes, the canvas, and the question worded.
    /// </param>
    /// <returns>True when the user agrees to the padding.</returns>
    private Task<bool> ConfirmPaddingAsync(PadRequest request) => Dispatcher.InvokeAsync(() =>
    {
        // The overlay is up at this point and a dialog sitting on top of a progress bar that
        // is not moving reads as a hang.
        Visibility overlay = LoadingOverlay.Visibility;
        LoadingOverlay.Visibility = Visibility.Collapsed;

        try
        {
            return MessageDialog.Confirm(this, UiText.Current.DifferentSizesTitle, request.Message);
        }
        finally
        {
            LoadingOverlay.Visibility = overlay;
        }
    }).Task;

    /// <summary>
    /// Puts a loaded pair on screen in place of the one shown, whose memory is given back first,
    /// and fills the title, the toolbar and the status bar.
    /// </summary>
    /// <param name="pair">The pair just loaded.</param>
    /// <param name="keepView">
    /// True for the same files in another display mode (R-113), and for one image changed on a
    /// canvas of the same size (R-065): the zoom, the pan and the divider stay; false for a new
    /// pair, shown zoomed to fit with the divider in the middle.
    /// </param>
    private void ShowPair(ImagePair pair, bool keepView = false)
    {
        bool replacing = _pair is not null;

        _pair = pair;
        _mode = pair.Mode;
        SyncModeButtons();

        if (keepView)
        {
            Compare.ReplacePair(pair);
        }
        else
        {
            Compare.SetPair(pair);
        }

        Compare.Focus();
        Log.Info("shown A=" + pair.Before.Info.FileName + " B=" + pair.After.Info.FileName
            + " mode=" + pair.Mode + " view=" + (keepView ? "kept" : "fit"));

        // What the process held before the outgoing pair was released, for the memory line below;
        // read only when that line is written.
        long beforeRelease = 0;
        if (replacing)
        {
            // The incoming pair was decoded while the outgoing one stayed on screen, so both are
            // resident up to here. The surface's images keep the outgoing bitmaps in their
            // render content until the next render pass, which comes after this method: a
            // collection before it finds them referenced and frees nothing, and the process
            // then carries two pairs until the next open (measured in the application on the
            // real pair, 2026-10-26, ADR 0040). So one render pass is let through here, which
            // also puts the incoming pair on screen, and only then is the outgoing one released.
            Dispatcher.Invoke(static () => { }, DispatcherPriority.Render);
            if (Log.IsDebugEnabled)
            {
                beforeRelease = MemoryHousekeeping.Snapshot().PrivateBytes;
            }

            MemoryHousekeeping.ReleaseImageBuffers();
        }

        Title = $"{pair.Before.Info.FileName}  vs  {pair.After.Info.FileName}  —  {VersionInfo.Banner}";
        BeforeName.Text = pair.Before.Info.FileName;
        AfterName.Text = pair.After.Info.FileName;
        ChangeBeforeButton.IsEnabled = true;
        ChangeAfterButton.IsEnabled = true;
        // When the pair had to be padded, the canvas size alone would be a half-truth: neither
        // image is that size. Both originals are named instead.
        string dimensions = pair.Pad.RequiresPadding
            ? $"{UiText.Current.PaddedCanvas(pair.Pad.Dimensions)}  ·  A {pair.Before.Info.Dimensions}  ·  B {pair.After.Info.Dimensions}"
            : pair.Before.Info.Dimensions;

        // The mode is named only when it is not the linear one (R-114).
        string modeSuffix = UiText.Current.ModeName(_mode) is { } name ? $"  ·  {name}" : string.Empty;
        StatusPair.Text =
            $"{dimensions}  ·  A {pair.Before.Info.FormatDescription}  ·  B {pair.After.Info.FormatDescription}{modeSuffix}";

        UpdateZoomReadout();
        MemorySnapshot memory = MemoryHousekeeping.Snapshot();
        StatusMemory.Text = memory.Describe();
        if (Log.IsDebugEnabled)
        {
            Log.Debug("memory: managed " + LogText.MiB(memory.ManagedBytes) + ", process " + LogText.MiB(memory.PrivateBytes)
                + (replacing ? "; the process held " + LogText.MiB(beforeRelease) + " before the outgoing pair was released" : string.Empty));
        }

        _memoryTimer.Start();
    }

    /// <summary>Shows the current zoom in the toolbar, as a whole percentage.</summary>
    private void UpdateZoomReadout() => ZoomLabel.Text = $"{Compare.Viewport.Scale * 100:F0}%";

    /// <summary>
    /// Shows the pixel under the cursor from both images at once. On a pair of finished astro
    /// frames the interesting differences are often below what the eye picks out of a dark
    /// field, and the numbers settle it.
    /// </summary>
    /// <param name="point">
    /// The image point under the pointer, in image pixels; null when the pointer is off the
    /// image, which clears the readout.
    /// </param>
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
        StatusBefore.Text = $"A {Describe(_pair.Before.Bitmap, x, y, _mode)}";
        StatusAfter.Text = $"B {Describe(_pair.After.Bitmap, x, y, _mode)}";
    }

    /// <summary>Reads one pixel of an image for the readout.</summary>
    /// <param name="bitmap">The image, Bgra32 as every loaded image is.</param>
    /// <param name="x">The pixel's column, in image pixels.</param>
    /// <param name="y">The pixel's row, in image pixels.</param>
    /// <param name="mode">The display mode the bitmap was rendered in.</param>
    /// <returns>
    /// Its red, green and blue, 0 to 255, each three wide; in <see cref="DisplayMode.FalseColours"/>
    /// the one level its colour stands for (R-114); a dash when the pixel is outside this image,
    /// in the padding of the smaller one.
    /// </returns>
    private static string Describe(BitmapSource bitmap, int x, int y, DisplayMode mode)
    {
        if (x < 0 || y < 0 || x >= bitmap.PixelWidth || y >= bitmap.PixelHeight)
        {
            return "—";
        }

        // Everything is Bgra32 by the time it gets here, so one pixel is four bytes in BGRA
        // order regardless of what the file on disk was.
        byte[] pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);

        // A false-colour pixel is the table's colour of one level; the level is what the user
        // reads, the colour's bytes say nothing. Every pixel of such a bitmap is in the table.
        if (mode == DisplayMode.FalseColours && RainbowTable.LevelOf(pixel[2], pixel[1], pixel[0]) is { } level)
        {
            return $"{level,3}";
        }

        return $"{pixel[2],3} {pixel[1],3} {pixel[0],3}";
    }
}
