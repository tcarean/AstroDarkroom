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

// GamutFitTab.cs - the Culori pentru hartie tab (R-178): the gallery of the original and the
// three variants of the paper fit, the red layer of what the paper still cannot hold, the
// evidence of each, the saving as a new FITS or 16-bit TIFF.
//
// It computes nothing: the variants, the previews, the masks, the figures and the sentences
// come from GamutFitRun; the saving is GamutFitExport's. Here are only the position in the
// gallery, the box, the commands and the bitmaps. Like Editare Curbe, the content is a run
// after the analysis: MainViewModel starts it once the other tabs are filled (ADR 0051), and
// "Calculeaza" runs it again at the gallery's current size.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NLog;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>An image of the gallery: the original or a variant of the fit.</summary>
    public sealed class GamutFitGalleryItem : ObservableObject
    {
        bool _isCurrent, _showOutside;

        /// <summary>
        /// It is the image shown; the evidence of every image sits stacked under the gallery
        /// and only the current one's is seen (R-146's layout).
        /// </summary>
        public bool IsCurrent { get => _isCurrent; internal set => Set(ref _isCurrent, value); }

        public string Name { get; init; }
        public BitmapSource Image { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }

        /// <summary>
        /// The red layer over what the paper cannot hold, when the box is on and there is
        /// anything to paint; null otherwise.
        /// </summary>
        public BitmapSource Overlay => _showOutside ? OutsideLayer : null;

        /// <summary>The layer itself, made once; null when nothing is outside.</summary>
        internal BitmapSource OutsideLayer { get; init; }

        /// <summary>The box's state, pushed by the tab so the layer follows it.</summary>
        internal bool ShowOutside { get => _showOutside; set { if (Set(ref _showOutside, value)) Raise(nameof(Overlay)); } }

        /// <summary>The evidence lines, with the core's warning mark; empty for the original.</summary>
        public IReadOnlyList<CurveEvidenceRow> EvidenceRows { get; init; } = Array.Empty<CurveEvidenceRow>();

        internal GamutFitVariant Variant { get; init; }
    }

    /// <summary>
    /// The Culori pentru hartie tab (R-178): filled after an analysis by the computation
    /// MainViewModel starts or by "Calculeaza", with the gallery of the original and the three
    /// variants, browsed with Next and Previous, each saveable as a new FITS or TIFF.
    /// </summary>
    public sealed class GamutFitTabViewModel : ResultTabViewModel
    {
        /// <summary>
        /// The diagnostic log: a computation that failed or was superseded, a save that failed;
        /// the core's GamutFitRun writes each computation's start and outcome (R-171, ADR 0069).
        /// </summary>
        static readonly Logger Log = LogManager.GetLogger(Loggers.GamutFit);

        /// <summary>The red of the layer: the palette's bad red, half transparent, as the soft proof's warning (R-161).</summary>
        static readonly Color OutsideColour = Color.FromArgb(150, VerdictPalette.Bad.R, VerdictPalette.Bad.G, VerdictPalette.Bad.B);

        AnalysisResult _result;
        GamutFitSet _set;
        IReadOnlyList<GamutFitGalleryItem> _items = Array.Empty<GamutFitGalleryItem>();
        int _index, _width = Thresholds.CurvePreviewDefaultWidth, _height = Thresholds.CurvePreviewDefaultHeight;

        // Each computation started has its number; one that ends after another image was opened,
        // or after another was started, touches nothing any more.
        int _run;
        bool _busy, _isProblem, _showOutside;
        string _message, _saveMessage, _saveError;

        /// <summary>Makes the four commands; the tab starts empty and disabled.</summary>
        public GamutFitTabViewModel()
        {
            Calculate = new RelayCommand(() => _ = ComputeAsync(), () => _result != null && !IsBusy);
            Next = new RelayCommand(() => Move(1), () => _index < _items.Count - 1);
            Previous = new RelayCommand(() => Move(-1), () => _index > 0);
            SaveAs = new RelayCommand(() => SaveRequested?.Invoke(GamutFitTexts.ProposedFileName(_result.Path, Current.Name)), HasVariant);
        }

        public ICommand Calculate { get; }
        public ICommand Next { get; }
        public ICommand Previous { get; }
        public ICommand SaveAs { get; }

        /// <summary>The window asks where to save, with the proposed name, then calls Save.</summary>
        public event Action<string> SaveRequested;

        public bool IsBusy
        {
            get => _busy;
            private set { if (Set(ref _busy, value)) (Calculate as RelayCommand)?.RaiseCanExecuteChanged(); }
        }

        /// <summary>The box "arata ce nu intra": the red layer on the shown item (R-178).</summary>
        public bool ShowOutside
        {
            get => _showOutside;
            set
            {
                if (!Set(ref _showOutside, value)) return;
                foreach (GamutFitGalleryItem i in _items) i.ShowOutside = value;
                Log.Debug("show outside=" + value);
            }
        }

        public IReadOnlyList<GamutFitGalleryItem> Items { get => _items; private set => Set(ref _items, value); }
        public bool HasGallery => _items.Count > 0;
        public GamutFitGalleryItem Current => _items.Count == 0 ? null : _items[_index];
        public string Position => _items.Count == 0 ? "" : $"{_index + 1} / {_items.Count}";

        /// <summary>Why there is nothing to show, or the computation's error; null when the gallery exists.</summary>
        public string Message { get => _message; private set => Set(ref _message, value); }

        /// <summary>The message is a reason of the core's (no profile, an unusable one): shown in red.</summary>
        public bool IsProblem { get => _isProblem; private set => Set(ref _isProblem, value); }

        /// <summary>The note under the gallery, the core's sentence.</summary>
        public string Note => GamutFitTexts.Note;

        public string SaveMessage { get => _saveMessage; private set => Set(ref _saveMessage, value); }
        public string SaveError { get => _saveError; private set => Set(ref _saveError, value); }

        /// <summary>The image's folder, where the save dialog starts; null without a file on disk.</summary>
        public string InputFolder => _result?.FullPath == null || !File.Exists(_result.FullPath) ? null : Path.GetDirectoryName(_result.FullPath);

        /// <summary>The size of the gallery area, in pixels: the view sets it, "Calculeaza" uses it.</summary>
        /// <param name="width">The width in device pixels; 0 or less leaves the size as it was.</param>
        /// <param name="height">The height in device pixels; 0 or less leaves the size as it was.</param>
        internal void SetGallerySize(int width, int height)
        {
            if (width > 0 && height > 0) { _width = width; _height = height; }
        }

        /// <summary>Whether the current item is a variant, not the original: what SaveAs needs.</summary>
        /// <returns>True on a variant; false on the original or with no gallery.</returns>
        bool HasVariant() => Current?.Variant != null;

        /// <summary>
        /// Keeps the result and enables "Calculeaza"; it fills nothing itself: MainViewModel
        /// starts the computation after every tab is filled (ADR 0051). Any computation in
        /// progress is abandoned.
        /// </summary>
        /// <param name="r">The analysis; not null.</param>
        internal override void Populate(AnalysisResult r)
        {
            Reset();
            _result = r;
            HasResult = true;
            (Calculate as RelayCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>Empties the tab, drops the result and disables "Calculeaza".</summary>
        internal override void Clear()
        {
            base.Clear();
            Reset();
            _result = null;
            (Calculate as RelayCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>
        /// Empties the tab, turns the box off and abandons any computation in progress: its
        /// result is nobody's any more.
        /// </summary>
        void Reset()
        {
            _run++;
            _set = null;
            _index = 0;
            Items = Array.Empty<GamutFitGalleryItem>();
            Message = null; IsProblem = false; SaveMessage = null; SaveError = null;
            if (Set(ref _showOutside, false, nameof(ShowOutside))) Log.Debug("show outside=False (reset)");
            IsBusy = false;
            RaiseGallery();
        }

        /// <summary>What "Calculeaza" runs, synchronously: the fit, at the given size of the gallery.</summary>
        /// <param name="width">The gallery's width in pixels; 0 or less keeps the current size.</param>
        /// <param name="height">The gallery's height in pixels; 0 or less keeps the current size.</param>
        internal void Compute(int width, int height)
        {
            SetGallerySize(width, height);
            _run++;
            Show(GamutFitRun.Execute(_result, _width, _height));
        }

        /// <summary>
        /// "Calculeaza": runs the core on a pool thread at the gallery's size, with "computing"
        /// shown meanwhile; the outcome is applied only if no Reset or newer computation came in
        /// between.
        /// </summary>
        internal async Task ComputeAsync()
        {
            AnalysisResult result = _result;
            int w = _width, h = _height, run = ++_run;
            IsBusy = true;
            IsProblem = false;
            Message = GamutFitTexts.Computing;
            try
            {
                GamutFitSet set = await Task.Run(() => GamutFitRun.Execute(result, w, h));
                if (run == _run) Show(set);
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
        /// Takes a set: the bitmaps and the layers made once, the previews dropped; a set with
        /// a problem empties the gallery and shows the problem in red.
        /// </summary>
        /// <param name="set">The core's set; not null.</param>
        void Show(GamutFitSet set)
        {
            SaveMessage = null; SaveError = null;
            _index = 0;
            if (set.Problem != null)
            {
                _set = null;
                Items = Array.Empty<GamutFitGalleryItem>();
                Message = set.Problem;
                IsProblem = true;
                RaiseGallery();
                return;
            }
            // The previews' rasters are no longer needed once the bitmaps exist: the items and
            // the set keep the variants without them. The table and the knees stay, for the save.
            GamutFitVariant[] stripped = set.Variants.Select(v => v with { Preview = null, Outside = null }).ToArray();
            var items = new List<GamutFitGalleryItem> { Item(CurveTexts.Original, set.OriginalPreview, set.OriginalOutside, null, set.Recipe) };
            for (int i = 0; i < set.Variants.Count; i++) items.Add(Item(stripped[i].Name, set.Variants[i].Preview, set.Variants[i].Outside, stripped[i], set.Recipe));
            _set = set with { OriginalPreview = null, OriginalOutside = null, Variants = stripped };
            Message = null;
            Items = items;
            foreach (GamutFitGalleryItem i in items) i.ShowOutside = _showOutside;
            RaiseGallery();
        }

        /// <summary>One gallery item: its bitmap, its layer and its evidence, formatted by the core.</summary>
        /// <param name="name">The item's name, as the gallery shows it.</param>
        /// <param name="preview">The preview raster, at the gallery's size.</param>
        /// <param name="outside">Per preview pixel, whether the paper cannot hold it; may be null.</param>
        /// <param name="v">The variant, without its preview; null for the original.</param>
        /// <param name="recipe">The recipe, for the evidence.</param>
        /// <returns>The item, not yet current.</returns>
        static GamutFitGalleryItem Item(string name, Raster preview, bool[] outside, GamutFitVariant v, GamutFitRecipe recipe)
        {
            int w = preview.W, h = preview.H;
            bool any = outside != null && outside.Any(o => o);
            return new GamutFitGalleryItem
            {
                Name = name,
                Image = RasterBitmaps.Region(preview, 0, 0, w, h),
                Width = w,
                Height = h,
                OutsideLayer = any ? RasterBitmaps.Overlay(w, h, (x, y) => outside[y * w + x], OutsideColour) : null,
                EvidenceRows = v == null ? Array.Empty<CurveEvidenceRow>()
                    : GamutFitTexts.EvidenceRows(v, recipe).Select(r => new CurveEvidenceRow { Text = r.Text, IsWarning = r.Warning }).ToArray(),
                Variant = v,
            };
        }

        /// <summary>Moves the position in the gallery, stopping at its ends.</summary>
        /// <param name="step">+1 for Next, -1 for Previous.</param>
        void Move(int step)
        {
            _index = Math.Clamp(_index + step, 0, Math.Max(0, _items.Count - 1));
            RaiseGallery();
        }

        /// <summary>Marks the current item, raises the gallery properties and requeries the commands.</summary>
        void RaiseGallery()
        {
            for (int i = 0; i < _items.Count; i++) _items[i].IsCurrent = i == _index;
            Raise(nameof(HasGallery)); Raise(nameof(Current)); Raise(nameof(Position));
            foreach (ICommand c in new[] { Next, Previous, SaveAs }) (c as RelayCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>Saves the variant shown into a new file (R-177); an error is told, not thrown.</summary>
        /// <param name="path">The file to write, as the save dialog gave it.</param>
        internal void Save(string path)
        {
            try
            {
                GamutFitExport.Save(_result, _set, Current.Variant, path);
                SaveError = null;
                SaveMessage = CurveTexts.Saved(path);
            }
            catch (Exception e)
            {
                Log.Warn(e, "the fit could not be saved to " + path + "; the tab shows the message");
                SaveMessage = null;
                SaveError = MainViewModel.Describe(e);
            }
        }
    }
}
