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
/// The Structure tab's guides are in plain words (R-122): the "How to read" guide and the map
/// guide, which live in the window alone, name no part of the method. The sentences shared
/// with the console are checked in the core test project.
/// </summary>
public class StructurePlainWordsTests
{
    /// <summary>The same words of the method as the core test bans, as whole words, in both
    /// languages.</summary>
    static readonly System.Text.RegularExpressions.Regex MethodWords = new(
        @"(?<![\w.])(radial symmetry|simetri[ae] radiala|purely radial|pur radial|scale-invariant"
        + @"|[Ll]ambertian|achromatic|acromatic|mosaic|mozaic|degenerate|degenerat|sampled|esantionat"
        + @"|sectors|sectoare|azimuth|azimuturi|peak-to-peak|varf-la-varf|gradient\w*|fit|fitul|fitat"
        + @"|curvature|curbura|RADIAL[AE]?|radial excess|exces radial|artefact|artifact)(?!\w)");

    /// <summary>The two guides' texts, headings included, in the current language.</summary>
    static IEnumerable<string> Texts()
    {
        yield return SessionMessages.HowToReadIntro();
        foreach (var (shape, meaning) in SessionMessages.HowToReadRows()) { yield return shape; yield return meaning; }
        yield return SessionMessages.MapGuideIntro();
        foreach (var (shape, meaning) in SessionMessages.MapGuideShapes()) { yield return shape; yield return meaning; }
    }

    /// <summary>Eighteen texts in each language, none with a word of the method.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void R122_TheGuidesAvoidTheMethodsVocabulary(bool english)
    {
        using var _ = Ui.Use(english ? UiLanguage.English : UiLanguage.Romanian);
        string[] texts = Texts().ToArray();

        Assert.Equal(18, texts.Length);
        foreach (string text in texts)
        {
            Assert.False(string.IsNullOrWhiteSpace(text));
            var hit = MethodWords.Match(text);
            Assert.False(hit.Success, $"'{hit.Value}' in: {text}");
        }
    }
}
