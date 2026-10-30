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

// LightTabViewModel.cs - tab 3, the end-to-end test on the whole series in process/.
//
// Three end states, and all three are correct answers:
//
//   1. a verdict on the series - the mean really estimates the flat error;
//   2. NEDETERMINAT - it ran, but the series is not conclusive, and it says why;
//   3. INAPLICABIL - the field is too wide, the sky and the vignetting have the same shape, so
//      no figure is produced.
//
// The third is NOT an error of the application and is not shown as one. It is the result of the
// guard from 1.2.0, added after the tool had confidently reported "+3.13 % SUB-corectie" on a
// session at 20 mm where 88 % of what it measured was sky. A tab that showed this as a failure
// would teach the user the application is broken, when in fact it has just spared them a false
// conclusion.
//
// The table fills as the frames are ready, through LightSeriesRun's callback. On 32 frames the
// run takes about two minutes; an empty screen all that time would look frozen.

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using static FlatCheck.Ui;

namespace FlatCheck.Wpf
{
    /// <summary>The End to end tab: the light series of process/, one table row per frame as it
    /// finishes, then the series' figures, verdict and radial map - or the field guard's
    /// refusal.</summary>
    public sealed class LightTabViewModel : TabViewModel
    {
        string _source = "";
        string _field = "";
        string _fieldWarning = "";
        string _meanRadial = "";
        string _spread = "";
        string _sem = "";
        string _correlation = "";
        string _verdict = "";
        string _diagnosis = "";
        string _reasons = "";
        bool _refused;
        byte[] _map;
        string _mapLegend = "";

        /// <summary>
        /// The thread Frames is written on, captured ONCE when the run starts.
        ///
        /// Application.Current is not asked at every frame, and that is not an optimisation: if
        /// the answer changed in the middle of the series, some rows would go in on one thread and
        /// the rest on another, and ObservableCollection does not stand that - rows are lost,
        /// silently. It did happen, in a test run where the WPF application appeared meanwhile:
        /// six frames processed, five rows in the table.
        /// </summary>
        Dispatcher _ui;

        public string Source { get => _source; private set => Set(ref _source, value); }

        /// <summary>The field covered and where it was learnt from - shown also when the test is
        /// refused.</summary>
        public string Field { get => _field; private set => Set(ref _field, value); }

        /// <summary>The warning of a field at the limit, or the note that it could not be
        /// checked.</summary>
        public string FieldWarning { get => _fieldWarning; private set => Set(ref _fieldWarning, value); }

        public string MeanRadial { get => _meanRadial; private set => Set(ref _meanRadial, value); }
        public string Spread { get => _spread; private set => Set(ref _spread, value); }
        public string Sem { get => _sem; private set => Set(ref _sem, value); }
        public string Correlation { get => _correlation; private set => Set(ref _correlation, value); }

        public string Verdict { get => _verdict; private set => Set(ref _verdict, value); }
        public string Diagnosis { get => _diagnosis; private set => Set(ref _diagnosis, value); }

        /// <summary>Why the series is not conclusive. Empty when it is.</summary>
        public string Reasons { get => _reasons; private set => Set(ref _reasons, value); }

        /// <summary>True when the field guard stopped the test. It is not an error.</summary>
        public bool Refused
        {
            get => _refused;
            private set { if (Set(ref _refused, value)) Raise(nameof(HasNumbers)); }
        }

        /// <summary>The figures panel is shown only when there really are figures.</summary>
        public bool HasNumbers => !_refused;

        public byte[] Map { get => _map; private set => Set(ref _map, value); }

        /// <summary>
        /// What the ends of the colours mean. Only the legend, without a scale switch: the map
        /// here is the fitted radial term, and its thresholds are Th.LightRad*, not the residual
        /// scale - so the caps of +/-2/10/20 % would mean nothing on it.
        /// </summary>
        public string MapLegend { get => _mapLegend; private set => Set(ref _mapLegend, value); }

        public ObservableCollection<SeriesRow> Frames { get; } = new();

        // The explanations in place (R-105): text from SessionMessages, the thresholds from Th.
        // None depends on the run, so they are the same before it, on a refusal and after Reset.

        /// <summary>The line under the mean radial component.</summary>
        public string MeanRadialNote => SessionMessages.LightMeanNote();

        /// <summary>The line under the spread.</summary>
        public string SpreadNote => SessionMessages.LightSpreadNote();

        /// <summary>The line under the error of the mean.</summary>
        public string SemNote => SessionMessages.LightSemNote();

        /// <summary>The line under the correlation with the sky gradient.</summary>
        public string CorrelationNote => SessionMessages.LightCorrelationNote();

        /// <summary>The paragraph under the map and its legend.</summary>
        public string MapNote => SessionMessages.LightMapNote();

        /// <summary>The tooltip of the table's "frame" header.</summary>
        public string FrameTip => SessionMessages.LightFrameTip();

        /// <summary>The tooltip of the table's "radial" header.</summary>
        public string RadialTip => SessionMessages.LightRadialTip();

        /// <summary>The tooltip of the table's "sky" header.</summary>
        public string SkyTip => SessionMessages.LightSkyTip();

        /// <summary>The tooltip of the table's "tiles" header.</summary>
        public string TilesTip => SessionMessages.LightTilesTip();

        /// <summary>Runs the light series of the session's process/, guarded, adding a row per
        /// frame as it finishes; a folder without a series gets the explanation instead.</summary>
        /// <param name="session">The folder just opened.</param>
        /// <param name="ct">The folder's token; checked between frames.</param>
        /// <returns>A task that completes when the run ended; it never faults.</returns>
        internal Task RunAsync(SirilSession session, CancellationToken ct)
            => RunGuardedAsync(T("Running the end-to-end test on the series of lights...", "Rulez testul end-to-end pe seria de lights..."), async () =>
            {
                // The list is asked for BEFORE the run, so "no frame" is a state of the interface
                // with an explanation, not an exception caught in the middle of a computation.
                var files = session.LightSeries();
                if (files.Count == 0)
                {
                    Refuse(T("Nothing to run the end-to-end test on", "Nu am pe ce rula testul end-to-end"), SessionMessages.NoLightSeries(session),
                           "no light series in " + session.ProcessDir);
                    return;
                }

                Source = T($"{session.ProcessDir}   ({files.Count} frames, pattern {SeriesFinder.DefaultPattern})",
                           $"{session.ProcessDir}   ({files.Count} cadre, tipar {SeriesFinder.DefaultPattern})");

                _ui = UiDispatcher();
                OnUi(Frames.Clear);
                ProgressKnown = true;

                var text = new StringWriter();
                var outcome = await Task.Run(() => LightSeriesRun.Execute(
                    session.ProcessDir, SeriesFinder.DefaultPattern, null, text,
                    onFrame: OnFrame, ct: ct), ct).ConfigureAwait(true);

                Report = text.ToString();
                Populate(outcome);
                HasResult = true;
            });

        /// <summary>
        /// Called from the computing thread. Adding to an ObservableCollection must happen on the
        /// UI thread, otherwise WPF throws at the first binding - so it goes through the
        /// dispatcher.
        ///
        /// Two situations bypass the dispatcher, and both are real:
        ///   - there is no WPF application (tests): nobody draws, so the row is added directly;
        ///   - the dispatcher is shutting down (the window was closed in the middle of a series of
        ///     32 frames): an Invoke there would throw, and nobody is left to see the row anyway.
        ///     The cancellation stops the loop at the next frame.
        /// </summary>
        /// <param name="index">The frame's index in the series, from 0.</param>
        /// <param name="total">How many frames the series has.</param>
        /// <param name="name">The frame's file name.</param>
        /// <param name="r">The frame's analysis.</param>
        void OnFrame(int index, int total, string name, LightResult r)
        {
            var row = new SeriesRow
            {
                Name = name,
                Radial = LightReport.Signed(r.RadPct) + " %",
                Sky = $"{r.PlanePct:F2} %",
                Tiles = $"{r.KeptTiles}/{r.TotalTiles}",
                Verdict = r.Verdict,
            };

            void Apply()
            {
                Frames.Add(row);
                Progress = (index + 1) / (double)total;
                BusyDetail = T($"frame {index + 1} of {total}: {name}", $"cadrul {index + 1} din {total}: {name}");
            }

            OnUi(Apply);
        }

        /// <summary>
        /// Runs on the thread that owns Frames for the current run.
        ///
        /// Without a WPF application - in the tests - there is no UI thread, so it runs directly:
        /// nobody draws and there is nothing to synchronise.
        ///
        /// With the dispatcher shut down during the run - the window closed in the middle of a
        /// series of 32 frames - the update is DROPPED, not run directly. It is tempting to fall
        /// back to the current thread, but that would break exactly the invariant that keeps the
        /// collection whole: some rows would reach the UI thread and the rest the computing
        /// thread, and ObservableCollection loses rows silently when that happens to it. Nobody is
        /// left to see the row anyway - the window is closing - and the cancellation stops the loop
        /// at the next frame.
        /// </summary>
        /// <param name="work">The update to make.</param>
        void OnUi(Action work)
        {
            Dispatcher d = _ui;
            if (d == null) { work(); return; }

            // The shutdown can come at any time after the capture. The update is DROPPED then,
            // not run on the current thread: that would break the invariant above halfway through
            // the run, and ObservableCollection loses rows silently when that happens to it.
            // Nobody is left to see the row anyway - the window is closing.
            if (d.HasShutdownStarted || d.HasShutdownFinished) return;

            try { d.Invoke(work); }
            catch (System.ComponentModel.Win32Exception) { }
            catch (TaskCanceledException) { }
        }

        /// <summary>
        /// The UI thread, if there is one USABLE now.
        ///
        /// It is not enough for an Application to exist: that object outlives its own dispatcher,
        /// so after a shutdown Application.Current is still non-null while its dispatcher no
        /// longer takes anything. Captured this way, the field says a single thing for the whole
        /// run - "there is a UI thread" or "there is not" - and does not change midway.
        /// </summary>
        /// <returns>The dispatcher, or null without an application or once it is shutting
        /// down.</returns>
        static Dispatcher UiDispatcher()
        {
            Dispatcher d = System.Windows.Application.Current?.Dispatcher;
            return d != null && !d.HasShutdownStarted && !d.HasShutdownFinished ? d : null;
        }

        /// <summary>Shows the series' outcome: the field and its warning, then either the
        /// refusal's explanation or the figures, the verdict, the diagnosis or the reasons, and
        /// the map on the automatic scale.</summary>
        /// <param name="outcome">The run's outcome.</param>
        void Populate(LightSeriesRunOutcome outcome)
        {
            Field = LightReport.FieldText(outcome.Suitability);
            FieldWarning = LightReport.FieldWarning(outcome.Suitability);

            if (outcome.Refused)
            {
                Refused = true;
                Verdict = Stats.NotApplicable;
                Diagnosis = "";
                Reasons = LightReport.TooWideText(outcome.Suitability);
                MeanRadial = Spread = Sem = Correlation = "";
                Map = null;
                MapLegend = "";
                return;
            }

            var r = outcome.Result;
            Refused = false;

            MeanRadial = LightReport.Signed(r.MeanRad) + " %";
            Spread = $"{LightReport.Signed(r.MinRad)} .. {LightReport.Signed(r.MaxRad)} %";
            Sem = T($"{r.Sem:F2} points", $"{r.Sem:F2} puncte");
            Correlation = LightSeriesReport.CorrelationValue(r);

            Verdict = r.Verdict;
            Diagnosis = r.Conclusive ? LightReport.Diagnosis(r.MeanRad) : "";
            Reasons = r.Conclusive ? "" : LightSeriesReport.ReasonsText(r);
            Map = outcome.Png;
            // The same ends the received map was encoded with, so the automatic scale.
            MapLegend = RunReport.MapLegend(PngWriter.AutoRange(r.RadialMap));
        }

        /// <inheritdoc/>
        internal override void Reset()
        {
            base.Reset();
            Source = "";
            Field = "";
            FieldWarning = "";
            MeanRadial = Spread = Sem = Correlation = "";
            Verdict = "";
            Diagnosis = "";
            Reasons = "";
            Refused = false;
            Map = null;
            MapLegend = "";
            OnUi(Frames.Clear);
            _ui = null;
        }
    }
}
