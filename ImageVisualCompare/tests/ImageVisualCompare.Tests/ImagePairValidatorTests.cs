using ImageVisualCompare.Core;

namespace ImageVisualCompare.Tests;

public class ImagePairValidatorTests
{
    private static ImageInfo Info(string path, int width, int height) =>
        new(path, width, height, 16, ColorChannels.Rgb);

    [Fact]
    public void MatchingDimensionsPass()
    {
        ValidationResult result = ImagePairValidator.Validate(
            Info(@"C:\before.png", 5794, 3637),
            Info(@"C:\after.tif", 5794, 3637));

        Assert.True(result.IsValid);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData(5794, 3637, 6000, 3637)]
    [InlineData(5794, 3637, 5794, 4000)]
    [InlineData(5794, 3637, 3637, 5794)]
    public void MismatchedDimensionsFail(int widthA, int heightA, int widthB, int heightB)
    {
        ValidationResult result = ImagePairValidator.Validate(
            Info(@"C:\before.png", widthA, heightA),
            Info(@"C:\after.png", widthB, heightB));

        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void TheFailureMessageNamesBothFilesAndBothResolutions()
    {
        ValidationResult result = ImagePairValidator.Validate(
            Info(@"C:\astro\before.png", 5794, 3637),
            Info(@"C:\astro\after.png", 6000, 4000));

        Assert.NotNull(result.Error);
        Assert.Contains("before.png", result.Error);
        Assert.Contains("after.png", result.Error);
        Assert.Contains("5794 \u00d7 3637", result.Error);
        Assert.Contains("6000 \u00d7 4000", result.Error);
    }

    [Fact]
    public void BitDepthAndChannelLayoutDoNotHaveToMatch()
    {
        ValidationResult result = ImagePairValidator.Validate(
            new ImageInfo(@"C:\a.png", 100, 100, 16, ColorChannels.Rgb),
            new ImageInfo(@"C:\b.png", 100, 100, 8, ColorChannels.Rgba));

        Assert.True(result.IsValid);
    }
}
