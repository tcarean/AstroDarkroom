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

// SplitTabViewModel.cs - tabul Split (ADR 0054).
//
// Un flux, nu o analiza: alege in cate parti (2 sau 4) si ce script, apasa, si tabul
// copiaza felia fiecarei parti intr-o sesiune temporara (SplitPlan), ruleaza scriptul
// in fiecare prin aceeasi rulare ca butonul de pe cardul de problema (SirilScriptRun +
// SirilRunViewModel, aratat aici in loc de fereastra lui), apoi da masterele partilor si
// masterul intreg nucleului (SplitRun), care compara, scrie raportul si compune mozaicul.
// Nimic din ce se vede in tabel nu se calculeaza aici.
//
// NU FOLOSESTE ECRANUL DE ASTEPTARE al celorlalte taburi: acela acopera tot tabul, iar
// aici omul trebuie sa vada faza, bara, log-ul lui Siril si butonul Opreste. Starea de
// rulare e IsRunning, iar butoanele si chipurile se dezactiveaza cat timp e true.
//
// O BARA PENTRU TOT: pe parte, copierea (dupa numarul de fisiere) si scriptul (dupa
// comanda curenta din CommandCount, cu progresul ei), plus analiza la sfarsit. Fazele
// sunt egale in bara, nu in timp; ce conteaza e ca merge inainte si nu minte.
//
// TEMP: se sterge dupa analiza si la Opreste; ramane dupa un esec (exit non-zero, sau un
// script care n-a lasat master), cu calea pe ecran - fara el n-ai ce diagnostica.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FlatCheck.Wpf
{
    /// <summary>Un chip "2 parti" / "4 parti".</summary>
    public sealed class SplitPartsOption : ObservableObject
    {
        readonly SplitTabViewModel _owner;
        bool _selected;

        internal SplitPartsOption(SplitTabViewModel owner, int parts)
        {
            _owner = owner;
            Parts = parts;
            Label = SessionMessages.SplitPartsLabel(parts);
        }

        public int Parts { get; }
        public string Label { get; }

        public bool IsSelected
        {
            get => _selected;
            set { if (Set(ref _selected, value) && value) _owner.SelectParts(Parts); }
        }

        internal void Unselect() => Set(ref _selected, false, nameof(IsSelected));
    }

    /// <summary>O celula a tabelului: valoarea si verdictul ei, gata formatate de nucleu.</summary>
    public sealed class SplitTableCell
    {
        public string Value { get; init; }
        public string Verdict { get; init; }
        public bool HasVerdict => !string.IsNullOrEmpty(Verdict);
    }

    public sealed class SplitTableRow
    {
        public string Label { get; init; }
        public IReadOnlyList<SplitTableCell> Cells { get; init; }
        public string Note { get; init; }
        public bool IsKey { get; init; }
        /// <summary>Randul profilului de referinta - cel care da verdictul adancimii.</summary>
        public bool IsReference { get; init; }
        public bool HasNote => !string.IsNullOrEmpty(Note);
    }

    /// <summary>O harta a mozaicului, pe ecran: eticheta si PNG-ul ei pe scara aleasa.</summary>
    public sealed class SplitMap : ObservableObject
    {
        byte[] _png;

        internal SplitMap(string label, Img raster)
        {
            Label = label;
            Raster = raster;
        }

        public string Label { get; }
        internal Img Raster { get; }
        public byte[] Png { get => _png; internal set => Set(ref _png, value); }
    }

    public sealed class SplitTabViewModel : TabViewModel
    {
        readonly Func<SirilInstall> _locate;
        readonly Func<bool> _guiRunning;
        readonly Func<string, string, SirilRunner> _runScript;
        readonly ProfileCatalog _catalog;
        readonly Func<string, LoadedFrame> _loadInfo;
        readonly Func<DateTime> _clock;
        readonly Func<DateTime, string> _tempRootFor;

        SirilSession _session;
        CancellationToken _ct;
        Func<Task> _waitIdle;
        CancellationTokenSource _run;

        int _frameCount;
        string _framesLine = "";
        int _parts = SplitPlan.Choices[0];
        string _blocker;
        bool _running;
        string _phase = "";
        SirilRunViewModel _sirilRun;
        IReadOnlyList<string> _headers = Array.Empty<string>();
        IReadOnlyList<SplitTableRow> _rows = Array.Empty<SplitTableRow>();
        IReadOnlyList<SplitMap> _maps = Array.Empty<SplitMap>();
        string _mapLegend = "";
        string _savedTo = "";
        string _saveError = "";
        string _tempNote = "";
        string _referenceLine = "";
        string _estimateLine = "";
        long _pixels;

        PngWriter.MapRange _sharedRange;
        readonly Dictionary<(int Index, MapScaleOption Scale), byte[]> _mapCache = new();

        public SplitTabViewModel() : this(null, null, null) { }

        /// <summary>
        /// Aceleasi doua intrebari despre masina ca pe cardul de problema (unde e Siril,
        /// daca ruleaza), fabrica de rulari (in teste, un Siril fals care scrie masterul
        /// unde ar fi scris scriptul), catalogul de profile si cititorul de metadate ca
        /// pe tabul Structura, ceasul (data din raport) si radacina temporara (in teste,
        /// in folderul testului, nu in %TEMP%). Null inseamna raspunsul adevarat.
        /// </summary>
        internal SplitTabViewModel(Func<SirilInstall> locate, Func<bool> guiRunning, Func<string, string, SirilRunner> runScript,
                                   ProfileCatalog catalog = null, Func<string, LoadedFrame> loadInfo = null,
                                   Func<DateTime> clock = null, Func<DateTime, string> tempRootFor = null)
        {
            _locate = locate ?? SirilInstall.Locate;
            _guiRunning = guiRunning ?? SirilInstall.IsGuiRunning;
            _runScript = runScript ?? ((folder, script) => SirilScriptRun.For(Script.CliPath, folder, script));
            _catalog = catalog;
            _loadInfo = loadInfo ?? ImageLoader.LoadInfo;
            _clock = clock ?? (() => DateTime.Now);
            _tempRootFor = tempRootFor ?? SplitPlan.NewTempRoot;

            PartOptions = SplitPlan.Choices.Select(n => new SplitPartsOption(this, n)).ToList();
            PartOptions[0].IsSelected = true;
            Script.Refreshed += Refresh;
            Start = new RelayCommand(() => Pending = StartAsync(), () => CanStart);
            Stop = new RelayCommand(StopRun, () => IsRunning);
            MapScale.Changed += RefreshMaps;
        }

        // ------------------------------------------------------------------ alegerea

        public string Intro => SessionMessages.SplitIntro();

        /// <summary>Cat loc pe disc cere impartirea aleasa: copiile exact, process/ estimat (SplitCost), discul temporar.</summary>
        public string EstimateLine { get => _estimateLine; private set => Set(ref _estimateLine, value); }

        public IReadOnlyList<SplitPartsOption> PartOptions { get; }

        /// <summary>In cate parti: 2 sau 4.</summary>
        public int Parts { get => _parts; private set => Set(ref _parts, value); }

        internal void SelectParts(int parts)
        {
            if (!SplitPlan.Choices.Contains(parts)) return;
            foreach (SplitPartsOption o in PartOptions)
                if (o.Parts != parts) o.Unselect();
                else if (!o.IsSelected) o.IsSelected = true;
            if (Parts == parts) { Refresh(); return; }
            Parts = parts;
            Refresh();
        }

        public ScriptChoiceViewModel Script { get; } = new();

        /// <summary>Cate cadre (raw sau FITS) are flats/; un readme.txt nu e cadru.</summary>
        public int FrameCount { get => _frameCount; private set => Set(ref _frameCount, value); }

        public string FramesLine { get => _framesLine; private set => Set(ref _framesLine, value); }

        /// <summary>De ce nu se poate apasa, sau null cand se poate: al impartirii, apoi al alegerii de script.</summary>
        public string Blocker
        {
            get => _blocker;
            private set { if (Set(ref _blocker, value)) Raise(nameof(HasBlocker)); }
        }

        public bool HasBlocker => !string.IsNullOrEmpty(_blocker);

        public bool CanStart => _session != null && _blocker == null && !_running;

        public string StartLabel => SessionMessages.SplitStartLabel();
        public string StopLabel => SessionMessages.SplitStopLabel();

        internal RelayCommand Start { get; }
        internal RelayCommand Stop { get; }
        public System.Windows.Input.ICommand StartCommand => Start;
        public System.Windows.Input.ICommand StopCommand => Stop;

        /// <summary>Rularea pornita din comanda, ca testele si fereastra sa o poata astepta.</summary>
        internal Task Pending { get; private set; } = Task.CompletedTask;

        // ------------------------------------------------------------------ rularea

        public bool IsRunning
        {
            get => _running;
            private set
            {
                if (!Set(ref _running, value)) return;
                Raise(nameof(CanStart));
                Start.RaiseCanExecuteChanged();
                Stop.RaiseCanExecuteChanged();
            }
        }

        /// <summary>Ce se intampla acum: "partea 2 din 4: ruleaza ...". Gol cand nu ruleaza nimic.</summary>
        public string Phase { get => _phase; private set => Set(ref _phase, value); }

        /// <summary>Rularea Siril curenta (sau ultima), cu log-ul ei; null inainte de prima.</summary>
        public SirilRunViewModel Run
        {
            get => _sirilRun;
            private set { if (Set(ref _sirilRun, value)) Raise(nameof(HasRun)); }
        }

        public bool HasRun => _sirilRun != null;

        // ------------------------------------------------------------------ rezultatul

        public IReadOnlyList<string> Headers { get => _headers; private set => Set(ref _headers, value); }
        public IReadOnlyList<SplitTableRow> Rows { get => _rows; private set => Set(ref _rows, value); }

        /// <summary>Profilul de referinta al tabelului, sau de ce nu e unul.</summary>
        public string ReferenceLine { get => _referenceLine; private set => Set(ref _referenceLine, value); }

        public IReadOnlyList<SplitMap> Maps { get => _maps; private set => Set(ref _maps, value); }
        public MapScaleViewModel MapScale { get; } = new();
        public string MapLegend { get => _mapLegend; private set => Set(ref _mapLegend, value); }

        public string SavedTo
        {
            get => _savedTo;
            private set { if (Set(ref _savedTo, value)) Raise(nameof(SavedLine)); }
        }
        public string SaveError { get => _saveError; private set => Set(ref _saveError, value); }

        /// <summary>Ce s-a intamplat cu folderul temporar cand n-a fost sters; gol altfel.</summary>
        public string TempNote { get => _tempNote; private set => Set(ref _tempNote, value); }

        public string Meaning => SplitReport.Meaning().Replace('\n', ' ');

        // ------------------------------------------------------------------ atasarea

        internal void Attach(SirilSession session, CancellationToken ct, Func<Task> waitIdle)
        {
            _session = session;
            _ct = ct;
            _waitIdle = waitIdle;
            HasResult = false;
            ClearError();
            FrameCount = session.FlatFrames().Count;
            _pixels = SensorPixels(session);
            Script.Attach(session, _locate(), _guiRunning);   // Refreshed -> Refresh(), o singura data
        }

        /// <summary>Pixelii masterului, din headerul lui (doar headerul); 0 cand nu se poate citi.</summary>
        long SensorPixels(SirilSession session)
        {
            string master = session.FlatMasterPath;
            if (master == null) return 0;
            try { return SplitCost.Pixels(_loadInfo(master).Header); }
            catch (Exception) { return 0; }
        }

        /// <summary>Recalculeaza linia cadrelor si blocajul; la atasare, la schimbarea partilor sau a scriptului, si inainte de rulare.</summary>
        internal void Refresh()
        {
            if (_session == null) return;

            bool divides = FrameCount > 0 && FrameCount % Parts == 0;
            FramesLine = SessionMessages.SplitFramesLine(FrameCount, Parts, divides ? FrameCount / Parts : 0);
            Blocker = FrameCount == 0 ? SessionMessages.SplitNoFrames(_session)
                    : !divides ? SessionMessages.SplitNotDivisible(FrameCount, Parts)
                    : Script.Blocker;
            // fara siril-cli sau fara script nu se stie ce foldere intra in joc: nicio cifra langa un blocaj
            EstimateLine = Blocker == null ? Estimate() : "";
            Raise(nameof(CanStart));
            Start.RaiseCanExecuteChanged();
        }

        string Estimate()
        {
            string tempRoot = _tempRootFor(_clock());
            var plan = new SplitPlan(_session, Parts, tempRoot, Script.RequiredFolders);
            SplitCostEstimate e = SplitCost.Estimate(_pixels, Parts, plan.WholeFrameCount(), plan.WholeFolders.Count,
                                                     plan.FramesPerPart, plan.LightsToCopy().Count, plan.CopiedBytes());
            return SplitReport.CostLine(e, FreeBytes(tempRoot));
        }

        static long? FreeBytes(string path)
        {
            try
            {
                string root = Path.GetPathRoot(Path.GetFullPath(path));
                return string.IsNullOrEmpty(root) ? null : new DriveInfo(root).AvailableFreeSpace;
            }
            catch (Exception) { return null; }
        }

        // ------------------------------------------------------------------ rularea

        internal async Task StartAsync()
        {
            if (_session == null || _running) return;
            Script.Refresh();   // Refreshed -> Refresh()
            if (!CanStart) return;

            SirilSession session = _session;
            int parts = Parts;
            DateTime when = _clock();
            var plan = new SplitPlan(session, parts, _tempRootFor(when), Script.RequiredFolders);
            // flats/ se poate schimba intre deschidere si apasare: numarul de la deschidere
            // a trecut de verificare, dar planul se face pe ce e pe disc acum.
            if (plan.Frames.Count != FrameCount)
            {
                FrameCount = plan.Frames.Count;
                Refresh();
                if (!CanStart) return;
            }
            string script = Script.SelectedPath;
            string scriptName = Script.SelectedName;
            Func<Task> waitIdle = _waitIdle;

            _run = CancellationTokenSource.CreateLinkedTokenSource(_ct);
            CancellationToken ct = _run.Token;
            using var killSiril = ct.Register(() => Run?.Cancel());

            IsRunning = true;
            ClearError();
            HasResult = false;
            Report = "";
            SavedTo = SaveError = TempNote = "";
            Run = null;
            ProgressKnown = true;
            Progress = 0;
            bool keepTemp = false;
            int totalSteps = parts * 2 + 1;

            try
            {
                if (waitIdle != null) await waitIdle().ConfigureAwait(true);
                ct.ThrowIfCancellationRequested();

                var masters = new List<string>(parts);
                for (int i = 1; i <= parts; i++)
                {
                    int done = (i - 1) * 2;
                    var list = plan.CopyList(i);
                    Phase = SessionMessages.SplitPhaseCopy(i, parts, list.Count);
                    var progress = new Progress<double>(f => Progress = (done + f) / totalSteps);
                    await Task.Run(() => Copy(list, progress, ct), ct).ConfigureAwait(true);
                    ct.ThrowIfCancellationRequested();

                    Phase = SessionMessages.SplitPhaseSiril(i, parts, scriptName);
                    var run = new SirilRunViewModel(plan.PartRoot(i), script) { Warning = Script.Warning };
                    Run = run;
                    PropertyChangedEventHandler follow = (_, e) =>
                    {
                        if (e.PropertyName is nameof(run.Progress) or nameof(run.CommandIndex))
                            Progress = (done + 1 + SirilFraction(run)) / totalSteps;
                    };
                    run.PropertyChanged += follow;
                    try { await run.RunAsync(_runScript(plan.PartRoot(i), script)).ConfigureAwait(true); }
                    finally { run.PropertyChanged -= follow; }
                    ct.ThrowIfCancellationRequested();

                    if (run.Succeeded != true)
                    {
                        keepTemp = true;
                        Fail(SessionMessages.SplitFailedTitle(), SessionMessages.SplitFailed(i, parts, run.Outcome, plan.TempRoot));
                        return;
                    }
                    string master = plan.PartSession(i).FlatMasterPath;
                    if (master == null)
                    {
                        keepTemp = true;
                        Fail(SessionMessages.SplitFailedTitle(), SessionMessages.SplitNoMaster(i, parts, plan.PartRoot(i), scriptName));
                        return;
                    }
                    masters.Add(master);
                    Progress = (double)(done + 2) / totalSteps;
                }

                Phase = SessionMessages.SplitPhaseAnalyse(parts);
                StructureQuery query = null;
                string rawPath = session.ReferenceRawPath;
                if (rawPath != null && _catalog != null)
                {
                    ProfileSources sources = await _catalog.EnsureAsync().ConfigureAwait(true);
                    ct.ThrowIfCancellationRequested();
                    query = new StructureQuery { Sources = sources, RawPath = rawPath, LoadInfo = _loadInfo };
                }

                var text = new StringWriter();
                string wholePath = session.FlatMasterPath;
                var outcome = await Task.Run(() =>
                {
                    RunReport.WriteBanner(text);
                    var r = SplitRun.Execute(wholePath, masters, query, when, plan.FramesPerPart, text);
                    RunReport.WriteMapSaved(session.SplitPngPath, text);
                    return r;
                }, ct).ConfigureAwait(true);
                ct.ThrowIfCancellationRequested();

                Report = text.ToString();
                Populate(outcome.Result);
                Progress = 1;
                Save(session, outcome.Png);
            }
            catch (OperationCanceledException)
            {
                HasResult = false;
            }
            catch (Exception e)
            {
                keepTemp = true;
                Fail(SessionMessages.SplitFailedTitle(), Describe(e) + "\n\n" + SessionMessages.SplitTempKept(plan.TempRoot));
            }
            finally
            {
                if (keepTemp) TempNote = SessionMessages.SplitTempKept(plan.TempRoot);
                else if (!plan.Delete()) TempNote = SessionMessages.SplitTempNotDeleted(plan.TempRoot);
                Phase = "";
                IsRunning = false;
                _run.Dispose();
                _run = null;
                Refresh();
            }
        }

        static double SirilFraction(SirilRunViewModel run)
        {
            if (run.CommandCount <= 0 || run.CommandIndex <= 0) return 0;
            double within = run.ProgressKnown ? Math.Clamp(run.Progress, 0, 1) : 0;
            return Math.Clamp((run.CommandIndex - 1 + within) / run.CommandCount, 0, 1);
        }

        static void Copy(IReadOnlyList<(string From, string To)> list, IProgress<double> progress, CancellationToken ct)
        {
            for (int k = 0; k < list.Count; k++)
            {
                ct.ThrowIfCancellationRequested();
                var (from, to) = list[k];
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.Copy(from, to, overwrite: true);
                // un raw de pe card e adesea read-only, si File.Copy pastreaza atributul;
                // copia e a noastra si trebuie sa se poata sterge la sfarsit.
                File.SetAttributes(to, FileAttributes.Normal);
                progress.Report((k + 1.0) / list.Count);
            }
        }

        /// <summary>Opreste rularea: omoara Siril daca ruleaza, si folderul temporar se sterge ca la anulare.</summary>
        internal void StopRun()
        {
            try { _run?.Cancel(); } catch (ObjectDisposedException) { }
        }

        // ------------------------------------------------------------------ rezultatul

        internal void Populate(SplitResult r)
        {
            Headers = SplitReport.Headers(r);
            Rows = SplitReport.Rows(r).Select(row => new SplitTableRow
            {
                Label = row.Label,
                // pe randul de verdict valoarea E verdictul: doar pastila, nu cuvantul de doua ori
                Cells = row.Cells.Select(c => new SplitTableCell { Value = c.Verdict != null && c.Value == c.Verdict ? "" : c.Value, Verdict = c.Verdict }).ToList(),
                Note = row.Note,
                IsKey = row.IsKey,
                IsReference = row.IsReference,
            }).ToList();
            ReferenceLine = SplitReport.ReferenceLine(r);

            _sharedRange = r.MapRange;
            _mapCache.Clear();
            Maps = r.Columns.Select(c => new SplitMap(c.Label, c.Outcome.Structure.Residual)).ToList();
            RefreshMaps();
            HasResult = true;
        }

        void RefreshMaps()
        {
            if (_maps.Count == 0) return;

            // "auto" pe ecran e scara comuna a fisierului, nu scara proprie a fiecarei harti:
            // partile si intregul trebuie sa se poata compara cu ochiul (ADR 0054).
            PngWriter.MapRange range = MapScale.Selected.CapPct is double cap ? PngWriter.FixedRange(cap) : _sharedRange;
            for (int i = 0; i < _maps.Count; i++)
            {
                var key = (i, MapScale.Selected);
                if (!_mapCache.TryGetValue(key, out byte[] png))
                {
                    png = PngWriter.EncodeResidualMap(_maps[i].Raster, range);
                    _mapCache[key] = png;
                }
                _maps[i].Png = png;
            }
            MapLegend = RunReport.MapLegend(range);
        }

        void Save(SirilSession session, byte[] png)
        {
            SavedTo = "";
            SaveError = "";
            try
            {
                File.WriteAllText(session.SplitResultTextPath, Report);
                File.WriteAllBytes(session.SplitPngPath, png);
                SavedTo = session.SplitResultTextPath;
            }
            catch (Exception e)
            {
                SaveError = "Nu am putut salva raportul:\n" + Describe(e);
            }
        }

        public string SavedLine => string.IsNullOrEmpty(_savedTo) ? "" : SessionMessages.SplitSaved(_savedTo, _session?.SplitPngPath);

        internal override void Reset()
        {
            base.Reset();
            _session = null;
            Script.Detach();
            FrameCount = 0;
            FramesLine = "";
            Blocker = null;
            EstimateLine = "";
            _pixels = 0;
            Phase = "";
            Run = null;
            Headers = Array.Empty<string>();
            Rows = Array.Empty<SplitTableRow>();
            ReferenceLine = "";
            Maps = Array.Empty<SplitMap>();
            _mapCache.Clear();
            _sharedRange = default;
            MapLegend = "";
            SavedTo = SaveError = TempNote = "";
            Raise(nameof(CanStart));
            Start.RaiseCanExecuteChanged();
        }
    }
}
