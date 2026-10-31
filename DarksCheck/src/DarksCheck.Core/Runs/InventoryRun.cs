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

namespace DarksCheck.Core;

/// <summary>
/// The inventory of a session: its folders, the metadata of every dark, bias and light, the
/// cadence of the darks and of the lights, and the match of check 1.
/// </summary>
/// <param name="Session">The folder opened, with its frames' paths.</param>
/// <param name="Darks">The darks' metadata, in file-name order; empty for a session without
/// darks.</param>
/// <param name="Biases">The biases' metadata, in file-name order; empty when there are
/// none.</param>
/// <param name="Lights">The lights' metadata, in file-name order; empty when there are
/// none.</param>
/// <param name="DarksCadence">The darks' gaps and cadence.</param>
/// <param name="LightsCadence">The lights' gaps and cadence.</param>
/// <param name="Match">The settings of each group and check 1's verdicts on them.</param>
/// <param name="DarksFromPartner">The partner's root when its darks stand in for a session without
/// darks (R-054), else null.</param>
/// <param name="PartnerDarks">The partner's darks' metadata in that case, else null.</param>
internal sealed record Inventory(
    SessionFolder Session,
    IReadOnlyList<FrameInfo> Darks,
    IReadOnlyList<FrameInfo> Biases,
    IReadOnlyList<FrameInfo> Lights,
    Cadence DarksCadence,
    Cadence LightsCadence,
    MatchAnalysis Match,
    string? DarksFromPartner = null,
    IReadOnlyList<FrameInfo>? PartnerDarks = null)
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
    /// <summary>
    /// Opens the folder (a session without darks allowed) and reads the metadata of every frame,
    /// without decoding pixels.
    /// </summary>
    /// <param name="folder">The session folder or the library folder.</param>
    /// <returns>The inventory; a file that could not be read is listed with its error.</returns>
    internal static Inventory Run(string folder)
    {
        using RunClock clock = RunClock.Start(Log, "inventory", "folder=" + folder);
        SessionFolder session = SessionFolder.Open(folder, allowLightsOnly: true);
        clock.Lap("open");
        FrameInfo[] darks = ReadAll(session.Darks);
        clock.Lap("darks");
        FrameInfo[] biases = ReadAll(session.Biases);
        clock.Lap("biases");
        FrameInfo[] lights = ReadAll(session.Lights);
        clock.Lap("lights");
        Cadence darksCadence = CadenceAnalyzer.Analyze(darks);
        Cadence lightsCadence = CadenceAnalyzer.Analyze(lights);
        MatchAnalysis match = MatchAnalyzer.Analyze(darks, biases, lights);
        clock.Lap("cadence");
        if (Log.IsDebugEnabled)
        {
            Log.Debug("cadence of the darks: " + LogText.Figures(darksCadence));
            Log.Debug("cadence of the lights: " + LogText.Figures(lightsCadence));
            Log.Debug("match: " + LogText.Figures(match));
        }

        Inventory inventory = new(session, darks, biases, lights, darksCadence, lightsCadence, match);
        clock.Done("darks=" + LogText.Int(darks.Length) + " biases=" + LogText.Int(biases.Length) + " lights=" + LogText.Int(lights.Length)
            + " errors=" + LogText.Int(inventory.ErrorCount));
        return inventory;
    }

    /// <summary>The diagnostic log: the run's lines, the cadences and the match of check 1 (R-101).</summary>
    private static readonly Logger Log = LogManager.GetLogger(nameof(InventoryRun));

    /// <summary>
    /// Reads the metadata of each file, numbering them from 1 in the order given.
    /// </summary>
    /// <param name="paths">The files' paths.</param>
    /// <returns>One record per path, in the same order; never throws for a bad file.</returns>
    internal static FrameInfo[] ReadAll(IReadOnlyList<string> paths)
    {
        FrameInfo[] frames = new FrameInfo[paths.Count];
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = FrameInfo.Read(i + 1, paths[i]);
        }

        return frames;
    }
}
