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

    private static FitsHeader Read(List<string> cards) => FitsHeader.ReadFrom(new MemoryStream(FitsTestData.HeaderBlock(cards)));

    [Fact]
    public void R016_SelectKeysToDrop_TheDestinationsWcsAndSipKeysTheSourceLacks_InTheDestinationsOrder()
    {
        var source = Read(FitsTestData.PlateSolvedCards());
        var dest = Read(FitsTestData.OldSolveCards());

        var drop = WcsMetadataKeys.SelectKeysToDrop(source, dest);

        Assert.Equal(FitsTestData.OldSolveKeysToDrop, drop);
    }

    [Fact]
    public void R016_SelectKeysToDrop_NeverAnOpticalKey_AKeyTheSourceHas_OrAnUnrelatedKey()
    {
        // SITELAT is optical and absent from the source; CRVAL1, A_0_2 and OBJECT are in both;
        // ABC_1_2 only looks like a SIP coefficient.
        var source = Read(FitsTestData.PlateSolvedCards());
        List<string> destCards = FitsTestData.OldSolveCards();
        destCards.Add(FitsTestData.Card("ABC_1_2", "1.0"));
        var dest = Read(destCards);

        var drop = WcsMetadataKeys.SelectKeysToDrop(source, dest);

        Assert.DoesNotContain("SITELAT", drop);
        Assert.DoesNotContain("CRVAL1", drop);
        Assert.DoesNotContain("A_0_2", drop);
        Assert.DoesNotContain("OBJECT", drop);
        Assert.DoesNotContain("ABC_1_2", drop);
    }

    [Fact]
    public void R016_SelectKeysToDrop_AKeywordTwiceInTheDestination_IsNamedOnce()
    {
        var source = Read(FitsTestData.PlateSolvedCards());
        List<string> destCards = FitsTestData.ProcessedCards();
        destCards.Add(FitsTestData.Card("CD1_1", "-0.000264"));
        destCards.Add(FitsTestData.Card("CD1_1", "-0.000265"));
        var dest = Read(destCards);

        var drop = WcsMetadataKeys.SelectKeysToDrop(source, dest);

        Assert.Equal(["CD1_1"], drop);
    }

    [Fact]
    public void R017_Orientation_ReadsTheCdMatrixFirst_AsSirilDoes()
    {
        // The SyQon-like header: CDELT as Siril left it (negative), the CD turned over (positive).
        Assert.Equal(1, WcsMetadataKeys.Orientation(Read(FitsTestData.MirroredSolveCards())));
    }

    [Fact]
    public void R017_Orientation_WithoutCd_IsCdeltTimesThePcMatrix_TheIdentityWhereAPcCardIsMissing()
    {
        Assert.Equal(-1, WcsMetadataKeys.Orientation(Read(FitsTestData.PlateSolvedCards())));

        List<string> turnedOver = FitsTestData.PlateSolvedCards();
        turnedOver.Add(FitsTestData.Card("PC2_2", "-1.0"));
        Assert.Equal(1, WcsMetadataKeys.Orientation(Read(turnedOver)));
    }

    [Fact]
    public void R017_Orientation_AMissingCdeltCountsAs1()
    {
        List<string> cards = [FitsTestData.Card("SIMPLE", "T"), FitsTestData.Card("CTYPE1", "'RA---TAN'")];

        Assert.Equal(-1, WcsMetadataKeys.Orientation(Read([.. cards, FitsTestData.Card("CDELT1", "-0.000305")])));
        Assert.Equal(1, WcsMetadataKeys.Orientation(Read([.. cards, FitsTestData.Card("CDELT2", "0.000305")])));
    }

    /// <summary>
    /// Two plate solves are compared only when both have a linear part (a CD or a CDELT card) and
    /// the same first axis: a header with neither, one whose first axis is DEC, or a LINEAR one
    /// is no mirror of an RA-first solve (R-017).
    /// </summary>
    [Fact]
    public void R017_Mirrored_ComparesOnlyTwoSolvesWithALinearPartAndTheSameFirstAxis()
    {
        var source = Read(FitsTestData.PlateSolvedCards());

        Assert.True(WcsMetadataKeys.Mirrored(source, Read(FitsTestData.MirroredSolveCards())));
        Assert.False(WcsMetadataKeys.Mirrored(source, Read(FitsTestData.OldSolveCards())));

        List<string> noLinearPart = FitsTestData.ProcessedCards();
        noLinearPart.AddRange([FitsTestData.Card("CTYPE1", "'RA---TAN'"), FitsTestData.Card("CTYPE2", "'DEC--TAN'"), FitsTestData.Card("CRVAL1", "83.6")]);
        Assert.Null(WcsMetadataKeys.Mirrored(source, Read(noLinearPart)));

        List<string> decFirst = FitsTestData.MirroredSolveCards();
        decFirst[decFirst.FindIndex(c => c.StartsWith("CTYPE1", StringComparison.Ordinal))] = FitsTestData.Card("CTYPE1", "'DEC--TAN'");
        decFirst[decFirst.FindIndex(c => c.StartsWith("CTYPE2", StringComparison.Ordinal))] = FitsTestData.Card("CTYPE2", "'RA---TAN'");
        Assert.Null(WcsMetadataKeys.Mirrored(source, Read(decFirst)));

        List<string> linear = FitsTestData.ProcessedCards();
        linear.AddRange([FitsTestData.Card("CTYPE1", "'LINEAR'"), FitsTestData.Card("CDELT1", "1.0"), FitsTestData.Card("CDELT2", "1.0")]);
        Assert.Null(WcsMetadataKeys.Mirrored(source, Read(linear)));
        Assert.Null(WcsMetadataKeys.Mirrored(source, Read(FitsTestData.ProcessedCards())));
    }

    [Fact]
    public void R017_Orientation_ACrotaTurnNeverMirrors()
    {
        List<string> cards = FitsTestData.PlateSolvedCards();
        cards.Add(FitsTestData.Card("CROTA2", "180.0"));

        Assert.Equal(-1, WcsMetadataKeys.Orientation(Read(cards)));
    }

    [Fact]
    public void R017_Orientation_IsUnknownWithoutCtype1_ForAValueThatIsNotANumber_OrADeterminantOf0()
    {
        Assert.Null(WcsMetadataKeys.Orientation(Read(FitsTestData.ProcessedCards())));

        List<string> notANumber = FitsTestData.PlateSolvedCards();
        notANumber[notANumber.FindIndex(c => c.StartsWith("CDELT1", StringComparison.Ordinal))] = FitsTestData.Card("CDELT1", "'x'");
        Assert.Null(WcsMetadataKeys.Orientation(Read(notANumber)));

        List<string> flat = FitsTestData.PlateSolvedCards();
        flat.Add(FitsTestData.Card("CD1_1", "0.0"));
        Assert.Null(WcsMetadataKeys.Orientation(Read(flat)));
    }

    [Fact]
    public void R016_SelectKeysToDrop_ADestinationWithoutAPlateSolve_DropsNothing()
    {
        var source = Read(FitsTestData.PlateSolvedCards());
        var dest = Read(FitsTestData.ProcessedCards());

        Assert.Empty(WcsMetadataKeys.SelectKeysToDrop(source, dest));
    }
}
