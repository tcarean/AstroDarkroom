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
// Regula e a nucleului (LensIdentification), aceeasi pe care o aplica tabul Structura si
// consola; aici se citesc doar metadatele (ImageLoader.LoadInfo, niciun pixel) si se pun
// valorile in campuri. Toate campurile raman editabile; ce e scris de om bate ce s-a citit.
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
    /// <summary>
    /// Un rand din lista de obiective. Public pentru legare; alegerea nucleului ramane internal.
    /// Observabil pentru <see cref="Details"/>, care soseste mai tarziu: un fisier Adobe se
    /// parseaza in fundal, si randul se completeaza fara sa se reconstruiasca lista (si sa
    /// se piarda selectia).
    /// </summary>
    public sealed class LensRow : ObservableObject
    {
        string _details = "";

        public string Label { get; init; }
        public string SourceLabel { get; init; }
        /// <summary>Montura, plus formatul de calibrare la lensfun (LensChoice.Where).</summary>
        public string Where { get; init; }
        internal LensChoice Choice { get; init; }

        /// <summary>Focale, diafragme, distante, numar de profile (SyntheticReport.LensDetails); gol pe o lista lunga sau pana soseste.</summary>
        public string Details { get => _details; internal set { if (Set(ref _details, value ?? "")) Raise(nameof(HasDetails)); } }
        public bool HasDetails => _details.Length > 0;
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
        readonly ProfileCatalog _catalog;
        /// <summary>Metadatele unui fisier fara pixeli; in teste, un cadru fabricat in locul unui raw.</summary>
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

        internal SyntheticTabViewModel(Func<string, bool> confirm = null, Func<ProfileSources> locate = null,
                                       Func<string, LoadedFrame> loadInfo = null)
            : this(confirm, new ProfileCatalog(locate), loadInfo) { }

        /// <param name="confirm">Asks the user a yes/no question (overwriting the synthetic flat);
        /// null answers yes.</param>
        /// <param name="catalog">Catalogul comun al ferestrei (MainViewModel il da si tabului Structura).</param>
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
                RefreshFormat();
            };
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

        public IReadOnlyList<LensRow> Lenses { get => _lenses; private set { if (Set(ref _lenses, value)) RefreshDetails(); } }

        /// <summary>De ce randurile n-au detalii: lista e prea lunga (SyntheticReport.ListDetailsHidden). Gol cand le au.</summary>
        public string ListNote { get => _listNote; private set { if (Set(ref _listNote, value ?? "")) Raise(nameof(HasListNote)); } }
        public bool HasListNote => _listNote.Length > 0;

        /// <summary>Calculul din spatele detaliilor randurilor, ca un test sa-l poata astepta.</summary>
        internal Task DetailsPending { get; private set; } = Task.CompletedTask;

        /// <summary>
        /// Umple a doua linie a fiecarui rand cu focalele, diafragmele si distantele lui - regula
        /// consolei (--lenses): numai cand lista are cel mult ListDetailsMax randuri, fiindca
        /// detaliile unui obiectiv Adobe cer parsarea fisierului lui (~10 ms fiecare, 36 s pe
        /// toate). Aceeasi regula si pentru lensfun, desi acolo ar fi gratis, ca lista sa nu
        /// arate jumatate cu detalii, jumatate fara. In fundal, in paralel, cu numar de
        /// generatie: o cautare noua nu primeste detaliile celei vechi.
        /// </summary>
        void RefreshDetails() => DetailsPending = RefreshDetailsAsync();

        async Task RefreshDetailsAsync()
        {
            int gen = ++_detailsGen;
            IReadOnlyList<LensRow> rows = Lenses;
            ProfileSources sources = _sources;
            if (sources == null || rows.Count == 0) { ListNote = ""; return; }
            if (rows.Count > SyntheticReport.ListDetailsMax)
            {
                ListNote = SyntheticReport.ListDetailsHidden(rows.Count) + " - scrie un text in campul obiectiv";
                return;
            }
            ListNote = "";
            List<(LensRow Row, string Details)> details;
            try
            {
                details = await Task.Run(() => rows.AsParallel().AsOrdered()
                    .Select(r => (r, Safe(() => SyntheticReport.LensDetails(r.Choice, sources.Catalog)))).ToList()).ConfigureAwait(true);
            }
            catch (Exception) { return; }
            if (gen != _detailsGen) return;
            foreach (var (row, text) in details) row.Details = text;
        }

        static string Safe(Func<string> f)
        {
            try { return f(); } catch (Exception) { return ""; }   // un fisier Adobe stricat lasa randul fara detalii; alegerea lui va spune de ce
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
                    // Chip-urile urmaresc valoarea; nota spune de unde e si ce format e. Sursa o
                    // pune cine scrie in camp (tabelul, la preumplere); fara ea, e tastat.
                    double? crop = Num(_cropText);
                    SensorFormat.Follow(crop);
                    string source = _cropSource ?? SessionMessages.SyntheticCropTyped();
                    _cropSource = null;
                    CropNote = crop.HasValue ? SessionMessages.SyntheticCropNote(_cropText.Trim(), source, SensorFormats.LabelOf(crop.Value))
                             : _cropText.Trim().Length > 0 ? "" : _cropNote.StartsWith("crop necunoscut") ? _cropNote : "";
                }
                RefreshFormat();
            }
        }

        /// <summary>Chip-urile de tip de senzor: arata formatul crop-ului din camp, si il pot pune cand nu vine din tabel.</summary>
        public SensorFormatViewModel SensorFormat { get; } = new();

        /// <summary>De unde e crop-ul din camp si ce format e; sau ca nu se stie inca.</summary>
        public string CropNote { get => _cropNote; private set => Set(ref _cropNote, value); }

        /// <summary>Profilul ales fata de senzorul tau: formatul de calibrare, al tau, si ce urmeaza. Gol fara obiectiv.</summary>
        public string FormatNote { get => _formatNote; private set => Set(ref _formatNote, value); }

        /// <summary>Adevarat cand FormatNote e un avertisment (extrapolare mare): se coloreaza.</summary>
        public bool FormatWarning { get => _formatWarning; private set => Set(ref _formatWarning, value); }

        /// <summary>
        /// Cand focala sau diafragma din casute e departe de profilul care s-ar folosi
        /// (SyntheticReport.ProfileMismatch): mereu un avertisment, gol cand nu e cazul.
        /// Recalculat odata cu FormatNote, la fiecare tasta.
        /// </summary>
        public string RequestNote { get => _requestNote; private set { if (Set(ref _requestNote, value ?? "")) Raise(nameof(HasRequestNote)); } }
        public bool HasRequestNote => _requestNote.Length > 0;

        /// <summary>Acelasi avertisment pentru flatul scris, sub "profil folosit"; gol cand cererea a fost in toleranta.</summary>
        public string WrittenRequestNote { get => _writtenRequestNote; private set { if (Set(ref _writtenRequestNote, value ?? "")) Raise(nameof(HasWrittenRequestNote)); } }
        public bool HasWrittenRequestNote => _writtenRequestNote.Length > 0;

        /// <summary>Acelasi lucru pentru linia de sub adancimile scrise.</summary>
        public bool CornerWarning { get => _cornerWarning; private set => Set(ref _cornerWarning, value); }

        /// <summary>Calculul din spatele lui FormatNote, ca un test sa-l poata astepta.</summary>
        internal Task FormatPending { get; private set; } = Task.CompletedTask;

        /// <summary>
        /// Recalculeaza FormatNote: profilul cel mai apropiat al obiectivului ales (un fisier
        /// Adobe se parseaza abia acum, unul singur), pus fata in fata cu crop-ul din camp pe
        /// cadrul masterului. In fundal, cu numar de generatie: doar ultima cerere ajunge pe
        /// ecran, ca la celelalte taburi.
        /// </summary>
        void RefreshFormat() => FormatPending = RefreshFormatAsync();

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
                    // Fara focala sau diafragma in casuta nu e nicio cerere de comparat: scrierea
                    // le va lua din header / raw, si nota ei le va judeca atunci.
                    string request = focal.HasValue && fstop.HasValue
                        ? SyntheticReport.ProfileMismatch(p, focal.Value, fstop.Value, profiles) ?? "" : "";
                    return ($"{lens.Label}: " + SyntheticReport.FormatMatch(p.Model, crop ?? 0, w, h),
                            SyntheticReport.FormatMatchIsWarning(p.Model, crop ?? 0, w, h), request);
                }).ConfigureAwait(true);
            }
            catch (Exception) { note = ("", false, ""); }   // un fisier Adobe stricat lasa nota goala; scrierea il va refuza cu motivul
            if (gen != _formatGen) return;
            FormatNote = note.Text;
            FormatWarning = note.Warning;
            RequestNote = note.Request;
        }

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

        /// <summary>Catalogul comun, incarcat o singura data, in fundal.</summary>
        internal Task<ProfileSources> EnsureProfilesAsync() => _catalog.EnsureAsync();

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
            string rawPath = session.ReferenceRawPath;
            try
            {
                _sources = await EnsureProfilesAsync().ConfigureAwait(true);
                ct.ThrowIfCancellationRequested();
                ShowSources(_sources);
                RefreshLenses();
                ProfileSources sources = _sources;

                // Metadatele, fara pixeli: headerul masterului si, cand exista, primul raw
                // din flats/ - acelasi pe care il ia si tabul Structura. Identificarea e a
                // nucleului; aici doar se pun valorile in campuri. Un raw pe care libraw
                // nu-l deschide lasa diafragma goala si spune atat.
                var (id, reference, rawError) = await Task.Run(() =>
                {
                    LoadedFrame reference = _loadInfo(master);
                    LoadedFrame raw = null;
                    string error = null;
                    if (rawPath != null)
                    {
                        try { raw = _loadInfo(rawPath); }
                        catch (Exception e) { error = $"{Path.GetFileName(rawPath)} nu s-a putut citi ({e.Message}); diafragma se scrie de mana"; }
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
                    else CropNote = SessionMessages.SyntheticCropUnknown(id.Camera);
                }
                if (id.HasRawLensId && SelectedLens == null)
                {
                    string note = OfferCandidates(id.Candidates, id.RawLensLabel);
                    if (note != null) notes.Add(note);
                }
                PrefillNote = notes.Count == 0 ? "" : "preumplut: " + string.Join("; ", notes);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                // Preumplerea e un confort, nu o analiza: un header ciudat lasa campurile
                // goale si spune de ce. Un task de fundal care arunca ar dobori aplicatia.
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
                Where = l.Where,
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
            ProfileLine = $"{p.Lens.Name}  [{p.Lens.Where}]  ({p.Lens.SourceLabel}): {p.Settings}   -   cerut: {SyntheticReport.Requested(req)}";
            ModelLine = p.Model.Describe();
            CornerNote = SyntheticReport.FormatMatch(p.Model, r.CameraCrop, r.Width, r.Height);
            CornerWarning = SyntheticReport.FormatMatchIsWarning(p.Model, r.CameraCrop, r.Width, r.Height);
            WrittenRequestNote = SyntheticReport.ProfileMismatch(p, req.FocalMm, req.FStop, r.LensProfiles) ?? "";
            Depth = SyntheticReport.DepthRows(r).Select(d => new MetricRow { Label = d.Label, Value = d.Value, Note = d.Note }).ToList();
            HasResult = true;
        }

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
            CropNote = "";
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
