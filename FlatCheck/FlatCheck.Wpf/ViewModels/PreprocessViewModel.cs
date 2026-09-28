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

// PreprocessViewModel.cs - alegerea unui script Siril, si blocul "ruleaza scriptul de
// aici" de pe ecranul de problema, care e cel mai mic consumator al ei.
//
// ScriptChoiceViewModel e partea comuna: lista de scripturi din meniul Siril, scriptul
// propus, blocajul (siril-cli negasit, niciun script, un subfolder de intrare gol) si
// avertismentul ca Siril e deschis. O folosesc cardul de problema (PreprocessViewModel,
// care adauga doar textul de deasupra listei) si tabul Split, care ruleaza acelasi script
// pe fiecare sesiune temporara (ADR 0054). Alegerea se face pe card sau pe tab, nu in
// fereastra de rulare: fereastra aia e log-ul unei rulari deja pornite, si un ecran care
// intai intreaba si apoi ruleaza ar fi doua ecrane in unul.
//
// Nu decide nimic despre flat-uri. Traduce trei intrebari de mediu - exista Siril, ce
// scripturi stie, are folderul ce cere scriptul - in text si intr-un buton activ sau nu.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FlatCheck.Wpf
{
    /// <summary>Un script din lista: numele pentru ecran, calea pentru rulare.</summary>
    public sealed class ScriptOption
    {
        internal ScriptOption(string path)
        {
            Path = path;
            Name = System.IO.Path.GetFileName(path);
        }

        public string Name { get; }
        internal string Path { get; }

        public override string ToString() => Name;
    }

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

        /// <summary>true cat timp e atasata unei sesiuni: pe card, cand folderul are o problema pe care un script o rezolva.</summary>
        public bool IsOffered { get => _offered; private set => Set(ref _offered, value); }

        /// <summary>Scripturile din meniul Siril, in ordinea de acolo.</summary>
        public IReadOnlyList<ScriptOption> Scripts { get => _scripts; private set => Set(ref _scripts, value); }

        /// <summary>Scriptul ales; implicit cel din SirilInstall.DefaultScript daca exista.</summary>
        public ScriptOption Selected
        {
            get => _selected;
            set { if (Set(ref _selected, value)) Refresh(); }
        }

        /// <summary>De ce nu se poate apasa butonul, sau null cand se poate.</summary>
        public string Blocker
        {
            get => _blocker;
            private set { if (Set(ref _blocker, value)) { Raise(nameof(HasBlocker)); Raise(nameof(CanRun)); } }
        }

        public bool HasBlocker => !string.IsNullOrEmpty(_blocker);

        /// <summary>Avertismentul ca Siril e deschis, sau null. Nu blocheaza - vezi SirilInstall.IsGuiRunning.</summary>
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

        /// <summary>Se ridica dupa fiecare Refresh, ca un consumator sa-si recalculeze propriul blocaj peste al alegerii.</summary>
        internal event Action Refreshed;

        /// <summary>Pregateste blocul pentru un folder. Lista si alegerea implicita se fac o data, aici.</summary>
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
        /// Recalculeaza blocajul si avertismentul. Se cheama la atasare, la schimbarea
        /// scriptului, si inca o data chiar inainte de rulare - Siril se poate deschide
        /// sau inchide intre timp, si un folder de intrare se poate umple.
        /// </summary>
        internal void Refresh()
        {
            if (!_offered || _session == null) return;

            Warning = _guiRunning() ? SessionMessages.SirilGuiRunning() : null;
            Blocker = FindBlocker();
            Raise(nameof(CanRun));
            Refreshed?.Invoke();
        }

        string FindBlocker()
        {
            if (!_install.HasCli) return SessionMessages.SirilCliNotFound();
            if (Scripts.Count == 0) return SessionMessages.NoSirilScripts(_install.ConfigPath);
            if (_selected == null) return SessionMessages.NoScriptChosen();

            string text;
            try { text = File.ReadAllText(_selected.Path); }
            catch (IOException e) { return SessionMessages.ScriptUnreadable(_selected.Path, e.Message); }
            catch (UnauthorizedAccessException e) { return SessionMessages.ScriptUnreadable(_selected.Path, e.Message); }

            var required = SirilScript.RequiredFolders(text);
            var missing = _session.MissingInputFolders(required);
            return missing.Count == 0 ? null : SessionMessages.MissingScriptInputs(_selected.Name, required, missing);
        }
    }

    /// <summary>Blocul de pe cardul de problema: alegerea scriptului plus textul de deasupra ei.</summary>
    public sealed class PreprocessViewModel : ScriptChoiceViewModel
    {
        public string Lead => SessionMessages.PreprocessLead();
    }
}
