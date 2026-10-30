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

// SmoothTabViewModel.cs - the "Netezire" tab: a second master, without the texture.
//
// IT RUNS ON A BUTTON, NOT ON OPENING. Structure and End to end compute when the folder is
// opened; this one writes a 96 MB file beside the master, and a file is not produced because
// someone looked at a folder. Attach only prepares the paths and the guide.
//
// SEQUENTIAL: the button can be pressed while the structure run is still going on the same 24 MP
// master; RunAsync first awaits the opening chain (MainViewModel), for the memory reason given
// there.
//
// THE FILE IS WRITTEN AFTER THE RESULT IS ON SCREEN, and the overwrite is asked through a delegate
// given from outside - a MessageBox in the application, a fixed answer in the tests. A "no" leaves
// the result on screen and the old file on disk, and says so.
//
// Like the structure tab, it keeps the RASTERS of the two maps, not the bytes: the map is
// re-encoded on a change of cap and cached per (map, cap). The default cap is +-2 %, not +-10 %:
// what is removed is small, and at 10 % the map would be white.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static FlatCheck.Ui;

namespace FlatCheck.Wpf
{
    /// <summary>The Netezire tab: on a button, the session master smoothed at the chosen scale,
    /// the numbers before and after, the removed component and the result's residual as maps,
    /// then the smoothed master written beside the master; a second button proposes the
    /// scale.</summary>
    public sealed class SmoothTabViewModel : TabViewModel
    {
        SirilSession _session;
        CancellationToken _ct;
        HeavyRunGate _gate;
        readonly Func<string, bool> _confirm;

        string _source = "";
        string _target = "";
        bool _canRun;
        string _frame = "";
        string _scaleLine = "";
        string _rejected = "";
        IReadOnlyList<MetricRow> _removed = Array.Empty<MetricRow>();
        string _warning = "";
        IReadOnlyList<MetricRow> _before = Array.Empty<MetricRow>();
        IReadOnlyList<MetricRow> _after = Array.Empty<MetricRow>();
        string _beforeVerdict = "";
        string _afterVerdict = "";
        string _savedTo = "";
        string _saveError = "";
        byte[] _map;
        string _mapLegend = "";
        bool _showAfterMap;

        Img _removedRaster;
        Img _afterResidual;
        readonly Dictionary<(bool After, MapScaleOption Scale), byte[]> _mapCache = new();

        /// <summary>An empty tab with its two commands, listening to its map scale.</summary>
        /// <param name="confirm">The overwrite question: it receives the text, returns yes/no;
        /// null always answers yes.</param>
        public SmoothTabViewModel(Func<string, bool> confirm = null)
        {
            _confirm = confirm ?? (_ => true);
            Run = new RelayCommand(() => Pending = RunAsync(), () => CanRun && IsIdle);
            Advise = new RelayCommand(() => Pending = AdviseAsync(), () => CanRun && IsIdle);
            MapScale.Changed += RefreshMap;
        }

        IReadOnlyList<MetricRow> _adviceRows = Array.Empty<MetricRow>();
        string _adviceTexture = "";
        string _adviceVerdict = "";
        string _adviceNote = "";

        /// <summary>The second button: proposes the scale, ticks the chip, the user presses the
        /// smoothing.</summary>
        internal RelayCommand Advise { get; }
        public System.Windows.Input.ICommand AdviseCommand => Advise;
        public string AdviseLabel => SessionMessages.SmoothAdviseLabel();

        /// <summary>One row per rung tried; empty until the first proposal and on "nimic de
        /// netezit".</summary>
        public IReadOnlyList<MetricRow> AdviceRows { get => _adviceRows; private set => Set(ref _adviceRows, value); }

        /// <summary>The input's fine texture, with the threshold.</summary>
        public string AdviceTexture { get => _adviceTexture; private set => Set(ref _adviceTexture, value); }

        /// <summary>"SCARA PROPUSA: 300 px" / "NIMIC DE NETEZIT" / "NEDETERMINAT: ...".</summary>
        public string AdviceVerdict
        {
            get => _adviceVerdict;
            private set { if (Set(ref _adviceVerdict, value)) Raise(nameof(HasAdvice)); }
        }

        public bool HasAdvice => !string.IsNullOrEmpty(_adviceVerdict);

        public string AdviceNote { get => _adviceNote; private set => Set(ref _adviceNote, value); }

        /// <summary>What the rule does not know. An instance property, so {Binding} sees
        /// it.</summary>
        public string AdviceCaveat => SmoothReport.AdviceCaveat().Replace('\n', ' ');

        /// <summary>The scale proposed at the last proposal; NaN when there was none.</summary>
        internal double ProposedScalePx { get; private set; } = double.NaN;

        /// <summary>
        /// The proposal: the advisor on the session's master, after the opening chain like the
        /// smoothing, then the proposed chip ticked. It writes no file; the result on screen, if
        /// there is one, stays.
        /// </summary>
        /// <returns>A task that completes when the proposal ended; it never faults.</returns>
        internal async Task AdviseAsync()
        {
            if (_session == null || !CanRun) return;
            string path = _session.FlatMasterPath;
            CancellationToken ct = _ct;

            await RunGuardedAsync(T("Proposing a smoothing scale...", "Propun scara de netezire..."), async () =>
            {
                Run.RaiseCanExecuteChanged();
                Advise.RaiseCanExecuteChanged();
                using IDisposable lease = await EnterGateAsync(_gate, ct).ConfigureAwait(true);
                ct.ThrowIfCancellationRequested();

                var text = new StringWriter();
                var outcome = await Task.Run(() =>
                {
                    RunReport.WriteBanner(text);
                    var run = SmoothRun.Advise(path, text);
                    ct.ThrowIfCancellationRequested();
                    return run;
                }, ct).ConfigureAwait(true);

                if (outcome.Refused)
                {
                    Report = text.ToString();
                    Fail(SessionMessages.SmoothRefusedTitle(), outcome.Refusal);
                    return;
                }
                PopulateAdvice(outcome.Advice);
                if (!HasResult) Report = text.ToString();
            }).ConfigureAwait(true);
            Run.RaiseCanExecuteChanged();
            Advise.RaiseCanExecuteChanged();
        }

        /// <summary>Internal, so the rendering tests can bring a proposal without a
        /// file.</summary>
        /// <param name="a">The advisor's proposal; a proposed scale ticks its chip.</param>
        internal void PopulateAdvice(SmoothScaleAdvice a)
        {
            AdviceTexture = SmoothReport.InputTextureLine(a);
            AdviceRows = a.Trials.Select(t => new MetricRow
            {
                Label = T($"scale {t.ScalePx:F0} px", $"scara {t.ScalePx:F0} px"),
                Value = SmoothReport.TrialVerdict(t),
                Note = T($"texture left {t.TextureAfterPct:F2} %, asymmetry {t.AzimuthalDelta:+0.00;-0.00}, "
                         + $"tilt {t.TiltDelta:+0.00;-0.00}, removed {t.RemovedPtpPct:F2} % ptp",
                         $"textura ramasa {t.TextureAfterPct:F2} %, asimetrie {t.AzimuthalDelta:+0.00;-0.00}, "
                         + $"inclinare {t.TiltDelta:+0.00;-0.00}, scos {t.RemovedPtpPct:F2} % ptp"),
            }).ToList();
            AdviceNote = SmoothReport.AdviceNote(a).Replace('\n', ' ');
            AdviceVerdict = SmoothReport.AdviceVerdict(a);
            ProposedScalePx = a.ScalePx;
            if (a.Kind == SmoothAdviceKind.Proposed) Scale.SelectScale(a.ScalePx);
        }

        /// <summary>The master the smoothed flat is made from.</summary>
        public string Source { get => _source; private set => Set(ref _source, value); }

        /// <summary>Where the smoothed flat is written.</summary>
        public string Target { get => _target; private set => Set(ref _target, value); }

        /// <summary>There is a master to smooth and nothing is running already.</summary>
        public bool CanRun
        {
            get => _canRun;
            private set { if (Set(ref _canRun, value)) { Run.RaiseCanExecuteChanged(); Advise?.RaiseCanExecuteChanged(); } }
        }

        /// <summary>The button. It starts the run; nobody awaits the result outside the
        /// tests.</summary>
        internal RelayCommand Run { get; }

        /// <summary>The XAML binds here: RelayCommand is internal.</summary>
        public System.Windows.Input.ICommand RunCommand => Run;

        /// <summary>The button's label, from SessionMessages.</summary>
        public string ButtonLabel => SessionMessages.SmoothButtonLabel();

        /// <summary>The smoothing scale: three chips. Read on the click.</summary>
        public SmoothScaleViewModel Scale { get; } = new();

        /// <summary>The guide: what it does, what it cannot know. Open by default.</summary>
        public SmoothGuideViewModel Guide { get; } = new();

        public string Frame { get => _frame; private set => Set(ref _frame, value); }
        public string ScaleLine { get => _scaleLine; private set => Set(ref _scaleLine, value); }
        public string Rejected { get => _rejected; private set => Set(ref _rejected, value); }

        /// <summary>The four figures about the removed component, as rows without a
        /// verdict.</summary>
        public IReadOnlyList<MetricRow> Removed { get => _removed; private set => Set(ref _removed, value); }

        /// <summary>The warning when more than the texture was removed, or empty.</summary>
        public string Warning
        {
            get => _warning;
            private set { if (Set(ref _warning, value)) Raise(nameof(HasWarning)); }
        }

        public bool HasWarning => !string.IsNullOrEmpty(_warning);

        /// <summary>The structure battery on the input and on the output - the same rows as
        /// tab 1.</summary>
        public IReadOnlyList<MetricRow> Before { get => _before; private set => Set(ref _before, value); }
        public IReadOnlyList<MetricRow> After { get => _after; private set => Set(ref _after, value); }
        public string BeforeVerdict { get => _beforeVerdict; private set => Set(ref _beforeVerdict, value); }
        public string AfterVerdict { get => _afterVerdict; private set => Set(ref _afterVerdict, value); }

        /// <summary>What the file does not know, and how it gets into Siril. Instance properties,
        /// so {Binding} sees them.</summary>
        public string Caveat => SmoothReport.Caveat().Replace('\n', ' ');
        public string SirilNote => SmoothReport.SirilNote().Replace('\n', ' ');

        /// <summary>Where the flat was written, or empty.</summary>
        public string SavedTo { get => _savedTo; private set => Set(ref _savedTo, value); }

        /// <summary>
        /// Raised once, with the path, after the file really was written - not on a refused
        /// overwrite and not on a write error, when nothing usable changed on disk. It is a fact
        /// the tab REPORTS, unlike the overwrite question, which it ASKS; hence an event, not a
        /// delegate in the constructor. The listener is MainViewModel, which rebuilds the
        /// Histogram tab's list.
        /// </summary>
        internal event Action<string> Saved;

        /// <summary>Why it was not written (a refused overwrite, the disk), or empty. The figures
        /// stay.</summary>
        public string SaveError { get => _saveError; private set => Set(ref _saveError, value); }

        /// <summary>The map shown: the removed component, or the smoothed flat's
        /// residual.</summary>
        public byte[] Map { get => _map; private set => Set(ref _map, value); }

        public bool ShowAfterMap
        {
            get => _showAfterMap;
            set { if (Set(ref _showAfterMap, value)) RefreshMap(); }
        }

        /// <summary>The map's cap; by default +-2 %, the PERFECT band - what is removed is
        /// small.</summary>
        public MapScaleViewModel MapScale { get; } = new(Th.ResidPerfect);

        public string MapLegend { get => _mapLegend; private set => Set(ref _mapLegend, value); }

        /// <summary>The last run started by a button, so a test can await it.</summary>
        internal Task Pending { get; private set; } = Task.CompletedTask;

        /// <summary>The scale used at the last run, in sensor pixels; NaN before the
        /// first.</summary>
        internal double LastScalePx { get; private set; } = double.NaN;

        /// <summary>Prepares the paths and enables the buttons when the session has a master;
        /// it computes nothing.</summary>
        /// <param name="session">The folder just opened.</param>
        /// <param name="ct">The folder's token.</param>
        /// <param name="gate">The window's heavy-run gate, taken around a run; null runs at
        /// once.</param>
        internal void Attach(SirilSession session, CancellationToken ct, HeavyRunGate gate = null)
        {
            _session = session;
            _ct = ct;
            _gate = gate;
            Source = session.FlatMasterPath ?? "";
            Target = session.SmoothedMasterPath ?? "";
            CanRun = session.FlatMasterPath != null;
            Advise.RaiseCanExecuteChanged();
            ClearError();
        }

        /// <summary>Smooths the session's master at the chosen scale, guarded, after the opening
        /// chain; shows the numbers, then writes the smoothed master beside it, asking before an
        /// overwrite, and raises <see cref="Saved"/> once written.</summary>
        /// <returns>A task that completes when the run ended; it never faults.</returns>
        internal async Task RunAsync()
        {
            if (_session == null || !CanRun) return;
            string path = _session.FlatMasterPath;
            string target = _session.SmoothedMasterPath;
            double scale = Scale.ScalePx;
            CancellationToken ct = _ct;

            await RunGuardedAsync(T("Smoothing the flat master...", "Netezesc masterul de flat..."), async () =>
            {
                Run.RaiseCanExecuteChanged();
                using IDisposable lease = await EnterGateAsync(_gate, ct).ConfigureAwait(true);
                ct.ThrowIfCancellationRequested();

                var text = new StringWriter();
                var outcome = await Task.Run(() =>
                {
                    RunReport.WriteBanner(text);
                    var run = SmoothRun.Execute(path, scale, text);
                    ct.ThrowIfCancellationRequested();
                    return run;
                }, ct).ConfigureAwait(true);

                if (outcome.Refused)
                {
                    Report = text.ToString();
                    Fail(SessionMessages.SmoothRefusedTitle(), outcome.Refusal);
                    return;
                }

                LastScalePx = scale;
                Populate(outcome.Result);
                // the bytes received are on the automatic scale: into the cache, so they are not
                // re-encoded
                _mapCache[(false, MapScale.Options[0])] = outcome.Png;
                _mapCache[(true, MapScale.Options[0])] = outcome.SecondaryPng;

                // The file, after the figures are on screen: a refused overwrite or a full disk
                // have no reason to hide a result already computed.
                SavedTo = "";
                SaveError = "";
                if (File.Exists(target) && !_confirm(SessionMessages.SmoothOverwriteQuestion(target)))
                {
                    Log.Info("the user kept the existing " + target + "; nothing written");
                    SaveError = SessionMessages.SmoothNotOverwritten(target);
                    Report = text.ToString();
                    return;
                }
                try
                {
                    await Task.Run(() =>
                    {
                        using var fs = new FileStream(target, FileMode.Create, FileAccess.Write);
                        SmoothRun.WriteFits(outcome, path, fs);
                    }, ct).ConfigureAwait(true);
                    text.WriteLine(SmoothReport.SavedLine(target));
                    SavedTo = target;
                    Log.Info("wrote " + target + " (" + LogText.FileKiB(target) + ")");
                    Saved?.Invoke(target);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    Log.Warn(e, "the smoothed flat could not be written to " + target);
                    SaveError = T("Could not write the smoothed flat:\n", "Nu am putut scrie flat-ul netezit:\n") + Describe(e);
                }
                Report = text.ToString();
            }).ConfigureAwait(true);
            // the button is re-enabled after the waiting screen went off
            Run.RaiseCanExecuteChanged();
        }

        /// <summary>Internal, so the rendering tests can bring a result without a
        /// file.</summary>
        /// <param name="r">The analyzer's result; its two rasters are kept for the maps.</param>
        internal void Populate(SmoothResult r)
        {
            Frame = SmoothReport.FrameLine(r);
            ScaleLine = SmoothReport.ScaleLine(r);
            Rejected = SmoothReport.RejectedLine(r);
            Removed = SmoothReport.RemovedLines(r).Select(line =>
            {
                int colon = line.IndexOf(':');
                return new MetricRow
                {
                    Label = line.Substring(0, colon).Trim(),
                    Value = line.Substring(colon + 1).Trim(),
                };
            }).ToList();
            Warning = SmoothReport.Warning(r)?.Replace('\n', ' ') ?? "";
            Before = StructureRows.Tests(r.Before);
            After = StructureRows.Tests(r.After);
            BeforeVerdict = r.Before.Verdict;
            AfterVerdict = r.After.Verdict;
            _removedRaster = r.Removed;
            _afterResidual = r.After.Residual;
            _mapCache.Clear();
            RefreshMap();
            HasResult = true;
        }

        /// <summary>Shows the map chosen by the toggle on the chosen cap, from the cache or
        /// encoded now, with its legend; empty before a result.</summary>
        void RefreshMap()
        {
            Img raster = _showAfterMap ? _afterResidual : _removedRaster;
            if (raster == null) { Map = null; MapLegend = ""; return; }

            var range = MapScale.RangeFor(raster);
            var key = (_showAfterMap, MapScale.Selected);
            if (!_mapCache.TryGetValue(key, out byte[] png))
            {
                png = PngWriter.EncodeResidualMap(raster, range);
                _mapCache[key] = png;
            }
            Map = png;
            MapLegend = RunReport.MapLegend(range);
        }

        /// <inheritdoc/>
        internal override void Reset()
        {
            base.Reset();
            _session = null;
            _gate = null;
            Source = "";
            Target = "";
            CanRun = false;
            Frame = "";
            ScaleLine = "";
            Rejected = "";
            Removed = Array.Empty<MetricRow>();
            Warning = "";
            Before = Array.Empty<MetricRow>();
            After = Array.Empty<MetricRow>();
            BeforeVerdict = "";
            AfterVerdict = "";
            SavedTo = "";
            SaveError = "";
            Map = null;
            MapLegend = "";
            LastScalePx = double.NaN;
            AdviceRows = Array.Empty<MetricRow>();
            AdviceTexture = "";
            AdviceVerdict = "";
            AdviceNote = "";
            ProposedScalePx = double.NaN;
            _removedRaster = null;
            _afterResidual = null;
            _mapCache.Clear();
        }
    }
}
