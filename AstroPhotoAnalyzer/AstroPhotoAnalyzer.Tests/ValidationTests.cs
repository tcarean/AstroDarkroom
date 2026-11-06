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
        string kind = c.GetProperty("kind").GetString()!;
        Assert.Contains(kind, new[] { "analysis", "icc", "curves", "proof", "gamutfit" });

        string name = c.GetProperty("image").GetString()!;
        string? image = TestData.Large(name);
        if (image == null)
        {
            _out.WriteLine($"NOT MEASURED {id}: {name} is absent under {TestData.LargeDir()}");
            return;
        }

        // An icc case reads the Dmax of a printer profile (R-028); a curves case suggests the
        // curves for print and saves one (R-140 to R-145); an analysis case runs the whole chain.
        Dictionary<string, object> measured = kind switch
        {
            "icc" => IccFigures(IccPaperBlack.Read(image)),
            "curves" => CurveFigures(image, c.GetProperty("parameters")),
            "proof" => ProofFigures(image, c.GetProperty("parameters")),
            "gamutfit" => GamutFitFigures(image, c.GetProperty("parameters")),
            _ => Figures(AnalysisRun.Execute(image, ParametersOf(c.GetProperty("parameters")))),
        };

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

    /// <summary>
    /// What a curves case guards: the anchors, each suggestion's upper points, the five L* after
    /// it, its noise gain, clipped share and, with the parameter <c>icc</c> (a profile beside the
    /// image), the share out of gamut; and a save of the last suggestion into a temporary folder:
    /// every card of a FITS input kept, the input's bytes unchanged.
    /// </summary>
    static Dictionary<string, object> CurveFigures(string image, JsonElement parameters)
    {
        Parameters p = ParametersOf(parameters);
        if (parameters.TryGetProperty("icc", out JsonElement icc))
        {
            string profile = TestData.Large(icc.GetString()!) ?? throw new FileNotFoundException("the case's profile is absent: " + icc.GetString());
            p = p.WithDmaxFromProfile(profile, out string error) ?? throw new Exception(error);
        }
        AnalysisResult a = AnalysisRun.Execute(image, p);
        CurveSet set = CurveRun.Execute(a, CurvePurpose.Print, 1600, 900);
        string[] cls = { "dust", "sky", "band", "clouds", "peaks" };
        var f = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["curves.method"] = set.Method.ToString(),
            ["curves.problem"] = set.Problem ?? "none",
            ["curves.count"] = set.Suggestions.Count,
            ["curves.names"] = string.Join(",", set.Suggestions.Select(s => s.Name)),
            ["curves.previewWidth"] = set.OriginalPreview.W,
        };
        double[] anchors = set.Anchors.AsArray();
        for (int i = 0; i < 5; i++) f["curves.anchor." + cls[i]] = R(anchors[i]);
        foreach (CurveSuggestion s in set.Suggestions)
        {
            string k = "curves." + s.Name + ".";
            CurveEvidence e = s.Evidence;
            for (int i = 0; i < 5; i++)
            {
                f["curves.before.lstar." + cls[i]] = R(e.LstarBefore[i]);
                f[k + "lstar." + cls[i]] = R(e.LstarAfter[i]);
                if (i >= 2) f[k + "point." + cls[i]] = R(s.Curve.Points[i + 1].Y);
            }
            f["curves.before.pctClipped"] = R(e.PctClippedBefore);
            f[k + "noiseGain"] = R(e.NoiseGain);
            f[k + "noiseAfterLsb"] = R(e.NoiseAfterLsb);
            f[k + "pctClipped"] = R(e.PctClippedAfter);
            f[k + "paperChecks"] = e.PaperChecks.Count;
            f[k + "monotone"] = s.Curve.IsMonotone;
            if (e.Gamut == null) continue;
            f["curves.before.gamut.all"] = R(e.GamutBefore.PctAll);
            f[k + "gamut.all"] = R(e.Gamut.PctAll);
            for (int i = 0; e.Gamut.PctByClass != null && i < 5; i++)
            {
                f["curves.before.gamut." + cls[i]] = R(e.GamutBefore.PctByClass[i]);
                f[k + "gamut." + cls[i]] = R(e.Gamut.PctByClass[i]);
            }
        }
        if (set.Suggestions.Count == 0) return f;

        using var ws = new TempWorkspace();
        static string Hash(string path)
        {
            using FileStream fs = File.OpenRead(path);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(fs));
        }
        string before = Hash(image), target = ws.PathTo("curba.fit");
        CurveSuggestion last = set.Suggestions[^1];
        CurveExport.Save(a, last, target);
        f["save.inputUnchanged"] = Hash(image) == before;
        var saved = FitsReader.ReadImageHeader(target);
        f["save.bitpix"] = FitsReader.HInt(saved.Header, "BITPIX", 0);
        f["save.width"] = saved.W;
        f["save.fileBytes"] = new FileInfo(target).Length;
        if (image.EndsWith(".fit", StringComparison.OrdinalIgnoreCase))
        {
            var source = FitsReader.ReadImageHeader(image);
            f["save.cards"] = saved.Header.Count;
            f["save.everyCardKept"] = source.Header.All(kv => saved.Header.TryGetValue(kv.Key, out string? v) && v == kv.Value);
        }
        Raster back = ImageLoader.Load(target), want = last.Curve.Apply(a.Raster);
        double worst = 0;
        for (int pl = 0; pl < want.Planes.Length; pl++)
            for (int i = 0; i < want.Planes[pl].Length; i += 97)
                worst = Math.Max(worst, Math.Abs(want.Planes[pl][i] - back.Planes[pl][i]));
        f["save.maxDifference"] = R(worst);
        f["save.hasWcs"] = back.Source.Wcs != null;
        return f;
    }

    /// <summary>What an icc case guards: the profile's name, whether it could be read, its black and its Dmax.</summary>
    /// <summary>
    /// A proof case (R-160 to R-162, ADR 0066): the analysis with the case's profile, the proof
    /// prepared on it, and each intent rendered with the simulation off and on: the profile's
    /// facts, the share outside the paper's gamut and the mean L* of each proof.
    /// </summary>
    static Dictionary<string, object> ProofFigures(string image, JsonElement parameters)
    {
        Parameters p = ParametersOf(parameters);
        string profile = TestData.Large(parameters.GetProperty("icc").GetString()!) ?? throw new FileNotFoundException("the case's profile is absent: " + parameters.GetProperty("icc").GetString());
        p = p.WithDmaxFromProfile(profile, out string error) ?? throw new Exception(error);
        AnalysisResult a = AnalysisRun.Execute(image, p);
        ProofSet set = ProofRun.Prepare(a);
        var f = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["proof.problem"] = set.Problem ?? "none",
        };
        if (set.Problem != null) return f;
        ProofProfile pr = set.Profile;
        f["proof.description"] = pr.Description;
        f["proof.paperWhite.L"] = R(pr.PaperWhiteLab.L);
        f["proof.paperWhite.a"] = R(pr.PaperWhiteLab.A);
        f["proof.paperWhite.b"] = R(pr.PaperWhiteLab.B);
        f["proof.paperWhite.srgb"] = string.Join(",", pr.PaperWhiteSrgb.Select(v => RasterOps.Level8(v)));
        f["proof.paperBlackLstar"] = R(pr.PaperBlackLstar);
        f["proof.dmax"] = pr.Dmax;
        f["proof.width"] = set.Reduced.W;
        f["proof.pctPaperGamut"] = R(set.PctPaper);
        foreach (ProofIntent intent in Enum.GetValues<ProofIntent>())
            foreach (bool simulate in new[] { false, true })
            {
                SoftProofResult r = ProofRun.Render(set, intent, simulate);
                string k = "proof." + intent + (simulate ? ".simulated." : ".");
                float[] L = RasterOps.LstarPlane(r.Image, 1);
                f[k + "meanLstar"] = R(L.Average());
            }
        return f;
    }

    /// <summary>
    /// The paper fit through a profile (R-175 to R-177): the three variants' shares and losses
    /// on the tonal subsample, the recipe, and the two files written to a temporary folder,
    /// read back.
    /// </summary>
    static Dictionary<string, object> GamutFitFigures(string image, JsonElement parameters)
    {
        Parameters p = ParametersOf(parameters);
        string profile = TestData.Large(parameters.GetProperty("icc").GetString()!) ?? throw new FileNotFoundException("the case's profile is absent: " + parameters.GetProperty("icc").GetString());
        p = p.WithDmaxFromProfile(profile, out string error) ?? throw new Exception(error);
        AnalysisResult a = AnalysisRun.Execute(image, p);
        GamutFitSet set = GamutFitRun.Execute(a, Thresholds.CurvePreviewDefaultWidth, Thresholds.CurvePreviewDefaultHeight);
        var f = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["gamutfit.problem"] = set.Problem ?? "none",
        };
        if (set.Problem != null) return f;
        f["gamutfit.description"] = set.ProfileDescription;
        f["gamutfit.names"] = string.Join(",", set.Variants.Select(v => v.Name));
        foreach (GamutFitVariant v in set.Variants)
        {
            string k = "gamutfit." + v.Name.Replace(' ', '-') + ".";
            GamutFitEvidence e = v.Evidence;
            f[k + "before"] = R(e.PctOutsideBefore);
            f[k + "after"] = R(e.PctOutsideAfter);
            f[k + "touched"] = R(e.PctTouched);
            f[k + "unreachable"] = R(e.PctUnreachable);
            f[k + "lostMedian"] = R(e.ChromaLostMedian);
            f[k + "lostP99"] = R(e.ChromaLostP99);
            f[k + "dark"] = R(e.PctOutsideUnderLowSeparation);
        }
        f["gamutfit.recipe.lstar"] = R(set.Recipe.LstarCeiling);
        f["gamutfit.recipe.factor"] = R(set.Recipe.Factor);
        f["gamutfit.recipe.left"] = R(set.Recipe.PctLeft);
        f["gamutfit.recipe.inBand"] = R(set.Recipe.PctInBand);
        using var ws = new TempWorkspace();
        GamutFitVariant exact = set.Variants[0];
        string fits = ws.PathTo("fit.fit"), tiff = ws.PathTo("fit.tif");
        GamutFitExport.Save(a, set, exact, fits);
        GamutFitExport.Save(a, set, exact, tiff);
        Raster fromFits = ImageLoader.Load(fits), fromTiff = ImageLoader.Load(tiff);
        f["gamutfit.fits.bytes"] = new FileInfo(fits).Length;
        f["gamutfit.tiff.bytes"] = new FileInfo(tiff).Length;
        f["gamutfit.tiff.bitDepth"] = fromTiff.Source.BitDepth;
        f["gamutfit.tiff.icc"] = fromTiff.Source.IccDescription ?? "none";
        double maxDiff = 0;
        for (int pl = 0; pl < 3; pl++)
            for (int i = 0; i < fromFits.W * fromFits.H; i += 97)
                maxDiff = Math.Max(maxDiff, Math.Abs(fromFits.Planes[pl][i] - fromTiff.Planes[pl][i]) * 65535);
        f["gamutfit.tiffVsFits.maxLevels16"] = R(maxDiff);
        return f;
    }

    static Dictionary<string, object> IccFigures(IccBlack b) => new()
    {
        ["icc.description"] = b.Description,
        ["icc.readable"] = b.Readable,
        ["icc.blackLstar"] = R(b.BlackLstar),
        ["icc.dmax"] = b.Dmax,
    };

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
        f["simulation.skyReason"] = s.Map.SkyReason.ToString();
        f["simulation.latitudeSpanDeg"] = double.IsNaN(s.Map.LatitudeSpanDeg) ? "n/a" : R(s.Map.LatitudeSpanDeg);
        foreach (PatchClass c in new[] { PatchClass.Dust, PatchClass.Sky, PatchClass.Band, PatchClass.BrightCloud, PatchClass.Peak })
            f["simulation.pctPatches." + c] = R(100 * s.Map.FractionOf(c));
        f["simulation.patchCount"] = s.PatchCount;
        f["simulation.cloudCount"] = s.CloudCount;
        f["simulation.dustPatchCount"] = s.DustPatchCount;
        f["simulation.asIs.lstarDust"] = R(s.AsIs.LstarDust);
        f["simulation.asIs.lstarSky"] = R(s.AsIs.LstarSky);
        f["simulation.asIs.pctPatchesBelowPaperBlack"] = R(s.AsIs.PctPatchesBelowPaperBlack);
        f["simulation.dustOnPaperBlackAsIs"] = s.DustOnPaperBlackAsIs;
        f["simulation.skyBelowPaperBlack"] = s.SkyBelowPaperBlack;
        f["simulation.skyBelowVisibility"] = s.SkyBelowVisibility;
        f["simulation.visibilityTarget"] = s.VisibilityTarget is double vt ? R(vt) : "none";
        f["simulation.recommendedTarget"] = s.RecommendedTarget is double rt ? R(rt) : "none";
        f["simulation.recommendedIsAsIs"] = s.RecommendedIsAsIs;
        f["simulation.exactTarget"] = s.ExactTarget is double et ? R(et) : "none";
        f["simulation.exactTargetDmaxLower"] = s.ExactTargetDmaxLower is double el ? R(el) : "none";
        f["simulation.exactTargetDmaxHigher"] = s.ExactTargetDmaxHigher is double eh ? R(eh) : "none";
        f["simulation.dmaxDustOnPaperBlackAsIs"] = R(s.DmaxDustOnPaperBlackAsIs);
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
            f["background.lab.l"] = R(b.Lab.L);
            f["background.lab.a"] = R(b.Lab.A);
            f["background.lab.b"] = R(b.Lab.B);
            f["background.chroma"] = R(b.Chroma);
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
        f["crop.cutStartPx"] = cr.CutStartPx;
        f["crop.cutEndPx"] = cr.CutEndPx;
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
        f["quantization.plateauPositions"] = q.PlateauPositions;
        f["quantization.plateauFramePct"] = R(q.PlateauFramePct);
        f["quantization.plateauPx"] = R(q.PlateauPx);
        f["quantization.plateauSigmaLsb"] = R(q.PlateauSigmaLsb);
        f["quantization.plateauVisibleFramePctAtReference"] = a.Size.ReferenceRow == null ? "n/a" : R(q.PlateauVisibleFramePctAt(a.Size.ReferenceRow.Dpi));
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
