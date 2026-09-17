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
using FlatCheck;

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

    /// <summary>A profiles folder as --profiles expects it: an .lcp anywhere in it, and lensfun/ beneath.</summary>
    internal static string ProfilesDir(TempWorkspace ws)
    {
        string dir = Path.Combine(ws.Dir, "profiles");
        Directory.CreateDirectory(Path.Combine(dir, "Tokina"));
        Directory.CreateDirectory(Path.Combine(dir, "lensfun"));
        File.WriteAllText(Path.Combine(dir, "Tokina", "NIKON CORPORATION (Tokina AT-X 11-20mm F2.8 PRO DX) - RAW.lcp"), Lcp());
        File.WriteAllText(Path.Combine(dir, "Tokina", "Canon (Tokina AT-X 11-20mm F2.8 PRO DX) - RAW.lcp"), Lcp(make: "Canon"));
        File.WriteAllText(Path.Combine(dir, "lensfun", "slr-tokina.xml"), LensfunXml);
        File.WriteAllText(Path.Combine(dir, "lensfun", "LENSFUN-VERSION.txt"), "lensfun database snapshot\nupstream commit: test\n");
        return dir;
    }

    internal static ProfileSources Sources(TempWorkspace ws)
    {
        string dir = ProfilesDir(ws);
        return ProfileLocator.Locate(new[] { dir }, Path.Combine(dir, "lensfun"));
    }
}
