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

    // ---------------------------------------------------------------- levels

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

    // ---------------------------------------------------------------- the advice

    /// <summary>
    /// The figures are an upper bound: a camera-fixed part (an axis off the sensor
    /// centre, a lens with genuinely non-radial vignetting) lands in REFA too, without
    /// being a flat error. Measured: an axis decentred by 5 % of the width gives REFA on a
    /// flat that corrects perfectly. So the advice must say so BEFORE "reshoot".
    /// </summary>
    [Fact]
    public void TheReshootAdviceSaysTheFigureIsAnUpperBoundBeforeReshooting()
    {
        string advice = StructureReport.Advice("REFA");

        // The console breaks the line after "limita".
        Assert.Contains("Cifra e o limita", advice);
        Assert.Contains("nu e eroare de flat", advice);
        Assert.True(advice.IndexOf("Cifra e o limita") < advice.IndexOf("refa flat-urile"),
            "the upper-bound caveat should come before the instruction to reshoot");
    }

    // ---------------------------------------------------------------- local spots

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
            LocalSpotPct = -4.99,
            LocalSpotX = 441,
            LocalSpotY = 297,
            LocalSpotFlagged = true,
        };

        string warning = StructureReport.LocalSpotWarning(r)!;

        Assert.StartsWith("ATENTIE", warning);
        Assert.Contains("4.99 %", warning);
        Assert.Contains("(441, 297)", warning);
        Assert.Contains("sesiunea", warning);
        Assert.Contains("biblioteca", warning);
        Assert.Contains("Tine de aparat", warning);
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
        Assert.Contains("mai luminoasa", note);
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

    /// <summary>
    /// The window's corner line and vignetting note use the console's ratios: each corner over
    /// the corners' mean, and the corners' mean over the centre. On a flat field all of them
    /// are 100 %.
    /// </summary>
    [Fact]
    public void TheWindowsCornerSharesAndVignetteNoteUseTheConsolesRatios()
    {
        StructureResult r = Result((_, _) => 1000);

        string shares = StructureReport.CornerShares(r);
        Assert.Equal(r.Corners.Count, shares.Split("100.0 %").Length - 1);
        Assert.StartsWith("coltul e 100 % din centru", StructureReport.VignetteNote(r));
    }

    /// <summary>
    /// The illumination-centre note names the vertex against the sensor's centre when the fit
    /// found one inside the frame, and says why not otherwise.
    /// </summary>
    [Fact]
    public void TheWindowsCentroidNoteNamesTheVertexOrWhyThereIsNone()
    {
        StructureResult vignetted = Result((x, y) => 1000 - 0.002 * ((x - 256.0) * (x - 256.0) + (y - 256.0) * (y - 256.0)));
        StructureResult flat = Result((_, _) => 1000);

        foreach (StructureResult r in new[] { vignetted, flat })
        {
            string note = StructureReport.CentroidNote(r);
            if (r.CentroidValid)
                Assert.StartsWith($"punctul cel mai luminos e la ({r.FitCx:F0}, {r.FitCy:F0}), centrul senzorului la", note);
            else
                Assert.Equal("punctul cel mai luminos cade in afara cadrului; nu se poate masura", note);
        }
        Assert.True(vignetted.CentroidValid);
    }

    /// <summary>
    /// The console prints the whole note under the row, indented like its neighbours.
    /// </summary>
    [Fact]
    public void TheReportPrintsTheNoteUnderTheRow()
    {
        var w = new StringWriter();

        StructureReport.Write(Result(FitsTestData.Radial(512, 512)), "x.fit", w);
        string text = w.ToString();

        foreach (string line in StructureReport.LocalSpotNote().Split('\n'))
            Assert.Contains("        " + line, text);
    }

    /// <summary>
    /// The report prints the warning under the informative rows, indented like them.
    /// </summary>
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

        Assert.Contains("    ATENTIE: pata mica de", w.ToString());
        Assert.Contains("pete locale (praf etc.) : -", w.ToString());
    }

    // ---------------------------------------------------------------- the tests' notes

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

        Assert.Contains("felii", note);
        Assert.Contains("inel", note);
        Assert.Contains("cercul care incape intreg in cadru", note);
    }

    [Fact]
    public void TheTiltNoteSaysItIsAnAmplitudeAndCatchesDecentring()
    {
        string note = StructureReport.TestNote(nameof(StructureResult.TiltPct))!;

        Assert.Contains("margine", note);
        Assert.Contains("mai luminoasa", note);
        Assert.Contains("descentrata", note);
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

    // ---------------------------------------------------------------- the scale

    [Fact]
    public void TheScaleHeadingCountsFiveTests()
    {
        var w = new StringWriter();

        StructureReport.Write(Result(FitsTestData.Radial(512, 512)), "x.fit", w);

        Assert.Contains("SCARA celor cinci teste", w.ToString());
        Assert.Contains("pata maxima (o dala)    :", w.ToString());
        Assert.Contains("Pata maxima", StructureReport.WhyResidualMatters());
    }

    // -------------------------------------------------------------- vignetting against the profile

    static LoadedFrame MasterFrame() => new LoadedFrame
    {
        Header = new Dictionary<string, string> { ["SIMPLE"] = "T", ["FOCALLEN"] = "20", ["INSTRUME"] = "Nikon D3300" },
        FocalMm = 20,
        Camera = "Nikon D3300",
    };

    static LoadedFrame RawFrame() => new LoadedFrame
    {
        Header = new Dictionary<string, string>(),
        FocalMm = 20,
        FNumber = 2.8,
        Camera = "NIKON D3300",
        LensId = 139,
        LensMinFocalMm = 11,
        LensMaxFocalMm = 20,
        LensMaxAperture = 2.8,
    };

    /// <summary>
    /// The fixture master (k = 0.45, 0.86 stops) against the fixture profiles, through the run's
    /// own Compare.
    /// </summary>
    static StructureOutcome Compared(TempWorkspace ws, bool withRaw = true, string? lensText = null, double? fstop = null)
    {
        var sources = LensProfileTestData.Sources(ws);
        var r = Result(FitsTestData.Radial(512, 512));
        var query = new StructureQuery
        {
            Sources = sources,
            RawPath = withRaw ? "DSC_0001.NEF" : null,
            LoadInfo = _ => RawFrame(),
            LensText = lensText,
            FStop = fstop,
        };
        return StructureRun.Compare(r, MasterFrame(), query);
    }

    [Fact]
    public void TheProfileBlockNamesTheMasterEveryProfileAndTheOneThatDecides()
    {
        using var ws = new TempWorkspace();
        StructureOutcome o = Compared(ws);
        var w = new StringWriter();

        StructureReport.Write(o, "x.fit", w);
        string text = w.ToString();

        Assert.True(o.Compared);
        Assert.Contains(StructureReport.ProfilesHeading, text);
        Assert.Contains("master                  : " + StructureReport.MasterDepth(o.Profiles), text);
        var rows = StructureReport.ProfileRows(o.Profiles);
        Assert.Equal(2, rows.Count);
        Assert.Single(rows, r => r.GivesVerdict);
        Assert.Contains("<- Adobe, da verdictul", text);                // the raw's ID found the lens (R-128)
        Assert.Contains(AgainstReport.DepthScale(), text);
        Assert.Contains("simetrica in jurul centrului", text);
        foreach (ProfileRow row in rows)
        {
            Assert.Contains(row.Lens, text);
            Assert.Contains(row.Diff, text);
            Assert.Matches(@"^[+-]\d\.\d\d stopuri$", row.Diff);
            Assert.NotEmpty(row.Note);
        }
    }

    [Fact]
    public void TheVerdictIsTheWorseOfShapeAndDepth_AndTheDetailSaysBoth()
    {
        using var ws = new TempWorkspace();
        StructureOutcome o = Compared(ws);
        var w = new StringWriter();

        StructureReport.Write(o, "x.fit", w);
        string text = w.ToString();

        Assert.Equal("PERFECT", o.Structure.Verdict);                 // the fixture is purely radial
        Assert.NotEqual("PERFECT", o.Profiles.Verdict);               // but 0.2 stops deeper than the model
        Assert.Equal(o.Profiles.Verdict, o.Verdict);
        Assert.True(o.DepthCarriesTheVerdict);
        Assert.Contains("VERDICT: " + o.Verdict, text);
        string detail = StructureReport.VerdictDetail(o);
        Assert.Contains("forma     : PERFECT", detail);
        Assert.Contains("adancimea : " + o.Profiles.Verdict, detail);
        Assert.Contains(detail.Split('\n')[0], text);
        // the advice is the depth's, with the sign read as the cause
        Assert.Equal(StructureReport.DepthAdvice(o.Profiles.Deciding.DiffStops, o.Profiles.Verdict), StructureReport.Advice(o));
        Assert.Contains(o.Profiles.Deciding.DiffStops > 0 ? "MAI MULT" : "MAI PUTIN", StructureReport.Advice(o));
    }

    /// <summary>
    /// The depth scale is the residual scale in stops - the same Th.Resid* through
    /// 2^|D| - 1: the boundaries in stops must be the ones the one-line scale prints, and the
    /// note under the window's one table (R-126) must say it is not a new threshold.
    /// </summary>
    [Fact]
    public void TheDepthScaleRowsAreTheResidualScaleInStops()
    {
        var rows = StructureReport.DepthScaleRows();

        Assert.Equal(new[] { "PERFECT", "BUN", "ACCEPTABIL", "REFA" }, rows.Select(r => r.Band));
        Assert.Contains("sub 0.03 stopuri", rows[0].Range);
        Assert.Contains("0.14 - 0.26 stopuri", rows[2].Range);
        Assert.Contains("peste 0.26 stopuri", rows[3].Range);
        foreach (string boundary in new[] { "0.03", "0.14", "0.26" })
            Assert.Contains(boundary, AgainstReport.DepthScale());
        Assert.Contains("2 / 10 / 20 %", StructureReport.DepthScaleNote());
        Assert.Contains("Nu e un prag nou", StructureReport.DepthScaleNote());
    }

    /// <summary>
    /// The depth verdict and --light judge the same number - the corner error of a
    /// calibrated light - on two scales: 10 % is BUN here and 5 % is PROBLEMA there. The
    /// depth verdict stays on the residual scale because its reference, the published
    /// profile, is uncertain by ~0.2 stops between the two databases (15 % in the corner,
    /// three times --light's PROBLEMA line), and the report says so instead of choosing
    /// for the reader: the corner error, the word --light would give it (through the same
    /// Verdict and the same Th.LightRad* as LightAnalyzer, so a recalibration cannot leave
    /// the sentence quoting old values), and the reference's own spread from Th. Decided
    /// 2026-09-23 over aligning the bands, which would have put most of the library on
    /// REFA below the reference's resolution.
    /// </summary>
    [Fact]
    public void TheDepthNoteSaysWhatTheLightScaleWouldReadAndWhyItDoesNotDecide()
    {
        string tenth = StructureReport.DepthLightScaleNote(0.10);   // 7.2 % in the corner
        Assert.Contains("7.2 %", tenth);
        Assert.Contains("i-ar spune PROBLEMA", tenth);
        Assert.Contains($"{Th.LightRadOk:F0} % OK", tenth);
        Assert.Contains($"{Th.LightRadBad:F0} % PROBLEMA", tenth);
        Assert.Contains($"{Th.ProfileDatabaseSpreadStops:F1} stopuri", tenth);
        Assert.Contains($"{Stats.DepthErrorPct(Th.ProfileDatabaseSpreadStops):F0} %", tenth);
        Assert.Contains("ramane pe scara celor cinci teste", tenth);

        Assert.Contains("i-ar spune OK", StructureReport.DepthLightScaleNote(0.02));          // 1.4 %
        Assert.Contains("i-ar spune SUSPECT", StructureReport.DepthLightScaleNote(-0.05));    // 3.5 %, sign irrelevant
        Assert.Contains("nu ar avea ce citi", StructureReport.DepthLightScaleNote(double.NaN));

        // the word is --light's own, not a copy: same function, same thresholds
        Assert.Equal(Stats.Verdict(Stats.DepthErrorPct(0.10), Th.LightRadOk, Th.LightRadBad), Stats.DepthOnLightScale(0.10));
        Assert.Equal("BUN", Stats.DepthVerdict(0.10));                                    // the verdict itself did not move
    }

    [Fact]
    public void TheDepthAdviceReadsTheSignAsTheCause()
    {
        string deeper = StructureReport.DepthAdvice(+0.5, "REFA");
        string shallower = StructureReport.DepthAdvice(-0.4, "REFA");
        string mild = StructureReport.DepthAdvice(+0.2, "ACCEPTABIL");

        Assert.Contains("MAI MULT", deeper);
        Assert.Contains("difuzor", deeper);
        Assert.Contains("Refa flat-urile", deeper);
        Assert.Contains("MAI PUTIN", shallower);
        Assert.Contains("cer", shallower);
        Assert.Contains("cele cinci teste nu o vad", mild);
        Assert.DoesNotContain("Refa flat-urile", mild);
        Assert.Contains("0.2 stopuri", mild);
    }

    [Fact]
    public void WithoutARawTheBlockExplainsAndTheVerdictStaysTheFiveTests()
    {
        using var ws = new TempWorkspace();
        StructureOutcome o = Compared(ws, withRaw: false);
        var w = new StringWriter();

        StructureReport.Write(o, "x.fit", w);
        string text = w.ToString();

        Assert.False(o.Compared);
        Assert.Equal(ComparisonGap.NoRaw, o.Profiles.Gap);
        Assert.Equal(o.Structure.Verdict, o.Verdict);
        Assert.False(o.DepthCarriesTheVerdict);
        Assert.Contains("nu se poate compara: " + StructureReport.GapText(o), text);
        Assert.Contains("niciun raw langa master", StructureReport.GapText(o));
        Assert.Contains("--lens", StructureReport.GapText(o));
        Assert.Contains("VERDICT: PERFECT", text);
        Assert.Contains(StructureReport.Advice("PERFECT"), text);     // the shape's advice, unchanged
    }

    [Fact]
    public void WithALensButNoFStopTheGapIsTheFStop()
    {
        using var ws = new TempWorkspace();
        StructureOutcome o = Compared(ws, withRaw: false, lensText: "Tokina");

        Assert.Equal(ComparisonGap.NoFStop, o.Profiles.Gap);
        Assert.Contains("diafragma nu se stie", StructureReport.GapText(o));
        Assert.Contains("--fstop", StructureReport.GapText(o));

        // the same with the f-stop given compares with every Tokina on the camera's mount
        o = Compared(ws, withRaw: false, lensText: "Tokina", fstop: 2.8);
        Assert.True(o.Compared);
        Assert.Equal(2, o.Profiles.Profiles.Count);
    }

    [Fact]
    public void ARawThatCannotBeReadIsNamedInTheGap()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        var r = Result(FitsTestData.Radial(512, 512));
        var query = new StructureQuery { Sources = sources, RawPath = "bad.NEF", LoadInfo = _ => throw new IOException("stricat") };

        StructureOutcome o = StructureRun.Compare(r, MasterFrame(), query);

        Assert.Equal(ComparisonGap.NoRaw, o.Profiles.Gap);
        Assert.Contains("bad.NEF nu s-a putut citi", o.RawError);
        Assert.Contains("stricat", StructureReport.GapText(o));
        Assert.Equal("PERFECT", o.Verdict);
    }

    /// <summary>
    /// The Canon EOS 40D session of 2026-10-26: the raw is there and is read, but carries no lens
    /// (focal length 0, aperture 0, lens ID 0). The gap names the file and what is missing, and
    /// asks for the lens by name plus the aperture, which the master does not carry either; the
    /// focal length is known from FOCALLEN, so it is not asked for.
    /// </summary>
    [Fact]
    public void R106_ARawThatCarriesNoLensIsSaidSoNotAsMissing()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        var r = Result(FitsTestData.Radial(512, 512));
        var noLens = new LoadedFrame { Header = new Dictionary<string, string>(), Camera = "Canon EOS 40D" };
        var query = new StructureQuery { Sources = sources, RawPath = "IMG_7290.CR2", LoadInfo = _ => noLens };

        StructureOutcome o = StructureRun.Compare(r, MasterFrame(), query);
        var w = new StringWriter();
        StructureReport.Write(o, "x.fit", w);
        string gap = StructureReport.GapText(o);

        Assert.Equal(ComparisonGap.RawWithoutLens, o.Profiles.Gap);
        Assert.Null(o.RawError);
        Assert.StartsWith("IMG_7290.CR2 nu spune nimic despre obiectiv", gap);
        Assert.Contains("telescop", gap);
        Assert.Contains("--lens <text>", gap);
        Assert.Contains("--fstop <f>", gap);
        Assert.DoesNotContain("--focal", gap);                     // FOCALLEN 20 is in the master
        Assert.DoesNotContain("niciun raw langa master", gap);
        Assert.Contains("nu se poate compara: " + gap, w.ToString());
        Assert.Equal("PERFECT", o.Verdict);

        // Without FOCALLEN the focal length is asked for too, in one sentence with the aperture.
        var bare = new LoadedFrame { Header = new Dictionary<string, string> { ["SIMPLE"] = "T" } };
        string both = StructureReport.GapText(StructureRun.Compare(r, bare, query));
        Assert.Contains("--focal <mm> si --fstop <f>", both);

        // The raw as the reference itself (the console on a CR2): no name, so "raw-ul".
        string self = StructureReport.GapText(StructureRun.Compare(r, noLens, new StructureQuery { Sources = sources }));
        Assert.StartsWith("raw-ul nu spune nimic despre obiectiv", self);
    }

    [Fact]
    public void EveryGapHasWordsAndNoneOfThemUsesDiacritics()
    {
        foreach (ComparisonGap gap in Enum.GetValues(typeof(ComparisonGap)))
        {
            if (gap == ComparisonGap.None) continue;
            var c = new VignettingComparison(0.5, LensIdentification.Identify(MasterFrame(), null, null), null, gap, null);
            string text = StructureReport.GapText(new StructureOutcome(Result(FitsTestData.Radial(256, 256)), c, c.Shot, null));
            Assert.False(string.IsNullOrWhiteSpace(text), gap.ToString());
            Assert.DoesNotMatch("[ăâîșțĂÂÎȘȚ]", text);
        }
        Assert.DoesNotMatch("[ăâîșțĂÂÎȘȚ]", StructureReport.ComparisonNote() + StructureReport.GapMeaning() + StructureReport.DepthAdvice(1, "REFA") + StructureReport.DepthAdvice(-1, "BUN"));
    }

    // ---------------------------------------------------------------- without Adobe's profiles

    /// <summary>The two Adobe folders the hand-made searches below looked in.</summary>
    static readonly string[] AdobeDirs = { @"C:\ProgramData\Adobe\CameraRaw\LensProfiles\1.0", @"D:\profiles\Adobe" };

    /// <summary>Sources found by a search of the two Adobe folders that held
    /// <paramref name="adobeFiles"/> files, beside a lensfun/ folder whose lenses were read when
    /// <paramref name="lensfun"/>, as on a computer with FlatCheck installed whole.</summary>
    static ProfileSources AdobeSearch(int adobeFiles, bool lensfun = true)
    {
        LensfunData lf = lensfun ? LensfunReader.ReadXml(LensProfileTestData.LensfunXml, "slr-tokina.xml") : LensfunData.Empty;
        return new(AdobeDirs, adobeFiles, 0, "lf", lensfun, false, null, new LensCatalog(lf.Profiles.ToList()), lf);
    }

    /// <summary>
    /// The red paragraph and the folders line belong to a search that found no Adobe file:
    /// with a single file found, or for a run that searched nothing (no raw), there is nothing
    /// to say.
    /// </summary>
    [Fact]
    public void R113_TheAdobeTextsAreEmptyWhenOneFileWasFoundOrTheRunSearchedNothing()
    {
        Assert.Empty(StructureReport.AdobeMissing(AdobeSearch(1)));
        Assert.Empty(StructureReport.AdobeSearched(AdobeSearch(1)));
        Assert.Empty(StructureReport.AdobeMissing(null));
        Assert.Empty(StructureReport.AdobeSearched(null));
    }

    /// <summary>
    /// Without an Adobe file the card says the profiles were not found and what that leaves,
    /// names the free DNG Converter and the other programs that bring them, says FlatCheck must
    /// restart, and lists the folders searched, in each language.
    /// </summary>
    [Fact]
    public void R113_WithoutAdobeFilesTheTextsSaySoNameTheConverterAndListTheFolders()
    {
        ProfileSources s = AdobeSearch(0);
        using (Ui.Use(UiLanguage.English))
        {
            string missing = StructureReport.AdobeMissing(s);
            Assert.StartsWith("Adobe's lens profiles were not found on this computer.", missing);
            Assert.Contains("lensfun alone", missing);
            Assert.Contains("focal range", missing);
            string install = StructureReport.AdobeInstall();
            Assert.Contains("free Adobe DNG Converter", install);
            Assert.Contains("Lightroom Classic", install);
            Assert.Contains("Camera Raw", install);
            Assert.Contains("restart FlatCheck", install);
            Assert.Equal(@"Searched in: C:\ProgramData\Adobe\CameraRaw\LensProfiles\1.0, D:\profiles\Adobe", StructureReport.AdobeSearched(s));
        }
        using (Ui.Use(UiLanguage.Romanian))
        {
            Assert.StartsWith("Profilele de obiectiv Adobe nu au fost gasite pe acest calculator.", StructureReport.AdobeMissing(s));
            Assert.Contains("Adobe DNG Converter, gratuit", StructureReport.AdobeInstall());
            Assert.Contains("reporneste FlatCheck", StructureReport.AdobeInstall());
            Assert.Equal(@"Cautate in: C:\ProgramData\Adobe\CameraRaw\LensProfiles\1.0, D:\profiles\Adobe", StructureReport.AdobeSearched(s));
        }
    }

    /// <summary>
    /// With neither Adobe's files nor the lensfun/ folder, the card's gap already says no
    /// profile is on the machine: the red paragraph then says only that Adobe's were not found,
    /// never that the lens is looked for in lensfun.
    /// </summary>
    [Fact]
    public void R113_WithoutLensfunEitherTheParagraphDoesNotSayLensfunIsSearched()
    {
        ProfileSources s = AdobeSearch(0, lensfun: false);
        using (Ui.Use(UiLanguage.English))
            Assert.Equal("Adobe's lens profiles were not found on this computer.", StructureReport.AdobeMissing(s));
        using (Ui.Use(UiLanguage.Romanian))
            Assert.Equal("Profilele de obiectiv Adobe nu au fost gasite pe acest calculator.", StructureReport.AdobeMissing(s));
    }

    /// <summary>
    /// A lensfun/ folder none of whose files could be read holds no lens: the card's gap then
    /// says no profile is on the machine, so the red paragraph keeps its first sentence, as
    /// without the folder.
    /// </summary>
    [Fact]
    public void R113_ALensfunFolderThatYieldsNoLensCountsAsNoLensfun()
    {
        var unreadable = new ProfileSources(AdobeDirs, 0, 0, "lf", true, false, null, LensCatalog.Empty, LensfunData.Empty);

        using (Ui.Use(UiLanguage.English))
            Assert.Equal("Adobe's lens profiles were not found on this computer.", StructureReport.AdobeMissing(unreadable));
    }

    /// <summary>Each text differs between the two languages and uses no diacritics.</summary>
    [Fact]
    public void R113_TheAdobeTextsDifferBetweenLanguagesAndUseNoDiacritics()
    {
        ProfileSources s = AdobeSearch(0);
        static string[] Texts(ProfileSources s, UiLanguage lang)
        {
            using (Ui.Use(lang))
                return new[] { StructureReport.AdobeMissing(s), StructureReport.AdobeInstall(), StructureReport.AdobeSearched(s) };
        }
        string[] en = Texts(s, UiLanguage.English), ro = Texts(s, UiLanguage.Romanian);

        for (int i = 0; i < en.Length; i++)
        {
            Assert.NotEqual(en[i], ro[i]);
            Assert.All(en[i] + ro[i], c => Assert.True(c < 128, $"non-ASCII: '{c}'"));
        }
    }

    /// <summary>The link is Adobe's own page for the DNG Converter, over https (checked in a
    /// browser on 2026-10-28, ADR 0073).</summary>
    [Fact]
    public void R113_TheConverterLinkIsAdobesHttpsPage()
    {
        var uri = new Uri(StructureReport.AdobeDngConverterUrl);

        Assert.Equal(Uri.UriSchemeHttps, uri.Scheme);
        Assert.Equal("helpx.adobe.com", uri.Host);
        Assert.EndsWith("/adobe-dng-converter.html", uri.AbsolutePath);
    }
}
