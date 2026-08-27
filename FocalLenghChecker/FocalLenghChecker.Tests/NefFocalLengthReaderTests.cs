namespace FocalLenghChecker.Tests;

public class NefFocalLengthReaderTests
{
    private static FocalLengthReadResult ReadBytes(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);

        return new NefFocalLengthReader().Read(stream);
    }

    [Theory]
    [InlineData(18u, 1u, 18.0)]
    [InlineData(180u, 10u, 18.0)]
    [InlineData(105u, 2u, 52.5)]
    [InlineData(2000u, 10u, 200.0)]
    public void A_rational_focal_length_is_read(uint numerator, uint denominator, double expected)
    {
        var result = ReadBytes(TiffBuilder.WithFocalLength(numerator, denominator));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(expected, result.FocalLengthMm!.Value, 6);
        Assert.Null(result.Error);
    }

    [Fact]
    public void A_file_with_no_focal_length_tag_reports_why()
    {
        var result = ReadBytes(TiffBuilder.WithoutFocalLength());

        Assert.False(result.Succeeded);
        Assert.Null(result.FocalLengthMm);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void A_focal_length_with_a_zero_denominator_is_not_accepted()
    {
        var result = ReadBytes(TiffBuilder.WithFocalLength(18, 0));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void A_focal_length_of_zero_is_not_accepted()
    {
        var result = ReadBytes(TiffBuilder.WithFocalLength(0, 1));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void A_truncated_file_fails_instead_of_throwing()
    {
        var complete = TiffBuilder.WithFocalLength(18, 1);
        var truncated = complete.Take(complete.Length / 2).ToArray();

        var result = ReadBytes(truncated);

        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void Bytes_that_are_not_an_image_at_all_fail_instead_of_throwing()
    {
        var result = ReadBytes(new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 });

        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void An_empty_file_fails_instead_of_throwing()
    {
        var result = ReadBytes(Array.Empty<byte>());

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void A_missing_file_fails_instead_of_throwing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"no-such-file-{Guid.NewGuid():N}.NEF");

        var result = new NefFocalLengthReader().Read(path);

        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void A_file_on_disk_is_read_the_same_way_as_a_stream()
    {
        var path = Path.Combine(Path.GetTempPath(), $"focal-{Guid.NewGuid():N}.NEF");

        try
        {
            File.WriteAllBytes(path, TiffBuilder.WithFocalLength(35, 1));

            var result = new NefFocalLengthReader().Read(path);

            Assert.True(result.Succeeded, result.Error);
            Assert.Equal(35.0, result.FocalLengthMm!.Value, 6);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
