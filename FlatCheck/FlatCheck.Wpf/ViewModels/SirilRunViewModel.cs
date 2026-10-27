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

// SirilRunViewModel.cs - a Siril script run, seen from its window.
//
// It receives the stdout lines on the process's reading threads and moves them to the UI thread
// with the dispatcher captured ONCE at the start - the same lesson as the lights series
// (LightTabViewModel): asked at every line, the answer can change midway and half the updates
// land on another thread.
//
// What is shown: the "log:" lines as text, as in Siril's panel; the progress lines as a bar and
// a label, not as text - they are three quarters of the output and would scroll like a waterfall;
// and the current command, "comanda 7 din 20", counted from the "Running command:" lines and read
// whole from the script, because Siril prints only the verb and "cd" says nothing without its
// argument. At the end, the run's verdict - succeeded, stopped at command X with code N,
// cancelled - and nothing else: the window does not interpret what the script did, the folder is
// reopened and Check() says whether the master appeared.

using System;
using NLog;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using static FlatCheck.Ui;

namespace FlatCheck.Wpf
{
    /// <summary>One run of a Siril script: its log, its progress, the current command and the
    /// verdict at the end. The run window shows it; the Split tab shows one per part
    /// inline.</summary>
    public sealed class SirilRunViewModel : ObservableObject
    {
        readonly StringBuilder _log = new();
        readonly IReadOnlyList<string> _commands;
        CancellationTokenSource _cts;
        Dispatcher _ui;
        int _commandIndex;

        string _logText = "";
        bool _running;
        bool _started;
        double _progress;
        bool _progressKnown;
        string _progressLabel = "";
        string _step = "";
        string _outcome = "";
        bool? _succeeded;
        string _warning;

        /// <summary>Prepares a run of the script, reading its commands from the file.</summary>
        /// <param name="folder">The session folder the script runs in.</param>
        /// <param name="scriptPath">The script's full path; an unreadable file gives a run with
        /// no commands listed.</param>
        public SirilRunViewModel(string folder, string scriptPath)
            : this(folder, scriptPath, ReadScript(scriptPath)) { }

        /// <summary>Prepares a run with the script's text given, for the tests.</summary>
        /// <param name="folder">The session folder the script runs in.</param>
        /// <param name="scriptPath">The script's full path; only its file name is shown.</param>
        /// <param name="scriptText">The script's text, from which the commands are
        /// listed.</param>
        internal SirilRunViewModel(string folder, string scriptPath, string scriptText)
        {
            Folder = folder;
            ScriptPath = scriptPath;
            ScriptName = Path.GetFileName(scriptPath);
            _commands = SirilScript.Commands(scriptText);
        }

        /// <summary>Reads the script's text.</summary>
        /// <param name="path">The script's full path.</param>
        /// <returns>The text, or "" when the file cannot be read.</returns>
        static string ReadScript(string path)
        {
            try { return File.ReadAllText(path); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                RunLog.Warn(e, "the script " + path + " could not be read; its commands are not listed");
                return "";
            }
        }

        /// <summary>The diagnostic log of the Siril runs: start, every line siril-cli prints,
        /// the end (R-110). Not <c>Log</c>, which is the text the window shows.</summary>
        static readonly Logger RunLog = LogManager.GetLogger(Loggers.Siril);

        public string Title => SessionMessages.RunWindowTitle();
        public string ScriptName { get; }
        public string ScriptPath { get; }
        public string Folder { get; }

        /// <summary>How many commands the script has - the denominator in "comanda 7 din
        /// 20".</summary>
        public int CommandCount => _commands.Count;

        /// <summary>Which command is running (from 1); 0 before the first. The Split tab makes
        /// its bar from it.</summary>
        public int CommandIndex { get => _commandIndex; private set => Set(ref _commandIndex, value); }

        /// <summary>The log lines, as Siril would show them, with a newline between
        /// them.</summary>
        public string Log { get => _logText; private set => Set(ref _logText, value); }

        public bool IsRunning
        {
            get => _running;
            private set { if (Set(ref _running, value)) Raise(nameof(IsFinished)); }
        }

        /// <summary>True after the run ended, however it ended.</summary>
        public bool IsFinished => _started && !_running;

        public double Progress { get => _progress; private set => Set(ref _progress, value); }
        public bool ProgressKnown { get => _progressKnown; private set => Set(ref _progressKnown, value); }
        public string ProgressLabel { get => _progressLabel; private set => Set(ref _progressLabel, value); }

        /// <summary>The current command, with its number out of the total.</summary>
        public string Step { get => _step; private set => Set(ref _step, value); }

        /// <summary>The run's verdict, empty while it runs.</summary>
        public string Outcome { get => _outcome; private set => Set(ref _outcome, value); }

        /// <summary>Null while running or not started; then whether the script exited with
        /// 0.</summary>
        public bool? Succeeded
        {
            get => _succeeded;
            private set { if (Set(ref _succeeded, value)) { Raise(nameof(IsSuccess)); Raise(nameof(IsFailure)); } }
        }

        public bool IsSuccess => _succeeded == true;
        public bool IsFailure => _succeeded == false;

        /// <summary>The card's warning (Siril open), repeated in the window. Null when there is
        /// none.</summary>
        public string Warning
        {
            get => _warning;
            set { if (Set(ref _warning, value)) Raise(nameof(HasWarning)); }
        }

        public bool HasWarning => !string.IsNullOrEmpty(_warning);

        /// <summary>
        /// Starts the run and awaits its end. Called once; the window calls it on loading. Any
        /// failure to START (the executable gone meanwhile) is a verdict on screen, not an
        /// exception in the window.
        /// </summary>
        /// <param name="runner">What runs siril-cli, feeding each stdout line to
        /// <see cref="OnLine"/>.</param>
        /// <returns>A task that completes when the run ended; it faults only when called a second
        /// time.</returns>
        internal async Task RunAsync(SirilRunner runner)
        {
            if (_started) throw new InvalidOperationException(T("the run has started already", "rularea a pornit deja"));
            _started = true;
            _ui = UiDispatcher();
            _cts = new CancellationTokenSource();
            IsRunning = true;
            Outcome = "";
            var clock = System.Diagnostics.Stopwatch.StartNew();
            RunLog.Info("run started: script=" + ScriptPath + " folder=" + Folder + " commands=" + LogText.Int(_commands.Count));

            try
            {
                int code = await runner(OnLine, _cts.Token).ConfigureAwait(true);
                RunLog.Info("run ended with code " + LogText.Int(code) + " after " + Seconds(clock) + " at " + CurrentCommand());
                Succeeded = code == 0;
                Outcome = code == 0
                    ? SessionMessages.RunSucceeded()
                    : SessionMessages.RunFailed(code, Step);
            }
            catch (OperationCanceledException)
            {
                RunLog.Info("run cancelled after " + Seconds(clock) + " at " + CurrentCommand());
                Succeeded = false;
                Outcome = SessionMessages.RunCancelled();
            }
            catch (Exception e)
            {
                RunLog.Error(e, "siril-cli could not be started or read; the window says so");
                Succeeded = false;
                Outcome = SessionMessages.RunStartFailed(TabViewModel.Describe(e));
            }
            finally
            {
                IsRunning = false;
                ProgressKnown = false;
                _cts.Dispose();
                _cts = null;
            }
        }

        /// <summary>The elapsed time in seconds, one decimal, for the log.</summary>
        /// <param name="clock">The run's clock.</param>
        /// <returns>The text.</returns>
        static string Seconds(System.Diagnostics.Stopwatch clock)
            => clock.Elapsed.TotalSeconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + " s";

        /// <summary>The command running, by number and as the script writes it, for the
        /// log.</summary>
        /// <returns>The text.</returns>
        string CurrentCommand()
            => "command " + LogText.Int(_commandIndex) + " of " + LogText.Int(_commands.Count)
             + (_commandIndex >= 1 && _commandIndex <= _commands.Count ? ": " + _commands[_commandIndex - 1] : "");

        /// <summary>Kills the process. No effect when nothing is running.</summary>
        public void Cancel()
        {
            try { _cts?.Cancel(); } catch (ObjectDisposedException) { }
        }

        /// <summary>From the process's reading threads, or directly in the tests.</summary>
        /// <param name="raw">One stdout line, as Siril printed it.</param>
        internal void OnLine(string raw)
        {
            SirilLogLine line = SirilLogLine.Parse(raw);
            // Every line in the log, the progress ones (most of the output) only at Trace.
            if (line.Kind == SirilLineKind.Progress) RunLog.Trace("siril: " + raw);
            else RunLog.Debug("siril: " + raw);
            if (line.Kind == SirilLineKind.Other) return;

            OnUi(() => Apply(line));
        }

        /// <summary>Shows one parsed line, on the UI thread: a progress line moves the bar and
        /// nothing else; any other line is appended to the log, and a command start also advances
        /// the step and resets the bar.</summary>
        /// <param name="line">The parsed line; never of kind Other.</param>
        void Apply(SirilLogLine line)
        {
            if (line.Kind == SirilLineKind.Progress)
            {
                ProgressKnown = true;
                Progress = Math.Clamp(line.Percent / 100.0, 0, 1);
                if (line.Label.Length > 0) ProgressLabel = line.Label;
                return;
            }

            if (line.IsCommandStart(out string verb))
            {
                CommandIndex = _commandIndex + 1;
                string full = _commandIndex <= _commands.Count ? _commands[_commandIndex - 1] : verb;
                Step = SessionMessages.RunStep(_commandIndex, _commands.Count, full);
                ProgressKnown = false;
                Progress = 0;
                ProgressLabel = "";
            }

            _log.Append(line.Text).Append('\n');
            Log = _log.ToString();
        }

        /// <summary>Runs the work on the dispatcher captured at the start, synchronously; directly
        /// when there is none (the tests), and not at all once the dispatcher is shutting
        /// down.</summary>
        /// <param name="work">The update to make.</param>
        void OnUi(Action work)
        {
            Dispatcher d = _ui;
            if (d == null) { work(); return; }
            if (d.HasShutdownStarted || d.HasShutdownFinished) return;

            try { d.Invoke(work); }
            catch (System.ComponentModel.Win32Exception) { }
            catch (TaskCanceledException) { }
        }

        /// <summary>The application's dispatcher, if it is alive.</summary>
        /// <returns>The dispatcher, or null without an application (the tests) or once it is
        /// shutting down.</returns>
        static Dispatcher UiDispatcher()
        {
            Dispatcher d = System.Windows.Application.Current?.Dispatcher;
            return d != null && !d.HasShutdownStarted && !d.HasShutdownFinished ? d : null;
        }
    }
}
