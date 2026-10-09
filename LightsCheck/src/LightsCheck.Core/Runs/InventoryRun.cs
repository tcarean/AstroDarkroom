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

namespace LightsCheck.Core;

/// <summary>The inventory of a session: its folders, every light's metadata, and the cadence.</summary>
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
    internal static Inventory Run(string folder)
    {
        SessionFolder session = SessionFolder.Open(folder);
        FrameInfo[] lights = new FrameInfo[session.Lights.Count];
        for (int i = 0; i < lights.Length; i++)
        {
            lights[i] = FrameInfo.Read(i + 1, session.Lights[i]);
        }

        return new Inventory(session, lights, CadenceAnalyzer.Analyze(lights));
    }
}
