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

// Tabul Sintetic: un flat scris din profilul de vignetare al obiectivului, si comparatia
// masterului fotografiat cu el. Pe modelul tabului Netezire: nimic nu ruleaza singur,
// butonul scrie un fisier langa master dupa intrebarea de suprascriere, fisierul nu devine
// masterul sesiunii.
//
// Ce decide tabul: nimic. Lista de obiective vine din ProfileLocator (Adobe de pe masina,
// lensfun/ de langa exe), alegerea profilului si cifrele din SyntheticRun, comparatia din
// AgainstRun. Ce face in plus fata de consola e PREUMPLEREA: focala si aparatul din
// headerul masterului (doar headerul, fara cei 96 MB), diafragma si ID-ul de obiectiv
// Nikon dintr-un raw din flats/, cand exista - si obiectivul ales din lista dupa acel ID.
// Toate campurile raman editabile; ce e scris de om bate ce s-a citit din fisiere.
//
// Catalogul se incarca o singura data, in fundal, la prima deschidere de sesiune: sute de
// fisiere .lcp de parsat inseamna o secunda-doua, care nu trebuie sa tina fereastra.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FlatCheck.Wpf
{
    /// <summary>Un rand din lista de obiective. Public pentru legare; alegerea nucleului ramane internal.</summary>
    public sealed class LensRow
    {
        public string Label { get; init; }
        public string SourceLabel { get; init; }
        public string Mount { get; init; }
        internal LensChoice Choice { get; init; }
    }

    public sealed class SyntheticGuideViewModel : ObservableObject
    {
        bool _expanded;   // colapsat implicit: e documentatie, nu preconditie
        public string Intro => SessionMessages.SyntheticGuideIntro();
        public string Title => SessionMessages.SyntheticSourcesTitle();
        public bool IsExpanded { get => _expanded; set => Set(ref _expanded, value); }
    }

    public sealed class SyntheticTabViewModel : TabViewModel
    {
        SirilSession _session;
        CancellationToken _ct;
        Func<Task> _waitIdle;
        readonly Func<string, bool> _confirm;
        readonly Func<ProfileSources> _locate;
        ProfileSources _sources;
        Task<ProfileSources> _loading;

        string _source = "", _target = "";
        bool _canRun;
        string _search = "";
        IReadOnlyList<LensRow> _lenses = Array.Empty<LensRow>();
        LensRow _selectedLens;
        string _focalText = "", _fstopText = "", _cropText = "";
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

        internal SyntheticTabViewModel(Func<string, bool> confirm = null, Func<ProfileSources> locate = null)
        {
            _confirm = confirm ?? (_ => true);
            _locate = locate ?? ProfileLocator.Locate;
            Run = new RelayCommand(() => Pending = RunAsync(), () => CanRun && IsIdle && SelectedLens != null);
            Compare = new RelayCommand(() => Pending = CompareAsync(), () => CanCompare && IsIdle);
            MapScale.Changed += RefreshMap;
        }

        // ------------------------------------------------------------ ce se leaga

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

        public IReadOnlyList<LensRow> Lenses { get => _lenses; private set => Set(ref _lenses, value); }

        public LensRow SelectedLens
        {
            get => _selectedLens;
            set { if (Set(ref _selectedLens, value)) RaiseCommands(); }
        }

        public string FocalText { get => _focalText; set => Set(ref _focalText, value ?? ""); }
        public string FStopText { get => _fstopText; set => Set(ref _fstopText, value ?? ""); }
        public string CropText { get => _cropText; set => Set(ref _cropText, value ?? ""); }

        /// <summary>De unde au venit valorile preumplute, sau ce n-a putut fi citit.</summary>
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
        public string CompareColumnA => "masterul";
        public string CompareColumnB => "sinteticul";
        public string CompareColumnDiff => "diferenta (A / B)";
        public string CompareVerdict
        {
            get => _compareVerdict;
            private set { if (Set(ref _compareVerdict, value)) Raise(nameof(HasCompare)); }
        }
        public bool HasCompare => !string.IsNullOrEmpty(_compareVerdict);
        /// <summary>Forma si adancimea, cele doua parti ale verdictului, cum le tipareste si consola.</summary>
        public string CompareVerdictDetail { get => _compareVerdictDetail; private set => Set(ref _compareVerdictDetail, value); }
        public string CompareNote => AgainstReport.Meaning().Replace('\n', ' ');
        public byte[] CompareMap { get => _compareMap; private set => Set(ref _compareMap, value); }
        public MapScaleViewModel MapScale { get; } = new();
        public string MapLegend { get => _mapLegend; private set => Set(ref _mapLegend, value); }

        internal Task Pending { get; private set; } = Task.CompletedTask;
        internal event Action<string> Saved;

        // ------------------------------------------------------------ sesiunea

        internal void Attach(SirilSession session, CancellationToken ct, Func<Task> waitIdle = null)
        {
            _session = session;
            _ct = ct;
            _waitIdle = waitIdle;
            Source = session.FlatMasterPath ?? "";
            Target = session.SyntheticMasterPath ?? "";
            CanRun = session.FlatMasterPath != null;
            ClearError();
            if (CanRun) Pending = PrefillAsync();
        }

        /// <summary>Catalogul, incarcat o singura data, in fundal.</summary>
        internal Task<ProfileSources> EnsureProfilesAsync()
        {
            if (_sources != null) return Task.FromResult(_sources);
            _loading ??= Task.Run(() => _locate());
            return _loading;
        }

        /// <summary>
        /// Preumplerea: catalogul, apoi headerul masterului (FOCALLEN, INSTRUME) si, dupa ce
        /// lantul de deschidere s-a terminat, un raw din flats/ pentru diafragma si ID-ul de
        /// obiectiv. Nimic din ce scrie aici nu suprascrie ce a tastat deja omul.
        /// </summary>
        internal async Task PrefillAsync()
        {
            SirilSession session = _session;
            CancellationToken ct = _ct;
            if (session == null) return;
            string master = session.FlatMasterPath;
            try
            {
                _sources = await EnsureProfilesAsync().ConfigureAwait(true);
                ct.ThrowIfCancellationRequested();
                ShowSources(_sources);
                RefreshLenses();

                var notes = new List<string>();
                IReadOnlyDictionary<string, string> hdr = await Task.Run(() => FitsReader.LoadHeader(master), ct).ConfigureAwait(true);
                ct.ThrowIfCancellationRequested();
                double focal = FitsReader.HDbl(hdr, "FOCALLEN", 0);
                string camera = FitsReader.HStr(hdr, "INSTRUME", null);
                if (focal > 0 && FocalText.Length == 0) { FocalText = focal.ToString("0.#", CultureInfo.InvariantCulture); notes.Add("focala din FOCALLEN"); }
                if (!string.IsNullOrWhiteSpace(camera))
                {
                    double? crop = _sources.Lensfun.CropOf(camera);
                    if (crop.HasValue && CropText.Length == 0) { CropText = crop.Value.ToString("0.###", CultureInfo.InvariantCulture); notes.Add($"crop din baza lensfun ({camera.Trim()})"); }
                }

                string raw = session.FlatFrames().FirstOrDefault(RawReader.IsRawPath);
                LoadedFrame f = null;
                if (raw != null)
                {
                    if (_waitIdle != null) await _waitIdle().ConfigureAwait(true);
                    ct.ThrowIfCancellationRequested();
                    // Un raw pe care libraw nu-l deschide lasa diafragma goala si spune atat;
                    // focala si crop-ul din header raman.
                    try { f = await Task.Run(() => ImageLoader.Load(raw), ct).ConfigureAwait(true); }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception) { notes.Add($"{Path.GetFileName(raw)} nu s-a putut citi; diafragma se scrie de mana"); }
                    ct.ThrowIfCancellationRequested();
                }
                if (f != null)
                {
                    if (f.FNumber.HasValue && FStopText.Length == 0) { FStopText = f.FNumber.Value.ToString("0.0", CultureInfo.InvariantCulture); notes.Add($"diafragma din {Path.GetFileName(raw)}"); }
                    if (f.FocalMm.HasValue && FocalText.Length == 0) { FocalText = f.FocalMm.Value.ToString("0.#", CultureInfo.InvariantCulture); notes.Add("focala din raw"); }
                    if (f.NikonLensId.HasValue && SelectedLens == null)
                    {
                        // Aceiasi candidati ca in consola (SyntheticRun.CandidatesFromRaw), apoi
                        // montura aparatului din header, cand o stim; ce ramane se ofera.
                        string maker = (camera ?? f.Camera ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                        var hits = await Task.Run(() => SyntheticRun.CandidatesFromRaw(_sources.Catalog, f), ct).ConfigureAwait(true);
                        var byMount = string.IsNullOrEmpty(maker) ? hits
                            : hits.Where(h => (h.Mount + " " + h.Maker).Contains(maker, StringComparison.OrdinalIgnoreCase)).ToList();
                        string note = OfferCandidates(byMount.Count > 0 ? byMount : hits, SyntheticReport.RawLensLabel(f));
                        if (note != null) notes.Add(note);
                    }
                }
                PrefillNote = notes.Count == 0 ? "" : "preumplut: " + string.Join("; ", notes);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                // Preumplerea e un confort, nu o analiza: un raw pe care libraw nu-l poate
                // deschide sau un header ciudat lasa campurile goale si spune de ce. Un task
                // de fundal care arunca ar dobori aplicatia.
                PrefillNote = "preumplerea a esuat: " + Describe(e);
            }
        }

        void ShowSources(ProfileSources s)
        {
            AdobeWhere = SyntheticReport.AdobeWhere(s);
            LensfunWhere = SyntheticReport.LensfunWhere(s);
            ProfilesSummary = SyntheticReport.SourcesSummary(s).Replace("\n", "  |  ").Trim();
        }

        void RefreshLenses()
        {
            if (_sources == null) return;
            LensChoice keep = SelectedLens?.Choice;
            Lenses = Rows(_sources.Catalog.Search(_search));
            SelectedLens = Lenses.FirstOrDefault(r => r.Choice == keep);
        }

        static List<LensRow> Rows(IEnumerable<LensChoice> lenses)
            => lenses.Select(l => new LensRow
            {
                Label = l.Name,
                SourceLabel = l.SourceLabel,
                Mount = l.Mount,
                Choice = l,
            }).ToList();

        /// <summary>
        /// Ce a spus raw-ul despre obiectiv, pus pe ecran: un singur candidat se alege; mai
        /// multi (ID-ul 139 e si AT-X 11-20, si atx-i 11-20) restrang lista la ei si omul
        /// alege - nu se ia primul in ordine alfabetica, cum se lua, cand era un Nikkor.
        /// Intoarce nota de preumplere, sau null cand nu s-a facut nimic.
        /// </summary>
        internal string OfferCandidates(IReadOnlyList<LensChoice> candidates, string rawLabel)
        {
            if (candidates == null || candidates.Count == 0) return null;
            Search = "";
            if (candidates.Count == 1)
            {
                SelectedLens = Lenses.FirstOrDefault(r => r.Choice == candidates[0]);
                return "obiectivul dupa " + rawLabel;
            }
            Lenses = Rows(candidates);
            SelectedLens = null;
            return SessionMessages.SyntheticLensCandidates(rawLabel, candidates.Count);
        }

        void RaiseCommands()
        {
            Run.RaiseCanExecuteChanged();
            Compare.RaiseCanExecuteChanged();
            Raise(nameof(CanCompare));
        }

        static double? Num(string text)
            => double.TryParse((text ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) && v > 0 ? v : null;

        // ------------------------------------------------------------ butoanele

        internal async Task RunAsync()
        {
            if (_session == null || !CanRun) return;
            string master = _session.FlatMasterPath;
            string target = _session.SyntheticMasterPath;
            CancellationToken ct = _ct;
            LensRow lens = SelectedLens;
            double? focal = Num(FocalText), fstop = Num(FStopText), crop = Num(CropText);

            await RunGuardedAsync("Scriu flatul sintetic...", async () =>
            {
                RaiseCommands();
                if (_waitIdle != null) await _waitIdle().ConfigureAwait(true);
                ct.ThrowIfCancellationRequested();
                ProfileSources sources = await EnsureProfilesAsync().ConfigureAwait(true);
                ct.ThrowIfCancellationRequested();
                if (sources.Catalog.Lenses.Count == 0) { Fail(SessionMessages.SyntheticRefusedTitle(), SessionMessages.SyntheticNoProfiles()); return; }
                if (lens == null) { Fail(SessionMessages.SyntheticRefusedTitle(), SessionMessages.SyntheticNoLensSelected()); return; }

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
                    Saved?.Invoke(target);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    SaveError = "Nu am putut scrie flatul sintetic:\n" + Describe(e);
                }
                Report = text.ToString();
            }).ConfigureAwait(true);
            RaiseCommands();

            // Fisierul abia scris se compara imediat cu masterul: a scrie flatul sintetic si
            // a nu-l pune fata in fata cu masterul e jumatate de treaba, si ecranul de dupa
            // "Scrie" fara tabel si fara harta a fost citit ca un bug. Butonul separat ramane
            // pentru un fisier scris alta data. Nu dupa un refuz de suprascriere (fisierul
            // vechi poate fi al altui obiectiv), nu dupa o eroare de scriere.
            if (SavedTo == target && !HasError && !ct.IsCancellationRequested)
                await CompareAsync(Report).ConfigureAwait(true);
        }

        /// <param name="reportSoFar">Raportul scrierii, cand comparatia ii urmeaza; altfel null si se pune bannerul.</param>
        internal async Task CompareAsync(string reportSoFar = null)
        {
            if (_session == null || !CanCompare) return;
            string master = _session.FlatMasterPath;
            string target = _session.SyntheticMasterPath;
            CancellationToken ct = _ct;

            await RunGuardedAsync("Compar masterul cu flatul sintetic...", async () =>
            {
                RaiseCommands();
                if (_waitIdle != null) await _waitIdle().ConfigureAwait(true);
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

        internal void Populate(SyntheticFlatResult r, SyntheticRequest req)
        {
            LensProfile p = r.Profile;
            ProfileLine = $"{p.Lens.Name}  [{p.Lens.Mount}]  ({p.Lens.SourceLabel}): {p.Settings}   -   cerut: {SyntheticReport.Requested(req)}";
            ModelLine = p.Model.Describe();
            CornerNote = p.Lens.Source == ProfileSource.Lensfun && r.CornerRadius > 1.0
                ? $"Coltul cade la r = {r.CornerRadius.ToString("0.00", CultureInfo.InvariantCulture)}, dincolo de senzorul pe care a fost calibrat profilul (crop {p.Model.CropFactor.ToString("0.##", CultureInfo.InvariantCulture)} fata de {r.CameraCrop.ToString("0.###", CultureInfo.InvariantCulture)}): o extrapolare mica."
                : "";
            Depth = SyntheticReport.DepthRows(r).Select(d => new MetricRow { Label = d.Label, Value = d.Value, Note = d.Note }).ToList();
            HasResult = true;
        }

        internal void PopulateCompare(AgainstResult a)
        {
            CompareTable = AgainstReport.Rows(a).Select(r => new CompareRow
            {
                Label = r.Label, A = r.A, AVerdict = r.AVerdict, B = r.B, BVerdict = r.BVerdict,
                Diff = r.Diff, DiffVerdict = r.DiffVerdict, Note = r.Note, IsKey = r.IsKey,
            }).ToList();
            _compareResidual = a.Structure.Residual;
            _mapCache.Clear();
            CompareVerdictDetail = AgainstReport.VerdictDetail(a);
            CompareVerdict = a.Verdict;
            RefreshMap();
        }

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

        internal override void Reset()
        {
            base.Reset();
            _session = null;
            _waitIdle = null;
            Source = "";
            Target = "";
            CanRun = false;
            SelectedLens = null;
            FocalText = "";
            FStopText = "";
            CropText = "";
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
