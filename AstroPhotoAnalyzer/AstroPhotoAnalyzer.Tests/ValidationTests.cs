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
using System.Text.RegularExpressions;
using Xunit.Abstractions;

namespace AstroPhotoAnalyzer.Tests;

/// <summary>
/// The acceptance figures measured on the reference image, read from docs/validation.json so the
/// document and the test cannot drift apart (ADR 0004). One kind of case, <c>analysis</c>:
/// <see cref="AnalysisRun.Execute(string, Parameters, IProgress{string})"/> on the image with the
/// case's parameters, exactly as the console runs it; <see cref="Figures"/> reads a fixed set of
/// figures from the result, and the case's <c>expected</c> lists the ones it guards.
///
/// Not in the gate: trait Category=Validation, about 11 s per case on a 23 MP image. A case
/// whose image is absent on this machine returns early with a message (xUnit 2 has no dynamic
/// skip). A case with <c>expected</c> null is unmeasured and only prints what it found. Every
/// case prints its figures as one JSON line (test output, and the file named by
/// APA_VALIDATION_DUMP when set), which is how validation.json gets filled: never by editing the
/// expected values to match (the kit's rule "No test edited to pass").
/// </summary>
[Trait("Category", "Validation")]
public class ValidationTests
{
    readonly ITestOutputHelper _out;

    public ValidationTests(ITestOutputHelper output) => _out = output;

    static string ValidationJson => Path.Combine(TestData.RepoDir(), "docs", "validation.json");

    public static IEnumerable<object[]> Cases()
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(ValidationJson));
        return doc.RootElement.GetProperty("cases").EnumerateArray()
            .Select(c => new object[] { c.GetProperty("id").GetString()! })
            .ToList();
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ValidationCase_MatchesRecordedFigures(string id)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(ValidationJson));
        JsonElement c = doc.RootElement.GetProperty("cases").EnumerateArray()
            .Single(e => e.GetProperty("id").GetString() == id);
        Assert.Equal("analysis", c.GetProperty("kind").GetString());

        string name = c.GetProperty("image").GetString()!;
        string? image = TestData.Large(name);
        if (image == null)
        {
            _out.WriteLine($"NOT MEASURED {id}: {name} is absent under {TestData.LargeDir()}");
            return;
        }

        AnalysisResult result = AnalysisRun.Execute(image, ParametersOf(c.GetProperty("parameters")));
        Dictionary<string, object> measured = Figures(result);

        string line = id + " " + JsonSerializer.Serialize(measured);
        _out.WriteLine("MEASURED " + line);
        string? dump = Environment.GetEnvironmentVariable("APA_VALIDATION_DUMP");
        if (!string.IsNullOrEmpty(dump))
            File.AppendAllText(dump, line + "\n");

        if (!c.TryGetProperty("expected", out JsonElement expected) || expected.ValueKind == JsonValueKind.Null)
        {
            _out.WriteLine($"UNMEASURED {id}: no expected figures recorded yet");
            return;
        }

        double tol = c.GetProperty("tolerance").GetDouble();
        var failures = new List<string>();
        foreach (JsonProperty p in expected.EnumerateObject())
        {
            if (!measured.TryGetValue(p.Name, out object? got))
            {
                failures.Add($"{p.Name}: not measured");
                continue;
            }

            switch (p.Value.ValueKind)
            {
                case JsonValueKind.Number when got is int or long:
                    if (p.Value.GetInt64() != Convert.ToInt64(got, CultureInfo.InvariantCulture))
                        failures.Add($"{p.Name}: expected {p.Value.GetInt64()} got {got}");
                    break;
                case JsonValueKind.Number when got is double have:
                    double want = p.Value.GetDouble();
                    if (Math.Abs(have - want) > tol)
                        failures.Add($"{p.Name}: expected {want} got {have} (tolerance {tol})");
                    break;
                case JsonValueKind.True or JsonValueKind.False when got is bool b:
                    if (b != p.Value.GetBoolean())
                        failures.Add($"{p.Name}: expected {p.Value.GetBoolean()} got {b}");
                    break;
                case JsonValueKind.String:
                    if (!string.Equals(p.Value.GetString(), Convert.ToString(got, CultureInfo.InvariantCulture), StringComparison.Ordinal))
                        failures.Add($"{p.Name}: expected '{p.Value.GetString()}' got '{got}'");
                    break;
                default:
                    failures.Add($"{p.Name}: expected {p.Value} got {got} (a different kind)");
                    break;
            }
        }

        Assert.True(failures.Count == 0, $"{id} drifted from docs/validation.json:\n  " + string.Join("\n  ", failures));
    }

    /// <summary>The case's parameters on top of the core's defaults, as the console maps its flags.</summary>
    static Parameters ParametersOf(JsonElement p)
    {
        double? aspect = null;
        if (p.TryGetProperty("aspect", out JsonElement a))
        {
            string[] wh = a.GetString()!.Split(':');
            aspect = double.Parse(wh[0], CultureInfo.InvariantCulture) / double.Parse(wh[1], CultureInfo.InvariantCulture);
        }

        var d = new Parameters();
        return new Parameters
        {
            Medium = p.TryGetProperty("medium", out JsonElement m) ? Parameters.MediumFromName(m.GetString()) : d.Medium,
            DmaxOverride = p.TryGetProperty("dmax", out JsonElement x) ? x.GetDouble() : null,
            ViewingDistanceM = p.TryGetProperty("distanceM", out JsonElement v) ? v.GetDouble() : null,
            TargetAspect = aspect,
            TargetBackground = p.TryGetProperty("targetBg", out JsonElement t) ? t.GetDouble() : d.TargetBackground,
        };
    }

    /// <summary>
    /// Every figure a case may guard, named by the record and the field it comes from. Doubles are
    /// rounded to four decimals, NaN is "n/a", an absent value "none"; integers, booleans and
    /// strings are compared exactly.
    /// </summary>
    static Dictionary<string, object> Figures(AnalysisResult a)
    {
        var f = new Dictionary<string, object>(StringComparer.Ordinal);

        f["file.width"] = a.File.W;
        f["file.height"] = a.File.H;
        f["file.bitDepth"] = a.File.BitDepth;
        f["file.fileBytes"] = a.File.FileBytes;

        f["bitDepth.applicable"] = a.BitDepth.Applicable;
        if (a.BitDepth.Applicable)
        {
            f["bitDepth.upscaledFraction"] = R(a.BitDepth.UpscaledFraction);
            Planes(f, "bitDepth.distinctLevels", a.BitDepth.DistinctLevels.Select(n => (object)n).ToList());
        }

        TonalResult t = a.Tonal;
        Planes(f, "tonal.meanLevels", t.MeanLevels.Select(v => R(v)).ToList());
        f["tonal.pctBelowDarkA"] = R(t.PctBelowDarkA);
        f["tonal.pctBelowDarkB"] = R(t.PctBelowDarkB);
        string[] ranks = { "p0_1", "p1", "p50", "p90", "p99", "p99_9", "p100" };
        for (int i = 0; i < t.ReferencePercentiles.Count && i < ranks.Length; i++)
            f["tonal.percentile." + ranks[i]] = R(t.ReferencePercentiles[i]);
        f["tonal.backgroundMedian"] = R(t.BackgroundMedian);
        f["tonal.dmax"] = R(t.Dmax);
        f["tonal.paperBlackLstar"] = R(t.PaperBlackLstar);
        f["tonal.lstarMedian"] = R(t.LstarMedian);
        f["tonal.pctBelowPaperBlack"] = R(t.PctBelowPaperBlack);
        f["tonal.pctBelowLowSeparation"] = R(t.PctBelowLowSeparation);
        f["tonal.pctAboveHighlight"] = R(t.PctAboveHighlight);

        SimulationResult s = a.Simulation;
        f["simulation.method"] = s.Method.ToString();
        f["simulation.patchCount"] = s.PatchCount;
        f["simulation.cloudCount"] = s.CloudCount;
        f["simulation.dustPatchCount"] = s.DustPatchCount;
        f["simulation.asIs.lstarDust"] = R(s.AsIs.LstarDust);
        f["simulation.asIs.lstarSky"] = R(s.AsIs.LstarSky);
        f["simulation.asIs.pctPatchesBelowPaperBlack"] = R(s.AsIs.PctPatchesBelowPaperBlack);
        f["simulation.alreadyPrintSafe"] = s.AlreadyPrintSafe;
        f["simulation.skyBelowPaperBlack"] = s.SkyBelowPaperBlack;
        f["simulation.recommendedTarget"] = s.RecommendedTarget is double rt ? R(rt) : "none";
        f["simulation.recommendedIsAsIs"] = s.RecommendedIsAsIs;
        f["simulation.exactTarget"] = s.ExactTarget is double et ? R(et) : "none";
        f["simulation.exactTargetDmaxLower"] = s.ExactTargetDmaxLower is double el ? R(el) : "none";
        f["simulation.exactTargetDmaxHigher"] = s.ExactTargetDmaxHigher is double eh ? R(eh) : "none";
        f["simulation.dmaxToPassAsIs"] = R(s.DmaxToPassAsIs);
        if (s.Primary != null)
        {
            f["simulation.primary.target"] = R(s.Primary.Target);
            f["simulation.primary.gamma"] = R(s.Primary.Gamma);
            f["simulation.primary.lstarDust"] = R(s.Primary.LstarDust);
            f["simulation.primary.lstarSky"] = R(s.Primary.LstarSky);
            f["simulation.primary.pctPatchesBelowPaperBlack"] = R(s.Primary.PctPatchesBelowPaperBlack);
            f["simulation.primary.pctBelowLowSeparation"] = R(s.Primary.PctBelowLowSeparation);
            f["simulation.primary.printSafe"] = s.Primary.PrintSafe;
        }
        f["simulation.clouds.count"] = s.Clouds.Count;
        for (int i = 0; i < Math.Min(5, s.Clouds.Count); i++)
        {
            string k = "simulation.cloud" + (i + 1) + ".";
            f[k + "name"] = s.Clouds[i].Name;
            f[k + "patches"] = s.Clouds[i].Patches;
            f[k + "lstar"] = R(s.Clouds[i].Lstar);
            f[k + "exactTarget"] = s.Clouds[i].ExactTarget is double ce ? R(ce) : "none";
        }

        NoiseResult n = a.Noise;
        f["noise.smoothTiles"] = n.SmoothTiles;
        f["noise.sigmaMedianLsb"] = R(n.SigmaMedianLsb);
        f["noise.sigma10thLsb"] = R(n.Sigma10thLsb);
        f["noise.sigmaMinLsb"] = R(n.SigmaMinLsb);
        f["noise.pctTilesBelowBanding"] = R(n.PctTilesBelowBanding);
        f["noise.bandingRisk"] = n.BandingRisk;

        BackgroundResult b = a.Background;
        Planes(f, "background.levels", b.Levels.Select(v => R(v)).ToList());
        if (b.ColorApplicable)
        {
            f["background.greenExcess"] = R(b.GreenExcess);
            f["background.redMinusBlue"] = R(b.RedMinusBlue);
        }
        f["background.hasCast"] = b.HasCast;
        foreach (EdgeRegion e in b.Regions)
        {
            string key = "background.region." + Slug(e.Name);
            Planes(f, key + ".levels", e.Levels.Select(v => R(v)).ToList());
            f[key + ".flagged"] = e.Flagged;
            f[key + ".coloured"] = e.Coloured;
        }

        StarResult st = a.Stars;
        f["stars.count"] = st.Count;
        f["stars.fwhmMedian"] = R(st.FwhmMedian);
        f["stars.fwhmQ1"] = R(st.FwhmQ1);
        f["stars.fwhmQ3"] = R(st.FwhmQ3);
        foreach (StarRegion r in st.Regions)
            f["stars.region." + Slug(r.Name) + ".fwhmMedian"] = r.Known ? R(r.FwhmMedian) : "n/a";
        f["stars.cornerToCenterRatio"] = R(st.CornerToCenterRatio);
        f["stars.softCorners"] = st.SoftCorners;

        CropResult cr = a.Crop;
        f["crop.needed"] = cr.Needed;
        f["crop.width"] = cr.W;
        f["crop.height"] = cr.H;
        f["crop.axis"] = cr.Axis ?? "none";
        f["crop.cutPerSidePx"] = cr.CutPerSidePx;
        f["crop.cutPct"] = R(cr.CutPct);

        PrintSizeResult z = a.Size;
        f["size.canRecommend"] = z.CanRecommend;
        f["size.naturalRatio"] = R(z.NaturalRatio);
        f["size.referenceWidthCm"] = z.ReferenceRow != null ? R(z.ReferenceRow.WidthCm) : "none";
        f["size.recommendedWidthCm"] = z.RecommendedRow != null ? R(z.RecommendedRow.WidthCm) : "none";
        foreach (SizeRow row in z.Rows)
        {
            string key = "size.row" + row.WidthCm.ToString("0.##", CultureInfo.InvariantCulture);
            f[key + ".dpi"] = R(row.Dpi);
            f[key + ".starMm"] = R(row.StarMm);
            f[key + ".minDistanceM"] = R(row.MinDistanceM);
            f[key + ".pointAtViewingDistance"] = row.PointAtViewingDistance;
        }

        QuantizationResult q = a.Quantization;
        f["quantization.plateauTiles"] = q.PlateauTiles;
        f["quantization.plateauPx"] = R(q.PlateauPx);
        if (q.Applicable)
        {
            f["quantization.roundingMeanAbs"] = R(q.RoundingMeanAbs);
            f["quantization.roundingMax"] = R(q.RoundingMax);
            f["quantization.roundingP999"] = R(q.RoundingP999);
        }
        f["quantization.levelsUsedFrom16"] = q.LevelsUsedFrom16;
        f["quantization.missingFrom16"] = q.MissingFrom16;
        f["quantization.levelsUsedFrom8"] = q.LevelsUsedFrom8;
        f["quantization.missingFrom8"] = q.MissingFrom8;
        f["quantization.keep16BitMaster"] = q.Keep16BitMaster;

        foreach (JpegRow j in a.Jpeg.Rows)
        {
            string key = "jpeg.q" + j.Quality.ToString(CultureInfo.InvariantCulture);
            f[key + ".megabytes"] = R(j.Megabytes);
            f[key + ".meanAbsError"] = R(j.MeanAbsError);
            f[key + ".maxError"] = R(j.MaxError);
            f[key + ".p999"] = R(j.P999);
            f[key + ".pctOverThreshold"] = R(j.PctOverThreshold);
            f[key + ".meanLevelOverThreshold"] = R(j.MeanLevelOverThreshold);
            f[key + ".acceptable"] = j.Acceptable;
        }
        f["jpeg.tiff8LzwMegabytes"] = R(a.Jpeg.Tiff8LzwMegabytes);

        f["overall"] = VerdictText.Label(a.Overall);
        return f;
    }

    /// <summary>One figure per plane: .r/.g/.b on colour, .l on mono.</summary>
    static void Planes(Dictionary<string, object> f, string key, IReadOnlyList<object> values)
    {
        string[] names = values.Count == 3 ? new[] { "r", "g", "b" } : new[] { "l" };
        for (int i = 0; i < values.Count && i < names.Length; i++)
            f[key + "." + names[i]] = values[i];
    }

    static object R(double v) => double.IsNaN(v) ? "n/a" : Math.Round(v, 4);

    static string Slug(string name) => Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
}
