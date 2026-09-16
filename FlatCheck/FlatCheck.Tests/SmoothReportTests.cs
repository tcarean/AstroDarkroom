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

namespace FlatCheck.Tests;

public class SmoothReportTests
{
    const int N = 512;

    static SmoothResult Clean() => SmoothAnalyzer.Analyze(TestImages.From(N, N, FitsTestData.Radial(N, N)), 24);

    static SmoothResult Blotched() => SmoothAnalyzer.Analyze(TestImages.From(N, N, (x, y) =>
        FitsTestData.Radial(N, N)(x, y) * (1 + 0.10 * Math.Exp(-((x - 100) * (x - 100) + (y - 300) * (y - 300)) / (2 * 20.0 * 20.0)))), 24);

    static string Written(SmoothResult r, string path = "flat.fit")
    {
        var w = new StringWriter();
        SmoothReport.Write(r, path, w);
        return w.ToString();
    }

    [Fact]
    public void TheReportNamesTheFileTheScaleAndWhatWasRemoved()
    {
        string text = Written(Clean(), "master.fit");

        Assert.Contains("=== NETEZIRE: master.fit ===", text);
        Assert.Contains("512 x 512, mono/procesat", text);
        Assert.Contains("24 px pe senzor", text);
        Assert.Contains("CE S-A SCOS", text);
        Assert.Contains("p1 / p99 pe pixel", text);
        Assert.Contains("dala cea mai abatuta", text);
        Assert.Contains("INAINTE / DUPA", text);
        Assert.Contains("VERDICT", text);
    }

    /// <summary>The before/after rows use the structure report's own labels, verbatim.</summary>
    [Fact]
    public void TheBeforeAfterRowsCarryTheStructureLabels()
    {
        string text = Written(Clean());

        foreach (string key in StructureReport.TestKeys)
            Assert.Contains(StructureReport.TestLabel(key), text);
        Assert.Contains("[PERFECT]  ->", text);
    }

    [Fact]
    public void TheWarningAppearsOnlyWhenFlagged_AndQuotesTheThresholdFromTh()
    {
        SmoothResult clean = Clean(), blotched = Blotched();

        Assert.False(clean.RemovedFlagged);
        Assert.Null(SmoothReport.Warning(clean));
        Assert.DoesNotContain("ATENTIE", Written(clean));

        Assert.True(blotched.RemovedFlagged);
        string warning = SmoothReport.Warning(blotched);
        Assert.Contains($"peste {Th.SmoothKeptWarn:F0} %", warning);
        Assert.Contains("ATENTIE", Written(blotched));
    }

    [Fact]
    public void EveryValueTheWindowBindsIsNonEmpty()
    {
        SmoothResult r = Clean();

        Assert.NotEmpty(SmoothReport.FrameLine(r));
        Assert.NotEmpty(SmoothReport.ScaleLine(r));
        Assert.NotEmpty(SmoothReport.RejectedLine(r));
        Assert.Equal(4, SmoothReport.RemovedLines(r).Length);
        Assert.NotEmpty(SmoothReport.Caveat());
        Assert.NotEmpty(SmoothReport.SirilNote());
        Assert.Contains("pp_flat_stacked", SmoothReport.SirilNote());
        Assert.Contains("a.fit", SmoothReport.RefusalRaw("a.fit"));
        Assert.Contains("3 planuri", SmoothReport.RefusalCube("a.fit", 3));
        Assert.Contains("x.fit", SmoothReport.SavedLine("x.fit"));
    }

    [Fact]
    public void TheRejectedLineSaysNoneWhenNothingWasRejected_AndCountsPerPositionOtherwise()
    {
        Assert.StartsWith("niciuna din", SmoothReport.RejectedLine(Clean()));

        var r = new SmoothResult
        {
            IsCfa = true,
            Positions = new[]
            {
                new SmoothPositionFit { Tiles = 100, Rejected = 3 }, new SmoothPositionFit { Tiles = 100, Rejected = 0 },
                new SmoothPositionFit { Tiles = 100, Rejected = 5 }, new SmoothPositionFit { Tiles = 100, Rejected = 1 },
            },
        };
        Assert.StartsWith("3, 0, 5, 1 din 100 pe pozitie", SmoothReport.RejectedLine(r));
    }
}
