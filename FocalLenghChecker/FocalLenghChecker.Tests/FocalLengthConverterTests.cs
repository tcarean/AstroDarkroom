namespace FocalLenghChecker.Tests;

public class FocalLengthConverterTests
{
    private const double Crop = 1.5;

    [Theory]
    [InlineData(18, 27)]
    [InlineData(35, 52.5)]
    [InlineData(10.5, 15.75)]
    [InlineData(200, 300)]
    public void A_DX_focal_length_is_multiplied_by_the_crop_factor(double dx, double expectedFf)
    {
        Assert.Equal(expectedFf, FocalLengthConverter.ToFullFrame(dx, sourceIsFullFrame: false, Crop), 10);
        Assert.Equal(dx, FocalLengthConverter.ToDx(dx, sourceIsFullFrame: false, Crop), 10);
    }

    [Theory]
    [InlineData(27, 18)]
    [InlineData(52.5, 35)]
    [InlineData(24, 16)]
    public void A_full_frame_focal_length_is_divided_by_the_crop_factor_for_DX(double ff, double expectedDx)
    {
        Assert.Equal(ff, FocalLengthConverter.ToFullFrame(ff, sourceIsFullFrame: true, Crop), 10);
        Assert.Equal(expectedDx, FocalLengthConverter.ToDx(ff, sourceIsFullFrame: true, Crop), 10);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_pair_of_conversions_always_differs_by_the_crop_factor(bool sourceIsFullFrame)
    {
        var ff = FocalLengthConverter.ToFullFrame(40, sourceIsFullFrame, Crop);
        var dx = FocalLengthConverter.ToDx(40, sourceIsFullFrame, Crop);

        Assert.Equal(Crop, ff / dx, 10);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void An_unusable_crop_factor_is_rejected(double cropFactor)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FocalLengthConverter.ToFullFrame(18, false, cropFactor));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => FocalLengthConverter.ToDx(18, true, cropFactor));
    }
}
