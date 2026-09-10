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

    [Fact]
    public void TheLocalSpotValueCarriesTheSignAndThePosition()
    {
        var r = new StructureResult { LocalSpotPct = -0.31, LocalSpotX = 729, LocalSpotY = 873 };

        Assert.Equal("-0.31 % la (729, 873)", StructureReport.LocalSpotValue(r));
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
