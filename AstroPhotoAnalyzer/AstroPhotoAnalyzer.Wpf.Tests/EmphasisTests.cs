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

using System.Windows.Controls;
using System.Windows.Documents;

namespace AstroPhotoAnalyzer.Wpf.Tests;

[Collection("fereastra")]
public class EmphasisTests
{
    readonly StaFixture _sta;

    public EmphasisTests(StaFixture sta) => _sta = sta;

    [Fact]
    public void R112_TheBecauseMarksAreShownBoldAfterTheLabelAndStrippedFromTheText()
    {
        const string marked = "pe **0.20 %** din cadru (prag **0.10 %**)";
        Assert.Equal("pe 0.20 % din cadru (prag 0.10 %)", Fmt.Plain(marked));
        Assert.Equal("**0.20 %**", Fmt.Strong("0.20 %"));

        _sta.Invoke(() =>
        {
            var block = new TextBlock();
            block.Inlines.Add(new Run("fiindca: "));
            Emphasis.SetText(block, marked);

            Inline[] inlines = block.Inlines.ToArray();
            Assert.Equal(6, inlines.Length);
            Assert.Equal("fiindca: ", ((Run)inlines[0]).Text);
            Assert.Equal("pe ", ((Run)inlines[1]).Text);
            Assert.Equal("0.20 %", ((Run)((Bold)inlines[2]).Inlines.FirstInline).Text);
            Assert.Equal(" din cadru (prag ", ((Run)inlines[3]).Text);
            Assert.Equal("0.10 %", ((Run)((Bold)inlines[4]).Inlines.FirstInline).Text);
            Assert.Equal(")", ((Run)inlines[5]).Text);

            // A new text replaces the appended runs and keeps the label; an unclosed mark is
            // kept as text.
            Emphasis.SetText(block, "plain **stray");
            inlines = block.Inlines.ToArray();
            Assert.Equal(2, inlines.Length);
            Assert.Equal("plain **stray", ((Run)inlines[1]).Text);
        });
    }
}
