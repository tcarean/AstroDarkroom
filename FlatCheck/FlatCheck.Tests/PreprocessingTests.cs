namespace FlatCheck.Tests;

/// <summary>
/// The 2x2 + downsample front end. This exists because the golden-output tests cannot
/// see it: at the frame sizes those use, the second BlockMean is a no-op (k == 1), so a
/// change to the downsample arithmetic slips straight past them. Verified by mutation -
/// changing targetLong from 1200 to 600 left all the golden tests green.
/// </summary>
public class PreprocessingTests
{
    static Img Make(int w, int h, Func<int, int, double> f) => TestImages.From(w, h, f);


    // ---------------------------------------------------------------- BlockMean

    [Fact]
    public void BlockMean_WithKOfOne_ReturnsTheInputUnchanged()
    {
        var img = Make(4, 4, (x, y) => x + y);

        var result = Img.BlockMean(img, 1);

        Assert.Same(img, result);
    }

    [Fact]
    public void BlockMean_AveragesEachBlock()
    {
        // 4x4 where the value is the column index: each 2x2 block averages its two columns
        var img = Make(4, 4, (x, y) => x);

        var result = Img.BlockMean(img, 2);

        Assert.Equal(2, result.W);
        Assert.Equal(2, result.H);
        Assert.Equal(0.5, result.At(0, 0), 9);   // columns 0,1
        Assert.Equal(2.5, result.At(0, 1), 9);   // columns 2,3
    }

    [Fact]
    public void BlockMean_TruncatesPartialBlocks()
    {
        var img = Make(5, 5, (x, y) => 1);

        var result = Img.BlockMean(img, 2);

        Assert.Equal(2, result.W);   // the 5th column is dropped, not padded
        Assert.Equal(2, result.H);
    }

    /// <summary>
    /// The reason the tool can run straight on an undebayered CFA master: every 2x2 RGGB
    /// block holds exactly 1 R, 2 G, 1 B, so the block mean is a fixed linear combination
    /// and the underlying field survives intact.
    /// </summary>
    [Fact]
    public void BlockMean_Of2x2_FlattensABayerMosaicToItsMeanGain()
    {
        var mosaic = FitsTestData.WithBayerMosaic(FitsTestData.Constant(1000), rGain: 0.6, bGain: 0.4);
        var img = Make(8, 8, mosaic);

        var result = Img.BlockMean(img, 2);

        double expected = 1000 * (0.6 + 1.0 + 1.0 + 0.4) / 4;
        for (int y = 0; y < result.H; y++)
            for (int x = 0; x < result.W; x++)
                Assert.Equal(expected, result.At(y, x), 9);
    }

    // ---------------------------------------------------------------- Prepare

    [Fact]
    public void Prepare_AlwaysAppliesTheTwoByTwoBlockMean()
    {
        var img = Make(512, 512, (x, y) => 100);

        var result = Img.Prepare(img);

        Assert.Equal(256, result.W);
        Assert.Equal(256, result.H);
    }

    /// <summary>
    /// The ~1200 px target is loose, because k is truncated to an int. A 3000x2000 frame
    /// becomes 1500x1000 - not 1200. This matches flatcheck.py exactly, so it is
    /// parity-correct; the test exists so nobody "fixes" it into a parity break.
    /// </summary>
    [Fact]
    public void Prepare_LeavesFramesBetween1200And2400Alone()
    {
        var img = Make(3000, 2000, (x, y) => 100);

        var result = Img.Prepare(img);

        Assert.Equal(1500, result.W);
        Assert.Equal(1000, result.H);
    }

    /// <summary>
    /// The k > 1 path, which the golden tests never reach. A wide, thin frame exercises
    /// it without allocating a full 24 MP raster.
    /// </summary>
    [Fact]
    public void Prepare_DownsamplesWhenTheLongSideExceedsTwiceTheTarget()
    {
        var img = Make(4800, 128, (x, y) => 100);

        var result = Img.Prepare(img);

        Assert.Equal(1200, result.W);   // 4800 -> 2400 (2x2) -> 1200 (k = 2)
        Assert.Equal(32, result.H);
    }

    [Fact]
    public void Prepare_HonoursAnExplicitTarget()
    {
        var img = Make(4800, 128, (x, y) => 100);

        var result = Img.Prepare(img, targetLong: 600);

        Assert.Equal(600, result.W);    // 2400 / 4
        Assert.Equal(16, result.H);
    }

    [Fact]
    public void Prepare_PreservesTheMeanLevel()
    {
        var img = Make(4800, 128, (x, y) => 1234.5);

        var result = Img.Prepare(img);

        Assert.Equal(1234.5, result.At(result.H / 2, result.W / 2), 9);
    }
}
