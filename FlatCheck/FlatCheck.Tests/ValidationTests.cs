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
using Xunit.Abstractions;

namespace FlatCheck.Tests;

/// <summary>
/// The acceptance figures measured on the real masters, read from docs/validation.json so the
/// document and the test cannot drift apart (ADR 0004). Four kinds of case, one per run the
/// figures were measured with: <c>structure</c> (the five tests, the vignetting depth and the
/// local spot on a master), <c>profile</c> (the master's depth against the nearest lens
/// profile, identified from a raw beside it), <c>smooth</c> (what --smooth removes) and
/// <c>against</c> (the master against the synthetic flat written from its lens's Adobe profile).
///
/// Not in the gate: trait Category=Validation, minutes on 96 MB files. A case whose file is
/// absent on this machine returns early with a message (xUnit 2 has no dynamic skip). A case
/// with <c>expected</c> null is unmeasured and only prints what it found. Every case prints
/// its measured figures as one JSON line (test output, and the file named by
/// FLATCHECK_VALIDATION_DUMP when set), which is how validation.json gets filled: never by
/// editing the expected values to match (the kit's rule "No test edited to pass").
/// </summary>
[Trait("Category", "Validation")]
public class ValidationTests
{
    readonly ITestOutputHelper _out;

    public ValidationTests(ITestOutputHelper output) => _out = output;

    static readonly Lazy<ProfileSources> Sources = new(() =>
        ProfileLocator.Locate(ProfileLocator.DefaultAdobeDirectories(),
                              Path.Combine(TestData.RepoDir(), ProfileLocator.LensfunFolderName)));

    /// <summary>The shipped lensfun copy alone, no Adobe folder: a computer without Adobe's profiles (V-049).</summary>
    static readonly Lazy<ProfileSources> LensfunOnly = new(() =>
        ProfileLocator.Locate(Array.Empty<string>(), Path.Combine(TestData.RepoDir(), ProfileLocator.LensfunFolderName)));

    [Theory]
    [MemberData(nameof(Cases))]
    public void ValidationCase_MatchesRecordedFigures(string id)
    {
        JsonElement c = CaseById(id);
        string kind = c.GetProperty("kind").GetString()!;
        string? master = TestData.Large(c.GetProperty("master").GetString()!);
        if (master == null)
        {
            _out.WriteLine($"NOT MEASURED {id}: {c.GetProperty("master").GetString()} is absent under {TestData.LargeDir()}");
            return;
        }

        Dictionary<string, object> measured = kind switch
        {
            "structure" => Structure(master),
            "profile" => Profile(c, master),
            "smooth" => Smooth(c, master),
            "against" => Against(c, master),
            _ => throw new InvalidOperationException($"{id}: unknown kind {kind}"),
        };

        string line = id + " " + JsonSerializer.Serialize(measured);
        _out.WriteLine("MEASURED " + line);
        string? dump = Environment.GetEnvironmentVariable("FLATCHECK_VALIDATION_DUMP");
        if (!string.IsNullOrEmpty(dump))
            File.AppendAllText(dump, line + "\n");

        if (!c.TryGetProperty("expected", out JsonElement expected) || expected.ValueKind == JsonValueKind.Null)
        {
            _out.WriteLine($"UNMEASURED {id}: no expected figures recorded yet");
            return;
        }

        double tol = c.TryGetProperty("tolerance", out JsonElement t) ? t.GetDouble() : 0.05;
        List<string> failures = new();
        foreach (JsonProperty p in expected.EnumerateObject())
        {
            if (!measured.TryGetValue(p.Name, out object? got))
            {
                failures.Add($"{p.Name}: not measured by this kind");
                continue;
            }

            if (p.Value.ValueKind == JsonValueKind.Number)
            {
                double want = p.Value.GetDouble();
                double have = Convert.ToDouble(got, CultureInfo.InvariantCulture);
                if (double.IsNaN(have) || Math.Abs(have - want) > tol)
                    failures.Add($"{p.Name}: expected {want} got {have} (tolerance {tol})");
            }
            else if (!string.Equals(p.Value.GetString(), got.ToString(), StringComparison.Ordinal))
            {
                failures.Add($"{p.Name}: expected '{p.Value.GetString()}' got '{got}'");
            }
        }

        Assert.True(failures.Count == 0, $"{id} drifted from docs/validation.json:\n  " + string.Join("\n  ", failures));
    }

    static Dictionary<string, object> Structure(string master)
    {
        StructureResult r = StructureRun.Execute(master, TextWriter.Null).Result.Structure;
        return new Dictionary<string, object>
        {
            ["cornerSpreadPct"] = R(r.CornerSpreadPct),
            ["azimuthalMaxPct"] = R(r.AzimuthalMaxPct),
            ["residualPtpPct"] = R(r.ResidualPtpPct),
            ["tiltPct"] = R(r.TiltPct),
            ["maxTilePct"] = R(r.MaxTilePct),
            ["vignetteStops"] = R(r.VignetteStops),
            ["localSpotPct"] = R(r.LocalSpotPct),
            ["verdict"] = r.Verdict,
        };
    }

    static Dictionary<string, object> Profile(JsonElement c, string master)
    {
        string? raw = TestData.Large(c.GetProperty("raw").GetString()!);
        Assert.True(raw != null, "raw absent: " + c.GetProperty("raw").GetString());
        bool lensfunOnly = c.TryGetProperty("sources", out JsonElement src) && src.GetString() == "lensfun";
        var q = new StructureQuery { Sources = lensfunOnly ? LensfunOnly.Value : Sources.Value, RawPath = raw };
        StructureOutcome o = StructureRun.Execute(master, TextWriter.Null, q).Result;
        var m = new Dictionary<string, object>
        {
            ["masterStops"] = R(o.Structure.VignetteStops),
            ["shapeVerdict"] = o.Structure.Verdict,
            ["verdict"] = o.Verdict,
        };
        if (o.Compared)
        {
            m["diffStops"] = R(o.Profiles.Nearest.DiffStops);
            m["nearest"] = o.Profiles.Nearest.Profile.Lens.Name + " [" + o.Profiles.Nearest.Profile.Lens.Where + "]";
            m["depthVerdict"] = o.Profiles.Verdict;
            m["profiles"] = o.Profiles.Profiles.Count;
        }
        else
        {
            m["gap"] = o.Profiles?.Gap.ToString() ?? (o.RawError ?? "no comparison");
        }

        return m;
    }

    static Dictionary<string, object> Smooth(JsonElement c, string master)
    {
        double scale = c.TryGetProperty("scalePx", out JsonElement s) ? s.GetDouble() : SmoothAnalyzer.DefaultScalePx;
        SmoothRunOutcome o = SmoothRun.Execute(master, scale, TextWriter.Null);
        if (o.Refused)
            return new Dictionary<string, object> { ["refusal"] = o.Refusal };
        SmoothResult r = o.Result;
        return new Dictionary<string, object>
        {
            ["removedPtpPct"] = R(r.RemovedPtpPct),
            ["removedMaxTilePct"] = R(r.RemovedMaxTilePct),
            ["removedSpotPct"] = R(r.RemovedSpotPct),
            ["removedSpotX"] = r.RemovedSpotX,
            ["removedSpotY"] = r.RemovedSpotY,
            ["inputTexturePct"] = R(r.InputTexturePct),
            ["outputTexturePct"] = R(r.OutputTexturePct),
            ["beforeVerdict"] = r.Before.Verdict,
            ["afterVerdict"] = r.After.Verdict,
            ["beforeVignetteStops"] = R(r.Before.VignetteStops),
            ["afterVignetteStops"] = R(r.After.VignetteStops),
        };
    }

    static Dictionary<string, object> Against(JsonElement c, string master)
    {
        var req = new SyntheticRequest
        {
            LensText = c.GetProperty("lens").GetString(),
            Source = c.TryGetProperty("source", out JsonElement src) && src.GetString() == "Lensfun" ? ProfileSource.Lensfun : ProfileSource.Adobe,
        };
        double? focal = c.TryGetProperty("focalMm", out JsonElement f) ? f.GetDouble() : null;
        double? fstop = c.TryGetProperty("fstop", out JsonElement a) ? a.GetDouble() : null;
        double? crop = c.TryGetProperty("crop", out JsonElement k) ? k.GetDouble() : null;
        SyntheticRunOutcome syn = SyntheticRun.Execute(master, req, Sources.Value, focal, fstop, crop, TextWriter.Null);
        if (syn.Refused)
            return new Dictionary<string, object> { ["refusal"] = syn.Refusal };

        string temp = Path.Combine(Path.GetTempPath(), "flatcheck-validation-" + Guid.NewGuid().ToString("N") + ".fit");
        try
        {
            using (FileStream fs = File.Create(temp))
                SyntheticRun.WriteFits(syn, master, fs);
            AgainstResult r = AgainstRun.Execute(master, temp, TextWriter.Null).Result;
            return new Dictionary<string, object>
            {
                ["profile"] = syn.Result.Profile.Lens.Name + " " + syn.Result.Profile.Settings,
                ["masterStops"] = R(r.A.VignetteStops),
                ["syntheticStops"] = R(r.B.VignetteStops),
                ["depthDiffStops"] = R(r.DepthDiffStops),
                ["depthErrorPct"] = R(r.DepthErrorPct),
                ["depthVerdict"] = r.DepthVerdict,
                ["ratioResidualPtpPct"] = R(r.Structure.ResidualPtpPct),
                ["shapeVerdict"] = r.Structure.Verdict,
                ["verdict"] = r.Verdict,
            };
        }
        finally
        {
            File.Delete(temp);
        }
    }

    static double R(double v) => Math.Round(v, 3);

    static string JsonPath => Path.Combine(TestData.RepoDir(), "docs", "validation.json");

    static JsonElement CaseById(string id)
    {
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(JsonPath));
        foreach (JsonElement c in doc.RootElement.GetProperty("cases").EnumerateArray())
            if (c.GetProperty("id").GetString() == id)
                return c.Clone();
        throw new InvalidOperationException("no case " + id);
    }

    public static TheoryData<string> Cases()
    {
        TheoryData<string> data = new();
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(JsonPath));
        foreach (JsonElement c in doc.RootElement.GetProperty("cases").EnumerateArray())
            data.Add(c.GetProperty("id").GetString()!);
        return data;
    }
}
