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

// CropTab.cs - the Crop with aspect ratio tab (R-183, ADR 0093): the frame's two sizes, the
// image with the rectangle of those proportions over it, moved and resized with the mouse, and
// the save of what the rectangle keeps as a new file of the input's kind.
//
// It computes nothing: the rectangle, each of its moves and its figures are FrameCrop's, the
// sentences CropTexts', the save CropExport's. Here are only the two fields, the rectangle as
// the core last gave it, the reduced copy as a bitmap and the commands. The fields are the one
// thing kept from an image to the next: the frame does not change with the image.

using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using NLog;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// The Crop with aspect ratio tab (R-183): the frame's width and height as typed, the crop
    /// rectangle the core makes of them over the analysed image, its figures, and the save of
    /// the crop as a new file.
    /// </summary>
    public sealed class CropTabViewModel : ResultTabViewModel
    {
        /// <summary>
        /// The diagnostic log: the frame and the rectangle it gives, where a drag left it, a
        /// save that failed; the core's CropExport writes each file saved (R-171, ADR 0069).
        /// </summary>
        static readonly Logger Log = LogManager.GetLogger(Loggers.Crop);

        AnalysisResult _result;
        CropOutput _output;
        string _frameWidth = "", _frameHeight = "";
        double _width, _height;
        CropRect? _rect;
        BitmapSource _image;
        int _areaWidth = Thresholds.CurvePreviewDefaultWidth, _areaHeight = Thresholds.CurvePreviewDefaultHeight;

        // The step of the reduced copy shown; 0 before any.
        int _stride;

        // Where the rectangle was left on the image last shown, with what made it (the file,
        // the image's size, the frame): a new analysis of that same image, which a change of a
        // parameter or of the profile starts by itself, must not undo the placing.
        (string File, int W, int H, double FrameW, double FrameH, CropRect Rect)? _placed;

        /// <summary>
        /// What writes the file and words the outcome: the core's save. A field so that a test
        /// can hold a save in progress for as long as it looks at the tab.
        /// </summary>
        internal Func<AnalysisResult, CropRect, double, double, string, (string Saved, string Error)> Writer = Write;
        bool _busy, _isProblem;
        string _message, _keptText, _frameText, _saveMessage, _saveError;

        /// <summary>Makes the two commands; the tab starts empty, asking for the frame's sizes.</summary>
        public CropTabViewModel()
        {
            Largest = new RelayCommand(Reframe, () => _rect != null);
            SaveAs = new RelayCommand(() => SaveRequested?.Invoke(CropTexts.ProposedFileName(_result.Path, _output.Extension, _width, _height)), CanSave);
            _message = CropTexts.AskSizes;
        }

        public ICommand Largest { get; }
        public ICommand SaveAs { get; }

        /// <summary>The window asks where to save, with the proposed name, then calls SaveAsync.</summary>
        public event Action<string> SaveRequested;

        /// <summary>The frame's width as typed, in cm; kept when another image is opened.</summary>
        public string FrameWidth
        {
            get => _frameWidth;
            set { if (Set(ref _frameWidth, value ?? "")) Reframe(); }
        }

        /// <summary>The frame's height as typed, in cm; kept when another image is opened.</summary>
        public string FrameHeight
        {
            get => _frameHeight;
            set { if (Set(ref _frameHeight, value ?? "")) Reframe(); }
        }

        /// <summary>The analysed image as a reduced copy; null without an image.</summary>
        public BitmapSource Image { get => _image; private set => Set(ref _image, value); }

        /// <summary>The analysed image's width, in pixels: the unit of the rectangle; 0 without an image.</summary>
        public int ImageWidth => _result?.Raster.W ?? 0;

        /// <summary>The analysed image's height, in pixels; 0 without an image.</summary>
        public int ImageHeight => _result?.Raster.H ?? 0;

        /// <summary>Whether there is a rectangle: an image and two valid sizes.</summary>
        public bool HasCrop => _rect != null;

        public int CropX => _rect?.X ?? 0;
        public int CropY => _rect?.Y ?? 0;
        public int CropWidth => _rect?.W ?? 0;
        public int CropHeight => _rect?.H ?? 0;

        /// <summary>What the tab asks for, or which field is not a size; null with a rectangle.</summary>
        public string Message { get => _message; private set => Set(ref _message, value); }

        /// <summary>The message names a field that is not a number above 0: shown in red.</summary>
        public bool IsProblem { get => _isProblem; private set => Set(ref _isProblem, value); }

        /// <summary>What the rectangle keeps and cuts, the core's sentence; null without a rectangle.</summary>
        public string KeptText { get => _keptText; private set => Set(ref _keptText, value); }

        /// <summary>The pixels per inch on the frame, the core's sentence; null without a rectangle.</summary>
        public string FrameText { get => _frameText; private set => Set(ref _frameText, value); }

        /// <summary>What the save will write, or why this input cannot be cropped; null without an image.</summary>
        public string OutputText => _output == null ? null : CropTexts.Output(_output);

        /// <summary>The input cannot be cropped without loss: <see cref="OutputText"/> is the reason, shown in red.</summary>
        public bool OutputIsProblem => _output?.Problem != null;

        /// <summary>How the rectangle is moved and resized, the core's sentence.</summary>
        public string Help => CropTexts.Help;

        /// <summary>A save is being written.</summary>
        public bool IsBusy { get => _busy; private set => Set(ref _busy, value); }

        public string SaveMessage { get => _saveMessage; private set => Set(ref _saveMessage, value); }
        public string SaveError { get => _saveError; private set => Set(ref _saveError, value); }

        /// <summary>The image's folder, where the save dialog starts; null without a file on disk.</summary>
        public string InputFolder => _result?.FullPath == null || !File.Exists(_result.FullPath) ? null : Path.GetDirectoryName(_result.FullPath);

        /// <summary>The extension of the file a save writes, with its point: the input's, or the output's own.</summary>
        public string SaveExtension => _output?.Extension ?? (_result == null ? "" : Path.GetExtension(_result.Path));

        /// <summary>The save dialog's one filter: the kind of file a save writes ("TIFF|*.tif").</summary>
        public string SaveFilter
        {
            get
            {
                return _output == null ? "" : CropTexts.FormatName(_output) + "|*" + SaveExtension;
            }
        }

        /// <summary>The last save started, for whoever must wait; completed before the first.</summary>
        internal Task Saving { get; private set; } = Task.CompletedTask;

        /// <summary>Whether a save can start: a rectangle, an input the crop can write, no save in progress.</summary>
        /// <returns>True when it can.</returns>
        bool CanSave() => _rect != null && _output?.Problem == null && !_busy;

        /// <summary>
        /// Keeps the result, shows its reduced copy and, when the two fields already hold sizes,
        /// the rectangle: the largest one on another image, and the one already placed when the
        /// result is a new analysis of the image last shown, with the same frame.
        /// </summary>
        /// <param name="r">The analysis; not null.</param>
        internal override void Populate(AnalysisResult r)
        {
            Remember();
            _result = r;
            _output = CropExport.OutputOf(r.Raster.Source);
            _stride = 0;
            ForgetTheLastSave();
            ShowImage();
            HasResult = true;
            Raise(nameof(ImageWidth)); Raise(nameof(ImageHeight));
            Raise(nameof(OutputText)); Raise(nameof(OutputIsProblem));
            Reframe();
            if (_placed is { } p && _rect != null && r.FullPath != null && p.File == r.FullPath && p.W == ImageWidth && p.H == ImageHeight
                && p.FrameW == _width && p.FrameH == _height && p.Rect.FitsIn(ImageWidth, ImageHeight))
            {
                SetRect(p.Rect);
                Log.Debug("the same image analysed again: the rectangle stays at " + p.Rect);
            }
        }

        /// <summary>
        /// Notes where the rectangle is on the image shown, before the image goes: only for an
        /// image from a file, which is what a new analysis can be told to be the same by.
        /// </summary>
        void Remember()
        {
            if (_result?.FullPath != null && _rect is CropRect r) _placed = (_result.FullPath, ImageWidth, ImageHeight, _width, _height, r);
        }

        /// <summary>
        /// Drops the outcome of the last save with the image it was of; while a file is still
        /// being written its message stays, since the save button is off because of it.
        /// </summary>
        void ForgetTheLastSave()
        {
            if (_busy) return;
            SaveMessage = null;
            SaveError = null;
        }

        /// <summary>Empties the tab and drops the result; the two fields stay as typed.</summary>
        internal override void Clear()
        {
            base.Clear();
            Remember();
            _result = null;
            _output = null;
            _stride = 0;
            Image = null;
            ForgetTheLastSave();
            Raise(nameof(ImageWidth)); Raise(nameof(ImageHeight));
            Raise(nameof(OutputText)); Raise(nameof(OutputIsProblem));
            Reframe();
        }

        /// <summary>
        /// The size of the image area, in pixels: the view sets it at every layout pass. When it
        /// allows a finer reduced copy than the one shown, the copy is made again (the rule of
        /// the galleries, ADR 0084); the rectangle is in pixels of the full image and does not
        /// move.
        /// </summary>
        /// <param name="width">The width in device pixels; 0 or less leaves the size as it was.</param>
        /// <param name="height">The height in device pixels; 0 or less leaves the size as it was.</param>
        internal void SetArea(int width, int height)
        {
            if (width <= 0 || height <= 0) return;
            _areaWidth = width; _areaHeight = height;
            if (_result != null && CurveRun.PreviewStride(_result.Raster, width, height) < _stride) ShowImage();
        }

        /// <summary>Makes the bitmap of the reduced copy for the area as it is known.</summary>
        void ShowImage()
        {
            Raster copy = CropRun.Preview(_result, _areaWidth, _areaHeight);
            _stride = CurveRun.PreviewStride(_result.Raster, _areaWidth, _areaHeight);
            Image = RasterBitmaps.Region(copy, 0, 0, copy.W, copy.H);
            Log.Debug("reduced copy " + LogText.Int(copy.W) + "x" + LogText.Int(copy.H) + " step=" + LogText.Int(_stride) + " for the area " + LogText.Int(_areaWidth) + "x" + LogText.Int(_areaHeight));
        }

        /// <summary>
        /// Reads the two fields and puts the rectangle at the largest centred one, or shows why
        /// there is none: the sizes are asked for, or the field that is not a size is named.
        /// </summary>
        void Reframe()
        {
            double? w = FrameCrop.ParseSize(_frameWidth), h = FrameCrop.ParseSize(_frameHeight);
            bool badWidth = w == null && !string.IsNullOrWhiteSpace(_frameWidth), badHeight = h == null && !string.IsNullOrWhiteSpace(_frameHeight);
            if (w == null || h == null)
            {
                IsProblem = badWidth || badHeight;
                Message = badWidth ? CropTexts.BadSize(width: true, _frameWidth) : badHeight ? CropTexts.BadSize(width: false, _frameHeight) : CropTexts.AskSizes;
                SetRect(null);
                return;
            }
            _width = w.Value; _height = h.Value;
            IsProblem = false;
            Message = null;
            if (_result == null) { SetRect(null); return; }
            CropRect largest = FrameCrop.Largest(ImageWidth, ImageHeight, _width, _height);
            SetRect(largest);
            Log.Debug("frame=" + LogText.Num(_width) + "x" + LogText.Num(_height) + " cm largest rectangle=" + largest);
        }

        /// <summary>Takes the rectangle the core gave: its four numbers, its sentences, the commands.</summary>
        /// <param name="rect">The rectangle, or null for none.</param>
        void SetRect(CropRect? rect)
        {
            _rect = rect;
            if (rect is CropRect r)
            {
                FrameCropFacts facts = FrameCrop.Facts(r, ImageWidth, ImageHeight, _width);
                KeptText = CropTexts.Kept(facts);
                FrameText = CropTexts.AtFrame(facts, _width, _height);
            }
            else
            {
                KeptText = null;
                FrameText = null;
            }
            Raise(nameof(HasCrop)); Raise(nameof(CropX)); Raise(nameof(CropY)); Raise(nameof(CropWidth)); Raise(nameof(CropHeight));
            Requery();
        }

        /// <summary>Asks the two commands again whether they can run.</summary>
        void Requery()
        {
            foreach (ICommand c in new[] { Largest, SaveAs }) (c as RelayCommand)?.RaiseCanExecuteChanged();
        }

        // The rectangle a drag started from: a move is told as a distance from it.
        CropRect _dragStart;

        /// <summary>A drag starts: the moves that follow are counted from the rectangle as it is.</summary>
        internal void BeginDrag()
        {
            if (_rect is CropRect r) _dragStart = r;
        }

        /// <summary>The rectangle dragged by a distance from where its drag started; the core stops it at the edges.</summary>
        /// <param name="dx">The distance to the right, in pixels of the full image.</param>
        /// <param name="dy">The distance down, in pixels of the full image.</param>
        internal void MoveBy(int dx, int dy)
        {
            if (_rect is not CropRect now) return;
            // A move told without a drag started (no viewer does that) counts from the
            // rectangle as it is, never from a start that belongs to another rectangle's size.
            CropRect from = _dragStart.W == now.W && _dragStart.H == now.H ? _dragStart : now;
            SetRect(FrameCrop.Move(from, dx, dy, ImageWidth, ImageHeight));
        }

        /// <summary>A corner dragged to a point; the core keeps the opposite corner, the proportions and the limits.</summary>
        /// <param name="corner">The corner dragged.</param>
        /// <param name="x">Where it is pulled to, in pixels of the full image from its left edge.</param>
        /// <param name="y">Where it is pulled to, in pixels of the full image from its top edge.</param>
        internal void ResizeTo(CropCorner corner, double x, double y)
        {
            if (_rect is not CropRect r) return;
            SetRect(FrameCrop.Resize(r, corner, x, y, ImageWidth, ImageHeight, _width, _height));
        }

        /// <summary>A drag ended: where it left the rectangle goes to the log.</summary>
        internal void EndDrag()
        {
            if (_rect is CropRect r) Log.Debug("rectangle dragged to " + r);
        }

        /// <summary>
        /// Saves what the rectangle keeps into a new file (R-181), on the calling thread; an
        /// error is told, not thrown.
        /// </summary>
        /// <param name="path">The file to write, as the save dialog gave it.</param>
        internal void Save(string path)
        {
            if (!CanSave()) return;
            (string saved, string error) = Writer(_result, _rect.Value, _width, _height, path);
            SaveMessage = saved;
            SaveError = error;
        }

        /// <summary>
        /// Saves what the rectangle keeps into a new file (R-181) on a pool thread, a large PNG
        /// taking seconds, with "writing the file..." shown meanwhile and the save button off;
        /// an error is told, not thrown.
        /// </summary>
        /// <param name="path">The file to write, as the save dialog gave it.</param>
        /// <returns>The save, to wait for; the one in progress when there is one, which a second call does not replace.</returns>
        internal Task SaveAsync(string path) => _busy ? Saving : Saving = SaveCore(path);

        /// <summary>The body of <see cref="SaveAsync"/>.</summary>
        /// <param name="path">The file to write.</param>
        async Task SaveCore(string path)
        {
            if (!CanSave()) return;
            AnalysisResult result = _result;
            CropRect rect = _rect.Value;
            double w = _width, h = _height;
            IsBusy = true;
            Requery();
            SaveError = null;
            SaveMessage = CropTexts.Saving;
            Func<AnalysisResult, CropRect, double, double, string, (string Saved, string Error)> writer = Writer;
            (string saved, string error) = await Task.Run(() => writer(result, rect, w, h, path));
            // The outcome is told even when another image was opened meanwhile: the file is
            // written, or it is not, whatever the tab shows now.
            SaveMessage = saved;
            SaveError = error;
            IsBusy = false;
            Requery();
        }

        /// <summary>Writes the crop through the core and words the outcome.</summary>
        /// <param name="result">The analysis whose image is cropped.</param>
        /// <param name="rect">The rectangle to keep.</param>
        /// <param name="w">The frame's width, in cm.</param>
        /// <param name="h">The frame's height, in cm.</param>
        /// <param name="path">The file to write.</param>
        /// <returns>The message of a save that succeeded, or the error of one that did not; the other is null.</returns>
        static (string Saved, string Error) Write(AnalysisResult result, CropRect rect, double w, double h, string path)
        {
            try
            {
                CropExport.Save(result, rect, w, h, path);
                return (CurveTexts.Saved(path), null);
            }
            catch (Exception e)
            {
                Log.Warn(e, "the crop could not be saved to " + path + "; the tab shows the message");
                return (null, MainViewModel.Describe(e));
            }
        }
    }
}
