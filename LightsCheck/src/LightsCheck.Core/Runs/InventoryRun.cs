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

using NLog;

namespace LightsCheck.Core;

/// <summary>
/// The inventory of a session: its folders, every light's metadata, and the cadence.
/// </summary>
/// <param name="Session">The session's folders and frame lists.</param>
/// <param name="Lights">
/// Every light's metadata, in the session's order; an unreadable one carries its error.
/// </param>
/// <param name="Cadence">The timing of the series, from the lights' times and exposures.</param>
internal sealed record Inventory(SessionFolder Session, IReadOnlyList<FrameInfo> Lights, Cadence Cadence)
{
    internal bool HasErrors => Lights.Any(f => f.Error is not null);
}

/// <summary>
/// Chains open, read and measure for the inventory (R-002, R-003, R-006). The console and the
/// window call this and the report; neither computes a number.
/// </summary>
internal static class InventoryRun
{
    /// <summary>
    /// Opens the session and reads every light's metadata, without decoding a pixel. The folder
    /// errors of <see cref="SessionFolder.Open"/> propagate; a light that cannot be read only
    /// carries its error.
    /// </summary>
    /// <param name="folder">The session folder.</param>
    /// <returns>The session's inventory.</returns>
    internal static Inventory Run(string folder)
    {
        using RunClock clock = RunClock.Start(Log, "inventory", folder);
        SessionFolder session = SessionFolder.Open(folder);
        clock.Lap("open");
        FrameInfo[] lights = new FrameInfo[session.Lights.Count];
        for (int i = 0; i < lights.Length; i++)
        {
            lights[i] = FrameInfo.Read(i + 1, session.Lights[i]);
        }

        clock.Lap("read");
        Cadence cadence = CadenceAnalyzer.Analyze(lights);
        clock.Lap("cadence");
        if (Log.IsDebugEnabled)
        {
            Log.Debug("cadence: " + LogText.Timing(cadence));
        }

        Inventory inventory = new(session, lights, cadence);
        clock.Done("lights=" + LogText.Int(lights.Length) + " errors=" + LogText.Int(lights.Count(f => f.Error is not null)));
        return inventory;
    }

    /// <summary>The diagnostic log: the run's lines (R-071).</summary>
    private static readonly Logger Log = LogManager.GetLogger(nameof(InventoryRun));
}
