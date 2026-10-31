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

// The Sintetic tab: a flat written from the lens's vignetting profile, and the comparison of the
// photographed master with it. On the model of the Netezire tab: nothing runs by itself, the
// button writes a file beside the master after the overwrite question, the file does not become
// the session's master.
//
// What the tab decides: nothing. The lens list comes from ProfileLocator (Adobe from the machine,
// lensfun/ from beside the exe), the choice of profile and the figures from SyntheticRun, the
// comparison from AgainstRun. What it does beyond the console is the PREFILL: the focal length and
// the camera from the master's header (the header only, without the 96 MB), the aperture and the
// lens's ID and name from a raw in flats/, when there is one - and the lens chosen from the
// list by them. The rule is the core's (LensIdentification), the same one the Structure tab and the
// console apply; here only the metadata are read (ImageLoader.LoadInfo, no pixel) and the values
// put in the fields. Every field stays editable; what the user wrote beats what was read.
//
// The catalog is loaded once, in the background, at the first opening of a session: hundreds of
// .lcp files to parse mean a second or two, which must not hold the window.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static FlatCheck.Ui;

namespace FlatCheck.Wpf
{
    /// <summary>
    /// A row of the lens list. Public for the binding; the core's choice stays internal.
    /// Observable for <see cref="Details"/>, which arrives later: an Adobe file is parsed in the
    /// background, and the row is completed without rebuilding the list (and losing the
    /// selection).
    /// </summary>
    public sealed class LensRow : ObservableObject
    {
        string _details = "";

        public string Label { get; init; }
        public string SourceLabel { get; init; }
        /// <summary>The mount, plus the calibration format for lensfun
        /// (LensChoice.Where).</summary>
        public string Where { get; init; }
        internal LensChoice Choice { get; init; }

        /// <summary>Focal lengths, apertures, distances, number of profiles
        /// (SyntheticReport.LensDetails); empty on a long list or until it arrives.</summary>
        public string Details { get => _details; internal set { if (Set(ref _details, value ?? "")) Raise(nameof(HasDetails)); } }
        public bool HasDetails => _details.Length > 0;
    }

    /// <summary>The Sintetic tab's collapsible note on where the profiles come from; the text
    /// from <see cref="SessionMessages"/>.</summary>
    public sealed class SyntheticGuideViewModel : ObservableObject
    {
        bool _expanded;   // collapsed by default: it is documentation, not a precondition
        public string Intro => SessionMessages.SyntheticGuideIntro();
        public string Title => SessionMessages.SyntheticSourcesTitle();
        public bool IsExpanded { get => _expanded; set => Set(ref _expanded, value); }
    }

    /// <summary>The Sintetic tab: a lens from the profile list, its focal length, aperture and
    /// crop prefilled from the session, the synthetic flat written beside the master on a button,
    /// and the master compared with it.</summary>
    public sealed class SyntheticTabViewModel : TabViewModel
    {
        SirilSession _session;
        CancellationToken _ct;
        HeavyRunGate _gate;
        readonly Func<string, bool> _confirm;
        readonly ProfileCatalog _catalog;
        /// <summary>A file's metadata without pixels; in the tests, a made-up frame in place of a
        /// raw.</summary>
        readonly Func<string, LoadedFrame> _loadInfo;
        ProfileSources _sources;

        string _source = "", _target = "";
        bool _canRun;
        string _search = "";
        IReadOnlyList<LensRow> _lenses = Array.Empty<LensRow>();
        LensRow _selectedLens;
        string _focalText = "", _fstopText = "", _cropText = "", _cropNote = "", _formatNote = "";
        bool _formatWarning, _cornerWarning;
        bool _cropFromChip;

        // True while CropNote is the "crop unknown" note (SessionMessages.SyntheticCropUnknown):
        // an emptied field shows it again. A flag, not a match on the note's words, which
        // differ between the languages.
        bool _cropUnknown;
        string _cropSource;
        int _masterW, _masterH;
        int _formatGen;
        int _detailsGen;
        string _listNote = "";
        string _requestNote = "", _writtenRequestNote = "";
        string _prefillNote = "";
        string _adobeWhere = "", _lensfunWhere = "", _profilesSummary = "";
        string _profileLine = "", _modelLine = "", _cornerNote = "";
        IReadOnlyList<MetricRow> _depth = Array.Empty<MetricRow>();
        string _savedTo = "", _saveError = "";
        IReadOnlyList<CompareRow> _compareTable = Array.Empty<CompareRow>();
        string _compareVerdict = "", _compareVerdictDetail = "";
        byte[] _compareMap;
        string _mapLegend = "";
        Img _compareResidual;
        readonly Dictionary<MapScaleOption, byte[]> _mapCache = new();

        /// <summary>A tab with a catalog of its own, for the tests that need no shared
        /// one.</summary>
        /// <param name="confirm">Asks the user a yes/no question (overwriting the synthetic flat);
        /// null answers yes.</param>
        /// <param name="locate">Finds the profile sources for the tab's own catalog; null for the
        /// real lookup.</param>
        /// <param name="loadInfo">Reads a file's header without its pixels; null uses
        /// <see cref="ImageLoader.LoadInfo"/>.</param>
        internal SyntheticTabViewModel(Func<string, bool> confirm = null, Func<ProfileSources> locate = null,
                                       Func<string, LoadedFrame> loadInfo = null)
            : this(confirm, new ProfileCatalog(locate), loadInfo) { }

        /// <summary>A tab on the window's shared catalog, with its commands and listeners
        /// wired.</summary>
        /// <param name="confirm">Asks the user a yes/no question (overwriting the synthetic flat);
        /// null answers yes.</param>
        /// <param name="catalog">The window's shared catalog (MainViewModel gives it to the
        /// Structure tab too); null makes one of its own.</param>
        /// <param name="loadInfo">Reads a file's header without its pixels; null uses
        /// <see cref="ImageLoader.LoadInfo"/>.</param>
        internal SyntheticTabViewModel(Func<string, bool> confirm, ProfileCatalog catalog, Func<string, LoadedFrame> loadInfo)
        {
            _confirm = confirm ?? (_ => true);
            _catalog = catalog ?? new ProfileCatalog();
            _loadInfo = loadInfo ?? ImageLoader.LoadInfo;
            Run = new RelayCommand(() => Pending = RunAsync(), () => CanRun && IsIdle && SelectedLens != null);
            Compare = new RelayCommand(() => Pending = CompareAsync(), () => CanCompare && IsIdle);
            MapScale.Changed += RefreshMap;
            SensorFormat.Chosen += f =>
            {
                _cropFromChip = true;
                try { CropText = f.CropText; }
                finally { _cropFromChip = false; }
                CropNote = SessionMessages.SyntheticCropFromFormat(f.Label, f.CropText);
                _cropUnknown = false;
                RefreshFormat();
            };
        }

        // ------------------------------------------------------------ what is bound

        public string Source { get => _source; private set => Set(ref _source, value); }
        public string Target { get => _target; private set => Set(ref _target, value); }

        public bool CanRun
        {
            get => _canRun;
            private set { if (Set(ref _canRun, value)) RaiseCommands(); }
        }

        public bool CanCompare => CanRun && !string.IsNullOrEmpty(Target) && File.Exists(Target);

        internal RelayCommand Run { get; }
        internal RelayCommand Compare { get; }
        public System.Windows.Input.ICommand RunCommand => Run;
        public System.Windows.Input.ICommand CompareCommand => Compare;
        public string ButtonLabel => SessionMessages.SyntheticButtonLabel();
        public string CompareLabel => SessionMessages.SyntheticCompareLabel();

        public SyntheticGuideViewModel Guide { get; } = new();

        public string Search
        {
            get => _search;
            set { if (Set(ref _search, value ?? "")) RefreshLenses(); }
        }

        public IReadOnlyList<LensRow> Lenses { get => _lenses; private set { if (Set(ref _lenses, value)) RefreshDetails(); } }

        /// <summary>Why the rows have no details: the list is too long
        /// (SyntheticReport.ListDetailsHidden). Empty when they have them.</summary>
        public string ListNote { get => _listNote; private set { if (Set(ref _listNote, value ?? "")) Raise(nameof(HasListNote)); } }
        public bool HasListNote => _listNote.Length > 0;

        /// <summary>The computation behind the rows' details, so a test can await it.</summary>
        internal Task DetailsPending { get; private set; } = Task.CompletedTask;

        /// <summary>
        /// Fills each row's second line with its focal lengths, apertures and distances - the
        /// console's rule (--lenses): only when the list has at most ListDetailsMax rows, because
        /// an Adobe lens's details need its file parsed (~10 ms each, 36 s for all). The same rule
        /// for lensfun too, though it would be free there, so the list does not show half with
        /// details, half without. In the background, in parallel, with a generation number: a new
        /// search does not receive the old one's details.
        /// </summary>
        void RefreshDetails() => DetailsPending = RefreshDetailsAsync();

        /// <summary>The work behind <see cref="RefreshDetails"/>: the details of every row,
        /// computed in parallel off the UI thread, applied only if no newer list arrived
        /// meanwhile.</summary>
        /// <returns>A task that completes when the details were applied or dropped; it never
        /// faults.</returns>
        async Task RefreshDetailsAsync()
        {
            int gen = ++_detailsGen;
            IReadOnlyList<LensRow> rows = Lenses;
            ProfileSources sources = _sources;
            if (sources == null || rows.Count == 0) { ListNote = ""; return; }
            if (rows.Count > SyntheticReport.ListDetailsMax)
            {
                ListNote = SyntheticReport.ListDetailsHidden(rows.Count) + T(" - type a text in the lens field", " - scrie un text in campul obiectiv");
                return;
            }
            ListNote = "";
            List<(LensRow Row, string Details)> details;
            try
            {
                details = await Task.Run(() => rows.AsParallel().AsOrdered()
                    .Select(r => (r, Safe(() => SyntheticReport.LensDetails(r.Choice, sources.Catalog)))).ToList()).ConfigureAwait(true);
            }
            catch (Exception e)
            {
                Log.Debug(e, "the lens list's details could not be computed; the rows stay without");
                return;
            }
            if (gen != _detailsGen) return;
            foreach (var (row, text) in details) row.Details = text;
        }

        /// <summary>Runs a text computation that may throw.</summary>
        /// <param name="f">The computation.</param>
        /// <returns>Its text, or "" when it threw.</returns>
        string Safe(Func<string> f)
        {
            try { return f(); }
            catch (Exception e)
            {
                // A broken Adobe file leaves the row without details; choosing it will say why.
                Log.Debug(e, "a lens row's details could not be read");
                return "";
            }
        }

        public LensRow SelectedLens
        {
            get => _selectedLens;
            set { if (Set(ref _selectedLens, value)) { RaiseCommands(); RefreshFormat(); } }
        }

        public string FocalText { get => _focalText; set { if (Set(ref _focalText, value ?? "")) RefreshFormat(); } }
        public string FStopText { get => _fstopText; set { if (Set(ref _fstopText, value ?? "")) RefreshFormat(); } }
        public string CropText
        {
            get => _cropText;
            set
            {
                if (!Set(ref _cropText, value ?? "")) return;
                if (!_cropFromChip)
                {
                    // The chips follow the value; the note says where it is from and what format
                    // it is. The source is set by whoever writes into the field (the table, at
                    // the prefill); without it, it was typed.
                    double? crop = Num(_cropText);
                    SensorFormat.Follow(crop);
                    string source = _cropSource ?? SessionMessages.SyntheticCropTyped();
                    _cropSource = null;
                    CropNote = crop.HasValue ? SessionMessages.SyntheticCropNote(_cropText.Trim(), source, SensorFormats.LabelOf(crop.Value))
                             : _cropText.Trim().Length > 0 ? "" : _cropUnknown ? _cropNote : "";
                    if (crop.HasValue || _cropText.Trim().Length > 0) _cropUnknown = false;
                }
                RefreshFormat();
            }
        }

        /// <summary>The sensor-format chips: they show the format of the crop in the field, and
        /// can set it when it does not come from the table.</summary>
        public SensorFormatViewModel SensorFormat { get; } = new();

        /// <summary>Where the crop in the field is from and what format it is; or that it is not
        /// known yet.</summary>
        public string CropNote { get => _cropNote; private set => Set(ref _cropNote, value); }

        /// <summary>The chosen profile against your sensor: the calibration format, yours, and
        /// what follows. Empty without a lens.</summary>
        public string FormatNote { get => _formatNote; private set => Set(ref _formatNote, value); }

        /// <summary>True when FormatNote is a warning (a large extrapolation): it is
        /// coloured.</summary>
        public bool FormatWarning { get => _formatWarning; private set => Set(ref _formatWarning, value); }

        /// <summary>
        /// When the focal length or the aperture in the boxes is far from the profile that would
        /// be used (SyntheticReport.ProfileMismatch): always a warning, empty when it does not
        /// apply. Recomputed together with FormatNote, at every keystroke.
        /// </summary>
        public string RequestNote { get => _requestNote; private set { if (Set(ref _requestNote, value ?? "")) Raise(nameof(HasRequestNote)); } }
        public bool HasRequestNote => _requestNote.Length > 0;

        /// <summary>The same warning for the written flat, under "profil folosit"; empty when the
        /// request was within tolerance.</summary>
        public string WrittenRequestNote { get => _writtenRequestNote; private set { if (Set(ref _writtenRequestNote, value ?? "")) Raise(nameof(HasWrittenRequestNote)); } }
        public bool HasWrittenRequestNote => _writtenRequestNote.Length > 0;

        /// <summary>The same thing for the line under the written depths.</summary>
        public bool CornerWarning { get => _cornerWarning; private set => Set(ref _cornerWarning, value); }

        /// <summary>The computation behind FormatNote, so a test can await it.</summary>
        internal Task FormatPending { get; private set; } = Task.CompletedTask;

        /// <summary>
        /// Recomputes FormatNote: the chosen lens's nearest profile (an Adobe file is parsed only
        /// now, a single one), set against the crop in the field on the master's frame. In the
        /// background, with a generation number: only the last request reaches the screen, as on
        /// the other tabs.
        /// </summary>
        void RefreshFormat() => FormatPending = RefreshFormatAsync();

        /// <summary>The work behind <see cref="RefreshFormat"/>: the format note, its warning flag
        /// and the request note, computed off the UI thread and applied only if no newer request
        /// came meanwhile.</summary>
        /// <returns>A task that completes when the notes were applied or dropped; it never
        /// faults.</returns>
        async Task RefreshFormatAsync()
        {
            int gen = ++_formatGen;
            LensRow lens = SelectedLens;
            ProfileSources sources = _sources;
            if (lens == null || sources == null) { FormatNote = ""; FormatWarning = false; RequestNote = ""; return; }
            double? focal = Num(FocalText), fstop = Num(FStopText), crop = Num(CropText);
            int w = _masterW > 0 ? _masterW : 3000, h = _masterH > 0 ? _masterH : 2000;
            (string Text, bool Warning, string Request) note;
            try
            {
                note = await Task.Run(() =>
                {
                    var profiles = sources.Catalog.ProfilesOf(lens.Choice);
                    if (profiles.Count == 0) return ("", false, "");
                    LensProfile p = LensCatalog.Nearest(profiles, focal ?? profiles[0].FocalMm, fstop ?? profiles[0].FStop);
                    // Without a focal length or an aperture in the box there is no request to
                    // compare: the write will take them from the header / raw, and its note will
                    // judge them then.
                    string request = focal.HasValue && fstop.HasValue
                        ? SyntheticReport.ProfileMismatch(p, focal.Value, fstop.Value, profiles) ?? "" : "";
                    return ($"{lens.Label}: " + SyntheticReport.FormatMatch(p.Model, crop ?? 0, w, h),
                            SyntheticReport.FormatMatchIsWarning(p.Model, crop ?? 0, w, h), request);
                }).ConfigureAwait(true);
            }
            catch (Exception e)
            {
                // The note is a convenience: without it the fields stay as they are, and the
                // write judges the request itself. (A broken Adobe file does not come here:
                // the catalog parses it to no profile, with a Warn, and the write refuses the
                // lens with the reason, R-071.)
                Log.Debug(e, "the format note could not be computed");
                note = ("", false, "");
            }
            if (gen != _formatGen) return;
            FormatNote = note.Text;
            FormatWarning = note.Warning;
            RequestNote = note.Request;
        }

        /// <summary>Where the prefilled values came from, or what could not be read.</summary>
        public string PrefillNote { get => _prefillNote; private set => Set(ref _prefillNote, value); }

        public string AdobeWhere { get => _adobeWhere; private set => Set(ref _adobeWhere, value); }
        public string LensfunWhere { get => _lensfunWhere; private set => Set(ref _lensfunWhere, value); }
        public string ProfilesSummary { get => _profilesSummary; private set => Set(ref _profilesSummary, value); }

        public string ProfileLine { get => _profileLine; private set => Set(ref _profileLine, value); }
        public string ModelLine { get => _modelLine; private set => Set(ref _modelLine, value); }
        public string CornerNote
        {
            get => _cornerNote;
            private set { if (Set(ref _cornerNote, value)) Raise(nameof(HasCornerNote)); }
        }
        public bool HasCornerNote => !string.IsNullOrEmpty(_cornerNote);
        public IReadOnlyList<MetricRow> Depth { get => _depth; private set => Set(ref _depth, value); }
        public string Caveat => SyntheticReport.Caveat().Replace('\n', ' ');
        public string SirilNote => SyntheticReport.SirilNote().Replace('\n', ' ');
        public string SavedTo { get => _savedTo; private set => Set(ref _savedTo, value); }
        public string SaveError { get => _saveError; private set => Set(ref _saveError, value); }

        public IReadOnlyList<CompareRow> CompareTable { get => _compareTable; private set => Set(ref _compareTable, value); }
        public string CompareColumnA => T("the master", "masterul");
        public string CompareColumnB => T("the synthetic", "sinteticul");
        public string CompareColumnDiff => T("difference (A / B)", "diferenta (A / B)");
        public string CompareVerdict
        {
            get => _compareVerdict;
            private set { if (Set(ref _compareVerdict, value)) Raise(nameof(HasCompare)); }
        }
        public bool HasCompare => !string.IsNullOrEmpty(_compareVerdict);
        /// <summary>The shape and the depth, the verdict's two parts, as the console prints
        /// them too.</summary>
        public string CompareVerdictDetail { get => _compareVerdictDetail; private set => Set(ref _compareVerdictDetail, value); }
        public string CompareNote => AgainstReport.Meaning().Replace('\n', ' ');
        public byte[] CompareMap { get => _compareMap; private set => Set(ref _compareMap, value); }
        public MapScaleViewModel MapScale { get; } = new();
        public string MapLegend { get => _mapLegend; private set => Set(ref _mapLegend, value); }

        internal Task Pending { get; private set; } = Task.CompletedTask;
        internal event Action<string> Saved;

        // ------------------------------------------------------------ the session

        /// <summary>Prepares the paths, enables the button when the session has a master, and
        /// starts the prefill in the background; it writes nothing.</summary>
        /// <param name="session">The folder just opened.</param>
        /// <param name="ct">The folder's token.</param>
        /// <param name="gate">The window's heavy-run gate, taken around a write or a comparison;
        /// null runs at once.</param>
        internal void Attach(SirilSession session, CancellationToken ct, HeavyRunGate gate = null)
        {
            _session = session;
            _ct = ct;
            _gate = gate;
            Source = session.FlatMasterPath ?? "";
            Target = session.SyntheticMasterPath ?? "";
            CanRun = session.FlatMasterPath != null;
            ClearError();
            if (CanRun) Pending = PrefillAsync();
        }

        /// <summary>The shared catalog, loaded once, in the background.</summary>
        /// <returns>The profile sources, from the first load.</returns>
        internal Task<ProfileSources> EnsureProfilesAsync() => _catalog.EnsureAsync();

        /// <summary>
        /// The prefill: the catalog, then the master's header (FOCALLEN, INSTRUME) and, when there
        /// is one, the first raw from flats/ for the aperture and the lens ID - metadata only, read
        /// at once, without waiting for the opening chain. Nothing written here overwrites what the
        /// user has already typed.
        /// </summary>
        /// <returns>A task that completes when the fields are filled; it never faults, a failure
        /// ends up in <see cref="PrefillNote"/>.</returns>
        internal async Task PrefillAsync()
        {
            SirilSession session = _session;
            CancellationToken ct = _ct;
            if (session == null) return;
            string master = session.FlatMasterPath;
            try
            {
                // Inside the try: it lists flats/, and a folder that cannot be listed is this
                // method's failure to log, not a faulted task nobody awaits (R-110).
                string rawPath = session.ReferenceRawPath;
                _sources = await EnsureProfilesAsync().ConfigureAwait(true);
                ct.ThrowIfCancellationRequested();
                ShowSources(_sources);
                RefreshLenses();
                ProfileSources sources = _sources;

                // The metadata, without pixels: the master's header and, when there is one, the
                // first raw from flats/ - the same one the Structure tab takes. The identification
                // is the core's; here the values are only put in the fields. A raw libraw does not
                // open leaves the aperture empty and says only that.
                var (id, reference, rawError) = await Task.Run(() =>
                {
                    LoadedFrame reference = _loadInfo(master);
                    LoadedFrame raw = null;
                    string error = null;
                    if (rawPath != null)
                    {
                        try { raw = _loadInfo(rawPath); }
                        catch (Exception e)
                        {
                            Log.Warn(e, "the raw " + rawPath + " could not be read for the prefill");
                            error = T($"{Path.GetFileName(rawPath)} could not be read ({e.Message}); type the aperture by hand",
                                      $"{Path.GetFileName(rawPath)} nu s-a putut citi ({e.Message}); diafragma se scrie de mana");
                        }
                    }
                    return (LensIdentification.Identify(reference, raw, sources, rawName: LensIdentification.NameOf(rawPath)), reference, error);
                }, ct).ConfigureAwait(true);
                ct.ThrowIfCancellationRequested();

                var notes = new List<string>(id.Notes);
                if (rawError != null) notes.Add(rawError);
                _masterW = FitsReader.HInt(reference.Header, "NAXIS1", 0);
                _masterH = FitsReader.HInt(reference.Header, "NAXIS2", 0);
                if (id.FocalMm.HasValue && FocalText.Length == 0) FocalText = id.FocalMm.Value.ToString("0.#", CultureInfo.InvariantCulture);
                if (id.FStop.HasValue && FStopText.Length == 0) FStopText = id.FStop.Value.ToString("0.0", CultureInfo.InvariantCulture);
                if (CropText.Length == 0)
                {
                    if (id.Crop.HasValue && id.CropFromTable)
                    {
                        _cropSource = SessionMessages.SyntheticCropFromTable(id.Camera);
                        CropText = id.Crop.Value.ToString("0.###", CultureInfo.InvariantCulture);
                    }
                    else { CropNote = SessionMessages.SyntheticCropUnknown(id.Camera); _cropUnknown = true; }
                }
                if (id.RawNamesLens && SelectedLens == null)
                {
                    string note = OfferCandidates(id.Candidates, id.RawLensLabel);
                    if (note != null) notes.Add(note);
                }
                PrefillNote = notes.Count == 0 ? "" : T("prefilled: ", "preumplut: ") + string.Join("; ", notes);
                Log.Info("prefilled from " + master + " focal=" + LogText.Str(FocalText) + " fstop=" + LogText.Str(FStopText)
                         + " crop=" + LogText.Str(CropText) + " lens=" + (SelectedLens?.Choice.Key ?? "none"));
            }
            catch (OperationCanceledException)
            {
                Log.Debug("the prefill was cancelled; another folder was opened or the window closes");
            }
            catch (Exception e)
            {
                // The prefill is a convenience, not an analysis: a strange header leaves the
                // fields empty and says why. A background task that threw would bring the
                // application down.
                Log.Warn(e, "the prefill failed; the fields stay empty");
                PrefillNote = T("the prefill failed: ", "preumplerea a esuat: ") + Describe(e);
            }
        }

        /// <summary>Shows where each profile source was looked for and what it holds.</summary>
        /// <param name="s">The profile sources found.</param>
        void ShowSources(ProfileSources s)
        {
            AdobeWhere = SyntheticReport.AdobeWhere(s);
            LensfunWhere = SyntheticReport.LensfunWhere(s);
            ProfilesSummary = SyntheticReport.SourcesSummary(s).Replace("\n", "  |  ").Trim();
        }

        /// <summary>Rebuilds the lens list from the search text, keeping the selected lens if it
        /// is still in it; nothing before the catalog is loaded.</summary>
        void RefreshLenses()
        {
            if (_sources == null) return;
            LensChoice keep = SelectedLens?.Choice;
            Lenses = Rows(_sources.Catalog.Search(_search));
            SelectedLens = Lenses.FirstOrDefault(r => r.Choice == keep);
        }

        /// <summary>One list row per lens.</summary>
        /// <param name="lenses">The core's lens choices.</param>
        /// <returns>The rows, in the same order, without details yet.</returns>
        static List<LensRow> Rows(IEnumerable<LensChoice> lenses)
            => lenses.Select(l => new LensRow
            {
                Label = l.Name,
                SourceLabel = l.SourceLabel,
                Where = l.Where,
                Choice = l,
            }).ToList();

        /// <summary>
        /// What the raw said about the lens, put on screen: a single candidate is selected; several
        /// (ID 139 is both AT-X 11-20 and atx-i 11-20) narrow the list to them and the user
        /// chooses - the first in alphabetical order is not taken, as it was when it was a Nikkor.
        /// Returns the prefill note, or null when nothing was done.
        /// </summary>
        /// <param name="candidates">The lenses the raw's lens name or ID matches; null or empty
        /// does nothing.</param>
        /// <param name="rawLabel">How the raw named the lens, for the note.</param>
        /// <returns>The prefill note, or null.</returns>
        internal string OfferCandidates(IReadOnlyList<LensChoice> candidates, string rawLabel)
        {
            if (candidates == null || candidates.Count == 0) return null;
            Search = "";
            if (candidates.Count == 1)
            {
                SelectedLens = Lenses.FirstOrDefault(r => r.Choice == candidates[0]);
                return T("chosen: ", "ales: ") + rawLabel;
            }
            Lenses = Rows(candidates);
            SelectedLens = null;
            return SessionMessages.SyntheticLensCandidates(rawLabel, candidates.Count);
        }

        /// <summary>Re-evaluates both buttons and <see cref="CanCompare"/>.</summary>
        void RaiseCommands()
        {
            Run.RaiseCanExecuteChanged();
            Compare.RaiseCanExecuteChanged();
            Raise(nameof(CanCompare));
        }

        /// <summary>Reads a positive number from a field, with either decimal separator.</summary>
        /// <param name="text">The field's text, or null.</param>
        /// <returns>The number, or null when it is not a number above 0.</returns>
        static double? Num(string text)
            => double.TryParse((text ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && v > 0 ? v : null;

        // ------------------------------------------------------------ the buttons

        /// <summary>Writes the synthetic flat for the chosen lens and the fields' values, guarded,
        /// after the gate: the numbers on screen first, then the file beside the master,
        /// asking before an overwrite, then the comparison with the master.</summary>
        /// <returns>A task that completes when the write and its comparison ended; it never
        /// faults.</returns>
        internal async Task RunAsync()
        {
            if (_session == null || !CanRun) return;
            string master = _session.FlatMasterPath;
            string target = _session.SyntheticMasterPath;
            CancellationToken ct = _ct;
            LensRow lens = SelectedLens;
            double? focal = Num(FocalText), fstop = Num(FStopText), crop = Num(CropText);

            await RunGuardedAsync(T("Writing the synthetic flat...", "Scriu flatul sintetic..."), async () =>
            {
                RaiseCommands();
                using IDisposable lease = await EnterGateAsync(_gate, ct).ConfigureAwait(true);
                ct.ThrowIfCancellationRequested();
                ProfileSources sources = await EnsureProfilesAsync().ConfigureAwait(true);
                ct.ThrowIfCancellationRequested();
                if (sources.Catalog.Lenses.Count == 0) { Refuse(SessionMessages.SyntheticRefusedTitle(), SessionMessages.SyntheticNoProfiles(), "no lens profile found on this machine"); return; }
                if (lens == null) { Refuse(SessionMessages.SyntheticRefusedTitle(), SessionMessages.SyntheticNoLensSelected(), "no lens selected in the list"); return; }

                var req = new SyntheticRequest { Lens = lens.Choice, Source = lens.Choice.Source, LensText = lens.Choice.Name };
                var text = new StringWriter();
                var outcome = await Task.Run(() =>
                {
                    RunReport.WriteBanner(text);
                    var run = SyntheticRun.Execute(master, req, sources, focal, fstop, crop, text);
                    ct.ThrowIfCancellationRequested();
                    return run;
                }, ct).ConfigureAwait(true);

                if (outcome.Refused)
                {
                    Report = text.ToString();
                    Fail(SessionMessages.SyntheticRefusedTitle(), outcome.Refusal);
                    return;
                }
                Populate(outcome.Result, outcome.Request);

                SavedTo = "";
                SaveError = "";
                if (File.Exists(target) && !_confirm(SessionMessages.SyntheticOverwriteQuestion(target)))
                {
                    Log.Info("the user kept the existing " + target + "; nothing written");
                    SaveError = SessionMessages.SyntheticNotOverwritten(target);
                    Report = text.ToString();
                    return;
                }
                try
                {
                    await Task.Run(() =>
                    {
                        using var fs = new FileStream(target, FileMode.Create, FileAccess.Write);
                        SyntheticRun.WriteFits(outcome, master, fs);
                    }, ct).ConfigureAwait(true);
                    text.WriteLine(SyntheticReport.SavedLine(target));
                    SavedTo = target;
                    Log.Info("wrote " + target + " (" + LogText.FileKiB(target) + ")");
                    Saved?.Invoke(target);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    Log.Warn(e, "the synthetic flat could not be written to " + target);
                    SaveError = T("Could not write the synthetic flat:\n", "Nu am putut scrie flatul sintetic:\n") + Describe(e);
                }
                Report = text.ToString();
            }).ConfigureAwait(true);
            RaiseCommands();

            // The file just written is compared with the master at once: writing the synthetic
            // flat and not setting it against the master is half the job, and the screen after
            // "Scrie" without a table and without a map was read as a bug. The separate button
            // stays for a file written another time. Not after a refused overwrite (the old file
            // may be another lens's), not after a write error.
            if (SavedTo == target && !HasError && !ct.IsCancellationRequested)
                await CompareAsync(Report).ConfigureAwait(true);
        }

        /// <summary>Compares the session's master with the synthetic flat beside it, guarded,
        /// after the gate, and shows the table, the verdict and the map.</summary>
        /// <param name="reportSoFar">The write's report, when the comparison follows it; otherwise
        /// null and the banner is written.</param>
        /// <returns>A task that completes when the comparison ended; it never faults.</returns>
        internal async Task CompareAsync(string reportSoFar = null)
        {
            if (_session == null || !CanCompare) return;
            string master = _session.FlatMasterPath;
            string target = _session.SyntheticMasterPath;
            CancellationToken ct = _ct;

            await RunGuardedAsync(T("Comparing the master with the synthetic flat...", "Compar masterul cu flatul sintetic..."), async () =>
            {
                RaiseCommands();
                using IDisposable lease = await EnterGateAsync(_gate, ct).ConfigureAwait(true);
                ct.ThrowIfCancellationRequested();
                var text = new StringWriter();
                if (reportSoFar != null) text.Write(reportSoFar);
                var outcome = await Task.Run(() =>
                {
                    if (reportSoFar == null) RunReport.WriteBanner(text);
                    var run = AgainstRun.Execute(master, target, text);
                    ct.ThrowIfCancellationRequested();
                    return run;
                }, ct).ConfigureAwait(true);
                PopulateCompare(outcome.Result);
                _mapCache[MapScale.Options[0]] = outcome.Png;
                RefreshMap();
                Report = text.ToString();
            }).ConfigureAwait(true);
            RaiseCommands();
        }

        /// <summary>Shows a written flat: the profile used against the request, the model, the
        /// corner note and the depth rows. Internal, for the rendering tests.</summary>
        /// <param name="r">The synthetic flat's result.</param>
        /// <param name="req">The request it was written for, with the focal length and aperture
        /// actually used.</param>
        internal void Populate(SyntheticFlatResult r, SyntheticRequest req)
        {
            LensProfile p = r.Profile;
            ProfileLine = $"{p.Lens.Name}  [{p.Lens.Where}]  ({p.Lens.SourceLabel}): {p.Settings}   -   "
                        + T("requested: ", "cerut: ") + SyntheticReport.Requested(req);
            ModelLine = p.Model.Describe();
            CornerNote = SyntheticReport.FormatMatch(p.Model, r.CameraCrop, r.Width, r.Height);
            CornerWarning = SyntheticReport.FormatMatchIsWarning(p.Model, r.CameraCrop, r.Width, r.Height);
            WrittenRequestNote = SyntheticReport.ProfileMismatch(p, req.FocalMm, req.FStop, r.LensProfiles) ?? "";
            Depth = SyntheticReport.DepthRows(r).Select(d => new MetricRow { Label = d.Label, Value = d.Value, Note = d.Note }).ToList();
            HasResult = true;
        }

        /// <summary>Shows a comparison: the A / B table, the verdict and its two parts, and the
        /// ratio's residual map. Internal, for the rendering tests.</summary>
        /// <param name="a">The comparison's result.</param>
        internal void PopulateCompare(AgainstResult a)
        {
            CompareTable = AgainstReport.Rows(a).Select(r => new CompareRow
            {
                Label = r.Label,
                A = r.A,
                AVerdict = r.AVerdict,
                B = r.B,
                BVerdict = r.BVerdict,
                Diff = r.Diff,
                DiffVerdict = r.DiffVerdict,
                Note = r.Note,
                IsKey = r.IsKey,
            }).ToList();
            _compareResidual = a.Structure.Residual;
            _mapCache.Clear();
            CompareVerdictDetail = AgainstReport.VerdictDetail(a);
            CompareVerdict = a.Verdict;
            RefreshMap();
        }

        /// <summary>Shows the comparison's map on the chosen cap, from the cache or encoded now,
        /// with its legend; empty before a comparison.</summary>
        void RefreshMap()
        {
            if (_compareResidual == null) { CompareMap = null; MapLegend = ""; return; }
            var range = MapScale.RangeFor(_compareResidual);
            if (!_mapCache.TryGetValue(MapScale.Selected, out byte[] png))
            {
                png = PngWriter.EncodeResidualMap(_compareResidual, range);
                _mapCache[MapScale.Selected] = png;
            }
            CompareMap = png;
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
            SelectedLens = null;
            FocalText = "";
            FStopText = "";
            CropText = "";
            CropNote = "";
            _cropUnknown = false;
            FormatNote = "";
            FormatWarning = CornerWarning = false;
            _masterW = _masterH = 0;
            SensorFormat.Clear();
            PrefillNote = "";
            ProfileLine = "";
            ModelLine = "";
            CornerNote = "";
            Depth = Array.Empty<MetricRow>();
            SavedTo = "";
            SaveError = "";
            CompareTable = Array.Empty<CompareRow>();
            CompareVerdict = "";
            CompareVerdictDetail = "";
            _compareResidual = null;
            _mapCache.Clear();
            CompareMap = null;
            MapLegend = "";
        }
    }
}
