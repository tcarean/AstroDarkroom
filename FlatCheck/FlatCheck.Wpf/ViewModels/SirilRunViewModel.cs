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

// SirilRunViewModel.cs - o rulare de script Siril, vazuta din fereastra ei.
//
// Primeste liniile de pe stdout de pe firele de citire ale procesului si le muta pe
// firul de interfata cu dispecerul captat O DATA la pornire - aceeasi lectie ca la seria
// de lights (LightTabViewModel): intrebat la fiecare linie, raspunsul se poate schimba
// la mijloc si jumatate din actualizari ajung pe alt fir.
//
// Ce se arata: liniile "log:" ca text, ca in panoul Siril; liniile de progres ca bara si
// eticheta, nu ca text - sunt trei sferturi din iesire si s-ar derula ca o cascada; si
// comanda curenta, "comanda 7 din 20", numarata din liniile "Running command:" si citita
// intreaga din script, fiindca Siril tipareste doar verbul si "cd" nu spune nimic fara
// argument. La final, verdictul rularii - reusit, oprit la comanda X cu codul N, anulat -
// si nimic altceva: fereastra nu interpreteaza ce a facut scriptul, folderul se
// redeschide si Check() spune daca a aparut masterul.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace FlatCheck.Wpf
{
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

        public SirilRunViewModel(string folder, string scriptPath)
            : this(folder, scriptPath, ReadScript(scriptPath)) { }

        internal SirilRunViewModel(string folder, string scriptPath, string scriptText)
        {
            Folder = folder;
            ScriptPath = scriptPath;
            ScriptName = Path.GetFileName(scriptPath);
            _commands = SirilScript.Commands(scriptText);
        }

        static string ReadScript(string path)
        {
            try { return File.ReadAllText(path); }
            catch (IOException) { return ""; }
            catch (UnauthorizedAccessException) { return ""; }
        }

        public string Title => SessionMessages.RunWindowTitle();
        public string ScriptName { get; }
        public string ScriptPath { get; }
        public string Folder { get; }

        /// <summary>Cate comenzi are scriptul - numitorul din "comanda 7 din 20".</summary>
        public int CommandCount => _commands.Count;

        /// <summary>Liniile de log, asa cum le-ar arata Siril, cu linie noua intre ele.</summary>
        public string Log { get => _logText; private set => Set(ref _logText, value); }

        public bool IsRunning
        {
            get => _running;
            private set { if (Set(ref _running, value)) Raise(nameof(IsFinished)); }
        }

        /// <summary>true dupa ce rularea s-a terminat, oricum s-ar fi terminat.</summary>
        public bool IsFinished => _started && !_running;

        public double Progress { get => _progress; private set => Set(ref _progress, value); }
        public bool ProgressKnown { get => _progressKnown; private set => Set(ref _progressKnown, value); }
        public string ProgressLabel { get => _progressLabel; private set => Set(ref _progressLabel, value); }

        /// <summary>Comanda curenta, cu numarul ei din total.</summary>
        public string Step { get => _step; private set => Set(ref _step, value); }

        /// <summary>Verdictul rularii, gol cat timp ruleaza.</summary>
        public string Outcome { get => _outcome; private set => Set(ref _outcome, value); }

        /// <summary>null cat timp ruleaza sau n-a pornit; apoi daca scriptul a iesit cu 0.</summary>
        public bool? Succeeded
        {
            get => _succeeded;
            private set { if (Set(ref _succeeded, value)) { Raise(nameof(IsSuccess)); Raise(nameof(IsFailure)); } }
        }

        public bool IsSuccess => _succeeded == true;
        public bool IsFailure => _succeeded == false;

        /// <summary>Avertismentul de pe card (Siril deschis), repetat in fereastra. Null cand nu e.</summary>
        public string Warning
        {
            get => _warning;
            set { if (Set(ref _warning, value)) Raise(nameof(HasWarning)); }
        }

        public bool HasWarning => !string.IsNullOrEmpty(_warning);

        /// <summary>
        /// Porneste rularea si asteapta sfarsitul ei. Se cheama o singura data; fereastra
        /// o cheama la incarcare. Orice esec de PORNIRE (executabil disparut intre timp)
        /// e un verdict pe ecran, nu o exceptie in fereastra.
        /// </summary>
        internal async Task RunAsync(SirilRunner runner)
        {
            if (_started) throw new InvalidOperationException("rularea a pornit deja");
            _started = true;
            _ui = UiDispatcher();
            _cts = new CancellationTokenSource();
            IsRunning = true;
            Outcome = "";

            try
            {
                int code = await runner(OnLine, _cts.Token).ConfigureAwait(true);
                Succeeded = code == 0;
                Outcome = code == 0
                    ? SessionMessages.RunSucceeded()
                    : SessionMessages.RunFailed(code, Step);
            }
            catch (OperationCanceledException)
            {
                Succeeded = false;
                Outcome = SessionMessages.RunCancelled();
            }
            catch (Exception e)
            {
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

        /// <summary>Omoara procesul. Fara efect daca nu ruleaza nimic.</summary>
        public void Cancel()
        {
            try { _cts?.Cancel(); } catch (ObjectDisposedException) { }
        }

        /// <summary>De pe firele de citire ale procesului, sau direct in teste.</summary>
        internal void OnLine(string raw)
        {
            SirilLogLine line = SirilLogLine.Parse(raw);
            if (line.Kind == SirilLineKind.Other) return;

            OnUi(() => Apply(line));
        }

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
                _commandIndex++;
                string full = _commandIndex <= _commands.Count ? _commands[_commandIndex - 1] : verb;
                Step = SessionMessages.RunStep(_commandIndex, _commands.Count, full);
                ProgressKnown = false;
                Progress = 0;
                ProgressLabel = "";
            }

            _log.Append(line.Text).Append('\n');
            Log = _log.ToString();
        }

        void OnUi(Action work)
        {
            Dispatcher d = _ui;
            if (d == null) { work(); return; }
            if (d.HasShutdownStarted || d.HasShutdownFinished) return;

            try { d.Invoke(work); }
            catch (System.ComponentModel.Win32Exception) { }
            catch (TaskCanceledException) { }
        }

        static Dispatcher UiDispatcher()
        {
            Dispatcher d = System.Windows.Application.Current?.Dispatcher;
            return d != null && !d.HasShutdownStarted && !d.HasShutdownFinished ? d : null;
        }
    }
}
