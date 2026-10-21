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
    /// repairs a stale path. A key this account may not write is not an error: nothing is
    /// registered, and the next start tries again.
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

            _autoStart.Enable();
            _registry.SetString(SettingsKeyPath, RegisteredMarkerValueName, "1");
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            // A key this account may not write (a policy, security software) is not a reason
            // to fail to start: nothing is registered and no marker is written, so the next
            // start tries again. The tray checkbox reports the same refusal when clicked.
            return false;
        }
    }
}
