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
using LightsCheck.Core;

namespace LightsCheck.Tests;

/// <summary>
/// Reads docs/validation.json and checks every case against the current code, on the real
/// sessions the folder LIGHTSCHECK_TESTDATA_LARGE names (ADR 0004). Out of the gate (kit.json's
/// testFilter): the sessions are gigabytes and live outside git. A case whose session is absent
/// returns early with a message, since xUnit 2 cannot skip dynamically. When a figure changes on
/// purpose, the change is made in validation.json with a note in validation.md, never here.
/// </summary>
[Trait("Category", "Validation")]
public class ValidationTests
{
    [Theory]
    [MemberData(nameof(Cases))]
    public void ValidationCase_MatchesRecordedFigures(string id, string kind, string session, int frame, string expectedJson, string toleranceJson)
    {
        string? folder = TestData.Large(session);
        if (folder is null)
        {
            Console.WriteLine($"{id}: session {session} is absent under LIGHTSCHECK_TESTDATA_LARGE; nothing checked");
            return;
        }

        using JsonDocument expected = JsonDocument.Parse(expectedJson);
        using JsonDocument tolerance = JsonDocument.Parse(toleranceJson);
        JsonElement e = expected.RootElement;
        JsonElement t = tolerance.RootElement;
        if (kind == "inventory")
        {
            CheckInventory(InventoryRun.Run(folder), e, t);
        }
        else
        {
            CheckFrame(FrameRun.Run(folder, frame).Measurement, e, t);
        }
    }

    private static void CheckInventory(Inventory inv, JsonElement e, JsonElement t)
    {
        Assert.Equal(e.GetProperty("lights").GetInt32(), inv.Lights.Count);
        Assert.Equal(e.GetProperty("biases").GetInt32(), inv.Session.Biases.Count);
        Assert.False(inv.HasErrors, string.Join("; ", inv.Lights.Where(f => f.Error is not null).Select(f => f.FileName + ": " + f.Error)));
        Assert.Equal(e.GetProperty("camera").GetString(), inv.Lights[0].Camera);
        Assert.Equal(e.GetProperty("exposureSeconds").GetDouble(), inv.Lights[0].ExposureSeconds!.Value, t.GetProperty("exposureSeconds").GetDouble());
        Assert.Equal(e.GetProperty("iso").GetDouble(), inv.Lights[0].Iso!.Value, 0.5);
        Assert.Equal(DateTime.Parse(e.GetProperty("firstStart").GetString()!, CultureInfo.InvariantCulture), inv.Cadence.Start!.Value);
        Assert.Equal(e.GetProperty("spanSeconds").GetDouble(), inv.Cadence.SpanSeconds, t.GetProperty("spanSeconds").GetDouble());
        Assert.Equal(e.GetProperty("dutyPct").GetDouble(), inv.Cadence.DutyPct, t.GetProperty("dutyPct").GetDouble());
    }

    private static void CheckFrame(FrameMeasurement m, JsonElement e, JsonElement t)
    {
        double counts = t.GetProperty("counts").GetDouble();
        AssertWithin("detected", e.GetProperty("detected").GetInt32(), m.Detected, counts * e.GetProperty("detected").GetInt32());
        AssertWithin("bright", e.GetProperty("bright").GetInt32(), m.DetectedBright, counts * e.GetProperty("bright").GetInt32());
        AssertWithin("fwhm", e.GetProperty("fwhm").GetDouble(), Figure(m.FwhmMedian, "fwhm"), t.GetProperty("fwhm").GetDouble());
        AssertWithin("fwhmP90", e.GetProperty("fwhmP90").GetDouble(), Figure(m.FwhmP90, "fwhmP90"), t.GetProperty("fwhm").GetDouble());
        AssertWithin("elongation", e.GetProperty("elongation").GetDouble(), Figure(m.ElongationMedian, "elongation"), t.GetProperty("elongation").GetDouble());
        AssertWithin("elongatedFraction", e.GetProperty("elongatedFraction").GetDouble(), Figure(m.ElongatedFraction, "elongatedFraction"), t.GetProperty("fraction").GetDouble());
        AssertWithin("angle", e.GetProperty("angle").GetDouble(), Figure(m.AngleDeg, "angle"), t.GetProperty("angle").GetDouble());
        AssertWithin("coherence", e.GetProperty("coherence").GetDouble(), Figure(m.AngleCoherence, "coherence"), t.GetProperty("coherence").GetDouble());
        AssertWithin("halo", e.GetProperty("halo").GetDouble(), Figure(m.Halo, "halo"), t.GetProperty("halo").GetDouble());
        AssertWithin("background", e.GetProperty("background").GetDouble(), m.BackgroundMedian, t.GetProperty("background").GetDouble());
        AssertWithin("sigma", e.GetProperty("sigma").GetDouble(), m.NoiseSigma, t.GetProperty("sigma").GetDouble());
        double[] channels = e.GetProperty("channels").EnumerateArray().Select(c => c.GetDouble()).ToArray();
        for (int i = 0; i < 4; i++)
        {
            AssertWithin("channel " + m.Channels[i].Name, channels[i], m.Channels[i].Median, t.GetProperty("background").GetDouble());
        }

        AssertWithin("fwhmCentre", e.GetProperty("fwhmCentre").GetDouble(), Figure(m.Centre.Fwhm, "fwhmCentre"), t.GetProperty("fwhm").GetDouble());
        AssertWithin("fwhmEdge", e.GetProperty("fwhmEdge").GetDouble(), Figure(m.Edge.Fwhm, "fwhmEdge"), t.GetProperty("fwhm").GetDouble());
        Assert.Equal(e.GetProperty("trails").GetInt32(), m.Trails.Count);
    }

    /// <summary>A figure the frame did not yield (too few stars) fails by name, not with a null dereference.</summary>
    private static double Figure(double? v, string what)
    {
        Assert.True(v.HasValue, $"{what}: the frame yielded no figure (null)");
        return v!.Value;
    }

    private static void AssertWithin(string what, double expected, double actual, double tolerance)
        => Assert.True(Math.Abs(expected - actual) <= tolerance, $"{what}: expected {expected} +- {tolerance}, measured {actual}");

    public static TheoryData<string, string, string, int, string, string> Cases()
    {
        TheoryData<string, string, string, int, string, string> data = [];
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
                c.TryGetProperty("frame", out JsonElement f) ? f.GetInt32() : 0,
                c.GetProperty("expected").GetRawText(),
                c.GetProperty("tolerance").GetRawText());
        }

        return data;
    }
}
