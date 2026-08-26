namespace FlatCheck.Tests;

/// <summary>
/// Writes the exact synthetic FITS files the golden tests use, so flatcheck.py can be
/// run over the *same bytes* and the two implementations compared number by number.
///
/// flatcheck.py is the numeric oracle for this port (see CLAUDE.md), but the golden
/// files were captured from the C# itself - they pin behaviour without independently
/// confirming it. This is how that gap gets closed, and how it should be re-closed
/// after any formula change.
///
///     FLATCHECK_DUMP_FIXTURES=/some/dir dotnet test --filter DumpsFixtures
///     python flatcheck.py /some/dir/clean.fit
///
/// Does nothing unless the variable is set, so a normal test run is unaffected.
/// </summary>
public class CrossCheckFixtures
{
    const int Size = 512;

    [Fact]
    public void DumpsFixturesWhenAsked()
    {
        string dir = Environment.GetEnvironmentVariable("FLATCHECK_DUMP_FIXTURES");
        if (string.IsNullOrEmpty(dir)) return;

        Directory.CreateDirectory(dir);

        var radial = FitsTestData.Radial(Size, Size);
        var gradient = FitsTestData.WithLinearGradient(FitsTestData.Radial(Size, Size), Size, 0.12);
        var rawCfa = FitsTestData.WithBayerMosaic(FitsTestData.Radial(Size, Size, center: 34000));
        var raw12 = FitsTestData.WithBayerMosaic(FitsTestData.Radial(Size, Size, center: 3600));

        Write(dir, "clean.fit", radial);        // structure / light / debayered-expo
        Write(dir, "grad.fit", gradient);       // structure, and the B side of the ratio
        Write(dir, "raw_cfa.fit", rawCfa);      // --expo and --expo --sky
        Write(dir, "raw12bit.fit", raw12);      // --expo --sat 4095 --black 150

        // --- wider coverage, to exercise branches the golden scenarios miss ---

        // over-correction: background RISES toward the edges, so radPct goes negative
        Write(dir, "overcorrected.fit", TestImages.InvertedRadial(Size, Size));
        // a pure sky plane with no radial term at all
        Write(dir, "skyplane.fit", (x, y) => 8000 * (1.0 + 0.30 * (x / (double)Size - 0.5)));
        // a flat field: every structure metric should sit at zero
        Write(dir, "uniform.fit", FitsTestData.Constant(20000));
        // mild vs heavy vignetting, to move the stops figure
        Write(dir, "vign_mild.fit", FitsTestData.Radial(Size, Size, k: 0.15));
        Write(dir, "vign_heavy.fit", FitsTestData.Radial(Size, Size, k: 0.70));
        // a diagonal gradient - the golden one is horizontal only
        Write(dir, "diag.fit", (x, y) =>
            FitsTestData.Radial(Size, Size)(x, y)
            * (1.0 + 0.10 * ((x + y) / (2.0 * Size) - 0.5)));
        // nebulosity in one quadrant, to drive the sigma-clipping
        Write(dir, "nebula.fit", (x, y) =>
            FitsTestData.Radial(Size, Size)(x, y)
            + (x > Size * 0.6 && y > Size * 0.6 ? 6000 : 0));
        // an uncalibrated flat still carrying its offset pedestal
        Write(dir, "pedestal.fit", FitsTestData.WithOffset(FitsTestData.Radial(Size, Size), 600));

        // non-square, so the w/h asymmetries in every metric get exercised
        File.WriteAllBytes(Path.Combine(dir, "wide.fit"),
            FitsTestData.BuildFits(768, 512, FitsTestData.Radial(768, 512)));
        File.WriteAllBytes(Path.Combine(dir, "tall.fit"),
            FitsTestData.BuildFits(512, 768, FitsTestData.WithLinearGradient(
                FitsTestData.Radial(512, 768), 512, 0.12)));

        // other BITPIX, to confirm both readers agree on the scaling
        File.WriteAllBytes(Path.Combine(dir, "float32.fit"),
            FitsTestData.BuildFits(Size, Size, radial, bitpix: -32));
        File.WriteAllBytes(Path.Combine(dir, "int32.fit"),
            FitsTestData.BuildFits(Size, Size, radial, bitpix: 32));

        // exposure levels either side of the band boundaries
        foreach (var (name, pct) in new[]
                 { ("expo25", 25.0), ("expo30", 30.0), ("expo50", 50.0),
                   ("expo65", 65.0), ("expo81", 81.0) })
            File.WriteAllBytes(Path.Combine(dir, name + ".fit"),
                FitsTestData.BuildFits(256, 256, (x, y) =>
                {
                    double bright = 4095 * pct / 100.0;
                    if (y % 2 == 0 && x % 2 == 0) return bright * 0.62;
                    if (y % 2 == 1 && x % 2 == 1) return bright * 0.48;
                    return bright;
                }));

        Assert.True(File.Exists(Path.Combine(dir, "clean.fit")));
    }

    static void Write(string dir, string name, Func<int, int, double> scene)
        => File.WriteAllBytes(Path.Combine(dir, name), FitsTestData.BuildFits(Size, Size, scene));
}
