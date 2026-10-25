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

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace LightsCheck.Wpf;

/// <summary>INotifyPropertyChanged with a Set that only raises on a change.</summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Raises <see cref="PropertyChanged"/> for one property.</summary>
    /// <param name="name">The property's name; the caller's own name when left out.</param>
    protected void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>
    /// Stores a value in a property's field and raises the change, only when it differs from the
    /// stored one by the type's default equality.
    /// </summary>
    /// <typeparam name="T">The property's type.</typeparam>
    /// <param name="field">The property's backing field.</param>
    /// <param name="value">The new value.</param>
    /// <param name="name">The property's name; the caller's own name when left out.</param>
    /// <returns>
    /// True when the value changed and the change was raised; false when it was equal.
    /// </returns>
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Raise(name);
        return true;
    }
}

/// <summary>
/// A command over a delegate; CanExecuteChanged is raised explicitly, not through CommandManager.
/// </summary>
/// <param name="run">What the command does.</param>
/// <param name="can">Whether it can run now; null = always.</param>
internal sealed class RelayCommand(Action run, Func<bool>? can = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;

    /// <summary>Whether the command can run now.</summary>
    /// <param name="parameter">The command parameter, not used.</param>
    /// <returns>True when no condition was given, else the condition's answer.</returns>
    public bool CanExecute(object? parameter) => can is null || can();

    /// <summary>Runs the command's delegate.</summary>
    /// <param name="parameter">The command parameter, not used.</param>
    public void Execute(object? parameter) => run();

    /// <summary>Tells the bound controls to ask <see cref="CanExecute"/> again.</summary>
    internal void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
