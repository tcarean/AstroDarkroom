namespace FlatCheck.Tests;

/// <summary>
/// Phase 4: the raster type itself and <see cref="Img.Divide"/>.
/// The block-mean and downsample transforms live in <see cref="PreprocessingTests"/>.
/// </summary>
public class ImgTests
{
    // ---------------------------------------------------------------- the raster

    /// <summary>
    /// The constructor takes (height, width) in that order, while almost everything else
    /// in the codebase talks about images as w x h. Easy to transpose by accident.
    /// </summary>
    [Fact]
    public void Constructor_TakesHeightFirstThenWidth()
    {
        var img = new Img(3, 7);

        Assert.Equal(3, img.H);
        Assert.Equal(7, img.W);
        Assert.Equal(21, img.D.Length);
    }

    [Fact]
    public void AtAndSet_AreRowMajor()
    {
        var img = new Img(3, 4);

        img.Set(2, 1, 99);

        Assert.Equal(99, img.At(2, 1), 9);
        Assert.Equal(99, img.D[2 * 4 + 1], 9);
    }

    [Fact]
    public void NewRaster_StartsZeroed()
    {
        var img = new Img(2, 2);

        Assert.All(img.D, v => Assert.Equal(0, v, 9));
    }

    // ---------------------------------------------------------------- Divide

    [Fact]
    public void Divide_DividesElementwise()
    {
        var a = Filled(2, 2, 10);
        var b = Filled(2, 2, 4);

        var result = Img.Divide(a, b);

        Assert.Equal(2.5, result.At(0, 0), 9);
        Assert.Equal(2.5, result.At(1, 1), 9);
    }

    [Fact]
    public void Divide_KeepsTheShapeOfTheNumerator()
    {
        var result = Img.Divide(new Img(3, 5), new Img(3, 5));

        Assert.Equal(3, result.H);
        Assert.Equal(5, result.W);
    }

    /// <summary>
    /// The denominator is floored at 1e-12 (mirroring numpy's np.maximum(model, 1e-12)),
    /// so a zero divisor yields a huge finite number rather than an infinity.
    /// </summary>
    [Fact]
    public void Divide_FloorsAZeroDenominator_SoTheResultStaysFinite()
    {
        var a = Filled(1, 1, 1);
        var b = Filled(1, 1, 0);

        double v = Img.Divide(a, b).At(0, 0);

        Assert.Equal(1e12, v, 0);
        Assert.False(double.IsInfinity(v));
    }

    /// <summary>
    /// The floor is a max, not an abs: a negative denominator is clamped up to 1e-12, so
    /// the quotient comes out positive and huge. Same as numpy - preserved for parity.
    /// </summary>
    [Fact]
    public void Divide_ClampsANegativeDenominatorUpToTheFloor()
    {
        var a = Filled(1, 1, 1);
        var b = Filled(1, 1, -5);

        double v = Img.Divide(a, b).At(0, 0);

        Assert.True(v > 0);
        Assert.Equal(1e12, v, 0);
    }

    [Fact]
    public void Divide_OfAFrameByItself_IsAllOnes()
    {
        var a = new Img(4, 4);
        for (int i = 0; i < a.D.Length; i++) a.D[i] = i + 1;

        var result = Img.Divide(a, a);

        Assert.All(result.D, v => Assert.Equal(1.0, v, 9));
    }

    static Img Filled(int h, int w, double v)
    {
        var img = new Img(h, w);
        Array.Fill(img.D, v);
        return img;
    }
}
