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
// opened, but one after the other. A 24 MP frame reaches hundreds of MB in the analysis (see
// docs/design/known-issues.md, point 7), and the series also holds a loaded frame beside it; two
// chains at once could go past a gigabyte on an ordinary machine. The time gained would not be
// worth the risk.
//
// NEW FOLDER = CANCELLATION. When another folder is chosen while something runs, the old work is
// cancelled and the tabs empty, instead of leaving results from the previous folder beside the
// new ones. The cancellation is caught between frames, so it answers in under a second.

using System;
using System.Threading;
using System.Threading.Tasks;

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

        /// <summary>The chain started on opening (structure, series, ratio), so the smoothing tab
        /// can wait for it.</summary>
        Task _chain = Task.CompletedTask;

        /// <summary>The application's view-model, every question answered by the real
        /// machine.</summary>
        public MainViewModel() : this(null, null, null) { }

        /// <summary>
        /// The two questions about the machine - where Siril is, and whether it runs - are given
        /// from outside so the tests do not depend on what is installed on the computer running
        /// them. Null means the real answer. The third is the smoothing tab's overwrite question:
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
        internal MainViewModel(Func<SirilInstall> locate, Func<bool> sirilGuiRunning, Func<string, bool> confirm = null,
                               Func<ProfileSources> locateProfiles = null, Func<string, LoadedFrame> loadInfo = null,
                               Func<string, string, SirilRunner> runScript = null, Func<DateTime> clock = null,
                               Func<DateTime, string> splitTempRootFor = null)
        {
            _locate = locate ?? SirilInstall.Locate;
            _sirilGuiRunning = sirilGuiRunning ?? SirilInstall.IsGuiRunning;

            // The profile catalog, one for the Structure tab (the vignetting comparison) and for
            // the Sintetic tab; the metadata reader, one as well, so the tests can put a made-up
            // frame in place of a raw.
            var catalog = new ProfileCatalog(locateProfiles);
            Structure = new StructureTabViewModel(catalog, loadInfo);
            Ratio = new RatioTabViewModel();
            Light = new LightTabViewModel();
            Exposure = new ExposureTabViewModel();
            HistEq = new HistEqTabViewModel();
            Smooth = new SmoothTabViewModel(confirm ?? AskOnScreen);
            Synthetic = new SyntheticTabViewModel(confirm ?? AskOnScreen, catalog, loadInfo);
            // The Split tab runs the Siril script like the card's button, so it gets the same two
            // questions about the machine, plus the run factory (in the tests, a fake Siril),
            // the clock and the temp root (in the tests, the test's folder).
            Split = new SplitTabViewModel(_locate, _sirilGuiRunning, runScript, catalog, loadInfo, clock, splitTempRootFor);
            Preprocess = new PreprocessViewModel();

            // The only link between two tabs: the smoothed flat Netezire writes appears in the
            // Histogram tab's list, beside the master. Wired here, so neither of them knows the
            // other; FileWritten ignores by itself a path that is not the current session's, so
            // a change of folder has nothing to unwire.
            Smooth.Saved += HistEq.FileWritten;
        }

        /// <summary>Asks a yes/no question in a MessageBox, No by default.</summary>
        /// <param name="question">The question, Romanian.</param>
        /// <returns>True when the user answered Yes.</returns>
        static bool AskOnScreen(string question)
            => System.Windows.MessageBox.Show(question, "FlatCheck", System.Windows.MessageBoxButton.YesNo,
                   System.Windows.MessageBoxImage.Question, System.Windows.MessageBoxResult.No)
               == System.Windows.MessageBoxResult.Yes;

        public StructureTabViewModel Structure { get; }
        public RatioTabViewModel Ratio { get; }
        public LightTabViewModel Light { get; }
        public ExposureTabViewModel Exposure { get; }
        public HistEqTabViewModel HistEq { get; }
        public SmoothTabViewModel Smooth { get; }
        public SyntheticTabViewModel Synthetic { get; }
        public SplitTabViewModel Split { get; }

        /// <summary>
        /// Completes when the chain started on opening has ended. The smoothing tab runs from a
        /// click, so it could overlap the structure run if the user pressed during it; it waits
        /// first - the "sequential, not parallel" rule above.
        /// </summary>
        /// <returns>The chain's task; already complete when nothing runs.</returns>
        internal Task WhenIdleAsync() => _chain;

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
        /// tabs are not shown at all: there is no point seeing four empty panels when what has to
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
        public bool IsRunning { get => _running; private set => Set(ref _running, value); }

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

            Folder = folder ?? "";
            ResetTabs();

            if (string.IsNullOrWhiteSpace(folder)) { ClearProblem(); return; }

            var session = new SirilSession(folder);
            SessionProblem problem = session.Check();
            if (problem != SessionProblem.None)
            {
                _session = null;
                ProblemTitle = SessionMessages.Title(problem);
                ProblemText = SessionMessages.Explain(session, problem);

                // A Siril script produces process/ and the master in it; no script helps a folder
                // that does not exist.
                if (problem == SessionProblem.NoProcessDir || problem == SessionProblem.NoFlatMaster)
                    Preprocess.Attach(session, _locate(), _sirilGuiRunning);
                return;
            }

            ClearProblem();
            _session = session;
            _cts = new CancellationTokenSource();
            CancellationToken ct = _cts.Token;

            // The tabs that wait for a choice are prepared at once: their lists are read from the
            // folder, not from a computation, so they have no reason to wait.
            Ratio.Attach(session, ct);
            Exposure.Attach(session, ct);
            // A list too, also without a computation: the 24 MP master is not loaded twice on
            // opening - Structure is already reading it, below.
            HistEq.Attach(session, ct);
            // The smoothing does not compute on opening either: it writes a file, and a file is
            // not produced because a folder was opened. It waits for a click.
            Smooth.Attach(session, ct, WhenIdleAsync);
            Synthetic.Attach(session, ct, WhenIdleAsync);
            // Nor does Split: it copies and runs Siril N times, so only on the button, and after
            // the chain.
            Split.Attach(session, ct, WhenIdleAsync);

            IsRunning = true;
            _chain = RunChainAsync(session, ct);
            try
            {
                await _chain.ConfigureAwait(true);
            }
            finally
            {
                IsRunning = false;
            }
        }

        /// <summary>The opening chain, one after the other: Structure, then the end-to-end
        /// series, then Ratio; it stops at the first cancellation.</summary>
        /// <param name="session">The folder just opened.</param>
        /// <param name="ct">The folder's token, cancelled by another folder or by closing the
        /// window.</param>
        /// <returns>A task that completes when the chain ended.</returns>
        async Task RunChainAsync(SirilSession session, CancellationToken ct)
        {
            await Structure.RunAsync(session, ct).ConfigureAwait(true);
            if (ct.IsCancellationRequested) return;

            await Light.RunAsync(session, ct).ConfigureAwait(true);
            if (ct.IsCancellationRequested) return;

            // The Ratio tab usually has two preselected masters; only now, after the other two
            // have finished, can it compute without fighting over memory.
            await Ratio.RecomputeAsync().ConfigureAwait(true);
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
            Preprocess.Detach();
            Structure.Reset();
            Ratio.Reset();
            Light.Reset();
            Exposure.Reset();
            HistEq.Reset();
            Smooth.Reset();
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
