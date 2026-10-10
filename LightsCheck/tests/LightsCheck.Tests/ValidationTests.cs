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
        switch (kind)
        {
            case "inventory":
                CheckInventory(InventoryRun.Run(folder), e, t);
                break;
            case "frame":
                CheckFrame(FrameRun.Run(folder, frame).Measurement, e, t);
                break;
            case "words":
                if (e.ValueKind == JsonValueKind.Null)
                {
                    Console.WriteLine($"{id}: unmeasured (expected is null); nothing checked");
                    return;
                }

                CheckWords(SeriesRun.Run(folder, new SeriesOptions()), e);
                break;
            case "tables":
                IReadOnlyList<ReportTable> tables = SeriesReport.DitherTables(SeriesRun.Run(folder, new SeriesOptions()));
                if (e.ValueKind == JsonValueKind.Null)
                {
                    Console.WriteLine($"{id}: unmeasured (expected is null); the tables measured, to record:");
                    foreach (ReportTable table in tables)
                    {
                        Console.WriteLine($"  {table.Title}: {string.Join(" || ", table.Rows.Select(row => string.Join(" | ", row)))}");
                    }

                    return;
                }

                CheckTables(tables, e);
                break;
            case "moves":
                IReadOnlyList<FrameTableRow> moveRows = SeriesReport.FrameRows(SeriesRun.Run(folder, new SeriesOptions()));
                if (e.ValueKind == JsonValueKind.Null)
                {
                    Console.WriteLine($"{id}: unmeasured (expected is null); the moves measured, to record:");
                    foreach (FrameTableRow row in moveRows.Where(x => x.Move != "-"))
                    {
                        Console.WriteLine($"  {row.Index}: {row.Move}");
                    }

                    Console.WriteLine($"  south {MovesWith(moveRows, " S")}, north {MovesWith(moveRows, " N")}, east {MovesWith(moveRows, " E")}, west {MovesWith(moveRows, " V")}");
                    return;
                }

                CheckMoves(moveRows, e);
                break;
            default:
                CheckSeries(SeriesRun.Run(folder, new SeriesOptions()), e, t);
                break;
        }
    }

    private static void CheckSeries(SeriesResult r, JsonElement e, JsonElement t)
    {
        SeriesAnalysis s = r.Series;
        DitherAnalysis d = r.Dither;
        Assert.Equal(e.GetProperty("good").GetInt32(), s.GoodCount);
        Assert.Equal(e.GetProperty("marginal").GetInt32(), s.MarginalCount);
        Assert.Equal(e.GetProperty("bad").GetInt32(), s.BadCount);
        int[] bad = Enumerable.Range(0, r.Frames.Count).Where(i => s.Classes[i] == FrameClass.Bad).Select(i => r.Frames[i].Info.Index).ToArray();
        Assert.Equal(e.GetProperty("badFrames").EnumerateArray().Select(x => x.GetInt32()).ToArray(), bad);
        Assert.Equal(e.GetProperty("registered").GetInt32(), d.Registered);
        Assert.Equal(e.GetProperty("moves").GetInt32(), d.Moves);
        Assert.Equal(e.GetProperty("pauses").GetInt32(), d.Pauses);
        Assert.Equal(e.GetProperty("pausesWithoutMove").GetInt32(), d.PausesWithoutMove);
        Assert.Equal(e.GetProperty("distinctMid").GetInt32(), d.DistinctMid);
        Assert.Equal(e.GetProperty("distinctLoose").GetInt32(), d.DistinctLoose);
        Assert.Equal(e.GetProperty("isolated").GetInt32(), d.IsolatedLoose);
        Assert.Equal(e.GetProperty("longestRunLoose").GetInt32(), d.RunsLoose.Max(x => x.Frames));
        AssertWithin("medianMove", e.GetProperty("medianMove").GetDouble(), Figure(d.MedianMove, "medianMove"), t.GetProperty("px").GetDouble());
        AssertWithin("rotationArcmin", e.GetProperty("rotationArcmin").GetDouble(), Figure(s.RotationTotalDeg, "rotation") * 60, t.GetProperty("arcmin").GetDouble());
        AssertWithin("driftYPerMin", e.GetProperty("driftYPerMin").GetDouble(), Figure(s.DriftYPerMin, "driftY"), t.GetProperty("px").GetDouble());
        AssertWithin("sessionFwhm", e.GetProperty("sessionFwhm").GetDouble(), Figure(s.SessionFwhm, "sessionFwhm"), t.GetProperty("fwhm").GetDouble());
        AssertWithin("readNoiseG1", e.GetProperty("readNoiseG1").GetDouble(), s.ReadNoise![1].PairSigma, t.GetProperty("adu").GetDouble());
        AssertWithin("swampingRFrame1", e.GetProperty("swampingRFrame1").GetDouble(), s.Swamping![0][0].SwampingRatio, t.GetProperty("ratio").GetDouble());
        Assert.Equal(e.GetProperty("raAxis").GetString(), s.RaAxis.ToString());
        Assert.Equal(e.GetProperty("focusHeld").GetBoolean(), s.FocusHeld);
        Assert.Equal(e.GetProperty("dew").GetBoolean(), s.DewSuspected);
        Assert.Equal(e.GetProperty("hotPixelsOk").GetBoolean(), d.HotPixelsOk);
        Assert.Equal(e.GetProperty("walkingNoiseOk").GetBoolean(), d.WalkingNoiseOk);
    }

    /// <summary>The words per frame on the real session: the trail axis of every frame with a trailing flag, by class, and the run cell of the frames listed (R-033, R-034).</summary>
    private static void CheckWords(SeriesResult r, JsonElement e)
    {
        SeriesAnalysis s = r.Series;
        IReadOnlyList<FrameTableRow> rows = SeriesReport.FrameRows(r);
        for (int i = 0; i < r.Frames.Count; i++)
        {
            FrameFlag? trail = s.Flags[i].FirstOrDefault(f => f.Kind is FlagKind.Trailed or FlagKind.MarginalTrail);
            if (trail is null)
            {
                continue;
            }

            string expectedWord = e.GetProperty(s.Classes[i] == FrameClass.Bad ? "badTrailOn" : "marginalTrailOn").GetString()!;
            string word = trail.On.ToString().ToUpperInvariant();
            Assert.True(expectedWord == word, $"frame {r.Frames[i].Info.Index}: trail on {word}, expected {expectedWord}");
        }

        foreach (JsonProperty p in e.GetProperty("spotCells").EnumerateObject())
        {
            int index = int.Parse(p.Name, CultureInfo.InvariantCulture);
            Assert.True(p.Value.GetString() == rows[index - 1].Spot, $"frame {index}: cell '{rows[index - 1].Spot}', expected '{p.Value.GetString()}'");
        }
    }

    /// <summary>The dithering tables on the real session: every table named in `expected` has exactly the rows recorded, cell by cell (R-036).</summary>
    private static void CheckTables(IReadOnlyList<ReportTable> tables, JsonElement e)
    {
        foreach (JsonProperty p in e.EnumerateObject())
        {
            ReportTable table = Assert.Single(tables, t => t.Title == p.Name);
            string[] expected = p.Value.EnumerateArray().Select(row => string.Join(" | ", row.EnumerateArray().Select(c => c.GetString()))).ToArray();
            string[] actual = table.Rows.Select(row => string.Join(" | ", row)).ToArray();
            Assert.True(expected.SequenceEqual(actual), $"{p.Name}: measured [{string.Join(" || ", actual)}], expected [{string.Join(" || ", expected)}]");
        }
    }

    private static int MovesWith(IReadOnlyList<FrameTableRow> rows, string sense) => rows.Count(r => r.Move.Contains(sense, StringComparison.Ordinal));

    /// <summary>The dithering cells on the real session: the frames listed under `cells`, and how many moves went each way (R-038).</summary>
    private static void CheckMoves(IReadOnlyList<FrameTableRow> rows, JsonElement e)
    {
        foreach (JsonProperty p in e.GetProperty("cells").EnumerateObject())
        {
            int index = int.Parse(p.Name, CultureInfo.InvariantCulture);
            Assert.True(p.Value.GetString() == rows[index - 1].Move, $"frame {index}: cell '{rows[index - 1].Move}', expected '{p.Value.GetString()}'");
        }

        Assert.Equal(e.GetProperty("south").GetInt32(), MovesWith(rows, " S"));
        Assert.Equal(e.GetProperty("north").GetInt32(), MovesWith(rows, " N"));
        Assert.Equal(e.GetProperty("east").GetInt32(), MovesWith(rows, " E"));
        Assert.Equal(e.GetProperty("west").GetInt32(), MovesWith(rows, " V"));
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
