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

using System;
using System.IO;
using System.Linq;
using FlatCheck;
using Xunit;

namespace FlatCheck.Tests;

public class VignettingModelTests
{
    static VignettingModel Adobe() => new(VignettingModelKind.Adobe,
        new[] { LensProfileTestData.A1, LensProfileTestData.A2, LensProfileTestData.A3 }, null, 20, 1.5);

    static VignettingModel Lensfun() => new(VignettingModelKind.Lensfun,
        new[] { LensProfileTestData.K1, LensProfileTestData.K2, LensProfileTestData.K3 }, null, 20, 1.61);

    /// <summary>
    /// The cross-check that fixed the Adobe convention: the piecewise table in the same LCP
    /// ends at r = 0.7094, which is the DX corner in units of the focal length
    /// (14.1 mm / 20 mm = 0.705), and the polynomial there gives 0.597 against the table's
    /// 0.603. Here the corner is fed in Dmax units the way the builder does it, with the
    /// profile's own SFF standing for the sensor (36 / 1.5 = 24 mm long side).
    /// </summary>
    [Fact]
    public void Adobe_CornerRadiusIsInUnitsOfTheFocalLength_AndMatchesThePiecewiseTable()
    {
        var m = Adobe();
        // corner of a 6016 x 4016 frame: xN = 0.5, yN = 4016 / 2 / 6016. Adobe's SFF 1.5 stands for
        // a 24 mm long side, so the corner sits at r = 0.72 (the real 23.5 mm sensor: 0.705).
        double r = m.Radius(0.5, 4016 / 2.0 / 6016, 0);
        Assert.InRange(r, 0.71, 0.73);
        Assert.Equal(1.0, m.Factor(0, 0, 0), 12);
        // the table's last point, r = 0.709416 -> 0.603; the polynomial there reads 0.597
        double fx = 20 * 1.5 / 36;
        Assert.InRange(m.Factor(0.709416 * fx, 0, 0), 0.59, 0.61);
    }

    [Fact]
    public void Adobe_FocalLengthXAndCentreMoveTheModel()
    {
        var centred = new VignettingModel(VignettingModelKind.Adobe, new[] { -0.5, 0.0, 0.0 }, null, 20, 1.5);
        var shifted = new VignettingModel(VignettingModelKind.Adobe, new[] { -0.5, 0.0, 0.0 }, null, 20, 1.5,
            focalX: 20 * 1.5 / 36, focalY: 20 * 1.5 / 36, centerX: 0.4, centerY: 0.5);
        Assert.Equal(centred.Factor(0.3, 0, 0), shifted.Factor(0.2, 0, 0), 12);   // same point, 0.1 Dmax to the right of the shifted centre
        Assert.True(shifted.Factor(0.3, 0, 0) < centred.Factor(0.3, 0, 0));
        // a camera crop equal to the profile's SFF changes nothing, with or without FocalLengthX
        Assert.Equal(shifted.Factor(0.2, 0, 0), shifted.Factor(0.2, 0, 1.5), 12);
        Assert.Equal(centred.Factor(0.3, 0.1, 0), centred.Factor(0.3, 0.1, 1.5), 12);
    }

    /// <summary>
    /// The camera's crop sets the frame's physical size; the profile's SFF is the calibration
    /// body and only the fallback. On the D3300 the difference is 1.5 % (1.523 vs 1.5); on a
    /// full-frame body with a DX-calibrated profile it is the whole corner.
    /// </summary>
    [Fact]
    public void Adobe_TheCamerasCropSetsTheFrameSize_TheProfilesSffIsOnlyTheFallback()
    {
        var m = Adobe();
        double yN = 4016 / 2.0 / 6016;
        double rSff = m.Radius(0.5, yN, 0);
        double rD3300 = m.Radius(0.5, yN, 1.523);
        double rFx = m.Radius(0.5, yN, 1.0);
        Assert.Equal(rSff, m.Radius(0.5, yN, 1.5), 12);
        Assert.InRange(rD3300, 0.700, 0.712);           // 14.2 mm / 20 mm: where the piecewise table ends
        Assert.Equal(rSff * 1.5 / 1.523, rD3300, 9);
        Assert.Equal(rSff * 1.5, rFx, 9);                 // 36 mm wide: the corner sits 1.5x further out
        Assert.True(m.Factor(0.5, yN, 1.0) < m.Factor(0.5, yN, 1.523));
        // decentring is a property of the lens, in mm: the shift does not scale with the crop
        var shifted = new VignettingModel(VignettingModelKind.Adobe, new[] { -0.5, 0.0, 0.0 }, null, 20, 1.5,
            focalX: 20 * 1.5 / 36, centerX: 0.4);
        Assert.Equal(shifted.Radius(0.0, 0, 1.0), (0.1 * 24) / 20, 9);
    }

    [Fact]
    public void Adobe_ATableOnlyProfileInterpolatesLinearlyAndClampsAtTheEnds()
    {
        var table = new[] { (0.0, 1.0), (0.5, 0.8), (1.0, 0.5) };
        var m = new VignettingModel(VignettingModelKind.Adobe, null, table, 20, 1.5);
        double fx = 20 * 1.5 / 36;
        Assert.Equal(0.9, m.Factor(0.25 * fx, 0, 0), 9);     // r = 0.25, halfway between the first two points
        Assert.Equal(0.5, m.Factor(2.0 * fx, 0, 0), 9);      // beyond the table: clamped
    }

    /// <summary>
    /// lensfun's Hugin convention: r = 1 at the half-diagonal of a 36x24 frame divided by the
    /// calibration crop. On that same sensor the corner is r = 1 exactly, and the polynomial
    /// there is 1 + k1 + k2 + k3 = 0.5587. On a 1.523-crop body the corner sits at r = 1.06.
    /// </summary>
    [Fact]
    public void Lensfun_RadiusIsNormalisedToTheCalibrationSensorsHalfDiagonal()
    {
        var m = Lensfun();
        double halfW = 0.5, halfH = 24.0 / 36.0 / 2;          // a 3:2 frame in Dmax units
        Assert.Equal(1.0, m.Radius(halfW, halfH, 1.61), 9);
        Assert.Equal(1 + LensProfileTestData.K1 + LensProfileTestData.K2 + LensProfileTestData.K3, m.Factor(halfW, halfH, 1.61), 9);
        Assert.InRange(m.Radius(halfW, halfH, 1.523), 1.05, 1.07);
    }

    [Fact]
    public void SensorFormats_AreTheSixNominalCrops_AndTheRefusalListsThem()
    {
        Assert.Equal(6, SensorFormats.All.Count);
        Assert.Equal(1.0, SensorFormats.All.Single(f => f.Label == "full frame").Crop);
        Assert.Equal(1.5, SensorFormats.All.Single(f => f.Label == "APS-C").Crop);
        Assert.Equal(1.6, SensorFormats.All.Single(f => f.Label == "APS-C Canon").Crop);
        Assert.Equal("1.0", SensorFormats.All[0].CropText);
        Assert.Contains("APS-C Canon 1.6", SensorFormats.Describe());
        Assert.Contains("APS-C Canon 1.6", SyntheticReport.NoCrop("Canon EOS 7D"));
        Assert.Contains("Canon EOS 7D", SyntheticReport.NoCrop("Canon EOS 7D"));
    }

    [Fact]
    public void Lensfun_RefusesToRunWithoutTheCameraCrop()
    {
        Assert.Throws<ArgumentException>(() => Lensfun().Factor(0.1, 0.1, 0));
    }

    [Fact]
    public void AModelNeedsCoefficientsOrATable()
    {
        Assert.Throws<ArgumentException>(() => new VignettingModel(VignettingModelKind.Adobe, null, null, 20, 1.5));
        Assert.Throws<ArgumentException>(() => new VignettingModel(VignettingModelKind.Adobe, new[] { -0.1, 0, 0 }, null, 0, 1.5));
    }
}

public class LensProfileReaderTests
{
    [Fact]
    public void Lcp_ReadsEveryEntryWithAVignetteModel_WithApexApertureAndAliases()
    {
        var profiles = LcpReader.ReadXml(LensProfileTestData.Lcp(), "x.lcp");

        Assert.Equal(3, profiles.Count);
        var p = profiles.Single(q => q.FocalMm == 20 && Math.Abs(q.FStop - 2.8) < 0.01);
        Assert.Equal(ProfileSource.Adobe, p.Lens.Source);
        Assert.Equal("Tokina AT-X 11-20mm F2.8 PRO DX", p.Lens.Name);
        Assert.Equal("NIKON CORPORATION", p.Lens.Mount);
        Assert.Equal("Tokina", p.Lens.Maker);
        Assert.Equal(10000, p.DistanceM);
        Assert.Contains("11.0-20.0 mm f/2.8", p.Aliases);
        Assert.Contains(139L, p.LensIds);
        Assert.Contains(122L, p.LensIds);
        Assert.Equal(LensProfileTestData.A1, p.Model.Coefficients[0], 9);
        Assert.Equal(4, p.Model.Table!.Count);                  // the piecewise table rides along
        Assert.Equal(0.603, p.Model.Table[3].V, 6);

        var f4 = profiles.Single(q => q.FocalMm == 20 && Math.Abs(q.FStop - 4.0) < 0.01);
        Assert.Null(f4.Model.Table);
        Assert.Equal(-0.289055, f4.Model.Coefficients[0], 9);

        var decentred = profiles.Single(q => q.FocalMm == 18);
        Assert.Equal(0.899246, decentred.Model.FocalX!.Value, 9);
        Assert.Equal(0.493778, decentred.Model.CenterX, 9);
    }

    [Fact]
    public void Lcp_SkipsEntriesWithoutAVignetteModelAndFilesThatAreNotXmp()
    {
        Assert.Empty(LcpReader.ReadXml("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"/>", "empty.lcp"));
        Assert.ThrowsAny<Exception>(() => LcpReader.ReadXml("not xml at all", "bad.lcp"));
    }

    [Fact]
    public void Lensfun_ReadsPaVignettingEntriesAndCameras_AndSkipsLensesWithoutVignetting()
    {
        var d = LensfunReader.ReadXml(LensProfileTestData.LensfunXml, "slr-tokina.xml");

        Assert.Equal(4, d.Profiles.Count);
        Assert.All(d.Profiles, p => Assert.Equal(ProfileSource.Lensfun, p.Lens.Source));
        var p = d.Profiles.First(q => q.FocalMm == 20 && q.FStop == 2.8 && q.DistanceM == 1000);
        Assert.Equal("Tokina AT-X 11-20 F2.8 PRO DX Aspherical 11-20mm f/2.8", p.Lens.Name);
        Assert.Equal("Canon EF / Nikon F AF", p.Lens.Mount);
        Assert.Contains("Tokina AT-X 11-20 F2.8 PRO DX", p.Aliases);
        Assert.Equal(1.61, p.Model.CropFactor, 9);
        Assert.Equal(LensProfileTestData.K1, p.Model.Coefficients[0], 9);
        Assert.DoesNotContain(d.Profiles, q => q.Lens.Name.Contains("Samyang"));

        var cam = Assert.Single(d.Cameras);
        Assert.Equal("Nikon D3300", cam.Model);
        Assert.Equal(1.523, cam.CropFactor, 9);
    }

    [Fact]
    public void Lensfun_CropOfMatchesTheCameraNameFromTheHeaderOrTheRaw()
    {
        var d = LensfunReader.ReadXml(LensProfileTestData.LensfunXml, "x.xml");
        Assert.Equal(1.523, d.CropOf("NIKON D3300")!.Value, 9);
        Assert.Equal(1.523, d.CropOf("Nikon Corporation NIKON D3300")!.Value, 9);
        Assert.Equal(1.523, d.CropOf("D3300")!.Value, 9);
        Assert.Null(d.CropOf("Canon EOS 7D"));
        Assert.Null(d.CropOf(null));
    }

    [Fact]
    public void Locate_MergesBothSourcesAndReportsWhereItLooked()
    {
        using var ws = new TempWorkspace();
        var s = LensProfileTestData.Sources(ws);

        Assert.Equal(2, s.AdobeFiles);
        Assert.Equal(0, s.AdobeFilesSkipped);
        Assert.True(s.LensfunPresent);
        Assert.Contains("upstream commit: test", s.LensfunVersion);
        Assert.Equal(2, s.AdobeLenses);          // the same lens on two mounts, known from the file names alone
        Assert.Equal(1, s.LensfunLenses);
        Assert.Equal(4, s.Catalog.Profiles.Count);          // only lensfun is parsed up front
        var nikon = s.Catalog.Lenses.Single(l => l.Source == ProfileSource.Adobe && l.Mount.StartsWith("NIKON"));
        Assert.Equal(3, s.Catalog.ProfilesOf(nikon).Count);  // the Adobe file is parsed on demand
        Assert.All(s.Catalog.ProfilesOf(nikon), p => Assert.Equal(nikon, p.Lens));
        Assert.Single(s.Catalog.ByLensId(139, "Nikon"));
        Assert.Equal(2, s.Catalog.ByLensId(139).Count);
    }

    [Theory]
    [InlineData(@"Tokina\Nikon\NIKON CORPORATION (Tokina AT-X 11-20mm F2.8 PRO DX) - RAW.lcp", "NIKON CORPORATION", "Tokina AT-X 11-20mm F2.8 PRO DX", "Tokina", "NIKON CORPORATION")]
    [InlineData(@"Tokina\Canon\Canon EOS 50D (Tokina AT-X 14-20mm F2 PRO DX (IF)) - RAW.lcp", "Canon EOS 50D", "Tokina AT-X 14-20mm F2 PRO DX (IF)", "Tokina", "Canon EOS 50D")]
    [InlineData(@"Nikon\Nikon\NIKON D800 (AF-S NIKKOR 24-70mm f2.8G ED) - JPEG.lcp", "NIKON D800", "AF-S NIKKOR 24-70mm f2.8G ED", "AF-S", "NIKON D800, JPEG")]
    public void AnAdobeFileNameCarriesTheCameraAndTheLens(string path, string make, string lens, string maker, string mount)
    {
        var stub = AdobeProfileFile.FromName(path);
        Assert.NotNull(stub);
        Assert.Equal(lens, stub!.Lens.Name);
        Assert.Equal(maker, stub.Lens.Maker);
        Assert.Equal(mount, stub.Lens.Mount);
        Assert.Equal(ProfileSource.Adobe, stub.Lens.Source);
        _ = make;
    }

    [Fact]
    public void AFixedLensCameraFileIsAStubNamedAfterTheCamera()
    {
        var phone = AdobeProfileFile.FromName(System.IO.Path.Combine("x", "Apple", "iPhone 6.lcp"));
        Assert.Equal("iPhone 6", phone.Lens.Name);
        Assert.Equal("Apple", phone.Lens.Maker);
        Assert.Equal("", phone.Lens.Mount);
        var compact = AdobeProfileFile.FromName(System.IO.Path.Combine("x", "Canon", "Canon PowerShot G12 - RAW.lcp"));
        Assert.Equal("Canon PowerShot G12", compact.Lens.Name);
    }

    [Fact]
    public void Locate_WithNothingInstalledIsEmptyNotAnError()
    {
        using var ws = new TempWorkspace();
        var s = ProfileLocator.Locate(new[] { Path.Combine(ws.Dir, "nu-exista") }, Path.Combine(ws.Dir, "nici-asta"));
        Assert.Equal(0, s.AdobeFiles);
        Assert.False(s.LensfunPresent);
        Assert.Empty(s.Catalog.Lenses);
    }
}

public class LensCatalogTests
{
    static LensCatalog Catalog()
    {
        var adobe = LcpReader.ReadXml(LensProfileTestData.Lcp(), "n.lcp")
            .Concat(LcpReader.ReadXml(LensProfileTestData.Lcp(make: "Canon"), "c.lcp"));
        var lf = LensfunReader.ReadXml(LensProfileTestData.LensfunXml, "x.xml").Profiles;
        return new LensCatalog(adobe.Concat(lf).ToList());
    }

    [Fact]
    public void Search_MatchesEveryWordInNameMakerMountOrAlias_CaseInsensitively()
    {
        var c = Catalog();
        Assert.Equal(3, c.Lenses.Count);
        Assert.Equal(3, c.Search("tokina 11-20").Count);
        Assert.Equal(2, c.Search("tokina 11-20", ProfileSource.Adobe).Count);
        Assert.Single(c.Search("11-20 nikon corporation"));
        Assert.Single(c.Search("Aspherical"));
        Assert.Equal(2, c.Search("11.0-20.0 mm").Count);       // the Adobe alias, carried by both mount files
        Assert.Empty(c.Search("Samyang"));
        Assert.Equal(3, c.Search("").Count);
    }

    [Fact]
    public void Nearest_PicksTheFocalThenTheFStopInStopsThenTheFarthestDistance()
    {
        var c = Catalog();
        var lf = c.Lenses.Single(l => l.Source == ProfileSource.Lensfun);
        var ps = c.ProfilesOf(lf);

        var p = LensCatalog.Nearest(ps, 20, 2.8);
        Assert.Equal(20, p!.FocalMm);
        Assert.Equal(2.8, p.FStop);
        Assert.Equal(1000, p.DistanceM);                        // infinity over 10 m

        Assert.Equal(18, LensCatalog.Nearest(ps, 17, 2.8)!.FocalMm);
        Assert.Equal(4.0, LensCatalog.Nearest(ps, 20, 3.5)!.FStop);    // 3.5 is 0.64 stops from 2.8 and 0.39 from 4
        Assert.Equal(2.8, LensCatalog.Nearest(ps, 20, 1.4)!.FStop);
        Assert.Null(LensCatalog.Nearest(Array.Empty<LensProfile>(), 20, 2.8));
    }

    [Fact]
    public void ByLensId_FindsTheAdobeProfilesThatCarryTheNikonId()
    {
        var c = Catalog();
        var hits = c.ByLensId(139);
        Assert.Equal(2, hits.Count);
        Assert.All(hits, h => Assert.Equal(ProfileSource.Adobe, h.Source));
        Assert.Empty(c.ByLensId(7));
    }

    [Theory]
    [InlineData("Tokina AT-X 11-20mm F2.8 PRO DX", 11.0, 20.0, 2.8)]
    [InlineData("Tokina atx-i 11-20mm f2.8 CF", 11.0, 20.0, 2.8)]
    [InlineData("Tokina AT-X 11-20 F2.8 PRO DX Aspherical 11-20mm f/2.8", 11.0, 20.0, 2.8)]
    [InlineData("Nikon AF-S DX NIKKOR 18-200mm f3.5-5.6G ED VR II", 18.0, 200.0, 3.5)]
    [InlineData("SIGMA 18-35mm F1.8 DC HSM A013", 18.0, 35.0, 1.8)]
    [InlineData("Samyang 135mm f/2.0 ED UMC", 135.0, 135.0, 2.0)]
    [InlineData("Canon EF 50mm f/1.8 II", 50.0, 50.0, 1.8)]
    [InlineData("Tamron SP 35mm F/1.8 Di VC USD F012", 35.0, 35.0, 1.8)]
    [InlineData("Zeiss Batis 2/25", null, null, null)]
    public void LensSpec_ReadsTheRangeAndTheApertureOutOfTheName(string name, double? min, double? max, double? ap)
    {
        var spec = LensSpec.Parse(name);
        Assert.Equal(min, spec.MinFocalMm);
        Assert.Equal(max, spec.MaxFocalMm);
        Assert.Equal(ap, spec.MaxAperture);
    }

    [Fact]
    public void Narrow_DropsTheLensesWhoseNameContradictsTheRaw_AndKeepsTheOnesItCannotRead()
    {
        var c = Catalog();
        var hits = c.ByLensId(139);                                        // the Tokina 11-20 on two mounts
        Assert.Equal(2, LensCatalog.Narrow(hits, 11, 20, 2.8).Count);
        Assert.Equal(2, LensCatalog.Narrow(hits, 11.5, 20, 2.83).Count);   // a rounded name, a sixth-stop code
        Assert.Equal(2, LensCatalog.Narrow(hits, null, null, null).Count); // a raw that says nothing
        Assert.Empty(LensCatalog.Narrow(hits, 18, 200, 3.5));              // the Nikkor that shares ID 139
        Assert.Empty(LensCatalog.Narrow(hits, 11, 20, 4.0));               // right range, wrong aperture
        var unreadable = new[] { new LensChoice(ProfileSource.Lensfun, "Zeiss", "Zeiss Batis 2/25", "Sony E") };
        Assert.Single(LensCatalog.Narrow(unreadable, 18, 200, 3.5));
    }

    [Fact]
    public void LensSpec_DescribesWhatTheRawSaid()
    {
        Assert.Equal("11-20 mm f/2.8", LensSpec.Describe(11, 20, 2.8));
        Assert.Equal("135 mm f/2.0", LensSpec.Describe(135, 135, 2));
        Assert.Equal("11-20 mm", LensSpec.Describe(11, 20, null));
        Assert.Equal("f/2.8", LensSpec.Describe(null, null, 2.8));
        Assert.Equal("", LensSpec.Describe(null, null, null));
    }
}

public class SyntheticFlatTests
{
    static LensProfile AdobeProfile()
        => LcpReader.ReadXml(LensProfileTestData.Lcp(), "x.lcp").Single(p => p.FocalMm == 20 && Math.Abs(p.FStop - 2.8) < 0.01);

    [Fact]
    public void TheFlatIsOneOnTheAxis_DarkerOutwards_AndSymmetric()
    {
        var r = SyntheticFlatBuilder.Build(AdobeProfile(), 600, 400, 0);
        Img f = r.Flat;
        Assert.Equal(600, f.W);
        Assert.Equal(400, f.H);
        Assert.InRange(f.At(200, 300), 0.999, 1.0);
        Assert.True(f.At(0, 0) < 0.7);
        Assert.Equal(f.At(0, 0), f.At(399, 599), 9);
        Assert.Equal(f.At(0, 599), f.At(399, 0), 9);
        Assert.InRange(r.CornerStops, 0.6, 0.8);
        Assert.True(r.LongEdgeStops > r.ShortEdgeStops);
        Assert.True(r.HalfDiagonalStops < r.CornerStops);
    }

    /// <summary>A synthetic flat is radial by construction, so the structure battery must call it perfect.</summary>
    [Fact]
    public void TheFlatIsPerfectlyRadialForTheStructureAnalyzer()
    {
        var r = SyntheticFlatBuilder.Build(AdobeProfile(), 1200, 800, 0);
        var s = StructureAnalyzer.Analyze(r.Flat);
        Assert.Equal("PERFECT", s.Verdict);
        Assert.True(s.CornerSpreadPct < 0.5, "spread " + s.CornerSpreadPct);
        Assert.InRange(s.VignetteStops, 0.6, 0.8);
    }

    [Fact]
    public void EveryDepthRowCarriesANote_AndTheConsoleLinesAreTheSameRows()
    {
        var r = SyntheticFlatBuilder.Build(AdobeProfile(), 600, 400, 0);
        var rows = SyntheticReport.DepthRows(r);
        Assert.Equal(4, rows.Length);
        Assert.All(rows, d => { Assert.NotEmpty(d.Label); Assert.NotEmpty(d.Value); Assert.True(d.Note.Length > 40, d.Label); });
        Assert.Contains("vignetare centru -> colt", rows[0].Note);   // the corner is the master's own figure
        var lines = SyntheticReport.DepthLines(r);
        Assert.Equal(4, lines.Length);
        for (int i = 0; i < 4; i++)
        {
            Assert.StartsWith(rows[i].Label, lines[i]);
            Assert.EndsWith(rows[i].Value, lines[i]);
            Assert.DoesNotContain(rows[i].Note, lines[i]);
        }
    }

    [Fact]
    public void AgainstItself_AFlatIsPerfectWithZeroStops()
    {
        var r = SyntheticFlatBuilder.Build(AdobeProfile(), 600, 400, 0);
        var a = AgainstAnalyzer.Analyze(r.Flat, r.Flat);
        Assert.Equal("PERFECT", a.Structure.Verdict);
        Assert.Equal("PERFECT", a.DepthVerdict);
        Assert.Equal("PERFECT", a.Verdict);
        Assert.Equal(0.0, a.Structure.VignetteStops, 6);
        Assert.Equal(0.0, a.DepthErrorPct, 6);
        Assert.Equal(a.A.VignetteStops, a.B.VignetteStops, 9);   // the two columns are the flat itself
        Assert.InRange(a.A.VignetteStops, 0.6, 0.8);
        var rows = AgainstReport.Rows(a);
        Assert.Equal(8, rows.Count);
        Assert.Equal("vignetare centru -> colt", rows[0].Label);
        Assert.Equal(rows[0].A, rows[0].B);
        Assert.StartsWith("0.00 stopuri", rows[0].Diff);
        Assert.All(rows, row => Assert.NotEmpty(row.Note));
        // the depth row is the only one with a verdict in the difference column; the five
        // tests keep theirs on A and B only
        Assert.Equal("PERFECT", rows[0].DiffVerdict);
        Assert.True(rows[0].IsKey);
        Assert.All(rows.Skip(3), row => { Assert.Null(row.DiffVerdict); Assert.NotNull(row.AVerdict); Assert.NotNull(row.BVerdict); });
    }

    /// <summary>
    /// The depth verdict is the residual scale on 2^|stops| - 1, the error the wrong flat
    /// leaves in the corner of a calibrated light. The figures are the Tokina masters' (docs/lens-profiles.md).
    /// </summary>
    [Theory]
    [InlineData(0.00, 0.0, "PERFECT")]
    [InlineData(0.02, 1.4, "PERFECT")]
    [InlineData(0.09, 6.4, "BUN")]
    [InlineData(0.16, 11.7, "ACCEPTABIL")]
    [InlineData(-0.16, 11.7, "ACCEPTABIL")]
    [InlineData(-0.42, 33.8, "REFA")]
    public void TheDepthDifferenceIsGradedAsTheCornerErrorItLeaves(double stops, double errorPct, string verdict)
    {
        double e = AgainstResult.DepthErrorOf(stops);
        Assert.Equal(errorPct, e, 1);
        Assert.Equal(verdict, Stats.ResidualVerdict(e));
        Assert.Equal(Math.Abs(stops), AgainstReport.StopsFor(e), 9);
    }

    [Fact]
    public void TheDepthScaleIsDerivedFromTheResidualBands_NotWrittenOut()
    {
        Assert.Equal("sub 0.03 stopuri PERFECT, sub 0.14 BUN, sub 0.26 ACCEPTABIL, peste REFA", AgainstReport.DepthScale());
        Assert.Equal(Th.ResidUsable, AgainstResult.DepthErrorOf(AgainstReport.StopsFor(Th.ResidUsable)), 9);
    }

    [Fact]
    public void Against_ReadsTheDepthDifferenceInStops()
    {
        var shallow = SyntheticFlatBuilder.Build(AdobeProfile(), 600, 400, 0).Flat;
        var deeper = new Img(400, 600);
        for (int i = 0; i < deeper.D.Length; i++) deeper.D[i] = shallow.D[i] * shallow.D[i];   // twice the depth in stops
        var a = AgainstAnalyzer.Analyze(deeper, shallow);
        Assert.Equal("PERFECT", a.Structure.Verdict);              // the difference is radial...
        Assert.InRange(a.Structure.VignetteStops, 0.6, 0.8);      // deeper / shallow = shallow: the synthetic's own depth
        Assert.Equal("REFA", a.DepthVerdict);                     // ...and far too deep: 0.6-0.8 stops = 52-74 % in the corner
        Assert.Equal("REFA", a.Verdict);                          // the worse of shape and depth
        Assert.InRange(a.DepthErrorPct, 50, 75);
        var sw = new StringWriter();
        AgainstReport.Write(a, "a", "b", sw);
        Assert.Contains("VERDICTUL DIFERENTEI: REFA", sw.ToString());
        Assert.Contains("forma     : PERFECT", sw.ToString());
        Assert.Contains("adancimea : REFA", sw.ToString());
    }
}

public class SyntheticRunTests
{
    static byte[] Reference(double focal = 20) =>
        FitsTestData.BuildFits(240, 160, FitsTestData.Radial(240, 160),
            extraCards: new[] { FitsTestData.Card("FOCALLEN", focal.ToString("0.0")), FitsTestData.Card("INSTRUME", "'Nikon D3300'"), FitsTestData.Card("BAYERPAT", "'RGGB'") });

    [Fact]
    public void PicksTheMountFromTheReferenceCamera_FillsFocalFromTheHeader_AndWritesACfaFloatMaster()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        string reference = ws.WriteFile("ref.fit", Reference());
        var req = new SyntheticRequest { LensText = "Tokina AT-X 11-20mm F2.8 PRO DX" };
        var sw = new StringWriter();

        var run = SyntheticRun.Execute(reference, req, sources, null, 2.8, null, sw);

        Assert.False(run.Refused);
        Assert.Equal("NIKON CORPORATION", req.Lens.Mount);
        Assert.Equal(20, req.FocalMm);
        Assert.Equal("FOCALLEN", req.FocalSource);
        Assert.Equal("--fstop", req.FStopSource);
        Assert.Contains("profil folosit     : 20 mm, f/2.8, infinit", sw.ToString());

        using var ms = new MemoryStream();
        SyntheticRun.WriteFits(run, reference, ms);
        var (img, hdr, cards, _, bitpix) = FitsReader.LoadFull(ms.ToArray());
        Assert.Equal(-32, bitpix);
        Assert.Equal(240, img.W);
        Assert.Equal("RGGB", FitsReader.HStr(hdr, "BAYERPAT", ""));
        Assert.Contains(cards, c => c.StartsWith("HISTORY") && c.Contains("--synthetic"));
        Assert.InRange(img.At(80, 120), 0.99, 1.0);
    }

    [Fact]
    public void LensfunNeedsTheCameraCrop_AndTakesItFromTheDatabaseByTheHeaderCamera()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        string reference = ws.WriteFile("ref.fit", Reference());
        var req = new SyntheticRequest { LensText = "Aspherical", Source = ProfileSource.Lensfun };

        var run = SyntheticRun.Execute(reference, req, sources, null, 2.8, null, new StringWriter());

        Assert.False(run.Refused);
        Assert.Equal(1.523, req.CameraCrop, 9);
        Assert.Contains("baza lensfun", req.CropSource);
        Assert.True(run.Result.CornerRadius > 1.0);

        // an unknown camera and no --crop: refused, with the reason
        string other = ws.WriteFile("other.fit", FitsTestData.BuildFits(240, 160, FitsTestData.Radial(240, 160),
            extraCards: new[] { FitsTestData.Card("FOCALLEN", "20.0"), FitsTestData.Card("INSTRUME", "'Canon EOS 7D'") }));
        var refused = SyntheticRun.Execute(other, new SyntheticRequest { LensText = "Aspherical" }, sources, null, 2.8, null, new StringWriter());
        Assert.True(refused.Refused);
        Assert.Contains("--crop", refused.Refusal);
        Assert.False(refused.RefusedChoice);

        // the same unknown camera with an Adobe profile: not refused, the profile's SFF stands in, and the report says so
        var adobeReq = new SyntheticRequest { LensText = "Tokina AT-X 11-20mm F2.8 PRO DX", Source = ProfileSource.Adobe };
        var sw = new StringWriter();
        var adobe = SyntheticRun.Execute(other, adobeReq, sources, null, 2.8, null, sw);
        Assert.False(adobe.Refused, adobe.Refusal);
        Assert.Equal(0, adobeReq.CameraCrop);
        Assert.Contains("necunoscut", adobeReq.CropSource);
        Assert.Contains("crop aparat        : necunoscut", sw.ToString());
        Assert.Contains("SFF 1.5", sw.ToString());
        // with the camera known, the crop is used and reported, and at 1.523 vs SFF 1.5 no format warning
        var known = new SyntheticRequest { LensText = "Tokina AT-X 11-20mm F2.8 PRO DX", Source = ProfileSource.Adobe };
        var sw2 = new StringWriter();
        var run2 = SyntheticRun.Execute(reference, known, sources, null, 2.8, null, sw2);
        Assert.False(run2.Refused);
        Assert.Equal(1.523, known.CameraCrop, 9);
        Assert.Contains("crop aparat        : 1.523  (baza lensfun, Nikon D3300)", sw2.ToString());
        Assert.DoesNotContain("alt format", sw2.ToString());
        Assert.True(run2.Result.CornerStops < adobe.Result.CornerStops);   // the real 23.6 mm sensor reaches a little less far out than 24 mm
        // a full-frame body on the DX-calibrated file: the format warning fires
        var ffReq = new SyntheticRequest { LensText = "Tokina AT-X 11-20mm F2.8 PRO DX", Source = ProfileSource.Adobe };
        var sw3 = new StringWriter();
        var ff = SyntheticRun.Execute(reference, ffReq, sources, null, 2.8, 1.0, sw3);
        Assert.False(ff.Refused);
        Assert.Contains("format             : profil calibrat pe APS-C (crop 1.5); senzorul tau full frame (1): alt format", sw3.ToString());
        // ...and the corner sits 1.5x beyond the calibration sensor's: a sixth-order polynomial
        // extrapolated that far is meaningless (it goes negative on this fixture), and the report says so
        Assert.InRange(ff.Result.Extrapolation, 1.45, 1.55);
        Assert.Contains("extrapolare MARE", sw3.ToString());
        Assert.InRange(run2.Result.Extrapolation, 0.97, 1.0);            // the D3300 sits just inside the DX calibration corner
        Assert.Equal("", SyntheticReport.ExtrapolationNote(run2.Result));
        Assert.Contains("format             : profil calibrat pe APS-C (crop 1.5); senzorul tau APS-C (1.523): acelasi format", sw2.ToString());
        Assert.Contains("format             : profil calibrat pe APS-C (crop 1.5); crop-ul tau nu se stie", sw.ToString());
    }

    [Theory]
    [InlineData(1.523, "APS-C")]
    [InlineData(1.5, "APS-C")]
    [InlineData(1.534, "APS-C")]
    [InlineData(1.61, "APS-C Canon")]
    [InlineData(1.0, "full frame")]
    [InlineData(1.005, "full frame")]
    [InlineData(2.0, "Micro 4/3")]
    [InlineData(1.15, null)]
    [InlineData(0.0, null)]
    public void SensorFormats_NearestLightsTheChipWithinFivePercent(double crop, string label)
    {
        Assert.Equal(label, SensorFormats.Nearest(crop)?.Label);
        Assert.Equal(label ?? SensorFormats.Unusual, SensorFormats.LabelOf(crop));
    }

    /// <summary>The one sentence both applications say about a profile beside the user's sensor.</summary>
    [Fact]
    public void FormatMatch_SaysTheCalibrationFormat_YoursAndWhatFollows()
    {
        var adobe = new VignettingModel(VignettingModelKind.Adobe, new[] { -0.5, 0.0, 0.0 }, null, 20, 1.5);
        string same = SyntheticReport.FormatMatch(adobe, 1.523, 6000, 4000);
        Assert.Equal("profil calibrat pe APS-C (crop 1.5); senzorul tau APS-C (1.523): acelasi format", same);
        string ff = SyntheticReport.FormatMatch(adobe, 1.0, 6000, 4000);
        Assert.StartsWith("profil calibrat pe APS-C (crop 1.5); senzorul tau full frame (1): alt format, raza se recalculeaza prin mm; coltul tau cade la 1.50 x coltul de calibrare: extrapolare MARE", ff);
        string smaller = SyntheticReport.FormatMatch(adobe, 2.0, 6000, 4000);
        Assert.Contains("Micro 4/3 (2): alt format", smaller);
        Assert.Contains("cade la 0.75 din coltul de calibrare, in interiorul profilului", smaller);
        Assert.Equal("profil calibrat pe APS-C (crop 1.5); crop-ul tau nu se stie, se ia senzorul de calibrare", SyntheticReport.FormatMatch(adobe, 0, 6000, 4000));
        var lensfun = new VignettingModel(VignettingModelKind.Lensfun, new[] { -0.5, 0.0, 0.0 }, null, 20, 1.61);
        string d3300 = SyntheticReport.FormatMatch(lensfun, 1.523, 6016, 4016);
        Assert.StartsWith("profil calibrat pe APS-C Canon (crop 1.61); senzorul tau APS-C (1.523): alt format, raza se recalculeaza prin mm; coltul tau cade la 1.06 x coltul de calibrare: extrapolare mica", d3300);
        Assert.Contains("format neobisnuit (1.15)", SyntheticReport.FormatMatch(adobe, 1.15, 6000, 4000));
    }

    [Fact]
    public void ABodySpecificAdobeFileBeatsTheGenericMountFile()
    {
        var generic = new LensChoice(ProfileSource.Adobe, "Tokina", "Tokina X", "NIKON CORPORATION");
        var body = new LensChoice(ProfileSource.Adobe, "Tokina", "Tokina X", "NIKON D750");
        var other = new LensChoice(ProfileSource.Adobe, "Tokina", "Tokina X", "NIKON D90");
        var lensfun = new LensChoice(ProfileSource.Lensfun, "Tokina", "Tokina X", "Nikon F AF");
        var all = new[] { generic, body, other, lensfun };
        Assert.Equal(new[] { body }, SyntheticRun.PreferCamera(all, "NIKON D750"));
        Assert.Equal(new[] { body }, SyntheticRun.PreferCamera(all, "Nikon D750"));      // case and spacing do not matter
        Assert.Equal(all, SyntheticRun.PreferCamera(all, "NIKON D3300"));                // no file of its own: nothing changes
        Assert.Equal(all, SyntheticRun.PreferCamera(all, null));
        Assert.Equal(new[] { body }, SyntheticRun.PreferCamera(new[] { body }, "Canon EOS 6D"));   // a single hit is never dropped
    }

    [Fact]
    public void TheRawsLensIdIsNarrowedByItsRange_ThenTheCameraPicksTheMount()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        var tokina = new LoadedFrame { Camera = "NIKON D3300", NikonLensId = 139, LensMinFocalMm = 11, LensMaxFocalMm = 20, LensMaxAperture = 2.8 };

        // the Adobe file on the camera's mount by ID, plus the lensfun Tokina of the same range:
        // lensfun carries no Nikon IDs, so it joins by maker and range
        var candidates = SyntheticRun.CandidatesFromRaw(sources.Catalog, tokina);
        Assert.Equal(2, candidates.Count);
        Assert.Equal(ProfileSource.Adobe, candidates[0].Source);
        Assert.Equal("NIKON CORPORATION", candidates[0].Mount);
        Assert.Equal(ProfileSource.Lensfun, candidates[1].Source);
        Assert.Contains("11-20", candidates[1].Name);

        // both sit on the Nikon mount, so the console lists them and asks for --source; with it, one is chosen
        string both = SyntheticRun.ChooseLens(new SyntheticRequest(), sources, tokina);
        Assert.Contains("2 obiective", both);
        var req = new SyntheticRequest { Source = ProfileSource.Adobe };
        Assert.Null(SyntheticRun.ChooseLens(req, sources, tokina));
        Assert.Equal("NIKON CORPORATION", req.Lens.Mount);
        Assert.Equal(ProfileSource.Adobe, req.Lens.Source);
        Assert.Equal("ID-ul Nikon 139 din raw (11-20 mm f/2.8)", SyntheticReport.RawLensLabel(tokina));

        // the same ID from a lens whose range every name contradicts: nothing is chosen, --lens is asked for
        var nikkor = new LoadedFrame { Camera = "NIKON D3300", NikonLensId = 139, LensMinFocalMm = 18, LensMaxFocalMm = 200, LensMaxAperture = 3.5 };
        Assert.Empty(SyntheticRun.CandidatesFromRaw(sources.Catalog, nikkor));
        string refusal = SyntheticRun.ChooseLens(new SyntheticRequest(), sources, nikkor);
        Assert.Contains("--lens", refusal);

        // no camera: both Adobe mounts and the lensfun entry survive, and the refusal names what the raw said
        var unknownCamera = new LoadedFrame { NikonLensId = 139, LensMinFocalMm = 11, LensMaxFocalMm = 20, LensMaxAperture = 2.8 };
        string ambiguous = SyntheticRun.ChooseLens(new SyntheticRequest(), sources, unknownCamera);
        Assert.Contains("ID-ul Nikon 139 din raw (11-20 mm f/2.8)", ambiguous);
        Assert.Contains("3 obiective", ambiguous);
    }

    [Fact]
    public void AnAmbiguousOrUnknownLensIsAChoiceRefusal_NotAnInputOne()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        string reference = ws.WriteFile("ref.fit", Reference());

        var many = SyntheticRun.Execute(reference, new SyntheticRequest { LensText = "11-20" }, sources, null, 2.8, null, new StringWriter());
        Assert.True(many.Refused);
        Assert.True(many.RefusedChoice);           // the Nikon mount filter leaves Adobe-Nikon and lensfun (Nikon F AF)

        var none = SyntheticRun.Execute(reference, new SyntheticRequest { LensText = "Zeiss" }, sources, null, 2.8, null, new StringWriter());
        Assert.True(none.Refused);
        Assert.True(none.RefusedChoice);

        var noText = SyntheticRun.Execute(reference, new SyntheticRequest(), sources, null, 2.8, null, new StringWriter());
        Assert.True(noText.Refused);
        Assert.True(noText.RefusedChoice);
    }

    [Fact]
    public void WithoutAnFStopFromAnywhere_ItRefusesAndSaysSo()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        string reference = ws.WriteFile("ref.fit", Reference());

        var run = SyntheticRun.Execute(reference, new SyntheticRequest { LensText = "Tokina AT-X 11-20mm F2.8 PRO DX" }, sources, null, null, null, new StringWriter());

        Assert.True(run.Refused);
        Assert.False(run.RefusedChoice);
        Assert.Contains("--fstop", run.Refusal);
    }

    [Fact]
    public void TheNearestProfileIsReportedWhenTheRequestFallsBetweenEntries()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        string reference = ws.WriteFile("ref.fit", Reference(focal: 19));
        var sw = new StringWriter();

        var run = SyntheticRun.Execute(reference, new SyntheticRequest { LensText = "Tokina AT-X 11-20mm F2.8 PRO DX" }, sources, null, 3.2, null, sw);

        Assert.False(run.Refused);
        Assert.Contains("(cerut: 19 mm din FOCALLEN, f/3.2 din --fstop)", sw.ToString());
        Assert.True(run.Result.Profile.FocalMm == 18 || run.Result.Profile.FocalMm == 20);
    }
}
