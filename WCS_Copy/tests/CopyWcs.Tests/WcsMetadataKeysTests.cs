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

namespace CopyWcs.Tests;

public class WcsMetadataKeysTests
{
    private static FitsHeader Header(params string[] extraCards)
    {
        var cards = new List<string> { FitsTestData.Card("SIMPLE", "T") };
        cards.AddRange(extraCards);
        return FitsHeader.ReadFrom(new MemoryStream(FitsTestData.HeaderBlock(cards)));
    }

    [Fact]
    public void R007_SelectKeysToCopy_StandardWcsKeyPresent_Included()
    {
        var header = Header(FitsTestData.Card("CTYPE1", "'RA---TAN'"));

        var selected = WcsMetadataKeys.SelectKeysToCopy(header, includeOptical: false);

        Assert.Contains("CTYPE1", selected);
    }

    [Fact]
    public void R007_SelectKeysToCopy_StandardWcsKeyAbsent_Excluded()
    {
        var header = Header();

        var selected = WcsMetadataKeys.SelectKeysToCopy(header, includeOptical: false);

        Assert.DoesNotContain("CTYPE1", selected);
    }

    [Theory]
    [InlineData("A_0_2")]
    [InlineData("B_1_1")]
    [InlineData("AP_2_0")]
    [InlineData("BP_0_3")]
    public void R007_SelectKeysToCopy_SipCoefficientPresent_MatchedAndIncluded(string sipKey)
    {
        var header = Header(FitsTestData.Card(sipKey, "1.0e-05"));

        var selected = WcsMetadataKeys.SelectKeysToCopy(header, includeOptical: false);

        Assert.Contains(sipKey, selected);
    }

    [Fact]
    public void R007_SelectKeysToCopy_StandardMatrixKeyIncluded_ViaLiteralListNotSipRegex()
    {
        // CD1_1 is a standard WCS key, included through WcsKeys; the SIP regex should not
        // add it a second time.
        var header = Header(FitsTestData.Card("CD1_1", "1.0"));

        var selected = WcsMetadataKeys.SelectKeysToCopy(header, includeOptical: false);

        Assert.Equal(1, selected.Count(k => k == "CD1_1"));
    }

    [Fact]
    public void R007_SelectKeysToCopy_UnrelatedKeyResemblingSipPattern_NotMatched()
    {
        // "ABC_1_2" does not start with one of the SIP prefixes (A, B, AP, BP followed directly by "_").
        var header = Header(FitsTestData.Card("ABC_1_2", "1.0"));

        var selected = WcsMetadataKeys.SelectKeysToCopy(header, includeOptical: false);

        Assert.DoesNotContain("ABC_1_2", selected);
    }

    [Fact]
    public void R007_SelectKeysToCopy_OpticalKeyIncludedOnlyWhenIncludeOpticalTrue()
    {
        var header = Header(FitsTestData.Card("TELESCOP", "'My Scope'"));

        var withOptical = WcsMetadataKeys.SelectKeysToCopy(header, includeOptical: true);
        var withoutOptical = WcsMetadataKeys.SelectKeysToCopy(header, includeOptical: false);

        Assert.Contains("TELESCOP", withOptical);
        Assert.DoesNotContain("TELESCOP", withoutOptical);
    }

    [Fact]
    public void R007_SelectKeysToCopy_NoRelevantKeysInSource_ReturnsEmptyList()
    {
        var header = Header(FitsTestData.Card("UNRELATED", "'x'"));

        var selected = WcsMetadataKeys.SelectKeysToCopy(header, includeOptical: true);

        Assert.Empty(selected);
    }

    [Fact]
    public void R007_SelectKeysToCopy_ResultHasNoDuplicates()
    {
        var header = Header(
            FitsTestData.Card("CTYPE1", "'RA---TAN'"),
            FitsTestData.Card("TELESCOP", "'My Scope'"));

        var selected = WcsMetadataKeys.SelectKeysToCopy(header, includeOptical: true);

        Assert.Equal(selected.Count, selected.Distinct().Count());
    }
}
