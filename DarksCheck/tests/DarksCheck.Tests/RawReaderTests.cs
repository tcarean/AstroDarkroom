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

using System.Reflection;
using DarksCheck.Core;

namespace DarksCheck.Tests;

/// <summary>
/// Reading camera raws is the one place the tool leans on someone else's code, so what is
/// pinned here is the seam (R-007): which paths are routed to libraw, that a FITS never is,
/// that the assembly stays lazily loaded. A raw cannot live in the repo (the smallest real NEF
/// is 18 MB), so the decode itself is checked only when DARKSCHECK_NEF points at one.
/// </summary>
public class RawReaderTests
{
    private static readonly RawReader Reader = new();

    [Theory]
    [InlineData("light.NEF")]
    [InlineData("light.nef")]
    [InlineData("a/b/DSC_0060.Nef")]
    [InlineData("x.cr2")]
    [InlineData("x.cr3")]
    [InlineData("x.arw")]
    [InlineData("x.dng")]
    [InlineData("x.raf")]
    [InlineData("x.pef")]                                  // Pentax, taken from FlatCheck 2026-10-26 (ADR 0023)
    [InlineData("x.PEF")]
    public void R008_RecognisesTheRawExtensions(string path) => Assert.True(Reader.Handles(path));

    [Theory]
    [InlineData("light_00001.fit")]
    [InlineData("x.fits")]
    [InlineData("x.fts")]
    [InlineData("noextension")]
    [InlineData("")]
    [InlineData(null)]
    public void R008_DoesNotClaimAnythingThatIsNotARaw(string? path) => Assert.False(Reader.Handles(path!));

    [Fact]
    public void AMissingRawFailsWithTheFileNameNotALibrawErrorCode()
    {
        string path = Path.Combine(Path.GetTempPath(), "darkscheck-nu-exista.NEF");
        FileNotFoundException e = Assert.Throws<FileNotFoundException>(() => Reader.Load(path));
        Assert.Contains("darkscheck-nu-exista.NEF", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AFileThatIsNotActuallyARawIsRefused()
    {
        using TempWorkspace ws = new();
        string path = ws.WriteFile("minte.NEF", new byte[4096]);
        Assert.ThrowsAny<Exception>(() => Reader.Load(path));
        Assert.ThrowsAny<Exception>(() => Reader.LoadInfo(path));
    }

    /// <summary>
    /// The end-to-end proof, on a real file: DARKSCHECK_NEF=E:/.../DSC_0001.NEF. Does nothing
    /// without the variable. The mosaic is undebayered, the scale exact, the CFA order read,
    /// and the shot metadata filled; the metadata-only load agrees with the full one.
    /// </summary>
    [Fact]
    public void R008_ARealRawDecodesToAnUndebayeredMosaicWithItsScaleAndMetadata()
    {
        string? path = Environment.GetEnvironmentVariable("DARKSCHECK_NEF");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        LoadedFrame f = ImageLoader.Load(path);
        Assert.True(f.Image.H > 1000 && f.Image.W > 1000, $"{f.Image.W}x{f.Image.H}");
        Assert.Equal("libraw (exact)", f.Scale.Source);
        Assert.True(f.Scale.White > f.Scale.Black, $"white {f.Scale.White} black {f.Scale.Black}");
        Assert.NotNull(f.Header);
        Assert.Empty(f.Header);
        Assert.NotNull(f.Cfa);
        Assert.Equal("libraw", f.Cfa.Source);
        Assert.Equal(2, f.Cfa.Letters.Count(c => c == 'G'));

        // Undebayered: the four Bayer positions do not agree on a colour image.
        Img[] positions = Mosaic.Split(f.Image);
        double[] medians = positions.Select(p => Stats.Percentile(p.D, 50)).ToArray();
        Assert.True(medians.Max() / Math.Max(medians.Min(), 1) > 1.05, "the four positions agree: the frame was demosaiced");

        Assert.True(f.ExposureSeconds > 0);
        Assert.True(f.Iso > 0);
        Assert.NotNull(f.Timestamp);
        Assert.NotNull(f.Camera);

        LoadedFrame info = ImageLoader.LoadInfo(path);
        Assert.Null(info.Image);
        Assert.Equal(f.ExposureSeconds, info.ExposureSeconds);
        Assert.Equal(f.Iso, info.Iso);
        Assert.Equal(f.Timestamp, info.Timestamp);
        Assert.Equal(f.Camera, info.Camera);
        Assert.Equal(f.FocalMm, info.FocalMm);
        Assert.Equal(f.FNumber, info.FNumber);
        // the gated file is a D3300 NEF: the mount is Nikon's F, read through MountOf (the lens's, else the camera's);
        // the ID may be null on a telescope, so it is not pinned
        Assert.Equal("Nikon F", f.LensMount);
        Assert.Equal(f.LensId, info.LensId);
        Assert.Equal(f.LensMount, info.LensMount);
        Assert.Equal(f.LensName, info.LensName);
        Assert.Equal(f.LensMinFocalMm, info.LensMinFocalMm);
        Assert.Equal(f.LensMaxFocalMm, info.LensMaxFocalMm);
        Assert.Equal(f.LensMaxAperture, info.LensMaxAperture);
    }

    // The four rules below are FlatCheck's hunk of 2026-10-26 (its R-108, ADR 0023), pinned on
    // the values libraw 0.21.1 gave FlatCheck on a D3300 and on the raw.pixls.us samples of its
    // docs/validation.md (V-041 to V-048); no raw is needed.

    [Fact]
    public void R008_TheMountIsTheLenssAndTheCamerasOnlyWhenTheLenssIsUnknown()
    {
        Assert.Equal(6, RawReader.MountOf(6, 5));                 // the R5 Mark II read by an older libraw
        Assert.Equal(5, RawReader.MountOf(5, 5));
        Assert.Equal(5, RawReader.MountOf(0, 5));
        Assert.Equal(0, RawReader.MountOf(0, 0));
    }

    [Fact]
    public void R008_TheLensIdComesFromNikonsFieldOnANikonAndFromTheMakerNoteElsewhere()
    {
        const ulong signature = 5356031691206446338UL;      // what MakerNotes.LensID holds on a D3300
        Assert.Equal(74L, RawReader.LensIdOf(74, signature, 27));
        Assert.Equal(139L, RawReader.LensIdOf(139, signature, 27));  // the Tokina 11-20 on the D3300
        Assert.Null(RawReader.LensIdOf(0, signature, 27));   // a Nikon on a telescope: the signature is no ID
        Assert.Null(RawReader.LensIdOf(0, 139, 28));         // Nikon Z: the same field, the same rule
        Assert.Equal(198L, RawReader.LensIdOf(0, 198, 5));   // Canon EF
        Assert.Equal(155L, RawReader.LensIdOf(0, 155, 5));
        Assert.Equal(32831L, RawReader.LensIdOf(0, 32831, 40));  // Sony E
        Assert.Equal(2263L, RawReader.LensIdOf(0, 2263, 33));    // Pentax K
        Assert.Null(RawReader.LensIdOf(0, 155, 0));          // an unknown mount: nothing is guessed
        Assert.Null(RawReader.LensIdOf(0, 0, 5));
        Assert.Null(RawReader.LensIdOf(0, 1UL << 40, 5));    // does not fit 31 bits: not an ID
    }

    [Theory]
    [InlineData(27, "Nikon F")]
    [InlineData(28, "Nikon Z")]
    [InlineData(5, "Canon EF")]
    [InlineData(4, "Canon EF-S")]
    [InlineData(6, "Canon RF")]
    [InlineData(40, "Sony E")]
    [InlineData(25, "Sony A")]
    [InlineData(33, "Pentax K")]
    [InlineData(13, "Fujifilm X")]
    [InlineData(10, "Micro 4/3")]
    [InlineData(16, "Hasselblad XCD")]
    [InlineData(2, null)]                                          // "C", a mount the table does not spell: no programming name leaks
    [InlineData(45, null)]                                         // libraw's sentinel
    [InlineData(999, null)]
    [InlineData(0, null)]
    public void R008_TheMountIsNamedAsLibrawNamesIt(int code, string? label)
        => Assert.Equal(label, RawReader.MountLabel(code));

    [Fact]
    public void R008_TheRangeFallsBackToTheTopFields_AndToMaxApertureOnlyAtTheShortEnd()
    {
        // Canon prime: the MakerNote's MaxAperture4MinFocal is 0, MaxAperture 1.414
        Assert.Equal<(double?, double?, double?)>((50.0, 50.0, 1.4142135), RawReader.RangeOf(50, 50, 0, 1.4142135, 50, 50, 0, 50));
        // Canon zoom shot at the long end: MaxAperture is the aperture THERE, not the name's; not taken
        Assert.Equal<(double?, double?, double?)>((18.0, 135.0, null), RawReader.RangeOf(18, 135, 0, 5.6, 18, 135, 0, 135));
        // the same zoom at its short end: the two apertures coincide, so it is taken
        Assert.Equal<(double?, double?, double?)>((18.0, 135.0, 3.5), RawReader.RangeOf(18, 135, 0, 3.5, 18, 135, 0, 18));
        // a constant-aperture zoom at the long end is still not taken: nothing says it is constant
        Assert.Equal<(double?, double?, double?)>((70.0, 200.0, null), RawReader.RangeOf(70, 200, 0, 2.828427, 70, 200, 0, 200));
        // Nikon: the MakerNote alone, the top fields 0 (the D3300 with the Tokina 11-20 at 20 mm)
        Assert.Equal<(double?, double?, double?)>((11.0, 20.0, 2.8), RawReader.RangeOf(11, 20, 2.8, 0, 0, 0, 0, 20));
        // Sony: both filled, the MakerNote first
        Assert.Equal<(double?, double?, double?)>((16.0, 35.0, 2.8), RawReader.RangeOf(16, 35, 2.8, 0, 16, 35, 2.8, 32));
        // Pentax: nothing anywhere
        Assert.Equal<(double?, double?, double?)>((null, null, null), RawReader.RangeOf(0, 0, 0, 0, 0, 0, 0, 18));
        // only the top fields
        Assert.Equal<(double?, double?, double?)>((24.0, 70.0, 2.8), RawReader.RangeOf(0, 0, 0, 0, 24, 70, 2.8, 50));
        // within 1 mm or 5 % the two ends count as one, a prime: the aperture is taken, the ends stay as given
        Assert.Equal<(double?, double?, double?)>((50.0, 51.0, 1.4), RawReader.RangeOf(50, 51, 0, 1.4, 0, 0, 0, 0));
        Assert.Equal<(double?, double?, double?)>((50.0, 60.0, null), RawReader.RangeOf(50, 60, 0, 1.4, 0, 0, 0, 0));
    }

    /// <summary>
    /// A frame fabricated with the values libraw gave FlatCheck for each make reaches the
    /// inventory's record with its three lens fields: a reader that filled them is all
    /// <see cref="FrameInfo.From"/> needs.
    /// </summary>
    [Theory]
    [InlineData("Canon EOS 7D Mark II", 198L, "Canon EF", "EF50mm f/1.4 USM", 50.0, 50.0, 1.4142135)]
    [InlineData("Sony ILCE-7M3", 32831L, "Sony E", "FE 16-35mm F2.8 GM", 16.0, 35.0, 2.8)]
    [InlineData("Pentax K-70", 2263L, "Pentax K", null, null, null, null)]
    [InlineData("Nikon D3300", 139L, "Nikon F", null, 11.0, 20.0, 2.8)]
    [InlineData("Nikon D3300", null, "Nikon F", null, null, null, null)]       // a D3300 on a telescope
    public void R008_AFabricatedFrameOfAnyMakeCarriesItsLensIntoFrameInfo(string camera, long? lensId, string? mount, string? name,
                                                                             double? minFocal, double? maxFocal, double? maxAperture)
    {
        LoadedFrame f = new()
        {
            Header = new Dictionary<string, string>(),
            Camera = camera,
            ExposureSeconds = 120.2,
            Iso = 1600,
            LensId = lensId,
            LensMount = mount,
            LensName = name,
            LensMinFocalMm = minFocal,
            LensMaxFocalMm = maxFocal,
            LensMaxAperture = maxAperture,
        };

        FrameInfo info = FrameInfo.From(3, "darks/x.cr2", f);

        Assert.Equal(3, info.Index);
        Assert.Equal(camera, info.Camera);
        Assert.Equal(lensId, info.LensId);
        Assert.Equal(mount, info.LensMount);
        Assert.Equal(name, info.LensName);
        Assert.Equal(minFocal, info.LensMinFocalMm);
        Assert.Equal(maxFocal, info.LensMaxFocalMm);
        Assert.Equal(maxAperture, info.LensMaxAperture);
        Assert.Equal(1600, info.Iso);
        Assert.Null(info.Error);
    }

    /// <summary>
    /// A FITS run must keep working when Sdcb.LibRaw is absent entirely. That holds only while
    /// no field of the reader is a libraw type: fields are resolved when the type is loaded,
    /// which happens on every run since ImageLoader asks it Handles(); method bodies at JIT
    /// time. Measured in FlatCheck by deleting the DLL (its ADR 0009).
    /// </summary>
    [Fact]
    public void R008_HoldsNoLibrawTypedFields_SoTheAssemblyStaysLazilyLoaded()
    {
        FieldInfo[] fields = typeof(RawReader).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        foreach (FieldInfo f in fields)
        {
            string asm = f.FieldType.Assembly.GetName().Name ?? "";
            Assert.False(asm.StartsWith("Sdcb", StringComparison.Ordinal), $"field {f.Name} is of type {f.FieldType.FullName} from {asm}: that forces libraw to load on FITS runs too");
        }
    }
}
