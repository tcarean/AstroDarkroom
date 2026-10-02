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

namespace AstroPhotoAnalyzer.Tests;

public class FileFactsTests
{
    [Fact]
    public void CopiesWhatTheReaderSaidAndDerivesTheRest()
    {
        var r = SyntheticRasters.Flat(3888, 5938, 0.1f);
        r.Source = new SourceInfo
        {
            Path = "x.png",
            Format = "PNG",
            BitDepth = 16,
            IsFloat = false,
            PlanesInFile = 3,
            Compression = "Deflate",
            Icc = new byte[10],
            IccDescription = "sRGB-elle-V2-srgbtrc.icc",
            FileBytes = 103387067,
            Note = null,
        };

        var f = FileFacts.Of(r);

        Assert.Equal(5938, f.W);
        Assert.Equal(3888, f.H);
        Assert.Equal(23.09, f.Megapixels, 2);
        Assert.Equal(1.527, f.NativeAspect, 3);
        Assert.True(f.IsColor);
        Assert.Equal("PNG", f.Format);
        Assert.Equal(16, f.BitDepth);
        Assert.True(f.HasIcc);
        Assert.True(f.IccLooksSrgb);
        Assert.Equal(98.6, f.FileMegabytes, 1);
    }

    [Theory]
    [InlineData("Adobe RGB (1998)", false)]
    [InlineData("ProPhoto RGB", false)]
    [InlineData("sRGB IEC61966-2.1", true)]
    public void RecognisesSrgbByName(string desc, bool srgb)
    {
        var r = SyntheticRasters.Flat(2, 2, 0.1f);
        r.Source = new SourceInfo { Icc = new byte[1], IccDescription = desc };

        Assert.Equal(srgb, FileFacts.Of(r).IccLooksSrgb);
    }

    [Fact]
    public void WithoutAProfileNothingIsKnownAboutTheColourSpace()
    {
        var r = SyntheticRasters.Flat(2, 2, 0.1f);
        r.Source = new SourceInfo { Format = "TIFF" };

        var f = FileFacts.Of(r);
        Assert.False(f.HasIcc);
        Assert.Null(f.IccLooksSrgb);
    }

    [Fact]
    public void AnInMemoryRasterWithoutASourceStillHasFacts()
    {
        var f = FileFacts.Of(SyntheticRasters.Flat(4, 8, 0.1f, planes: 1));

        Assert.Equal(8, f.W);
        Assert.False(f.IsColor);
        Assert.Equal("memorie", f.Format);
    }
}
