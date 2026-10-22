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

namespace MindfulnessReminder.Core;

/// <summary>
/// Decides what auto-start should do on each launch.
///
/// The requirement is that the app registers itself for Windows startup, but that must not
/// mean "re-register on every launch" -- that would silently undo the user unticking the
/// tray checkbox. So registration happens once, on the very first run, and is recorded by a
/// marker value. Later launches only repair a stale executable path.
/// </summary>
public sealed class StartupRegistrar
{
    public const string SettingsKeyPath = @"Software\MindfulnessReminder";
    public const string RegisteredMarkerValueName = "AutoStartInitialized";

    private readonly IRegistryStore _registry;
    private readonly IAutoStartManager _autoStart;

    /// <summary>A registrar that keeps its marker under <see cref="SettingsKeyPath"/>.</summary>
    /// <param name="registry">The per-user registry, which holds the marker.</param>
    /// <param name="autoStart">
    /// The auto-start entry to register on the first run and repair after it.
    /// </param>
    public StartupRegistrar(IRegistryStore registry, IAutoStartManager autoStart)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(autoStart);

        _registry = registry;
        _autoStart = autoStart;
    }

    /// <summary>
    /// On the first run, registers auto-start and writes the marker; on every later run, only
    /// repairs a stale path. A key this account may not read or write is not an error: nothing
    /// is registered, no marker is written, and the next start tries again. A Run value this
    /// run wrote before the refusal is deleted again; one that was there before the first run
    /// stays.
    /// </summary>
    /// <returns><c>true</c> when this call performed the initial registration.</returns>
    public bool EnsureRegisteredOnFirstRun()
    {
        try
        {
            if (_registry.GetString(SettingsKeyPath, RegisteredMarkerValueName) is not null)
            {
                _autoStart.RepairPathIfNeeded();
                return false;
            }
        }
        catch (Exception ex) when (IsRefusal(ex))
        {
            // A key this account may not read or write (a policy, security software) is not a
            // reason to fail to start: the next start tries again.
            return false;
        }

        // An entry that was there before the first run was not written by it, so a refusal
        // below must not take it away. GetStatus does not throw for a value it may not read.
        var foundEntry = _autoStart.GetStatus() is not AutoStartStatus.Disabled;

        try
        {
            _autoStart.Enable();
            _registry.SetString(SettingsKeyPath, RegisteredMarkerValueName, "1");
            return true;
        }
        catch (Exception ex) when (IsRefusal(ex))
        {
            // Enable may have written the Run value before the Task Manager key or the marker's
            // key refused. Left there without its marker, it would be registered again at every
            // start, undoing an untick. A click on the tray item reports a refusal of the Run or
            // Task Manager key.
            if (!foundEntry)
            {
                TryDisable();
            }

            return false;
        }
    }

    /// <summary>
    /// Deletes the Run value this run wrote. A refusal is let go: the Run key then refused the
    /// first write as well, so there is nothing to delete.
    /// </summary>
    private void TryDisable()
    {
        try
        {
            _autoStart.Disable();
        }
        catch (Exception ex) when (IsRefusal(ex))
        {
            // Nothing was written to a Run key that refuses writes.
        }
    }

    /// <summary>Whether a registry access failed because this account may not make it.</summary>
    /// <param name="exception">What the access threw.</param>
    /// <returns>
    /// <c>true</c> for an <see cref="UnauthorizedAccessException"/> or a
    /// <see cref="System.Security.SecurityException"/>.
    /// </returns>
    private static bool IsRefusal(Exception exception) =>
        exception is UnauthorizedAccessException or System.Security.SecurityException;
}
