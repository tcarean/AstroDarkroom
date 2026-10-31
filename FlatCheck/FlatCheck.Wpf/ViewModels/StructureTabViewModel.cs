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
        string _adobeMissing = "", _adobeSearched = "";
        bool _noFilters = true;
        string _filteredNote = "";
        IReadOnlyList<LensOption> _lensOptions = Array.Empty<LensOption>();
        LensOption _selectedLens;
        string _lensChoicePrompt = "";

        /// <summary>The folder's remembered switches and chosen lens (R-119, R-121);
        /// <see cref="SessionSettings.None"/> before a folder and in the tests.</summary>
        SessionSettings _settings = SessionSettings.None;

        /// <summary>
        /// What a flip of the box needs to redo the comparison without a load (R-118): the
        /// folder, its token, the five tests' result, the master's path and the raw's. Null
        /// before the first run and after Reset.
        /// </summary>
        SirilSession _session;
        CancellationToken _ct;
        StructureResult _structure;
        string _masterPath, _rawPath;
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

        /// <summary>
        /// The box at the top of the card (R-118): true, the default, when no filter was on the
        /// lens, so the vignetting is compared with the lens profile. Flipping it writes the
        /// folder's settings (R-119) and, with a result on screen, redoes the comparison and the
        /// verdict on the kept result - the master is not loaded again.
        /// </summary>
        public bool NoFilters
        {
            get => _noFilters;
            set
            {
                if (!Set(ref _noFilters, value)) return;
                _settings.NoFilters = value;
                _settings.Write();
                Pending = RecompareAsync();
            }
        }

        /// <summary>The recompare the last flip of the box started, so a test can await it;
        /// completed when none is running. The box is disabled while the tab is busy, so two
        /// never overlap from the screen.</summary>
        internal Task Pending { get; private set; } = Task.CompletedTask;

        /// <summary>Why nothing is compared when the box is off (R-118), under the master's
        /// depth; empty when it is on.</summary>
        public string FilteredNote { get => _filteredNote; private set => Set(ref _filteredNote, value); }

        /// <summary>
        /// The lens picker's entries (R-121): the no-choice entry first, then one per candidate
        /// in the rows' order, when the run compared a list found by range alone with more than
        /// one lens; empty otherwise, and the picker is off the card.
        /// </summary>
        public IReadOnlyList<LensOption> LensOptions
        {
            get => _lensOptions;
            private set { if (Set(ref _lensOptions, value)) Raise(nameof(OffersLensChoice)); }
        }

        /// <summary>True when the card shows the lens picker (R-121).</summary>
        public bool OffersLensChoice => _lensOptions.Count > 0;

        /// <summary>The line above the picker, from the core: why there is a choice to make and
        /// what it does (R-121); empty without a picker.</summary>
        public string LensChoicePrompt { get => _lensChoicePrompt; private set => Set(ref _lensChoicePrompt, value); }

        /// <summary>
        /// The picker's selection (R-121): the entry of the chosen lens, the no-choice entry when
        /// none is chosen, null without a picker. A pick on screen writes the folder's settings
        /// (R-119) and redoes the comparison on the kept result with that lens alone, as a flip
        /// of the box does; a run sets it from the outcome without writing or recomparing.
        /// </summary>
        public LensOption SelectedLens
        {
            get => _selectedLens;
            set
            {
                // The ComboBox pushes null through the binding whenever LensOptions is replaced
                // (every Populate): that is not a pick, and the field must not lose the entry
                // ShowLensPicker is about to set, so a null never reaches the field from here.
                if (value == null || !Set(ref _selectedLens, value)) return;
                _settings.Lens = value.Key;
                _settings.Write();
                Pending = RecompareAsync();
            }
        }

        /// <summary>The red paragraph when the run's search found no Adobe profile (R-113);
        /// empty otherwise, and when the run searched nothing.</summary>
        public string AdobeMissing { get => _adobeMissing; private set => Set(ref _adobeMissing, value); }

        /// <summary>What to install and that FlatCheck must restart; on screen only with
        /// <see cref="AdobeMissing"/>.</summary>
        public string AdobeInstall => StructureReport.AdobeInstall();

        /// <summary>Adobe's DNG Converter page, the card's link.</summary>
        public string AdobeLink => StructureReport.AdobeDngConverterUrl;

        /// <summary>The folders searched; empty whenever <see cref="AdobeMissing"/> is.</summary>
        public string AdobeSearched { get => _adobeSearched; private set => Set(ref _adobeSearched, value); }

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
        /// <param name="settings">The folder's remembered switches (R-119), whose "no filters"
        /// the box takes before the run; null for none remembered.</param>
        /// <returns>A task that completes when the run ended; it never faults.</returns>
        internal Task RunAsync(SirilSession session, CancellationToken ct, SessionSettings settings = null)
        {
            _session = session;
            _ct = ct;
            _settings = settings ?? SessionSettings.None;
            // The box takes the folder's value without writing it back and without a recompare:
            // there is nothing on screen yet, and the run below reads it. Without settings (a
            // test, a run with nothing remembered) the box stays as it was set.
            if (settings != null)
            {
                _noFilters = settings.NoFilters;
                Raise(nameof(NoFilters));
            }

            string rawPath = session.ReferenceRawPath;
            _rawPath = rawPath;
            bool compares = Compares(rawPath);
            return RunGuardedAsync(RunMessage(compares), async () =>
            {
                FlatMasterLookup master = session.FindFlatMaster();
                string path = master.Path;
                _masterPath = path;
                SourceWarning = master.Ambiguous ? SessionMessages.TwoFlatMasters(master) : "";
                var text = new StringWriter();

                // With a raw beside the master and the box on, the run is the console's with
                // --raw: the shared catalog, the raw from flats/, the comparison in the same
                // report. Without one, or with the box off, the plain run.
                StructureQuery query = compares ? await QueryAsync(rawPath, ct).ConfigureAwait(true) : null;

                var (outcome, gap) = await Task.Run(() =>
                {
                    RunReport.WriteBanner(text);
                    var run = StructureRun.Execute(path, text, query);
                    RunReport.WriteMapSaved(session.ResidualPngPath, text);
                    ct.ThrowIfCancellationRequested();
                    return (run, Gap(run.Result, path, rawPath));
                }, ct).ConfigureAwait(true);
                // Back on the UI thread: the folder may have been closed meanwhile (Reset nulls
                // the session), and then nothing of this run lands on screen or on disk.
                if (!ReferenceEquals(_session, session))
                {
                    // RunGuardedAsync's own line then says the run ended; this one says where its
                    // result went.
                    Log.Debug("run dropped: the folder was closed while the structure was analysed");
                    return;
                }

                Source = path;
                Report = text.ToString();
                _structure = outcome.Result.Structure;
                Populate(outcome.Result, gap, query?.Sources);

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
        /// Redoes the comparison and the verdict on the result already on screen, after a flip
        /// of the box (R-118): the console's report for the new query is written again and
        /// saved; the five tests, the raster and the map are kept. Nothing before the first run,
        /// after Reset, or while the folder's token is cancelled.
        /// </summary>
        /// <returns>A task that completes when the recompare ended; it never faults.</returns>
        internal Task RecompareAsync()
        {
            SirilSession session = _session;
            StructureResult structure = _structure;
            string path = _masterPath, rawPath = _rawPath;
            CancellationToken ct = _ct;
            if (session == null || structure == null || ct.IsCancellationRequested) return Task.CompletedTask;

            bool compares = Compares(rawPath);
            return RunGuardedAsync(RecompareMessage(compares), async () =>
            {
                StructureQuery query = compares ? await QueryAsync(rawPath, ct).ConfigureAwait(true) : null;
                var text = new StringWriter();

                var (outcome, gap) = await Task.Run(() =>
                {
                    RunReport.WriteBanner(text);
                    StructureOutcome o = StructureRun.Recompare(structure, _loadInfo(path), path, text, query);
                    RunReport.WriteMapSaved(session.ResidualPngPath, text);
                    ct.ThrowIfCancellationRequested();
                    return (o, Gap(o, path, rawPath));
                }, ct).ConfigureAwait(true);
                // The folder may have been closed while the comparison ran: then this result is
                // nobody's, and it neither lands on the next folder's tab nor rewrites a file.
                if (!ReferenceEquals(_session, session))
                {
                    Log.Debug("recompare dropped: the folder was closed while the vignetting was compared");
                    return;
                }

                Report = text.ToString();
                Populate(outcome, gap, query?.Sources);
                HasResult = true;
                SaveReport(session);
            });
        }

        /// <summary>Whether a run compares with the profiles: a raw beside the master, a catalog,
        /// and the box on.</summary>
        /// <param name="rawPath">The raw in flats/, or null.</param>
        /// <returns>True when the run is the console's <c>--raw</c> one.</returns>
        bool Compares(string rawPath) => rawPath != null && _catalog != null && NoFilters;

        /// <summary>The busy overlay's line for a run.</summary>
        /// <param name="compares">Whether the run looks for the lens profile too.</param>
        /// <returns>The line, in the interface language.</returns>
        static string RunMessage(bool compares) => compares
            ? T("Analysing the structure of the flat master and looking for the lens profile...",
                "Analizez structura masterului de flat si caut profilul obiectivului...")
            : T("Analysing the structure of the flat master...", "Analizez structura masterului de flat...");

        /// <summary>The busy overlay's line for a recompare after a flip of the box, which
        /// loads no master: it says what it does, not what the run did.</summary>
        /// <param name="compares">Whether the recompare looks for the lens profile.</param>
        /// <returns>The line, in the interface language.</returns>
        static string RecompareMessage(bool compares) => compares
            ? T("Comparing the vignetting with the lens profile...", "Compar vignetarea cu profilul obiectivului...")
            : T("Redoing the verdict without the comparison...", "Refac verdictul fara comparatie...");

        /// <summary>The console's <c>--raw</c> query on the shared catalog, awaited (a second or
        /// two on the first open).</summary>
        /// <param name="rawPath">The raw in flats/.</param>
        /// <param name="ct">The folder's token.</param>
        /// <returns>The query for the run.</returns>
        async Task<StructureQuery> QueryAsync(string rawPath, CancellationToken ct)
        {
            ProfileSources sources = await _catalog.EnsureAsync().ConfigureAwait(true);
            ct.ThrowIfCancellationRequested();
            // The folder's chosen lens (R-121) rides on the query; the core ignores a key that
            // matches no candidate, and Populate then selects the no-choice entry.
            return new StructureQuery { Sources = sources, RawPath = rawPath, LoadInfo = _loadInfo, LensKey = _settings.Lens };
        }

        /// <summary>
        /// Without a comparison in the run, the reason for the card: the same core says why, on
        /// the master's metadata (the header only) and without a catalog. With the box off there
        /// is no reason to give - the card says why in its own words - and nothing is read.
        /// </summary>
        /// <param name="o">The run's outcome.</param>
        /// <param name="path">The master's path.</param>
        /// <param name="rawPath">The raw in flats/, or null.</param>
        /// <returns>The explaining comparison, or null.</returns>
        StructureOutcome Gap(StructureOutcome o, string path, string rawPath)
            => o.Profiles != null || !NoFilters ? null
             : StructureRun.Compare(o.Structure, _loadInfo(path), new StructureQuery { RawPath = rawPath, LoadInfo = _loadInfo });

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
        /// parts, the profile card or the reason it is empty, and what the profile search left
        /// out of it. Sets <see cref="TabViewModel.HasResult"/>; the map and the report are the
        /// caller's.</summary>
        /// <param name="o">The run, with or without a comparison.</param>
        /// <param name="gap">Without a comparison in the run: a comparison that says why it could
        /// not; null otherwise.</param>
        /// <param name="searched">The profile sources the run searched; null when it searched
        /// none (no raw in flats/), so nothing is said about Adobe.</param>
        internal void Populate(StructureOutcome o, StructureOutcome gap = null, ProfileSources searched = null)
        {
            Populate(o.Structure);
            // Internal and with HasResult, so the rendering test can show the tab from a result,
            // without a session - as on the Synthetic tab.
            HasResult = true;

            // A search that found no Adobe file says so above the card (R-113); the core decides.
            AdobeMissing = StructureReport.AdobeMissing(searched);
            AdobeSearched = StructureReport.AdobeSearched(searched);

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
                FilteredNote = "";
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
                    Note = (!r.IsNearest ? ""
                            : explained.Shot?.ChosenLens != null ? T("The lens you chose - it gives the verdict. ", "Obiectivul ales de tine - da verdictul. ")
                            : T("The nearest profile - it gives the verdict. ", "Cel mai apropiat profil - da verdictul. ")) + r.Note,
                    IsWarning = r.IsWarning,
                }).ToList();
                ProfileSummary = StructureReport.ComparisonSummary(c).Replace('\n', ' ');
                ProfileScale = StructureReport.DepthScaleRows()
                    .Select(s => new ScaleRow { Band = s.Band, Range = s.Range, Meaning = s.Meaning, IsCurrent = s.Band == c.Verdict })
                    .ToList();
                ProfileScaleNote = StructureReport.DepthScaleNote().Replace('\n', ' ') + " "
                                 + StructureReport.DepthLightScaleNote(c.Nearest.DiffStops).Replace('\n', ' ');
                ProfileNote = StructureReport.ComparisonNote().Replace('\n', ' ');
                ShowLensPicker(explained.Shot);
            }
            else
            {
                ProfileRows = Array.Empty<LensProfileRow>();
                ProfileScale = Array.Empty<ScaleRow>();
                ProfileSummary = ProfileScaleNote = ProfileNote = "";
                // On a gap the picker stays only when the gap is the choice's doing: a chosen
                // lens whose profile makes no row must still be undone from the screen, or the
                // choice kept in flatcheck.json would lock the card on the gap. A gap nothing was
                // chosen for (no f-stop in the raw, no crop for any candidate) is not cured by a
                // pick, so the picker goes with the rows; with the box off nothing was identified.
                ShowLensPicker(NoFilters && explained?.Shot?.ChosenLens != null ? explained.Shot : null);
                // With the box off (R-118): the master's own depth, for information, and why
                // nothing is compared - both from the core. With it on: the gap, when known.
                ProfileMaster = NoFilters ? "" : StructureReport.MasterDepth(o.Structure);
                FilteredNote = NoFilters ? "" : StructureReport.FilteredLensNote();
                ProfileGap = explained == null || !NoFilters ? ""
                    : T("Cannot compare: ", "Nu se poate compara: ") + StructureReport.GapText(explained) + "\n" + StructureReport.GapMeaning().Replace('\n', ' ');
            }
        }

        /// <summary>
        /// Fills or empties the lens picker (R-121) from the compared shot: the no-choice entry
        /// and one entry per candidate when the list came by range alone with more than one
        /// lens, the selection on the chosen lens or on the no-choice entry; nothing otherwise.
        /// The selection is set on the field, not through the setter: a run or a recompare
        /// shows the choice, it does not make one.
        /// </summary>
        /// <param name="shot">The compared shot; null empties the picker.</param>
        void ShowLensPicker(ShotIdentity shot)
        {
            // The core says whether there is a choice to make (ShotIdentity.OffersLensChoice),
            // and the prompt speaks exactly then.
            if (shot == null || !shot.OffersLensChoice)
            {
                LensChoicePrompt = "";
                LensOptions = Array.Empty<LensOption>();
                _selectedLens = null;
                Raise(nameof(SelectedLens));
                return;
            }
            LensChoicePrompt = StructureReport.LensChoicePrompt(shot);
            var options = new List<LensOption>
            {
                new LensOption { Key = null, Label = T("the nearest profile decides", "decide cel mai apropiat profil") },
            };
            options.AddRange(shot.Candidates.Select(c => new LensOption { Key = c.Key, Label = $"{c.SourceLabel} {c.Name} [{c.Where}]" }));
            LensOptions = options;
            _selectedLens = options.FirstOrDefault(o => o.Key == shot.ChosenLens?.Key) ?? options[0];
            Raise(nameof(SelectedLens));
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

            // The five rows are built in StructureRows, not here: two compositions of the same
            // battery have already diverged once.
            Tests.Clear();
            foreach (var row in StructureRows.Tests(r)) Tests.Add(row);

            // The informative rows come from StructureRows too.
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
            if (!SaveReport(session)) return;
            try
            {
                File.WriteAllBytes(session.ResidualPngPath, png);
                Log.Info("wrote " + session.ResidualPngPath + " (" + LogText.KiB(png.Length) + ")");
            }
            catch (Exception e)
            {
                Log.Warn(e, "the map could not be saved beside the session");
                SaveError = T("Could not save the report:\n", "Nu am putut salva raportul:\n") + Describe(e);
            }
        }

        /// <summary>The report alone, in the session folder: after a recompare (R-118) the map
        /// on disk is already the right one. A write failure is shown apart, as above.</summary>
        /// <param name="session">The session folder; its result path is written.</param>
        /// <returns>True when the file was written.</returns>
        bool SaveReport(SirilSession session)
        {
            SavedTo = "";
            SaveError = "";
            try
            {
                File.WriteAllText(session.ResultTextPath, Report);
                SavedTo = session.ResultTextPath;
                Log.Info("wrote " + session.ResultTextPath + " (" + LogText.FileKiB(session.ResultTextPath) + ")");
                return true;
            }
            catch (Exception e)
            {
                Log.Warn(e, "the report could not be saved beside the session");
                SaveError = T("Could not save the report:\n", "Nu am putut salva raportul:\n") + Describe(e);
                return false;
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
            AdobeMissing = AdobeSearched = "";
            FilteredNote = "";
            ShowLensPicker(null);
            SavedTo = "";
            SaveError = "";
            Map = null;
            MapLegend = "";
            // The raster and the encodings go; the choice of scale does NOT - it is a viewing
            // preference, not a result, so it must survive the change of folder.
            _residual = null;
            _mapCache.Clear();
            // What a recompare needs goes with the result; the box stays as it is, the next
            // folder's file sets it (R-119).
            _session = null;
            _structure = null;
            _masterPath = _rawPath = null;
            _settings = SessionSettings.None;
            Tests.Clear();
            Informative.Clear();
            Scale.Clear();
        }
    }
}
