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

// TabViewModel.cs - what the seven tabs have in common.
//
// They all do the same thing: show a waiting screen, compute on another thread, then show either
// results or an explanation. That state - busy / result / error - lives here, once, so the seven
// do not end up with seven ways of failing. The Split tab is the one exception to the waiting
// screen: it keeps its own running state, so its log stays visible, and uses the result, the
// error and the progress (Progress, ProgressKnown, for its own bar), not RunGuardedAsync.
//
// SUPERSEDED RUNS. The histogram tab leaves its list clickable while it computes, so a second
// run can start over the first. RunGuardedAsync numbers its runs and lets only the latest one
// close the state: a superseded run does not turn off the waiting screen (the new one owns it),
// shows no error and does not clear the result. For the other tabs, which never overlap runs,
// the check is always true and changes nothing.
//
// WHY these classes ARE PUBLIC, when the rest of the application is internal: the WPF binding
// reads public properties through reflection. The consequence is useful, not only bureaucratic -
// a public view-model CANNOT expose a core type, which is internal, so the compiler itself stops
// a StructureResult from leaking into XAML. What shows on screen has to pass through plain
// fields, chosen on purpose.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using static FlatCheck.Ui;

namespace FlatCheck.Wpf
{
    /// <summary>The base of every tab's view-model: the busy, result and error state, the
    /// report text, and the guarded run that moves between them.</summary>
    public abstract class TabViewModel : ObservableObject
    {
        bool _busy;
        string _busyMessage = "";
        string _busyDetail = "";
        double _progress;
        bool _progressKnown;
        string _errorTitle;
        string _errorText;
        bool _hasResult;
        string _report = "";
        int _generation;

        /// <summary>While true, the tab shows its waiting screen.</summary>
        public bool IsBusy
        {
            get => _busy;
            private set { if (Set(ref _busy, value)) Raise(nameof(IsIdle)); }
        }

        public bool IsIdle => !_busy;

        public string BusyMessage { get => _busyMessage; private set => Set(ref _busyMessage, value); }

        /// <summary>The waiting screen's second line: which frame, the how-manieth of how
        /// many.</summary>
        public string BusyDetail { get => _busyDetail; protected set => Set(ref _busyDetail, value); }

        /// <summary>0..1. Meaningful only when <see cref="ProgressKnown"/> is true.</summary>
        public double Progress { get => _progress; protected set => Set(ref _progress, value); }

        /// <summary>
        /// False for a single analysis - there is no knowing how long it still takes, so the bar
        /// runs indeterminate. True only on a series, where the number of frames is known.
        /// </summary>
        public bool ProgressKnown { get => _progressKnown; protected set => Set(ref _progressKnown, value); }

        public string ErrorTitle { get => _errorTitle; private set => Set(ref _errorTitle, value); }

        public string ErrorText
        {
            get => _errorText;
            private set { if (Set(ref _errorText, value)) Raise(nameof(HasError)); }
        }

        public bool HasError => !string.IsNullOrEmpty(_errorText);

        /// <summary>True when the tab really has figures to show.</summary>
        public bool HasResult { get => _hasResult; protected set => Set(ref _hasResult, value); }

        /// <summary>
        /// The whole report, exactly as the console would have written it. Shown in a collapsible
        /// panel and what is saved to disk - reformatted nowhere.
        /// </summary>
        public string Report { get => _report; protected set => Set(ref _report, value); }

        /// <summary>
        /// Runs the work on another thread, behind the waiting screen, and turns any failure
        /// into an error the tab can show.
        ///
        /// The result is AWAITED, not abandoned: after the await, execution comes back to the UI
        /// thread (in the application; in the tests there is no context, so on the thread pool),
        /// so what follows may safely touch the properties bound in XAML.
        ///
        /// A cancellation is not an error - it means the user changed the folder or closed the
        /// window, and then the tab simply stays empty.
        ///
        /// A SUPERSEDED run - another one started over it - touches nothing once it ends: neither
        /// the result, nor the error, nor the waiting screen, which all belong to the new one.
        /// Its work stops by itself at the token check in the lambda; here it is only guaranteed
        /// that its ending is silent.
        /// </summary>
        /// <param name="busyMessage">The waiting screen's first line, Romanian, as the user
        /// reads it.</param>
        /// <param name="work">The run itself; it observes its own cancellation token.</param>
        /// <returns>A task that completes when the run ended, whatever the outcome; it never
        /// faults.</returns>
        protected async Task RunGuardedAsync(string busyMessage, Func<Task> work)
        {
            int gen = ++_generation;
            var clock = Stopwatch.StartNew();
            string run = "run " + LogText.Int(gen);
            Log.Info(run + " started");
            BeginBusy(busyMessage);
            _refused = false;
            try
            {
                await work().ConfigureAwait(true);
                // A run that ended in Refuse was refused, not done; a failure the tab worded
                // itself (Fail) is still "done", its own line said what happened.
                Log.Info(run + (_refused ? " refused in " : " done in ") + LogText.Ms(clock.ElapsedMilliseconds));
            }
            catch (OperationCanceledException)
            {
                Log.Info(run + " cancelled after " + LogText.Ms(clock.ElapsedMilliseconds));
                if (gen == _generation) HasResult = false;
            }
            catch (Exception e)
            {
                // A superseded run's failure is shown nowhere, but kept in the log (R-110).
                if (gen != _generation)
                {
                    Log.Debug(e, run + " failed after a newer run started; nothing is shown");
                    return;
                }
                Log.Error(e, run + " failed after " + LogText.Int(clock.ElapsedMilliseconds) + " ms; the tab shows the message");
                HasResult = false;
                Fail(T("The analysis failed", "Analiza a esuat"), Describe(e));
            }
            finally
            {
                if (gen == _generation) EndBusy();
            }
        }

        /// <summary>The tab's diagnostic log, named <c>Window.&lt;Tab&gt;</c>: its runs, the
        /// user's actions, the failures it shows or swallows (R-110). The logger's name says
        /// which tab, so the messages carry no prefix.</summary>
        private protected readonly Logger Log;

        /// <summary>The tab's name in the log: its view-model's class name without
        /// <c>TabViewModel</c> (<c>Structure</c>, <c>Exposure</c>, ...).</summary>
        private protected string TabName => GetType().Name.Replace("TabViewModel", "");

        /// <summary>Gives the tab its logger, by its runtime type's name.</summary>
        protected TabViewModel()
        {
            Log = LogManager.GetLogger(Loggers.Window + "." + TabName);
        }

        /// <summary>
        /// Enters the window's heavy-run gate before a run a click started (ADR 0061), with the
        /// waiting screen saying so: while another run holds the gate, the first line reads
        /// <see cref="SessionMessages.WaitingForOpeningChain"/> instead of the run's own message,
        /// which would claim work that has not begun; the run's message comes back once it is
        /// in. A free gate, or no gate at all, shows nothing. Of two runs waiting together, only
        /// the latest one puts its message back (the generation of <see cref="RunGuardedAsync"/>),
        /// so a superseded run does not write over the new one's.
        /// </summary>
        /// <param name="gate">The window's gate; null means there is none and the run starts at
        /// once.</param>
        /// <param name="ct">The run's token; a cancellation while queued throws
        /// <see cref="OperationCanceledException"/>, which <see cref="RunGuardedAsync"/>
        /// takes as a cancelled run.</param>
        /// <returns>The lease on the gate, to dispose when the heavy work is over; a no-op one
        /// without a gate.</returns>
        private protected async Task<IDisposable> EnterGateAsync(HeavyRunGate gate, CancellationToken ct)
        {
            if (gate == null) return NoLease.Instance;
            if (!gate.IsHeld) return await gate.EnterAsync(ct).ConfigureAwait(true);

            int gen = _generation;
            string message = BusyMessage;
            BusyMessage = SessionMessages.WaitingForOpeningChain();
            IDisposable lease = await gate.EnterAsync(ct).ConfigureAwait(true);
            if (gen == _generation) BusyMessage = message;
            return lease;
        }

        /// <summary>What <see cref="EnterGateAsync"/> hands back without a gate: nothing to
        /// release.</summary>
        sealed class NoLease : IDisposable
        {
            internal static readonly NoLease Instance = new();

            /// <inheritdoc/>
            public void Dispose() { }
        }

        /// <summary>
        /// An exception's message, with its causes if there are any. No stack trace is shown: on
        /// a broken FITS file the reader's message already says what could not be read, and a
        /// ten-frame stack on screen helps nobody redo the flats.
        /// </summary>
        /// <param name="e">The exception; not null.</param>
        /// <returns>The message, then each inner exception's message not already contained in
        /// it, separated by a blank line.</returns>
        internal static string Describe(Exception e)
        {
            string m = e.Message;
            for (Exception inner = e.InnerException; inner != null; inner = inner.InnerException)
                if (!string.IsNullOrWhiteSpace(inner.Message) && !m.Contains(inner.Message))
                    m += "\n\n" + inner.Message;
            return m;
        }

        /// <summary>Clears the error and shows the waiting screen, indeterminate, with an empty
        /// second line.</summary>
        /// <param name="message">The waiting screen's first line.</param>
        protected void BeginBusy(string message)
        {
            ClearError();
            BusyMessage = message;
            BusyDetail = "";
            Progress = 0;
            ProgressKnown = false;
            IsBusy = true;
        }

        /// <summary>Hides the waiting screen and clears its second line.</summary>
        protected void EndBusy()
        {
            IsBusy = false;
            BusyDetail = "";
        }

        /// <summary>
        /// Shows an explanation in place of the results. Title + text, because the second part is
        /// always long: the requirements ask to explain WHAT the missing file is and WHAT the user
        /// should do, not only that it is missing.
        /// </summary>
        /// <param name="title">The explanation's heading.</param>
        /// <param name="text">The explanation itself.</param>
        protected void Fail(string title, string text)
        {
            HasResult = false;
            ErrorTitle = title;
            ErrorText = text;
        }

        /// <summary>
        /// <see cref="Fail"/>, and a line in the log saying why, in English, with the title the
        /// user sees (R-110): for the refusals a tab decides itself, which no exception reaches
        /// the log for. A run that ends this way is logged as refused, not done.
        /// </summary>
        /// <param name="title">The explanation's heading.</param>
        /// <param name="text">The explanation itself.</param>
        /// <param name="why">The reason, in the log's words.</param>
        protected void Refuse(string title, string text, string why)
        {
            Log.Info("refused: " + why + "; the tab shows \"" + title + "\"");
            _refused = true;
            Fail(title, text);
        }

        /// <summary>True once <see cref="Refuse"/> ran during the current run, so its last
        /// line says <c>refused</c>; cleared when a run starts.</summary>
        bool _refused;

        /// <summary>Removes the explanation, if one is shown.</summary>
        protected void ClearError()
        {
            ErrorTitle = null;
            ErrorText = null;
        }

        /// <summary>For when the tab empties: another folder chosen, or an invalid
        /// one.</summary>
        internal virtual void Reset()
        {
            ClearError();
            HasResult = false;
            Report = "";
            EndBusy();
        }
    }
}
