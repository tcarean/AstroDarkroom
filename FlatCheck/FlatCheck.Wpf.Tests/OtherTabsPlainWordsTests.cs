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

namespace FlatCheck.Wpf.Tests;

/// <summary>
/// The guides of the Exposure, Histogram and Synthetic tabs, which live in the window alone,
/// are in plain words (R-123): none names the method. The sentences shared with the console are
/// checked in the core test project.
/// </summary>
public class OtherTabsPlainWordsTests
{
    /// <summary>The same words of the method as the core test bans, as whole words, in both
    /// languages.</summary>
    static readonly System.Text.RegularExpressions.Regex MethodWords = new(
        @"(?<![\w.])(median\w*|distributi\w*|repartiti\w*|CFA|Bayer|gamma|gama|clipping|clipeaz\w*|clips?"
        + @"|SNR|radial\w*|gradient\w*|sectors|sectoare|dala|tile|ranks|ranguri|polynomial|polinom\w*"
        + @"|extrapolat\w*|extrapolare|azimut\w*|acromatic\w*|achromatic|cromatic\w*|chromatic)(?!\w)");

    /// <summary>The guides' texts, headings included, in the current language.</summary>
    static IEnumerable<string> Texts()
    {
        yield return SessionMessages.HistogramGuideIntro();
        foreach (var (shape, meaning) in SessionMessages.HistogramGuideRows()) { yield return shape; yield return meaning; }
        yield return SessionMessages.HistEqGuideIntro();
        foreach (var (shape, meaning) in SessionMessages.HistEqGuideRows()) { yield return shape; yield return meaning; }
        yield return SessionMessages.SyntheticGuideIntro();
    }

    /// <summary>Forty-one texts in each language, none with a word of the method.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void R123_TheGuidesAvoidTheMethodsVocabulary(bool english)
    {
        using var _ = Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        string[] texts = Texts().ToArray();

        Assert.Equal(41, texts.Length);
        foreach (string text in texts)
        {
            Assert.False(string.IsNullOrWhiteSpace(text));
            var hit = MethodWords.Match(text);
            Assert.False(hit.Success, $"'{hit.Value}' in: {text}");
        }
    }

    /// <summary>The requirement's example sentence: the histogram guide's vertical line, in
    /// English.</summary>
    [Fact]
    public void R123_TheVerticalLineRowIsTheRequirementsExample()
    {
        using var _ = Ui.Use(UiLanguage.English);
        var row = SessionMessages.HistogramGuideRows().Single(r => r.Shape == "the vertical line");
        Assert.Equal("The average level at the middle of the frame, the one figure that decides. On the "
                     + "brightest channel it is the very number in the verdict pill above.", row.Meaning);
    }
}
