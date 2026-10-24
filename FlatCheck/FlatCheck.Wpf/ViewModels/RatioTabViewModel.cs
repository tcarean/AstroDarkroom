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

// RatioTabViewModel.cs - tab 2, the attribution by 180-degree rotation.
//
// WHY IT NO LONGER LOOKS IN THE FOLDER. Until the rewrite, the tab filled a list from process/
// and preselected two files. It was wrong by construction: the mode asks for two MASTERS, one for
// each orientation, and a Siril session folder never holds such a thing - the two orientations
// are two sittings, so two folders. What the list offered were the individual frames, and a pair
// of individual frames gives a result that is confident and false: the noise of a single frame
// is of the same order as the signal looked for. So now two files are asked for explicitly, and
// nothing is assumed.
//
// It does NOT run at start - only Structure and End to end do - and with good reason: until the
// user chooses two files, there is nothing to compute. Until then it shows the documentation -
// see SessionMessages.RatioNeedsTwoMasters.
//
// A NEW CHOICE SUPERSEDES THE ONE IN PROGRESS. Choose starts the computation without awaiting it,
// so a re-choice during it started a second analysis over the first, and both reached the screen,
// in the order they finished. In the application the UI thread serialised them; in the tests,
// without a synchronisation context, the two continuations ran in parallel and wrote into the
// same _mapCache from two threads - a Dictionary, so a rare exception and a test that failed now
// and then. Now each run has its own token, linked to the session's, as on the histogram tab: the
// new one cancels the old one, and the old lambda stops at the check after the await without
// touching anything. The analysis already started in RatioRun still goes to the end - it gets no
// token - but its result no longer reaches anything.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace FlatCheck.Wpf
{
    /// <summary>The Ratio tab: two flat masters chosen by hand, one per orientation, split into
    /// the part fixed to the camera (the structure battery and its verdict) and the part fixed in
    /// the world (informative), with both maps behind a toggle.</summary>
    public sealed class RatioTabViewModel : TabViewModel
    {
        SirilSession _session;
        CancellationToken _ct;
        CancellationTokenSource _runCts;

        string _fileA;
        string _fileB;
        IReadOnlyList<MetricRow> _worldRows = System.Array.Empty<MetricRow>();
        string _provenanceA = "";
        string _provenanceB = "";
        IReadOnlyList<MetricRow> _tests = System.Array.Empty<MetricRow>();
        IReadOnlyList<MetricRow> _informative = System.Array.Empty<MetricRow>();
        string _localSpotWarning = "";
        string _verdict = "";
        string _advice = "";
        string _mapLegend = "";
        bool _showWorldMap;

        /// <summary>
        /// The two rasters, not the bytes: the map is re-encoded on a change of scale, and both
        /// come in the result already. See the note in StructureTabViewModel.
        /// </summary>
        Img _sensorResidual;
        Img _worldRaster;

        /// <summary>The encodings already made, per (map, cap).</summary>
        readonly Dictionary<(bool World, MapScaleOption Scale), byte[]> _mapCache = new();

        /// <summary>An empty tab, listening to its scale switch.</summary>
        public RatioTabViewModel()
        {
            MapScale.Changed += RefreshMap;
        }

        public string FileA { get => _fileA; private set => Set(ref _fileA, value); }
        public string FileB { get => _fileB; private set => Set(ref _fileB, value); }

        /// <summary>
        /// The component fixed in the world: the figure with its error bar and informative label,
        /// and the corner spread. Two MetricRow rows, like the rest of the screen, each with its
        /// description from the core - they used to be two hand-written Grids, one without a word
        /// under it and the other with a coloured pill that looked like a verdict without being
        /// one. A replaceable list, like <see cref="Tests"/>, for the same reason.
        /// </summary>
        public IReadOnlyList<MetricRow> WorldRows { get => _worldRows; private set => Set(ref _worldRows, value); }

        /// <summary>How many frames and from when, from the FITS headers. The precondition shows
        /// here.</summary>
        public string ProvenanceA { get => _provenanceA; private set => Set(ref _provenanceA, value); }
        public string ProvenanceB { get => _provenanceB; private set => Set(ref _provenanceB, value); }

        /// <summary>
        /// The structure battery on the combined master - the same five rows as tab 1, built by
        /// <see cref="StructureRows"/>, so with the same labels, the same order and the same
        /// descriptions. They were ten properties and five Grid blocks hand-written in XAML, and
        /// that is why they were left without descriptions when tab 1 got them.
        ///
        /// THE LIST IS REPLACED, not cleared and refilled - and that is not a detail: Choose()
        /// starts RecomputeAsync without awaiting it, so two quick choices leave two computations
        /// in the air. Each run now has its own token and the new one cancels the old (see the
        /// header), but the analysis already in RatioRun still runs to its end; a replaced
        /// reference lets only the last one land, where an ObservableCollection cleared and
        /// refilled by both would come out with duplicated rows. The same reason as for Histograms
        /// on the exposure tab.
        /// </summary>
        public IReadOnlyList<MetricRow> Tests { get => _tests; private set => Set(ref _tests, value); }

        /// <summary>
        /// The combined master's quantities without a verdict - vignetting, illumination centre,
        /// local spots - exactly what the console prints under INFORMATIV in the --ratio report.
        /// The same replaceable list as <see cref="Tests"/>, for the same reason.
        /// </summary>
        public IReadOnlyList<MetricRow> Informative { get => _informative; private set => Set(ref _informative, value); }

        /// <summary>
        /// The dust warning on the combined master, or empty. As on tab 1: it sits under the
        /// verdict without colouring it, with the text from StructureReport.LocalSpotWarning.
        /// </summary>
        public string LocalSpotWarning { get => _localSpotWarning; private set => Set(ref _localSpotWarning, value); }

        public string Verdict { get => _verdict; private set => Set(ref _verdict, value); }
        public string Advice { get => _advice; private set => Set(ref _advice, value); }

        /// <summary>
        /// The two maps and the switch between them. The mode splits the master into two
        /// components, so it has two shapes to show: the residual of the part fixed to the
        /// camera, which matches the verdict, and the A/B ratio, which shows the shape of the
        /// external gradient. <see cref="Map"/> is the one displayed.
        /// </summary>
        public byte[] Map { get => _map; private set => Set(ref _map, value); }

        byte[] _map;

        public bool ShowWorldMap
        {
            get => _showWorldMap;
            set { if (Set(ref _showWorldMap, value)) RefreshMap(); }
        }

        /// <summary>The scale switch. It applies to the displayed map, whichever of the two it
        /// is.</summary>
        public MapScaleViewModel MapScale { get; } = new();

        /// <summary>
        /// The legend follows the DISPLAYED map: on the automatic scale the two components have
        /// different ranges, and the real ends are exactly what must be said.
        /// </summary>
        public string MapLegend { get => _mapLegend; private set => Set(ref _mapLegend, value); }

        /// <summary>The same guide as on the structure tab: the same residual, the same
        /// scale.</summary>
        public MapGuideViewModel MapGuide { get; } = new();

        /// <summary>
        /// Rebuilds the map for (the displayed component, the current scale). See the note in
        /// StructureTabViewModel on why the raster is kept and not the bytes.
        /// </summary>
        void RefreshMap()
        {
            var raster = _showWorldMap ? _worldRaster : _sensorResidual;
            if (raster == null)
            {
                Map = null;
                MapLegend = "";
                return;
            }

            var range = MapScale.RangeFor(raster);
            var key = (_showWorldMap, MapScale.Selected);
            if (!_mapCache.TryGetValue(key, out var png))
            {
                png = PngWriter.EncodeResidualMap(raster, range);
                _mapCache[key] = png;
            }

            Map = png;
            MapLegend = RunReport.MapLegend(range);
        }

        // ------------------------------------------------------------ the fixed texts
        // They come from the core, they are not rewritten here: the same sentences are in the
        // saved report.

        // INSTANCE properties, though the value is constant: {Binding} resolves through the
        // DataContext, so it does not see static properties - a binding to one would fail
        // silently, and the tab's very documentation would have stayed empty.
        public string Documentation => SessionMessages.RatioNeedsTwoMasters();
        public string Precondition => RatioReport.Precondition();
        public string WorldMeaning => RatioReport.WorldMeaning();
        public string SensorCaveat => RatioReport.SensorCaveat();

        /// <summary>
        /// Keeps the session and nothing more. It proposes nothing and computes nothing: the two
        /// masters come from two sittings, and the current folder is at most one of them.
        /// </summary>
        /// <param name="session">The folder just opened; it only seeds the file
        /// dialogs.</param>
        /// <param name="ct">The folder's token, to which every run's own token is
        /// linked.</param>
        internal void Attach(SirilSession session, CancellationToken ct)
        {
            _session = session;
            _ct = ct;
            HasResult = false;
            ClearError();
        }

        /// <summary>
        /// The folder the dialog for set A starts in: the one holding the open session's master,
        /// because the stacks are there; otherwise process/, if it exists.
        /// </summary>
        internal string InitialDirectory
        {
            get
            {
                if (_session == null) return null;
                string master = _session.FlatMasterPath;
                if (master != null) return Path.GetDirectoryName(master);
                return Directory.Exists(_session.ProcessDir) ? _session.ProcessDir : null;
            }
        }

        /// <summary>
        /// A file chosen by the user. It is validated by its name, and if it is not a flat master
        /// the tab explains what these files are and why the test asks for one.
        /// </summary>
        /// <param name="isA">True for set A, false for set B.</param>
        /// <param name="path">The file chosen; null or empty is ignored.</param>
        internal void Choose(bool isA, string path)
        {
            if (string.IsNullOrEmpty(path)) return;

            if (!SirilSession.IsFlatMasterName(path))
            {
                Fail("Fisierul ales nu e un master de flat", SessionMessages.NotAFlatMaster(path));
                return;
            }

            // Caught at the choice, not at the computation: that way the user sees at once what
            // went wrong, and the good selection from before is not lost.
            string other = isA ? FileB : FileA;
            if (string.Equals(path, other, System.StringComparison.OrdinalIgnoreCase))
            {
                Fail("Aceleasi doua fisiere", SessionMessages.SameFileTwice());
                return;
            }

            ClearError();
            if (isA) FileA = path; else FileB = path;
            if (!string.IsNullOrEmpty(FileA) && !string.IsNullOrEmpty(FileB))
                Pending = RecomputeAsync();
        }

        /// <summary>The last run started by a choice, so a test can await one it did not
        /// start.</summary>
        internal Task Pending { get; private set; } = Task.CompletedTask;

        /// <summary>Compares the two chosen masters, guarded, superseding any run still going;
        /// without both files it only clears the result.</summary>
        /// <returns>A task that completes when the run ended; it never faults.</returns>
        internal Task RecomputeAsync()
        {
            if (string.IsNullOrEmpty(FileA) || string.IsNullOrEmpty(FileB))
            {
                HasResult = false;
                return Task.CompletedTask;
            }

            string a = FileA, b = FileB;

            // The run from before, if it still goes, is superseded: its token is cancelled, and
            // its lambda stops at the first check after the await, without writing anything.
            CancellationTokenSource old = _runCts;
            old?.Cancel();
            old?.Dispose();
            var cts = CancellationTokenSource.CreateLinkedTokenSource(_ct);
            _runCts = cts;
            CancellationToken ct = cts.Token;

            return RunGuardedAsync("Compar cele doua mastere de flat...", async () =>
            {
                var text = new StringWriter();
                var outcome = await Task.Run(() =>
                {
                    RunReport.WriteBanner(text);
                    var run = RatioRun.Execute(a, b, text);
                    ct.ThrowIfCancellationRequested();
                    return run;
                }, ct).ConfigureAwait(true);

                // If another file was chosen meanwhile, this token is cancelled and the result has
                // no business on screen any more - nor in _mapCache, which it would share with the
                // new run.
                ct.ThrowIfCancellationRequested();

                Report = text.ToString();
                Populate(outcome.Result);

                // The rasters, plus the bytes on the automatic scale straight into the cache -
                // they are exactly what RefreshMap would re-encode for the "auto" chip.
                _sensorResidual = outcome.Result.Sensor.Residual;
                _worldRaster = outcome.Result.World;
                _mapCache.Clear();
                _mapCache[(false, MapScale.Options[0])] = outcome.Png;
                _mapCache[(true, MapScale.Options[0])] = outcome.SecondaryPng;
                RefreshMap();

                HasResult = true;
            });
        }

        /// <summary>Fills the rows, the provenance lines, the verdict and the advice from a
        /// result; the maps are set by the caller.</summary>
        /// <param name="r">The analyzer's result.</param>
        void Populate(RatioResult r)
        {
            WorldRows = new[]
            {
                new MetricRow
                {
                    Label = "gradient extern / orientare",
                    Value = RatioReport.WorldValue(r),
                    Verdict = r.WorldLabel,          // informative; the note says it does not vote
                    Note = RatioReport.WorldNote(),
                },
                new MetricRow
                {
                    Label = "imprastiere colturi",
                    Value = StructureRows.Pct(r.WorldCornerPct),
                    Note = RatioReport.WorldCornerNote(),
                },
            };
            ProvenanceA = RatioReport.Provenance(r.StackCountA, r.DateObsA);
            ProvenanceB = RatioReport.Provenance(r.StackCountB, r.DateObsB);

            Tests = StructureRows.Tests(r.Sensor);
            Informative = StructureRows.Informative(r.Sensor);
            LocalSpotWarning = StructureReport.LocalSpotWarning(r.Sensor) ?? "";

            Verdict = r.Verdict;
            // The advice comes from the core, as on the structure tab: it is the same verdict, on
            // the same scale, so it must be the same sentence.
            Advice = StructureReport.Advice(r.Verdict);
        }

        /// <inheritdoc/>
        internal override void Reset()
        {
            CancellationTokenSource cts = _runCts;
            _runCts = null;
            try { cts?.Cancel(); } catch (System.ObjectDisposedException) { }
            cts?.Dispose();

            base.Reset();
            _session = null;
            FileA = null;
            FileB = null;
            WorldRows = System.Array.Empty<MetricRow>();
            ProvenanceA = ProvenanceB = "";
            Tests = System.Array.Empty<MetricRow>();
            Informative = System.Array.Empty<MetricRow>();
            LocalSpotWarning = "";
            Verdict = "";
            Advice = "";
            // The rasters and the encodings go; the choice of scale stays, as on Structure.
            _sensorResidual = null;
            _worldRaster = null;
            _mapCache.Clear();
            ShowWorldMap = false;
            RefreshMap();
        }
    }
}
