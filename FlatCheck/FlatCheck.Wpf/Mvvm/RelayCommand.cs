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

// RelayCommand.cs - ICommand over a delegate. ObservableObject's pair.

using System;
using System.Windows.Input;

namespace FlatCheck.Wpf
{
    /// <summary>A command that runs a delegate, enabled by another; refreshed only on
    /// <see cref="RaiseCanExecuteChanged"/>.</summary>
    internal sealed class RelayCommand : ICommand
    {
        readonly Action _run;
        readonly Func<bool> _can;

        /// <summary>Wraps the two delegates. Throws an
        /// <see cref="ArgumentNullException"/> when <paramref name="run"/> is null.</summary>
        /// <param name="run">What the command does; not null.</param>
        /// <param name="can">Whether it may run now; null = always.</param>
        internal RelayCommand(Action run, Func<bool> can = null)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            _can = can;
        }

        public event EventHandler CanExecuteChanged;

        /// <summary>Asks the enabling delegate.</summary>
        /// <param name="parameter">The binding's parameter; ignored.</param>
        /// <returns>True when there is no enabling delegate or it says yes.</returns>
        public bool CanExecute(object parameter) => _can == null || _can();

        /// <summary>Runs the command's delegate.</summary>
        /// <param name="parameter">The binding's parameter; ignored.</param>
        public void Execute(object parameter) => _run();

        /// <summary>
        /// Called when something CanExecute depends on changes. Explicit, not through
        /// CommandManager.RequerySuggested: here the buttons are disabled during a
        /// computation, and the automatic re-evaluation request would do it tens of times a
        /// second exactly when the UI thread has something else to do.
        /// </summary>
        internal void RaiseCanExecuteChanged()
            => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
