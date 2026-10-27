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
// (ADR 0051), and "Calculeaza" runs it again at the gallery's current size.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

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

        /// <summary>The control points, and the positions of the five classes.</summary>
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
            foreach (CurvePoint p in c.Points)
            {
                bool labelled = p.Label != null;
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
                PointsNote = CurveTexts.PointsNote(method, colour),
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
    /// starts or by "Calculeaza", with the gallery of the original and the suggested curves,
    /// browsed with Next and Previous, each openable in the curve window and saveable as a new
    /// FITS.
    /// </summary>
    public sealed class CurvesTabViewModel : ResultTabViewModel
    {
        AnalysisResult _result;

        // The curves are computed once, for print; the set for the screen is the same without
        // the paper checks (CurveRun.ForScreen). Changing the purpose computes nothing more and
        // remakes no image: it chooses between the two sets of rows.
        CurveSet _print, _screen;
        IReadOnlyList<BitmapSource> _images = Array.Empty<BitmapSource>();
        IReadOnlyList<CurveGalleryItem> _items = Array.Empty<CurveGalleryItem>();
        int _index, _width = Thresholds.CurvePreviewDefaultWidth, _height = Thresholds.CurvePreviewDefaultHeight;

        // Each computation started has its number; one that ends after another image was opened,
        // or after another was started, touches nothing any more.
        int _run;
        bool _isPrint = true, _busy, _isProblem;
        string _message, _anchorSource, _saveMessage, _saveError;

        /// <summary>Makes the five commands; the tab starts empty and disabled.</summary>
        public CurvesTabViewModel()
        {
            Calculate = new RelayCommand(() => _ = ComputeAsync(), () => _result != null && !IsBusy);
            Next = new RelayCommand(() => Move(1), () => _index < _items.Count - 1);
            Previous = new RelayCommand(() => Move(-1), () => _index > 0);
            OpenCurve = new RelayCommand(() => CurveRequested?.Invoke(
                CurveWindowViewModel.From(Current.Suggestion, Path.GetFileName(_result.Path), _print.Method, _result.Raster.IsColor)), HasCurve);
            SaveAs = new RelayCommand(() => SaveRequested?.Invoke(CurveTexts.ProposedFileName(_result.Path, Current.Name)), HasCurve);
        }

        public ICommand Calculate { get; }
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
                // The purpose changes only the evidence: the same curves, images and position.
                if (HasGallery) ShowItems(_index);
            }
        }

        public bool IsScreen { get => !_isPrint; set => IsPrint = !value; }

        public bool IsBusy
        {
            get => _busy;
            private set { if (Set(ref _busy, value)) (Calculate as RelayCommand)?.RaiseCanExecuteChanged(); }
        }

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

        public string SaveMessage { get => _saveMessage; private set => Set(ref _saveMessage, value); }
        public string SaveError { get => _saveError; private set => Set(ref _saveError, value); }

        /// <summary>
        /// The image's folder, where the save dialog starts; null without a file on disk.
        /// </summary>
        public string InputFolder => _result?.FullPath == null || !File.Exists(_result.FullPath) ? null : Path.GetDirectoryName(_result.FullPath);

        /// <summary>
        /// The size of the gallery area, in pixels: the view sets it, "Calculeaza" uses it.
        /// </summary>
        /// <param name="width">
        /// The width in device pixels; 0 or less leaves the size as it was.
        /// </param>
        /// <param name="height">
        /// The height in device pixels; 0 or less leaves the size as it was.
        /// </param>
        internal void SetGallerySize(int width, int height)
        {
            if (width > 0 && height > 0) { _width = width; _height = height; }
        }

        /// <summary>
        /// Whether the current item is a suggestion, not the original: what OpenCurve and SaveAs
        /// need.
        /// </summary>
        /// <returns>True on a suggestion; false on the original or with no gallery.</returns>
        bool HasCurve() => Current?.Suggestion != null;

        /// <summary>
        /// Keeps the result and enables "Calculeaza"; unlike the other tabs, it fills nothing
        /// itself: MainViewModel starts the computation after every tab is filled (R-146, ADR
        /// 0051). Any computation in progress is abandoned.
        /// </summary>
        /// <param name="r">The analysis; not null.</param>
        internal override void Populate(AnalysisResult r)
        {
            Reset();
            _result = r;
            HasResult = true;
            (Calculate as RelayCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Empties the tab, drops the result, returns the purpose to print and disables
        /// "Calculeaza".
        /// </summary>
        internal override void Clear()
        {
            base.Clear();
            Reset();
            _result = null;
            if (Set(ref _isPrint, true, nameof(IsPrint))) Raise(nameof(IsScreen));
            (Calculate as RelayCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Empties the tab and abandons any computation in progress: its result is nobody's any
        /// more.
        /// </summary>
        void Reset()
        {
            _run++;
            _print = _screen = null;
            _images = Array.Empty<BitmapSource>();
            _index = 0;
            Items = Array.Empty<CurveGalleryItem>();
            Message = null; IsProblem = false; AnchorSource = null; SaveMessage = null; SaveError = null;
            IsBusy = false;
            RaiseGallery();
        }

        /// <summary>
        /// What "Calculeaza" runs, synchronously: the image's curves, at the given size of the
        /// gallery.
        /// </summary>
        /// <param name="width">
        /// The gallery's width in pixels; 0 or less keeps the current size.
        /// </param>
        /// <param name="height">
        /// The gallery's height in pixels; 0 or less keeps the current size.
        /// </param>
        internal void Compute(int width, int height)
        {
            SetGallerySize(width, height);
            _run++;
            Show(CurveRun.Execute(_result, CurvePurpose.Print, _width, _height));
        }

        /// <summary>
        /// "Calculeaza": runs the core on a pool thread at the gallery's size, with "se
        /// calculeaza" shown meanwhile; the outcome, the set or the error's message, is applied
        /// only if no Reset or newer computation came in between.
        /// </summary>
        internal async Task ComputeAsync()
        {
            AnalysisResult result = _result;
            int w = _width, h = _height, run = ++_run;
            IsBusy = true;
            IsProblem = false;
            Message = CurveTexts.Computing;
            try
            {
                CurveSet set = await Task.Run(() => CurveRun.Execute(result, CurvePurpose.Print, w, h));
                if (run == _run) Show(set);
            }
            catch (Exception e)
            {
                if (run == _run)
                {
                    Reset();
                    Message = MainViewModel.Describe(e);
                }
            }
            finally
            {
                if (run == _run) IsBusy = false;
            }
        }

        /// <summary>
        /// Takes a set computed for print: the images are made once, the rows for both purposes;
        /// a set with a problem empties the gallery and shows the problem in red.
        /// </summary>
        /// <param name="print">The core's set for print; not null.</param>
        void Show(CurveSet print)
        {
            SaveMessage = null; SaveError = null;
            AnchorSource = print.SourceSentence;
            _print = print;
            _screen = CurveRun.ForScreen(print);
            _index = 0;
            if (print.Problem != null)
            {
                _images = Array.Empty<BitmapSource>();
                Items = Array.Empty<CurveGalleryItem>();
                Message = print.Problem;
                IsProblem = true;
                RaiseGallery();
                return;
            }
            var images = new List<BitmapSource> { Bitmap(print.OriginalPreview) };
            foreach (CurveSuggestion s in print.Suggestions) images.Add(Bitmap(s.Preview));
            _images = images;
            // The previews' rasters are no longer needed: the bitmaps take their place. The
            // curves and the evidence stay, for the curve window and for saving.
            _print = Slim(_print);
            _screen = Slim(_screen);
            Message = null;
            ShowItems(0);
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
            CurveSet set = _isPrint ? _print : _screen;
            var items = new List<CurveGalleryItem> { Item(CurveTexts.Original, _images[0], null) };
            for (int i = 0; i < set.Suggestions.Count; i++) items.Add(Item(set.Suggestions[i].Name, _images[i + 1], set.Suggestions[i]));
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
        /// Marks the current item, raises the gallery properties and requeries the four commands.
        /// </summary>
        void RaiseGallery()
        {
            for (int i = 0; i < _items.Count; i++) _items[i].IsCurrent = i == _index;
            Raise(nameof(HasGallery)); Raise(nameof(Current)); Raise(nameof(Position));
            foreach (ICommand c in new[] { Next, Previous, OpenCurve, SaveAs }) (c as RelayCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Saves the curve shown into a new FITS (R-145); an error is told, not thrown.
        /// </summary>
        /// <param name="path">The file to write, as the save dialog gave it.</param>
        internal void Save(string path)
        {
            try
            {
                CurveExport.Save(_result, Current.Suggestion, path);
                SaveError = null;
                SaveMessage = CurveTexts.Saved(path);
            }
            catch (Exception e)
            {
                SaveMessage = null;
                SaveError = MainViewModel.Describe(e);
            }
        }
    }
}
