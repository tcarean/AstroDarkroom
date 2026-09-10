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
/// The wording pieces of the structure report that both applications show, tested as
/// values. The golden files pin the whole text; these say what each piece must contain,
/// so a rewording that drops the point fails here with a reason.
/// </summary>
public class StructureReportTests
{
    static StructureResult Result(Func<int, int, double> scene, int w = 512, int h = 512)
        => StructureAnalyzer.Analyze(TestImages.From(w, h, scene));

    // ---------------------------------------------------------------- niveluri

    /// <summary>
    /// Siril writes the master as float divided by 65535, so the median is ~0.014. With
    /// zero decimals the line read "0 / 0" on every real master - as if the file were
    /// empty. Measured on a real Nikkor 35 ISO800 master: median 0.0143, max 0.0190.
    /// </summary>
    [Fact]
    public void LevelsOnAFloatMasterAreNotRoundedToZero()
    {
        var r = Result(FitsTestData.Scaled(FitsTestData.Radial(512, 512), 1.0 / 65535));

        string levels = StructureReport.Levels(r);

        Assert.DoesNotContain("0 / 0", levels);
        Assert.Contains("0.4578", levels);                 // 30000 / 65535
        Assert.Contains("float normalizat", levels);
    }

    [Fact]
    public void LevelsOnAnAduMasterStayWhole()
    {
        var r = Result(FitsTestData.Radial(512, 512));

        string levels = StructureReport.Levels(r);

        Assert.Contains("/ 30000", levels);
        Assert.DoesNotContain(".", levels);
        Assert.DoesNotContain("float", levels);
    }

    [Fact]
    public void TheConsoleLineAndTheWindowShareTheLevelsText()
    {
        var r = Result(FitsTestData.Scaled(FitsTestData.Radial(512, 512), 1.0 / 65535));
        var w = new StringWriter();

        StructureReport.Write(r, "x.fit", w);

        Assert.Contains("median / max ADU   : " + StructureReport.Levels(r), w.ToString());
    }

    // ---------------------------------------------------------------- sfatul

    /// <summary>
    /// The figures are an upper bound: a camera-fixed part (an axis off the sensor
    /// centre, a lens with genuinely non-radial vignetting) lands in REFA too, without
    /// being a flat error. Measured: an axis decentred by 5 % of the width gives REFA on a
    /// flat that corrects perfectly. So the advice must send to --ratio before "reshoot".
    /// </summary>
    [Fact]
    public void TheReshootAdvicePointsAtRatioBeforeReshooting()
    {
        string advice = StructureReport.Advice("REFA");

        Assert.Contains("--ratio", advice);
        Assert.Contains("fixa pe aparat", advice);
        Assert.True(advice.IndexOf("--ratio") < advice.IndexOf("refa flat-urile"),
            "--ratio should come before the instruction to reshoot");
    }

    // ---------------------------------------------------------------- pete locale

    [Fact]
    public void TheLocalSpotWarningIsNullWhenNothingIsFlagged()
    {
        var r = Result(FitsTestData.Radial(512, 512));

        Assert.False(r.LocalSpotFlagged);
        Assert.Null(StructureReport.LocalSpotWarning(r));
    }

    /// <summary>
    /// The warning says three things or it is useless: how deep, where, and the
    /// session-versus-library question - which is the user's own framing of why a dust
    /// shadow is not a verdict.
    /// </summary>
    [Fact]
    public void TheLocalSpotWarningSaysHowDeepWhereAndForWhatUse()
    {
        var r = new StructureResult
        {
            LocalSpotPct = -4.99, LocalSpotX = 441, LocalSpotY = 297, LocalSpotFlagged = true,
        };

        string warning = StructureReport.LocalSpotWarning(r)!;

        Assert.StartsWith("ATENTIE", warning);
        Assert.Contains("4.99 %", warning);
        Assert.Contains("(441, 297)", warning);
        Assert.Contains("sesiunea", warning);
        Assert.Contains("biblioteca", warning);
        Assert.Contains("fixa pe aparat", warning);
    }

    /// <summary>
    /// The sign is the diagnosis, so the row says which direction in words rather than
    /// leaving it to be inferred. A minus on a row called "pete locale" reads as an
    /// arithmetic slip if you do not already know direction is the point - the first
    /// question asked once the figure existed was whether it could be negative at all.
    /// </summary>
    [Theory]
    [InlineData(-0.31, "-0.31 % (mai intunecat) la (729, 873)")]
    [InlineData(0.36, "+0.36 % (mai luminos) la (729, 873)")]
    [InlineData(0.0, "+0.00 % la (729, 873)")]     // a constant residual states no direction
    public void TheLocalSpotValueNamesTheDirectionInWords(double pct, string expected)
    {
        var r = new StructureResult { LocalSpotPct = pct, LocalSpotX = 729, LocalSpotY = 873 };

        Assert.Equal(expected, StructureReport.LocalSpotValue(r));
    }

    [Fact]
    public void TheLocalSpotNoteExplainsBothDirections()
    {
        string note = StructureReport.LocalSpotNote();

        Assert.Contains("minus", note);
        Assert.Contains("mai intunecat", note);
        Assert.Contains("praf", note);
        Assert.Contains("plus", note);
        Assert.Contains("mai luminos", note);
        Assert.Contains("scurgere", note);
    }

    /// <summary>
    /// The note states the warning threshold, because it is the only place the user
    /// learns from where the figure starts to matter - and it takes the number from
    /// <see cref="Th.LocalSpotWarn"/> rather than spelling it out, so a recalibration
    /// cannot leave the text quoting the old value.
    ///
    /// The assertion cannot tell a literal "2" from the formatted constant while the two
    /// agree; what it catches is the two drifting apart afterwards, which is the actual
    /// risk. Same shape as `TheFixedCapsAreTheResidualScaleBoundaries`.
    /// </summary>
    [Fact]
    public void TheLocalSpotNoteQuotesTheThresholdFromTh()
    {
        string note = StructureReport.LocalSpotNote();

        Assert.Contains($"Peste {Th.LocalSpotWarn:F0} %", note);
        Assert.Contains("atentionare", note);
        Assert.Contains("informativa", note);
    }

    /// <summary>The console prints the whole note under the row, indented like its neighbours.</summary>
    [Fact]
    public void TheReportPrintsTheNoteUnderTheRow()
    {
        var w = new StringWriter();

        StructureReport.Write(Result(FitsTestData.Radial(512, 512)), "x.fit", w);
        string text = w.ToString();

        foreach (string line in StructureReport.LocalSpotNote().Split('\n'))
            Assert.Contains("        " + line, text);
    }

    /// <summary>The report prints the warning under the informative rows, indented like them.</summary>
    [Fact]
    public void TheReportPrintsTheWarningWhenFlagged()
    {
        const int W = 1536, H = 1024;
        var radial = FitsTestData.Radial(W, H);
        var r = Result((x, y) =>
        {
            double dx = x - 0.3 * W, dy = y - 0.3 * H;
            return radial(x, y) * (dx * dx + dy * dy < 30 * 30 ? 0.95 : 1.0);
        }, W, H);
        var w = new StringWriter();

        StructureReport.Write(r, "x.fit", w);

        Assert.Contains("    ATENTIE: pata compacta de", w.ToString());
        Assert.Contains("pete locale (praf etc.) : -", w.ToString());
    }

    // ---------------------------------------------------------------- notele testelor

    /// <summary>
    /// Every one of the five tests explains itself. Two of them - the azimuthal
    /// asymmetry and the tilt - sat in the window with a number, a verdict and no word
    /// about what they measured, because the two notes that did exist were written
    /// inline in the view-model and nobody noticed the other three were missing.
    /// Keyed by the result field name, so reordering the rows cannot silently unpair
    /// a note from its metric.
    /// </summary>
    [Theory]
    [InlineData(nameof(StructureResult.CornerSpreadPct))]
    [InlineData(nameof(StructureResult.AzimuthalMaxPct))]
    [InlineData(nameof(StructureResult.ResidualPtpPct))]
    [InlineData(nameof(StructureResult.TiltPct))]
    [InlineData(nameof(StructureResult.MaxTilePct))]
    public void EveryTestHasANote(string metric)
    {
        string note = StructureReport.TestNote(metric);

        Assert.False(string.IsNullOrWhiteSpace(note));
        Assert.True(note!.Length > 30, "a note this short explains nothing: " + note);
    }

    [Fact]
    public void AnUnknownMetricHasNoNote()
    {
        Assert.Null(StructureReport.TestNote("VignetteStops"));
    }

    /// <summary>
    /// The two that were missing say the thing that is not visible from the number: the
    /// azimuthal test compares sectors of one ring and only inside the inscribed circle,
    /// the tilt is a peak-to-peak amplitude that also catches a decentred axis.
    /// </summary>
    [Fact]
    public void TheAzimuthalNoteSaysWhatIsComparedAndWhere()
    {
        string note = StructureReport.TestNote(nameof(StructureResult.AzimuthalMaxPct))!;

        Assert.Contains("sectoare", note);
        Assert.Contains("inel", note);
        Assert.Contains("cercul inscris", note);
    }

    [Fact]
    public void TheTiltNoteSaysItIsAnAmplitudeAndCatchesDecentring()
    {
        string note = StructureReport.TestNote(nameof(StructureResult.TiltPct))!;

        Assert.Contains("varf-la-varf", note);
        Assert.Contains("gradient", note);
        Assert.Contains("decentrata", note);
    }

    /// <summary>No diacritics, like every other string the tool shows.</summary>
    [Theory]
    [InlineData(nameof(StructureResult.CornerSpreadPct))]
    [InlineData(nameof(StructureResult.AzimuthalMaxPct))]
    [InlineData(nameof(StructureResult.ResidualPtpPct))]
    [InlineData(nameof(StructureResult.TiltPct))]
    [InlineData(nameof(StructureResult.MaxTilePct))]
    public void TheNotesUseNoDiacritics(string metric)
    {
        Assert.All(StructureReport.TestNote(metric)!, c => Assert.True(c < 128, $"non-ASCII: '{c}'"));
    }

    // ---------------------------------------------------------------- scara

    [Fact]
    public void TheScaleHeadingCountsFiveTests()
    {
        var w = new StringWriter();

        StructureReport.Write(Result(FitsTestData.Radial(512, 512)), "x.fit", w);

        Assert.Contains("SCARA celor cinci teste", w.ToString());
        Assert.Contains("pata maxima (o dala)    :", w.ToString());
        Assert.Contains("Pata maxima", StructureReport.WhyResidualMatters());
    }
}
