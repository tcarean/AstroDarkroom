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

// GamutFitTab.cs - the Culori pentru hartie tab (R-178): the gallery of the three variants of
// the paper fit, each shown beside the original (R-179), the red layers of what the paper
// cannot hold, the evidence of each variant, the saving as a new FITS or 16-bit TIFF.
//
// It computes nothing: the variants, the previews, the masks, the figures and the sentences
// come from GamutFitRun; the saving is GamutFitExport's. Here are only the position in the
// gallery, the box, the commands and the bitmaps; the place of the comparison's line and the
// zoom are the view's, held nowhere here (ADR 0090). Like Editare Curbe, the content is a run
// after the analysis: MainViewModel starts it once the other tabs are filled (ADR 0051), once
// per image, since the images are the whole image whatever the area (R-188, ADR 0118).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NLog;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// An image of the tab: a variant of the fit, a position of the gallery, or the original,
    /// which is the other side of the comparison and no position (R-179).
    /// </summary>
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

        /// <summary>The evidence lines of a variant, with the core's warning mark; the original has none.</summary>
        public IReadOnlyList<CurveEvidenceRow> EvidenceRows { get; init; } = Array.Empty<CurveEvidenceRow>();

        internal GamutFitVariant Variant { get; init; }
    }

    /// <summary>
    /// The Culori pentru hartie tab (R-178): filled after an analysis by the computation
    /// MainViewModel starts, with the gallery of the three variants, browsed with Next and
    /// Previous and each saveable as a new FITS or TIFF, and the original they are compared
    /// with, all of them the whole image (R-188).
    /// </summary>
    public sealed class GamutFitTabViewModel : ResultTabViewModel
    {
        /// <summary>
        /// The diagnostic log: a computation that failed, was superseded or was abandoned while
        /// it waited; "gallery prepared", with the images' number, size and time, for each
        /// gallery made (none for a set with a problem); the box when it changes; a save that failed. The core's GamutFitRun writes each computation's start
        /// and outcome (R-171, ADR 0069).
        /// </summary>
        static readonly Logger Log = LogManager.GetLogger(Loggers.GamutFit);

        /// <summary>The red of the layer: the palette's bad red, half transparent, as the soft proof's warning (R-161).</summary>
        static readonly Color OutsideColour = Color.FromArgb(150, VerdictPalette.Bad.R, VerdictPalette.Bad.G, VerdictPalette.Bad.B);

        AnalysisResult _result;
        GamutFitSet _set;
        IReadOnlyList<GamutFitGalleryItem> _items = Array.Empty<GamutFitGalleryItem>();
        GamutFitGalleryItem _before;
        int _index;

        // Each computation started has its number; one that ends after another image was opened,
        // or after another was started, touches nothing any more.
        int _run;
        // The box starts on: the owner looks for the colours the paper cannot hold first (R-178).
        bool _busy, _isProblem, _showOutside = true, _reduced;
        string _message, _saveMessage, _saveError;

        /// <summary>
        /// The most pixels of an image the core takes whole, handed to GamutFitRun: the core's
        /// own limit (R-188) unless a test lowers it to reach a reduced copy with a small frame.
        /// </summary>
        internal int MaxPixels { get; init; } = Thresholds.WholeImageMaxPixels;

        /// <summary>Makes the three commands; the tab starts empty and disabled.</summary>
        public GamutFitTabViewModel()
        {
            Next = new RelayCommand(() => Move(1), () => _index < _items.Count - 1);
            Previous = new RelayCommand(() => Move(-1), () => _index > 0);
            SaveAs = new RelayCommand(() => SaveRequested?.Invoke(GamutFitTexts.ProposedFileName(_result.Path, Current.Name)), HasVariant);
        }

        public ICommand Next { get; }
        public ICommand Previous { get; }
        public ICommand SaveAs { get; }

        /// <summary>The window asks where to save, with the proposed name, then calls Save.</summary>
        public event Action<string> SaveRequested;

        public bool IsBusy { get => _busy; private set => Set(ref _busy, value); }

        /// <summary>
        /// The last computation started, by MainViewModel or by the tests, for whoever must
        /// wait; completed before the first.
        /// </summary>
        internal Task Computation { get; private set; } = Task.CompletedTask;

        /// <summary>
        /// The box "arata ce nu intra": the red layer on the original and on the variant shown,
        /// each its own (R-178).
        /// </summary>
        public bool ShowOutside
        {
            get => _showOutside;
            set
            {
                if (!Set(ref _showOutside, value)) return;
                foreach (GamutFitGalleryItem i in _items) i.ShowOutside = value;
                if (_before != null) _before.ShowOutside = value;
                Log.Debug("show outside=" + value);
            }
        }

        public IReadOnlyList<GamutFitGalleryItem> Items { get => _items; private set => Set(ref _items, value); }
        public bool HasGallery => _items.Count > 0;
        public GamutFitGalleryItem Current => _items.Count == 0 ? null : _items[_index];

        /// <summary>
        /// The original at the variants' size, with its own red layer: the left side of the
        /// comparison, the same whatever variant is shown (R-179); null without a gallery.
        /// </summary>
        public GamutFitGalleryItem Before { get => _before; private set => Set(ref _before, value); }

        /// <summary>
        /// The sentence under the image: which side of the line is which, and how to zoom and
        /// move, the maximum being the viewer's own (R-179).
        /// </summary>
        public string ViewerHelp => ViewerHelpFor(_reduced);

        /// <summary>
        /// The sentence under the image for a whole image or for a reduced copy of one over the
        /// core's limit (R-188): the zoom enlarges a pixel of the one or of the other.
        /// </summary>
        /// <param name="reduced">Whether the images shown are a reduced copy.</param>
        /// <returns>The sentence, in the interface language.</returns>
        internal static string ViewerHelpFor(bool reduced)
        {
            string max = Fmt.F0(ZoomPanViewer.MaxScale);
            string ofEn = reduced ? "copy" : "image", ofRo = reduced ? "copiei" : "imaginii";
            return Tr.Pick(
                $"Left of the line is the image as it is (Before), right of it the fitted one (After); drag the line to move it. The mouse wheel zooms around the cursor, up to {max}:1 (one pixel of the {ofEn} becomes a square of {max} x {max} screen pixels, with Windows display scaling at 100 %); hold the left button and drag to move the image; a double click fits it again.",
                $"In stanga liniei e imaginea asa cum este (Inainte), in dreapta cea potrivita (Dupa); tragi de linie ca sa o muti. Rotita mouse-ului mareste in jurul cursorului, pana la {max}:1 (un pixel al {ofRo} devine un patrat de {max} x {max} pixeli pe ecran, cu scalarea Windows la 100 %); tii apasat butonul stang si tragi ca sa muti imaginea; dublu-clic o potriveste din nou.");
        }

        public string Position => _items.Count == 0 ? "" : $"{_index + 1} / {_items.Count}";

        /// <summary>Why there is nothing to show, or the computation's error; null when the gallery exists.</summary>
        public string Message { get => _message; private set => Set(ref _message, value); }

        /// <summary>The message is a reason of the core's (no profile, an unusable one): shown in red.</summary>
        public bool IsProblem { get => _isProblem; private set => Set(ref _isProblem, value); }

        /// <summary>The note under the gallery, the core's sentence for a whole image or for a reduced copy.</summary>
        public string Note => GamutFitTexts.Note(_reduced);

        /// <summary>
        /// The images shown are a reduced copy: the image is over the core's limit for the
        /// whole image (R-188). False for the whole image, and without a gallery.
        /// </summary>
        public bool IsReducedCopy
        {
            get => _reduced;
            private set
            {
                if (!Set(ref _reduced, value)) return;
                Raise(nameof(Note));
                Raise(nameof(ViewerHelp));
            }
        }

        public string SaveMessage { get => _saveMessage; private set => Set(ref _saveMessage, value); }
        public string SaveError { get => _saveError; private set => Set(ref _saveError, value); }

        /// <summary>The image's folder, where the save dialog starts; null without a file on disk.</summary>
        public string InputFolder => _result?.FullPath == null || !File.Exists(_result.FullPath) ? null : Path.GetDirectoryName(_result.FullPath);

        /// <summary>Whether there is a variant shown: what SaveAs needs.</summary>
        /// <returns>True with a gallery; false with none.</returns>
        bool HasVariant() => Current?.Variant != null;

        /// <summary>
        /// Keeps the result; it fills nothing itself: MainViewModel starts the computation after
        /// every tab is filled (ADR 0051). Any computation in progress is abandoned.
        /// </summary>
        /// <param name="r">The analysis; not null.</param>
        internal override void Populate(AnalysisResult r)
        {
            Reset();
            _result = r;
            HasResult = true;
        }

        /// <summary>Empties the tab and drops the result.</summary>
        internal override void Clear()
        {
            base.Clear();
            Reset();
            _result = null;
        }

        /// <summary>
        /// Empties the tab, puts the box back on and abandons any computation in progress: its
        /// result is nobody's any more.
        /// </summary>
        void Reset()
        {
            _run++;
            _set = null;
            _index = 0;
            Before = null;
            Items = Array.Empty<GamutFitGalleryItem>();
            Message = null; IsProblem = false; SaveMessage = null; SaveError = null; IsReducedCopy = false;
            if (Set(ref _showOutside, true, nameof(ShowOutside))) Log.Debug("show outside=True (reset)");
            IsBusy = false;
            RaiseGallery();
        }

        /// <summary>The computation, synchronously (the tests): the fit and its bitmaps.</summary>
        internal void Compute()
        {
            _run++;
            // A computation in flight, or one still waiting, is superseded: its finally will not touch this.
            IsBusy = false;
            Show(Prepare(GamutFitRun.Execute(_result, maxPixels: MaxPixels)));
        }

        /// <summary>
        /// Runs the core and makes the bitmaps on a pool thread, with "computing" shown
        /// meanwhile; the outcome is applied only if no Reset or newer computation came in
        /// between.
        /// </summary>
        internal Task ComputeAsync() => Computation = ComputeCore(null);

        /// <summary>
        /// As <see cref="ComputeAsync"/>, started only when <paramref name="before"/> has ended,
        /// with "computing" shown from now: MainViewModel hands it the soft proof's computation,
        /// since the two started together take every core from each other (on 22.6 MP the proof
        /// was shown after 9.5 to 14.5 s for 3.2 s, the fit after 18 s for 15 s; ADR 0118).
        /// Another image opened meanwhile abandons the wait, and the core is not started.
        /// </summary>
        /// <param name="before">The computation to wait for; not null. However it ends, the fit follows.</param>
        /// <returns>The task of the wait and the computation, which is also <see cref="Computation"/>.</returns>
        internal Task ComputeAfterAsync(Task before) => Computation = ComputeCore(before);

        /// <summary>The body of <see cref="ComputeAsync"/> and <see cref="ComputeAfterAsync"/>.</summary>
        /// <param name="before">A computation to wait for first; null to start at once.</param>
        async Task ComputeCore(Task before)
        {
            AnalysisResult result = _result;
            int run = ++_run;
            IsBusy = true;
            IsProblem = false;
            Message = GamutFitTexts.Computing;
            try
            {
                if (before != null)
                {
                    // The other computation reports its own failure; here only its end matters.
                    try { await before; }
                    catch (Exception e) { Log.Debug(e, "the computation waited for failed; the fit starts all the same"); }
                    if (run != _run)
                    {
                        Log.Debug("computation " + LogText.Int(run) + " was abandoned while it waited; the core is not started");
                        return;
                    }
                }
                // The bitmaps are made there too: whole-image ones would hold the window's thread
                // for most of a second (0.9 s measured on 22.6 MP, ADR 0118).
                Gallery gallery = await Task.Run(() => Prepare(GamutFitRun.Execute(result, maxPixels: MaxPixels)));
                if (run == _run) Show(gallery);
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
        /// What a computation hands to the window's thread: the core's set without its
        /// rasters, and the frozen images made from them.
        /// </summary>
        /// <param name="Set">The set, its previews and masks dropped; with a Problem, the rest is empty.</param>
        /// <param name="Before">The original's item; null with a Problem.</param>
        /// <param name="Items">One item per variant, in the set's order; empty with a Problem.</param>
        sealed record Gallery(GamutFitSet Set, GamutFitGalleryItem Before, IReadOnlyList<GamutFitGalleryItem> Items);

        /// <summary>
        /// Makes the bitmaps and the red layers of a set, once, on whichever thread, and drops
        /// its rasters: the items and the set keep the variants without them, the table and the
        /// knees staying for the save. The original is the other side of the comparison, the
        /// same for every variant, and no position of the gallery (R-179).
        /// </summary>
        /// <param name="set">The core's set; not null.</param>
        /// <returns>The gallery; without items when the set has a Problem.</returns>
        static Gallery Prepare(GamutFitSet set)
        {
            if (set.Problem != null) return new Gallery(set, null, Array.Empty<GamutFitGalleryItem>());
            var clock = Stopwatch.StartNew();
            GamutFitVariant[] stripped = set.Variants.Select(v => v with { Preview = null, Outside = null }).ToArray();
            GamutFitGalleryItem before = Item(CurveTexts.Original, set.OriginalPreview, set.OriginalOutside, null, set.Recipe);
            var items = new List<GamutFitGalleryItem>();
            for (int i = 0; i < set.Variants.Count; i++) items.Add(Item(stripped[i].Name, set.Variants[i].Preview, set.Variants[i].Outside, stripped[i], set.Recipe));
            // The line's thread is the one that made the bitmaps: a pool thread when the window
            // computes (ComputeAfterAsync, ComputeAsync), the caller's own in the tests' synchronous Compute.
            Log.Debug("gallery prepared: " + LogText.Int(items.Count + 1) + " images of " + LogText.Int(before.Width) + "x" + LogText.Int(before.Height) + " in " + LogText.Ms(clock.ElapsedMilliseconds));
            return new Gallery(set with { OriginalPreview = null, OriginalOutside = null, Variants = stripped }, before, items);
        }

        /// <summary>
        /// Takes a computation's gallery; a set with a problem empties the gallery and shows the
        /// problem in red.
        /// </summary>
        /// <param name="gallery">What <see cref="Prepare"/> made; not null.</param>
        void Show(Gallery gallery)
        {
            GamutFitSet set = gallery.Set;
            SaveMessage = null; SaveError = null;
            IsReducedCopy = set.Problem == null && set.Stride > 1;
            if (set.Problem != null)
            {
                _set = null;
                _index = 0;
                Before = null;
                Items = Array.Empty<GamutFitGalleryItem>();
                Message = set.Problem;
                IsProblem = true;
                RaiseGallery();
                return;
            }
            _set = set;
            Message = null;
            gallery.Before.ShowOutside = _showOutside;
            foreach (GamutFitGalleryItem i in gallery.Items) i.ShowOutside = _showOutside;
            // A new image starts at the first variant, since Reset put the position there.
            _index = Math.Clamp(_index, 0, Math.Max(0, gallery.Items.Count - 1));
            Before = gallery.Before;
            Items = gallery.Items;
            RaiseGallery();
        }

        /// <summary>
        /// One image of the tab: its bitmap, its layer and, for a variant, its evidence,
        /// formatted by the core.
        /// </summary>
        /// <param name="name">The item's name, as the gallery shows it.</param>
        /// <param name="preview">The image's raster, at the set's step.</param>
        /// <param name="outside">Per preview pixel, whether the paper cannot hold it; may be null.</param>
        /// <param name="v">The variant, without its preview; null for the original, which carries no evidence.</param>
        /// <param name="recipe">The recipe, for a variant's evidence.</param>
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
                EvidenceRows = v == null
                    ? Array.Empty<CurveEvidenceRow>()
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
