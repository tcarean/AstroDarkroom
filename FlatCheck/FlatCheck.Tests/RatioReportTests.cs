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

/// <summary>
/// The wording pieces of the attribution report that the window shows under the two
/// world-fixed rows. Tested as values: these two rows sat on screen with a number, a
/// coloured chip and no word about what "per orientation" meant or what the chip was.
/// </summary>
public class RatioReportTests
{
    /// <summary>
    /// The figure is what ONE orientation carried - half of what A/B shows, since the
    /// rotation inverts the gradient and the ratio counts it twice. The note has to say so,
    /// or the reader compares it with the map, which is at double amplitude.
    /// </summary>
    [Fact]
    public void TheWorldNoteSaysItIsPerOrientationAndHalfOfTheRatio()
    {
        string note = RatioReport.WorldNote();

        Assert.Contains("orientare", note);
        Assert.Contains("jumatate", note);
        Assert.Contains("A/B", note);
    }

    /// <summary>
    /// The chip looks like a verdict and is not one. The note says it does not vote, and
    /// quotes the two thresholds from <see cref="Th.RatioOk"/> / <see cref="Th.RatioBad"/>
    /// rather than spelling them out, so a recalibration cannot leave the text quoting old
    /// values. Same shape as `TheLocalSpotNoteQuotesTheThresholdFromTh`.
    /// </summary>
    [Fact]
    public void TheWorldNoteSaysTheLabelDoesNotVoteAndQuotesTheThresholdsFromTh()
    {
        string note = RatioReport.WorldNote();

        Assert.Contains("nu voteaza", note);
        Assert.Contains($"sub {Th.RatioOk:F0} %", note);
        Assert.Contains($"{Th.RatioBad:F0} %", note);
        Assert.Contains("OK", note);
        Assert.Contains("PROBLEMA", note);
    }

    /// <summary>The corner row had no description at all. It is the same gradient read in the four corners.</summary>
    [Fact]
    public void TheWorldCornerNoteSaysWhatItReadsAndWhere()
    {
        string note = RatioReport.WorldCornerNote();

        Assert.Contains("colturi", note);
        Assert.Contains("A/B", note);
        Assert.Contains("orientare", note);
    }

    /// <summary>No diacritics, like every other string the tool shows.</summary>
    [Fact]
    public void TheNotesUseNoDiacritics()
    {
        string all = RatioReport.WorldNote() + RatioReport.WorldCornerNote();

        Assert.All(all, c => Assert.True(c < 128, $"non-ASCII: '{c}'"));
    }

    /// <summary>
    /// The console report is untouched by the notes: they have no console caller, like
    /// StructureReport.TestNote, so the golden files and crosscheck.py stay as they are.
    /// </summary>
    [Fact]
    public void TheConsoleReportDoesNotPrintTheNotes()
    {
        var a = TestImages.From(256, 256, FitsTestData.Radial(256, 256));
        var b = TestImages.From(256, 256, FitsTestData.Radial(256, 256));
        var r = RatioAnalyzer.Analyze(a, b);
        var w = new StringWriter();

        RatioReport.Write(r, "a.fit", "b.fit", w);

        Assert.DoesNotContain("nu voteaza", w.ToString());
        Assert.Contains("gradient extern / orientare :", w.ToString());
        Assert.Contains("imprastiere colturi         :", w.ToString());
    }
}
