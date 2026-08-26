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
    public void SelectKeysToCopy_StandardWcsKeyPresent_Included()
    {
        var header = Header(FitsTestData.Card("CTYPE1", "'RA---TAN'"));

        var selected = WcsMetadataKeys.SelectKeysToCopy(header, includeOptical: false);

        Assert.Contains("CTYPE1", selected);
    }

    [Fact]
    public void SelectKeysToCopy_StandardWcsKeyAbsent_Excluded()
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
    public void SelectKeysToCopy_SipCoefficientPresent_MatchedAndIncluded(string sipKey)
    {
        var header = Header(FitsTestData.Card(sipKey, "1.0e-05"));

        var selected = WcsMetadataKeys.SelectKeysToCopy(header, includeOptical: false);

        Assert.Contains(sipKey, selected);
    }

    [Fact]
    public void SelectKeysToCopy_StandardMatrixKeyIncluded_ViaLiteralListNotSipRegex()
    {
        // CD1_1 e o cheie WCS standard, inclusa prin WcsKeys - regex-ul SIP nu ar trebui
        // sa o mai adauge o a doua oara.
        var header = Header(FitsTestData.Card("CD1_1", "1.0"));

        var selected = WcsMetadataKeys.SelectKeysToCopy(header, includeOptical: false);

        Assert.Equal(1, selected.Count(k => k == "CD1_1"));
    }

    [Fact]
    public void SelectKeysToCopy_UnrelatedKeyResemblingSipPattern_NotMatched()
    {
        // "ABC_1_2" nu incepe cu unul din prefixele SIP (A, B, AP, BP urmate direct de "_").
        var header = Header(FitsTestData.Card("ABC_1_2", "1.0"));

        var selected = WcsMetadataKeys.SelectKeysToCopy(header, includeOptical: false);

        Assert.DoesNotContain("ABC_1_2", selected);
    }

    [Fact]
    public void SelectKeysToCopy_OpticalKeyIncludedOnlyWhenIncludeOpticalTrue()
    {
        var header = Header(FitsTestData.Card("TELESCOP", "'My Scope'"));

        var withOptical = WcsMetadataKeys.SelectKeysToCopy(header, includeOptical: true);
        var withoutOptical = WcsMetadataKeys.SelectKeysToCopy(header, includeOptical: false);

        Assert.Contains("TELESCOP", withOptical);
        Assert.DoesNotContain("TELESCOP", withoutOptical);
    }

    [Fact]
    public void SelectKeysToCopy_NoRelevantKeysInSource_ReturnsEmptyList()
    {
        var header = Header(FitsTestData.Card("UNRELATED", "'x'"));

        var selected = WcsMetadataKeys.SelectKeysToCopy(header, includeOptical: true);

        Assert.Empty(selected);
    }

    [Fact]
    public void SelectKeysToCopy_ResultHasNoDuplicates()
    {
        var header = Header(
            FitsTestData.Card("CTYPE1", "'RA---TAN'"),
            FitsTestData.Card("TELESCOP", "'My Scope'"));

        var selected = WcsMetadataKeys.SelectKeysToCopy(header, includeOptical: true);

        Assert.Equal(selected.Count, selected.Distinct().Count());
    }
}
