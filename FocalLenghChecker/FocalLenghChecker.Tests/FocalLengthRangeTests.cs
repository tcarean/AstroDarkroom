namespace FocalLenghChecker.Tests;

public class FocalLengthRangeTests
{
    [Theory]
    [InlineData(15.9, false)]   // just below the range
    [InlineData(16.0, true)]    // lower bound is inclusive
    [InlineData(19.5, true)]
    [InlineData(23.0, true)]    // upper bound as written is inclusive
    [InlineData(23.25, true)]   // a 15.5 mm DX lens; would fall in the 23-24 gap
    [InlineData(23.99, true)]
    [InlineData(24.0, false)]   // Max + 1 belongs to the next range
    public void Contains_treats_the_written_upper_bound_as_inclusive_of_the_whole_millimetre(
        double ff, bool expected)
    {
        Assert.Equal(expected, new FocalLengthRange(16, 23).Contains(ff));
    }

    [Fact]
    public void Adjacent_ranges_never_both_match_and_never_leave_a_gap()
    {
        var lower = new FocalLengthRange(16, 23);
        var upper = new FocalLengthRange(24, 27);

        for (var ff = 16.0; ff < 28.0; ff += 0.05)
        {
            var matches = (lower.Contains(ff) ? 1 : 0) + (upper.Contains(ff) ? 1 : 0);
            Assert.True(matches == 1, $"{ff:0.00} mm matched {matches} ranges.");
        }
    }

    [Fact]
    public void A_single_millimetre_range_matches_only_that_millimetre()
    {
        var range = new FocalLengthRange(50, 50);

        Assert.False(range.Contains(49.9));
        Assert.True(range.Contains(50.0));
        Assert.True(range.Contains(50.9));
        Assert.False(range.Contains(51.0));
    }

    [Fact]
    public void FfLabel_shows_the_range_as_written()
    {
        Assert.Equal("28 - 70 mm", new FocalLengthRange(28, 70).FfLabel);
    }

    [Theory]
    [InlineData(16, 23, "10.7 - 15.3 mm")]
    [InlineData(24, 27, "16.0 - 18.0 mm")]
    [InlineData(28, 70, "18.7 - 46.7 mm")]
    [InlineData(71, 200, "47.3 - 133.3 mm")]
    public void DxLabel_divides_the_range_by_the_crop_factor(int min, int max, string expected)
    {
        Assert.Equal(expected, new FocalLengthRange(min, max).DxLabel(1.5));
    }

    [Fact]
    public void DxLabel_follows_a_changed_crop_factor()
    {
        Assert.Equal("10.0 - 12.5 mm", new FocalLengthRange(16, 20).DxLabel(1.6));
    }
}
