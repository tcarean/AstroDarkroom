namespace FocalLenghChecker.Tests;

public class RangeAnalyzerTests
{
    private const double Crop = 1.5;

    private static readonly IReadOnlyList<FocalLengthRange> Ranges = FocalLengthRanges.Default;

    private static FocalLengthTally TallyOf(params double[] focalLengths)
    {
        var tally = new FocalLengthTally();

        foreach (var focalLength in focalLengths)
            tally.Add(focalLength);

        return tally;
    }

    private static AnalysisResult AnalyzeDx(params double[] dxFocalLengths) =>
        RangeAnalyzer.Analyze(TallyOf(dxFocalLengths), sourceIsFullFrame: false, Ranges, Crop);

    private static AnalysisResult AnalyzeFf(params double[] ffFocalLengths) =>
        RangeAnalyzer.Analyze(TallyOf(ffFocalLengths), sourceIsFullFrame: true, Ranges, Crop);

    [Fact]
    public void An_empty_tally_produces_every_range_at_zero()
    {
        var result = AnalyzeDx();

        Assert.Equal(4, result.Ranges.Count);
        Assert.All(result.Ranges, r => Assert.Equal(0, r.Count));
        Assert.All(result.Ranges, r => Assert.Equal(0, r.Percentage));
        Assert.Equal(0, result.OutsideRangesCount);
        Assert.Equal(0, result.FilesWithFocalLength);
        Assert.Empty(result.FocalLengths);
    }

    [Fact]
    public void The_ranges_come_back_in_the_configured_order()
    {
        Assert.Equal(Ranges, AnalyzeDx(18).Ranges.Select(r => r.Range));
    }

    [Fact]
    public void DX_focal_lengths_are_bucketed_by_their_full_frame_equivalent()
    {
        // 12 -> FF 18 (16-23), 18 -> FF 27 (24-27), 35 -> FF 52.5 (28-70), 100 -> FF 150 (71-200)
        var result = AnalyzeDx(12, 18, 35, 100);

        Assert.Equal(new[] { 1, 1, 1, 1 }, result.Ranges.Select(r => r.Count));
        Assert.Equal(0, result.OutsideRangesCount);
    }

    [Fact]
    public void Full_frame_focal_lengths_are_bucketed_as_given()
    {
        var result = AnalyzeFf(18, 27, 52.5, 150);

        Assert.Equal(new[] { 1, 1, 1, 1 }, result.Ranges.Select(r => r.Count));
        Assert.Equal(0, result.OutsideRangesCount);
    }

    [Fact]
    public void The_same_number_lands_in_different_ranges_depending_on_the_ff_switch()
    {
        // 18 mm is FF 27 when read as DX, but stays 18 when read as full-frame.
        Assert.Equal(1, AnalyzeDx(18).Ranges[1].Count);
        Assert.Equal(1, AnalyzeFf(18).Ranges[0].Count);
    }

    [Fact]
    public void Everything_in_one_range_is_a_hundred_percent()
    {
        var result = AnalyzeDx(18, 18, 18, 18);

        Assert.Equal(4, result.Ranges[1].Count);
        Assert.Equal(100, result.Ranges[1].Percentage);
        Assert.Equal(0, result.Ranges[0].Percentage);
    }

    [Fact]
    public void Percentages_are_shares_of_the_files_that_had_a_focal_length()
    {
        var result = AnalyzeDx(12, 18, 18, 18);

        Assert.Equal(25, result.Ranges[0].Percentage);
        Assert.Equal(75, result.Ranges[1].Percentage);
    }

    [Fact]
    public void Every_percentage_including_the_outside_bucket_sums_to_a_hundred()
    {
        var result = RangeAnalyzer.Analyze(
            TallyOf(8, 12, 18, 35, 100, 300), sourceIsFullFrame: false, Ranges, Crop);

        var total = result.Ranges.Sum(r => r.Percentage) + result.OutsideRangesPercentage;

        Assert.Equal(100, total, 10);
    }

    [Fact]
    public void Focal_lengths_below_the_first_range_fall_outside()
    {
        // 10 mm DX is FF 15, just under the 16 mm floor.
        var result = AnalyzeDx(10);

        Assert.Equal(1, result.OutsideRangesCount);
        Assert.Equal(100, result.OutsideRangesPercentage);
        Assert.All(result.Ranges, r => Assert.Equal(0, r.Count));
    }

    [Fact]
    public void Focal_lengths_above_the_last_range_fall_outside()
    {
        // 300 mm DX is FF 450, past the 200 mm ceiling.
        var result = AnalyzeDx(300);

        Assert.Equal(1, result.OutsideRangesCount);
        Assert.All(result.Ranges, r => Assert.Equal(0, r.Count));
    }

    [Fact]
    public void A_focal_length_falling_between_two_written_ranges_is_still_counted()
    {
        // 15.5 mm DX is FF 23.25, in the gap between the ranges written as 16-23 and 24-27.
        var result = AnalyzeDx(15.5);

        Assert.Equal(0, result.OutsideRangesCount);
        Assert.Equal(1, result.Ranges[0].Count);
    }

    [Fact]
    public void No_focal_length_is_ever_counted_twice()
    {
        var result = RangeAnalyzer.Analyze(
            TallyOf(8, 10, 12, 15.5, 18, 20, 35, 47, 100, 133, 300),
            sourceIsFullFrame: false, Ranges, Crop);

        Assert.Equal(
            result.FilesWithFocalLength,
            result.Ranges.Sum(r => r.Count) + result.OutsideRangesCount);
    }

    [Fact]
    public void Each_distinct_focal_length_is_reported_with_both_its_DX_and_FF_value()
    {
        var result = AnalyzeDx(18, 18, 35);

        var eighteen = result.FocalLengths.Single(f => f.DxMm == 18);

        Assert.Equal(27, eighteen.FfMm, 10);
        Assert.Equal(2, eighteen.Count);

        var thirtyFive = result.FocalLengths.Single(f => f.DxMm == 35);

        Assert.Equal(52.5, thirtyFive.FfMm, 10);
        Assert.Equal(1, thirtyFive.Count);
    }

    [Fact]
    public void Full_frame_input_reports_the_derived_DX_value()
    {
        var focalLength = Assert.Single(AnalyzeFf(27).FocalLengths);

        Assert.Equal(27, focalLength.FfMm, 10);
        Assert.Equal(18, focalLength.DxMm, 10);
    }

    [Fact]
    public void The_most_used_focal_length_is_listed_first()
    {
        var result = AnalyzeDx(35, 18, 18, 18, 50, 50);

        Assert.Equal(new[] { 18.0, 50.0, 35.0 }, result.FocalLengths.Select(f => f.DxMm));
    }

    [Fact]
    public void Focal_lengths_used_equally_often_are_listed_shortest_first()
    {
        var result = AnalyzeDx(50, 18, 35);

        Assert.Equal(new[] { 18.0, 35.0, 50.0 }, result.FocalLengths.Select(f => f.DxMm));
    }

    [Fact]
    public void Unreadable_files_are_carried_through_but_kept_out_of_the_percentages()
    {
        var result = RangeAnalyzer.Analyze(
            TallyOf(18, 18, 18, 18), sourceIsFullFrame: false, Ranges, Crop, filesWithoutFocalLength: 6);

        Assert.Equal(6, result.FilesWithoutFocalLength);
        Assert.Equal(4, result.FilesWithFocalLength);
        Assert.Equal(10, result.FilesScanned);
        Assert.Equal(100, result.Ranges[1].Percentage);
    }

    [Fact]
    public void The_switch_and_crop_factor_used_are_reported_back()
    {
        var result = RangeAnalyzer.Analyze(TallyOf(18), sourceIsFullFrame: true, Ranges, 1.6);

        Assert.True(result.SourceIsFullFrame);
        Assert.Equal(1.6, result.CropFactor);
    }

    [Fact]
    public void Custom_ranges_are_honoured()
    {
        var custom = new[] { new FocalLengthRange(10, 20), new FocalLengthRange(21, 40) };

        // 20.9 still belongs to the range written as 10-20, whose exclusive bound is 21.
        var result = RangeAnalyzer.Analyze(TallyOf(10, 20, 20.9, 21, 40), sourceIsFullFrame: true, custom, Crop);

        Assert.Equal(3, result.Ranges[0].Count);
        Assert.Equal(2, result.Ranges[1].Count);
        Assert.Equal(0, result.OutsideRangesCount);
    }

    [Fact]
    public void Invalid_ranges_are_rejected_before_anything_is_counted()
    {
        var overlapping = new[] { new FocalLengthRange(16, 30), new FocalLengthRange(28, 70) };

        Assert.Throws<ArgumentException>(
            () => RangeAnalyzer.Analyze(TallyOf(18), false, overlapping, Crop));
    }

    [Fact]
    public void A_negative_unreadable_count_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RangeAnalyzer.Analyze(TallyOf(18), false, Ranges, Crop, filesWithoutFocalLength: -1));
    }

    [Fact]
    public void A_null_tally_is_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => RangeAnalyzer.Analyze(null!, false, Ranges, Crop));
    }
}
