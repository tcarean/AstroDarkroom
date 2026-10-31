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

// MainViewModel.cs - the session folder and the six tabs.
//
// Two decisions about behaviour deserve an explanation:
//
// SEQUENTIAL, NOT PARALLEL. Structure and the end-to-end test both start when a folder is
// opened, but one after the other, and no run a click starts computes beside them or beside
// another click's run: every heavy run takes the one HeavyRunGate (ADR 0061). Measured on
// 2026-10-25 on the 24 MP masters (docs/design/known-issues.md, point 7): one run alone peaks at
// 0.7-1.25 GB of working set, two at once at 1.3-2.2 GB. The time gained would not be worth
// the risk. The chain holds the gate once, from the structure run to the end of the series, so a
// click made during it waits for the whole chain, as it did before the gate.
//
// NEW FOLDER = CANCELLATION. When another folder is chosen while something runs, the old work is
// cancelled and the tabs empty, instead of leaving results from the previous folder beside the
// new ones. The cancellation is caught between frames, so it answers in under a second. The
// folder button itself is locked during the opening chain and while the Synthetic tab is
// busy (CanChooseFolder, R-117), so this applies to the other tabs' runs.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NLog;

namespace FlatCheck.Wpf
{
    /// <summary>The window's view-model: the session folder, the problem that stops it, the
    /// script block of the problem card, and the tabs, which it attaches and runs on every
    /// folder.</summary>
    public sealed class MainViewModel : ObservableObject
    {
        CancellationTokenSource _cts;
        SirilSession _session;

        string _folder = "";
        string _problemTitle;
        string _problemText;
        bool _running;

        readonly Func<SirilInstall> _locate;
        readonly Func<bool> _sirilGuiRunning;

        /// <summary>The one gate every heavy run takes: the opening chain once, each click-run
        /// around its computation (ADR 0061).</summary>
        readonly HeavyRunGate _gate;

        /// <summary>The application's view-model, every question answered by the real
        /// machine.</summary>
        public MainViewModel() : this(null, null, null) { }

        /// <summary>
        /// The two questions about the machine - where Siril is, and whether it runs - are given
        /// from outside so the tests do not depend on what is installed on the computer running
        /// them. Null means the real answer. The third is the Synthetic tab's overwrite question:
        /// by default a MessageBox, in the tests an answer given beforehand.
        /// </summary>
        /// <param name="locate">Finds Siril on this machine; null for
        /// <c>SirilInstall.Locate</c>.</param>
        /// <param name="sirilGuiRunning">Whether Siril's own window is open; null for
        /// <c>SirilInstall.IsGuiRunning</c>.</param>
        /// <param name="confirm">Asks a yes/no question before a file is overwritten; null for a
        /// MessageBox whose default is No.</param>
        /// <param name="locateProfiles">Finds the lens-profile sources for the shared
        /// <see cref="ProfileCatalog"/>; null for the real lookup.</param>
        /// <param name="loadInfo">Reads a file's header and metadata without its pixels; null for
        /// the real reader.</param>
        /// <param name="runScript">Makes the runner of a script in a folder, for the Split tab;
        /// null for the real siril-cli.</param>
        /// <param name="clock">The current time, for the Split tab's temp folder name; null for
        /// the system clock.</param>
        /// <param name="splitTempRootFor">The Split tab's temp root for a given time; null for the
        /// default one.</param>
        /// <param name="gate">The heavy-run gate; null for a new one. A test hands in a gate it
        /// holds, to see the runs wait.</param>
        internal MainViewModel(Func<SirilInstall> locate, Func<bool> sirilGuiRunning, Func<string, bool> confirm = null,
                               Func<ProfileSources> locateProfiles = null, Func<string, LoadedFrame> loadInfo = null,
                               Func<string, string, SirilRunner> runScript = null, Func<DateTime> clock = null,
                               Func<DateTime, string> splitTempRootFor = null, HeavyRunGate gate = null)
        {
            _locate = locate ?? SirilInstall.Locate;
            _sirilGuiRunning = sirilGuiRunning ?? SirilInstall.IsGuiRunning;
            _gate = gate ?? new HeavyRunGate();

            // The profile catalog, one for the Structure tab (the vignetting comparison) and for
            // the Sintetic tab; the metadata reader, one as well, so the tests can put a made-up
            // frame in place of a raw.
            var catalog = new ProfileCatalog(locateProfiles);
            Structure = new StructureTabViewModel(catalog, loadInfo);
            Light = new LightTabViewModel();
            Exposure = new ExposureTabViewModel();
            HistEq = new HistEqTabViewModel();
            Synthetic = new SyntheticTabViewModel(confirm ?? AskOnScreen, catalog, loadInfo);
            // The Split tab runs the Siril script like the card's button, so it gets the same two
            // questions about the machine, plus the run factory (in the tests, a fake Siril),
            // the clock and the temp root (in the tests, the test's folder).
            Split = new SplitTabViewModel(_locate, _sirilGuiRunning, runScript, catalog, loadInfo, clock, splitTempRootFor);
            Preprocess = new PreprocessViewModel();

            // The opening panel's rows (R-120, ADR 0079): what runs by itself on a folder, in
            // the order the user sees it end. The prefill runs beside the chain, not after it.
            _structureStep = new OpeningStep("Structure of the flat master, and the lens profile",
                                             "Structura masterului de flat, si profilul obiectivului", Structure);
            _lightStep = new OpeningStep("End to end on the series of lights", "End to end pe seria de lights", Light);
            _prefillStep = new OpeningStep("The lens list for the Synthetic tab", "Lista de obiective pentru tabul Sintetic", Synthetic);
            OpeningSteps = new[] { _structureStep, _lightStep, _prefillStep };

            // The folder button follows the tab whose main run takes longer than a folder
            // change (R-117, ADR 0077): its busy state is part of CanChooseFolder.
            Synthetic.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TabViewModel.IsIdle)) Raise(nameof(CanChooseFolder)); };
        }

        readonly OpeningStep _structureStep, _lightStep, _prefillStep;

        /// <summary>The opening panel's rows (R-120): the structure run, the end-to-end run and
        /// the Synthetic prefill, each with its state.</summary>
        public IReadOnlyList<OpeningStep> OpeningSteps { get; }

        /// <summary>Counts the openings, so the chain of an older one does not drop
        /// <see cref="IsRunning"/> under a newer one (R-120).</summary>
        int _opening;

        /// <summary>Asks a yes/no question in a MessageBox, No by default.</summary>
        /// <param name="question">The question, Romanian.</param>
        /// <returns>True when the user answered Yes.</returns>
        static bool AskOnScreen(string question)
            => System.Windows.MessageBox.Show(question, "FlatCheck", System.Windows.MessageBoxButton.YesNo,
                   System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.No)
               == System.Windows.MessageBoxResult.Yes;

        public StructureTabViewModel Structure { get; }
        public LightTabViewModel Light { get; }
        public ExposureTabViewModel Exposure { get; }
        public HistEqTabViewModel HistEq { get; }
        public SyntheticTabViewModel Synthetic { get; }
        public SplitTabViewModel Split { get; }

        /// <summary>
        /// The "run the script from here" block on the problem screen. It always exists, so the
        /// XAML bindings have something to stand on; it is offered only when the problem is one
        /// a script solves.
        /// </summary>
        public PreprocessViewModel Preprocess { get; }

        /// <summary>The session folder, or empty if none was chosen yet.</summary>
        public string Folder
        {
            get => _folder;
            private set { if (Set(ref _folder, value)) Raise(nameof(HasFolder)); }
        }

        public bool HasFolder => !string.IsNullOrEmpty(_folder);

        /// <summary>
        /// The problem that stops this folder from starting, if there is one. When it is set, the
        /// tabs are not shown at all: there is no point seeing six empty panels when what has to
        /// be done is to run a script in Siril.
        /// </summary>
        public string ProblemTitle { get => _problemTitle; private set => Set(ref _problemTitle, value); }

        public string ProblemText
        {
            get => _problemText;
            private set { if (Set(ref _problemText, value)) Raise(nameof(HasProblem)); }
        }

        public bool HasProblem => !string.IsNullOrEmpty(_problemText);

        /// <summary>True while the analyses started by opening the folder run.</summary>
        public bool IsRunning
        {
            get => _running;
            private set { if (Set(ref _running, value)) Raise(nameof(CanChooseFolder)); }
        }

        /// <summary>
        /// Whether the folder button is live: not during the opening chain, and not while the
        /// Synthetic tab is busy, the tab whose main run is long enough for a folder change to
        /// land on the new folder's tabs (measured 2026-10-30 on a 24 MP master: about 8 s;
        /// R-117, ADR 0077). The lock is per tab, so its shorter run (the stand-alone
        /// comparison) locks it too. The other tabs' runs take a few seconds and leave the
        /// button alone.
        /// </summary>
        public bool CanChooseFolder => !_running && Synthetic.IsIdle;

        /// <summary>The version and the copyright, the same as what the console writes on its
        /// first two lines.</summary>
        public string Banner => VersionInfo.Banner;
        public string Copyright => VersionInfo.Copyright;

        /// <summary>
        /// Opens a folder: validates it, prepares the tabs that wait for a choice, and starts the
        /// analyses that run by themselves.
        /// </summary>
        /// <param name="folder">The session folder; null or blank just empties the
        /// window.</param>
        /// <returns>A task that completes when the opening chain has ended, or at once when the
        /// folder has a problem.</returns>
        internal async Task OpenAsync(string folder)
        {
            CancelPending();
            int opening = ++_opening;

            Folder = folder ?? "";
            ResetTabs();

            // An opening that ends here is the latest: it owns the flag (R-120), whatever an
            // older one, still running and cancelled above, does in its finally.
            if (string.IsNullOrWhiteSpace(folder)) { ClearProblem(); _session = null; IsRunning = false; return; }

            var session = new SirilSession(folder);
            SessionProblem problem = session.Check();
            Log.Info("session " + folder + ": " + (problem == SessionProblem.None ? "usable" : "refused, " + problem));
            if (problem != SessionProblem.None)
            {
                _session = null;
                ProblemTitle = SessionMessages.Title(problem);
                ProblemText = SessionMessages.Explain(session, problem);

                // A Siril script produces process/ and the master in it; no script helps a folder
                // that does not exist.
                if (problem == SessionProblem.NoProcessDir || problem == SessionProblem.NoFlatMaster)
                    Preprocess.Attach(session, _locate(), _sirilGuiRunning);
                DescribeInLog(session, problem);
                IsRunning = false;
                return;
            }

            ClearProblem();
            _session = session;
            _cts = new CancellationTokenSource();
            CancellationToken ct = _cts.Token;

            IsRunning = true;
            try
            {
                // The folder's remembered switches (R-119): read once, before any run, so the
                // Structure run and the Exposure tab start on them.
                SessionSettings settings = SessionSettings.Read(session.SettingsPath);
                Log.Info("session " + folder + ": settings " + (File.Exists(session.SettingsPath) ? "read from " : "absent, defaults; would be ")
                         + session.SettingsPath + " noFilters=" + LogText.Yes(settings.NoFilters) + " skyFlat=" + LogText.Yes(settings.SkyFlat));

                // The tabs that wait for a choice are prepared at once: their lists are read from
                // the folder, not from a computation, so they have no reason to wait. Every run a
                // click starts in them takes the gate, so it waits for the chain below, and for
                // each other.
                Exposure.Attach(session, ct, _gate, settings);
                // A list too, also without a computation: the 24 MP master is not loaded twice
                // on opening - Structure is already reading it, below.
                HistEq.Attach(session, ct, _gate);
                // The synthetic flat does not compute on opening either: it writes a file, and a
                // file is not produced because a folder was opened. It waits for a click.
                Synthetic.Attach(session, ct, _gate);
                // The prefill is on its way from Attach: its row follows ITS task, captured now -
                // read later, the property would be the next folder's - so it reads done the
                // moment it ends, not when the chain does.
                _prefillStep.State = OpeningState.Running;
                Task prefill = WatchPrefillAsync(session, Synthetic.Pending);
                // Nor does Split: it copies and runs Siril N times, so only on the button, and
                // after the chain.
                Split.Attach(session, ct, _gate);
                DescribeInLog(session, problem);

                await RunChainAsync(session, ct, settings).ConfigureAwait(true);

                // Everything the opening started has to end before the tabs appear (R-120): the
                // prefill too, which never faults - a failure is its note.
                await prefill.ConfigureAwait(true);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // Check() looked only at what exists; listing the folders can still fail (a
                // folder the user may not list, a broken junction). The window fires this task
                // without awaiting it, so the exception would reach no handler: it goes to the
                // log whole and to the user as a card, not as six empty tabs (R-110).
                if (!ReferenceEquals(_session, session))
                {
                    Log.Debug(e, "session " + folder + ": the opening failed after another folder was opened; nothing is shown");
                    return;
                }
                Log.Error(e, "session " + folder + ": the opening failed; the card shows the message");
                _session = null;
                CancelPending();
                ResetTabs();
                ProblemTitle = SessionMessages.FolderUnreadableTitle();
                ProblemText = SessionMessages.FolderUnreadable(folder, TabViewModel.Describe(e));
            }
            finally
            {
                // Only the latest opening ends the panel: an older chain ending under a newer
                // opening must not show the tabs while the newer one still runs. From the screen
                // no folder opens over a running one (the button is locked, and the card's Siril
                // run exists only without a chain); a direct call, as in the tests, can.
                if (opening == _opening) IsRunning = false;
            }
        }

        /// <summary>Marks the prefill's row done when its own task ends, if the folder is still
        /// the open one (R-120).</summary>
        /// <param name="session">The folder the prefill belongs to.</param>
        /// <param name="prefill">The prefill's task, captured right after Attach; it never
        /// faults.</param>
        /// <returns>A task that completes when the prefill has.</returns>
        async Task WatchPrefillAsync(SirilSession session, Task prefill)
        {
            await prefill.ConfigureAwait(true);
            if (ReferenceEquals(_session, session)) _prefillStep.State = OpeningState.Done;
        }

        /// <summary>Writes <see cref="Describe"/> at <c>Debug</c>, after the card or the tabs
        /// are up: it lists the folders, and a folder that cannot be listed is a line here, not
        /// the end of the opening.</summary>
        /// <param name="session">The session.</param>
        /// <param name="problem">What its check found.</param>
        void DescribeInLog(SirilSession session, SessionProblem problem)
        {
            if (!Log.IsDebugEnabled) return;
            try { Log.Debug("session " + session.Root + ": " + Describe(session, problem)); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Log.Debug(e, "session " + session.Root + ": the folder could not be described");
            }
        }

        /// <summary>The diagnostic log of the window's own flow (R-110).</summary>
        static readonly Logger Log = LogManager.GetLogger(Loggers.Window);

        /// <summary>
        /// What a session folder holds, in one English line for the log: the master found (and
        /// an older one ignored), the counts of flats, raw flats and lights, the raw the lens is
        /// read from. Reads the disk, each count on its own: a folder that cannot be listed is
        /// written as <c>unreadable</c> with the exception's type, and the other counts stay.
        /// </summary>
        /// <param name="session">The session.</param>
        /// <param name="problem">What its check found.</param>
        /// <returns>The text.</returns>
        static string Describe(SirilSession session, SessionProblem problem)
        {
            if (problem == SessionProblem.NoFolder) return "the folder does not exist";
            FlatMasterLookup master = session.FindFlatMaster();
            // flats/ is listed once: the raw flats and the reference raw are read off the same
            // list, as the session reads them (RawFlats is FlatFrames narrowed to the raws).
            IReadOnlyList<string> flats = null;
            string flatCount = Read(() => { flats = session.FlatFrames(); return LogText.Int(flats.Count); });
            return "master=" + LogText.Str(master.Path) + (master.Ambiguous ? " (an older one ignored: " + master.Other + ")" : "")
                 + " flats=" + flatCount
                 + " rawFlats=" + (flats != null ? LogText.Int(flats.Count(RawReader.IsRawPath)) : flatCount)
                 + " lights=" + Count(() => session.LightSeries().Count)
                 + " referenceRaw=" + (flats != null ? LogText.Str(LensIdentification.FirstRaw(flats)) : flatCount)
                 + " process=" + LogText.Yes(Directory.Exists(session.ProcessDir)) + " masters=" + LogText.Yes(Directory.Exists(session.MastersDir));

            static string Count(Func<int> count) => Read(() => LogText.Int(count()));
            static string Read(Func<string> read)
            {
                try { return LogText.Str(read()); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { return "unreadable (" + e.GetType().Name + ")"; }
            }
        }

        /// <summary>The opening chain, one after the other: Structure, then the end-to-end
        /// series, holding the heavy-run gate throughout; the series does not start once the
        /// folder's token is cancelled.</summary>
        /// <param name="session">The folder just opened.</param>
        /// <param name="ct">The folder's token, cancelled by another folder or by closing the
        /// window.</param>
        /// <param name="settings">The folder's remembered switches (R-119), for the Structure
        /// run.</param>
        /// <returns>A task that completes when the chain ended.</returns>
        async Task RunChainAsync(SirilSession session, CancellationToken ct, SessionSettings settings)
        {
            // The gate, once for both runs. A run of the previous folder may still hold it: its
            // computation does not stop mid-way, so the new chain waits for it rather than loading
            // beside it. Another folder chosen during that wait cancels it, and that is no fault.
            IDisposable lease;
            try { lease = await _gate.EnterAsync(ct).ConfigureAwait(true); }
            catch (OperationCanceledException)
            {
                Log.Debug("session " + session.Root + ": the opening chain was cancelled while it waited for the gate");
                return;
            }

            using (lease)
            {
                // The rows of the opening panel follow the awaits (R-120); a step of a folder
                // closed meanwhile leaves no mark on the next folder's rows.
                _structureStep.State = OpeningState.Running;
                await Structure.RunAsync(session, ct, settings).ConfigureAwait(true);
                if (ct.IsCancellationRequested) return;
                _structureStep.State = OpeningState.Done;

                _lightStep.State = OpeningState.Running;
                await Light.RunAsync(session, ct).ConfigureAwait(true);
                if (ct.IsCancellationRequested) return;
                _lightStep.State = OpeningState.Done;
            }
        }

        /// <summary>Called when the window closes, so no threads are left working for
        /// nothing.</summary>
        internal void CancelPending()
        {
            CancellationTokenSource cts = _cts;
            _cts = null;
            if (cts == null) return;

            try { cts.Cancel(); } catch (ObjectDisposedException) { }
            cts.Dispose();
        }

        /// <summary>After a successful script run: the same folder, from the start, through
        /// Check().</summary>
        /// <returns>The task of <see cref="OpenAsync"/> on the current folder.</returns>
        internal Task ReopenAsync() => OpenAsync(Folder);

        /// <summary>Empties every tab and detaches the problem card's script block.</summary>
        void ResetTabs()
        {
            foreach (OpeningStep step in OpeningSteps) step.State = OpeningState.Waiting;
            Preprocess.Detach();
            Structure.Reset();
            Light.Reset();
            Exposure.Reset();
            HistEq.Reset();
            Synthetic.Reset();
            Split.Reset();
        }

        /// <summary>Removes the problem card.</summary>
        void ClearProblem()
        {
            ProblemTitle = null;
            ProblemText = null;
        }
    }
}
