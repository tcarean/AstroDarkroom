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

namespace FlatCheck.Tests;

/// <summary>
/// The structure explanations are in plain words (R-122): the sentences the window shows under
/// its figures and the console prints in the report name no part of the method. The guides that
/// live in the window alone are checked in the window's test project.
/// </summary>
public class StructurePlainWordsTests
{
    /// <summary>The words of the method, as whole words (not after a dot, so that a file
    /// pattern's ".fit" does not count), in both languages.</summary>
    internal static readonly System.Text.RegularExpressions.Regex MethodWords = new(
        @"(?<![\w.])(radial symmetry|simetri[ae] radiala|purely radial|pur radial|scale-invariant"
        + @"|[Ll]ambertian|achromatic|acromatic|mosaic|mozaic|degenerate|degenerat|sampled|esantionat"
        + @"|sectors|sectoare|azimuth|azimuturi|peak-to-peak|varf-la-varf|gradient\w*|fit|fitul|fitat"
        + @"|curvature|curbura|RADIAL[AE]?|radial excess|exces radial)(?!\w)");

    /// <summary>Every explanation the report composes, in the current language.</summary>
    static IEnumerable<string> Texts()
    {
        StructureResult vignetted = StructureAnalyzer.Analyze(TestImages.From(512, 512,
            (x, y) => 1000 - 0.002 * ((x - 256.0) * (x - 256.0) + (y - 256.0) * (y - 256.0))));
        StructureResult flat = StructureAnalyzer.Analyze(TestImages.From(512, 512, (_, _) => 1000));
        var spotted = new StructureResult { LocalSpotFlagged = true, LocalSpotPct = -3.2, LocalSpotX = 10, LocalSpotY = 20 };

        foreach (string key in StructureReport.TestKeys) yield return StructureReport.TestNote(key);
        yield return StructureReport.VignetteNote(vignetted);
        yield return StructureReport.CentroidNote(vignetted);
        yield return StructureReport.CentroidNote(flat);
        yield return StructureReport.LocalSpotNote();
        yield return StructureReport.LocalSpotWarning(spotted);
        foreach (var (_, _, meaning) in StructureReport.Scale()) yield return meaning;
        yield return StructureReport.WhyResidualMatters();
        foreach (string verdict in new[] { "PERFECT", "BUN", "ACCEPTABIL", "REFA" }) yield return StructureReport.Advice(verdict);
        yield return StructureReport.DepthAdvice(0.3, "REFA");
        yield return StructureReport.DepthAdvice(-0.3, "BUN");
        foreach (var (_, _, meaning) in StructureReport.DepthScaleRows()) yield return meaning;
        yield return StructureReport.DepthScaleNote();
        yield return StructureReport.DepthLightScaleNote(0.3);
        yield return StructureReport.DepthLightScaleNote(double.NaN);
        yield return StructureReport.ComparisonNote();
        yield return StructureReport.GapMeaning();
        yield return StructureReport.FilteredLensNote();
        yield return StructureReport.WorseOfBoth;
    }

    /// <summary>
    /// Thirty-two texts in each language, none naming the fit, the model, a gradient, a sector
    /// or the rest of the method's vocabulary.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void R122_TheReportsExplanationsAvoidTheMethodsVocabulary(bool english)
    {
        using var _ = Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        string[] texts = Texts().ToArray();

        Assert.Equal(32, texts.Length);
        foreach (string text in texts)
        {
            Assert.False(string.IsNullOrWhiteSpace(text));
            var hit = MethodWords.Match(text);
            Assert.False(hit.Success, $"'{hit.Value}' in: {text}");
        }
    }

    /// <summary>The requirement's example sentence, the advice at PERFECT, in English.</summary>
    [Fact]
    public void R122_TheAdviceAtPerfectIsTheRequirementsExample()
    {
        using var _ = Ui.Use(UiLanguage.English);
        Assert.Equal("The flat looks like a clean vignetting; what departs from it is below the noise.",
                     StructureReport.Advice("PERFECT"));
    }

    /// <summary>The labels of the five tests stay: they are names, not explanations.</summary>
    [Fact]
    public void R122_TheTestLabelsKeepTheirNames()
    {
        using var _ = Ui.Use(UiLanguage.Romanian);
        Assert.Equal("reziduu dupa model rad.", StructureReport.TestLabel(nameof(StructureResult.ResidualPtpPct)));
        Assert.Equal("asimetrie azimutala max", StructureReport.TestLabel(nameof(StructureResult.AzimuthalMaxPct)));
        Assert.Equal("pata maxima (o dala)", StructureReport.TestLabel(nameof(StructureResult.MaxTilePct)));
    }
}
