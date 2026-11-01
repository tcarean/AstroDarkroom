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
/// The explanations of the Exposure, Histogram, Synthetic and Split tabs that the console
/// shares with them are in plain words (R-123): none names the method. The guides that live in
/// the window alone are checked in the window's test project.
/// </summary>
public class OtherTabsPlainWordsTests
{
    /// <summary>The words of the method, as whole words (not after a dot), in both languages.
    /// The view named Mosaic and the word debayered, kept on purpose in one explanation, are
    /// not among them.</summary>
    internal static readonly System.Text.RegularExpressions.Regex MethodWords = new(
        @"(?<![\w.])(median\w*|distributi\w*|repartiti\w*|CFA|Bayer|gamma|gama|clipping|clipeaz\w*|clips?"
        + @"|SNR|radial\w*|gradient\w*|sectors|sectoare|dala|tile|ranks|ranguri|polynomial|polinom\w*"
        + @"|extrapolat\w*|extrapolare|azimut\w*|acromatic\w*|achromatic|cromatic\w*|chromatic)(?!\w)");

    /// <summary>Every shared explanation that needs no analysed frame, in the current
    /// language.</summary>
    static IEnumerable<string> Texts()
    {
        yield return HistEqReport.Caveat();
        yield return HistEqReport.MasterVersusFrame();
        yield return HistEqReport.RainbowLegend();
        yield return HistEqReport.RainbowNotOnRgb();
        yield return SyntheticReport.Caveat();
        yield return SyntheticReport.ExtrapolationTail(0.5);
        yield return SyntheticReport.ExtrapolationTail(1.05);
        yield return SyntheticReport.ExtrapolationTail(1.5);
        foreach (string key in new[] { "CornerSpreadPct", "AzimuthalMaxPct", "ResidualPtpPct", "TiltPct", "MaxTilePct", "VignetteStops", "DiffStops", "Verdict" })
            yield return SplitReport.RowNote(key);
        yield return SplitReport.Meaning();
        foreach (string key in StructureReport.TestKeys) yield return AgainstReport.TestNote(key);
        yield return AgainstReport.Meaning();
        yield return AgainstReport.WorseOfBoth();
        yield return ExposureReport.HistogramCaption(true);
        yield return ExposureReport.HistogramCaption(false);
    }

    /// <summary>Twenty-six texts in each language, none with a word of the method.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void R123_TheSharedExplanationsAvoidTheMethodsVocabulary(bool english)
    {
        using var _ = Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        string[] texts = Texts().ToArray();

        Assert.Equal(26, texts.Length);
        foreach (string text in texts)
        {
            Assert.False(string.IsNullOrWhiteSpace(text));
            var hit = MethodWords.Match(text);
            Assert.False(hit.Success, $"'{hit.Value}' in: {text}");
        }
    }
}
