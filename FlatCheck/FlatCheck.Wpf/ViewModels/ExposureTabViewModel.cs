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

// ExposureTabViewModel.cs - tab 4, the exposure check of a raw flat.
//
// The list comes from flats/ and holds ONLY camera raws: the mode needs the CFA mosaic intact and
// the original ADU scale, and on a raw the white and the black are read exactly from the file
// instead of being guessed from the maximum.
//
// It recomputes both on a change of file and on toggling "Sky", because the two bands differ:
// without --sky a sky flat at 22 % would be reported wrongly as underexposed. A sky flat MUST stay
// modest - the sky fades continuously and is full of faint stars, so the master's SNR comes from
// the number of frames, not from pushing a frame to 55 %.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static FlatCheck.Ui;

namespace FlatCheck.Wpf
{
    /// <summary>The Expunere tab: one raw flat from flats/ at a time, its exposure figures,
    /// verdict and advice, and the per-channel histograms of the raw data or of the camera's
    /// preview.</summary>
    public sealed class ExposureTabViewModel : TabViewModel
    {
        SirilSession _session;
        CancellationToken _ct;
        HeavyRunGate _gate;

        string _selected;
        bool _sky;
        string _scale = "";
        string _centre = "";
        string _global = "";
        string _clipping = "";
        string _channels = "";
        string _channelOrder = "";
        IReadOnlyList<ChannelHistogramRow> _histograms = System.Array.Empty<ChannelHistogramRow>();
        IReadOnlyList<ScreenPairRow> _comparison = System.Array.Empty<ScreenPairRow>();
        string _reading = "";
        string _sourceNote = "";
        string _caption = "";

        /// <summary>
        /// The last result, kept so the rows can be rebuilt on a change of source without
        /// re-reading a 24 MP NEF for a drawing. Null before the first run and after Reset.
        /// </summary>
        ExposureResult _last;

        /// <summary>An empty tab, listening to its histogram-source switch.</summary>
        public ExposureTabViewModel()
        {
            Source.Changed += RebuildRows;
        }
        string _verdict = "";
        string _advice = "";
        bool _isRaw = true;

        /// <summary>The raws of flats/, with the full path; only the name is shown.</summary>
        public ObservableCollection<string> Files { get; } = new();

        /// <summary>
        /// One histogram row per channel: four on CFA, one on mono.
        ///
        /// The list is REPLACED whole on every run, not cleared and refilled, so an
        /// ObservableCollection has no business here. The reason is concrete: a second choice (or
        /// the Sky switch) can start a run over the first's, and the gate orders their writes
        /// (ADR 0077). On plain properties the last one wins and that is that; a collection
        /// cleared and refilled by two runs ends up with duplicated rows and nulls in it. The
        /// end-to-end series really does need an ObservableCollection, because it appends its rows
        /// as they come - not here.
        /// </summary>
        public IReadOnlyList<ChannelHistogramRow> Histograms
        {
            get => _histograms;
            private set => Set(ref _histograms, value);
        }

        /// <summary>The histograms' reading guide. Documentation, not a diagnosis.</summary>
        public HistogramGuideViewModel HistogramGuide { get; } = new();

        /// <summary>
        /// The switch between the raw data and the camera's preview. Off when the file has no
        /// preview; then it stays on the raw data.
        /// </summary>
        public HistogramSourceViewModel Source { get; } = new();

        /// <summary>The raw -> screen table, centre against centre, per channel. Empty without a
        /// preview.</summary>
        public IReadOnlyList<ScreenPairRow> Comparison
        {
            get => _comparison;
            private set => Set(ref _comparison, value);
        }

        /// <summary>
        /// Under the table: why the two differ when the preview is shown, why it is missing when
        /// there is none, and nothing when the raw data are shown and the preview is available.
        /// </summary>
        public string SourceNote { get => _sourceNote; private set => Set(ref _sourceNote, value); }

        /// <summary>The drawings' caption. It changes with the source: on the preview there is no
        /// target band.</summary>
        public string Caption { get => _caption; private set => Set(ref _caption, value); }

        public bool HasComparison => _comparison.Count > 0;

        public string Selected
        {
            get => _selected;
            set { if (Set(ref _selected, value)) Recompute(); }
        }

        /// <summary>The window's --sky (R-043): the switch changes the bands, so it
        /// recomputes; it is the folder's, so a change writes its settings (R-119).</summary>
        public bool Sky
        {
            get => _sky;
            set
            {
                if (!Set(ref _sky, value)) return;
                _settings.SkyFlat = value;
                _settings.Write();
                Recompute();
            }
        }

        /// <summary>The folder's remembered switches (R-119); <see cref="SessionSettings.None"/>
        /// before a folder and in the tests.</summary>
        SessionSettings _settings = SessionSettings.None;

        public string Scale { get => _scale; private set => Set(ref _scale, value); }
        public string Centre { get => _centre; private set => Set(ref _centre, value); }
        public string Global { get => _global; private set => Set(ref _global, value); }
        public string Clipping { get => _clipping; private set => Set(ref _clipping, value); }
        public string Channels { get => _channels; private set => Set(ref _channels, value); }

        /// <summary>
        /// How we know (or do not know) which CFA position is R, G or B. It sits in the view-model
        /// and not in the XAML because it has two variants, and which of them appears is itself
        /// information about the file.
        /// </summary>
        public string ChannelOrder { get => _channelOrder; private set => Set(ref _channelOrder, value); }

        /// <summary>How this file's exposure reads - the text under the histograms.</summary>
        public string Reading { get => _reading; private set => Set(ref _reading, value); }

        public string Verdict { get => _verdict; private set => Set(ref _verdict, value); }
        public string Advice { get => _advice; private set => Set(ref _advice, value); }

        /// <summary>
        /// False when the sanity gate cut the verdict: the file is already calibrated or
        /// debayered. It is not a bad verdict, it is another file than the mode asks for.
        /// </summary>
        public bool IsRawFlat { get => _isRaw; private set => Set(ref _isRaw, value); }

        /// <summary>Lists the raws of the session's flats/, none chosen, and explains when there
        /// is none; it computes nothing until a file is chosen.</summary>
        /// <param name="session">The folder just opened.</param>
        /// <param name="ct">The folder's token, which every run observes.</param>
        /// <param name="gate">The window's heavy-run gate, taken around a measurement; null runs
        /// at once.</param>
        /// <param name="settings">The folder's remembered switches (R-119), whose "sky flat" the
        /// box takes before any file is chosen; null for none remembered.</param>
        internal void Attach(SirilSession session, CancellationToken ct, HeavyRunGate gate = null, SessionSettings settings = null)
        {
            _session = session;
            _ct = ct;
            _gate = gate;

            // The box takes the folder's value without writing it back and without a run: no
            // file is chosen yet, so Recompute would do nothing anyway.
            _settings = settings ?? SessionSettings.None;
            _sky = _settings.SkyFlat;
            Raise(nameof(Sky));

            Files.Clear();
            foreach (string p in session.RawFlats()) Files.Add(p);

            _selected = null;
            Raise(nameof(Selected));

            if (Files.Count == 0)
                Refuse(T("No raw flat to check", "Niciun flat brut de verificat"), SessionMessages.NoRawFlats(session),
                       "no raw flat to check in " + session.FlatsDir);
            else
                ClearError();
        }

        /// <summary>The last run started, so a test can await one it did not start.</summary>
        // A test that starts a second run beside the setter's has the two compute at once, and
        // the first one's figures can land last (ADR 0077): the tests await this one instead.
        internal Task Pending { get; private set; } = Task.CompletedTask;

        /// <summary>Starts a run on the chosen file and keeps it in <see cref="Pending"/>;
        /// nothing without a session or a file.</summary>
        void Recompute()
        {
            if (_session == null || string.IsNullOrEmpty(Selected)) return;
            Pending = RecomputeAsync();
        }

        /// <summary>Measures the chosen file's exposure, guarded, after the heavy-run gate, with
        /// the preview decoded, and shows the result and the console's report.</summary>
        /// <returns>A task that completes when the run ended; it never faults.</returns>
        Task RecomputeAsync()
        {
            string path = Selected;
            bool sky = Sky;
            if (string.IsNullOrEmpty(path)) return Task.CompletedTask;

            HeavyRunGate gate = _gate;
            CancellationToken ct = _ct;

            return RunGuardedAsync(T($"Measuring the exposure of {Path.GetFileName(path)}...", $"Masor expunerea pe {Path.GetFileName(path)}..."), async () =>
            {
                // A 24 MP raw and its preview do not decode beside another heavy run: the pick
                // waits for the gate, and the waiting screen says so meanwhile (ADR 0061).
                using IDisposable lease = await EnterGateAsync(gate, ct).ConfigureAwait(true);
                ct.ThrowIfCancellationRequested();

                var text = new StringWriter();
                var outcome = await Task.Run(() =>
                {
                    RunReport.WriteBanner(text);
                    // withPreview: only the window shows the preview, so only it decodes it. The
                    // console and the other modes do not pay for it.
                    return ExposureRun.Execute(path, null, null, sky, text, withPreview: true);
                }, ct).ConfigureAwait(true);

                Report = text.ToString();
                Populate(outcome.Result);
                HasResult = true;
            });
        }

        /// <summary>
        /// Internal, not private, so a test can bring a result with a preview without a real raw:
        /// it builds it with ExposureAnalyzer.Analyze on a JPEG made in the test and hands it in
        /// here.
        /// </summary>
        /// <param name="r">The analyzer's result; it is kept for a later change of
        /// source.</param>
        internal void Populate(ExposureResult r)
        {
            _last = r;
            Scale = $"white {r.White:F0}   black {r.Black:F0}   ({r.ScaleSource})";
            Centre = $"{r.CentreMedianPct:F1} %";
            Global = $"{r.GlobalMedianPct:F1} %";
            Clipping = $"{r.CentreClipPct:F3} %";
            Channels = ExposureReport.ChannelsSummary(r);
            ChannelOrder = ExposureReport.ChannelOrderNote(r);
            Reading = ExposureReport.HistogramReading(r);
            IsRawFlat = r.IsRaw;
            Verdict = ExposureReport.VerdictWord(r);
            Advice = ExposureReport.Advice(r);

            Comparison = ExposureReport.ScreenComparison(r)
                .Select(c => new ScreenPairRow
                {
                    Channel = c.Channel,
                    Colour = ChannelPalette.BrushFor(c.Colour),
                    Raw = $"{c.RawPct:F1} %",
                    Screen = $"{c.ScreenPct:F1} %",
                    Gap = ExposureReport.ScreenGap(c.RawPct, c.ScreenPct),
                }).ToList();
            Raise(nameof(HasComparison));

            // Turning it off forces the choice back to the raw data if it was on the preview, and
            // then Changed rebuilds the rows; otherwise we rebuild them ourselves.
            Source.IsEnabled = r.Preview != null;
            RebuildRows();

            // A populated tab HAS a result, whoever populated it. Without this, the panel stays
            // collapsed when a test brings the result directly, and its bindings are not
            // evaluated.
            HasResult = true;
        }

        /// <summary>
        /// The chosen source's rows, from the kept result. It tolerates its absence: before the
        /// first run and after Reset there is nothing to rebuild.
        /// </summary>
        void RebuildRows()
        {
            ExposureResult r = _last;
            if (r == null) return;

            bool preview = Source.IsPreviewSelected && r.Preview != null;
            Histograms = preview ? PreviewRows(r) : RawRows(r);
            Caption = ExposureReport.HistogramCaption(preview);
            SourceNote = r.Preview == null
                ? ExposureReport.NoPreviewReason(r)
                : preview ? ExposureReport.ScreenReading(r) : "";
        }

        /// <summary>
        /// One drawing row per channel. The normalisation and the formatting are of the same kind
        /// as "44.4 %": they prepare a figure so it can be shown, they decide nothing about it.
        /// The thresholds come already chosen in r.Bands, so the band drawn IS the band that
        /// voted.
        /// </summary>
        /// <param name="r">The analyzer's result.</param>
        /// <returns>One row per channel of the raw data, with the target band.</returns>
        static IReadOnlyList<ChannelHistogramRow> RawRows(ExposureResult r)
        {
            ExposureBands b = r.Bands;
            string bandLabel = ExposureReport.BandLabel(b);

            return r.Channels.Select(c => new ChannelHistogramRow
            {
                Label = c.Label,
                Position = r.IsCfa && r.Cfa != null ? $"CFA[{c.Position / 2},{c.Position % 2}]" : "",
                Colour = ChannelPalette.BrushFor(c.Colour),
                Median = T($"{c.MedianPct:F1} % whole channel", $"{c.MedianPct:F1} % tot canalul"),
                Centre = $"{c.CentreMedianPct:F1} %",
                Note = ExposureReport.ChannelNote(r, c),
                IsBrightest = c.IsBrightest && r.IsCfa,
                Whole = Normalise(c.Whole),
                CentreCurve = Normalise(c.Centre),
                Marker = c.CentreMedianPct,
                BandLo = b.Lo,
                BandHi = b.Hi,
                // On the sky there is no yellow shoulder on the left, so no zone to draw either:
                // NaN tells the drawing "skip it", not "draw a zone of width 0".
                TooLow = b.HasLowShoulder ? b.TooLow : double.NaN,
                TooHigh = b.TooHigh,
                BandLabel = bandLabel,
            }).ToList();
        }

        /// <summary>
        /// The preview's rows: the same drawings, but WITHOUT bands - all four NaN, and
        /// HistogramCanvas skips NaN by itself, so the drawing needed no flag. A target band on
        /// the screen's axis would say "expose until the screen shows 50-65 %", that is exactly the
        /// mistake this comparison exists to fix. Neither the note nor the highlighting of the
        /// brightest: nothing decides here. The clipping stripe stays, so it shows when the screen
        /// is stuck at 255.
        /// </summary>
        /// <param name="r">The analyzer's result; its preview is not null.</param>
        /// <returns>One row per channel of the preview, without bands.</returns>
        static IReadOnlyList<ChannelHistogramRow> PreviewRows(ExposureResult r)
            => r.Preview.Channels.Select(c => new ChannelHistogramRow
            {
                Label = c.Label,
                Position = "",
                Colour = ChannelPalette.BrushFor(c.Colour),
                Median = T($"{c.MedianPct:F1} % whole frame", $"{c.MedianPct:F1} % tot cadrul"),
                Centre = $"{c.CentreMedianPct:F1} %",
                Note = ExposureReport.PreviewChannelNote(c),
                IsBrightest = false,
                Whole = Normalise(c.Whole),
                CentreCurve = Normalise(c.Centre),
                Marker = c.CentreMedianPct,
                BandLo = double.NaN,
                BandHi = double.NaN,
                TooLow = double.NaN,
                TooHigh = double.NaN,
                BandLabel = null,
            }).ToList();

        /// <summary>
        /// The bins scaled to their own peak. Each curve on its own scale because the central
        /// window holds 4 % of the channel's pixels: on a common scale it would be a flat line,
        /// and it is the very one that decides. What is compared between the curves is the
        /// POSITION, not the height.
        /// </summary>
        /// <param name="h">The histogram, or null.</param>
        /// <returns><see cref="LevelHistogram.Bins"/> values in 0..1; all zero for a null or
        /// empty histogram.</returns>
        static double[] Normalise(LevelHistogram h)
        {
            var o = new double[LevelHistogram.Bins];
            if (h == null) return o;

            int peak = 0;
            foreach (int n in h.Counts) if (n > peak) peak = n;
            if (peak == 0) return o;

            for (int i = 0; i < o.Length; i++) o[i] = h.Counts[i] / (double)peak;
            return o;
        }

        /// <inheritdoc/>
        internal override void Reset()
        {
            base.Reset();
            _session = null;
            _gate = null;
            // The box stays as it is; the next folder's file sets it (R-119).
            _settings = SessionSettings.None;
            Files.Clear();
            _selected = null;
            Raise(nameof(Selected));
            Scale = Centre = Global = Clipping = Channels = "";
            ChannelOrder = "";
            Reading = "";
            Histograms = System.Array.Empty<ChannelHistogramRow>();
            Comparison = System.Array.Empty<ScreenPairRow>();
            Raise(nameof(HasComparison));
            SourceNote = "";
            Caption = "";
            _last = null;
            // Source.Selected stays as the user left it: it is a viewing preference, like the
            // map's scale. Only the availability is withdrawn, because there is no file any more.
            Source.IsEnabled = false;
            Verdict = "";
            Advice = "";
            IsRawFlat = true;
            // HistogramGuide.IsExpanded stays as the user left it: it is a viewing preference,
            // not a result - the same as the map guide.
        }
    }
}
