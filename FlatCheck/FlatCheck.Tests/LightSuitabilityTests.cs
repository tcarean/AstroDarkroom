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
/// The guard that decides whether --light has something to measure on the given frame.
///
/// What is checked here is not a preference of thresholds, but the order of the layers
/// and what happens when a layer does not know. The layers are, in order of trust:
/// --fov declared by the user, the scale measured by a plate solve, the pixel size plus
/// the focal length, and finally the focal length alone. The last is the case of a DSLR
/// passed through Siril, which does not write XPIXSZ.
///
/// The most important assertion in the file is
/// <see cref="AnEmptyHeaderRunsInsteadOfRefusing"/>: unknown means unchecked, not
/// suspect. If it refused, any FITS with a minimal header - including all the synthetic
/// fixtures of this suite - would become unanalysable, and the guard would do more harm
/// than the problem it solves.
/// </summary>
public class LightSuitabilityTests
{
    static Dictionary<string, string> Header(params (string Key, string Value)[] cards)
    {
        var h = new Dictionary<string, string> { ["NAXIS1"] = "6016", ["NAXIS2"] = "4016" };
        foreach (var (k, v) in cards) h[k] = v;
        return h;
    }

    // ------------------------------------------------------------ --fov declared

    [Fact]
    public void AnExplicitFovBeatsEverythingElseInTheHeader()
    {
        // the header would say "too wide" by the focal length; --fov tells the truth and wins
        var s = LightSuitability.Check(Header(("FOCALLEN", "20")), fovOverride: 12.0);

        Assert.Equal(FovLevel.Ok, s.Level);
        Assert.Equal(12.0, s.FovDeg, 6);
        Assert.Equal("--fov", s.Source);
    }

    [Fact]
    public void AnExplicitFovCanAlsoRefuse()
    {
        // --fov gives the tool the truth, it does not ask it to ignore it: a wide field declared
        // by hand is refused just like a deduced one.
        var s = LightSuitability.Check(Header(("FOCALLEN", "600")), fovOverride: 70.0);

        Assert.Equal(FovLevel.TooWide, s.Level);
        Assert.False(s.CanRun);
    }

    // ------------------------------------------------------------ plate solve

    [Fact]
    public void APlateSolveScaleGivesTheFieldDirectly()
    {
        // 6016 x 4016 at 0.001 degrees/px -> diagonal 7234 px -> ~7.2 degrees
        var s = LightSuitability.Check(Header(("CDELT1", "0.001")), null);

        Assert.Equal("WCS", s.Source);
        Assert.Equal(7.23, s.FovDeg, 1);
        Assert.Equal(FovLevel.Ok, s.Level);
    }

    [Fact]
    public void TheCdMatrixIsAcceptedWhenCdeltIsAbsent()
    {
        var s = LightSuitability.Check(Header(("CD1_1", "0.001"), ("CD2_1", "0")), null);

        Assert.Equal("WCS", s.Source);
        Assert.Equal(7.23, s.FovDeg, 1);
    }

    /// <summary>
    /// The measured scale beats the focal length: one is a measurement, the other a deduction.
    /// </summary>
    [Fact]
    public void TheWcsScaleWinsOverTheFocalLength()
    {
        var s = LightSuitability.Check(Header(("CDELT1", "0.001"), ("FOCALLEN", "20")), null);

        Assert.Equal("WCS", s.Source);
        Assert.Equal(FovLevel.Ok, s.Level);
    }

    // ------------------------------------------------------ pixel + focal length

    /// <summary>
    /// The exact case of the set that motivated the guard, but with the pixel size filled
    /// in: D3300 (6016 x 4016 pixels of 3.92 um -> sensor 23.6 x 15.7 mm) at 20 mm gives
    /// 70.7 degrees diagonal. That is the real figure, not the conservative assumption.
    /// </summary>
    [Fact]
    public void PixelSizeAndFocalLengthGiveTheRealField()
    {
        var s = LightSuitability.Check(
            Header(("FOCALLEN", "20"), ("XPIXSZ", "3.92"), ("YPIXSZ", "3.92")), null);

        Assert.Equal(70.66, s.FovDeg, 2);
        Assert.Equal(FovLevel.TooWide, s.Level);
        Assert.Contains("XPIXSZ", s.Source);
    }

    /// <summary>The same sensor at 135 mm: 12 degrees, that is exactly where the test
    /// works.</summary>
    [Fact]
    public void TheSameSensorAtALongFocalLengthIsFine()
    {
        var s = LightSuitability.Check(
            Header(("FOCALLEN", "135"), ("XPIXSZ", "3.92")), null);

        Assert.Equal(11.99, s.FovDeg, 2);
        Assert.Equal(FovLevel.Ok, s.Level);
        Assert.True(s.CanRun);
    }

    /// <summary>Binning makes the effective pixels larger, so the sensor just as
    /// large.</summary>
    [Fact]
    public void BinningIsFoldedIntoTheSensorSize()
    {
        var bin1 = LightSuitability.Check(
            Header(("FOCALLEN", "500"), ("XPIXSZ", "3.92"), ("XBINNING", "1"), ("YBINNING", "1")), null);
        var bin2 = LightSuitability.Check(
            Header(("FOCALLEN", "500"), ("XPIXSZ", "3.92"), ("XBINNING", "2"), ("YBINNING", "2")), null);

        // the field enters through the tangent, so a sensor twice as large doubles
        // tan(fov/2), not the fov itself
        static double HalfTan(double deg) => Math.Tan(deg / 2 * Math.PI / 180);
        Assert.Equal(2 * HalfTan(bin1.FovDeg), HalfTan(bin2.FovDeg), 6);
    }

    // ------------------------------------------------------------ focal length alone

    /// <summary>
    /// Without the pixel size the field cannot be computed - it depends on the sensor - so
    /// NO figure that would be invented is reported. The focal length is reported, and
    /// that is all.
    /// </summary>
    [Theory]
    // The level is passed as a name, not as FovLevel: the enum is internal, and a public
    // test method cannot have a parameter less accessible than itself. nameof still keeps
    // the link to the enum, so a rename does not pass silently.
    [InlineData(20, nameof(FovLevel.TooWide))]
    [InlineData(59.9, nameof(FovLevel.TooWide))]
    [InlineData(60, nameof(FovLevel.Marginal))]
    [InlineData(119.9, nameof(FovLevel.Marginal))]
    [InlineData(120, nameof(FovLevel.Ok))]
    [InlineData(600, nameof(FovLevel.Ok))]
    public void TheFocalOnlyFallbackIsGradedOnTheFocalLength(double focal, string expected)
    {
        var s = LightSuitability.Check(Header(("FOCALLEN", focal.ToString("0.###"))), null);

        Assert.Equal(expected, s.Level.ToString());
        Assert.True(double.IsNaN(s.FovDeg));      // no invented field figure
        Assert.Equal(focal, s.FocalMm, 3);
        Assert.Contains("fara marimea pixelului", s.Source);
    }

    /// <summary>The focal length is written without useless decimals: "20mm", not
    /// "20.0mm".</summary>
    [Fact]
    public void AWholeFocalLengthIsWrittenWithoutADecimalPoint()
    {
        var s = LightSuitability.Check(Header(("FOCALLEN", "20")), null);

        Assert.Contains("FOCALLEN=20mm", s.Source);
    }

    // ------------------------------------------------------------ unknown

    /// <summary>
    /// The header says nothing useful -> it RUNS, with a note. See the class comment: if it
    /// refused here, the guard would block any FITS with a minimal header.
    /// </summary>
    [Fact]
    public void AnEmptyHeaderRunsInsteadOfRefusing()
    {
        var s = LightSuitability.Check(Header(), null);

        Assert.Equal(FovLevel.Unknown, s.Level);
        Assert.True(s.CanRun);
        Assert.True(double.IsNaN(s.FovDeg));
        Assert.True(double.IsNaN(s.FocalMm));
    }

    [Fact]
    public void ANullHeaderAlsoRunsInsteadOfRefusing()
    {
        Assert.True(LightSuitability.Check(null, null).CanRun);
    }

    /// <summary>Without dimensions neither the scale nor the field can be computed.</summary>
    [Fact]
    public void MissingAxisLengthsFallBackToUnknown()
    {
        var h = new Dictionary<string, string> { ["CDELT1"] = "0.001" };

        Assert.Equal(FovLevel.Unknown, LightSuitability.Check(h, null).Level);
    }

    /// <summary>An absurd focal length (0 or negative) is not a focal length; nothing is
    /// guessed from it.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-50")]
    public void ANonsensicalFocalLengthIsTreatedAsAbsent(string focal)
    {
        Assert.Equal(FovLevel.Unknown, LightSuitability.Check(Header(("FOCALLEN", focal)), null).Level);
    }

    // ------------------------------------------------------------ the thresholds

    /// <summary>
    /// The boundaries are strict upwards, as everywhere in the tool: the value on the
    /// threshold falls in the better band, and the one above it in the worse one.
    /// </summary>
    [Theory]
    [InlineData(19.99, nameof(FovLevel.Ok))]
    [InlineData(20.0, nameof(FovLevel.Marginal))]
    [InlineData(39.99, nameof(FovLevel.Marginal))]
    [InlineData(40.0, nameof(FovLevel.TooWide))]
    public void TheFieldBandsAreLeftClosed(double fov, string expected)
    {
        Assert.Equal(expected, LightSuitability.Check(Header(), fovOverride: fov).Level.ToString());
    }

    /// <summary>Only TooWide stops the run; Marginal and Unknown go on.</summary>
    [Theory]
    [InlineData(nameof(FovLevel.Ok), true)]
    [InlineData(nameof(FovLevel.Marginal), true)]
    [InlineData(nameof(FovLevel.Unknown), true)]
    [InlineData(nameof(FovLevel.TooWide), false)]
    public void R031_OnlyTooWideStopsTheRun(string level, bool canRun)
    {
        var parsed = Enum.Parse<FovLevel>(level);

        Assert.Equal(canRun, new SuitabilityResult { Level = parsed }.CanRun);
    }
}
