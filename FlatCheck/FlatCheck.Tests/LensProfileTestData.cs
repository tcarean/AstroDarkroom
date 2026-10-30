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
/// Hand-written profile files, so no Adobe file is ever needed in the repo: an LCP in the
/// attribute form that the installed files use (with a piecewise table on one entry and a
/// decentred FocalLengthX/ImageXCenter on another), and a lensfun database with one lens and
/// one camera. The numbers are the Tokina AT-X 11-20 at 20 mm f/2.8, copied out of the real
/// files, so the model tests can pin what was measured against the real profiles.
/// </summary>
internal static class LensProfileTestData
{
    internal const double A1 = -1.903047, A2 = 4.514725, A3 = -4.657911;   // Adobe, 20 mm, f/2.8, infinity
    internal const double K1 = -0.6195, K2 = 0.3276, K3 = -0.1494;         // lensfun pa, 20 mm, f/2.8, crop 1.61

    internal static string Lcp(string make = "NIKON CORPORATION", string lens = "Tokina AT-X 11-20mm F2.8 PRO DX") => $@"<x:xmpmeta xmlns:x=""adobe:ns:meta/"">
 <rdf:RDF xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#"">
  <rdf:Description rdf:about="""" xmlns:photoshop=""http://ns.adobe.com/photoshop/1.0/"" xmlns:stCamera=""http://ns.adobe.com/photoshop/1.0/camera-profile"">
   <photoshop:CameraProfiles>
    <rdf:Seq>
     <rdf:li>
      <rdf:Description stCamera:Make=""{make}"" stCamera:CameraRawProfile=""True"" stCamera:LensID=""122""
       stCamera:Lens=""{lens}"" stCamera:CameraPrettyName=""{make}"" stCamera:LensPrettyName=""{lens}""
       stCamera:SensorFormatFactor=""1.5"" stCamera:FocalLength=""20"" stCamera:FocusDistance=""10000"" stCamera:ApertureValue=""2.970854"">
      <stCamera:AlternateLensIDs><rdf:Seq><rdf:li>139</rdf:li></rdf:Seq></stCamera:AlternateLensIDs>
      <stCamera:AlternateLensNames><rdf:Seq><rdf:li>11.0-20.0 mm f/2.8</rdf:li></rdf:Seq></stCamera:AlternateLensNames>
      <stCamera:PerspectiveModel>
       <rdf:Description stCamera:Version=""2"" stCamera:RadialDistortParam1=""0.017806"">
       <stCamera:VignetteModel>
        <rdf:Description stCamera:ResidualMeanError=""0.008557""
         stCamera:VignetteModelParam1=""{A1}"" stCamera:VignetteModelParam2=""{A2}"" stCamera:VignetteModelParam3=""{A3}"">
        <stCamera:VignetteModelPiecewiseParam><rdf:Seq>
          <rdf:li>0.000000, 1.000000</rdf:li><rdf:li>0.147989, 0.941000</rdf:li>
          <rdf:li>0.355496, 0.828000</rdf:li><rdf:li>0.709416, 0.603000</rdf:li>
        </rdf:Seq></stCamera:VignetteModelPiecewiseParam>
        </rdf:Description>
       </stCamera:VignetteModel>
       </rdf:Description>
      </stCamera:PerspectiveModel>
      </rdf:Description>
     </rdf:li>
     <rdf:li>
      <rdf:Description stCamera:Make=""{make}"" stCamera:CameraRawProfile=""True"" stCamera:LensID=""122""
       stCamera:Lens=""{lens}"" stCamera:CameraPrettyName=""{make}"" stCamera:LensPrettyName=""{lens}""
       stCamera:SensorFormatFactor=""1.5"" stCamera:FocalLength=""20"" stCamera:FocusDistance=""10000"" stCamera:ApertureValue=""4"">
      <stCamera:PerspectiveModel>
       <rdf:Description stCamera:Version=""2"">
       <stCamera:VignetteModel stCamera:VignetteModelParam1=""-0.289055"" stCamera:VignetteModelParam2=""-0.962432"" stCamera:VignetteModelParam3=""0.750297""/>
       </rdf:Description>
      </stCamera:PerspectiveModel>
      </rdf:Description>
     </rdf:li>
     <rdf:li>
      <rdf:Description stCamera:Make=""{make}"" stCamera:CameraRawProfile=""True""
       stCamera:Lens=""{lens}"" stCamera:CameraPrettyName=""{make}"" stCamera:LensPrettyName=""{lens}""
       stCamera:SensorFormatFactor=""1.5"" stCamera:FocalLength=""18"" stCamera:FocusDistance=""10000"" stCamera:ApertureValue=""2.970854"">
      <stCamera:PerspectiveModel>
       <rdf:Description stCamera:Version=""2"">
       <stCamera:VignetteModel stCamera:FocalLengthX=""0.899246"" stCamera:FocalLengthY=""0.899246"" stCamera:ImageXCenter=""0.493778"" stCamera:ImageYCenter=""0.5""
        stCamera:VignetteModelParam1=""-0.517792"" stCamera:VignetteModelParam2=""-2.080087"" stCamera:VignetteModelParam3=""2.465329""/>
       </rdf:Description>
      </stCamera:PerspectiveModel>
      </rdf:Description>
     </rdf:li>
    </rdf:Seq>
   </photoshop:CameraProfiles>
  </rdf:Description>
 </rdf:RDF>
</x:xmpmeta>";

    internal const string LensfunXml = @"<!DOCTYPE lensdatabase SYSTEM ""lensfun-database.dtd"">
<lensdatabase version=""2"">
    <camera>
        <maker>Nikon Corporation</maker>
        <maker lang=""en"">Nikon</maker>
        <model>Nikon D3300</model>
        <model lang=""en"">D3300</model>
        <mount>Nikon F AF</mount>
        <cropfactor>1.523</cropfactor>
    </camera>
    <lens>
        <maker>Tokina</maker>
        <model>Tokina AT-X 11-20 F2.8 PRO DX Aspherical 11-20mm f/2.8</model>
        <model lang=""en"">Tokina AT-X 11-20 F2.8 PRO DX</model>
        <mount>Canon EF</mount>
        <mount>Nikon F AF</mount>
        <cropfactor>1.61</cropfactor>
        <calibration>
            <distortion model=""ptlens"" focal=""20"" a=""0.0023467"" b=""-0.011435"" c=""0.0156342""/>
            <vignetting model=""pa"" focal=""18"" aperture=""2.8"" distance=""10"" k1=""-0.6246"" k2=""0.2896"" k3=""-0.1245""/>
            <vignetting model=""pa"" focal=""20"" aperture=""2.8"" distance=""10"" k1=""-0.6195"" k2=""0.3276"" k3=""-0.1494""/>
            <vignetting model=""pa"" focal=""20"" aperture=""2.8"" distance=""1000"" k1=""-0.6195"" k2=""0.3276"" k3=""-0.1494""/>
            <vignetting model=""pa"" focal=""20"" aperture=""4"" distance=""10"" k1=""-0.0500"" k2=""-0.8000"" k3=""0.4500""/>
            <vignetting model=""pa"" focal=""20"" aperture=""4"" distance=""1000"" k1=""-0.0338"" k2=""-0.8254"" k3=""0.4699""/>
        </calibration>
    </lens>
    <lens>
        <maker>Samyang</maker>
        <model>Samyang 135mm f/2.0 ED UMC</model>
        <mount>Nikon F AI</mount>
        <cropfactor>1.0</cropfactor>
        <calibration>
            <distortion model=""ptlens"" focal=""135"" a=""0"" b=""0"" c=""0""/>
        </calibration>
    </lens>
</lensdatabase>";

    /// <summary>
    /// The same Tokina, calibrated a second time on a full-frame body: same maker, model and
    /// mount as the entry in <see cref="LensfunXml"/>, cropfactor 1, deeper coefficients (a DX
    /// lens on a 36x24 sensor). Two calibrations of one lens are two catalog entries; before
    /// the crop entered <c>LensChoice</c> the second one was silently unreachable.
    /// </summary>
    internal const string LensfunTokinaFullFrameXml = @"<!DOCTYPE lensdatabase SYSTEM ""lensfun-database.dtd"">
<lensdatabase version=""2"">
    <lens>
        <maker>Tokina</maker>
        <model>Tokina AT-X 11-20 F2.8 PRO DX Aspherical 11-20mm f/2.8</model>
        <model lang=""en"">Tokina AT-X 11-20 F2.8 PRO DX</model>
        <mount>Canon EF</mount>
        <mount>Nikon F AF</mount>
        <cropfactor>1</cropfactor>
        <calibration>
            <vignetting model=""pa"" focal=""20"" aperture=""2.8"" distance=""1000"" k1=""-0.9000"" k2=""0.1000"" k3=""-0.0500""/>
            <vignetting model=""pa"" focal=""20"" aperture=""4"" distance=""1000"" k1=""-0.5000"" k2=""0.0500"" k3=""-0.0200""/>
        </calibration>
    </lens>
</lensdatabase>";

    /// <summary>
    /// The real pair from the shipped database (slr-nikon.xml, 2026-09-18): the AF-S DX Nikkor
    /// 35 f/1.8G calibrated once on an unnamed APS-C body ("average crop of Nikon APS-C
    /// cameras", 1.528) and once on a Nikon D780 (crop 1). At the DX corner and f/2.8 the
    /// first reads 0.19 stops and the second 0.51, beside Adobe's 0.61 on a D90.
    /// </summary>
    internal const string LensfunNikkor35Xml = @"<!DOCTYPE lensdatabase SYSTEM ""lensfun-database.dtd"">
<lensdatabase version=""2"">
    <camera>
        <maker>Nikon Corporation</maker>
        <maker lang=""en"">Nikon</maker>
        <model>Nikon D3300</model>
        <model lang=""en"">D3300</model>
        <mount>Nikon F AF</mount>
        <cropfactor>1.523</cropfactor>
    </camera>
    <lens>
        <maker>Nikon</maker>
        <model>Nikon AF-S DX Nikkor 35mm f/1.8G</model>
        <model lang=""en"">Nikkor AF-S 35mm f/1.8G DX</model>
        <mount>Nikon F AF</mount>
        <!-- Average crop factor of Nikon APS-C cameras -->
        <cropfactor>1.528</cropfactor>
        <calibration>
            <vignetting model=""pa"" focal=""35"" aperture=""1.8"" distance=""10"" k1=""-0.3298"" k2=""-0.0595"" k3=""0.0938""/>
            <vignetting model=""pa"" focal=""35"" aperture=""1.8"" distance=""1000"" k1=""-0.3298"" k2=""-0.0595"" k3=""0.0938""/>
            <vignetting model=""pa"" focal=""35"" aperture=""2.8"" distance=""10"" k1=""-0.1367"" k2=""0.0656"" k3=""-0.0502""/>
            <vignetting model=""pa"" focal=""35"" aperture=""2.8"" distance=""1000"" k1=""-0.1367"" k2=""0.0656"" k3=""-0.0502""/>
            <vignetting model=""pa"" focal=""35"" aperture=""4"" distance=""10"" k1=""-0.1170"" k2=""0.0027"" k3=""0.0072""/>
            <vignetting model=""pa"" focal=""35"" aperture=""4"" distance=""1000"" k1=""-0.1170"" k2=""0.0027"" k3=""0.0072""/>
        </calibration>
    </lens>
    <lens>
        <maker>Nikon</maker>
        <model>Nikon AF-S DX Nikkor 35mm f/1.8G</model>
        <model lang=""en"">Nikkor AF-S 35mm f/1.8G DX</model>
        <mount>Nikon F AF</mount>
        <cropfactor>1</cropfactor>
        <calibration>
            <!-- Taken with Nikon D780 -->
            <vignetting model=""pa"" focal=""35"" aperture=""1.8"" distance=""10"" k1=""-1.7870"" k2=""1.4093"" k3=""-0.5914""/>
            <vignetting model=""pa"" focal=""35"" aperture=""1.8"" distance=""1000"" k1=""-1.7870"" k2=""1.4093"" k3=""-0.5914""/>
            <vignetting model=""pa"" focal=""35"" aperture=""2.8"" distance=""10"" k1=""-0.1581"" k2=""-1.6132"" k3=""0.8860""/>
            <vignetting model=""pa"" focal=""35"" aperture=""2.8"" distance=""1000"" k1=""-0.1581"" k2=""-1.6132"" k3=""0.8860""/>
            <vignetting model=""pa"" focal=""35"" aperture=""4"" distance=""10"" k1=""0.1858"" k2=""-2.0061"" k3=""0.9425""/>
            <vignetting model=""pa"" focal=""35"" aperture=""4"" distance=""1000"" k1=""0.1858"" k2=""-2.0061"" k3=""0.9425""/>
        </calibration>
    </lens>
</lensdatabase>";

    /// <summary>
    /// One Adobe profile of one lens, with the identity tags as the installed files carry them
    /// (measured 2026-10-26 on the files for the four raw.pixls.us samples, R-108):
    /// <paramref name="lensTag"/> is <c>stCamera:Lens</c>, the camera's own LensModel string;
    /// <paramref name="prettyName"/> is <c>stCamera:LensPrettyName</c>; <paramref name="lensId"/>
    /// is <c>stCamera:LensID</c> as written - a number, Pentax's two numbers, or null for none, as
    /// on every Sony E file.
    /// </summary>
    internal static string LcpOneProfile(string make, string prettyName, string lensTag, string? lensId, string sff, string focal, string? camera = null)
    {
        string id = lensId == null ? "" : $@" stCamera:LensID=""{lensId}""";
        return $@"<x:xmpmeta xmlns:x=""adobe:ns:meta/"">
 <rdf:RDF xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#"">
  <rdf:Description rdf:about="""" xmlns:photoshop=""http://ns.adobe.com/photoshop/1.0/"" xmlns:stCamera=""http://ns.adobe.com/photoshop/1.0/camera-profile"">
   <photoshop:CameraProfiles>
    <rdf:Seq>
     <rdf:li>
      <rdf:Description stCamera:Make=""{make}"" stCamera:CameraRawProfile=""True""{id}
       stCamera:Lens=""{lensTag}"" stCamera:CameraPrettyName=""{camera ?? make}"" stCamera:LensPrettyName=""{prettyName}""
       stCamera:SensorFormatFactor=""{sff}"" stCamera:FocalLength=""{focal}"" stCamera:FocusDistance=""10000"" stCamera:ApertureValue=""4"">
      <stCamera:PerspectiveModel>
       <rdf:Description stCamera:Version=""2"">
       <stCamera:VignetteModel stCamera:VignetteModelParam1=""-0.289055"" stCamera:VignetteModelParam2=""-0.962432"" stCamera:VignetteModelParam3=""0.750297""/>
       </rdf:Description>
      </stCamera:PerspectiveModel>
      </rdf:Description>
     </rdf:li>
    </rdf:Seq>
   </photoshop:CameraProfiles>
  </rdf:Description>
 </rdf:RDF>
</x:xmpmeta>";
    }

    /// <summary>The Pentax lens of the K-70 sample, as Adobe names it (the lensfun twin spells it without the brackets).</summary>
    internal const string PentaxDa18135 = "smc PENTAX-DA 18-135mm F3.5-5.6 ED AL[IF] DC WR";

    /// <summary>
    /// The other mounts of the four raw.pixls.us samples (R-018, R-108): the cameras with their
    /// crops, the Canon EF 50 f/1.4 USM, the Sony FE 16-35 GM beside its A-mount twin of the same
    /// range (the name must tell them apart, the range cannot), and the Pentax DA 18-135 spelled
    /// as lensfun spells it, so only a letters-and-digits comparison equates it with Adobe's.
    /// </summary>
    internal const string LensfunOtherMountsXml = @"<!DOCTYPE lensdatabase SYSTEM ""lensfun-database.dtd"">
<lensdatabase version=""2"">
    <camera>
        <maker>Canon</maker>
        <model>Canon EOS 7D Mark II</model>
        <mount>Canon EF</mount>
        <cropfactor>1.6</cropfactor>
    </camera>
    <camera>
        <maker>Sony</maker>
        <model>Sony ILCE-7M3</model>
        <mount>Sony E</mount>
        <cropfactor>1.0</cropfactor>
    </camera>
    <camera>
        <maker>Pentax</maker>
        <model>Pentax K-70</model>
        <mount>Pentax KAF3</mount>
        <cropfactor>1.534</cropfactor>
    </camera>
    <lens>
        <maker>Canon</maker>
        <model>Canon EF 50mm f/1.4 USM</model>
        <mount>Canon EF</mount>
        <cropfactor>1.0</cropfactor>
        <calibration>
            <vignetting model=""pa"" focal=""50"" aperture=""1.4"" distance=""1000"" k1=""-0.9000"" k2=""0.1000"" k3=""-0.0500""/>
            <vignetting model=""pa"" focal=""50"" aperture=""4"" distance=""1000"" k1=""-0.2000"" k2=""0.0500"" k3=""-0.0200""/>
        </calibration>
    </lens>
    <camera>
        <maker>Canon</maker>
        <model>Canon EOS R</model>
        <mount>Canon RF</mount>
        <cropfactor>1.0</cropfactor>
    </camera>
    <lens>
        <maker>Canon</maker>
        <model>Canon RF 28mm F2.8 STM</model>
        <mount>Canon RF</mount>
        <cropfactor>1.0</cropfactor>
        <calibration>
            <vignetting model=""pa"" focal=""28"" aperture=""2.8"" distance=""1000"" k1=""-1.0000"" k2=""0.2000"" k3=""-0.0800""/>
        </calibration>
    </lens>
    <lens>
        <maker>Sony</maker>
        <model>FE 16-35mm f/2.8 GM</model>
        <mount>Sony E</mount>
        <cropfactor>1.0</cropfactor>
        <calibration>
            <vignetting model=""pa"" focal=""16"" aperture=""2.8"" distance=""1000"" k1=""-1.2000"" k2=""0.3000"" k3=""-0.1000""/>
            <vignetting model=""pa"" focal=""35"" aperture=""4"" distance=""1000"" k1=""-0.4000"" k2=""0.0500"" k3=""-0.0200""/>
        </calibration>
    </lens>
    <lens>
        <maker>Sony</maker>
        <model>Sony 16-35mm f/2.8 ZA SSM</model>
        <mount>Sony Alpha</mount>
        <cropfactor>1.0</cropfactor>
        <calibration>
            <vignetting model=""pa"" focal=""16"" aperture=""2.8"" distance=""1000"" k1=""-1.1000"" k2=""0.2000"" k3=""-0.0800""/>
        </calibration>
    </lens>
    <lens>
        <maker>Pentax</maker>
        <model>smc Pentax-DA 18-135mm f/3.5-5.6 ED AL IF DC WR</model>
        <mount>Pentax KAF3</mount>
        <cropfactor>1.5</cropfactor>
        <calibration>
            <vignetting model=""pa"" focal=""18"" aperture=""3.5"" distance=""1000"" k1=""-0.7000"" k2=""0.2000"" k3=""-0.0600""/>
            <vignetting model=""pa"" focal=""18"" aperture=""5.6"" distance=""1000"" k1=""-0.3000"" k2=""0.0500"" k3=""-0.0100""/>
        </calibration>
    </lens>
</lensdatabase>";

    /// <summary>
    /// The mount definitions and the lenses of the mount test (R-116): lensfun's own
    /// <c>Nikon F AF</c> and <c>Nikon Z</c> entries (slr-nikon.xml, mil-nikon.xml; the F body's
    /// compat list names the older F mounts and the adapter mounts, never Z, while the Z body's
    /// names every F mount), a Tokina of the D3300 raw's range on <c>Nikon Z</c>, which the
    /// camera's first word lets through ("Nikon Z Tokina" contains "NIKON"), and one on
    /// <c>Nikon F AI</c>, which only the compat list admits.
    /// </summary>
    internal const string LensfunMountsXml = @"<!DOCTYPE lensdatabase SYSTEM ""lensfun-database.dtd"">
<lensdatabase version=""2"">
    <mount>
        <name>Nikon F AF</name>
        <compat>Nikon F</compat>
        <compat>Nikon F AI</compat>
        <compat>Nikon F AI-S</compat>
        <compat>M42</compat>
        <compat>M39/1</compat>
        <compat>DKL</compat>
        <compat>T2</compat>
        <compat>Generic</compat>
    </mount>
    <mount>
        <name>Nikon Z</name>
        <compat>Nikon F</compat>
        <compat>Nikon F AI</compat>
        <compat>Nikon F AI-S</compat>
        <compat>Nikon F AF</compat>
        <compat>M42</compat>
        <compat>M39/1</compat>
        <compat>DKL</compat>
        <compat>T2</compat>
        <compat>Generic</compat>
    </mount>
    <camera>
        <maker>Nikon Corporation</maker>
        <maker lang=""en"">Nikon</maker>
        <model>Nikon Z 7</model>
        <model lang=""en"">Z 7</model>
        <mount>Nikon Z</mount>
        <cropfactor>1.0</cropfactor>
    </camera>
    <lens>
        <maker>Tokina</maker>
        <model>Tokina atx-m 11-20mm f/2.8 Z</model>
        <mount>Nikon Z</mount>
        <cropfactor>1.5</cropfactor>
        <calibration>
            <vignetting model=""pa"" focal=""11"" aperture=""2.8"" distance=""1000"" k1=""-0.9000"" k2=""0.3000"" k3=""-0.1000""/>
        </calibration>
    </lens>
    <lens>
        <maker>Tokina</maker>
        <model>Tokina AT-X 11-20mm f/2.8 AI</model>
        <mount>Nikon F AI</mount>
        <cropfactor>1.5</cropfactor>
        <calibration>
            <vignetting model=""pa"" focal=""11"" aperture=""2.8"" distance=""1000"" k1=""-0.7000"" k2=""0.2000"" k3=""-0.0800""/>
        </calibration>
    </lens>
</lensdatabase>";

    /// <summary>
    /// A profiles folder as --profiles expects it: an .lcp anywhere in it, and lensfun/ beneath.
    /// With <paramref name="tokinaOnTwoFormats"/> the lensfun half also carries the Tokina's
    /// full-frame calibration, so the same lens is listed twice, once per calibration format.
    /// With <paramref name="mounts"/> it also carries <see cref="LensfunMountsXml"/>: the Nikon
    /// mount definitions and two Tokinas of the raw's range on mounts the D3300 does not take.
    /// </summary>
    internal static string ProfilesDir(TempWorkspace ws, bool tokinaOnTwoFormats = false, bool otherMounts = false, bool mounts = false)
    {
        string dir = Path.Combine(ws.Dir, "profiles");
        Directory.CreateDirectory(Path.Combine(dir, "Tokina"));
        Directory.CreateDirectory(Path.Combine(dir, "lensfun"));
        File.WriteAllText(Path.Combine(dir, "Tokina", "NIKON CORPORATION (Tokina AT-X 11-20mm F2.8 PRO DX) - RAW.lcp"), Lcp());
        File.WriteAllText(Path.Combine(dir, "Tokina", "Canon (Tokina AT-X 11-20mm F2.8 PRO DX) - RAW.lcp"), Lcp(make: "Canon"));
        File.WriteAllText(Path.Combine(dir, "lensfun", "slr-tokina.xml"), LensfunXml);
        if (tokinaOnTwoFormats)
            File.WriteAllText(Path.Combine(dir, "lensfun", "slr-tokina-ff.xml"), LensfunTokinaFullFrameXml);
        if (otherMounts)
        {
            // The file names follow Adobe's "<Camera> (<Lens>) - RAW.lcp"; the tags inside are the
            // measured ones (docs/plans/2026-10-26-identify-the-lens-on-every-mount.md).
            Directory.CreateDirectory(Path.Combine(dir, "Canon"));
            Directory.CreateDirectory(Path.Combine(dir, "Sony"));
            Directory.CreateDirectory(Path.Combine(dir, "Pentax"));
            File.WriteAllText(Path.Combine(dir, "Canon", "Canon EOS-1Ds Mark III (Canon EF 50mm f1.4 USM) - RAW.lcp"),
                LcpOneProfile("Canon", "Canon EF 50mm f/1.4 USM", "EF50mm f/1.4 USM", "198", "0.995542", "50", camera: "Canon EOS-1Ds Mark III"));
            // Canon's lens type 198 is also what the Zeiss ZE lenses report; Adobe's files for them
            // carry 198 in AlternateLensIDs, and their names ("Milvus 1.4_25 ZE") give no range
            File.WriteAllText(Path.Combine(dir, "Canon", "Canon (Zeiss Milvus 1.4_25 ZE) - RAW.lcp"),
                LcpOneProfile("Canon", "Zeiss Milvus 1.4_25 ZE", "Milvus 1.4/25 ZE", "198", "1", "25", camera: "Canon"));
            // Every RF lens carries 61182 (11300 profiles on the owner's machine, 2026-10-26); the
            // names are the camera's LensModel strings, as on the EF files
            File.WriteAllText(Path.Combine(dir, "Canon", "Canon (Canon RF 28mm F2.8 STM) - RAW.lcp"),
                LcpOneProfile("Canon", "Canon RF 28mm F2.8 STM", "RF28mm F2.8 STM", "61182", "1", "28", camera: "Canon EOS R5"));
            File.WriteAllText(Path.Combine(dir, "Canon", "Canon (Canon RF 50mm F1.8 STM) - RAW.lcp"),
                LcpOneProfile("Canon", "Canon RF 50mm F1.8 STM", "RF50mm F1.8 STM", "61182", "1", "50", camera: "Canon EOS R5"));
            File.WriteAllText(Path.Combine(dir, "Sony", "SONY (Sony FE 16-35mm F2.8 GM) - RAW.lcp"),
                LcpOneProfile("SONY", "Sony FE 16-35mm F2.8 GM", "FE 16-35mm F2.8 GM", null, "1", "16", camera: "Sony"));
            File.WriteAllText(Path.Combine(dir, "Sony", "SONY (Sony 16-35mm F2.8 ZA SSM) - RAW.lcp"),
                LcpOneProfile("SONY", "Sony 16-35mm F2.8 ZA SSM", "16-35mm F2.8 ZA SSM", "54", "1", "16", camera: "Sony"));
            File.WriteAllText(Path.Combine(dir, "Pentax", $"PENTAX ({PentaxDa18135}) - RAW.lcp"),
                LcpOneProfile("PENTAX", PentaxDa18135, PentaxDa18135, "8 215", "1.5", "18"));
            File.WriteAllText(Path.Combine(dir, "lensfun", "other-mounts.xml"), LensfunOtherMountsXml);
        }
        if (mounts)
            File.WriteAllText(Path.Combine(dir, "lensfun", "slr-mounts.xml"), LensfunMountsXml);
        File.WriteAllText(Path.Combine(dir, "lensfun", "LENSFUN-VERSION.txt"), "lensfun database snapshot\nupstream commit: test\n");
        return dir;
    }

    internal static ProfileSources Sources(TempWorkspace ws, bool tokinaOnTwoFormats = false, bool otherMounts = false, bool mounts = false)
    {
        string dir = ProfilesDir(ws, tokinaOnTwoFormats, otherMounts, mounts);
        return ProfileLocator.Locate(new[] { dir }, Path.Combine(dir, "lensfun"));
    }
}
