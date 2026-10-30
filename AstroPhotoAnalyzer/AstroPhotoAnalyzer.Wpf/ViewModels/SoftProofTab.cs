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

// SoftProofTab.cs - the Soft proof tab (R-163) and its full-screen window: the proof of the
// image through the paper's ICC profile, the paper's white and black shown as they are, with the
// intent, the paper's gamut warning, the original for comparison and the margin around the image.
//
// It computes nothing: the proof, the classes of the pixels, the shares and every sentence come
// from ProofRun (R-160 to R-162). Here are only the state of the controls, the bitmaps made from
// the core's rasters and classes, the brush of the margin and the commands. Like the curves, the
// proof is a run after the analysis: MainViewModel starts it once the other tabs are filled
// (ADR 0051), a change of intent renders again on a pool thread, and the other controls only
// choose what is shown. The proof is always the paper and ink simulated (ADR 0066: the box
// Lightroom has was dropped, nothing in this tool needed its other state). The bitmaps are made on the pool thread too, frozen:
// three overlays over the reduction (2.7 MP on the reference image, up to 2400 px on the long
// side) would stall the window for a visible moment.

using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>The margin around the proof (R-163): Lightroom's backgrounds plus the automatic one.</summary>
    public enum ProofMarginChoice
    {
        /// <summary>The paper's white, the one the proof's white becomes.</summary>
        PaperWhite,
        White,
        LightGrey,
        Grey,
        DarkGrey,
        Black,
    }

    /// <summary>
    /// The Soft proof tab (R-163): the intent, the two boxes, the margin, the proof with its
    /// warning layer, the core's facts and legend, and the full-screen window's request.
    /// </summary>
    public sealed class SoftProofTabViewModel : ResultTabViewModel
    {
        // The warning's paint: red over the pixels outside the paper's gamut.
        static readonly Color PaperColour = Color.FromArgb(150, 0xF8, 0x51, 0x49);

        // Lightroom's fixed backgrounds: presentation, nothing is measured against them.
        static readonly Color White = Color.FromRgb(0xFF, 0xFF, 0xFF);
        static readonly Color LightGrey = Color.FromRgb(0xC0, 0xC0, 0xC0);
        static readonly Color Grey = Color.FromRgb(0x80, 0x80, 0x80);
        static readonly Color DarkGrey = Color.FromRgb(0x40, 0x40, 0x40);
        static readonly Color Black = Color.FromRgb(0x00, 0x00, 0x00);

        /// <summary>
        /// What one computation or render hands back from the pool thread: the set, the proof
        /// and the frozen bitmaps made from them (null bitmaps with a problem). The original and
        /// the paper layer belong to the set and are made once for it.
        /// </summary>
        /// <param name="Set">The prepared set, with or without a problem.</param>
        /// <param name="Proof">The proof; null when the set has a problem.</param>
        /// <param name="Original">The reduced original's bitmap; null with a problem.</param>
        /// <param name="Image">The proof's bitmap; null with a problem.</param>
        /// <param name="Paper">The paper-gamut layer; null with a problem.</param>
        sealed record Outcome(ProofSet Set, SoftProofResult Proof, BitmapSource Original, BitmapSource Image, BitmapSource Paper);

        AnalysisResult _result;
        ProofSet _set;
        SoftProofResult _proof;
        BitmapSource _original, _proofImage, _paperLayer;
        int _run;
        bool _busy, _isProblem;
        string _message;

        ProofIntent _intent = ProofIntent.Perceptual;
        bool _showPaper, _showOriginal;
        ProofMarginChoice _margin = ProofMarginChoice.PaperWhite;

        /// <summary>Makes the command; the tab starts empty.</summary>
        public SoftProofTabViewModel()
        {
            FullScreen = new RelayCommand(() => ProofRequested?.Invoke(this), () => HasProof);
        }

        /// <summary>Opens the full-screen window over this same view-model (R-163).</summary>
        public ICommand FullScreen { get; }

        /// <summary>The window opens the full-screen window; the view-model only asks for it.</summary>
        public event Action<SoftProofTabViewModel> ProofRequested;

        /// <summary>
        /// The computation or render last started (R-163), for whoever must wait for it;
        /// completed before the first and after one that was abandoned. Both
        /// <see cref="ComputeAsync"/> and a change of intent set it.
        /// </summary>
        internal Task ProofComputation { get; private set; } = Task.CompletedTask;

        // ---- the controls

        public bool IsPerceptual { get => _intent == ProofIntent.Perceptual; set { if (value) Intent = ProofIntent.Perceptual; } }
        public bool IsRelative { get => _intent == ProofIntent.Relative; set { if (value) Intent = ProofIntent.Relative; } }
        public bool IsRelativeNoBpc { get => _intent == ProofIntent.RelativeNoBpc; set { if (value) Intent = ProofIntent.RelativeNoBpc; } }

        /// <summary>
        /// The intent shown; a change renders again (the intent's table is built on its first
        /// use). During the first computation the change is kept and rendered when it ends.
        /// </summary>
        internal ProofIntent Intent
        {
            get => _intent;
            set
            {
                if (_intent == value) return;
                _intent = value;
                Raise(nameof(IsPerceptual)); Raise(nameof(IsRelative)); Raise(nameof(IsRelativeNoBpc));
                if (HasControls) ProofComputation = RenderAsync();
            }
        }

        public bool ShowPaperGamut { get => _showPaper; set { if (Set(ref _showPaper, value)) RaiseLayers(); } }

        /// <summary>"Originalul": the image as it is, inside the same margin, without the warnings.</summary>
        public bool ShowOriginal { get => _showOriginal; set { if (Set(ref _showOriginal, value)) { Raise(nameof(Image)); RaiseLayers(); } } }

        public ProofMarginChoice Margin { get => _margin; set { if (Set(ref _margin, value)) Raise(nameof(MarginBrush)); } }

        // ---- what is shown

        /// <summary>The proof, or the original when <see cref="ShowOriginal"/> is on; null before a proof.</summary>
        public BitmapSource Image => _showOriginal ? _original : _proofImage;
        public int Width => _proof?.Image.W ?? 0;
        public int Height => _proof?.Image.H ?? 0;

        /// <summary>The pixels outside the paper's gamut, in red (R-161).</summary>
        public BitmapSource PaperLayer => _paperLayer;

        public bool ShowPaperLayer => HasProof && !_showOriginal && _showPaper;

        /// <summary>The margin's colour: the paper's white, or the chosen fixed background.</summary>
        public Brush MarginBrush
        {
            get
            {
                Color c = _margin switch
                {
                    ProofMarginChoice.White => White,
                    ProofMarginChoice.LightGrey => LightGrey,
                    ProofMarginChoice.Grey => Grey,
                    ProofMarginChoice.DarkGrey => DarkGrey,
                    ProofMarginChoice.Black => Black,
                    _ => HasControls ? PaperWhiteColour : White,
                };
                var b = new SolidColorBrush(c);
                b.Freeze();
                return b;
            }
        }

        /// <summary>The paper's white as the core displays it (R-163); the screen's white before a proof.</summary>
        public Color PaperWhiteColour
        {
            get
            {
                float[] w = _set?.Profile?.PaperWhiteSrgb;
                return w == null ? White : Color.FromRgb((byte)RasterOps.Level8(w[0]), (byte)RasterOps.Level8(w[1]), (byte)RasterOps.Level8(w[2]));
            }
        }

        /// <summary>
        /// The set is prepared and usable: the controls are shown, a render can be asked for,
        /// even while one is computed or after one failed.
        /// </summary>
        public bool HasControls => _set != null && _set.Problem == null;

        public bool HasProof => _proof != null;
        public bool IsBusy { get => _busy; private set { if (Set(ref _busy, value)) (FullScreen as RelayCommand)?.RaiseCanExecuteChanged(); } }

        /// <summary>Why there is no proof (no profile, an unusable one or table), "computing", or an error; null with a proof.</summary>
        public string Message { get => _message; private set => Set(ref _message, value); }

        /// <summary>The message is a reason of the core's (R-162): the window shows it in red.</summary>
        public bool IsProblem { get => _isProblem; private set => Set(ref _isProblem, value); }

        /// <summary>The window's title: the image's name.</summary>
        public string Title => _result == null ? "Soft proof" : $"Soft proof - {System.IO.Path.GetFileName(_result.Path)}";

        public string Caption => _proof == null ? "" : ProofTexts.Caption(_set.Profile, _proof.Intent);
        public string ProfileLine => _set?.Profile == null ? "" : ProofTexts.ProfileLine(_set.Profile);
        public string PaperWhiteLine => _set?.Profile == null ? "" : ProofTexts.PaperWhiteLine(_set.Profile);
        public string PaperBlackLine => _set?.Profile == null ? "" : ProofTexts.PaperBlackLine(_set.Profile);
        public string PaperGamutLegend => HasControls ? ProofTexts.PaperGamutLegend(_set.PctPaper) : "";

        public string PerceptualName => ProofTexts.IntentName(ProofIntent.Perceptual);
        public string RelativeName => ProofTexts.IntentName(ProofIntent.Relative);
        public string RelativeNoBpcName => ProofTexts.IntentName(ProofIntent.RelativeNoBpc);
        public string PerceptualHelp => ProofTexts.IntentHelp(ProofIntent.Perceptual);
        public string RelativeHelp => ProofTexts.IntentHelp(ProofIntent.Relative);
        public string RelativeNoBpcHelp => ProofTexts.IntentHelp(ProofIntent.RelativeNoBpc);

        /// <summary>
        /// Keeps the result; unlike most tabs it fills nothing itself: MainViewModel starts the
        /// computation after every tab is filled (ADR 0051). Any computation in progress is
        /// abandoned. The controls keep their state (R-163).
        /// </summary>
        /// <param name="r">The analysis; not null.</param>
        internal override void Populate(AnalysisResult r)
        {
            Reset();
            _result = r;
            HasResult = true;
            Raise(nameof(Title));
        }

        /// <inheritdoc/>
        internal override void Clear()
        {
            base.Clear();
            Reset();
            _result = null;
            Raise(nameof(Title));
        }

        /// <summary>Drops the set and the proof and abandons any computation in progress: its result is nobody's any more.</summary>
        void Reset()
        {
            _run++;
            _set = null;
            DropProof();
            Message = null; IsProblem = false;
            IsBusy = false;
            RaiseProof();
        }

        /// <summary>Drops the proof and its bitmap; the set, the original and the paper layer stay with it.</summary>
        void DropProof()
        {
            _proof = null;
            _proofImage = null;
            if (_set == null) _original = _paperLayer = null;
        }

        /// <summary>
        /// What the analysis starts, synchronously: the set prepared and the proof rendered with
        /// the controls as they are (the tests' path).
        /// </summary>
        internal void Compute()
        {
            _run++;
            // A computation in flight is superseded: its finally will not touch these.
            IsBusy = false; Message = null; IsProblem = false;
            Show(Prepare(_result, _intent));
        }

        /// <summary>
        /// Prepares the set and renders the proof on a pool thread, "computing" shown
        /// meanwhile; the outcome is applied only if no Reset or newer computation came in
        /// between, and a control changed meanwhile is rendered right after. An exception
        /// becomes the message, never a throw. The task is also <see cref="ProofComputation"/>.
        /// </summary>
        internal Task ComputeAsync() => ProofComputation = ComputeCore();

        /// <summary>The body of <see cref="ComputeAsync"/>.</summary>
        async Task ComputeCore()
        {
            AnalysisResult result = _result;
            if (result == null) return;
            ProofIntent intent = _intent;
            int run = ++_run;
            IsBusy = true; IsProblem = false; Message = ProofTexts.Computing;
            try
            {
                Outcome outcome = await Task.Run(() => Prepare(result, intent));
                if (run != _run) return;
                Show(outcome);
                IsBusy = false;
                // The intent changed while the table was built: the proof shown is the old
                // one's, so render the new one now rather than wait for the next change.
                if (HasControls && _intent != intent) await RenderAsync();
            }
            catch (Exception e)
            {
                if (run == _run) { Reset(); Message = MainViewModel.Describe(e); }
            }
            finally
            {
                if (run == _run) IsBusy = false;
            }
        }

        /// <summary>
        /// Renders the prepared set again on a pool thread, after a change of intent; a late
        /// outcome is dropped like a late computation. A table the core
        /// refuses (its Problem) leaves the set, so another intent can still be chosen, and shows
        /// the reason in red with no image.
        /// </summary>
        async Task RenderAsync()
        {
            ProofSet set = _set; ProofIntent intent = _intent;
            BitmapSource original = _original, paper = _paperLayer;   // read here, on the window's thread, as the set is
            int run = ++_run;
            IsBusy = true; IsProblem = false; Message = ProofTexts.Computing;
            try
            {
                Outcome outcome = await Task.Run(() => Render(set, intent, original, paper));
                if (run == _run) Show(outcome);
            }
            catch (Exception e)
            {
                if (run != _run) return;
                DropProof();
                Message = e is ArgumentException ? e.Message : MainViewModel.Describe(e);
                IsProblem = e is ArgumentException;
                RaiseProof();
            }
            finally
            {
                if (run == _run) IsBusy = false;
            }
        }

        /// <summary>The core's two steps and the bitmaps, on whichever thread; a new set, so the original is made too.</summary>
        /// <param name="result">The analysis.</param>
        /// <param name="intent">The intent to render.</param>
        /// <returns>The outcome; without a proof and bitmaps when the set has a problem.</returns>
        static Outcome Prepare(AnalysisResult result, ProofIntent intent)
        {
            ProofSet set = ProofRun.Prepare(result);
            return set.Problem == null ? Render(set, intent, null, null) : new Outcome(set, null, null, null, null);
        }

        /// <summary>
        /// The core's render, the paper and ink simulated, and the frozen bitmaps of its result,
        /// on whichever thread.
        /// </summary>
        /// <param name="set">A set without a problem.</param>
        /// <param name="intent">The intent to render; one the core refuses throws its reason.</param>
        /// <param name="original">The original's bitmap when already made for this set; null to make it.</param>
        /// <param name="paper">The paper layer when already made for this set; null to make it.</param>
        /// <returns>The outcome, bitmaps included.</returns>
        static Outcome Render(ProofSet set, ProofIntent intent, BitmapSource original, BitmapSource paper)
        {
            SoftProofResult proof = ProofRun.Render(set, intent, simulate: true);
            Raster img = proof.Image;
            int w = img.W, h = img.H;
            bool[] outside = set.PaperOutside;
            return new Outcome(set, proof,
                original ?? RasterBitmaps.Region(set.Reduced, 0, 0, w, h),
                RasterBitmaps.Region(img, 0, 0, w, h),
                paper ?? RasterBitmaps.Overlay(w, h, (x, y) => outside[y * w + x], PaperColour));
        }

        /// <summary>Takes a set and its proof with their bitmaps; a set with a problem empties the tab and shows it in red.</summary>
        /// <param name="o">The outcome; a set with a problem carries no proof.</param>
        void Show(Outcome o)
        {
            _set = o.Set;
            _proof = o.Proof;
            if (_set.Problem != null)
            {
                _original = _proofImage = _paperLayer = null;
                Message = _set.Problem; IsProblem = true;
                RaiseProof();
                return;
            }
            _original = o.Original;
            _proofImage = o.Image;
            _paperLayer = o.Paper;
            Message = null; IsProblem = false;
            RaiseProof();
        }

        /// <summary>Raises everything that follows the set and the proof.</summary>
        void RaiseProof()
        {
            Raise(nameof(HasControls)); Raise(nameof(HasProof)); Raise(nameof(Image)); Raise(nameof(Width)); Raise(nameof(Height));
            Raise(nameof(PaperLayer));
            Raise(nameof(Caption)); Raise(nameof(ProfileLine)); Raise(nameof(PaperWhiteLine)); Raise(nameof(PaperBlackLine));
            Raise(nameof(PaperGamutLegend));
            Raise(nameof(MarginBrush)); Raise(nameof(PaperWhiteColour));
            RaiseLayers();
            (FullScreen as RelayCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>Raises the layer's visibility.</summary>
        void RaiseLayers() => Raise(nameof(ShowPaperLayer));

        /// <summary>
        /// The full-screen window's keys (R-163): G and O toggle the two boxes, I moves to the
        /// next intent; anything else is left alone. The window passes a key only without
        /// modifiers.
        /// </summary>
        /// <param name="key">The key pressed.</param>
        /// <returns>Whether the key was one of the three.</returns>
        internal bool HandleKey(Key key)
        {
            switch (key)
            {
                case Key.G: ShowPaperGamut = !ShowPaperGamut; return true;
                case Key.O: ShowOriginal = !ShowOriginal; return true;
                case Key.I: Intent = (ProofIntent)(((int)_intent + 1) % 3); return true;
                default: return false;
            }
        }
    }
}
