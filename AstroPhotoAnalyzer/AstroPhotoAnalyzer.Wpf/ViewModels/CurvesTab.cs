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

// CurvesTab.cs - the Editare Curbe tab (R-146) and the curve window (R-147): the gallery of
// previews of the suggested curves, the evidence of each, the table and the charts of one
// curve, the saving.
//
// It computes nothing: the curves, the previews, the figures and the sentences come from
// CurveRun; the saving is CurveExport's. Here are only the position in the gallery, the
// commands and the formatting of the points for the table. Unlike the other tabs, the content
// is a second run after the analysis: MainViewModel starts it once the other tabs are filled
// (ADR 0051), and the tab runs it again by itself when the gallery's area allows a finer
// preview than the one shown (ADR 0084).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using NLog;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>An image of the gallery: the original or a suggested curve.</summary>
    public sealed class CurveGalleryItem : ObservableObject
    {
        bool _isCurrent;

        /// <summary>
        /// It is the image shown. The evidence of every image sits stacked under the gallery and
        /// only the current one's is seen: the area always has the height of the longest, and
        /// the image does not change its size or place while browsing (R-146).
        /// </summary>
        public bool IsCurrent { get => _isCurrent; internal set => Set(ref _isCurrent, value); }

        public string Name { get; init; }
        public BitmapSource Image { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }

        /// <summary>The curve's evidence, line by line; empty for the original.</summary>
        public IReadOnlyList<string> Evidence { get; init; } = Array.Empty<string>();

        /// <summary>The same lines, with the core's warning mark.</summary>
        public IReadOnlyList<CurveEvidenceRow> EvidenceRows { get; init; } = Array.Empty<CurveEvidenceRow>();

        internal CurveSuggestion Suggestion { get; init; }
    }

    /// <summary>
    /// A line of a curve's evidence; the warnings (the paper checks) are shown in red.
    /// </summary>
    public sealed class CurveEvidenceRow
    {
        public string Text { get; init; }
        public bool IsWarning { get; init; }
    }

    /// <summary>A row of the table of points, as it is typed into Siril.</summary>
    public sealed class CurvePointRow
    {
        public string Label { get; init; }
        public string Input { get; init; }
        public string Output { get; init; }
        public string LstarBefore { get; init; }
        public string LstarAfter { get; init; }
    }

    /// <summary>Where a class sits on the input axis.</summary>
    public sealed class CurveMarker
    {
        public double X { get; init; }
        public string Label { get; init; }
    }

    /// <summary>
    /// The curve window's content (R-147): the table of control points, the two charts and the
    /// sentences around them, all formatted once by <see cref="From"/>.
    /// </summary>
    public sealed class CurveWindowViewModel
    {
        public string Title { get; init; }
        public string Caption { get; init; }
        public IReadOnlyList<CurvePointRow> Rows { get; init; }

        /// <summary>The curve sampled on 0..1, and the shadows alone, on 0..ShadowLimit.</summary>
        public IReadOnlyList<Point> Chart { get; init; }
        public IReadOnlyList<Point> ShadowChart { get; init; }
        public double ShadowLimit { get; init; }
        public string ShadowHeading { get; init; }

        /// <summary>What the table's columns mean, under it.</summary>
        public string PointsNote { get; init; }

        /// <summary>
        /// The control points, and the positions of the labelled ones: the five classes, and the
        /// rows a dust move adds.
        /// </summary>
        public IReadOnlyList<Point> ControlPoints { get; init; }
        public IReadOnlyList<CurveMarker> Markers { get; init; }

        /// <summary>
        /// Formats one suggestion for its window: a row per control point, with the L* before
        /// and after on the labelled ones, the two sampled charts and the core's sentences.
        /// </summary>
        /// <param name="s">The suggestion; its curve and evidence are read.</param>
        /// <param name="imageName">The image's file name, for the title.</param>
        /// <param name="method">
        /// The path the classes were made on, for the note under the table.
        /// </param>
        /// <param name="colour">Whether the image is colour, for the caption and the note.</param>
        /// <returns>The view-model.</returns>
        internal static CurveWindowViewModel From(CurveSuggestion s, string imageName, DustMethod method, bool colour)
        {
            ToneCurve c = s.Curve;
            CurveEvidence e = s.Evidence;
            var rows = new List<CurvePointRow>();
            int k = 0;
            // Of the two points a dust move adds, the helper is the one before the first class's
            // and the sky's dark end the one after it; a curve may have either, both or neither.
            bool helper = c.Points.TakeWhile(p => p.Label == null || p.Extra).Any(p => p.Extra);
            bool held = c.Points.SkipWhile(p => p.Label == null || p.Extra).Any(p => p.Extra);
            foreach (CurvePoint p in c.Points)
            {
                // The L* columns are the five classes': the two points a dust move adds have none.
                bool labelled = p.Label != null && !p.Extra;
                rows.Add(new CurvePointRow
                {
                    Label = p.Label ?? "",
                    Input = Fmt.F3(p.X),
                    Output = Fmt.F3(p.Y),
                    LstarBefore = labelled && e != null ? Fmt.F1(e.LstarBefore[k]) : "",
                    LstarAfter = labelled && e != null ? Fmt.F1(e.LstarAfter[k]) : "",
                });
                if (labelled) k++;
            }
            double shadow = CurveAdvisor.ShadowChartLimit(c);
            return new CurveWindowViewModel
            {
                Title = Tr.Pick($"Curve {s.Name} - {imageName}", $"Curba {s.Name} - {imageName}"),
                Caption = CurveTexts.CurveCaption(colour),
                PointsNote = CurveTexts.PointsNote(method, colour, held, helper),
                Rows = rows,
                Chart = c.Sample(1, Thresholds.CurveChartSamples).Select(p => new Point(p.X, p.Y)).ToArray(),
                ShadowChart = c.Sample(shadow, Thresholds.CurveChartSamples).Select(p => new Point(p.X, p.Y)).ToArray(),
                ShadowLimit = shadow,
                ShadowHeading = CurveTexts.ShadowHeading(shadow),
                ControlPoints = c.Points.Select(p => new Point(p.X, p.Y)).ToArray(),
                Markers = c.Points.Where(p => p.Label != null).Select(p => new CurveMarker { X = p.X, Label = p.Label }).ToArray(),
            };
        }
    }

    /// <summary>
    /// The Editare Curbe tab (R-146): filled after an analysis, by the computation MainViewModel
    /// starts, recomputed when its area allows finer previews, with the gallery of the original and the suggested curves,
    /// browsed with Next and Previous, each openable in the curve window and saveable as a new
    /// FITS.
    /// </summary>
    public sealed class CurvesTabViewModel : ResultTabViewModel
    {
        /// <summary>
        /// The diagnostic log: a computation that failed or was superseded, a save that failed;
        /// the core's CurveRun writes each computation's start and outcome (R-171, ADR 0069).
        /// </summary>
        static readonly Logger Log = LogManager.GetLogger(Loggers.Curves);

        AnalysisResult _result;

        /// <summary>
        /// One computed gallery: the core's set for print, the same without the paper checks for
        /// the screen (CurveRun.ForScreen; null for the curves with the dust move, which the
        /// screen never shows), the bitmaps, the reduction step its previews were made with
        /// (CurveRun.PreviewStride of the area it was computed for) and whether the core held
        /// them under its cap.
        /// </summary>
        sealed class Gallery
        {
            internal CurveSet Print, Screen;
            internal IReadOnlyList<BitmapSource> Images;
            internal int Stride;

            // Its previews were held under the core's cap: the view may enlarge them (ADR 0084).
            internal bool Capped;
        }

        // Two galleries (R-185): the curves with the dust move and the plain ones, each computed
        // for print the first time it is wanted and kept. Changing the purpose or the box chooses
        // between them and between the two sets of rows; it computes only a gallery not yet made.
        Gallery _moved, _plain, _shown;

        // The owner's choice for the box; it survives another image. Whether the move can be had
        // on this image is known once a set was computed (_moveKnown), with the core's reason when
        // it cannot (_dustRefusal).
        bool _dustChoice = true, _moveKnown;
        string _dustRefusal;

        // A refusal the core gives only when the move is asked for (no curve is left with it):
        // kept for the image, since a computation that does not ask would not repeat it.
        string _askedRefusal;

        // Which gallery the computation running was asked for.
        bool _runMoved;

        // The suggestion Save as... was pressed on, until Save writes it; null without a dialog.
        CurveSuggestion _saving;
        IReadOnlyList<CurveGalleryItem> _items = Array.Empty<CurveGalleryItem>();
        int _index, _width = Thresholds.CurvePreviewDefaultWidth, _height = Thresholds.CurvePreviewDefaultHeight;

        // The size the last computation was asked for; 0 before any.
        int _askedWidth, _askedHeight;

        // Each computation started has its number; one that ends after another image was opened,
        // or after another was started, touches nothing any more.
        int _run;
        bool _isPrint = true, _busy, _isProblem, _previewCapped;
        string _message, _anchorSource, _saveMessage, _saveError;

        /// <summary>Makes the four commands; the tab starts empty and disabled.</summary>
        public CurvesTabViewModel()
        {
            Next = new RelayCommand(() => Move(1), () => _index < _items.Count - 1);
            Previous = new RelayCommand(() => Move(-1), () => _index > 0);
            OpenCurve = new RelayCommand(() => CurveRequested?.Invoke(
                CurveWindowViewModel.From(Current.Suggestion, Path.GetFileName(_result.Path), _shown.Print.Method, _result.Raster.IsColor)), HasCurve);
            SaveAs = new RelayCommand(() =>
            {
                // The curve on screen now is the one the dialog is opened for: another gallery may
                // land under the dialog (a finer preview, the box, the purpose) before it returns.
                _saving = Current.Suggestion;
                SaveRequested?.Invoke(CurveTexts.ProposedFileName(_result.Path, Current.Name));
            }, HasCurve);
        }

        public ICommand Next { get; }
        public ICommand Previous { get; }
        public ICommand OpenCurve { get; }
        public ICommand SaveAs { get; }

        /// <summary>The window opens the curve window; the view-model only asks for it.</summary>
        public event Action<CurveWindowViewModel> CurveRequested;

        /// <summary>
        /// The window asks where to save, with the proposed name, then calls Save.
        /// </summary>
        public event Action<string> SaveRequested;

        public bool IsPrint
        {
            get => _isPrint;
            set
            {
                if (!Set(ref _isPrint, value)) return;
                Raise(nameof(IsScreen));
                RaiseDust();
                // The purpose changes the evidence, and with the box ticked the gallery too: the
                // screen has the plain curves (R-185). The position is kept.
                if (_moveKnown) ShowWanted();
            }
        }

        public bool IsScreen { get => !_isPrint; set => IsPrint = !value; }

        /// <summary>
        /// The box "Put dust on paper black" (R-185) as the view shows it: ticked only when the
        /// owner's choice is on and the move can be had, so a box that would do nothing is never
        /// shown ticked. Setting it sets the choice, which another image does not change.
        /// </summary>
        public bool DustOnPaperBlack
        {
            get => _dustChoice && DustBoxEnabled;
            set
            {
                _dustChoice = value;
                RaiseDust();
                if (_moveKnown) ShowWanted();
            }
        }

        /// <summary>Whether the box can be changed: for print, on an image the move can be had on.</summary>
        public bool DustBoxEnabled => _isPrint && _moveKnown && _dustRefusal == null;

        /// <summary>
        /// Why the box is off, beside it: the screen has no paper black, or the core's reason for
        /// this image; null when it is enabled or nothing is computed yet.
        /// </summary>
        public string DustBoxReason => !_moveKnown ? null : !_isPrint ? CurveTexts.DustMoveOnlyForPrint : _dustRefusal;

        /// <summary>
        /// The sentence under the evidence, the core's: what the curves shown move and what they
        /// hold; null without a gallery. While the screen's gallery is still being computed, the
        /// one on screen is the print's with the dust move, and the sentence is that one's.
        /// </summary>
        public string Footnote => _shown == null ? null : (_isPrint || _shown.Screen == null ? _shown.Print : _shown.Screen).Footnote;

        /// <summary>Whether the gallery wanted now is the one with the dust move.</summary>
        bool WantMoved => _isPrint && _dustChoice && _moveKnown && _dustRefusal == null;

        /// <summary>
        /// Whether the next computation asks the core for the dust move: as <see cref="WantMoved"/>,
        /// and also before anything is known about the image, when the core answers whether it can.
        /// </summary>
        bool AskMoved => _isPrint && _dustChoice && (!_moveKnown || _dustRefusal == null);

        /// <summary>Raises the box's three properties and the footnote.</summary>
        void RaiseDust()
        {
            Raise(nameof(DustOnPaperBlack)); Raise(nameof(DustBoxEnabled)); Raise(nameof(DustBoxReason)); Raise(nameof(Footnote));
        }

        public bool IsBusy { get => _busy; private set => Set(ref _busy, value); }

        /// <summary>
        /// The last computation started, by MainViewModel, by the tests or by a larger area, for
        /// whoever must wait; completed before the first.
        /// </summary>
        internal Task Computation { get; private set; } = Task.CompletedTask;

        public IReadOnlyList<CurveGalleryItem> Items { get => _items; private set => Set(ref _items, value); }
        public bool HasGallery => _items.Count > 0;
        public CurveGalleryItem Current => _items.Count == 0 ? null : _items[_index];
        public string Position => _items.Count == 0 ? "" : $"{_index + 1} / {_items.Count}";

        /// <summary>
        /// Why nothing was suggested, or the computation's error; null when the gallery exists.
        /// </summary>
        public string Message { get => _message; private set => Set(ref _message, value); }

        /// <summary>
        /// The message is a reason of the core's for which there are no curves (R-142, R-146):
        /// the window shows it in red. False for "se calculeaza" and for an error.
        /// </summary>
        public bool IsProblem { get => _isProblem; private set => Set(ref _isProblem, value); }

        /// <summary>Where the anchors come from (R-141).</summary>
        public string AnchorSource { get => _anchorSource; private set => Set(ref _anchorSource, value); }

        /// <summary>
        /// The previews are held under the core's cap and smaller than the area: the view may
        /// enlarge them, the one case it does (ADR 0084).
        /// </summary>
        public bool PreviewCapped { get => _previewCapped; private set => Set(ref _previewCapped, value); }

        public string SaveMessage { get => _saveMessage; private set => Set(ref _saveMessage, value); }
        public string SaveError { get => _saveError; private set => Set(ref _saveError, value); }

        /// <summary>
        /// The image's folder, where the save dialog starts; null without a file on disk.
        /// </summary>
        public string InputFolder => _result?.FullPath == null || !File.Exists(_result.FullPath) ? null : Path.GetDirectoryName(_result.FullPath);

        /// <summary>
        /// The size of the gallery area, in pixels: the view sets it at every layout pass. When
        /// it allows a finer preview than the gallery shown was made with, or, while a
        /// computation runs, than that one was asked for, the gallery is computed again by itself
        /// (ADR 0084); a size that allows no finer one starts nothing, so the layout passes of a
        /// steady window cost nothing.
        /// </summary>
        /// <param name="width">
        /// The width in device pixels; 0 or less leaves the size as it was.
        /// </param>
        /// <param name="height">
        /// The height in device pixels; 0 or less leaves the size as it was.
        /// </param>
        internal void SetGallerySize(int width, int height)
        {
            if (width <= 0 || height <= 0) return;
            _width = width; _height = height;
            if (_result == null || _askedWidth == 0) return;
            // Finer than what: the computation running, or else the gallery on screen, which may
            // have been kept from a smaller area while the other one was refined (R-185).
            int had = IsBusy || _shown == null ? CurveRun.PreviewStride(_result.Raster, _askedWidth, _askedHeight) : _shown.Stride;
            if (CurveRun.PreviewStride(_result.Raster, width, height) < had)
            {
                Log.Debug("the gallery area " + LogText.Int(width) + "x" + LogText.Int(height) + " allows a finer preview than " + LogText.Int(_askedWidth) + "x" + LogText.Int(_askedHeight) + ": computing again");
                ComputeAsync();
            }
        }

        /// <summary>
        /// Whether the current item is a suggestion, not the original: what OpenCurve and SaveAs
        /// need.
        /// </summary>
        /// <returns>True on a suggestion; false on the original or with no gallery.</returns>
        bool HasCurve() => Current?.Suggestion != null;

        /// <summary>
        /// Keeps the result; unlike the other tabs, it fills nothing itself: MainViewModel starts
        /// the computation after every tab is filled (R-146, ADR 0051). Any computation in
        /// progress is abandoned.
        /// </summary>
        /// <param name="r">The analysis; not null.</param>
        internal override void Populate(AnalysisResult r)
        {
            Reset();
            _result = r;
            HasResult = true;
        }

        /// <summary>Empties the tab, drops the result and returns the purpose to print.</summary>
        internal override void Clear()
        {
            base.Clear();
            Reset();
            _result = null;
            if (Set(ref _isPrint, true, nameof(IsPrint))) Raise(nameof(IsScreen));
        }

        /// <summary>
        /// Empties the tab and abandons any computation in progress: its result is nobody's any
        /// more.
        /// </summary>
        void Reset()
        {
            _run++;
            _askedWidth = _askedHeight = 0;
            _moved = _plain = _shown = null;
            // What the box may offer is the image's; the owner's choice is not, and stays.
            _moveKnown = false; _dustRefusal = null; _askedRefusal = null;
            _saving = null;
            _index = 0;
            Items = Array.Empty<CurveGalleryItem>();
            Message = null; IsProblem = false; AnchorSource = null; SaveMessage = null; SaveError = null; PreviewCapped = false;
            IsBusy = false;
            RaiseGallery();
            RaiseDust();
        }

        /// <summary>
        /// The computation, synchronously (the tests): the image's curves, at the given size of
        /// the gallery.
        /// </summary>
        /// <param name="width">
        /// The gallery's width in pixels; 0 or less keeps the current size.
        /// </param>
        /// <param name="height">
        /// The gallery's height in pixels; 0 or less keeps the current size.
        /// </param>
        internal void Compute(int width, int height)
        {
            if (width > 0 && height > 0) { _width = width; _height = height; }
            _run++;
            _askedWidth = _width; _askedHeight = _height;
            bool moved = _runMoved = AskMoved;
            Show(CurveRun.Execute(_result, CurvePurpose.Print, _width, _height, moved), _width, _height, moved);
        }

        /// <summary>
        /// Runs the core on a pool thread at the gallery's size, with "se calculeaza" shown
        /// meanwhile (the gallery already shown stays); the outcome, the set or the error's
        /// message, is applied only if no Reset or newer computation came in between.
        /// </summary>
        internal Task ComputeAsync() => Computation = ComputeCore();

        /// <summary>The body of <see cref="ComputeAsync"/>.</summary>
        async Task ComputeCore()
        {
            AnalysisResult result = _result;
            int w = _width, h = _height, run = ++_run;
            bool moved = _runMoved = AskMoved;
            _askedWidth = w; _askedHeight = h;
            IsBusy = true;
            IsProblem = false;
            Message = CurveTexts.Computing;
            try
            {
                CurveSet set = await Task.Run(() => CurveRun.Execute(result, CurvePurpose.Print, w, h, moved));
                if (run == _run) Show(set, w, h, moved);
                else Log.Debug("computation " + LogText.Int(run) + " ended after a newer one started; nothing is shown");
            }
            catch (Exception e)
            {
                if (run == _run)
                {
                    Log.Error(e, "computation " + LogText.Int(run) + " failed; the tab shows the message");
                    Reset();
                    Message = MainViewModel.Describe(e);
                }
                else Log.Debug(e, "computation " + LogText.Int(run) + " failed after a newer one started; nothing is shown");
            }
            finally
            {
                if (run == _run) IsBusy = false;
            }
        }

        /// <summary>
        /// Takes a set computed for print: the images are made once, the rows for both purposes,
        /// and the set becomes the gallery with the dust move or the plain one, by what the core
        /// applied; then the gallery wanted now is shown, which starts the other's computation
        /// when it is that one and not made yet. A set with a problem empties the tab and shows
        /// the problem in red.
        /// </summary>
        /// <param name="print">The core's set for print; not null.</param>
        /// <param name="width">The area's width the set was computed for, in pixels.</param>
        /// <param name="height">The area's height the set was computed for, in pixels.</param>
        /// <param name="asked">Whether the computation asked the core for the dust move.</param>
        void Show(CurveSet print, int width, int height, bool asked)
        {
            SaveMessage = null; SaveError = null;
            AnchorSource = print.SourceSentence;
            if (print.Problem != null)
            {
                PreviewCapped = false;
                // No gallery to refine: a larger area must not run this again (the reason does
                // not depend on the size), so the asked size goes back to "nothing to redo".
                _askedWidth = _askedHeight = 0;
                _moved = _plain = _shown = null;
                _moveKnown = false; _dustRefusal = null; _askedRefusal = null;
                _index = 0;
                Items = Array.Empty<CurveGalleryItem>();
                Message = print.Problem;
                IsProblem = true;
                RaiseGallery();
                RaiseDust();
                return;
            }
            var images = new List<BitmapSource> { Bitmap(print.OriginalPreview) };
            foreach (CurveSuggestion s in print.Suggestions) images.Add(Bitmap(s.Preview));
            // The previews' rasters are no longer needed: the bitmaps take their place. The
            // curves and the evidence stay, for the curve window and for saving.
            var gallery = new Gallery
            {
                Print = Slim(print),
                Screen = print.DustOnPaperBlack ? null : Slim(CurveRun.ForScreen(print)),
                Images = images,
                Stride = CurveRun.PreviewStride(_result.Raster, width, height),
                Capped = print.PreviewCapped,
            };
            if (print.DustOnPaperBlack) _moved = gallery; else _plain = gallery;
            _moveKnown = true;
            // Asked for and refused: the reason stays for this image, also when a later
            // computation, which no longer asks, comes back without it.
            if (asked && !print.DustOnPaperBlack && print.DustMoveRefusal != null) _askedRefusal = print.DustMoveRefusal;
            _dustRefusal = print.DustMoveRefusal ?? _askedRefusal;
            Message = null;
            RaiseDust();
            ShowWanted();
        }

        /// <summary>
        /// Shows the gallery the purpose and the box ask for, at the position kept; when that
        /// gallery is not made yet, or the area allows finer previews than it was made with,
        /// starts its computation and leaves the one on screen until it is there (R-185). The
        /// area's size itself does not count: it changes with the evidence under the image, and
        /// only a finer reduction step makes a better gallery, as in SetGallerySize.
        /// </summary>
        void ShowWanted()
        {
            Gallery wanted = WantMoved ? _moved : _plain;
            if (wanted != null)
            {
                // Made with a coarser step, it is still the right gallery to look at meanwhile.
                _shown = wanted;
                PreviewCapped = wanted.Capped;
                ShowItems(_index);
                Raise(nameof(Footnote));
            }
            if (wanted != null && wanted.Stride <= CurveRun.PreviewStride(_result.Raster, _width, _height)) return;
            // The computation running may already be this gallery's, at this size.
            if (!(IsBusy && _runMoved == WantMoved)) ComputeAsync();
        }

        /// <summary>A preview raster as a frozen bitmap, pixel for pixel.</summary>
        /// <param name="image">The preview the core made, already at the gallery's size.</param>
        /// <returns>The bitmap.</returns>
        static BitmapSource Bitmap(Raster image) => RasterBitmaps.Region(image, 0, 0, image.W, image.H);

        /// <summary>
        /// A copy of a set without its preview rasters, which the bitmaps replace.
        /// </summary>
        /// <param name="set">The set; not null.</param>
        /// <returns>The copy, with the curves and the evidence kept.</returns>
        static CurveSet Slim(CurveSet set) => set with
        {
            OriginalPreview = null,
            Suggestions = set.Suggestions.Select(s => s with { Preview = null }).ToArray(),
        };

        /// <summary>The gallery for the chosen purpose, from the images already made.</summary>
        /// <param name="index">The item to show; clamped into the gallery.</param>
        void ShowItems(int index)
        {
            CurveSet set = _isPrint ? _shown.Print : _shown.Screen;
            IReadOnlyList<BitmapSource> images = _shown.Images;
            var items = new List<CurveGalleryItem> { Item(CurveTexts.Original, images[0], null) };
            for (int i = 0; i < set.Suggestions.Count; i++) items.Add(Item(set.Suggestions[i].Name, images[i + 1], set.Suggestions[i]));
            _index = Math.Clamp(index, 0, items.Count - 1);
            Items = items;
            RaiseGallery();
        }

        /// <summary>One gallery item, with its evidence lines formatted by the core.</summary>
        /// <param name="name">The item's name, as the gallery shows it.</param>
        /// <param name="image">Its bitmap.</param>
        /// <param name="s">The suggestion; null for the original, which has no evidence.</param>
        /// <returns>The item, not yet current.</returns>
        static CurveGalleryItem Item(string name, BitmapSource image, CurveSuggestion s) => new()
        {
            Name = name,
            Image = image,
            Width = image.PixelWidth,
            Height = image.PixelHeight,
            Evidence = s == null ? Array.Empty<string>() : CurveTexts.EvidenceLines(s),
            EvidenceRows = s == null ? Array.Empty<CurveEvidenceRow>()
                : CurveTexts.EvidenceRows(s).Select(r => new CurveEvidenceRow { Text = r.Text, IsWarning = r.Warning }).ToArray(),
            Suggestion = s,
        };

        /// <summary>Moves the position in the gallery, stopping at its ends.</summary>
        /// <param name="step">+1 for Next, -1 for Previous.</param>
        void Move(int step)
        {
            _index = Math.Clamp(_index + step, 0, Math.Max(0, _items.Count - 1));
            RaiseGallery();
        }

        /// <summary>
        /// Marks the current item, raises the gallery properties and requeries the commands.
        /// </summary>
        void RaiseGallery()
        {
            for (int i = 0; i < _items.Count; i++) _items[i].IsCurrent = i == _index;
            Raise(nameof(HasGallery)); Raise(nameof(Current)); Raise(nameof(Position));
            foreach (ICommand c in new[] { Next, Previous, OpenCurve, SaveAs }) (c as RelayCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Saves into a new FITS (R-145) the curve Save as... was pressed on, whatever gallery
        /// is on screen by now; without a dialog before it, the curve shown. An error is told,
        /// not thrown.
        /// </summary>
        /// <param name="path">The file to write, as the save dialog gave it.</param>
        internal void Save(string path)
        {
            CurveSuggestion suggestion = _saving ?? Current?.Suggestion;
            _saving = null;
            try
            {
                CurveExport.Save(_result, suggestion, path);
                SaveError = null;
                SaveMessage = CurveTexts.Saved(path);
            }
            catch (Exception e)
            {
                Log.Warn(e, "the curve could not be saved to " + path + "; the tab shows the message");
                SaveMessage = null;
                SaveError = MainViewModel.Describe(e);
            }
        }
    }
}
