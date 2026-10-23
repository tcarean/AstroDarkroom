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

// RelayCommand.cs - ICommand over a delegate. The pair of ObservableObject.

using System;
using System.Windows.Input;

namespace AstroPhotoAnalyzer.Wpf
{
    /// <summary>
    /// An <see cref="ICommand"/> that runs one delegate and asks another whether it may; the
    /// command parameter is ignored by both.
    /// </summary>
    internal sealed class RelayCommand : ICommand
    {
        readonly Action _run;
        readonly Func<bool> _can;

        /// <summary>Makes a command over its delegates.</summary>
        /// <param name="run">
        /// What <see cref="Execute"/> runs; null throws ArgumentNullException.
        /// </param>
        /// <param name="can">
        /// What <see cref="CanExecute"/> asks; null means the command can always run.
        /// </param>
        internal RelayCommand(Action run, Func<bool> can = null)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
            _can = can;
        }

        public event EventHandler CanExecuteChanged;

        /// <summary>Whether the command may run now.</summary>
        /// <param name="parameter">Ignored.</param>
        /// <returns>True without a predicate, otherwise what the predicate says.</returns>
        public bool CanExecute(object parameter) => _can == null || _can();

        /// <summary>Runs the delegate; the caller checks <see cref="CanExecute"/> first.</summary>
        /// <param name="parameter">Ignored.</param>
        public void Execute(object parameter) => _run();

        /// <summary>
        /// Raises <see cref="CanExecuteChanged"/>: the owner of the command says when the answer
        /// changed, explicitly, not through CommandManager.RequerySuggested (see FlatCheck for
        /// why).
        /// </summary>
        internal void RaiseCanExecuteChanged()
            => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
