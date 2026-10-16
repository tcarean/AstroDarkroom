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
            case "darks":
                CheckDarks(DarkSeriesRun.Run(folder, new DarkOptions()), e, t);
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

    /// <summary>The dark series (R-020 to R-041): the master, the hot pixels, the noise, the maps, the warming and the six verdicts.</summary>
    private static void CheckDarks(DarkSeriesResult r, JsonElement e, JsonElement t)
    {
        DarkSeriesAnalysis d = r.Darks;
        int rp = r.ReferencePlane;
        double adu = t.GetProperty("adu").GetDouble(), ratio = t.GetProperty("ratio").GetDouble(), counts = t.GetProperty("counts").GetDouble();
        Assert.False(r.HasErrors, "a frame could not be read or decoded");
        Assert.Equal(e.GetProperty("used").GetInt32(), d.Used);
        Assert.Equal(e.GetProperty("quantStep").GetInt32(), d.QuantStep);
        Assert.Equal(e.GetProperty("referencePlane").GetString(), d.PlaneNames[rp]);
        AssertWithin("norm", e.GetProperty("norm").GetDouble(), d.Norm, 0.005);

        int[] above = e.GetProperty("hotAbove").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        for (int k = 0; k < above.Length; k++)
        {
            AssertWithin($"hotAbove[{k}]", above[k], d.Master.Sum(m => m.Above[k]), counts);
        }

        AssertWithin("hotAlways", e.GetProperty("hotAlways").GetInt32(), d.Hot.Always, counts);
        AssertWithin("hotFlicker", e.GetProperty("hotFlicker").GetInt32(), d.Hot.Flicker, counts);
        AssertWithin("hotP50", e.GetProperty("hotP50").GetDouble(), d.Hot.P50 / d.Norm, 1.0);
        AssertWithin("hotTemporalStd", e.GetProperty("hotTemporalStd").GetDouble(), d.Hot.HotTemporalStd, adu);
        AssertWithin("quietTemporalStd", e.GetProperty("quietTemporalStd").GetDouble(), d.Hot.QuietTemporalStd, adu);
        AssertWithin("rts", e.GetProperty("rts").GetInt32(), d.Hot.RtsCandidates, counts);
        Assert.Equal(e.GetProperty("dead").GetInt32(), r.Verdicts.DeadSat.Dead);
        Assert.Equal(e.GetProperty("saturated").GetInt32(), r.Verdicts.DeadSat.Saturated);

        AssertWithin("sigmaFrame", e.GetProperty("sigmaFrame").GetDouble(), d.Noise.SigmaFrame, adu);
        AssertWithin("residualSigmaFirst", e.GetProperty("residualSigmaFirst").GetDouble(), d.Frames[0].Residuals[rp].Sigma, adu);
        AssertWithin("masterSigma", e.GetProperty("masterSigma").GetDouble(), d.Master[rp].Sigma, adu);
        AssertWithin("rowBandingRatio", e.GetProperty("rowBandingRatio").GetDouble(), r.Verdicts.Noise.RowBandingRatio, 0.3);
        foreach (JsonElement point in e.GetProperty("noiseCurve").EnumerateArray())
        {
            int n = point[0].GetInt32();
            NoisePoint measured = Assert.Single(d.Noise.Curve, p => p.N == n);
            AssertWithin($"noiseCurve n={n}", point[1].GetDouble(), measured.Measured, adu);
        }

        AssertWithin("masterNoise", e.GetProperty("masterNoise").GetDouble(), d.Noise.MasterNoise, adu);
        AssertWithin("medianHalfVsMeanHalf", e.GetProperty("medianHalfVsMeanHalf").GetDouble(), d.Noise.MedianHalfVsMeanHalf, adu);
        AssertWithin("darkCentre", e.GetProperty("darkCentre").GetDouble(), d.MeanMaps[rp].Regions.Centre, adu);
        AssertWithin("darkBottom", e.GetProperty("darkBottom").GetDouble(), d.MeanMaps[rp].Regions.Bottom, adu);
        AssertWithin("warmFirst", e.GetProperty("warmFirst").GetDouble(), d.Warming.FirstRatio, ratio);
        AssertWithin("warmLast", e.GetProperty("warmLast").GetDouble(), d.Warming.LastRatio, ratio);
        AssertWithin("driftCelsius", e.GetProperty("driftCelsius").GetDouble(), d.Warming.DeltaCelsius, 0.1);
        AssertWithin("bandingRowP2pMax", e.GetProperty("bandingRowP2pMax").GetDouble(), d.Banding.Max(b => b.RowP2p), adu);
        AssertWithin("bandingColP2pMax", e.GetProperty("bandingColP2pMax").GetDouble(), d.Banding.Max(b => b.ColP2p), adu);
        AssertWithin("leakOffset", e.GetProperty("leakOffset").GetDouble(), r.Verdicts.Leak.OffsetAdu, adu);
        Assert.Equal(e.GetProperty("leakWhere").GetString(), r.Verdicts.Leak.Where.ToString());

        if (e.TryGetProperty("readNoise", out JsonElement rn) && rn.ValueKind != JsonValueKind.Null)
        {
            Assert.NotNull(r.Biases);
            double[] expectedRn = rn.EnumerateArray().Select(x => x.GetDouble()).ToArray();
            for (int p = 0; p < 4; p++)
            {
                AssertWithin($"readNoise[{p}]", expectedRn[p], r.Biases!.ReadNoise[p], adu);
            }

            Assert.NotNull(r.DarkCurrent);
            AssertWithin("dcMedian", e.GetProperty("dcMedian").GetDouble(), r.DarkCurrent!.Median, adu);
            AssertWithin("dcCentre", e.GetProperty("dcCentre").GetDouble(), r.DarkCurrent.Maps[rp].Regions.Centre, adu);
            AssertWithin("dcTopLeft", e.GetProperty("dcTopLeft").GetDouble(), r.DarkCurrent.Maps[rp].Regions.TopLeft, adu);
            AssertWithin("biasFractionBelowBlack", e.GetProperty("biasFractionBelowBlack").GetDouble(), r.Biases.Clip.FractionBelowBlack, 0.01);
        }
        else
        {
            Assert.Null(r.Biases);
        }

        JsonElement v = e.GetProperty("verdicts");
        Assert.Equal(v.GetProperty("leak").GetString(), r.Verdicts.Leak.Verdict.ToString());
        Assert.Equal(v.GetProperty("drift").GetString(), r.Verdicts.Drift.Verdict.ToString());
        Assert.Equal(v.GetProperty("noise").GetString(), r.Verdicts.Noise.Verdict.ToString());
        Assert.Equal(v.GetProperty("pattern").GetString(), r.Verdicts.Pattern.Verdict.ToString());
        Assert.Equal(v.GetProperty("deadSat").GetString(), r.Verdicts.DeadSat.Verdict.ToString());
        Assert.Equal(v.GetProperty("enough").GetString(), r.Verdicts.Enough.Verdict.ToString());
    }

    private static void AssertWithin(string what, double expected, double actual, double tolerance)
        => Assert.True(Math.Abs(expected - actual) <= tolerance, $"{what}: expected {expected} +- {tolerance}, measured {actual}");

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
