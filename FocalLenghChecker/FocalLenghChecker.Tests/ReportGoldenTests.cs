namespace FocalLenghChecker.Tests;

/// <summary>
/// A whole-report comparison, so that a change to the layout has to be a
/// deliberate one. The scenario: five files read (three at 18 mm, one at 35 mm,
/// one at 10 mm which is too wide for any range) plus one file that could not
/// be read at all.
/// </summary>
public class ReportGoldenTests
{
    private const string Expected = """
        Scanned 6 .NEF files in E:\Photos
        Recorded focal lengths treated as DX; full-frame equivalent is 1.5x the DX value.

        Range (FF)       Range (DX)        Count        %
        16 - 23 mm       10.7 - 15.3 mm        0     0.0%
        24 - 27 mm       16.0 - 18.0 mm        3    60.0%
        28 - 70 mm       18.7 - 46.7 mm        1    20.0%
        71 - 200 mm      47.3 - 133.3 mm       0     0.0%
        Outside ranges                         1    20.0%
                                           -----   ------
        Total                                  5   100.0%

        Individual focal lengths
          DX 18.0 mm  (FF 27.0 mm)   3   60.0%
          DX 10.0 mm  (FF 15.0 mm)   1   20.0%
          DX 35.0 mm  (FF 52.5 mm)   1   20.0%

        1 file had no readable focal length.
        """;

    [Fact]
    public void The_whole_report_reads_exactly_as_intended()
    {
        var tally = new FocalLengthTally();

        foreach (var millimetres in new[] { 18.0, 18.0, 18.0, 35.0, 10.0 })
            tally.Add(millimetres);

        var result = RangeAnalyzer.Analyze(
            tally, sourceIsFullFrame: false, FocalLengthRanges.Default, 1.5, filesWithoutFocalLength: 1);

        var report = ReportFormatter.Format(result, @"E:\Photos");

        Assert.Equal(Expected.ReplaceLineEndings("\n") + "\n", report);
    }
}
