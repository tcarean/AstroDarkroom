// SPDX-License-Identifier: GPL-3.0-or-later
//
// Copyright (C) 2026 Tudor Carean
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using System.Text.Json;

namespace CopyWcs.Tests;

/// <summary>
/// The acceptance figures of <c>docs/validation.json</c>, measured on real headers kept as
/// fixtures (ADR 0005): which keys the tool selects from a real Siril plate solve, with and
/// without the optical ones, in which order, and that a dry run of the console lists the same
/// cards (the cases with <c>keysWithOptical</c>); which keys a copy drops from a real
/// destination that holds an old plate solve, and what it copies (the cases with a
/// <c>destination</c>, R-016). In the gate, since the fixtures are in the repo. A figure here
/// changes only after a re-measurement recorded in <c>docs/validation.md</c>.
/// </summary>
public class ValidationTests
{
    private sealed record Case(string Id, string Fixture, int Naxis1, int Naxis2, string[] WithOptical, string[] WcsOnly);

    private sealed record DropCase(string Id, string Fixture, string Destination, int Naxis1, int Naxis2, int KeysCopied, string[] KeysDropped);

    private static JsonElement[] AllCases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "docs", "validation.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.GetProperty("cases").EnumerateArray().Select(c => c.Clone()).ToArray();
    }

    private static IEnumerable<DropCase> DropCases()
    {
        foreach (var c in AllCases().Where(c => c.TryGetProperty("destination", out _)))
        {
            var expected = c.GetProperty("expected");
            yield return new DropCase(
                c.GetProperty("id").GetString()!,
                c.GetProperty("fixture").GetString()!,
                c.GetProperty("destination").GetString()!,
                expected.GetProperty("naxis1").GetInt32(),
                expected.GetProperty("naxis2").GetInt32(),
                expected.GetProperty("keysCopied").GetInt32(),
                expected.GetProperty("keysDropped").EnumerateArray().Select(k => k.GetString()!).ToArray());
        }
    }

    public static IEnumerable<object[]> DropCaseIds() => DropCases().Select(c => new object[] { c.Id });

    [Theory]
    [MemberData(nameof(DropCaseIds))]
    public void R016_TheKeysDroppedFromTheRealPair_AreTheOnesRecorded(string id)
    {
        var c = DropCases().Single(x => x.Id == id);
        var source = FitsHeader.ReadPrimary(TestData.Fixture(c.Fixture));
        var dest = FitsHeader.ReadPrimary(TestData.Fixture(c.Destination));

        Assert.Equal(c.Naxis1, dest.GetInt("NAXIS1"));
        Assert.Equal(c.Naxis2, dest.GetInt("NAXIS2"));
        Assert.Equal(c.KeysDropped, WcsMetadataKeys.SelectKeysToDrop(source, dest));
    }

    /// <summary>A copy of the real pair into a new file copies the recorded count of keys and leaves none of the dropped ones.</summary>
    [Theory]
    [MemberData(nameof(DropCaseIds))]
    public void R016_ACopyOfTheRealPair_CopiesTheRecordedCountAndLeavesNoDroppedKey(string id)
    {
        var c = DropCases().Single(x => x.Id == id);
        using var ws = new TempWorkspace();
        var output = ws.FilePath("out.fit");

        var outcome = WcsCopy.Run(new WcsCopyRequest(TestData.Fixture(c.Fixture), TestData.Fixture(c.Destination), output,
            IncludeOptical: true, Force: false, DryRun: false));

        Assert.Equal(WcsCopyKind.Copied, outcome.Kind);
        Assert.Equal(c.KeysCopied, outcome.Cards.Count);
        Assert.NotNull(outcome.Dropped);
        var written = FitsHeader.ReadPrimary(output);
        Assert.All(c.KeysDropped, k => Assert.False(written.Contains(k), k));
    }

    private static IEnumerable<Case> Cases()
    {
        foreach (var c in AllCases().Where(c => c.GetProperty("expected").TryGetProperty("keysWithOptical", out _)))
        {
            var expected = c.GetProperty("expected");
            yield return new Case(
                c.GetProperty("id").GetString()!,
                c.GetProperty("fixture").GetString()!,
                expected.GetProperty("naxis1").GetInt32(),
                expected.GetProperty("naxis2").GetInt32(),
                expected.GetProperty("keysWithOptical").EnumerateArray().Select(k => k.GetString()!).ToArray(),
                expected.GetProperty("keysWcsOnly").EnumerateArray().Select(k => k.GetString()!).ToArray());
        }
    }

    public static IEnumerable<object[]> CaseIds() => Cases().Select(c => new object[] { c.Id });

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void R024_TheKeysSelectedFromTheRealSolve_AreTheOnesRecorded(string id)
    {
        var c = Cases().Single(x => x.Id == id);
        var header = FitsHeader.ReadPrimary(TestData.Fixture(c.Fixture));

        Assert.Equal(c.Naxis1, header.GetInt("NAXIS1"));
        Assert.Equal(c.Naxis2, header.GetInt("NAXIS2"));
        Assert.Equal(c.WithOptical, WcsMetadataKeys.SelectKeysToCopy(header, includeOptical: true));
        Assert.Equal(c.WcsOnly, WcsMetadataKeys.SelectKeysToCopy(header, includeOptical: false));
    }

    /// <summary>The console's dry run on the fixture lists the recorded cards, in order, and the same count.</summary>
    [Theory]
    [MemberData(nameof(CaseIds))]
    public void R024_TheConsoleDryRunOnTheRealSolve_ListsTheRecordedCards(string id)
    {
        var c = Cases().Single(x => x.Id == id);
        using var ws = new TempWorkspace();
        var dest = ws.WriteFile("dest.fit", FitsTestData.MinimalFitsFile(FitsTestData.ProcessedCards(c.Naxis1, c.Naxis2)));
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var code = CopyWcsCli.Run([TestData.Fixture(c.Fixture), dest, "--dry-run", "--lang", "en"], stdout, stderr);

        Assert.Equal(0, code);
        Assert.Equal("", stderr.ToString());
        var lines = stdout.ToString().Replace("\r\n", "\n").Split('\n');
        Assert.Contains($"Keys that would be copied ({c.WithOptical.Length}):", lines);
        var listed = lines.Where(l => l.StartsWith("  ", StringComparison.Ordinal))
            .Select(l => l.Length >= 10 ? l[2..10].Trim() : l.Trim())
            .ToArray();
        Assert.Equal(c.WithOptical, listed);
    }
}
