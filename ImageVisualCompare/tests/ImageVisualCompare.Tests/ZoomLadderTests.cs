using ImageVisualCompare.Core;

namespace ImageVisualCompare.Tests;

public class ZoomLadderTests
{
    [Theory]
    [InlineData(1.0, 1.5)]
    [InlineData(0.5, 0.67)]
    [InlineData(0.37, 0.5)]   // off-ladder scale, as left by wheel zoom
    [InlineData(0.02, 0.03)]
    public void Next_ReturnsLowestStopAboveTheCurrentScale(double current, double expected)
    {
        Assert.Equal(expected, ZoomLadder.Next(current), 6);
    }

    [Theory]
    [InlineData(1.0, 0.67)]
    [InlineData(0.5, 0.33)]
    [InlineData(0.37, 0.33)]
    [InlineData(16.0, 12.0)]
    public void Previous_ReturnsHighestStopBelowTheCurrentScale(double current, double expected)
    {
        Assert.Equal(expected, ZoomLadder.Previous(current), 6);
    }

    [Fact]
    public void Next_SaturatesAtTheTopOfTheLadder()
    {
        Assert.Equal(16.0, ZoomLadder.Next(16.0), 6);
        Assert.Equal(16.0, ZoomLadder.Next(50.0), 6);
    }

    [Fact]
    public void Previous_SaturatesAtTheBottomOfTheLadder()
    {
        Assert.Equal(0.02, ZoomLadder.Previous(0.02), 6);
        Assert.Equal(0.02, ZoomLadder.Previous(0.001), 6);
    }

    [Fact]
    public void Steps_AreStrictlyIncreasing()
    {
        for (int i = 1; i < ZoomLadder.Steps.Count; i++)
        {
            Assert.True(ZoomLadder.Steps[i] > ZoomLadder.Steps[i - 1]);
        }
    }
}
