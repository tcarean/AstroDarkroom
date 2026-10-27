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

// PreprocessViewModel.cs - the choice of a Siril script, and the "run the script from here" block
// on the problem screen, which is its smallest consumer.
//
// ScriptChoiceViewModel is the common part: the list of scripts from Siril's menu, the proposed
// script, the blocker (siril-cli not found, no script, an empty input subfolder) and the warning
// that Siril is open. The problem card uses it (PreprocessViewModel, which only adds the text
// above the list) and so does the Split tab, which runs the same script on each temporary session
// (ADR 0054). The choice is made on the card or on the tab, not in the run window: that window is
// the log of a run already started, and a screen that first asks and then runs would be two
// screens in one.
//
// It decides nothing about flats. It translates three questions about the environment - does
// Siril exist, which scripts does it know, does the folder have what the script asks for - into
// text and a button that is enabled or not.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlatCheck.Wpf
{
    /// <summary>A script in the list: the name for the screen, the path for the run.</summary>
    public sealed class ScriptOption
    {
        /// <summary>Makes the option; its name is the file name of the path.</summary>
        /// <param name="path">The script's full path.</param>
        internal ScriptOption(string path)
        {
            Path = path;
            Name = System.IO.Path.GetFileName(path);
        }

        public string Name { get; }
        internal string Path { get; }

        /// <summary>The script's file name, which the combo box shows.</summary>
        /// <returns><see cref="Name"/>.</returns>
        public override string ToString() => Name;
    }

    /// <summary>The choice of a Siril script for a session folder: the list, the chosen one, what
    /// blocks the run and the warning that Siril is open. The problem card and the Split tab each
    /// hold one.</summary>
    public class ScriptChoiceViewModel : ObservableObject
    {
        SirilSession _session;
        SirilInstall _install;
        Func<bool> _guiRunning;

        IReadOnlyList<ScriptOption> _scripts = Array.Empty<ScriptOption>();
        ScriptOption _selected;
        string _blocker;
        string _warning;
        bool _offered;

        /// <summary>True while attached to a session: on the card, when the folder has a problem
        /// a script solves.</summary>
        public bool IsOffered { get => _offered; private set => Set(ref _offered, value); }

        /// <summary>The scripts from Siril's menu, in the order they have there.</summary>
        public IReadOnlyList<ScriptOption> Scripts { get => _scripts; private set => Set(ref _scripts, value); }

        /// <summary>The chosen script; by default SirilInstall.DefaultScript's, if it
        /// exists.</summary>
        public ScriptOption Selected
        {
            get => _selected;
            set { if (Set(ref _selected, value)) Refresh(); }
        }

        /// <summary>Why the button cannot be pressed, or null when it can.</summary>
        public string Blocker
        {
            get => _blocker;
            private set { if (Set(ref _blocker, value)) { Raise(nameof(HasBlocker)); Raise(nameof(CanRun)); } }
        }

        public bool HasBlocker => !string.IsNullOrEmpty(_blocker);

        /// <summary>The warning that Siril is open, or null. It does not block - see
        /// SirilInstall.IsGuiRunning.</summary>
        public string Warning
        {
            get => _warning;
            private set { if (Set(ref _warning, value)) Raise(nameof(HasWarning)); }
        }

        public bool HasWarning => !string.IsNullOrEmpty(_warning);

        public bool CanRun => _offered && _blocker == null && _selected != null;

        internal string CliPath => _install?.CliPath;
        internal string SelectedPath => _selected?.Path;
        internal string SelectedName => _selected?.Name;

        /// <summary>The folders the chosen script enters, read at the last Refresh; empty when the
        /// script could not be read.</summary>
        internal IReadOnlyList<string> RequiredFolders { get; private set; } = Array.Empty<string>();

        /// <summary>Raised after every Refresh, so a consumer can recompute its own blocker on top
        /// of the choice's.</summary>
        internal event Action Refreshed;

        /// <summary>Prepares the block for a folder. The list and the default choice are made
        /// once, here.</summary>
        /// <param name="session">The session folder the script will run in.</param>
        /// <param name="install">The Siril found on this machine; null counts as none.</param>
        /// <param name="guiRunning">Whether Siril's own window is open, asked at every Refresh;
        /// null counts as never.</param>
        internal void Attach(SirilSession session, SirilInstall install, Func<bool> guiRunning)
        {
            _session = session;
            _install = install ?? new SirilInstall(null, Array.Empty<string>());
            _guiRunning = guiRunning ?? (() => false);

            Scripts = _install.Scripts.Select(p => new ScriptOption(p)).ToList();
            string def = _install.DefaultScriptPath;
            _selected = Scripts.FirstOrDefault(o => o.Path == def);
            Raise(nameof(Selected));

            IsOffered = true;
            Refresh();
        }

        /// <summary>Empties the block: no session, no list, no choice, nothing offered.</summary>
        internal void Detach()
        {
            _session = null;
            IsOffered = false;
            Scripts = Array.Empty<ScriptOption>();
            _selected = null;
            Raise(nameof(Selected));
            Blocker = null;
            Warning = null;
            Raise(nameof(CanRun));
        }

        /// <summary>
        /// Recomputes the blocker and the warning. Called on attaching, on a change of script, and
        /// once more right before the run - Siril can be opened or closed meanwhile, and an input
        /// folder can fill up.
        /// </summary>
        internal void Refresh()
        {
            if (!_offered || _session == null) return;

            Warning = _guiRunning() ? SessionMessages.SirilGuiRunning() : null;
            RequiredFolders = Array.Empty<string>();
            Blocker = FindBlocker();
            Raise(nameof(CanRun));
            Refreshed?.Invoke();
        }

        /// <summary>The first thing that stops the run, in order: no siril-cli, no script in the
        /// menu, none chosen, the chosen one unreadable, an input folder missing or empty. Sets
        /// <see cref="RequiredFolders"/> when the script was read.</summary>
        /// <returns>The explanation to show, or null when nothing blocks.</returns>
        string FindBlocker()
        {
            if (!_install.HasCli) return SessionMessages.SirilCliNotFound();
            if (Scripts.Count == 0) return SessionMessages.NoSirilScripts(_install.ConfigPath);
            if (_selected == null) return SessionMessages.NoScriptChosen();

            string text;
            try { text = File.ReadAllText(_selected.Path); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                NLog.LogManager.GetLogger("Siril").Warn(e, "the script " + _selected.Path + " could not be read; the run is blocked");
                return SessionMessages.ScriptUnreadable(_selected.Path, e.Message);
            }

            var required = SirilScript.RequiredFolders(text);
            RequiredFolders = required;
            var missing = _session.MissingInputFolders(required);
            return missing.Count == 0 ? null : SessionMessages.MissingScriptInputs(_selected.Name, required, missing);
        }
    }

    /// <summary>The block on the problem card: the choice of script plus the text above
    /// it.</summary>
    public sealed class PreprocessViewModel : ScriptChoiceViewModel
    {
        public string Lead => SessionMessages.PreprocessLead();
    }
}
