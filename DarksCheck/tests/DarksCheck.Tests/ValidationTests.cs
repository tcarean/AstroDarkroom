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

using System.Globalization;
using System.Text.Json;
using DarksCheck.Core;

namespace DarksCheck.Tests;

/// <summary>
/// Reads docs/validation.json and checks every case against the current code, on the real
/// sessions the folder DARKSCHECK_TESTDATA_LARGE names (ADR 0006). Out of the gate (kit.json's
/// testFilter): the sessions are gigabytes and live outside git. A case whose session is absent
/// returns early with a message, since xUnit 2 cannot skip dynamically. When a figure changes on
/// purpose, the change is made in validation.json with a note in validation.md, never here.
/// </summary>
[Trait("Category", "Validation")]
public class ValidationTests
{
    [Theory]
    [MemberData(nameof(Cases))]
    public void ValidationCase_MatchesRecordedFigures(string id, string kind, string session, string expectedJson, string toleranceJson)
    {
        string? folder = TestData.Large(session);
        if (folder is null)
        {
            Console.WriteLine($"{id}: session {session} is absent under DARKSCHECK_TESTDATA_LARGE; nothing checked");
            return;
        }

        using JsonDocument expected = JsonDocument.Parse(expectedJson);
        using JsonDocument tolerance = JsonDocument.Parse(toleranceJson);
        JsonElement e = expected.RootElement;
        JsonElement t = tolerance.RootElement;
        switch (kind)
        {
            case "inventory":
                CheckInventory(InventoryRun.Run(folder), e, t);
                break;
            default:
                Assert.Fail($"{id}: unknown kind {kind}");
                break;
        }
    }

    private static void CheckInventory(Inventory inv, JsonElement e, JsonElement t)
    {
        Assert.Equal(e.GetProperty("darks").GetInt32(), inv.Darks.Count);
        Assert.Equal(e.GetProperty("biases").GetInt32(), inv.Biases.Count);
        Assert.Equal(e.GetProperty("lights").GetInt32(), inv.Lights.Count);
        Assert.False(inv.HasErrors, string.Join("; ", inv.Darks.Concat(inv.Biases).Concat(inv.Lights).Where(f => f.Error is not null).Select(f => f.FileName + ": " + f.Error)));
        Assert.Equal(e.GetProperty("camera").GetString(), inv.Darks[0].Camera);
        GroupSettings darks = inv.Match.Darks!;
        Assert.True(darks.Uniform, "the darks do not share one exposure and ISO");
        Assert.Equal(e.GetProperty("darksExposureSeconds").GetDouble(), darks.ExposureSeconds!.Value, t.GetProperty("exposureSeconds").GetDouble());
        Assert.Equal(e.GetProperty("darksIso").GetDouble(), darks.Iso!.Value, 0.5);
        Assert.Equal(DateTime.Parse(e.GetProperty("firstDarkStart").GetString()!, CultureInfo.InvariantCulture), inv.DarksCadence.Start!.Value);
        Assert.Equal(e.GetProperty("darksSpanSeconds").GetDouble(), inv.DarksCadence.SpanSeconds, t.GetProperty("spanSeconds").GetDouble());
        Assert.Equal(e.GetProperty("darksIntervalSeconds").GetDouble(), inv.DarksCadence.MedianIntervalSeconds!.Value, t.GetProperty("intervalSeconds").GetDouble());
        Assert.Equal(e.GetProperty("darksLongPauses").GetInt32(), inv.DarksCadence.LongPauses.Count);
        Assert.Equal(e.GetProperty("darksVsLights").GetString(), inv.Match.DarksVsLights.ToString());
        Assert.Equal(e.GetProperty("bias").GetString(), inv.Match.Bias.ToString());
    }

    public static TheoryData<string, string, string, string, string> Cases()
    {
        TheoryData<string, string, string, string, string> data = [];
        // From the source tree, not the build output's copy: `dotnet test --no-build` after an
        // edit of the figures would otherwise compare the stale copy.
        string path = Path.Combine(TestData.FindRepoRoot(), "docs", "validation.json");
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (JsonElement c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            data.Add(
                c.GetProperty("id").GetString()!,
                c.GetProperty("kind").GetString()!,
                c.GetProperty("session").GetString()!,
                c.GetProperty("expected").GetRawText(),
                c.GetProperty("tolerance").GetRawText());
        }

        return data;
    }
}
