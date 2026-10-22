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
/// marker value. Later launches only repair a stale executable path, and so does a first run
/// that finds an entry already there.
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
    /// On every run, repairs a stale path. On the first run, registers auto-start and writes
    /// the marker; an entry the first run finds already there is only repaired, and the marker
    /// is written. A key this account may not read or write is not an error: no registration
    /// is made, no marker is written, and the next start tries again. A path repaired before
    /// the refusal stays repaired; a Run value this run wrote is deleted again.
    /// </summary>
    /// <returns>
    /// <c>true</c> when this call performed the initial registration; <c>false</c> on a later
    /// run, when an entry was already there, or when Windows refused an access.
    /// </returns>
    public bool EnsureRegisteredOnFirstRun()
    {
        // Set once this run may write a Run value that a refusal must take back.
        var undoOnRefusal = false;

        try
        {
            var firstRun = _registry.GetString(SettingsKeyPath, RegisteredMarkerValueName) is null;

            // The repair reads the Run value before anything is written, so an entry this
            // account may not read stops the run here, rather than reading as no entry below
            // and being overwritten.
            _autoStart.RepairPathIfNeeded();
            if (!firstRun)
            {
                return false;
            }

            if (_autoStart.GetStatus() is not AutoStartStatus.Disabled)
            {
                // An entry that was there before the first run was not written by it: it is
                // treated as a later run treats it, its path repaired above and a Task Manager
                // veto kept. The marker ends the first run; nothing was registered, so no
                // balloon.
                _registry.SetString(SettingsKeyPath, RegisteredMarkerValueName, "1");
                return false;
            }

            undoOnRefusal = true;
            _autoStart.Enable();
            _registry.SetString(SettingsKeyPath, RegisteredMarkerValueName, "1");
            return true;
        }
        catch (Exception ex) when (IsRefusal(ex))
        {
            // A key this account may not read or write (a policy, security software) is not a
            // reason to fail to start: no marker is written, so the next start tries again.
            // Enable may have written the Run value before the Task Manager key or the marker's
            // key refused. Left there without its marker, it would be registered again at every
            // start, undoing an untick. A click on the tray item reports a refusal of the Run or
            // Task Manager key.
            if (undoOnRefusal)
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

    /// <summary>
    /// Whether a registry access of the registration failed because this account may not make
    /// it. The registration reads and writes in one block; the exceptions of a refused read
    /// include those of a refused write, so the read's test takes both.
    /// </summary>
    /// <param name="exception">What the access threw.</param>
    /// <returns><c>true</c> for a refused read, as <see cref="RegistryRefusal"/> defines it.</returns>
    private static bool IsRefusal(Exception exception) => RegistryRefusal.IsReadRefusal(exception);
}
