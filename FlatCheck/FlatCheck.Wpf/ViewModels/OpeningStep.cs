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

// OpeningStep.cs - one row of the opening panel (R-120, ADR 0079): a step of what runs by
// itself when a folder is opened, its state and the tab whose busy line and progress it shows.
// It formats, never decides: the state is set by MainViewModel as it awaits each run, and the
// words come from the tab or from a T pair.

using static FlatCheck.Ui;

namespace FlatCheck.Wpf
{
    /// <summary>Where a step of the opening stands.</summary>
    public enum OpeningState
    {
        /// <summary>Not started yet: the chain is before it, or waits for the gate.</summary>
        Waiting,
        /// <summary>Running now; the tab's waiting line and progress are shown.</summary>
        Running,
        /// <summary>Ended, with a result, a refusal or a failure, which the tab shows.</summary>
        Done,
    }

    /// <summary>
    /// One step of the opening: a label, the tab that runs it (its busy line, detail and
    /// progress are bound through <see cref="Tab"/>) and the state MainViewModel sets.
    /// </summary>
    public sealed class OpeningStep : ObservableObject
    {
        readonly string _en, _ro;
        OpeningState _state;

        /// <summary>A step on a tab, waiting.</summary>
        /// <param name="en">The label in English.</param>
        /// <param name="ro">The label in Romanian, without diacritics.</param>
        /// <param name="tab">The tab whose run this step is; not null.</param>
        internal OpeningStep(string en, string ro, TabViewModel tab)
        {
            _en = en;
            _ro = ro;
            Tab = tab;
        }

        /// <summary>The step's label, in the interface language.</summary>
        public string Label => T(_en, _ro);

        /// <summary>The tab that runs this step; its busy line, detail and progress are the
        /// row's while the step runs.</summary>
        public TabViewModel Tab { get; }

        /// <summary>Where the step stands; set by MainViewModel around each run.</summary>
        public OpeningState State
        {
            get => _state;
            internal set
            {
                if (!Set(ref _state, value)) return;
                Raise(nameof(IsWaiting));
                Raise(nameof(IsRunning));
                Raise(nameof(IsDone));
                Raise(nameof(StatusWord));
            }
        }

        public bool IsWaiting => _state == OpeningState.Waiting;
        public bool IsRunning => _state == OpeningState.Running;
        public bool IsDone => _state == OpeningState.Done;

        /// <summary>
        /// The state in a word, or, once done, the tab's own refusal or failure title when it
        /// has one - the panel repeats what the tab will show, it judges nothing.
        /// </summary>
        public string StatusWord => _state switch
        {
            OpeningState.Waiting => T("waiting", "asteapta"),
            OpeningState.Running => T("running...", "ruleaza..."),
            _ => Tab.HasError && !string.IsNullOrEmpty(Tab.ErrorTitle) ? Tab.ErrorTitle : T("done", "gata"),
        };
    }
}
