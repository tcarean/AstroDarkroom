namespace FocalLenghChecker.Tests;

public class FocalLengthTallyTests
{
    [Fact]
    public void A_new_tally_is_empty()
    {
        var tally = new FocalLengthTally();

        Assert.Equal(0, tally.Total);
        Assert.Empty(tally.Counts);
    }

    [Fact]
    public void Repeats_of_one_focal_length_are_counted_together()
    {
        var tally = new FocalLengthTally();

        tally.Add(18);
        tally.Add(18);
        tally.Add(18);

        Assert.Equal(3, tally.Total);
        Assert.Equal(3, Assert.Single(tally.Counts).Value);
    }

    [Fact]
    public void Different_focal_lengths_are_counted_separately()
    {
        var tally = new FocalLengthTally();

        tally.Add(18);
        tally.Add(35);
        tally.Add(18);

        Assert.Equal(3, tally.Total);
        Assert.Equal(2, tally.Counts[18.0]);
        Assert.Equal(1, tally.Counts[35.0]);
    }

    [Fact]
    public void Values_that_differ_only_below_the_rounding_precision_land_in_one_bucket()
    {
        var tally = new FocalLengthTally();

        tally.Add(18.0);
        tally.Add(17.999999);
        tally.Add(18.04);

        Assert.Equal(3, Assert.Single(tally.Counts).Value);
        Assert.Equal(18.0, tally.Counts.Keys.Single());
    }

    [Fact]
    public void Values_that_differ_above_the_rounding_precision_stay_apart()
    {
        var tally = new FocalLengthTally();

        tally.Add(18.0);
        tally.Add(18.2);

        Assert.Equal(2, tally.Counts.Count);
    }

    [Fact]
    public void Focal_lengths_are_kept_in_ascending_order()
    {
        var tally = new FocalLengthTally();

        tally.Add(200);
        tally.Add(18);
        tally.Add(50);

        Assert.Equal(new[] { 18.0, 50.0, 200.0 }, tally.Counts.Keys);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-18)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void A_focal_length_that_cannot_be_real_is_rejected(double focalLength)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FocalLengthTally().Add(focalLength));
    }
}
