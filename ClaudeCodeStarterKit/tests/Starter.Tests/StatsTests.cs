using Starter.Core;
using Xunit;

namespace Starter.Tests;

/// <summary>Unit tests. The name of each test carries the requirement ID it covers.</summary>
public class StatsTests
{
    [Fact]
    public void R001_Median_OddCount_ReturnsMiddleValue()
    {
        Assert.Equal(3.0, Stats.Median([5, 1, 3]));
    }

    [Fact]
    public void R001_Median_EvenCount_ReturnsMeanOfMiddleTwo()
    {
        Assert.Equal(2.5, Stats.Median([4, 1, 3, 2]));
    }

    [Fact]
    public void R001_Median_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() => Stats.Median([]));
    }

    [Fact]
    public void R002_RobustSigma_SymmetricSample_MatchesHandCalculation()
    {
        // deviations from median 5.5 are 4.5,3.5,2.5,1.5,0.5 twice; their median is 2.5
        double[] values = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        Assert.Equal(1.4826 * 2.5, Stats.RobustSigma(values), precision: 10);
    }

    [Fact]
    public void R003_Parse_SkipsBlankLinesAndComments()
    {
        using StringReader reader = new("# header\n\n1\n 2 \n#3\n4\n");
        Assert.Equal([1, 2, 4], NumberFile.Parse(reader));
    }

    [Fact]
    public void R003_Parse_BadLine_ReportsLineNumber()
    {
        using StringReader reader = new("1\nx\n");
        FormatException ex = Assert.Throws<FormatException>(() => NumberFile.Parse(reader));
        Assert.Contains("Line 2", ex.Message, StringComparison.Ordinal);
    }
}
