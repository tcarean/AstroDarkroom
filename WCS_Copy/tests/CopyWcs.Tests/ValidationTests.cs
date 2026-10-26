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
/// The acceptance figures of <c>docs/validation.json</c>, measured on the header of a real
/// Siril plate solve kept as a fixture (ADR 0005): which keys the tool selects from it, with
/// and without the optical ones, in which order, and that a dry run of the console lists the
/// same cards. In the gate, since the fixture is in the repo. A figure here changes only after
/// a re-measurement recorded in <c>docs/validation.md</c>.
/// </summary>
public class ValidationTests
{
    private sealed record Case(string Id, string Fixture, int Naxis1, int Naxis2, string[] WithOptical, string[] WcsOnly);

    private static IEnumerable<Case> Cases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "docs", "validation.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var c in doc.RootElement.GetProperty("cases").EnumerateArray())
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
