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

// StructureTabViewModel.cs - tab 1.
//
// It runs StructureRun on the session's pp_flat_stacked.fit, in masters/ or process/
// (SirilSession.FindFlatMaster), and shows the same thing the console writes, only laid out
// differently. The figures are NOT recomputed here: each row takes a value from StructureResult
// and formats it; the corner percentages and the notes under the rows come from StructureReport
// too.
//
// Besides the display, the tab has a duty to the disk: it saves the report in the session folder,
// as a text file, IDENTICAL to what the console would have written - version header, copyright,
// report, the map's line. That is why StructureRun receives a StringWriter and nobody composes
// its text by hand.
//
// THE MAP IS RE-ENCODED ON A CHANGE OF SCALE, so the tab keeps the residual raster, not only the
// PNG bytes received from the run. The raster is at the analysis resolution (~1500x1000 doubles,
// some 12 MB), and it is already in StructureResult - so nothing is loaded or recomputed for a
// change of scale. The alternative - four PNGs encoded by the core on every run - would have been
// ~7 MB and three encodings nobody may look at.
//
// THE SAVED FILE STAYS ON THE AUTOMATIC SCALE, whichever chip is ticked. The console writes auto,
// and the two applications must write the same file for the same master; the chips are an aid to
// looking, not an output setting.
//
// THE VIGNETTING AGAINST THE LENS PROFILE is compared in the same run, when there is a raw in
// flats/ (SirilSession.ReferenceRawPath): the run is then exactly "flatcheck master --raw <that
// raw>", with the window's shared catalog, so the saved report stays the console's and the
// verdict appears once, already combined - not PERFECT on five tests and REFA two seconds later.
// Without a raw the run is the plain one, identical to "flatcheck master", and the card says why
// it could not compare (the core's text, not the tab's).

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
    /// <summary>The Structure tab: the session master's five tests, verdict, informative rows,
    /// residual map and, with a raw in flats/, the comparison with the lens profiles; it saves the
    /// console's report and map beside the session.</summary>
    public sealed class StructureTabViewModel : TabViewModel
    {
        string _source = "";
        string _sourceWarning = "";
        string _size = "";
        string _levels = "";
        string _verdict = "";
        string _advice = "";
        string _localSpotWarning = "";
        string _verdictDetail = "";
        string _whyResidual = StructureReport.WhyResidualMatters();
        string _profileMaster = "", _profileSummary = "", _profileScaleNote = "", _profileNote = "", _profileGap = "";
        IReadOnlyList<ScaleRow> _profileScale = Array.Empty<ScaleRow>();
        IReadOnlyList<LensProfileRow> _profileRows = Array.Empty<LensProfileRow>();
        string _savedTo = "";
        string _saveError = "";
        byte[] _map;
        string _mapLegend = "";

        /// <summary>The raster the map is drawn from, kept so it can be re-encoded on another
        /// scale.</summary>
        Img _residual;

        /// <summary>The encodings already made, so nothing is re-encoded on every click back and
        /// forth.</summary>
        readonly Dictionary<MapScaleOption, byte[]> _mapCache = new();

        /// <summary>The shared profile catalog, or null (tests): then nothing is compared and the
        /// card explains.</summary>
        readonly ProfileCatalog _catalog;
        /// <summary>A file's metadata without pixels; in the tests, a made-up frame in place of a
        /// raw.</summary>
        readonly Func<string, LoadedFrame> _loadInfo;

        /// <summary>A tab without a catalog, so it never compares with the profiles.</summary>
        public StructureTabViewModel() : this(null, null) { }

        /// <summary>A tab that compares with the profiles when it has a catalog.</summary>
        /// <param name="catalog">The window's shared profile catalog; null turns the comparison
        /// off.</param>
        /// <param name="loadInfo">Reads a file's header and metadata without its pixels; null for
        /// <c>ImageLoader.LoadInfo</c>.</param>
        internal StructureTabViewModel(ProfileCatalog catalog, Func<string, LoadedFrame> loadInfo)
        {
            _catalog = catalog;
            _loadInfo = loadInfo ?? ImageLoader.LoadInfo;
            MapScale.Changed += RefreshMap;
        }

        /// <summary>The file analysed, so it shows what was worked on.</summary>
        public string Source { get => _source; private set => Set(ref _source, value); }

        /// <summary>
        /// The warning that the folder has TWO masters (masters/ and process/) and the newest was
        /// taken. Empty in the usual case. See SirilSession.FindFlatMaster.
        /// </summary>
        public string SourceWarning
        {
            get => _sourceWarning;
            private set { if (Set(ref _sourceWarning, value)) Raise(nameof(HasSourceWarning)); }
        }

        public bool HasSourceWarning => !string.IsNullOrEmpty(_sourceWarning);

        public string Size { get => _size; private set => Set(ref _size, value); }
        public string Levels { get => _levels; private set => Set(ref _levels, value); }

        public string Verdict { get => _verdict; private set => Set(ref _verdict, value); }
        public string Advice { get => _advice; private set => Set(ref _advice, value); }

        /// <summary>
        /// The warning for a compact spot (dust, condensation, a small leak), or empty. It sits
        /// under the verdict without colouring it: it is not a verdict, it is a question put to
        /// the user - a session flat or a library one? The text comes from
        /// StructureReport.LocalSpotWarning, so it is the one in the saved report.
        /// </summary>
        public string LocalSpotWarning { get => _localSpotWarning; private set => Set(ref _localSpotWarning, value); }

        /// <summary>
        /// The verdict's two parts - the shape (the five tests) and the depth (against the
        /// profile) - when it compared; empty otherwise. The text is the one under VERDICT in the
        /// console.
        /// </summary>
        public string VerdictDetail { get => _verdictDetail; private set => Set(ref _verdictDetail, value); }

        // ----------------------------------------- the vignetting against the lens profile

        /// <summary>The card's heading, from the core, so it is the report's.</summary>
        public string ProfilesHeading => StructureReport.ProfilesHeading;

        /// <summary>One row per profile; empty when nothing was compared. The list is replaced,
        /// not filled.</summary>
        public IReadOnlyList<LensProfileRow> ProfileRows
        {
            get => _profileRows;
            private set { if (Set(ref _profileRows, value)) Raise(nameof(HasProfileRows)); }
        }

        public bool HasProfileRows => _profileRows.Count > 0;

        /// <summary>"1.26 stopuri (colt = 42 % din centru)" - the master's depth, the one in the
        /// informative row.</summary>
        public string ProfileMaster { get => _profileMaster; private set => Set(ref _profileMaster, value); }

        /// <summary>Whose the verdict is and how much the profiles differ among
        /// themselves.</summary>
        public string ProfileSummary { get => _profileSummary; private set => Set(ref _profileSummary, value); }

        /// <summary>The depth scale in stops - the same four bands as the residual scale, with the
        /// verdict's band marked.</summary>
        public IReadOnlyList<ScaleRow> ProfileScale { get => _profileScale; private set => Set(ref _profileScale, value); }

        /// <summary>Where the scale above comes from: the five tests' scale translated into stops,
        /// not a new threshold.</summary>
        public string ProfileScaleNote { get => _profileScaleNote; private set => Set(ref _profileScaleNote, value); }

        /// <summary>What the comparison can and cannot say.</summary>
        public string ProfileNote { get => _profileNote; private set => Set(ref _profileNote, value); }

        /// <summary>Why it could not compare, when it could not; empty otherwise.</summary>
        public string ProfileGap { get => _profileGap; private set => Set(ref _profileGap, value); }

        /// <summary>Constant, but kept as a property so it can be bound from XAML.</summary>
        public string WhyResidual { get => _whyResidual; private set => Set(ref _whyResidual, value); }

        /// <summary>The residual map as PNG bytes - shown without touching the disk.</summary>
        public byte[] Map { get => _map; private set => Set(ref _map, value); }

        /// <summary>The map's scale switch. It touches no figure, only the colours.</summary>
        public MapScaleViewModel MapScale { get; } = new();

        /// <summary>What the ends of the colours mean on the scale actually shown.</summary>
        public string MapLegend { get => _mapLegend; private set => Set(ref _mapLegend, value); }

        /// <summary>The map's reading guide. Documentation, not a diagnosis - see
        /// MapGuideViewModel.</summary>
        public MapGuideViewModel MapGuide { get; } = new();

        /// <summary>What the figures decide and what the eye decides. It sits under the verdict,
        /// in the figures' column.</summary>
        public HowToReadViewModel HowToRead { get; } = new();

        /// <summary>Where the report was saved, or empty if it was not saved yet.</summary>
        public string SavedTo { get => _savedTo; private set => Set(ref _savedTo, value); }

        /// <summary>
        /// The save can fail for reasons unrelated to the analysis (a read-only folder, a file
        /// open in another program). It is reported apart, so it does not look as if the
        /// measurement failed when in fact the figures are good and only the file could not be
        /// written.
        /// </summary>
        public string SaveError { get => _saveError; private set => Set(ref _saveError, value); }

        public ObservableCollection<MetricRow> Tests { get; } = new();
        public ObservableCollection<MetricRow> Informative { get; } = new();
        public ObservableCollection<ScaleRow> Scale { get; } = new();

        /// <summary>
        /// Runs the mode and saves the report. The session gives both the input file and the
        /// output place - the tab chooses no path by itself.
        /// </summary>
        /// <param name="session">The open session folder; its master is analysed and its raw in
        /// flats/, if any, names the lens.</param>
        /// <param name="ct">The folder's token.</param>
        /// <returns>A task that completes when the run ended; it never faults.</returns>
        internal Task RunAsync(SirilSession session, CancellationToken ct)
        {
            string rawPath = session.ReferenceRawPath;
            bool compares = rawPath != null && _catalog != null;
            return RunGuardedAsync(compares
                ? T("Analysing the structure of the flat master and looking for the lens profile...",
                    "Analizez structura masterului de flat si caut profilul obiectivului...")
                : T("Analysing the structure of the flat master...", "Analizez structura masterului de flat..."), async () =>
            {
                FlatMasterLookup master = session.FindFlatMaster();
                string path = master.Path;
                SourceWarning = master.Ambiguous ? SessionMessages.TwoFlatMasters(master) : "";
                var text = new StringWriter();

                // With a raw beside the master, the run is the console's with --raw: the shared
                // catalog, the raw from flats/, the comparison in the same report. Without one,
                // the plain run.
                StructureQuery query = null;
                if (compares)
                {
                    ProfileSources sources = await _catalog.EnsureAsync().ConfigureAwait(true);
                    ct.ThrowIfCancellationRequested();
                    query = new StructureQuery { Sources = sources, RawPath = rawPath, LoadInfo = _loadInfo };
                }

                var (outcome, gap) = await Task.Run(() =>
                {
                    RunReport.WriteBanner(text);
                    var run = StructureRun.Execute(path, text, query);
                    RunReport.WriteMapSaved(session.ResidualPngPath, text);
                    ct.ThrowIfCancellationRequested();
                    // Without a comparison, the reason for the card: the same core says why, on
                    // the master's metadata (the header only) and without a catalog.
                    StructureOutcome why = run.Result.Profiles != null ? null
                        : StructureRun.Compare(run.Result.Structure, _loadInfo(path), new StructureQuery { RawPath = rawPath, LoadInfo = _loadInfo });
                    return (run, why);
                }, ct).ConfigureAwait(true);

                Source = path;
                Report = text.ToString();
                Populate(outcome.Result, gap);

                // The raster, then the map on the chosen scale. The bytes received from the run
                // are those on the automatic scale: they go straight into the cache, so the same
                // image is not re-encoded when the chip is "auto" itself.
                _residual = outcome.Result.Structure.Residual;
                _mapCache.Clear();
                _mapCache[MapScale.Options[0]] = outcome.Png;
                RefreshMap();

                HasResult = true;

                // The automatic encoding always goes to disk, whichever chip is ticked. See the
                // note at the top: the console writes that, and the file must be the same.
                Save(session, outcome.Png);
            });
        }

        /// <summary>
        /// Rebuilds the map for the current scale.
        ///
        /// It is encoded on the UI thread, and that was measured, not assumed: on the real 24 MP
        /// masters (analysis raster 1504x1004) a cap costs 90-255 ms, once, because afterwards it
        /// comes from the cache. Moving it to Task.Run would have needed a generation counter so
        /// an old result does not land over a new one - more complexity than the delay it
        /// removes.
        /// </summary>
        void RefreshMap()
        {
            if (_residual == null) return;

            var range = MapScale.RangeFor(_residual);
            if (!_mapCache.TryGetValue(MapScale.Selected, out var png))
            {
                png = PngWriter.EncodeResidualMap(_residual, range);
                _mapCache[MapScale.Selected] = png;
            }

            Map = png;
            MapLegend = RunReport.MapLegend(range);
        }

        /// <summary>Shows a run's outcome: the structure rows, the combined verdict and its two
        /// parts, and the profile card or the reason it is empty. Sets
        /// <see cref="TabViewModel.HasResult"/>; the map and the report are the caller's.</summary>
        /// <param name="o">The run, with or without a comparison.</param>
        /// <param name="gap">Without a comparison in the run: a comparison that says why it could
        /// not; null otherwise.</param>
        internal void Populate(StructureOutcome o, StructureOutcome gap = null)
        {
            Populate(o.Structure);
            // Internal and with HasResult, so the rendering test can show the tab from a result,
            // without a session - as on the smoothing tab.
            HasResult = true;

            // The run's verdict - the worse of shape and depth - and its advice, which is the
            // depth's when the depth made it worse. The texts are the report's.
            Verdict = o.Verdict;
            Advice = StructureReport.Advice(o);
            // On three lines, with the labels without the console's padding: the parts come from
            // the report.
            VerdictDetail = o.Compared
                ? T($"shape: {StructureReport.ShapeDetail(o)}\ndepth: {StructureReport.DepthDetail(o)}\n{StructureReport.WorseOfBoth}",
                    $"forma: {StructureReport.ShapeDetail(o)}\nadancimea: {StructureReport.DepthDetail(o)}\n{StructureReport.WorseOfBoth}")
                : "";

            StructureOutcome explained = o.Profiles != null ? o : gap;
            if (explained != null && explained.Compared)
            {
                VignettingComparison c = explained.Profiles;
                ProfileGap = "";
                ProfileMaster = StructureReport.MasterDepth(c);
                ProfileRows = StructureReport.ProfileRows(c).Select(r => new LensProfileRow
                {
                    Source = r.Source,
                    Lens = r.Lens,
                    Where = r.Where,
                    Profile = r.Profile,
                    Depth = r.Depth,
                    Diff = r.Diff,
                    Error = r.Error,
                    Verdict = r.Verdict,
                    IsNearest = r.IsNearest,
                    Note = (r.IsNearest ? T("The nearest profile - it gives the verdict. ", "Cel mai apropiat profil - da verdictul. ") : "") + r.Note,
                    IsWarning = r.IsWarning,
                }).ToList();
                ProfileSummary = StructureReport.ComparisonSummary(c).Replace('\n', ' ');
                ProfileScale = StructureReport.DepthScaleRows()
                    .Select(s => new ScaleRow { Band = s.Band, Range = s.Range, Meaning = s.Meaning, IsCurrent = s.Band == c.Verdict })
                    .ToList();
                ProfileScaleNote = StructureReport.DepthScaleNote().Replace('\n', ' ') + " "
                                 + StructureReport.DepthLightScaleNote(c.Nearest.DiffStops).Replace('\n', ' ');
                ProfileNote = StructureReport.ComparisonNote().Replace('\n', ' ');
            }
            else
            {
                ProfileRows = Array.Empty<LensProfileRow>();
                ProfileScale = Array.Empty<ScaleRow>();
                ProfileMaster = ProfileSummary = ProfileScaleNote = ProfileNote = "";
                ProfileGap = explained == null ? ""
                    : T("Cannot compare: ", "Nu se poate compara: ") + StructureReport.GapText(explained) + "\n" + StructureReport.GapMeaning().Replace('\n', ' ');
            }
        }

        /// <summary>Shows the five tests' side: size, levels, the test and informative rows, the
        /// spot warning, the residual scale with the verdict's band marked, the verdict and its
        /// advice.</summary>
        /// <param name="r">The structure result.</param>
        void Populate(StructureResult r)
        {
            Size = T($"{r.Width} x {r.Height} px  (after 2x2 + downsample)", $"{r.Width} x {r.Height} px  (dupa 2x2 + downsample)");
            // The formatting comes from the core: on a float master written by Siril, "F0" gave
            // "0 / 0", and the window and the console must say the same thing.
            Levels = $"median / max ADU  {StructureReport.Levels(r)}";

            // The five rows are built in StructureRows, where the Netezire tab takes them from too:
            // it is the same battery, and two compositions of it have already diverged once.
            Tests.Clear();
            foreach (var row in StructureRows.Tests(r)) Tests.Add(row);

            // The informative rows come from StructureRows too, also for both tabs.
            Informative.Clear();
            foreach (var row in StructureRows.Informative(r)) Informative.Add(row);

            // The warning, not a verdict: the text and the threshold come from the core.
            LocalSpotWarning = StructureReport.LocalSpotWarning(r) ?? "";

            Scale.Clear();
            foreach (var (band, range, meaning) in StructureReport.Scale())
                Scale.Add(new ScaleRow
                {
                    Band = band,
                    Range = range,
                    Meaning = meaning,
                    IsCurrent = band == r.Verdict,
                });

            Verdict = r.Verdict;
            Advice = StructureReport.Advice(r.Verdict);
        }

        /// <summary>
        /// The report and the map, in the session folder. A write failure does not cancel the
        /// result - the figures are already computed and shown.
        /// </summary>
        /// <param name="session">The session folder; its result and map paths are written.</param>
        /// <param name="png">The map on the automatic scale.</param>
        void Save(SirilSession session, byte[] png)
        {
            SavedTo = "";
            SaveError = "";
            try
            {
                File.WriteAllText(session.ResultTextPath, Report);
                File.WriteAllBytes(session.ResidualPngPath, png);
                SavedTo = session.ResultTextPath;
                Log.Info("Structure tab: wrote " + session.ResultTextPath + " and " + session.ResidualPngPath + " (" + LogText.KiB(png.Length) + ")");
            }
            catch (Exception e)
            {
                Log.Warn(e, "Structure tab: the report or the map could not be saved beside the session");
                SaveError = T("Could not save the report:\n", "Nu am putut salva raportul:\n") + Describe(e);
            }
        }

        /// <inheritdoc/>
        internal override void Reset()
        {
            base.Reset();
            Source = "";
            SourceWarning = "";
            Size = "";
            Levels = "";
            Verdict = "";
            Advice = "";
            LocalSpotWarning = "";
            VerdictDetail = "";
            ProfileRows = Array.Empty<LensProfileRow>();
            ProfileScale = Array.Empty<ScaleRow>();
            ProfileMaster = ProfileSummary = ProfileScaleNote = ProfileNote = ProfileGap = "";
            SavedTo = "";
            SaveError = "";
            Map = null;
            MapLegend = "";
            // The raster and the encodings go; the choice of scale does NOT - it is a viewing
            // preference, not a result, so it must survive the change of folder.
            _residual = null;
            _mapCache.Clear();
            Tests.Clear();
            Informative.Clear();
            Scale.Clear();
        }
    }
}
