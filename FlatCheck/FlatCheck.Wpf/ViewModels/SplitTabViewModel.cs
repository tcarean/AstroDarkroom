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

// SplitTabViewModel.cs - the Split tab (ADR 0054).
//
// A workflow, not an analysis: choose into how many parts (2 or 4) and which script, press, and
// the tab copies each part's slice into a temporary session (SplitPlan), runs the script in each
// through the same run as the problem card's button (SirilScriptRun + SirilRunViewModel, shown
// here instead of in its window), then hands the parts' masters and the whole master to the core
// (SplitRun), which compares, writes the report and composes the mosaic. Nothing seen in the table
// is computed here.
//
// IT DOES NOT USE THE WAITING SCREEN of the other tabs: that one covers the whole tab, and here
// the user must see the phase, the bar, Siril's log and the Opreste button. The running state is
// IsRunning, and the buttons and chips are disabled while it is true.
//
// ONE BAR FOR EVERYTHING: per part, the copy (by the number of files) and the script (by the
// current command out of CommandCount, with its progress), plus the analysis at the end. The
// phases are equal on the bar, not in time; what matters is that it moves forward and does not
// lie.
//
// TEMP: deleted after the analysis and on Opreste; it stays after a failure (a non-zero exit, or
// a script that left no master), with its path on screen - without it there is nothing to
// diagnose.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static FlatCheck.Ui;

namespace FlatCheck.Wpf
{
    /// <summary>A "2 parti" / "4 parti" chip.</summary>
    public sealed class SplitPartsOption : ObservableObject
    {
        readonly SplitTabViewModel _owner;
        bool _selected;

        /// <summary>Makes the chip, its label worded by SessionMessages.</summary>
        /// <param name="owner">The tab the chip belongs to; told when it is ticked.</param>
        /// <param name="parts">The number of parts, one of <c>SplitPlan.Choices</c>.</param>
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

        /// <summary>Unticks without re-entering the owner.</summary>
        internal void Unselect() => Set(ref _selected, false, nameof(IsSelected));
    }

    /// <summary>A cell of the table: its value and its verdict, already formatted by the
    /// core.</summary>
    public sealed class SplitTableCell
    {
        public string Value { get; init; }
        public string Verdict { get; init; }
        public bool HasVerdict => !string.IsNullOrEmpty(Verdict);
    }

    /// <summary>A row of the Split table: a quantity across the parts, the whole master and
    /// the reference profile, with its note.</summary>
    public sealed class SplitTableRow
    {
        public string Label { get; init; }
        public IReadOnlyList<SplitTableCell> Cells { get; init; }
        public string Note { get; init; }
        public bool IsKey { get; init; }
        /// <summary>The reference profile's row - the one that gives the depth's
        /// verdict.</summary>
        public bool IsReference { get; init; }
        public bool HasNote => !string.IsNullOrEmpty(Note);
    }

    /// <summary>A map of the mosaic, on screen: its label and its PNG on the chosen
    /// scale.</summary>
    public sealed class SplitMap : ObservableObject
    {
        byte[] _png;

        /// <summary>Makes the map, with no PNG yet.</summary>
        /// <param name="label">The column's label, as in the table.</param>
        /// <param name="raster">The column's residual raster, in percent.</param>
        internal SplitMap(string label, Img raster)
        {
            Label = label;
            Raster = raster;
        }

        public string Label { get; }
        internal Img Raster { get; }
        public byte[] Png { get => _png; internal set => Set(ref _png, value); }
    }

    /// <summary>The Split tab: the session's flats cut into 2 or 4 parts, each preprocessed by
    /// the chosen Siril script in a temporary session, and the parts' masters compared with the
    /// whole master in a table and a mosaic of maps.</summary>
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
        HeavyRunGate _gate;
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

        /// <summary>The application's tab, every question answered by the real machine and
        /// without a profile catalog.</summary>
        public SplitTabViewModel() : this(null, null, null) { }

        /// <summary>
        /// The same two questions about the machine as on the problem card (where Siril is,
        /// whether it runs), the run factory (in the tests, a fake Siril that writes the master
        /// where the script would have written it), the profile catalog and the metadata reader as
        /// on the Structure tab, the clock (the date in the report) and the temp root (in the
        /// tests, in the test's folder, not in %TEMP%). Null means the real answer.
        /// </summary>
        /// <param name="locate">Finds Siril on this machine; null for
        /// <c>SirilInstall.Locate</c>.</param>
        /// <param name="guiRunning">Whether Siril's own window is open; null for
        /// <c>SirilInstall.IsGuiRunning</c>.</param>
        /// <param name="runScript">Makes the runner of a script (second argument) in a folder
        /// (first); null for siril-cli through <c>SirilScriptRun.For</c>.</param>
        /// <param name="catalog">The window's shared profile catalog; null leaves the reference
        /// profile out.</param>
        /// <param name="loadInfo">Reads a file's header and metadata without its pixels; null for
        /// <c>ImageLoader.LoadInfo</c>.</param>
        /// <param name="clock">The current time; null for <c>DateTime.Now</c>.</param>
        /// <param name="tempRootFor">The temp root for a given time; null for
        /// <c>SplitPlan.NewTempRoot</c>.</param>
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

        // ------------------------------------------------------------------ the choice

        public string Intro => SessionMessages.SplitIntro();

        /// <summary>How much disk space the chosen split asks for: the copies exactly, process/
        /// estimated (SplitCost), the temporary disk.</summary>
        public string EstimateLine { get => _estimateLine; private set => Set(ref _estimateLine, value); }

        public IReadOnlyList<SplitPartsOption> PartOptions { get; }

        /// <summary>Into how many parts: 2 or 4.</summary>
        public int Parts { get => _parts; private set => Set(ref _parts, value); }

        /// <summary>Ticks the chip of that number of parts, unticks the other and refreshes the
        /// frames line, the blocker and the estimate; a number not offered is ignored.</summary>
        /// <param name="parts">The number of parts.</param>
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

        /// <summary>How many frames (raw or FITS) flats/ has; a readme.txt is not a
        /// frame.</summary>
        public int FrameCount { get => _frameCount; private set => Set(ref _frameCount, value); }

        public string FramesLine { get => _framesLine; private set => Set(ref _framesLine, value); }

        /// <summary>Why it cannot be pressed, or null when it can: the split's reason first, then
        /// the script choice's.</summary>
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

        /// <summary>The run started from the command, so the tests and the window can await
        /// it.</summary>
        internal Task Pending { get; private set; } = Task.CompletedTask;

        // ------------------------------------------------------------------ the run

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

        /// <summary>What is happening now: "partea 2 din 4: ruleaza ...". Empty when nothing
        /// runs.</summary>
        public string Phase { get => _phase; private set => Set(ref _phase, value); }

        /// <summary>The current (or last) Siril run, with its log; null before the
        /// first.</summary>
        public SirilRunViewModel Run
        {
            get => _sirilRun;
            private set { if (Set(ref _sirilRun, value)) Raise(nameof(HasRun)); }
        }

        public bool HasRun => _sirilRun != null;

        // ------------------------------------------------------------------ the result

        public IReadOnlyList<string> Headers { get => _headers; private set => Set(ref _headers, value); }
        public IReadOnlyList<SplitTableRow> Rows { get => _rows; private set => Set(ref _rows, value); }

        /// <summary>The table's reference profile, or why there is none.</summary>
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

        /// <summary>What happened to the temporary folder when it was not deleted; empty
        /// otherwise.</summary>
        public string TempNote { get => _tempNote; private set => Set(ref _tempNote, value); }

        public string Meaning => SplitReport.Meaning().Replace('\n', ' ');

        // ------------------------------------------------------------------ the attaching

        /// <summary>Enters the window's heavy-run gate with the phase line saying so while another
        /// run holds it: the tab has no busy overlay, so the phase line says what the overlay
        /// would.</summary>
        /// <param name="gate">The window's gate; null means there is none.</param>
        /// <param name="ct">The run's token; a cancellation while queued throws
        /// <see cref="OperationCanceledException"/>.</param>
        /// <returns>The lease to dispose when the heavy work is over, or null without a
        /// gate.</returns>
        async Task<IDisposable> EnterGateForPhaseAsync(HeavyRunGate gate, CancellationToken ct)
        {
            if (gate == null) return null;
            if (gate.IsHeld) Phase = SessionMessages.WaitingForRunningAnalysis();
            return await gate.EnterAsync(ct).ConfigureAwait(true);
        }

        /// <summary>Counts the session's frames, reads the master's sensor size and attaches the
        /// script choice, which refreshes the tab once; it starts nothing.</summary>
        /// <param name="session">The folder just opened.</param>
        /// <param name="ct">The folder's token, to which the run's own token is linked.</param>
        /// <param name="gate">The window's heavy-run gate: waited for before the copies start
        /// and held around the analysis of the part masters; null runs at once.</param>
        internal void Attach(SirilSession session, CancellationToken ct, HeavyRunGate gate)
        {
            _session = session;
            _ct = ct;
            _gate = gate;
            HasResult = false;
            ClearError();
            FrameCount = session.FlatFrames().Count;
            _pixels = SensorPixels(session);
            Script.Attach(session, _locate(), _guiRunning);   // Refreshed -> Refresh(), once
        }

        /// <summary>The master's pixels, from its header (the header only); 0 when it cannot be
        /// read.</summary>
        /// <param name="session">The session whose master is read.</param>
        /// <returns>The pixel count, or 0 without a master or with an unreadable one.</returns>
        long SensorPixels(SirilSession session)
        {
            string master = session.FlatMasterPath;
            if (master == null) return 0;
            try { return SplitCost.Pixels(_loadInfo(master).Header); }
            catch (Exception e)
            {
                Log.Debug(e, "the master's header could not be read; no pixel count for the estimate");
                return 0;
            }
        }

        /// <summary>Recomputes the frames line and the blocker; on attaching, on a change of
        /// parts or of script, and before the run.</summary>
        internal void Refresh()
        {
            if (_session == null) return;

            bool divides = FrameCount > 0 && FrameCount % Parts == 0;
            FramesLine = SessionMessages.SplitFramesLine(FrameCount, Parts, divides ? FrameCount / Parts : 0);
            Blocker = FrameCount == 0 ? SessionMessages.SplitNoFrames(_session)
                    : !divides ? SessionMessages.SplitNotDivisible(FrameCount, Parts)
                    : Script.Blocker;
            // without siril-cli or without a script it is not known which folders come into play:
            // no figure beside a blocker
            EstimateLine = Blocker == null ? Estimate() : "";
            Raise(nameof(CanStart));
            Start.RaiseCanExecuteChanged();
        }

        /// <summary>The disk-space line for the current choice, from a plan made now and the free
        /// space of the temp root's drive.</summary>
        /// <returns>The core's cost line.</returns>
        string Estimate()
        {
            string tempRoot = _tempRootFor(_clock());
            var plan = new SplitPlan(_session, Parts, tempRoot, Script.RequiredFolders);
            SplitCostEstimate e = SplitCost.Estimate(_pixels, Parts, plan.WholeFrameCount(), plan.WholeFolders.Count,
                                                     plan.FramesPerPart, plan.LightsToCopy().Count, plan.CopiedBytes());
            return SplitReport.CostLine(e, FreeBytes(tempRoot));
        }

        /// <summary>The free space on the drive that holds a path.</summary>
        /// <param name="path">A path, which need not exist yet.</param>
        /// <returns>The free bytes, or null when the drive cannot be asked.</returns>
        long? FreeBytes(string path)
        {
            try
            {
                string root = Path.GetPathRoot(Path.GetFullPath(path));
                return string.IsNullOrEmpty(root) ? null : new DriveInfo(root).AvailableFreeSpace;
            }
            catch (Exception e)
            {
                Log.Debug(e, "the free space of " + path + "'s drive could not be read");
                return null;
            }
        }

        // ------------------------------------------------------------------ the run

        /// <summary>The whole split: per part, the copy, then the script; then the analysis, the
        /// table, the maps and the saved report. It stops at the first failure and keeps the
        /// temporary folder then; otherwise it deletes it, after a cancellation too.</summary>
        /// <returns>A task that completes when the split ended; it never faults.</returns>
        internal async Task StartAsync()
        {
            if (_session == null || _running) return;
            Script.Refresh();   // Refreshed -> Refresh()
            if (!CanStart) return;

            SirilSession session = _session;
            int parts = Parts;
            DateTime when = _clock();
            var plan = new SplitPlan(session, parts, _tempRootFor(when), Script.RequiredFolders);
            // flats/ can change between the opening and the press: the count from the opening
            // passed the check, but the plan is made on what is on disk now.
            if (plan.Frames.Count != FrameCount)
            {
                FrameCount = plan.Frames.Count;
                Refresh();
                if (!CanStart) return;
            }
            string script = Script.SelectedPath;
            string scriptName = Script.SelectedName;
            HeavyRunGate gate = _gate;

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
            var clock = System.Diagnostics.Stopwatch.StartNew();
            Log.Info("started, parts=" + LogText.Int(parts) + " framesPerPart=" + LogText.Int(plan.FramesPerPart)
                     + " script=" + LogText.Str(script) + " temp=" + plan.TempRoot);

            try
            {
                // Nothing heavy runs when the copies start (another click's run, say), as before
                // the gate; the gate is not held through the Siril runs, which are other
                // processes, so the other tabs stay usable meanwhile. The analysis below takes it
                // again, around the part masters (ADR 0061).
                (await EnterGateForPhaseAsync(gate, ct).ConfigureAwait(true))?.Dispose();
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
                    Log.Info("part " + LogText.Int(i) + "/" + LogText.Int(parts) + " copied, " + LogText.Int(list.Count)
                             + " files into " + plan.PartRoot(i));

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
                        Log.Warn("Siril failed on part " + LogText.Int(i) + "/" + LogText.Int(parts) + "; the temp folder is kept");
                        keepTemp = true;
                        Fail(SessionMessages.SplitFailedTitle(), SessionMessages.SplitFailed(i, parts, run.Outcome, plan.TempRoot));
                        return;
                    }
                    string master = plan.PartSession(i).FlatMasterPath;
                    if (master == null)
                    {
                        Log.Warn("part " + LogText.Int(i) + "/" + LogText.Int(parts) + " has no master after Siril, in "
                                 + plan.PartRoot(i) + "; the temp folder is kept");
                        keepTemp = true;
                        Fail(SessionMessages.SplitFailedTitle(), SessionMessages.SplitNoMaster(i, parts, plan.PartRoot(i), scriptName));
                        return;
                    }
                    masters.Add(master);
                    Progress = (double)(done + 2) / totalSteps;
                }

                using IDisposable lease = await EnterGateForPhaseAsync(gate, ct).ConfigureAwait(true);
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
                Log.Info("stopped after " + LogText.Seconds(clock.Elapsed));
                HasResult = false;
            }
            catch (Exception e)
            {
                Log.Error(e, "failed after " + LogText.Seconds(clock.Elapsed) + "; the temp folder "
                             + plan.TempRoot + " is kept");
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

        /// <summary>How far a Siril run is through its script: the commands done plus the
        /// current one's progress, over the command count.</summary>
        /// <param name="run">The running script.</param>
        /// <returns>A fraction in 0..1; 0 before the first command or without a command
        /// list.</returns>
        static double SirilFraction(SirilRunViewModel run)
        {
            if (run.CommandCount <= 0 || run.CommandIndex <= 0) return 0;
            double within = run.ProgressKnown ? Math.Clamp(run.Progress, 0, 1) : 0;
            return Math.Clamp((run.CommandIndex - 1 + within) / run.CommandCount, 0, 1);
        }

        /// <summary>Copies the files of a part, creating their folders and clearing the read-only
        /// attribute, and reports the share done after each.</summary>
        /// <param name="list">The pairs of source and destination paths.</param>
        /// <param name="progress">Receives the share copied, 0..1, after each file.</param>
        /// <param name="ct">Checked before each file.</param>
        static void Copy(IReadOnlyList<(string From, string To)> list, IProgress<double> progress, CancellationToken ct)
        {
            for (int k = 0; k < list.Count; k++)
            {
                ct.ThrowIfCancellationRequested();
                var (from, to) = list[k];
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.Copy(from, to, overwrite: true);
                // a raw from a card is often read-only, and File.Copy keeps the attribute; the
                // copy is ours and must be deletable at the end.
                File.SetAttributes(to, FileAttributes.Normal);
                progress.Report((k + 1.0) / list.Count);
            }
        }

        /// <summary>Stops the run: kills Siril if it runs, and the temporary folder is deleted as
        /// on a cancellation.</summary>
        internal void StopRun()
        {
            Log.Info("stop asked" + (_run == null ? ", nothing runs" : ""));
            try { _run?.Cancel(); } catch (ObjectDisposedException) { }
        }

        // ------------------------------------------------------------------ the result

        /// <summary>Shows a split's result: the table from the core's rows, the reference line
        /// and one map per column on the shared range. Internal, for the rendering
        /// test.</summary>
        /// <param name="r">The core's result.</param>
        internal void Populate(SplitResult r)
        {
            Headers = SplitReport.Headers(r);
            Rows = SplitReport.Rows(r).Select(row => new SplitTableRow
            {
                Label = row.Label,
                // on the verdict row the value IS the verdict: only the pill, not the word twice
                Cells = row.Cells.Select(c => new SplitTableCell { Value = c.Verdict != null && c.Value == VerdictWords.Of(c.Verdict) ? "" : c.Value, Verdict = c.Verdict }).ToList(),
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

        /// <summary>Re-encodes every map for the chosen cap, from the cache when it can, and sets
        /// the legend; nothing before a result.</summary>
        void RefreshMaps()
        {
            if (_maps.Count == 0) return;

            // "auto" on screen is the file's shared scale, not each map's own scale: the parts and
            // the whole must be comparable by eye (ADR 0054).
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

        /// <summary>Writes the report and the mosaic beside the session; a failure is shown
        /// apart and leaves the result.</summary>
        /// <param name="session">The session folder.</param>
        /// <param name="png">The mosaic, on the shared automatic range.</param>
        void Save(SirilSession session, byte[] png)
        {
            SavedTo = "";
            SaveError = "";
            try
            {
                File.WriteAllText(session.SplitResultTextPath, Report);
                File.WriteAllBytes(session.SplitPngPath, png);
                SavedTo = session.SplitResultTextPath;
                Log.Info("wrote " + session.SplitResultTextPath + " (" + LogText.FileKiB(session.SplitResultTextPath) + ")");
                Log.Info("wrote " + session.SplitPngPath + " (" + LogText.KiB(png.Length) + ")");
            }
            catch (Exception e)
            {
                Log.Warn(e, "the report or the mosaic could not be saved beside the session");
                SaveError = T("Could not save the report:\n", "Nu am putut salva raportul:\n") + Describe(e);
            }
        }

        public string SavedLine => string.IsNullOrEmpty(_savedTo) ? "" : SessionMessages.SplitSaved(_savedTo, _session?.SplitPngPath);

        /// <inheritdoc/>
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
