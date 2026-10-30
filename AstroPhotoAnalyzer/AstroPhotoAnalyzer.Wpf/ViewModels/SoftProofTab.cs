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
// image through the paper's ICC profile, with the intent, the simulation of paper and ink, the
// two gamut warnings, the original for comparison and the margin around the image.
//
// It computes nothing: the proof, the classes of the pixels, the shares and every sentence come
// from ProofRun (R-160 to R-162). Here are only the state of the controls, the bitmaps made from
// the core's rasters and classes, the brush of the margin and the commands. Like the curves,
// the proof is a run after the analysis: MainViewModel starts it once the other tabs are filled
// (ADR 0051), a change of intent or of the simulation renders again on a pool thread, and the
// other controls only choose what is shown.

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
        /// <summary>The screen's white with the simulation off, the paper's white with it on.</summary>
        Auto,
        White,
        LightGrey,
        Grey,
        DarkGrey,
        Black,
    }

    /// <summary>
    /// The Soft proof tab (R-163): the intent, the four boxes, the margin, the proof with its
    /// warning layers, the core's facts and legends, and the full-screen window's request.
    /// </summary>
    public sealed class SoftProofTabViewModel : ResultTabViewModel
    {
        // The warnings' paints: red for the paper's gamut and blue for the screen's, as
        // Lightroom; magenta where both apply.
        static readonly Color PaperColour = Color.FromArgb(150, 0xF8, 0x51, 0x49);
        static readonly Color ScreenColour = Color.FromArgb(150, 0x4E, 0xA8, 0xDE);
        static readonly Color BothColour = Color.FromArgb(170, 0xD9, 0x46, 0xEF);

        // Lightroom's fixed backgrounds: presentation, nothing is measured against them.
        static readonly Color White = Color.FromRgb(0xFF, 0xFF, 0xFF);
        static readonly Color LightGrey = Color.FromRgb(0xC0, 0xC0, 0xC0);
        static readonly Color Grey = Color.FromRgb(0x80, 0x80, 0x80);
        static readonly Color DarkGrey = Color.FromRgb(0x40, 0x40, 0x40);
        static readonly Color Black = Color.FromRgb(0x00, 0x00, 0x00);

        AnalysisResult _result;
        ProofSet _set;
        SoftProofResult _proof;
        BitmapSource _original, _proofImage, _paperLayer, _screenLayer, _bothLayer;
        int _run;
        bool _busy, _isProblem;
        string _message;

        ProofIntent _intent = ProofIntent.Perceptual;
        bool _simulate, _showPaper, _showScreen, _showOriginal;
        ProofMarginChoice _margin = ProofMarginChoice.Auto;

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
        /// The proof's computation last started (R-163), for whoever must wait for it; completed
        /// before the first and after one that was abandoned.
        /// </summary>
        internal Task ProofComputation { get; private set; } = Task.CompletedTask;

        // ---- the controls

        public bool IsPerceptual { get => _intent == ProofIntent.Perceptual; set { if (value) Intent = ProofIntent.Perceptual; } }
        public bool IsRelative { get => _intent == ProofIntent.Relative; set { if (value) Intent = ProofIntent.Relative; } }
        public bool IsRelativeNoBpc { get => _intent == ProofIntent.RelativeNoBpc; set { if (value) Intent = ProofIntent.RelativeNoBpc; } }

        /// <summary>The intent shown; a change renders again (the intent's table is built on its first use).</summary>
        internal ProofIntent Intent
        {
            get => _intent;
            set
            {
                if (_intent == value) return;
                _intent = value;
                Raise(nameof(IsPerceptual)); Raise(nameof(IsRelative)); Raise(nameof(IsRelativeNoBpc));
                if (_set != null && _set.Problem == null) ProofComputation = RenderAsync();
            }
        }

        /// <summary>"Simuleaza hartia si cerneala": the paper's white and black shown as they are; a change renders again.</summary>
        public bool SimulatePaper
        {
            get => _simulate;
            set
            {
                if (!Set(ref _simulate, value)) return;
                Raise(nameof(MarginBrush));
                if (_set != null && _set.Problem == null) ProofComputation = RenderAsync();
            }
        }

        public bool ShowPaperGamut { get => _showPaper; set { if (Set(ref _showPaper, value)) RaiseLayers(); } }
        public bool ShowScreenGamut { get => _showScreen; set { if (Set(ref _showScreen, value)) RaiseLayers(); } }

        /// <summary>"Originalul": the image as it is, inside the same margin, without the warnings.</summary>
        public bool ShowOriginal { get => _showOriginal; set { if (Set(ref _showOriginal, value)) { Raise(nameof(Image)); RaiseLayers(); } } }

        public ProofMarginChoice Margin { get => _margin; set { if (Set(ref _margin, value)) Raise(nameof(MarginBrush)); } }

        // ---- what is shown

        /// <summary>The proof, or the original when <see cref="ShowOriginal"/> is on; null before a proof.</summary>
        public BitmapSource Image => _showOriginal ? _original : _proofImage;
        public int Width => _proof?.Image.W ?? 0;
        public int Height => _proof?.Image.H ?? 0;

        /// <summary>The pixels outside the paper's gamut, those outside both included, in red.</summary>
        public BitmapSource PaperLayer => _paperLayer;

        /// <summary>The pixels outside the screen's gamut, those outside both included, in blue.</summary>
        public BitmapSource ScreenLayer => _screenLayer;

        /// <summary>The pixels outside both, in magenta, over the two others.</summary>
        public BitmapSource BothLayer => _bothLayer;

        public bool ShowPaperLayer => HasProof && !_showOriginal && _showPaper;
        public bool ShowScreenLayer => HasProof && !_showOriginal && _showScreen;
        public bool ShowBothLayer => HasProof && !_showOriginal && _showPaper && _showScreen;

        /// <summary>The margin's colour: automatic, the screen's white or the paper's; else the chosen background.</summary>
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
                    _ => _simulate && _set?.Profile != null ? PaperWhiteColour : White,
                };
                var b = new SolidColorBrush(c);
                b.Freeze();
                return b;
            }
        }

        /// <summary>The paper's white as the core displays it under the simulation (R-163); the screen's white before a proof.</summary>
        public Color PaperWhiteColour
        {
            get
            {
                float[] w = _set?.Profile?.PaperWhiteSrgb;
                return w == null ? White : Color.FromRgb((byte)RasterOps.Level8(w[0]), (byte)RasterOps.Level8(w[1]), (byte)RasterOps.Level8(w[2]));
            }
        }

        public bool HasProof => _proof != null;
        public bool IsBusy { get => _busy; private set { if (Set(ref _busy, value)) (FullScreen as RelayCommand)?.RaiseCanExecuteChanged(); } }

        /// <summary>Why there is no proof (no profile, an unusable one), "computing", or an error; null with a proof.</summary>
        public string Message { get => _message; private set => Set(ref _message, value); }

        /// <summary>The message is a reason of the core's (R-162): the window shows it in red.</summary>
        public bool IsProblem { get => _isProblem; private set => Set(ref _isProblem, value); }

        /// <summary>The window's title: the image's name.</summary>
        public string Title => _result == null ? "Soft proof" : $"Soft proof - {System.IO.Path.GetFileName(_result.Path)}";

        public string Caption => _proof == null ? "" : ProofTexts.Caption(_set.Profile, _proof.Intent, _proof.Simulated);
        public string ProfileLine => _set?.Profile == null ? "" : ProofTexts.ProfileLine(_set.Profile);
        public string PaperWhiteLine => _set?.Profile == null ? "" : ProofTexts.PaperWhiteLine(_set.Profile);
        public string PaperBlackLine => _set?.Profile == null ? "" : ProofTexts.PaperBlackLine(_set.Profile);
        public string PaperGamutLegend => _proof == null ? "" : ProofTexts.PaperGamutLegend(_proof.PctPaper);
        public string ScreenGamutLegend => _proof == null ? "" : ProofTexts.ScreenGamutLegend(_proof.PctScreen);

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

        /// <summary>Drops the proof and abandons any computation in progress: its result is nobody's any more.</summary>
        void Reset()
        {
            _run++;
            _set = null; _proof = null;
            _original = _proofImage = _paperLayer = _screenLayer = _bothLayer = null;
            Message = null; IsProblem = false;
            IsBusy = false;
            RaiseProof();
        }

        /// <summary>
        /// What the analysis starts, synchronously: the set prepared and the proof rendered with
        /// the controls as they are (the tests' path).
        /// </summary>
        internal void Compute()
        {
            _run++;
            Show(Prepare(_result, _intent, _simulate));
        }

        /// <summary>
        /// Prepares the set and renders the proof on a pool thread, "computing" shown
        /// meanwhile; the outcome is applied only if no Reset or newer computation came in
        /// between. An exception becomes the message, never a throw.
        /// </summary>
        internal async Task ComputeAsync()
        {
            AnalysisResult result = _result;
            if (result == null) return;
            ProofIntent intent = _intent; bool simulate = _simulate;
            int run = ++_run;
            IsBusy = true; IsProblem = false; Message = ProofTexts.Computing;
            try
            {
                var outcome = await Task.Run(() => Prepare(result, intent, simulate));
                if (run == _run) Show(outcome);
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
        /// Renders the prepared set again on a pool thread, after a change of intent or of the
        /// simulation; a late outcome is dropped like a late computation.
        /// </summary>
        async Task RenderAsync()
        {
            ProofSet set = _set; ProofIntent intent = _intent; bool simulate = _simulate;
            int run = ++_run;
            IsBusy = true; IsProblem = false; Message = ProofTexts.Computing;
            try
            {
                SoftProofResult proof = await Task.Run(() => ProofRun.Render(set, intent, simulate));
                if (run == _run) Show((set, proof));
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

        /// <summary>The core's two steps, on whichever thread.</summary>
        /// <param name="result">The analysis.</param>
        /// <param name="intent">The intent to render.</param>
        /// <param name="simulate">The simulation's state.</param>
        /// <returns>The set, and the proof when the set has no problem.</returns>
        static (ProofSet Set, SoftProofResult Proof) Prepare(AnalysisResult result, ProofIntent intent, bool simulate)
        {
            ProofSet set = ProofRun.Prepare(result);
            return (set, set.Problem == null ? ProofRun.Render(set, intent, simulate) : null);
        }

        /// <summary>Takes a set and its proof: the bitmaps are made here, once per render.</summary>
        /// <param name="outcome">The core's set and proof; a set with a problem empties the tab and shows it in red.</param>
        void Show((ProofSet Set, SoftProofResult Proof) outcome)
        {
            _set = outcome.Set;
            _proof = outcome.Proof;
            if (_set.Problem != null)
            {
                _original = _proofImage = _paperLayer = _screenLayer = _bothLayer = null;
                Message = _set.Problem; IsProblem = true;
                RaiseProof();
                return;
            }
            Raster img = _proof.Image;
            int w = img.W, h = img.H;
            ProofClass[] c = _proof.Classes;
            _original ??= RasterBitmaps.Region(_set.Reduced, 0, 0, w, h);
            _proofImage = RasterBitmaps.Region(img, 0, 0, w, h);
            _paperLayer = RasterBitmaps.Overlay(w, h, (x, y) => (c[y * w + x] & ProofClass.Paper) != 0, PaperColour);
            _screenLayer = RasterBitmaps.Overlay(w, h, (x, y) => (c[y * w + x] & ProofClass.Screen) != 0, ScreenColour);
            _bothLayer = RasterBitmaps.Overlay(w, h, (x, y) => c[y * w + x] == ProofClass.Both, BothColour);
            Message = null; IsProblem = false;
            RaiseProof();
        }

        /// <summary>Raises everything that follows the proof.</summary>
        void RaiseProof()
        {
            Raise(nameof(HasProof)); Raise(nameof(Image)); Raise(nameof(Width)); Raise(nameof(Height));
            Raise(nameof(PaperLayer)); Raise(nameof(ScreenLayer)); Raise(nameof(BothLayer));
            Raise(nameof(Caption)); Raise(nameof(ProfileLine)); Raise(nameof(PaperWhiteLine)); Raise(nameof(PaperBlackLine));
            Raise(nameof(PaperGamutLegend)); Raise(nameof(ScreenGamutLegend));
            Raise(nameof(MarginBrush)); Raise(nameof(PaperWhiteColour));
            RaiseLayers();
            (FullScreen as RelayCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>Raises the three layers' visibilities.</summary>
        void RaiseLayers()
        {
            Raise(nameof(ShowPaperLayer)); Raise(nameof(ShowScreenLayer)); Raise(nameof(ShowBothLayer));
        }

        /// <summary>
        /// The full-screen window's keys (R-163): P, G, M and O toggle the four boxes, I moves
        /// to the next intent; anything else is left alone.
        /// </summary>
        /// <param name="key">The key pressed.</param>
        /// <returns>Whether the key was one of the five.</returns>
        internal bool HandleKey(Key key)
        {
            switch (key)
            {
                case Key.P: SimulatePaper = !SimulatePaper; return true;
                case Key.G: ShowPaperGamut = !ShowPaperGamut; return true;
                case Key.M: ShowScreenGamut = !ShowScreenGamut; return true;
                case Key.O: ShowOriginal = !ShowOriginal; return true;
                case Key.I: Intent = (ProofIntent)(((int)_intent + 1) % 3); return true;
                default: return false;
            }
        }
    }
}
