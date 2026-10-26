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

using System.Globalization;
using Xunit.Abstractions;

namespace FlatCheck.Tests;

/// <summary>
/// What libraw says about the lens of the six CC0 raw.pixls.us samples kept as validation data
/// and of two of the owner's D3300 raws
/// (docs/validation.md, V-041 to V-048; the files and their hashes are in testdata/manifest.json,
/// under FLATCHECK_TESTDATA_LARGE). The values were measured on 2026-10-26 with the project's
/// package; <see cref="LensMountTests"/> fabricates frames with the same values, so the gate needs
/// no raw. Not in the gate: trait Category=Validation. A file absent on this machine returns early
/// with a message (xUnit 2 has no dynamic skip).
/// </summary>
[Trait("Category", "Validation")]
public class RawSampleTests
{
    readonly ITestOutputHelper _out;

    public RawSampleTests(ITestOutputHelper output) => _out = output;

    public static IEnumerable<object?[]> Samples => new[]
    {
        new object?[] { "V-041", "Samples/raw.pixls.us/Canon - EOS 7D Mark II - RAW (3-2).cr2", "Canon EOS 7D Mark II", 198L, "Canon EF", "EF50mm f/1.4 USM", 50.0, 50.0, 1.4142135, 50.0, 4.0 },
        new object?[] { "V-042", "Samples/raw.pixls.us/Canon - EOS 6D - RAW.CR2", "Canon EOS 6D", 155L, "Canon EF", "EF85mm f/1.8 USM", 85.0, 85.0, 1.8340081, 85.0, 5.6 },
        new object?[] { "V-043", "Samples/raw.pixls.us/Sony - ILCE-7M3 - 14bit compressed (3-2).ARW", "Sony ILCE-7M3", 32831L, "Sony E", "FE 16-35mm F2.8 GM", 16.0, 35.0, 2.8, 32.0, 4.0 },
        new object?[] { "V-044", "Samples/raw.pixls.us/Pentax - K-70 - 14bit (3-2).PEF", "Pentax K-70", 2263L, "Pentax K", null, null, null, null, 18.0, 5.6 },
        // RF: libraw's ID is Canon's RF lens type, not the 61182 of the Adobe files; the name decides
        // a zoom shot at its long end: libraw's MaxAperture is the aperture there, so no aperture is taken
        new object?[] { "V-045", "Samples/raw.pixls.us/Canon - EOS R6 - 3-2.CR3", "Canon EOS R6", 266L, "Canon RF", "RF70-200mm F2.8 L IS USM", 70.0, 200.0, null, 200.0, 25.0 },
        // a body newer than libraw 0.21.1 (named "EOS R5m2"): CameraMount says Canon EF, LensMount Canon RF
        new object?[] { "V-046", "Samples/raw.pixls.us/Canon - EOS R5 Mark II - CRAW (3-2).CR3", "Canon EOS R5m2", 303L, "Canon RF", "RF135mm F1.8 L IS USM", 135.0, 135.0, 1.8340081, 135.0, 3.2 },
        // the owner's D3300 with a Nikkor with CPU and with the Tokina: no name on either, Nikon's own ID, the MakerNote range
        new object?[] { "V-047", "Nikkor 35mm DX/ISO400/f2.8-sky/flats/DSC_0031.NEF", "Nikon D3300", 159L, "Nikon F", null, 35.0, 35.0, 1.8, 35.0, 2.8 },
        new object?[] { "V-048", "Tokina/ISO400/f2.8-sky-no-difuser/flats/DSC_0001.NEF", "Nikon D3300", 139L, "Nikon F", null, 11.0, 20.0, 2.8, 20.0, 2.8 },
    };

    [Theory]
    [MemberData(nameof(Samples))]
    public void TheSampleRawCarriesTheMeasuredLensFacts(string id, string name, string camera, long lensId, string mount, string? lensName,
                                                        double? minFocal, double? maxFocal, double? maxAperture, double focal, double fNumber)
    {
        string? path = TestData.Large(name);
        if (path == null)
        {
            _out.WriteLine($"NOT MEASURED {id}: {name} is absent under {TestData.LargeDir()}");
            return;
        }

        LoadedFrame f = ImageLoader.LoadInfo(path);
        _out.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "{0}: camera='{1}' lensId={2} mount='{3}' name='{4}' range={5}/{6}/{7} focal={8} f={9}",
            id, f.Camera, f.LensId, f.LensMount, f.LensName, f.LensMinFocalMm, f.LensMaxFocalMm, f.LensMaxAperture, f.FocalMm, f.FNumber));

        Assert.Equal(camera, f.Camera);
        Assert.Equal(lensId, f.LensId);
        Assert.Equal(mount, f.LensMount);
        Assert.Equal(lensName, f.LensName);
        Assert.Equal(minFocal, f.LensMinFocalMm);
        Assert.Equal(maxFocal, f.LensMaxFocalMm);
        if (maxAperture.HasValue) Assert.Equal(maxAperture.Value, f.LensMaxAperture!.Value, 3);
        else Assert.Null(f.LensMaxAperture);
        Assert.Equal(focal, f.FocalMm!.Value, 3);
        Assert.Equal(fNumber, f.FNumber!.Value, 3);
    }
}
