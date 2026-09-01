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
/// Garda care decide daca --light are ce masura pe cadrul dat.
///
/// Ce se verifica aici nu e o preferinta de praguri, ci ordinea straturilor si ce se
/// intampla cand un strat nu stie. Straturile sunt, in ordinea increderii: --fov declarat
/// de om, scara masurata de un plate solve, marimea pixelului plus focala, si in fine
/// focala singura. Ultimul e cazul unui DSLR trecut prin Siril, care nu scrie XPIXSZ.
///
/// Cea mai importanta asertiune din fisier e <see cref="AnEmptyHeaderRunsInsteadOfRefusing"/>:
/// necunoscut inseamna neverificat, nu suspect. Daca ar refuza, orice FITS cu header
/// minimal - inclusiv toate fixturile sintetice ale acestei suite - ar deveni de
/// neanalizat, si garda ar fi mai daunatoare decat problema pe care o rezolva.
/// </summary>
public class LightSuitabilityTests
{
    static Dictionary<string, string> Header(params (string Key, string Value)[] cards)
    {
        var h = new Dictionary<string, string> { ["NAXIS1"] = "6016", ["NAXIS2"] = "4016" };
        foreach (var (k, v) in cards) h[k] = v;
        return h;
    }

    // ------------------------------------------------------------ --fov declarat

    [Fact]
    public void AnExplicitFovBeatsEverythingElseInTheHeader()
    {
        // headerul ar zice "prea larg" pe focala; --fov spune adevarul si castiga
        var s = LightSuitability.Check(Header(("FOCALLEN", "20")), fovOverride: 12.0);

        Assert.Equal(FovLevel.Ok, s.Level);
        Assert.Equal(12.0, s.FovDeg, 6);
        Assert.Equal("--fov", s.Source);
    }

    [Fact]
    public void AnExplicitFovCanAlsoRefuse()
    {
        // --fov da unealtei adevarul, nu ii cere sa-l ignore: un camp larg declarat
        // manual e refuzat la fel ca unul dedus.
        var s = LightSuitability.Check(Header(("FOCALLEN", "600")), fovOverride: 70.0);

        Assert.Equal(FovLevel.TooWide, s.Level);
        Assert.False(s.CanRun);
    }

    // ------------------------------------------------------------ plate solve

    [Fact]
    public void APlateSolveScaleGivesTheFieldDirectly()
    {
        // 6016 x 4016 la 0.001 grade/px -> diagonala 7234 px -> ~7.2 grade
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

    /// <summary>Scara masurata bate focala: e o masuratoare, cealalta o deductie.</summary>
    [Fact]
    public void TheWcsScaleWinsOverTheFocalLength()
    {
        var s = LightSuitability.Check(Header(("CDELT1", "0.001"), ("FOCALLEN", "20")), null);

        Assert.Equal("WCS", s.Source);
        Assert.Equal(FovLevel.Ok, s.Level);
    }

    // ------------------------------------------------------ pixel + focala

    /// <summary>
    /// Cazul exact al setului care a motivat garda, dar cu marimea pixelului completata:
    /// D3300 (6016 x 4016 pixeli de 3.92 um -> senzor 23.6 x 15.7 mm) la 20 mm da 70.7
    /// grade diagonala. Asta e cifra reala, nu presupunerea conservatoare.
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

    /// <summary>Acelasi senzor la 135 mm: 12 grade, adica exact unde testul merge.</summary>
    [Fact]
    public void TheSameSensorAtALongFocalLengthIsFine()
    {
        var s = LightSuitability.Check(
            Header(("FOCALLEN", "135"), ("XPIXSZ", "3.92")), null);

        Assert.Equal(11.99, s.FovDeg, 2);
        Assert.Equal(FovLevel.Ok, s.Level);
        Assert.True(s.CanRun);
    }

    /// <summary>Binning-ul face pixelii efectivi mai mari, deci senzorul la fel de mare.</summary>
    [Fact]
    public void BinningIsFoldedIntoTheSensorSize()
    {
        var bin1 = LightSuitability.Check(
            Header(("FOCALLEN", "500"), ("XPIXSZ", "3.92"), ("XBINNING", "1"), ("YBINNING", "1")), null);
        var bin2 = LightSuitability.Check(
            Header(("FOCALLEN", "500"), ("XPIXSZ", "3.92"), ("XBINNING", "2"), ("YBINNING", "2")), null);

        // campul intra prin tangenta, deci un senzor de doua ori mai mare dubleaza
        // tan(fov/2), nu fov-ul insusi
        static double HalfTan(double deg) => Math.Tan(deg / 2 * Math.PI / 180);
        Assert.Equal(2 * HalfTan(bin1.FovDeg), HalfTan(bin2.FovDeg), 6);
    }

    // ------------------------------------------------------------ focala singura

    /// <summary>
    /// Fara marimea pixelului campul nu se poate calcula - depinde de senzor - deci NU se
    /// raporteaza o cifra care ar fi inventata. Se raporteaza focala si atat.
    /// </summary>
    [Theory]
    // Nivelul e trecut ca nume, nu ca FovLevel: enum-ul e internal, iar o metoda de test
    // publica nu poate avea un parametru mai putin accesibil decat ea. nameof pastreaza
    // totusi legatura cu enum-ul, deci o redenumire nu trece tacut.
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
        Assert.True(double.IsNaN(s.FovDeg));      // nicio cifra de camp inventata
        Assert.Equal(focal, s.FocalMm, 3);
        Assert.Contains("fara marimea pixelului", s.Source);
    }

    /// <summary>Focala se scrie fara zecimale inutile: "20mm", nu "20.0mm".</summary>
    [Fact]
    public void AWholeFocalLengthIsWrittenWithoutADecimalPoint()
    {
        var s = LightSuitability.Check(Header(("FOCALLEN", "20")), null);

        Assert.Contains("FOCALLEN=20mm", s.Source);
    }

    // ------------------------------------------------------------ necunoscut

    /// <summary>
    /// Headerul nu spune nimic util -> se RULEAZA, cu o nota. Vezi comentariul clasei:
    /// daca ar refuza aici, garda ar bloca orice FITS cu header minimal.
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

    /// <summary>Fara dimensiuni nu se poate calcula nici scara, nici campul.</summary>
    [Fact]
    public void MissingAxisLengthsFallBackToUnknown()
    {
        var h = new Dictionary<string, string> { ["CDELT1"] = "0.001" };

        Assert.Equal(FovLevel.Unknown, LightSuitability.Check(h, null).Level);
    }

    /// <summary>O focala absurda (0 sau negativa) nu e o focala; nu se ghiceste din ea.</summary>
    [Theory]
    [InlineData("0")]
    [InlineData("-50")]
    public void ANonsensicalFocalLengthIsTreatedAsAbsent(string focal)
    {
        Assert.Equal(FovLevel.Unknown, LightSuitability.Check(Header(("FOCALLEN", focal)), null).Level);
    }

    // ------------------------------------------------------------ pragurile

    /// <summary>
    /// Granitele sunt stricte in sus, ca peste tot in unealta: valoarea de pe prag cade in
    /// banda mai buna, iar cea de deasupra in cea mai proasta.
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

    /// <summary>Doar TooWide opreste rularea; Marginal si Unknown continua.</summary>
    [Theory]
    [InlineData(nameof(FovLevel.Ok), true)]
    [InlineData(nameof(FovLevel.Marginal), true)]
    [InlineData(nameof(FovLevel.Unknown), true)]
    [InlineData(nameof(FovLevel.TooWide), false)]
    public void OnlyTooWideStopsTheRun(string level, bool canRun)
    {
        var parsed = Enum.Parse<FovLevel>(level);

        Assert.Equal(canRun, new SuitabilityResult { Level = parsed }.CanRun);
    }
}
