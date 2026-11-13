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
    /// R-072: the vertical centre counts in heights of the calibration image, the horizontal one
    /// in widths. Adobe's document says "v0 / Dmax" for both, but the installed profiles carry
    /// about 0.5 for a centred axis on 3:2 and on 4:3 bodies alike, where v0 / Dmax would be
    /// 0.333 and 0.375 (ADR 0106). Before, the vertical shift was 1.5 times too large on 3:2.
    /// </summary>
    [Fact]
    public void R072_TheVerticalCentreCountsInHeightsOfTheCalibrationImage()
    {
        double fx = 20 * 1.5 / 36;                                     // 20 mm on a 24 mm long side
        var inLongSides = new VignettingModel(VignettingModelKind.Adobe, new[] { -0.5, 0.0, 0.0 }, null, 20, 1.5,
            focalX: fx, focalY: fx, centerX: 0.5, centerY: 0.6);
        var inHeights = new VignettingModel(VignettingModelKind.Adobe, new[] { -0.5, 0.0, 0.0 }, null, 20, 1.5,
            focalX: fx, focalY: fx, centerX: 0.5, centerY: 0.6, centerXUnit: 1.0, centerYUnit: 2.0 / 3);

        Assert.Equal(0.1 * 24 / 20, inLongSides.Radius(0, 0, 1.5), 12);            // 2.4 mm off the axis
        Assert.Equal(0.1 * 16 / 20, inHeights.Radius(0, 0, 1.5), 12);              // 1.6 mm: a tenth of the 16 mm height
        Assert.Equal(0, inHeights.Radius(0, 0.1 * 16 / 24, 1.5), 12);              // the axis itself, 1.6 mm below the centre
        // the horizontal centre is untouched by the vertical unit
        var sideways = new VignettingModel(VignettingModelKind.Adobe, new[] { -0.5, 0.0, 0.0 }, null, 20, 1.5,
            focalX: fx, focalY: fx, centerX: 0.4, centerY: 0.5, centerXUnit: 1.0, centerYUnit: 2.0 / 3);
        Assert.Equal(0.1 * 24 / 20, sideways.Radius(0, 0, 1.5), 12);

        // the profile the review of 2026-11-13 named: a 3:2 body of SFF 1.565304, 5202 x 3465
        var eos50d = new VignettingModel(VignettingModelKind.Adobe, new[] { -0.36, 0.0, 0.0 }, null, 35, 1.565304,
            centerX: 0.5, centerY: 0.536912, centerXUnit: 1.0, centerYUnit: 3465.0 / 5202);
        Assert.Equal(0.5655, eos50d.Radius(0, 0, 1.565304) * 35, 4);               // mm; 0.8489 when counted in long sides
    }

    /// <summary>R-072: a centred profile is the same model whatever the image's shape, and a
    /// portrait calibration image turns the two units round.</summary>
    [Fact]
    public void R072_ACentredProfileDoesNotDependOnTheUnits_AndAPortraitImageSwapsThem()
    {
        var plain = new VignettingModel(VignettingModelKind.Adobe, new[] { -0.5, 0.1, 0.0 }, null, 20, 1.5);
        var shaped = new VignettingModel(VignettingModelKind.Adobe, new[] { -0.5, 0.1, 0.0 }, null, 20, 1.5, centerXUnit: 1.0, centerYUnit: 2.0 / 3);
        foreach (var (x, y) in new[] { (0.0, 0.0), (0.5, 0.33), (-0.31, 0.2), (0.12, -0.33) })
            Assert.Equal(plain.Factor(x, y, 1.523), shaped.Factor(x, y, 1.523));

        double fx = 20 * 1.5 / 36;
        var portrait = new VignettingModel(VignettingModelKind.Adobe, new[] { -0.5, 0.0, 0.0 }, null, 20, 1.5,
            focalX: fx, focalY: fx, centerX: 0.6, centerY: 0.5, centerXUnit: 2.0 / 3, centerYUnit: 1.0);
        Assert.Equal(0.1 * 16 / 20, portrait.Radius(0, 0, 1.5), 12);               // a tenth of the 16 mm width

        Assert.Throws<ArgumentException>(() => new VignettingModel(VignettingModelKind.Adobe, new[] { -0.5, 0.0, 0.0 }, null, 20, 1.5, centerYUnit: 0));
        Assert.Throws<ArgumentException>(() => new VignettingModel(VignettingModelKind.Adobe, new[] { -0.5, 0.0, 0.0 }, null, 20, 1.5, centerXUnit: 1.5));
    }

    /// <summary>
    /// R-072: the reader takes the image's shape from the entry's ImageWidth and ImageLength,
    /// written as attributes or as elements, and leaves the units at the long side when the
    /// entry gives none.
    /// </summary>
    [Fact]
    public void R072_TheReaderHandsTheCalibrationImagesShapeToTheModel()
    {
        string plain = LensProfileTestData.LcpOneProfile("NIKON CORPORATION", "Lens 35mm f/1.8", "35mm f/1.8", "159", "1.5", "35")
            .Replace("<stCamera:VignetteModel stCamera:VignetteModelParam1=", "<stCamera:VignetteModel stCamera:ImageXCenter=\"0.492\" stCamera:ImageYCenter=\"0.5156\" stCamera:VignetteModelParam1=");
        Assert.Contains("ImageYCenter", plain);

        VignettingModel noSize = Assert.Single(LcpReader.ReadXml(plain, "a.lcp")).Model;
        Assert.Equal(0.5156, noSize.CenterY, 9);
        Assert.Equal(1.0, noSize.CenterXUnit);
        Assert.Equal(1.0, noSize.CenterYUnit);

        string sized = plain.Replace("stCamera:SensorFormatFactor=", "stCamera:ImageWidth=\"4310\" stCamera:ImageLength=\"2868\" stCamera:SensorFormatFactor=");
        VignettingModel landscape = Assert.Single(LcpReader.ReadXml(sized, "b.lcp")).Model;
        Assert.Equal(1.0, landscape.CenterXUnit);
        Assert.Equal(2868.0 / 4310, landscape.CenterYUnit, 12);
        Assert.Equal(0.492, landscape.CenterX, 9);

        string tall = plain.Replace("stCamera:SensorFormatFactor=", "stCamera:ImageWidth=\"2868\" stCamera:ImageLength=\"4310\" stCamera:SensorFormatFactor=");
        VignettingModel portrait = Assert.Single(LcpReader.ReadXml(tall, "c.lcp")).Model;
        Assert.Equal(2868.0 / 4310, portrait.CenterXUnit, 12);
        Assert.Equal(1.0, portrait.CenterYUnit);

        // the other spelling, properties as elements, which the installed profiles with a
        // centre use (the Canon EOS 50D and the Nikon D90 files)
        const string elements = @"<x:xmpmeta xmlns:x='adobe:ns:meta/'>
 <rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'>
  <rdf:Description rdf:about='' xmlns:photoshop='http://ns.adobe.com/photoshop/1.0/' xmlns:stCamera='http://ns.adobe.com/photoshop/1.0/camera-profile'>
   <photoshop:CameraProfiles>
    <rdf:Seq>
     <rdf:li rdf:parseType='Resource'>
      <stCamera:Make>Canon</stCamera:Make>
      <stCamera:CameraRawProfile>True</stCamera:CameraRawProfile>
      <stCamera:LensPrettyName>Lens 17-55mm f/2.8</stCamera:LensPrettyName>
      <stCamera:SensorFormatFactor>1.565304</stCamera:SensorFormatFactor>
      <stCamera:ImageWidth>5202</stCamera:ImageWidth>
      <stCamera:ImageLength>3465</stCamera:ImageLength>
      <stCamera:FocalLength>17</stCamera:FocalLength>
      <stCamera:ApertureValue>2.970854</stCamera:ApertureValue>
      <stCamera:PerspectiveModel rdf:parseType='Resource'>
       <stCamera:VignetteModel rdf:parseType='Resource'>
        <stCamera:ImageXCenter>0.4950</stCamera:ImageXCenter>
        <stCamera:ImageYCenter>0.5156</stCamera:ImageYCenter>
        <stCamera:VignetteModelParam1>-0.3603</stCamera:VignetteModelParam1>
       </stCamera:VignetteModel>
      </stCamera:PerspectiveModel>
     </rdf:li>
    </rdf:Seq>
   </photoshop:CameraProfiles>
  </rdf:Description>
 </rdf:RDF>
</x:xmpmeta>";
        VignettingModel asElements = Assert.Single(LcpReader.ReadXml(elements, "d.lcp")).Model;
        Assert.Equal(0.5156, asElements.CenterY, 9);
        Assert.Equal(1.0, asElements.CenterXUnit);
        Assert.Equal(3465.0 / 5202, asElements.CenterYUnit, 12);
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
        Assert.Equal((0.1 * 24) / 20, shifted.Radius(0.0, 0, 1.0), 9);
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

        Assert.Equal(4, d.Profiles.Count);                       // 18/2.8 at 10 m, 20/2.8 (the copied pair, one profile), 20/4 at 10 m and at 1000 m
        Assert.All(d.Profiles, p => Assert.Equal(ProfileSource.Lensfun, p.Lens.Source));
        var p = d.Profiles.Single(q => q.FocalMm == 20 && q.FStop == 2.8);
        Assert.True(double.IsNaN(p.DistanceM));                  // 10 m and 1000 m with the same coefficients = one measurement, distance unknown
        Assert.EndsWith(LensProfile.DistanceUnspecified, p.Settings);
        Assert.Equal(10, d.Profiles.Single(q => q.FocalMm == 18).DistanceM);      // a single stated distance stays
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

    /// <summary>A lensfun document of cameras only, each row "model|alias|crop" or
    /// "model|alias|crop|maker" (the alias may be empty), in the order given. Without a maker
    /// in the row it is the model's first word, as in lensfun's own entries ("Nikon D750").</summary>
    static LensfunData CameraTable(params string[] rows)
    {
        var xml = new System.Text.StringBuilder("<lensdatabase version=\"2\">");
        foreach (string row in rows)
        {
            string[] p = row.Split('|');
            string maker = p.Length > 3 ? p[3] : p[0].Split(' ')[0];
            xml.Append("<camera><maker>").Append(maker).Append("</maker><model>").Append(p[0]).Append("</model>");
            if (p[1].Length > 0) xml.Append("<model lang=\"en\">").Append(p[1]).Append("</model>");
            xml.Append("<mount>Mount of ").Append(p[0]).Append("</mount><cropfactor>").Append(p[2]).Append("</cropfactor></camera>");
        }
        return LensfunReader.ReadXml(xml.Append("</lensdatabase>").ToString(), "cameras.xml");
    }

    /// <summary>The pairs the review of 2026-11-13 found crossed (CR-001): each short name
    /// beside the longer one that contains it, and the Pentax Q with its one-letter alias.</summary>
    static LensfunData CrossedCameras() => CameraTable(
        "Nikon D7500|D7500|1.534", "Nikon D750|D750|1.0",
        "Nikon D7000|D7000|1.523", "Nikon D700|D700|1.0",
        "Nikon D800E|D800E|1.0", "Nikon D80|D80|1.523",
        "Nikon Z fc|Z fc|1.531", "Nikon Z f|Z f|1.0",
        "Canon EOS R50 V||1.613", "Canon EOS R5||1.0",
        "Pentax K100D Super||1.531", "Pentax K-1||1.0",
        "Pentax Q|Q|5.53");

    /// <summary>
    /// R-133: the camera is the table's entry of that very name, or the one whose words all
    /// stand in the file's name; a longer table name never wins over the exact one, a part of a
    /// word is no match, and a name with nothing to compare finds nothing.
    /// </summary>
    [Theory]
    [InlineData("Nikon D750", "Nikon D750")]
    [InlineData("NIKON D750", "Nikon D750")]
    [InlineData("Nikon D7500", "Nikon D7500")]
    [InlineData("Nikon D700", "Nikon D700")]
    [InlineData("Nikon D80", "Nikon D80")]
    [InlineData("NIKON Z f", "Nikon Z f")]
    [InlineData("Nikon Z fc", "Nikon Z fc")]
    [InlineData("Canon EOS R5", "Canon EOS R5")]
    [InlineData("Pentax K-1", "Pentax K-1")]
    [InlineData("PENTAX Q", "Pentax Q")]
    [InlineData("D750", "Nikon D750")]                              // the alias, exactly
    [InlineData("Nikon Corporation NIKON D750", "Nikon D750")]      // the table's words inside the file's name
    [InlineData("QHY5III678C", null)]                               // "Q" is a word of its own, not a letter of another
    [InlineData("Nikon D75", null)]                                 // a table name that only contains the file's name
    [InlineData("Nikon D9999", null)]
    [InlineData("---", null)]                                       // nothing to compare
    [InlineData("", null)]
    public void R133_TheCameraIsTheTablesEntryOfThatName_NeverALongerOne(string camera, string model)
    {
        LensfunData d = CrossedCameras();
        Assert.Equal(model, d.CameraOf(camera)?.Model);
        if (model == null)
        {
            Assert.Null(d.CropOf(camera));
            Assert.Null(d.MountsOf(camera));
        }
        else Assert.Equal(new[] { "Mount of " + model }, d.MountsOf(camera));
    }

    /// <summary>R-133 on the figures: the D750 is a full-frame body, the Z f too, the D80 is
    /// not; before, each took the crop of the longer name.</summary>
    [Fact]
    public void R133_TheCropIsTheBodysOwn()
    {
        LensfunData d = CrossedCameras();
        Assert.Equal(1.0, d.CropOf("NIKON D750")!.Value, 9);
        Assert.Equal(1.523, d.CropOf("NIKON D80")!.Value, 9);
        Assert.Equal(1.0, d.CropOf("NIKON Z f")!.Value, 9);
        Assert.Equal(1.0, d.CropOf("Canon EOS R5")!.Value, 9);
        Assert.Equal(1.0, d.CropOf("PENTAX K-1")!.Value, 9);
    }

    /// <summary>
    /// R-133: of two table names that both stand in the file's name, the one with the most
    /// letters is the camera, whatever their order in the table; and of two entries that carry
    /// one name, the first.
    /// </summary>
    [Fact]
    public void R133_OfTwoNamesInsideTheLongerWins_AndOfTwoEntriesWithOneNameTheFirst()
    {
        LensfunData shortFirst = CameraTable("Canon EOS-1D||1.255", "Canon EOS-1D X||1.0");
        LensfunData longFirst = CameraTable("Canon EOS-1D X||1.0", "Canon EOS-1D||1.255");
        Assert.Equal("Canon EOS-1D X", shortFirst.CameraOf("Canon EOS-1D X Mark III")?.Model);
        Assert.Equal("Canon EOS-1D X", longFirst.CameraOf("Canon EOS-1D X Mark III")?.Model);
        Assert.Equal(1.0, shortFirst.CropOf("Canon EOS-1D X Mark III")!.Value, 9);

        LensfunData twice = CameraTable("DMC-LX3||4.7|Panasonic", "DMC-LX3||5.1|Panasonic");
        Assert.Equal(4.7, twice.CropOf("DMC-LX3")!.Value, 9);
        Assert.Equal(4.7, twice.CropOf("Panasonic DMC-LX3")!.Value, 9);
    }

    /// <summary>
    /// R-133: a table name found inside the file's name is the camera only when the file's name
    /// also carries that entry's maker. Found by the review of the branch: a "Canon EOS Kiss
    /// X10" (the Japanese 250D, not in the table) holds the word "X10", which is a Fujifilm
    /// compact with crop 3.93. The exact name needs no maker.
    /// </summary>
    [Fact]
    public void R133_ANameOfAnotherMakerFoundInsideIsNotTheCamera()
    {
        LensfunData d = CameraTable("X10||3.93|Fujifilm", "Canon EOS Kiss X10i||1.613", "M10||1.0|Leica Camera AG", "D60||1.523|Nikon Corporation");

        Assert.Null(d.CameraOf("Canon EOS Kiss X10"));              // not Fujifilm's X10
        Assert.Null(d.CropOf("Canon EOS Kiss X10"));
        Assert.Null(d.MountsOf("Canon EOS Kiss X10"));
        Assert.Null(d.CameraOf("Canon EOS M10"));                   // not Leica's M10: "Leica" is not in the name
        Assert.Null(d.CameraOf("Canon EOS D60"));                   // not Nikon's D60
        Assert.Equal("X10", d.CameraOf("X10")?.Model);              // the exact name, without a maker
        Assert.Equal("X10", d.CameraOf("FUJIFILM X10")?.Model);     // the maker in front, as a raw gives it
        Assert.Equal("M10", d.CameraOf("LEICA M10")?.Model);
        Assert.Equal("D60", d.CameraOf("NIKON CORPORATION NIKON D60")?.Model);
    }

    /// <summary>
    /// R-133: the maker is carried by any word of its names but the generic ones. lensfun says
    /// "Konica Minolta" where a raw says "Minolta" (the second review of the branch: with the
    /// first word alone, 14 bodies of the shipped table lost their crop), and "Corporation" in
    /// a file's name is nobody's maker.
    /// </summary>
    [Fact]
    public void R133_AnyWordOfTheMakerCarriesIt_ExceptTheGenericOnes()
    {
        LensfunData d = CameraTable("Dynax 5D||1.531|Konica Minolta", "K10||1.5|Pentax Corporation", "S1||2.0|Olympus Imaging Corp.");

        Assert.Equal("Dynax 5D", d.CameraOf("Minolta DYNAX 5D")?.Model);
        Assert.Equal("Dynax 5D", d.CameraOf("KONICA MINOLTA DYNAX 5D")?.Model);
        Assert.Equal("K10", d.CameraOf("PENTAX K10")?.Model);
        Assert.Null(d.CameraOf("NIKON CORPORATION K10"));           // "Corporation" is not Pentax
        Assert.Null(d.CameraOf("Ricoh Imaging S1"));                // "Imaging" is not Olympus
        Assert.Null(d.CameraOf("Leica Corp. S1"));
        Assert.Equal("S1", d.CameraOf("OLYMPUS S1")?.Model);
    }

    /// <summary>
    /// R-133: the generic words are the ones the shipped table's maker names hold, and every
    /// maker there keeps a word that is its own. A lensfun update that brings a maker word not
    /// seen before fails here, so that someone decides whether it is a brand or a generic word.
    /// </summary>
    [Fact]
    public void R133_TheGenericMakerWordsAreTheOnesOfTheShippedTable()
    {
        LensfunData d = LensfunReader.ReadDirectory(Path.Combine(TestData.RepoDir(), ProfileLocator.LensfunFolderName));
        static IEnumerable<string> WordsOf(string s) => System.Text.RegularExpressions.Regex.Matches(s.ToUpperInvariant(), @"[\p{L}\p{Nd}]+").Select(m => m.Value);
        var makerNames = d.Cameras.SelectMany(c => c.MakerNames).Distinct().ToList();
        var words = makerNames.SelectMany(WordsOf).Distinct().ToList();

        // every maker keeps a word of its own
        Assert.All(makerNames, n => Assert.Contains(WordsOf(n), w => !LensfunData.GenericMakerWords.Contains(w)));
        // the generic words met in the table, by name
        Assert.Equal(new[] { "AG", "CAMERA", "CO", "COMPANY", "COMPUTER", "CORP", "CORPORATION", "DIGITAL", "DV", "IMAGING", "INC", "LTD",
                             "MOBILE", "ONE", "OPTICAL", "SOLUTIONS", "SYSTEM", "TECHNOLOGY" },
                     words.Where(LensfunData.GenericMakerWords.Contains).OrderBy(w => w, StringComparer.Ordinal));
        // and the words that count as a maker's own: 41 on 2026-11-13
        var own = words.Where(w => !LensfunData.GenericMakerWords.Contains(w)).OrderBy(w => w, StringComparer.Ordinal).ToList();
        Assert.True(own.Count == 41, "the maker words that are not generic changed: " + string.Join(" ", own));
        Assert.Contains("MINOLTA", own);
        Assert.Contains("KODAK", own);
        Assert.Contains("OM", own);
    }

    /// <summary>R-133: any of the entry's maker names counts, the canonical one or an alias:
    /// lensfun's "Eastman Kodak Company" is "Kodak" in a file.</summary>
    [Fact]
    public void R133_TheMakerMayBeAnyOfTheEntrysMakerNames()
    {
        LensfunData d = LensfunReader.ReadXml(@"<lensdatabase version=""2"">
<camera><maker>Eastman Kodak Company</maker><maker lang=""en"">Kodak</maker><model>DCS Pro 14n</model><mount>Nikon F AF</mount><cropfactor>1.0</cropfactor></camera>
</lensdatabase>", "kodak.xml");

        LensfunCamera cam = Assert.Single(d.Cameras);
        Assert.Equal(new[] { "Eastman Kodak Company", "Kodak" }, cam.MakerNames);
        Assert.Same(cam, d.CameraOf("Kodak DCS Pro 14n"));
        Assert.Same(cam, d.CameraOf("EASTMAN KODAK DCS Pro 14n"));
        Assert.Null(d.CameraOf("Nikon DCS Pro 14n"));
        Assert.Same(cam, d.CameraOf("DCS Pro 14n"));                // exact
    }

    /// <summary>R-133 on the shipped table: every camera, asked for by each of its names, is
    /// found in an entry that carries that very name (1053 cameras on 2026-11-13, of which the
    /// rule before sent 137 names to a body with another crop).</summary>
    [Fact]
    public void R133_EveryCameraOfTheShippedTableIsFoundUnderItsOwnName()
    {
        LensfunData d = LensfunReader.ReadDirectory(Path.Combine(TestData.RepoDir(), ProfileLocator.LensfunFolderName));
        Assert.True(d.Cameras.Count > 1000, "the shipped lensfun folder was not read");
        static string Letters(string s) => new string(s.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        var wrong = new List<string>();
        foreach (LensfunCamera c in d.Cameras)
            foreach (string name in new[] { c.Model }.Concat(c.Aliases))
            {
                LensfunCamera found = d.CameraOf(name);
                bool same = found != null && new[] { found.Model }.Concat(found.Aliases).Any(n => Letters(n) == Letters(name));
                if (!same) wrong.Add($"{name} -> {found?.Model ?? "nothing"}");

                // and with the maker's first word in front, as a raw names its camera when the
                // model does not begin with it ("Sony ILCE-7M3"): the name is then found inside,
                // which asks for the maker. A body is found, and it is the entry of this very name
                // (the first of the aspect-ratio twins that share one, as above) or one with this
                // body's crop: "Pentax" before the alias "K-5 IIs" also holds "Pentax K-5", the
                // longer name, a sibling of the same format.
                string brand = new string(c.Maker.TakeWhile(char.IsLetterOrDigit).ToArray());
                if (brand.Length == 0 || Letters(name).StartsWith(Letters(brand))) continue;
                LensfunCamera branded = d.CameraOf(brand + " " + name);
                bool sameBranded = branded != null
                    && (new[] { branded.Model }.Concat(branded.Aliases).Any(n => Letters(n) == Letters(name))
                        || Math.Abs(branded.CropFactor - c.CropFactor) <= 0.03 * c.CropFactor);
                if (!sameBranded) wrong.Add($"{brand} {name} -> {branded?.Model ?? "nothing"}");
            }
        Assert.True(wrong.Count == 0, string.Join("; ", wrong.Take(20)));
    }

    /// <summary>
    /// The shipped database holds the Nikkor 35 f/1.8G DX twice - once calibrated on an APS-C
    /// body, once on a D780 - with the same maker, model and mount. They are two lenses in the
    /// catalog, told apart by the calibration format, each with its own coefficients. Before
    /// the crop entered LensChoice the two collapsed into one and Nearest took the first in
    /// file order: the APS-C entry, 0.19 stops at the DX corner where the D780 one and Adobe
    /// read 0.51 and 0.61.
    /// </summary>
    [Fact]
    public void Lensfun_TheSameLensCalibratedOnTwoFormatsIsTwoLenses()
    {
        var d = LensfunReader.ReadXml(LensProfileTestData.LensfunNikkor35Xml, "slr-nikon.xml");

        var lenses = d.Profiles.Select(p => p.Lens).Distinct().ToList();
        Assert.Equal(2, lenses.Count);
        Assert.All(lenses, l => Assert.Equal("Nikon AF-S DX Nikkor 35mm f/1.8G", l.Name));
        Assert.All(lenses, l => Assert.Equal("Nikon F AF", l.Mount));
        Assert.Equal(new[] { "APS-C 1.528", "full frame 1" }, lenses.Select(l => l.Calibration).OrderBy(c => c));
        Assert.NotEqual(lenses[0].Key, lenses[1].Key);

        LensChoice dx = lenses.Single(l => l.Calibration == "APS-C 1.528");
        LensChoice ff = lenses.Single(l => l.Calibration == "full frame 1");
        Assert.Equal("Nikon F AF, calibrat pe APS-C 1.528", dx.Where);
        Assert.Equal("Nikon AF-S DX Nikkor 35mm f/1.8G (calibrat pe full frame 1)", ff.Title);
        Assert.Contains("[Nikon F AF, calibrat pe full frame 1] (lensfun)", ff.ToString());

        var atDx = d.Profiles.Single(p => p.Lens == dx && p.FStop == 2.8);
        var atFf = d.Profiles.Single(p => p.Lens == ff && p.FStop == 2.8);
        Assert.Equal(1.528, atDx.Model.CropFactor, 9);
        Assert.Equal(1.0, atFf.Model.CropFactor, 9);
        Assert.Equal(-0.1367, atDx.Model.Coefficients[0], 9);
        Assert.Equal(-0.1581, atFf.Model.Coefficients[0], 9);
    }

    /// <summary>
    /// An Adobe stub carries no calibration: the body is in the mount already, the SFF only in the
    /// file.
    /// </summary>
    [Fact]
    public void AnAdobeLensHasNoCalibrationLabel_SoItsWhereIsTheMount()
    {
        var stub = AdobeProfileFile.FromName("C:/p/Nikon/NIKON D90 (Nikon AF-S DX NIKKOR 35mm f1.8G) - RAW.lcp");   // slashes: splits on Unix too
        Assert.False(stub.Lens.HasCalibration);
        Assert.Equal("NIKON D90", stub.Lens.Where);
        Assert.Equal(stub.Lens.Name, stub.Lens.Title);
        Assert.Equal("Adobe|Nikon|Nikon AF-S DX NIKKOR 35mm f1.8G|NIKON D90", stub.Lens.Key);
    }

    [Fact]
    public void Locate_MergesBothSourcesAndReportsWhereItLooked()
    {
        using var ws = new TempWorkspace();
        var s = LensProfileTestData.Sources(ws);

        Assert.Equal(2, s.AdobeFiles);
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

    /// <summary>
    /// R-107: a subfolder the user may not list loses its own files only; the other subfolders
    /// of the same root are still read. Before, the enumeration threw inside the root's try
    /// and the whole root, 3500 files on the author's machine, was left out (the review of
    /// 2026-10-27, A7). The folder is made unlistable with a deny rule.
    /// </summary>
    [Fact]
    public void R107_AnUnlistableSubfolderLosesItsOwnFilesOnly_NotTheWholeRoot()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var ws = new TempWorkspace();
        string dir = LensProfileTestData.ProfilesDir(ws);
        string locked = Path.Combine(dir, "Locked");
        Directory.CreateDirectory(locked);
        File.WriteAllText(Path.Combine(locked, "Canon (Canon EF 50mm f1.4 USM) - RAW.lcp"),
            LensProfileTestData.LcpOneProfile("Canon", "Canon EF 50mm f/1.4 USM", "EF50mm f/1.4 USM", "198", "1", "50"));
        using var deny = UnlistableFolder.Deny(locked);

        var s = ProfileLocator.Locate(new[] { dir }, Path.Combine(dir, "lensfun"));

        Assert.Equal(2, s.AdobeFiles);
        Assert.Equal(2, s.AdobeLenses);
        Assert.DoesNotContain(s.Catalog.Lenses, l => l.Name.Contains("50mm"));
    }

    /// <summary>
    /// R-107: an Adobe root the user may not list at all is left out whole, with a Warn line
    /// naming it, as the design note and ADR 0066 say; the enumeration's IgnoreInaccessible
    /// must not swallow the root itself in silence (the review's finding of 2026-10-30).
    /// </summary>
    [Fact]
    public void R107_AnUnlistableRootIsLeftOutWithAWarnLine()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var ws = new TempWorkspace();
        string dir = LensProfileTestData.ProfilesDir(ws);
        string root = Path.Combine(ws.Dir, "locked-root");
        Directory.CreateDirectory(Path.Combine(root, "Canon"));
        File.WriteAllText(Path.Combine(root, "Canon", "Canon (Canon EF 50mm f1.4 USM) - RAW.lcp"),
            LensProfileTestData.LcpOneProfile("Canon", "Canon EF 50mm f/1.4 USM", "EF50mm f/1.4 USM", "198", "1", "50"));
        using var deny = UnlistableFolder.Deny(root);
        using var capture = LogCapture.Start();

        var s = ProfileLocator.Locate(new[] { root, dir }, Path.Combine(dir, "lensfun"));

        Assert.Equal(2, s.AdobeFiles);   // the second root's two, nothing of the first
        var warned = Assert.Single(capture.Events, e => e.Level == NLog.LogLevel.Warn && e.Message.Contains("could not be listed"));
        Assert.Contains(root, warned.Message);
    }

    /// <summary>
    /// R-115: a lensfun/ folder the user may not list (a deny rule, broken permissions) does not
    /// fail the search: it is left out whole with a Warn line naming it, as an Adobe root is, the
    /// sources mark it unreadable rather than absent, and the Adobe half still comes. Before,
    /// Locate threw from the folder's enumeration and every mode that locates the profiles
    /// failed with it (2026-10-30).
    /// </summary>
    [Fact]
    public void R115_AnUnlistableLensfunFolderIsLeftOutWithAWarnLine_NotAFailedSearch()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var ws = new TempWorkspace();
        string dir = LensProfileTestData.ProfilesDir(ws);
        string lensfun = Path.Combine(dir, "lensfun");
        using var deny = UnlistableFolder.Deny(lensfun);
        using var capture = LogCapture.Start();

        var s = ProfileLocator.Locate(new[] { dir }, lensfun);

        Assert.Equal(2, s.AdobeFiles);
        Assert.Equal(0, s.LensfunLenses);
        Assert.False(s.LensfunPresent);
        Assert.True(s.LensfunUnreadable);
        Assert.Null(s.LensfunVersion);   // the file can be opened by name past the deny; it is skipped on purpose
        var warned = Assert.Single(capture.Events, e => e.Level == NLog.LogLevel.Warn && e.Message.Contains("could not be listed"));
        Assert.Contains(lensfun, warned.Message);
        Assert.IsType<UnauthorizedAccessException>(warned.Exception);
    }

    /// <summary>R-115: the lensfun line of the summary tells an unreadable folder from a missing
    /// or empty one, in both languages; a folder read, lenses or not, gets no suffix.</summary>
    [Fact]
    public void R115_SourcesSummary_TellsAnUnreadableLensfunFolderFromAMissingOne()
    {
        static ProfileSources Sources(bool present, bool unreadable)
            => new(new[] { "x" }, 10, 0, "lf", present, unreadable, null, LensCatalog.Empty, LensfunData.Empty);
        using (Ui.Use(UiLanguage.English))
        {
            Assert.EndsWith("0 lenses from lf - the folder could not be read", SyntheticReport.SourcesSummary(Sources(false, true)));
            Assert.EndsWith("0 lenses from lf - the folder is missing or empty", SyntheticReport.SourcesSummary(Sources(false, false)));
            Assert.EndsWith("0 lenses from lf", SyntheticReport.SourcesSummary(Sources(true, false)));
        }
        using (Ui.Use(UiLanguage.Romanian))
        {
            Assert.EndsWith("0 obiective din lf - folderul nu a putut fi citit", SyntheticReport.SourcesSummary(Sources(false, true)));
            Assert.EndsWith("0 obiective din lf - folderul lipseste sau e gol", SyntheticReport.SourcesSummary(Sources(false, false)));
        }
    }

    /// <summary>
    /// A file that parses to no vignetting profile (the A3 lens) is a Debug line when the
    /// catalog parses it, so a report that says "no vignetting profile" can be traced to the
    /// file (the review of 2026-10-27, A8).
    /// </summary>
    [Fact]
    public void AFileThatParsesToNoProfileIsADebugLineInTheLog()
    {
        using var ws = new TempWorkspace();
        string dir = LensProfileTestData.ProfilesDir(ws);
        string path = Path.Combine(dir, "Tokina", "Canon (Canon RF 28mm F2.8 STM) - RAW.lcp");
        File.WriteAllText(path, LensProfileTestData.LcpWithoutVignette("Canon", "Canon RF 28mm F2.8 STM", "RF28mm F2.8 STM", "61182", "1", "28"));
        var s = ProfileLocator.Locate(new[] { dir }, Path.Combine(dir, "lensfun"));
        using var capture = LogCapture.Start();

        var profiles = s.Catalog.ProfilesOf(s.Catalog.Lenses.Single(l => l.Name.Contains("28mm")));

        Assert.Empty(profiles);
        var line = Assert.Single(capture.Events, e => e.Level == NLog.LogLevel.Debug && e.Message.Contains("no vignetting profile"));
        Assert.Contains(path, line.Message);
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

    /// <summary>
    /// Lightroom's folder repeats the DNG Converter's files under the same relative names: the
    /// second copy is skipped (whatever its case), so the lens is listed once and its profiles
    /// come from the first folder; a file only the second folder has is still found.
    /// </summary>
    [Fact]
    public void R107_Locate_ReadsTheSameRelativeNameOnceFromTheFirstFolder()
    {
        using var ws = new TempWorkspace();
        string first = Path.Combine(ws.Dir, "first");
        string second = Path.Combine(ws.Dir, "second");
        Directory.CreateDirectory(Path.Combine(first, "Tokina"));
        Directory.CreateDirectory(Path.Combine(second, "TOKINA"));
        const string nikon = "NIKON CORPORATION (Tokina AT-X 11-20mm F2.8 PRO DX) - RAW.lcp";
        File.WriteAllText(Path.Combine(first, "Tokina", nikon), LensProfileTestData.Lcp());
        // The copy is not even XML: were it read, the lens would have no profiles.
        File.WriteAllText(Path.Combine(second, "TOKINA", nikon), "not the profile");
        File.WriteAllText(Path.Combine(second, "TOKINA", "Canon (Tokina AT-X 11-20mm F2.8 PRO DX) - RAW.lcp"), LensProfileTestData.Lcp(make: "Canon"));
        // The same name at another depth (the user's folder is searched one level above
        // Adobe's 1.0) is another file, kept: the relative path is the key, not the name.
        Directory.CreateDirectory(Path.Combine(second, "1.0", "Tokina"));
        File.WriteAllText(Path.Combine(second, "1.0", "Tokina", nikon), LensProfileTestData.Lcp());

        var s = ProfileLocator.Locate(new[] { first, second }, Path.Combine(ws.Dir, "no-lensfun"));

        Assert.Equal(3, s.AdobeFiles);
        Assert.Equal(1, s.AdobeFilesDuplicate);
        var lenses = s.Catalog.Lenses;
        Assert.Equal(2, lenses.Count);
        Assert.Equal(new[] { "Canon", "NIKON CORPORATION" }, lenses.Select(l => l.Mount).OrderBy(m => m, StringComparer.Ordinal));
        var nikonLens = lenses.Single(l => l.Mount == "NIKON CORPORATION");
        var profiles = s.Catalog.ProfilesOf(nikonLens);
        // The fixture's three entries from the first folder and three from the deeper copy;
        // nothing from the garbage copy, which was never read.
        Assert.Equal(new[] { 18.0, 18.0, 20.0, 20.0, 20.0, 20.0 }, profiles.Select(p => p.FocalMm));
    }

    /// <summary>
    /// The Program Files half of the default list: only the applications that carry a
    /// Resources\LensProfiles\1.0 folder, in ordinal order; a missing root gives none.
    /// </summary>
    [Fact]
    public void R107_AdobeAppDirectories_AreTheExistingProfileFoldersInOrdinalOrder()
    {
        using var ws = new TempWorkspace();
        string root = Path.Combine(ws.Dir, "Adobe");
        string classic = Path.Combine(root, "Adobe Lightroom Classic", "Resources", "LensProfiles", "1.0");
        string cc = Path.Combine(root, "Adobe Lightroom CC", "Resources", "LensProfiles", "1.0");
        Directory.CreateDirectory(classic);
        Directory.CreateDirectory(cc);
        Directory.CreateDirectory(Path.Combine(root, "Adobe DNG Converter"));                 // no profiles inside
        Directory.CreateDirectory(Path.Combine(root, "Adobe Photoshop", "Resources"));         // nor here

        Assert.Equal(new[] { cc, classic }, ProfileLocator.AdobeAppDirectories(root));
        Assert.Empty(ProfileLocator.AdobeAppDirectories(Path.Combine(ws.Dir, "missing")));
        Assert.Empty(ProfileLocator.AdobeAppDirectories(null));
    }

    /// <summary>
    /// The default order: the Camera Raw folder, the user's, then the Program Files
    /// applications, every one of those an existing Resources\LensProfiles\1.0 under Program
    /// Files\Adobe (whatever this machine has installed). Windows only: the folders are Adobe's
    /// Windows layout.
    /// </summary>
    [Fact]
    public void R107_DefaultAdobeDirectories_StartWithCameraRawThenTheProgramFilesApps()
    {
        if (!OperatingSystem.IsWindows()) return;
        var dirs = ProfileLocator.DefaultAdobeDirectories();
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Adobe", "CameraRaw", "LensProfiles", "1.0"), dirs[0]);
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Adobe", "CameraRaw", "LensProfiles"), dirs[1]);
        string adobe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Adobe") + Path.DirectorySeparatorChar;
        var apps = dirs.Skip(2).ToList();
        Assert.All(apps, d =>
        {
            Assert.StartsWith(adobe, d, StringComparison.Ordinal);
            Assert.EndsWith(Path.Combine("Resources", "LensProfiles", "1.0"), d, StringComparison.Ordinal);
            Assert.True(Directory.Exists(d), d);
        });
        Assert.Equal(apps.OrderBy(d => d, StringComparer.Ordinal), apps);
    }

    /// <summary>The summary names the skipped copies only when there are any, in both languages.</summary>
    [Fact]
    public void R107_SourcesSummary_NamesTheDuplicatesSkippedOnlyWhenThereAreAny()
    {
        static ProfileSources Sources(int duplicates)
            => new(new[] { "x" }, 10, duplicates, "lf", false, false, null, LensCatalog.Empty, LensfunData.Empty);
        using (Ui.Use(UiLanguage.English))
        {
            Assert.Contains("10 files (7164 duplicates skipped)", SyntheticReport.SourcesSummary(Sources(7164)));
            Assert.DoesNotContain("duplicate", SyntheticReport.SourcesSummary(Sources(0)));
            Assert.Contains("a file present in two folders is read once, from the first", SyntheticReport.AdobeWhere(Sources(0)));
        }
        using (Ui.Use(UiLanguage.Romanian))
        {
            Assert.Contains("10 fisiere (7164 duplicate sarite)", SyntheticReport.SourcesSummary(Sources(7164)));
            Assert.Contains("un fisier prezent in doua foldere se citeste o data, din primul", SyntheticReport.AdobeWhere(Sources(0)));
        }
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

    /// <summary>A profile list that counts how many times it was walked (enumerated or read by
    /// index), so a test can say a search did not walk it.</summary>
    sealed class CountingProfiles : IReadOnlyList<LensProfile>
    {
        readonly IReadOnlyList<LensProfile> _inner;
        internal int Walks;

        internal CountingProfiles(IReadOnlyList<LensProfile> inner) => _inner = inner;

        public int Count => _inner.Count;
        public LensProfile this[int index] { get { Walks++; return _inner[index]; } }
        public IEnumerator<LensProfile> GetEnumerator() { Walks++; return _inner.GetEnumerator(); }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// A keystroke in the window's lens field is one search. It used to walk the whole profile
    /// list once for every lens, to gather the lens's aliases: half a second a keystroke on a
    /// real catalogue (3799 lenses, 18902 profiles, measured 2026-11-12). The texts are built
    /// once; a search after that reads them only, and still finds a lens by its alias.
    /// </summary>
    [Fact]
    public void R132_ASearchAfterTheFirstDoesNotWalkTheProfileList()
    {
        var profiles = new CountingProfiles(Catalog().Profiles);
        var c = new LensCatalog(profiles);
        Assert.False(c.SearchPrepared);
        var first = c.Search("tokina 11-20");
        int walks = profiles.Walks;
        Assert.True(c.SearchPrepared);          // a catalogue made by hand builds them at its first search

        Assert.Equal(first, c.Search("tokina 11-20"));
        Assert.Single(c.Search("Aspherical"));
        Assert.Equal(2, c.Search("11.0-20.0 mm").Count);       // found by the alias
        Assert.Equal(2, c.Search("tokina 11-20", ProfileSource.Adobe).Count);
        Assert.Equal(3, c.Search("").Count);
        Assert.Equal(walks, profiles.Walks);
    }

    /// <summary>
    /// The locator builds the search texts while it loads the profiles, so the first search in
    /// the window pays nothing either (the owner's choice, ADR 0099); a catalogue made by hand
    /// builds them at its first search.
    /// </summary>
    [Fact]
    public void R132_TheLocatorBuildsTheSearchTextsWhileItLoads()
    {
        using var ws = new TempWorkspace();
        Assert.True(LensProfileTestData.Sources(ws).Catalog.SearchPrepared);

        var byHand = Catalog();
        Assert.False(byHand.SearchPrepared);
        byHand.PrepareSearch();
        Assert.True(byHand.SearchPrepared);
        Assert.Equal(3, byHand.Search("tokina 11-20").Count);
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

    /// <summary>
    /// The calibration format is searchable, so "Nikkor 35mm full" picks one of the two
    /// calibrations of a lens whose name alone matches both - the way --lens tells them apart.
    /// </summary>
    [Fact]
    public void Search_FindsOneOfTwoCalibrationsByItsFormatWord()
    {
        var c = new LensCatalog(LensfunReader.ReadXml(LensProfileTestData.LensfunNikkor35Xml, "x.xml").Profiles);

        Assert.Equal(2, c.Lenses.Count);
        Assert.Equal(2, c.Search("Nikkor 35mm f/1.8G").Count);
        var ff = Assert.Single(c.Search("Nikkor 35mm f/1.8G full"));
        Assert.Equal("full frame 1", ff.Calibration);
        var dx = Assert.Single(c.Search("nikkor aps-c"));
        Assert.Equal("APS-C 1.528", dx.Calibration);
        Assert.Equal(3, c.ProfilesOf(ff).Count);                             // three apertures, each a copied 10 / 1000 m pair
        Assert.All(c.ProfilesOf(ff), p => Assert.EndsWith(LensProfile.DistanceUnspecified, p.Settings));
        Assert.NotEqual(c.ProfilesOf(ff)[0].Model.CropFactor, c.ProfilesOf(dx)[0].Model.CropFactor);
    }

    /// <summary>
    /// Only an exact copy is the "distance unknown" convention: two entries at 10 and 1000 m
    /// with different coefficients are two measurements and keep their distances.
    /// </summary>
    [Fact]
    public void OnlyIdenticalCoefficientsAtTwoDistancesCollapse()
    {
        var lens = new LensChoice(ProfileSource.Lensfun, "X", "X 50", "M", "full frame 1");
        LensProfile P(double stop, double dist, double k1) => new(lens, null, null, 50, stop, dist,
            new VignettingModel(VignettingModelKind.Lensfun, new[] { k1, 0.1, 0.0 }, null, 50, 1.0), "x.xml");

        var collapsed = LensfunReader.CollapseCopiedDistances(new[] { P(2, 10, -0.5), P(2, 1000, -0.5), P(4, 10, -0.2), P(4, 1000, -0.25), P(8, 1000, -0.1) });

        Assert.Equal(4, collapsed.Count);
        Assert.True(double.IsNaN(collapsed.Single(p => p.FStop == 2).DistanceM));
        Assert.Equal(new[] { 10.0, 1000.0 }, collapsed.Where(p => p.FStop == 4).Select(p => p.DistanceM));
        Assert.Equal(1000, collapsed.Single(p => p.FStop == 8).DistanceM);
        Assert.Equal("50 mm, f/2.0, distanta nespecificata", collapsed[0].Settings);
    }

    /// <summary>
    /// Nearest never interpolates and never refuses, so a request far from every profile
    /// silently lands on the nearest one; the warning names what was asked, what was taken
    /// and what the lens has. Inside the tolerances (1 mm / 5 % on the focal, half a stop on
    /// the aperture - Adobe's grid is at whole stops, so any request inside the range is
    /// under it) there is nothing to say.
    /// </summary>
    [Fact]
    public void ProfileMismatch_WarnsOnlyWhenTheRequestIsFarFromTheProfileUsed()
    {
        var c = Catalog();
        var adobe = c.Lenses.First(l => l.Source == ProfileSource.Adobe);
        var all = c.ProfilesOf(adobe);                                   // 18 and 20 mm; f/2.8 and f/4

        Assert.Null(SyntheticReport.ProfileMismatch(LensCatalog.Nearest(all, 20, 2.8), 20, 2.8, all));
        Assert.Null(SyntheticReport.ProfileMismatch(LensCatalog.Nearest(all, 20.5, 3.5), 20.5, 3.5, all));   // 0.5 mm, 0.39 stops from f/4
        Assert.Null(SyntheticReport.ProfileMismatch(LensCatalog.Nearest(all, 19, 2.5), 19, 2.5, all));       // 1 mm, 0.33 stops from f/2.8

        string far = SyntheticReport.ProfileMismatch(LensCatalog.Nearest(all, 500, 1.0), 500, 1.0, all);
        Assert.Equal("focala ceruta 500 mm n-are profil: s-a luat 20 mm (obiectivul are profile la 18, 20 mm);"
                   + " diafragma ceruta f/1.0 e la 3.0 stopuri de cel mai apropiat profil, f/2.8 (profile de la f/2.8 la f/4.0)."
                   + " Adancimea scrisa e a profilului folosit, nu a valorilor cerute.", far);

        string focalOnly = SyntheticReport.ProfileMismatch(LensCatalog.Nearest(all, 11, 2.8), 11, 2.8, all);
        Assert.StartsWith("focala ceruta 11 mm n-are profil: s-a luat 18 mm", focalOnly);
        Assert.DoesNotContain("diafragma", focalOnly);

        string stopOnly = SyntheticReport.ProfileMismatch(LensCatalog.Nearest(all, 20, 11), 20, 11, all);
        Assert.StartsWith("diafragma ceruta f/11.0 e la 2.9 stopuri de cel mai apropiat profil, f/4.0", stopOnly);
        Assert.DoesNotContain("focala", stopOnly);
        Assert.True(SyntheticReport.ProfileMismatches(LensCatalog.Nearest(all, 20, 11), 20, 11));
        Assert.False(SyntheticReport.ProfileMismatches(LensCatalog.Nearest(all, 20, 4), 20, 4));
    }

    /// <summary>
    /// The details line under a lens - what --lenses prints and the Sintetic tab shows on the
    /// row's second line: focals, apertures, distances (metres ascending, then infinity, then
    /// unspecified), profile count. One wording for both.
    /// </summary>
    [Fact]
    public void LensDetails_ListsFocalsAperturesDistancesAndTheProfileCount()
    {
        var c = Catalog();
        var lf = c.Lenses.Single(l => l.Source == ProfileSource.Lensfun);
        var adobe = c.Lenses.First(l => l.Source == ProfileSource.Adobe);

        Assert.Equal("focale: 18, 20 mm; diafragme: f/2.8, f/4.0; distante: 10 m, infinit, nespecificata; 4 profile",
                     SyntheticReport.LensDetails(lf, c));
        Assert.Equal("focale: 18, 20 mm; diafragme: f/2.8, f/4.0; distante: infinit; 3 profile",
                     SyntheticReport.LensDetails(adobe, c));
        Assert.Equal("", SyntheticReport.LensDetails(new LensChoice(ProfileSource.Lensfun, "X", "X", "M"), c));
        Assert.Equal("0.36 m", LensProfile.DistanceLabel(0.36));
        Assert.Equal("infinit", LensProfile.DistanceLabel(10000));
        Assert.Equal("nespecificata", LensProfile.DistanceLabel(double.NaN));
    }

    [Fact]
    public void Nearest_PicksTheFocalThenTheFStopInStopsThenTheFarthestDistance()
    {
        var c = Catalog();
        var lf = c.Lenses.Single(l => l.Source == ProfileSource.Lensfun);
        var ps = c.ProfilesOf(lf);

        var p = LensCatalog.Nearest(ps, 20, 4);
        Assert.Equal(20, p!.FocalMm);
        Assert.Equal(4, p.FStop);
        Assert.Equal(1000, p.DistanceM);                        // infinity over 10 m, when the two are real measurements
        Assert.True(double.IsNaN(LensCatalog.Nearest(ps, 20, 2.8)!.DistanceM));   // the copied pair is one profile

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

    /// <summary>
    /// R-018: the aperture of a name is never a model code or a focal length. Before, the first
    /// "F" with a number was taken: f/45 from Tamron's "F045", f/28 from Zeiss's "f28" and from
    /// "Pentax-F 28mm" (the names are the catalogues', counted on 2026-11-13: 37 of 3242).
    /// </summary>
    [Theory]
    [InlineData("TAMRON SP 35mm F_1.4 Di USD F045", 35.0, 35.0, null)]   // the underscore hides 1.4; F045 is the model
    [InlineData("TAMRON SP 35mm F_1.4 Di USD F045N", 35.0, 35.0, null)]
    [InlineData("Zeiss Distagon T 21 f28 ZE", null, null, null)]         // 2.8 written without its dot: not guessed
    [InlineData("Zeiss Otus 55 f14 ZE", null, null, null)]
    [InlineData("smc Pentax-F 28mm f/2.8", 28.0, 28.0, 2.8)]             // "F 28mm" is the focal length
    [InlineData("Pentax-F 28-80mm f/3.5-4.5", 28.0, 80.0, 3.5)]
    [InlineData("smc Pentax-F 35-80mm f/4-5.6", 35.0, 80.0, 4.0)]
    [InlineData("DSC-F707 & compatibles (Standard)", null, null, null)]
    [InlineData("FinePix F810 & compatibles (Standard)", null, null, null)]
    [InlineData("120mm f/32.0-4.0", 120.0, 120.0, null)]                 // a range written backwards: not read
    [InlineData("Canon RF 800mm F11 IS STM", 800.0, 800.0, 11.0)]        // the slowest real maximum aperture
    [InlineData("Voigtlander 29mm f/0.8 Super Nokton", 29.0, 29.0, 0.8)] // the fastest
    [InlineData("Distagon 28mm f/2.8 MMJ", 28.0, 28.0, 2.8)]             // "MMJ" is not "mm"
    public void R018_TheApertureOfANameIsNeverAModelCodeOrAFocalLength(string name, double? min, double? max, double? ap)
    {
        var spec = LensSpec.Parse(name);
        Assert.Equal(min, spec.MinFocalMm);
        Assert.Equal(max, spec.MaxFocalMm);
        Assert.Equal(ap, spec.MaxAperture);
    }

    /// <summary>R-018: the bounds are the thresholds', and they are the ends of what the two
    /// catalogues hold (0.8 to 11 on 2026-11-13).</summary>
    [Fact]
    public void R018_TheApertureBoundsAreTheThresholds()
    {
        Assert.Equal(0.7, Th.LensNameApertureMin);
        Assert.Equal(11.0, Th.LensNameApertureMax);
        Assert.Equal(11.0, LensSpec.Parse("Lens 500mm F11").MaxAperture);
        Assert.Null(LensSpec.Parse("Lens 500mm F11.5").MaxAperture);
        Assert.Equal(0.7, LensSpec.Parse("Lens 50mm f/0.7").MaxAperture);
        Assert.Null(LensSpec.Parse("Lens 50mm f/0.65").MaxAperture);
    }

    /// <summary>
    /// R-018, the harm: for a raw that says 35 mm f/1.4 the Tamron was left out, because its
    /// name "said" f/45, ten stops away, and the depth verdict came from another lens.
    /// </summary>
    [Fact]
    public void R018_ALensWhoseNameCarriesAModelCodeIsNotLeftOut()
    {
        var hits = new[]
        {
            new LensChoice(ProfileSource.Adobe, "TAMRON", "TAMRON SP 35mm F_1.4 Di USD F045", "NIKON CORPORATION"),
            new LensChoice(ProfileSource.Adobe, "Zeiss", "Zeiss Distagon T 35 f14 ZE", "Canon"),
            new LensChoice(ProfileSource.Lensfun, "Pentax", "smc Pentax-F 28mm f/2.8", "Pentax KAF"),
        };

        Assert.Equal(new[] { hits[0], hits[1] }, LensCatalog.Narrow(hits, 35, 35, 1.4));
        Assert.Equal(new[] { hits[1], hits[2] }, LensCatalog.Narrow(hits, 28, 28, 2.8));   // the Zeiss gives no range at all
        Assert.Equal(new[] { hits[1] }, LensCatalog.Narrow(hits, 28, 28, 4.0));
    }

    /// <summary>
    /// R-018: a name is read with the digits 0 to 9 only (ADR 0110). A regex's "\d" also
    /// matches every other Unicode digit, which the invariant parse then refuses: one name
    /// with full-width or Arabic-Indic digits threw a FormatException out of Parse (the
    /// review's CR-014). Such a number is found whole and not read: that part is not said.
    /// </summary>
    [Theory]
    [InlineData("Lens １３５mm F2.8", null, null, 2.8)]              // a full-width 135: no focal length
    [InlineData("Lens 135mm F２.８", 135.0, 135.0, null)]                // a full-width 2.8: no aperture
    [InlineData("Lens １３５mm F２.８", null, null, null)]
    [InlineData("Lens 18-２００mm F3.5-5.6", null, null, 3.5)]       // half a range is no range
    [InlineData("Lens ١٣٥mm f/٢", null, null, null)]         // Arabic-Indic digits
    [InlineData("Lens 50mm f/1.٨", 50.0, 50.0, null)]                       // 1.8 with one other digit is not f/1
    [InlineData("Lens １" + "35mm f/2", null, null, 2.0)]                    // nor is a 135 with one other digit 35 mm
    public void R018_ANameIsReadWithTheDigitsZeroToNineOnly(string name, double? min, double? max, double? ap)
    {
        var spec = LensSpec.Parse(name);
        Assert.Equal(min, spec.MinFocalMm);
        Assert.Equal(max, spec.MaxFocalMm);
        Assert.Equal(ap, spec.MaxAperture);
    }

    /// <summary>
    /// R-018, the harm of CR-014: the range path reads every name of the catalogue, so one
    /// such name stopped the identification of every raw that reached it. The name now says
    /// nothing: it contradicts no raw, and it is no candidate by range.
    /// </summary>
    [Fact]
    public void R018_OneNameWithOtherDigitsDoesNotStopTheIdentification()
    {
        const string Odd = "Tokina AT-X １１-２０mm F２.８ PRO DX";
        var plain = LcpReader.ReadXml(LensProfileTestData.Lcp(), "n.lcp")
            .Concat(LensfunReader.ReadXml(LensProfileTestData.LensfunXml, "x.xml").Profiles).ToList();
        var withOdd = plain.Concat(LcpReader.ReadXml(LensProfileTestData.Lcp(lens: Odd, lensId: "9001"), "odd.lcp")).ToList();
        LoadedFrame raw = LensProfileTestData.Range135Raw();

        var found = LensIdentification.CandidatesByRange(new LensCatalog(withOdd), raw, "NIKON D3300");

        Assert.Equal(LensIdentification.CandidatesByRange(new LensCatalog(plain), raw, "NIKON D3300"), found);
        Assert.DoesNotContain(found, l => l.Name == Odd);
        var odd = new LensCatalog(withOdd).Lenses.Single(l => l.Name == Odd);
        Assert.Equal(new[] { odd }, LensCatalog.Narrow(new[] { odd }, 135, 135, 2.0));
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

    /// <summary>
    /// A synthetic flat is radial by construction, so the structure battery must call it perfect.
    /// </summary>
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
    /// The depth verdict is the residual scale on 2^|stops| - 1, the error the wrong flat leaves in
    /// the corner of a calibrated light. The figures are the Tokina masters'
    /// (docs/design/lens-profiles.md).
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

    /// <summary>The one-profile LCP of the Canon EF 50 f/1.4 at 50 mm, as a raw profile
    /// (a1 = -0.289055) or as one made for rendered files (a1 = -0.1, a third of the
    /// falloff, the shape of the pair the review of 2026-11-13 measured in Adobe's files).</summary>
    static string Canon50Lcp(bool raw)
    {
        string xml = LensProfileTestData.LcpOneProfile("Canon", "Canon EF 50mm f/1.4 USM", "EF50mm f/1.4 USM", "198", "1", "50");
        return raw ? xml
            : xml.Replace("stCamera:CameraRawProfile=\"True\"", "stCamera:CameraRawProfile=\"False\"")
                 .Replace("VignetteModelParam1=\"-0.289055\"", "VignetteModelParam1=\"-0.1\"");
    }

    /// <summary>
    /// R-134: an entry that says it was made for rendered files is not read, whatever the case
    /// of the word; one that says raw, or says nothing, is.
    /// </summary>
    [Fact]
    public void R134_AnEntryMadeForRenderedFilesIsNotRead()
    {
        Assert.Single(LcpReader.ReadXml(Canon50Lcp(raw: true), "raw.lcp"));
        Assert.Empty(LcpReader.ReadXml(Canon50Lcp(raw: false), "jpeg.lcp"));
        Assert.Empty(LcpReader.ReadXml(Canon50Lcp(raw: false).Replace("\"False\"", "\"false\""), "jpeg.lcp"));
        string silent = Canon50Lcp(raw: true).Replace(" stCamera:CameraRawProfile=\"True\"", "");
        Assert.DoesNotContain("CameraRawProfile", silent);
        Assert.Single(LcpReader.ReadXml(silent, "silent.lcp"));
    }

    /// <summary>
    /// R-134: Adobe ships many lenses as two files, "X (L) - RAW.lcp" and "X (L).lcp", the
    /// second made for JPEGs; by name they are one lens. Its profiles are the raw file's alone:
    /// before, both files' entries were pooled and the nearest rule could take the JPEG one.
    /// </summary>
    [Fact]
    public void R134_ALensOfARawFileAndANonRawOneIsReadFromTheRawFileAlone()
    {
        using var ws = new TempWorkspace();
        string dir = LensProfileTestData.ProfilesDir(ws);
        string rawFile = Path.Combine(dir, "Tokina", "Canon (Canon EF 50mm f1.4 USM) - RAW.lcp");
        File.WriteAllText(rawFile, Canon50Lcp(raw: true));
        File.WriteAllText(Path.Combine(dir, "Tokina", "Canon (Canon EF 50mm f1.4 USM).lcp"), Canon50Lcp(raw: false));
        var s = ProfileLocator.Locate(new[] { dir }, Path.Combine(dir, "lensfun"));

        LensChoice lens = Assert.Single(s.Catalog.Lenses, l => l.Name.Contains("50mm"));   // one lens, two files
        LensProfile only = Assert.Single(s.Catalog.ProfilesOf(lens));
        Assert.Equal(rawFile, only.File);
        Assert.Equal(-0.289055, only.Model.Coefficients[0], 9);
        Assert.Same(only, LensCatalog.Nearest(s.Catalog.ProfilesOf(lens), 50, 4));
        Assert.Equal(new[] { lens }, s.Catalog.ByLensId(198, "Canon"));
    }

    /// <summary>
    /// R-134: the name does not decide. A raw profile whose file has no " - RAW" suffix (43 of
    /// Adobe's 3531 files on 2026-11-13, the Leica S and the Lensbaby Velvet among them) is
    /// read, and " - Raw" (two files) is the same lens as " - RAW".
    /// </summary>
    [Fact]
    public void R134_TheFilesNameDoesNotDecide_AndTheRawSuffixIsReadWithoutCase()
    {
        using var ws = new TempWorkspace();
        string dir = LensProfileTestData.ProfilesDir(ws);
        File.WriteAllText(Path.Combine(dir, "Tokina", "Canon (Canon EF 50mm f1.4 USM).lcp"), Canon50Lcp(raw: true));
        var s = ProfileLocator.Locate(new[] { dir }, Path.Combine(dir, "lensfun"));
        LensChoice lens = Assert.Single(s.Catalog.Lenses, l => l.Name.Contains("50mm"));
        Assert.Single(s.Catalog.ProfilesOf(lens));

        LensChoice upper = AdobeProfileFile.FromName(@"Canon\Canon (Canon EF 50mm f1.4 USM) - RAW.lcp").Lens;
        Assert.Equal(upper, AdobeProfileFile.FromName(@"Canon\Canon (Canon EF 50mm f1.4 USM) - Raw.lcp").Lens);
        Assert.Equal(upper, AdobeProfileFile.FromName(@"Canon\Canon (Canon EF 50mm f1.4 USM).lcp").Lens);
        Assert.Equal("Canon", upper.Mount);
    }

    /// <summary>
    /// R-134: a lens whose only file was made for JPEGs is listed, is no candidate by the raw's
    /// lens ID, and is refused with the reason and exit 1 when asked for.
    /// </summary>
    [Fact]
    public void R134_ALensWithOnlyANonRawFileIsListedAndRefusedWithTheReason()
    {
        using var ws = new TempWorkspace();
        string dir = LensProfileTestData.ProfilesDir(ws);
        File.WriteAllText(Path.Combine(dir, "Tokina", "Canon (Canon EF 50mm f1.4 USM).lcp"), Canon50Lcp(raw: false));
        var sources = ProfileLocator.Locate(new[] { dir }, Path.Combine(dir, "lensfun"));
        Assert.Single(sources.Catalog.Lenses, l => l.Name.Contains("50mm"));
        Assert.Empty(sources.Catalog.ByLensId(198, "Canon"));
        string reference = ws.WriteFile("ref.fit", Reference());
        var req = new SyntheticRequest { LensText = "50mm f1.4" };
        var sw = new StringWriter();

        var run = SyntheticRun.Execute(reference, req, sources, 50, 4, 1.0, sw);

        Assert.True(run.Refused);
        Assert.Equal($"FLAT SINTETIC: obiectivul \"{req.Lens.Title}\" nu are profil de vignetare pentru fisiere raw "
                     + "(fisierul lui descrie doar distorsiunea sau culoarea, ori e facut pentru JPEG); alege alt obiectiv.", run.Refusal);
        using (Ui.Use(UiLanguage.English))
            Assert.Equal($"SYNTHETIC FLAT: the lens \"{req.Lens.Title}\" has no vignetting profile for raw files "
                         + "(its file describes distortion or colour only, or was made for JPEGs); choose another lens.",
                         SyntheticReport.NoProfile(req.Lens));

        var stderr = new StringWriter();
        int exit = FlatCheckCli.Run(new[] { reference, "--synthetic", ws.PathTo("synth.fit"), "--lens", "50mm f1.4", "--focal", "50", "--fstop", "4", "--crop", "1", "--profiles", dir },
                                    new StringWriter(), stderr);
        Assert.Equal(1, exit);
        Assert.False(File.Exists(ws.PathTo("synth.fit")));
    }

    /// <summary>
    /// R-071: a lens whose Adobe file carries no vignetting model (1275 of the 3531 files on the
    /// author's machine on 2026-10-27, the extender combinations among them) is listed, since
    /// listing reads names only, but refused with the reason and exit 1 when asked for: not a
    /// NullReferenceException from a null nearest profile (the review of 2026-10-27, A3).
    /// </summary>
    [Fact]
    public void R071_ALensWhoseFileHasNoVignettingModelIsRefusedWithTheReason()
    {
        using var ws = new TempWorkspace();
        string dir = LensProfileTestData.ProfilesDir(ws);
        const string Name = "Canon RF 100-400mm F5.6-8 IS USM + EXTENDER RF2x";
        File.WriteAllText(Path.Combine(dir, "Tokina", $"Canon ({Name}) - RAW.lcp"),
            LensProfileTestData.LcpWithoutVignette("Canon", Name, "RF100-400mm F5.6-8 IS USM + RF2x", "61182", "1", "200"));
        var sources = ProfileLocator.Locate(new[] { dir }, Path.Combine(dir, "lensfun"));
        string reference = ws.WriteFile("ref.fit", Reference());
        var req = new SyntheticRequest { LensText = "EXTENDER RF2x" };
        var sw = new StringWriter();

        var run = SyntheticRun.Execute(reference, req, sources, null, 8, null, sw);

        Assert.True(run.Refused);
        Assert.False(run.RefusedChoice);
        Assert.Contains($"FLAT SINTETIC: obiectivul \"{req.Lens.Title}\" nu are profil de vignetare", run.Refusal);
        Assert.Contains(run.Refusal, sw.ToString());

        var stderr = new StringWriter();
        int exit = FlatCheckCli.Run(new[] { reference, "--synthetic", ws.PathTo("synth.fit"), "--lens", "EXTENDER RF2x", "--fstop", "8", "--profiles", dir },
                                    new StringWriter(), stderr);
        Assert.Equal(1, exit);
        Assert.StartsWith("EROARE: FLAT SINTETIC: obiectivul", stderr.ToString());
        Assert.False(File.Exists(ws.PathTo("synth.fit")));
    }

    /// <summary>
    /// A lens calibrated on two formats matches its name twice: the choice is refused as
    /// ambiguous, the list names both calibrations, and the format word picks one. The written
    /// file then says which calibration it came from.
    /// </summary>
    [Fact]
    public void ALensCalibratedOnTwoFormatsIsAmbiguousByName_AndTheFormatWordPicksOne()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws, tokinaOnTwoFormats: true);
        string reference = ws.WriteFile("ref.fit", Reference());

        var ambiguous = new SyntheticRequest { LensText = "Aspherical", Source = ProfileSource.Lensfun };
        var refused = SyntheticRun.Execute(reference, ambiguous, sources, null, 2.8, null, new StringWriter());
        Assert.True(refused.Refused);
        Assert.True(refused.RefusedChoice);
        Assert.Contains("calibrat pe APS-C Canon 1.61", refused.Refusal);
        Assert.Contains("calibrat pe full frame 1", refused.Refusal);

        var req = new SyntheticRequest { LensText = "Aspherical full", Source = ProfileSource.Lensfun };
        var sw = new StringWriter();
        var run = SyntheticRun.Execute(reference, req, sources, null, 2.8, null, sw);
        Assert.False(run.Refused, run.Refusal);
        Assert.Equal("full frame 1", req.Lens.Calibration);
        Assert.Equal(1.0, run.Result.Profile.Model.CropFactor, 9);
        Assert.Contains("[Canon EF / Nikon F AF, calibrat pe full frame 1]  sursa: lensfun", sw.ToString());
        Assert.Contains(SyntheticRun.History(run, reference), h => h.Contains("calibrat pe full frame 1"));
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

        // the same unknown camera with an Adobe profile: not refused, the profile's SFF stands in,
        // and the report says so
        var adobeReq = new SyntheticRequest { LensText = "Tokina AT-X 11-20mm F2.8 PRO DX", Source = ProfileSource.Adobe };
        var sw = new StringWriter();
        var adobe = SyntheticRun.Execute(other, adobeReq, sources, null, 2.8, null, sw);
        Assert.False(adobe.Refused, adobe.Refusal);
        Assert.Equal(0, adobeReq.CameraCrop);
        Assert.Contains("necunoscut", adobeReq.CropSource);
        Assert.Contains("crop aparat        : necunoscut", sw.ToString());
        Assert.Contains("SFF 1.5", sw.ToString());
        // with the camera known, the crop is used and reported, and at 1.523 vs SFF 1.5 no format
        // warning
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
        // extrapolated that far is meaningless (it goes negative on this fixture), and the report
        // says so
        Assert.InRange(ff.Result.Extrapolation, 1.45, 1.55);
        Assert.Contains("MULT dincolo de ce s-a masurat", sw3.ToString());
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

    /// <summary>
    /// The one sentence both applications say about a profile beside the user's sensor.
    /// </summary>
    [Fact]
    public void FormatMatch_SaysTheCalibrationFormat_YoursAndWhatFollows()
    {
        var adobe = new VignettingModel(VignettingModelKind.Adobe, new[] { -0.5, 0.0, 0.0 }, null, 20, 1.5);
        string same = SyntheticReport.FormatMatch(adobe, 1.523, 6000, 4000);
        Assert.Equal("profil calibrat pe APS-C (crop 1.5); senzorul tau APS-C (1.523): acelasi format", same);
        string ff = SyntheticReport.FormatMatch(adobe, 1.0, 6000, 4000);
        Assert.StartsWith("profil calibrat pe APS-C (crop 1.5); senzorul tau full frame (1): alt format, modelul e recalculat pentru senzorul tau; coltul tau e la 1.50 x coltul pe care a fost masurat profilul: MULT dincolo de ce s-a masurat", ff);
        string smaller = SyntheticReport.FormatMatch(adobe, 2.0, 6000, 4000);
        Assert.Contains("Micro 4/3 (2): alt format", smaller);
        Assert.Contains("e la 0.75 din coltul pe care a fost masurat profilul, in interiorul lui", smaller);
        Assert.Equal("profil calibrat pe APS-C (crop 1.5); crop-ul tau nu se stie, se ia senzorul de calibrare", SyntheticReport.FormatMatch(adobe, 0, 6000, 4000));
        var lensfun = new VignettingModel(VignettingModelKind.Lensfun, new[] { -0.5, 0.0, 0.0 }, null, 20, 1.61);
        string d3300 = SyntheticReport.FormatMatch(lensfun, 1.523, 6016, 4016);
        Assert.StartsWith("profil calibrat pe APS-C Canon (crop 1.61); senzorul tau APS-C (1.523): alt format, modelul e recalculat pentru senzorul tau; coltul tau e la 1.06 x coltul pe care a fost masurat profilul: putin dincolo de ce s-a masurat", d3300);
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
        Assert.Equal(new[] { body }, LensIdentification.PreferCamera(all, "NIKON D750"));
        Assert.Equal(new[] { body }, LensIdentification.PreferCamera(all, "Nikon D750"));      // case and spacing do not matter
        Assert.Equal(all, LensIdentification.PreferCamera(all, "NIKON D3300"));                // no file of its own: nothing changes
        Assert.Equal(all, LensIdentification.PreferCamera(all, null));
        Assert.Equal(new[] { body }, LensIdentification.PreferCamera(new[] { body }, "Canon EOS 6D"));   // a single hit is never dropped
    }

    [Fact]
    public void TheRawsLensIdIsNarrowedByItsRange_ThenTheCameraPicksTheMount()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        var tokina = new LoadedFrame { Camera = "NIKON D3300", LensId = 139, LensMinFocalMm = 11, LensMaxFocalMm = 20, LensMaxAperture = 2.8 };

        // the Adobe file on the camera's mount by ID, plus the lensfun Tokina of the same range:
        // lensfun carries no Nikon IDs, so it joins by maker and range
        var candidates = LensIdentification.CandidatesFromRaw(sources.Catalog, tokina);
        Assert.Equal(2, candidates.Count);
        Assert.Equal(ProfileSource.Adobe, candidates[0].Source);
        Assert.Equal("NIKON CORPORATION", candidates[0].Mount);
        Assert.Equal(ProfileSource.Lensfun, candidates[1].Source);
        Assert.Contains("11-20", candidates[1].Name);

        // both sit on the Nikon mount, so the console lists them and asks for --source; with it,
        // one is chosen
        string both = SyntheticRun.ChooseLens(new SyntheticRequest(), sources, tokina);
        Assert.Contains("2 obiective", both);
        var req = new SyntheticRequest { Source = ProfileSource.Adobe };
        Assert.Null(SyntheticRun.ChooseLens(req, sources, tokina));
        Assert.Equal("NIKON CORPORATION", req.Lens.Mount);
        Assert.Equal(ProfileSource.Adobe, req.Lens.Source);
        Assert.Equal("ID-ul obiectivului 139 din raw (11-20 mm f/2.8)", SyntheticReport.RawLensLabel(tokina));

        // the same ID from a lens whose range every name contradicts: nothing is chosen, --lens is
        // asked for
        var nikkor = new LoadedFrame { Camera = "NIKON D3300", LensId = 139, LensMinFocalMm = 18, LensMaxFocalMm = 200, LensMaxAperture = 3.5 };
        Assert.Empty(LensIdentification.CandidatesFromRaw(sources.Catalog, nikkor));
        string refusal = SyntheticRun.ChooseLens(new SyntheticRequest(), sources, nikkor);
        Assert.Contains("--lens", refusal);

        // no camera: both Adobe mounts and the lensfun entry survive, and the refusal names what
        // the raw said
        var unknownCamera = new LoadedFrame { LensId = 139, LensMinFocalMm = 11, LensMaxFocalMm = 20, LensMaxAperture = 2.8 };
        string ambiguous = SyntheticRun.ChooseLens(new SyntheticRequest(), sources, unknownCamera);
        Assert.Contains("ID-ul obiectivului 139 din raw (11-20 mm f/2.8)", ambiguous);
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

/// <summary>
/// The one identification the console, the Sintetic tab and the Structure tab share.
/// Pinned: the precedence (given beats read, the reference beats the raw beside it for the
/// focal, only a raw gives the f-stop), the sources printed beside each value, the crop
/// from the lensfun camera table, and the candidates narrowed by range and by mount.
/// </summary>
public class LensIdentificationTests
{
    /// <summary>
    /// R-133: the crop's source names the table's model, not the file's spelling of the camera,
    /// so the entry that gave the crop can be read. A raw says "NIKON D3300"; the table's entry
    /// is "Nikon D3300".
    /// </summary>
    [Fact]
    public void R133_TheCropsSourceNamesTheTablesModel_NotTheFilesSpelling()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        LoadedFrame master = Master();
        master.Camera = "Nikon Corporation NIKON D3300";

        ShotIdentity id = LensIdentification.Identify(master, null, sources);

        Assert.Equal("Nikon Corporation NIKON D3300", id.Camera);
        Assert.True(id.CropFromTable);
        Assert.Equal("Nikon D3300", id.CropCamera);
        Assert.Equal("baza lensfun, Nikon D3300", id.CropSource);
        Assert.Contains("crop din baza lensfun (Nikon D3300)", id.Notes);
        Assert.DoesNotContain(id.Notes, n => n.Contains("Corporation"));

        ShotIdentity typed = LensIdentification.Identify(master, null, sources, crop: 1.5);
        Assert.Null(typed.CropCamera);
    }

    static LoadedFrame Master() => new LoadedFrame
    {
        Header = new Dictionary<string, string> { ["SIMPLE"] = "T", ["FOCALLEN"] = "20", ["INSTRUME"] = "Nikon D3300" },
        FocalMm = 20,
        Camera = "Nikon D3300",
    };

    static LoadedFrame Raw() => new LoadedFrame
    {
        Header = new Dictionary<string, string>(),
        FocalMm = 18,
        FNumber = 2.8,
        Camera = "NIKON D3300",
        LensId = 139,
        LensMinFocalMm = 11,
        LensMaxFocalMm = 20,
        LensMaxAperture = 2.8,
    };

    [Fact]
    public void TheReferenceGivesTheFocal_TheRawBesideItGivesTheFStop_TheTableGivesTheCrop()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);

        ShotIdentity id = LensIdentification.Identify(Master(), Raw(), sources, rawName: "DSC_0001.NEF");

        Assert.Equal(20, id.FocalMm);                       // FOCALLEN beats the raw's 18
        Assert.Equal("FOCALLEN", id.FocalSource);
        Assert.Equal(2.8, id.FStop);
        Assert.Equal("raw (DSC_0001.NEF)", id.FStopSource);
        Assert.Equal(1.523, id.Crop!.Value, 3);
        Assert.True(id.CropFromTable);
        Assert.Equal("baza lensfun, Nikon D3300", id.CropSource);
        Assert.Equal("Nikon D3300", id.Camera);
        Assert.Contains("focala din FOCALLEN", id.Notes);
        Assert.Contains("diafragma din DSC_0001.NEF", id.Notes);
        Assert.Contains("crop din baza lensfun (Nikon D3300)", id.Notes);
        Assert.Equal("Nikon D3300", id.CropCamera);

        // the raw's lens ID, narrowed by its range, then the camera's mount: the Nikon Adobe
        // file and the lensfun Tokina, not the Canon file
        Assert.True(id.HasRawLensId);
        Assert.Equal(2, id.Candidates.Count);
        Assert.Equal(ProfileSource.Adobe, id.Candidates[0].Source);
        Assert.Equal("NIKON CORPORATION", id.Candidates[0].Mount);       // not the Canon file of the same lens
        Assert.Equal(ProfileSource.Lensfun, id.Candidates[1].Source);
        Assert.Equal("ID-ul obiectivului 139 din raw (11-20 mm f/2.8)", id.RawLensLabel);
    }

    [Fact]
    public void WhatIsGivenBeatsWhatIsRead()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);

        ShotIdentity id = LensIdentification.Identify(Master(), Raw(), sources, focal: 11, fstop: 4, crop: 1.0, rawName: "x.NEF");

        Assert.Equal((11.0, "--focal"), (id.FocalMm!.Value, id.FocalSource));
        Assert.Equal((4.0, "--fstop"), (id.FStop!.Value, id.FStopSource));
        Assert.Equal((1.0, "--crop"), (id.Crop!.Value, id.CropSource));
        Assert.False(id.CropFromTable);
        Assert.Empty(id.Notes);                              // nothing was read, so nothing to note
    }

    [Fact]
    public void WithoutARawNothingIsGuessed()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);

        ShotIdentity id = LensIdentification.Identify(Master(), null, sources);

        Assert.Equal(20, id.FocalMm);
        Assert.Null(id.FStop);
        Assert.Null(id.FStopSource);
        Assert.False(id.HasRawLensId);
        Assert.Empty(id.Candidates);
        Assert.Null(id.Raw);
        Assert.False(id.RawSaysNoLens);                      // no raw at all is not a raw without a lens
    }

    /// <summary>What a Canon EOS 40D on a telescope writes: a camera, and nothing of the lens.</summary>
    static LoadedFrame NoLensRaw() => new LoadedFrame
    {
        Header = new Dictionary<string, string>(),
        Camera = "Canon EOS 40D",
    };

    [Fact]
    public void ARawThatCarriesNoLensIsSaidSoInTheNotes()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);

        ShotIdentity id = LensIdentification.Identify(Master(), NoLensRaw(), sources, rawName: "IMG_7290.CR2");

        Assert.True(id.RawSaysNoLens);
        Assert.False(id.HasRawLensId);
        Assert.False(id.HasRawLensRange);
        Assert.Empty(id.Candidates);
        Assert.Null(id.FStop);
        Assert.Contains(id.Notes, n => n.StartsWith("IMG_7290.CR2 nu spune nimic despre obiectiv"));
        Assert.Contains(id.Notes, n => n.Contains("telescop"));
    }

    [Fact]
    public void ARawWithAnIdOrARangeIsNotARawWithoutALens()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        var manual = new LoadedFrame
        {
            Header = new Dictionary<string, string>(),
            FocalMm = 135,
            FNumber = 2,
            Camera = "NIKON D3300",
            LensId = 74,                                 // the "non-CPU lens" ID, with the menu's range
            LensMinFocalMm = 135,
            LensMaxFocalMm = 135,
            LensMaxAperture = 2,
        };

        Assert.False(LensIdentification.Identify(Master(), Raw(), sources, rawName: "DSC_0001.NEF").RawSaysNoLens);
        Assert.False(LensIdentification.Identify(Master(), manual, sources, rawName: "DSC_0002.NEF").RawSaysNoLens);
        Assert.DoesNotContain(LensIdentification.Identify(Master(), manual, sources, rawName: "DSC_0002.NEF").Notes,
                              n => n.Contains("nu spune nimic despre obiectiv"));
    }

    [Fact]
    public void ARawReferenceIsItsOwnSourceOfEverything()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);

        ShotIdentity id = LensIdentification.Identify(Raw(), null, sources);

        Assert.Equal((18.0, "raw (libraw)"), (id.FocalMm!.Value, id.FocalSource));
        Assert.Equal((2.8, "raw (libraw)"), (id.FStop!.Value, id.FStopSource));
        Assert.Contains("focala din raw", id.Notes);
        Assert.Equal(2, id.Candidates.Count);
    }

    [Fact]
    public void AnUnknownCameraLeavesTheCropUnknownWithTheReason()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        var master = Master();
        master.Camera = "Pentax K-1";

        ShotIdentity id = LensIdentification.Identify(master, null, sources);

        Assert.Null(id.Crop);
        Assert.StartsWith("necunoscut", id.CropSource);
        Assert.Equal("Pentax K-1", id.Camera);
    }

    /// <summary>
    /// A manual lens (Samyang 135 f/2 on a D3300) gives an ID no profile carries, but the
    /// camera's non-CPU lens data gives the range: then the candidates are the lenses on
    /// the camera's mount whose NAME says exactly that range, flagged as such - and never
    /// when the camera is unknown, since "11-20 mm f/2.8 on every mount" is a list, not an
    /// identification.
    /// </summary>
    [Fact]
    public void WhenTheIdMatchesNothingTheRawsRangeStillNamesTheCandidates()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws);
        var manual = Raw();
        manual.LensId = 999;                                   // no profile carries it

        ShotIdentity id = LensIdentification.Identify(Master(), manual, sources, rawName: "x.NEF");

        Assert.True(id.CandidatesByRangeOnly);
        Assert.Equal(2, id.Candidates.Count);                       // the Nikon Adobe file and the lensfun entry, not the Canon file
        Assert.All(id.Candidates, c => Assert.Contains("11-20", c.Name));
        Assert.StartsWith("gama 11-20 mm f/2.8 din raw (ID-ul obiectivului 999", id.RawLensLabel);

        // a range that no name matches: nothing, and the gap stays "no match"
        manual.LensMaxAperture = 5.6;
        id = LensIdentification.Identify(Master(), manual, sources, rawName: "x.NEF");
        Assert.Empty(id.Candidates);
        Assert.False(id.CandidatesByRangeOnly);

        // no camera: no range fallback
        var noCamera = Raw();
        noCamera.LensId = 999;
        noCamera.Camera = null;
        var master = Master();
        master.Camera = null;
        id = LensIdentification.Identify(master, noCamera, sources, rawName: "x.NEF");
        Assert.Empty(id.Candidates);

        // the ID still wins when it matches
        id = LensIdentification.Identify(Master(), Raw(), sources, rawName: "x.NEF");
        Assert.False(id.CandidatesByRangeOnly);
    }

    /// <summary>
    /// The camera's first word is not a mount: "Nikon Z Tokina" contains "NIKON", and on a
    /// machine without Adobe's profiles the Nikkor 35 DX master got its verdict from a Viltrox
    /// Z lens (2026-10-28). A lensfun lens on the range path must also sit on the mount lensfun
    /// gives the body, or on one that mount's definition calls compatible (R-116, ADR 0076); a
    /// body the table does not know keeps the first-word rule alone.
    /// </summary>
    [Fact]
    public void R116_ARangeOnlyCandidateFitsTheCamerasLensfunMount()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws, mounts: true);
        var manual = Raw();
        manual.LensId = 999;                                   // no profile carries it

        ShotIdentity id = LensIdentification.Identify(Master(), manual, sources, rawName: "x.NEF");

        Assert.True(id.CandidatesByRangeOnly);
        // the Nikon Adobe file, the lensfun Tokina on Nikon F AF and the one on Nikon F AI (compat); not the Z
        Assert.Equal(3, id.Candidates.Count);
        Assert.DoesNotContain(id.Candidates, c => c.Mount.Contains("Nikon Z"));
        Assert.Contains(id.Candidates, c => c.Mount == "Nikon F AI");
        Assert.Contains(id.Candidates, c => c.Mount == "Canon EF / Nikon F AF");

        // a body lensfun does not know: the first word alone, as before, and the Z lens is back
        var unknownBody = Raw();
        unknownBody.LensId = 999;
        unknownBody.Camera = "NIKON D9999";
        var master = Master();
        master.Camera = "NIKON D9999";
        id = LensIdentification.Identify(master, unknownBody, sources, rawName: "x.NEF");
        Assert.True(id.CandidatesByRangeOnly);
        Assert.Equal(4, id.Candidates.Count);
        Assert.Contains(id.Candidates, c => c.Mount == "Nikon Z");

        // a Z body takes every F lens: its compat list names them
        var zBody = Raw();
        zBody.LensId = 999;
        zBody.Camera = "NIKON Z 7";
        master.Camera = "NIKON Z 7";
        id = LensIdentification.Identify(master, zBody, sources, rawName: "x.NEF");
        Assert.Equal(4, id.Candidates.Count);
    }

    /// <summary>
    /// The same rule on the ID path: beside an Adobe hit the lensfun lenses of its maker join
    /// by range (ADR 0030), and on the owner's machine that put the Nikkor Z 35 f/1.8 S beside
    /// the Nikkor 35 DX. One rule for both paths (R-116).
    /// </summary>
    [Fact]
    public void R116_TheLensfunLensesThatJoinAnIdHitFitTheCamerasMountToo()
    {
        using var ws = new TempWorkspace();
        var sources = LensProfileTestData.Sources(ws, mounts: true);

        ShotIdentity id = LensIdentification.Identify(Master(), Raw(), sources, rawName: "x.NEF");

        Assert.False(id.CandidatesByRangeOnly);
        Assert.Equal(3, id.Candidates.Count);                  // the Adobe file by ID 139, the F AF and the F AI Tokinas
        Assert.DoesNotContain(id.Candidates, c => c.Mount.Contains("Nikon Z"));
        Assert.Equal(ProfileSource.Adobe, id.Candidates[0].Source);
    }

    [Fact]
    public void Lensfun_MountsOfIsTheCamerasMountAndItsCompatList()
    {
        var d = LensfunReader.ReadXml(LensProfileTestData.LensfunMountsXml, "x.xml");
        Assert.Equal(2, d.Mounts.Count);
        var z = d.MountsOf("NIKON Z 7");
        Assert.Contains("Nikon Z", z);
        Assert.Contains("Nikon F AF", z);
        Assert.Equal(10, z.Count);
        Assert.Null(d.MountsOf("NIKON D3300"));                // not in this document
        Assert.Null(d.MountsOf(null));

        // a mount without a definition is itself alone; a camera without a mount gives none
        var f = LensfunReader.ReadXml(LensProfileTestData.LensfunXml, "y.xml");
        Assert.Empty(f.Mounts);
        Assert.Equal(new[] { "Nikon F AF" }, f.MountsOf("NIKON D3300"));
        var noMount = LensfunReader.ReadXml(LensProfileTestData.LensfunXml.Replace("<mount>Nikon F AF</mount>", ""), "z.xml");
        Assert.Null(noMount.MountsOf("NIKON D3300"));
    }

    [Fact]
    public void FirstRawIsTheRuleBothTabsShare()
    {
        Assert.Equal("b.NEF", LensIdentification.FirstRaw(new[] { "a.fit", "b.NEF", "c.nef" }));
        Assert.Null(LensIdentification.FirstRaw(new[] { "a.fit" }));
        Assert.Null(LensIdentification.FirstRaw(null));
        Assert.Equal("b.NEF", LensIdentification.NameOf("E:/x/flats/b.NEF"));   // slashes: splits on Unix too
        Assert.Null(LensIdentification.NameOf(null));
    }
}
