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
// It computes nothing: the proof, the gamut mask, its share and every sentence come from
// ProofRun (R-160 to R-162). Here are only the state of the controls, the bitmaps made from the
// core's rasters and mask, the brush of the margin and the commands. Like the curves, the
// proof is a run after the analysis: MainViewModel starts it once the other tabs are filled
// (ADR 0051), a change of intent renders again on a pool thread, and the other controls only
// choose what is shown. The proof is always the paper and ink simulated (ADR 0066: the box
// Lightroom has was dropped, nothing in this tool needed its other state). The bitmaps are made
// on the pool thread too, frozen: those of a whole image (R-188; 0.4 s on the 22.6 MP reference
// image) would stall the window for a visible moment. The proofed raster is dropped once its
// bitmap exists: the tab keeps the proof's size and intent alone (Shown).
//
// On request the tab compares the proof with another image's (R-166, ADR 0091): a second file
// goes through the same profile, by the core (ProofRun.PrepareOther, R-165), and is shown beside
// the analysed image across a line. Here are its bitmaps, its name and its legend, which every
// control follows; the line and the zoom are the view's.

using System;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NLog;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>The margin around the proof (R-163): the paper's white, or a fixed background.</summary>
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
        /// One image of the tab, the analysed one or the one it is compared with, as a
        /// computation or a render hands it back from the pool thread: the set, what is left of
        /// the proof once drawn and the frozen bitmaps made from them (null bitmaps with a problem). The original and the
        /// paper layer belong to the set and are made once for it.
        /// </summary>
        /// <param name="Set">The prepared set, with or without a problem.</param>
        /// <param name="Proof">The proof's size and intent; null when the set has a problem.</param>
        /// <param name="Original">The original's bitmap, at the set's step; null with a problem.</param>
        /// <param name="Image">The proof's bitmap; null with a problem.</param>
        /// <param name="Paper">The paper-gamut layer; null with a problem.</param>
        sealed record Side(ProofSet Set, Shown Proof, BitmapSource Original, BitmapSource Image, BitmapSource Paper);

        /// <summary>
        /// What the tab keeps of a proof once its bitmap is made. The proofed raster itself is
        /// not kept: on a whole image it would hold 12 bytes a pixel, 271 MB on 22.6 MP, for
        /// nothing the tab reads (R-188).
        /// </summary>
        /// <param name="W">The proof's width, in pixels.</param>
        /// <param name="H">The proof's height, in pixels.</param>
        /// <param name="Intent">The intent it was rendered with.</param>
        sealed record Shown(int W, int H, ProofIntent Intent);

        /// <summary>What one computation or render hands back: the analysed image and, with a comparison, the other one.</summary>
        /// <param name="Main">The analysed image's side.</param>
        /// <param name="Other">The other image's side, its set with a problem when the core refused it; null without a comparison.</param>
        sealed record Outcome(Side Main, Side Other);

        AnalysisResult _result;
        ProofSet _set;
        Shown _proof;
        BitmapSource _original, _proofImage, _paperLayer;
        int _run;
        bool _busy, _isProblem;
        string _message;

        // The comparison (R-166): the other image as it is shown, null without one; the file it
        // came from, kept through an analysis run again so that it is proofed again; and the
        // analysed image that file was chosen for, since another image ends the comparison.
        Side _other;
        string _otherPath, _otherFor;

        // A file is being proofed for the comparison: a change of intent meanwhile must not
        // start a render of its own, which would supersede it; the comparison renders it after.
        bool _comparePending;

        /// <summary>
        /// The most pixels of an image the core takes whole, handed to ProofRun: the core's own
        /// limit (R-188) unless a test lowers it to reach a reduced copy with a small frame.
        /// </summary>
        internal int MaxPixels { get; init; } = Thresholds.WholeImageMaxPixels;

        ProofIntent _intent = ProofIntent.Perceptual;
        bool _showPaper, _showOriginal;
        ProofMarginChoice _margin = ProofMarginChoice.PaperWhite;

        /// <summary>
        /// The diagnostic log: the controls the user changes, a computation or a render that
        /// failed or was superseded; the core's ProofRun writes each preparation and render
        /// (R-171, ADR 0069).
        /// </summary>
        static readonly Logger Log = LogManager.GetLogger(Loggers.SoftProof);

        /// <summary>Makes the commands; the tab starts empty.</summary>
        public SoftProofTabViewModel()
        {
            FullScreen = new RelayCommand(() => ProofRequested?.Invoke(this), () => HasProof);
            StopComparing = new RelayCommand(StopCompare, () => IsComparing && !IsBusy);
        }

        /// <summary>Opens the full-screen window over this same view-model (R-163).</summary>
        public ICommand FullScreen { get; }

        /// <summary>"Opreste comparatia" (R-166): back to the single image; only while a comparison is shown and nothing is computed.</summary>
        public ICommand StopComparing { get; }

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
                Log.Info("intent=" + value);
                Raise(nameof(IsPerceptual)); Raise(nameof(IsRelative)); Raise(nameof(IsRelativeNoBpc));
                if (HasControls && !_comparePending) ProofComputation = RenderAsync();
            }
        }

        public bool ShowPaperGamut { get => _showPaper; set { if (Set(ref _showPaper, value)) { Log.Info("paper gamut warning=" + LogText.Yes(value)); RaiseLayers(); } } }

        /// <summary>
        /// "Originalul": the image as it is, inside the same margin, without the warnings; in a
        /// comparison, both images.
        /// </summary>
        public bool ShowOriginal
        {
            get => _showOriginal;
            set
            {
                if (!Set(ref _showOriginal, value)) return;
                Log.Info("original=" + LogText.Yes(value));
                Raise(nameof(Image)); Raise(nameof(OtherImage));
                RaiseLayers();
            }
        }

        public ProofMarginChoice Margin { get => _margin; set { if (Set(ref _margin, value)) { Log.Info("margin=" + value); Raise(nameof(MarginBrush)); } } }

        // ---- what is shown

        /// <summary>The proof, or the original when <see cref="ShowOriginal"/> is on; null before a proof.</summary>
        public BitmapSource Image => _showOriginal ? _original : _proofImage;
        public int Width => _proof?.W ?? 0;
        public int Height => _proof?.H ?? 0;

        /// <summary>The pixels outside the paper's gamut, in red (R-161).</summary>
        public BitmapSource PaperLayer => _paperLayer;

        public bool ShowPaperLayer => HasProof && !_showOriginal && _showPaper;

        // ---- the comparison with another image (R-166)

        /// <summary>
        /// Another image is shown beside the analysed one, across the line (R-166): its proof is
        /// there and so is the analysed image's.
        /// </summary>
        public bool IsComparing => HasProof && _other?.Image != null;

        /// <summary>
        /// A file can be chosen for the comparison: a proof is shown and nothing is being
        /// computed; the two buttons that open the file dialog follow it.
        /// </summary>
        public bool CanCompare => HasProof && !IsBusy;

        /// <summary>The other image's proof, or the image as it is when <see cref="ShowOriginal"/> is on; null without a comparison.</summary>
        public BitmapSource OtherImage => !IsComparing ? null : _showOriginal ? _other.Original : _other.Image;

        /// <summary>The red layer over the analysed image while its box is on, null otherwise: one property for the comparison's left side.</summary>
        public BitmapSource PaperOverlay => ShowPaperLayer ? _paperLayer : null;

        /// <summary>The red layer over the other image, by its own pixels, while the box is on; null otherwise.</summary>
        public BitmapSource OtherPaperOverlay => IsComparing && ShowPaperLayer ? _other.Paper : null;

        /// <summary>The analysed image's file name: the label left of the line.</summary>
        public string ImageName => _result == null ? "" : System.IO.Path.GetFileName(_result.Path);

        /// <summary>The other image's file name: the label right of the line; empty without a comparison.</summary>
        public string OtherName => IsComparing ? System.IO.Path.GetFileName(_otherPath) : "";

        /// <summary>The other image's legend, with its file name and its own share (R-166); empty without a comparison.</summary>
        public string OtherPaperGamutLegend => IsComparing ? ProofTexts.PaperGamutLegend(OtherName, _other.Set.PctPaper) : "";

        /// <summary>The core's sentence saying which side of the line is which image; empty without a comparison.</summary>
        public string CompareSides => IsComparing ? ProofTexts.CompareSides(ImageName, OtherName) : "";

        /// <summary>The analysed image's folder, where the dialog that chooses the other file starts; null without a file on disk.</summary>
        public string InputFolder => _result?.FullPath == null || !System.IO.File.Exists(_result.FullPath) ? null : System.IO.Path.GetDirectoryName(_result.FullPath);

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
        public bool IsBusy
        {
            get => _busy;
            private set
            {
                if (!Set(ref _busy, value)) return;
                Raise(nameof(CanCompare));
                RaiseCommands();
            }
        }

        /// <summary>
        /// Why there is no proof (no profile, an unusable one or table), "computing", an error,
        /// or why the file chosen for the comparison was refused, the proof staying (R-166); null
        /// otherwise.
        /// </summary>
        public string Message { get => _message; private set => Set(ref _message, value); }

        /// <summary>The message is a reason of the core's (R-162, R-165): the window shows it in red.</summary>
        public bool IsProblem { get => _isProblem; private set => Set(ref _isProblem, value); }

        /// <summary>The window's title: the image's name.</summary>
        public string Title => _result == null ? "Soft proof" : $"Soft proof - {System.IO.Path.GetFileName(_result.Path)}";

        public string Caption => _proof == null ? "" : ProofTexts.Caption(_set.Profile, _proof.Intent);
        public string ProfileLine => _set?.Profile == null ? "" : ProofTexts.ProfileLine(_set.Profile);
        public string PaperWhiteLine => _set?.Profile == null ? "" : ProofTexts.PaperWhiteLine(_set.Profile);
        public string PaperBlackLine => _set?.Profile == null ? "" : ProofTexts.PaperBlackLine(_set.Profile);

        /// <summary>The legend with the share of the analysed image; in a comparison it starts with the image's file name (R-166).</summary>
        public string PaperGamutLegend
            => !HasControls ? "" : IsComparing ? ProofTexts.PaperGamutLegend(ImageName, _set.PctPaper) : ProofTexts.PaperGamutLegend(_set.PctPaper);

        /// <summary>
        /// The image shown is a reduced copy: the image is over the core's limit for the whole
        /// image (R-188). False for the whole image, and without a proof.
        /// </summary>
        public bool IsReducedCopy => _set != null && _set.Stride > 1;

        /// <summary>The sentence under the tab's image: what is shown and how to zoom and move it (R-163, R-188).</summary>
        public string ViewerHelp => ViewerHelpFor(IsReducedCopy);

        /// <summary>The sentence of the full-screen window's panel: its keys, the zoom, what is shown (R-163, R-164, R-188).</summary>
        public string WindowHelp => WindowHelpFor(IsReducedCopy);

        /// <summary>
        /// How the zoom is explained, the maximum being the viewer's own: it enlarges a pixel of
        /// the image, or of the reduced copy when that is what is shown.
        /// </summary>
        /// <param name="reduced">Whether the image shown is a reduced copy.</param>
        /// <returns>The sentences, in the interface language.</returns>
        static string ZoomHelp(bool reduced)
        {
            string max = Fmt.F0(ZoomPanViewer.MaxScale);
            string ofEn = reduced ? "copy" : "image", ofRo = reduced ? "copiei" : "imaginii";
            return Tr.Pick(
                $"The mouse wheel zooms around the cursor, up to {max}:1 (one pixel of the {ofEn} becomes a square of {max} x {max} screen pixels, with Windows display scaling at 100 %); hold the left button and drag to move the image; a double click fits it again.",
                $"Rotita mouse-ului mareste in jurul cursorului, pana la {max}:1 (un pixel al {ofRo} devine un patrat de {max} x {max} pixeli pe ecran, cu scalarea Windows la 100 %); tii apasat butonul stang si tragi ca sa muti imaginea; dublu-clic o potriveste din nou.");
        }

        /// <summary>
        /// The tab's sentence for a whole image or for a reduced copy of one over the core's
        /// limit (R-188).
        /// </summary>
        /// <param name="reduced">Whether the image shown is a reduced copy.</param>
        /// <returns>The sentence, in the interface language.</returns>
        internal static string ViewerHelpFor(bool reduced)
            => (reduced
                    ? Tr.Pick("A reduced copy of the image, put through the paper profile, for viewing only;", "E o copie micsorata a imaginii, trecuta prin profilul hartiei, doar pentru privit;")
                    : Tr.Pick("The image, put through the paper profile, for viewing only;", "E imaginea trecuta prin profilul hartiei, doar pentru privit;"))
                + Tr.Pick(
                    " the program assumes the pixels and the screen are sRGB. The white and the black of the paper show as they are, like a printed photo laid beside a white sheet. The third mode, without black point compensation, shows the shadows the way the Tones tab judges them. ",
                    " programul presupune ca pixelii si ecranul sunt sRGB. Albul si negrul hartiei se vad asa cum sunt, ca o poza tiparita pusa langa o foaie alba. Al treilea mod, fara compensarea negrului, arata umbrele asa cum le judeca tabul Tonuri. ")
                + ZoomHelp(reduced);

        /// <summary>
        /// The full-screen window's sentence for a whole image or for a reduced copy of one
        /// over the core's limit (R-188).
        /// </summary>
        /// <param name="reduced">Whether the image shown is a reduced copy.</param>
        /// <returns>The sentence, in the interface language.</returns>
        internal static string WindowHelpFor(bool reduced)
            => Tr.Pick(
                    "Keys: G shows the colours the paper cannot reproduce, O shows the original, I moves to the next rendering mode; Esc or Close leaves the full screen. This panel shows while the mouse is over it. ",
                    "Taste: G arata culorile pe care hartia nu le poate reda, O arata originalul, I trece la urmatorul mod de redare; Esc sau Inchide iese din ecranul intreg. Panoul acesta se vede cat timp mouse-ul e deasupra lui. ")
                + ZoomHelp(reduced)
                + (reduced
                    ? Tr.Pick(" A reduced copy; the program assumes the pixels and the screen are sRGB.", " E o copie micsorata; programul presupune ca pixelii si ecranul sunt sRGB.")
                    : Tr.Pick(" The program assumes the pixels and the screen are sRGB.", " Programul presupune ca pixelii si ecranul sunt sRGB."));

        public string PerceptualName => ProofTexts.IntentName(ProofIntent.Perceptual);
        public string RelativeName => ProofTexts.IntentName(ProofIntent.Relative);
        public string RelativeNoBpcName => ProofTexts.IntentName(ProofIntent.RelativeNoBpc);
        public string PerceptualHelp => ProofTexts.IntentHelp(ProofIntent.Perceptual);
        public string RelativeHelp => ProofTexts.IntentHelp(ProofIntent.Relative);
        public string RelativeNoBpcHelp => ProofTexts.IntentHelp(ProofIntent.RelativeNoBpc);

        /// <summary>
        /// Keeps the result; unlike most tabs it fills nothing itself: MainViewModel starts the
        /// computation after every tab is filled (ADR 0051). Any computation in progress is
        /// abandoned. The controls keep their state (R-163). The file of a comparison is kept
        /// when the analysis is of the image it was chosen for, to be proofed again with it, and
        /// forgotten for any other image (R-166).
        /// </summary>
        /// <param name="r">The analysis; not null.</param>
        internal override void Populate(AnalysisResult r)
        {
            Reset();
            if (_otherPath != null && !string.Equals(r.Path, _otherFor, StringComparison.OrdinalIgnoreCase))
            {
                Log.Info("comparison ended: another image was opened");
                _otherPath = _otherFor = null;
            }
            _result = r;
            HasResult = true;
            Raise(nameof(Title)); Raise(nameof(ImageName));
        }

        /// <summary>
        /// Empties the tab. The file of a comparison is not forgotten here: this runs before
        /// every analysis, the same image's too, and <see cref="Populate"/> decides.
        /// </summary>
        internal override void Clear()
        {
            base.Clear();
            Reset();
            _result = null;
            Raise(nameof(Title)); Raise(nameof(ImageName));
        }

        /// <summary>
        /// Drops the set, the proof and the other image's and abandons any computation in
        /// progress: its result is nobody's any more.
        /// </summary>
        void Reset()
        {
            _run++;
            _set = null;
            _other = null;
            _comparePending = false;
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
            Show(Prepare(_result, _intent, _otherPath, MaxPixels));
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
            string otherPath = _otherPath;
            int run = ++_run;
            IsBusy = true; IsProblem = false; Message = ProofTexts.Computing;
            try
            {
                Outcome outcome = await Task.Run(() => Prepare(result, intent, otherPath, MaxPixels));
                if (run != _run)
                {
                    Log.Debug("computation " + LogText.Int(run) + " ended after a newer one started; nothing is shown");
                    return;
                }
                Show(outcome);
                IsBusy = false;
                // The intent changed while the table was built: the proof shown is the old
                // one's, so render the new one now rather than wait for the next change. A
                // comparison that ended at this run keeps its reason through that render.
                if (HasControls && _intent != intent) await RenderAsync(Message, IsProblem);
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
        /// Renders the prepared set again on a pool thread, after a change of intent, and the
        /// other image's with it in a comparison; a late outcome is dropped like a late
        /// computation. A table the core
        /// refuses (its Problem) leaves the set, so another intent can still be chosen, and shows
        /// the reason in red with no image.
        /// </summary>
        /// <param name="keepMessage">
        /// A message shown before this render that must still be there after it, since the render
        /// was not the user's answer to it: the reason a file of the comparison was refused, when
        /// the intent had changed meanwhile (R-166); null for none.
        /// </param>
        /// <param name="keepProblem">Whether <paramref name="keepMessage"/> is a reason of the core's, shown in red.</param>
        async Task RenderAsync(string keepMessage = null, bool keepProblem = false)
        {
            ProofSet set = _set; ProofIntent intent = _intent;
            BitmapSource original = _original, paper = _paperLayer;   // read here, on the window's thread, as the set is
            Side other = _other;
            int run = ++_run;
            IsBusy = true; IsProblem = false; Message = ProofTexts.Computing;
            try
            {
                Outcome outcome = await Task.Run(() => new Outcome(
                    Render(set, intent, original, paper),
                    other == null ? null : Render(other.Set, intent, other.Original, other.Paper)));
                if (run != _run) return;
                Show(outcome);
                if (keepMessage != null)
                {
                    Message = keepMessage; IsProblem = keepProblem;
                }
            }
            catch (Exception e)
            {
                if (run != _run)
                {
                    Log.Debug(e, "render " + LogText.Int(run) + " failed after a newer one started; nothing is shown");
                    return;
                }
                // An ArgumentException is a table the core refuses, shown as the reason; any other
                // is a failure.
                if (e is ArgumentException) Log.Warn(e, "render " + LogText.Int(run) + " refused with intent " + intent + ": " + e.Message);
                else Log.Error(e, "render " + LogText.Int(run) + " failed; the tab shows the message");
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

        /// <summary>
        /// The core's steps and the bitmaps, on whichever thread; a new set, so the original is
        /// made too. With a file to compare with, that file is prepared from the new set and
        /// rendered with it (R-165).
        /// </summary>
        /// <param name="result">The analysis.</param>
        /// <param name="intent">The intent to render.</param>
        /// <param name="otherPath">The file of the comparison to proof again; null without one.</param>
        /// <param name="maxPixels">The most pixels the core takes whole (<see cref="MaxPixels"/>).</param>
        /// <returns>The outcome; without a proof and bitmaps when the set has a problem, and then without the other image.</returns>
        static Outcome Prepare(AnalysisResult result, ProofIntent intent, string otherPath, int maxPixels)
        {
            ProofSet set = ProofRun.Prepare(result, maxPixels);
            if (set.Problem != null) return new Outcome(new Side(set, null, null, null, null), null);
            return new Outcome(Render(set, intent, null, null), otherPath == null ? null : PrepareOther(set, otherPath, intent));
        }

        /// <summary>The core's preparation of the file to compare with and its render, with the bitmaps, on whichever thread (R-165).</summary>
        /// <param name="first">The analysed image's set, without a problem.</param>
        /// <param name="path">The other image's file.</param>
        /// <param name="intent">The intent to render.</param>
        /// <returns>The other image's side; its set alone, with the problem, when the core refused the file.</returns>
        static Side PrepareOther(ProofSet first, string path, ProofIntent intent)
        {
            ProofSet set = ProofRun.PrepareOther(first, path);
            return set.Problem == null ? Render(set, intent, null, null) : new Side(set, null, null, null, null);
        }

        /// <summary>
        /// The core's render, the paper and ink simulated, and the frozen bitmaps of its result,
        /// on whichever thread.
        /// </summary>
        /// <param name="set">A set without a problem.</param>
        /// <param name="intent">The intent to render; one the core refuses throws its reason.</param>
        /// <param name="original">The original's bitmap when already made for this set; null to make it.</param>
        /// <param name="paper">The paper layer when already made for this set; null to make it.</param>
        /// <returns>The image's side, bitmaps included.</returns>
        static Side Render(ProofSet set, ProofIntent intent, BitmapSource original, BitmapSource paper)
        {
            SoftProofResult proof = ProofRun.Render(set, intent, simulate: true);
            Raster img = proof.Image;
            int w = img.W, h = img.H;
            bool[] outside = set.PaperOutside;
            return new Side(set, new Shown(w, h, proof.Intent),
                original ?? RasterBitmaps.Region(set.Reduced, 0, 0, w, h),
                RasterBitmaps.Region(img, 0, 0, w, h),
                paper ?? RasterBitmaps.Overlay(w, h, (x, y) => outside[y * w + x], PaperColour));
        }

        /// <summary>
        /// Takes a set and its proof with their bitmaps; a set with a problem empties the tab and
        /// shows it in red. The other image follows: taken as rendered; gone when the outcome
        /// has none; and when the core refused its file at this run, the comparison ends, the
        /// file is forgotten and the reason is shown in red beside the proof (R-166).
        /// </summary>
        /// <param name="o">The outcome; a set with a problem carries no proof.</param>
        void Show(Outcome o)
        {
            _set = o.Main.Set;
            _proof = o.Main.Proof;
            if (_set.Problem != null)
            {
                _original = _proofImage = _paperLayer = null;
                _other = null;
                Message = _set.Problem; IsProblem = true;
                RaiseProof();
                return;
            }
            _original = o.Main.Original;
            _proofImage = o.Main.Image;
            _paperLayer = o.Main.Paper;
            Message = null; IsProblem = false;
            if (o.Other?.Set.Problem != null)
            {
                Log.Warn("comparison ended: " + _otherPath + " refused: " + o.Other.Set.Problem);
                _other = null;
                _otherPath = _otherFor = null;
                Message = o.Other.Set.Problem; IsProblem = true;
            }
            else _other = o.Other;
            RaiseProof();
        }

        // ---- the comparison's commands (R-166)

        /// <summary>
        /// Proofs another image file through the same profile, with the intent shown, on a pool
        /// thread, "computing" shown meanwhile, and shows it beside the analysed image across the
        /// line; a file the core refuses (R-165) leaves what was shown, the single image or the
        /// earlier comparison, with the reason in red. Nothing happens unless
        /// <see cref="CanCompare"/>. The task is also <see cref="ProofComputation"/>.
        /// </summary>
        /// <param name="path">The other image's file.</param>
        /// <returns>The computation; completed at once when nothing was started.</returns>
        internal Task CompareAsync(string path) => CanCompare ? ProofComputation = CompareCore(path) : Task.CompletedTask;

        /// <summary>The body of <see cref="CompareAsync"/>.</summary>
        /// <param name="path">The other image's file.</param>
        async Task CompareCore(string path)
        {
            ProofSet set = _set; ProofIntent intent = _intent;
            int run = ++_run;
            Log.Info("compare with " + path);
            _comparePending = true;
            IsBusy = true; IsProblem = false; Message = ProofTexts.Computing;
            try
            {
                Side other = await Task.Run(() => PrepareOther(set, path, intent));
                if (run != _run)
                {
                    Log.Debug("comparison " + LogText.Int(run) + " ended after a newer computation started; nothing is shown");
                    return;
                }
                TakeOther(other, path);
            }
            catch (Exception e)
            {
                if (run == _run)
                {
                    Log.Error(e, "comparison " + LogText.Int(run) + " failed; the tab shows the message");
                    Message = MainViewModel.Describe(e); IsProblem = false;
                }
                else Log.Debug(e, "comparison " + LogText.Int(run) + " failed after a newer computation started; nothing is shown");
            }
            finally
            {
                if (run == _run) { _comparePending = false; IsBusy = false; }
            }
            // The intent changed while the file was proofed, when no render of its own could
            // start: what is shown, one image or two, is rendered now, and the reason of a
            // refusal or a failure stays through that render.
            if (run == _run && HasControls && _intent != intent) await RenderAsync(Message, IsProblem);
        }

        /// <summary>The synchronous body of <see cref="CompareAsync"/>, for the tests, as <see cref="Compute"/> is.</summary>
        /// <param name="path">The other image's file.</param>
        internal void Compare(string path)
        {
            if (!CanCompare) return;
            _run++;
            Log.Info("compare with " + path);
            TakeOther(PrepareOther(_set, path, _intent), path);
        }

        /// <summary>
        /// Takes the other image as the comparison's right side, for the analysed image shown;
        /// one the core refused changes nothing but the message, its reason in red.
        /// </summary>
        /// <param name="other">The other image's side, its set with a problem when refused.</param>
        /// <param name="path">The file it came from.</param>
        void TakeOther(Side other, string path)
        {
            if (other.Set.Problem != null)
            {
                Log.Warn("compare with " + path + " refused: " + other.Set.Problem);
                Message = other.Set.Problem; IsProblem = true;
                return;
            }
            _other = other;
            _otherPath = path;
            _otherFor = _result?.Path;
            Message = null; IsProblem = false;
            RaiseProof();
        }

        /// <summary>
        /// Forgets the file of a comparison, shown or kept for the next run, when the analysis
        /// of another image failed, or when another image was chosen and a bad field stopped its
        /// run before it began: the main view-model calls it then, since the tab is only
        /// emptied in those cases and never populated, so that an image that could not be
        /// analysed ends the comparison as one that could does (R-166), and the first image
        /// opened again comes back alone. A failed run of the image the file was chosen for
        /// forgets nothing: the next run of that image that succeeds brings the comparison back.
        /// </summary>
        /// <param name="otherImagePath">The path of the image opened instead of the one compared.</param>
        /// <param name="analysed">
        /// Whether that image's analysis ran and failed; false when the image was only chosen. For
        /// the log line alone.
        /// </param>
        internal void EndComparison(string otherImagePath, bool analysed = true)
        {
            if (_otherPath == null || string.Equals(otherImagePath, _otherFor, StringComparison.OrdinalIgnoreCase)) return;
            Log.Info("comparison ended: another image was opened, and " + (analysed ? "its analysis failed" : "it is not analysed yet"));
            _other = null;
            _otherPath = _otherFor = null;
            RaiseProof();
        }

        /// <summary>"Opreste comparatia": forgets the other image; the analysed image's proof stays as it is.</summary>
        void StopCompare()
        {
            if (!IsComparing || IsBusy) return;
            Log.Info("comparison stopped");
            _other = null;
            _otherPath = _otherFor = null;
            Message = null; IsProblem = false;
            RaiseProof();
        }

        /// <summary>Raises everything that follows the set, the proof and the other image.</summary>
        void RaiseProof()
        {
            Raise(nameof(HasControls)); Raise(nameof(HasProof)); Raise(nameof(Image)); Raise(nameof(Width)); Raise(nameof(Height));
            Raise(nameof(PaperLayer));
            Raise(nameof(Caption)); Raise(nameof(ProfileLine)); Raise(nameof(PaperWhiteLine)); Raise(nameof(PaperBlackLine));
            Raise(nameof(PaperGamutLegend));
            Raise(nameof(IsReducedCopy)); Raise(nameof(ViewerHelp)); Raise(nameof(WindowHelp));
            Raise(nameof(MarginBrush)); Raise(nameof(PaperWhiteColour));
            Raise(nameof(IsComparing)); Raise(nameof(CanCompare)); Raise(nameof(OtherImage)); Raise(nameof(OtherName));
            Raise(nameof(OtherPaperGamutLegend)); Raise(nameof(CompareSides));
            RaiseLayers();
            RaiseCommands();
        }

        /// <summary>Raises the layer's visibility and the two sides' layers.</summary>
        void RaiseLayers()
        {
            Raise(nameof(ShowPaperLayer)); Raise(nameof(PaperOverlay)); Raise(nameof(OtherPaperOverlay));
        }

        /// <summary>Asks the two commands again whether they can run.</summary>
        void RaiseCommands()
        {
            (FullScreen as RelayCommand)?.RaiseCanExecuteChanged();
            (StopComparing as RelayCommand)?.RaiseCanExecuteChanged();
        }

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
