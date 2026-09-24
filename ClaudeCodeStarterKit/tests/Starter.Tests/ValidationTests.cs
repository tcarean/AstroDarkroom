using System.Text.Json;
using Starter.Core;
using Xunit;

namespace Starter.Tests;

/// <summary>
/// Reads docs/validation.json and checks every case against the current code. The document
/// and the test cannot drift apart because they read the same file. When a number changes
/// on purpose, the change is made in validation.json with a note in validation.md, never here.
/// </summary>
public class ValidationTests
{
    [Theory]
    [MemberData(nameof(Cases))]
    public void ValidationCase_MatchesRecordedFigures(string id, string fixture, double median, double sigma, double medianTol, double sigmaTol)
    {
        using StreamReader reader = File.OpenText(TestData.Fixture(fixture));
        IReadOnlyList<double> values = NumberFile.Parse(reader);
        Assert.True(Math.Abs(Stats.Median(values) - median) <= medianTol, $"{id}: median {Stats.Median(values)} vs {median}");
        Assert.True(Math.Abs(Stats.RobustSigma(values) - sigma) <= sigmaTol, $"{id}: sigma {Stats.RobustSigma(values)} vs {sigma}");
    }

    public static TheoryData<string, string, double, double, double, double> Cases()
    {
        TheoryData<string, string, double, double, double, double> data = [];
        string path = Path.Combine(AppContext.BaseDirectory, "docs", "validation.json");
        using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (JsonElement c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            JsonElement expected = c.GetProperty("expected");
            JsonElement tolerance = c.GetProperty("tolerance");
            data.Add(
                c.GetProperty("id").GetString()!,
                c.GetProperty("fixture").GetString()!,
                expected.GetProperty("median").GetDouble(),
                expected.GetProperty("robustSigma").GetDouble(),
                tolerance.GetProperty("median").GetDouble(),
                tolerance.GetProperty("robustSigma").GetDouble());
        }

        return data;
    }
}
