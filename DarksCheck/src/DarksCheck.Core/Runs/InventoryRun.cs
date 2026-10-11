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

namespace DarksCheck.Core;

/// <summary>
/// The inventory of a session: its folders, the metadata of every dark, bias and light, the
/// cadence of the darks and of the lights, and the match of check 1.
/// </summary>
internal sealed record Inventory(
    SessionFolder Session,
    IReadOnlyList<FrameInfo> Darks,
    IReadOnlyList<FrameInfo> Biases,
    IReadOnlyList<FrameInfo> Lights,
    Cadence DarksCadence,
    Cadence LightsCadence,
    MatchAnalysis Match)
{
    internal int ErrorCount => Darks.Count(f => f.Error is not null) + Biases.Count(f => f.Error is not null) + Lights.Count(f => f.Error is not null);

    internal bool HasErrors => ErrorCount > 0;
}

/// <summary>
/// Chains open, read and measure for the inventory (R-002, R-003, R-006, R-007). The console
/// and the window call this and the report; neither computes a number.
/// </summary>
internal static class InventoryRun
{
    internal static Inventory Run(string folder)
    {
        SessionFolder session = SessionFolder.Open(folder);
        FrameInfo[] darks = ReadAll(session.Darks);
        FrameInfo[] biases = ReadAll(session.Biases);
        FrameInfo[] lights = ReadAll(session.Lights);
        return new Inventory(session, darks, biases, lights,
            CadenceAnalyzer.Analyze(darks), CadenceAnalyzer.Analyze(lights), MatchAnalyzer.Analyze(darks, biases, lights));
    }

    private static FrameInfo[] ReadAll(IReadOnlyList<string> paths)
    {
        FrameInfo[] frames = new FrameInfo[paths.Count];
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = FrameInfo.Read(i + 1, paths[i]);
        }

        return frames;
    }
}
